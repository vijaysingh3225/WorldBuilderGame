using System;
using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class TwoSlotWeaponPresenter : MonoBehaviour
    {
        private enum TransitionPhase
        {
            None,
            SheatheWithSword,
            ReturnEmptyHand,
            ReachForSword,
            DrawWithSword
        }

        public const int PrimarySlot = 0;
        public const int SecondarySlot = 1;
        public const string SwordReadyLayerName = "Short Sword Ready";
        public const float BowBraceHeight = 0.24f;
        public const float BowMaximumDrawDistance = 0.42f;

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Transform swordRoot;
        [SerializeField] private Transform backSocket;
        [SerializeField] private Transform bowRoot;
        [SerializeField] private Transform bowBackSocket;
        [SerializeField] private Transform arrowRoot;
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField] private CameraAimTarget aimTarget;
        [SerializeField] private ShortSwordAttackPresenter attackPresenter;
        [SerializeField] private ShortSwordBlockPresenter blockPresenter;
        [SerializeField, Min(0.1f)] private float transitionDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float bowPoseBlendDuration = 0.22f;
        [SerializeField, Range(0.65f, 0.95f)]
        private float wristReleaseStart = 0.80f;

        private Transform rightHand;
        private Transform leftHand;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftShoulder;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightShoulder;
        private Transform rightIndexIntermediate;
        private Transform rightIndexDistal;
        private Transform rightMiddleIntermediate;
        private Transform rightMiddleDistal;
        private Transform upperBowString;
        private Transform lowerBowString;
        private Transform upperChest;
        private Transform head;
        private Transform carryParent;
        private Vector3 carryLocalPosition;
        private Quaternion carryLocalRotation;
        private Vector3 carryLocalScale;
        private Vector3 bowLocalScale;
        private Vector3 handInSwordLocalPosition;
        private Quaternion handInSwordLocalRotation;
        private Vector3 transitionStartHandSheatheLocalPosition;
        private Quaternion transitionStartHandSheatheLocalRotation;
        private Vector3 transitionStartBladeDirectionSheatheLocal;
        private Vector3 transitionStartElbowSheatheLocalPosition;
        private Vector3 sheatheBendDirectionLocal;
        private Quaternion lockedHandLocalRotation;
        private int swordReadyLayerIndex = -1;
        private int activeSlot;
        private int targetSlot;
        private float transitionStartedAt;
        private float transitionProgress;
        private bool transitioning;
        private bool bowEquipped;
        private float bowPoseWeight;
        private TransitionPhase transitionPhase;
        private bool hasStableBowRigPose;
        private Quaternion stableLeftShoulderLocalRotation;
        private Quaternion stableRightShoulderLocalRotation;
        private Quaternion stableLeftHandLocalRotation;
        private Quaternion stableRightHandLocalRotation;
        private Quaternion stableLeftUpperArmLocalRotation;
        private Quaternion stableLeftLowerArmLocalRotation;
        private Quaternion stableRightUpperArmLocalRotation;
        private Quaternion stableRightLowerArmLocalRotation;

        public event Action<int> ActiveSlotChanged;

        public int ActiveSlot => activeSlot;
        public bool IsTransitioning => transitioning;
        public bool SwordIsOnBack =>
            !transitioning &&
            activeSlot == SecondarySlot &&
            swordRoot != null &&
            swordRoot.parent == backSocket;
        public bool BowIsEquipped =>
            !transitioning &&
            activeSlot == SecondarySlot &&
            bowEquipped &&
            bowRoot != null &&
            bowRoot.parent == leftHand;

        public void Configure(
            Animator targetAnimator,
            PlayerInputSource intentSource,
            Transform root,
            Transform equippedSword,
            Transform swordBackSocket,
            Transform bow,
            Transform bowSocket,
            Transform arrow,
            BowWeapon equippedBowWeapon,
            ShortSwordAttackPresenter swordAttackPresenter,
            ShortSwordBlockPresenter swordBlockPresenter)
        {
            Unsubscribe();
            animator = targetAnimator;
            input = intentSource;
            characterRoot = root;
            swordRoot = equippedSword;
            backSocket = swordBackSocket;
            bowRoot = bow;
            bowBackSocket = bowSocket;
            arrowRoot = arrow;
            bowWeapon = equippedBowWeapon;
            attackPresenter = swordAttackPresenter;
            blockPresenter = swordBlockPresenter;
            ResolveRig();
            CaptureCarryTransform();
            CaptureBowTransform();
            StowBow();
            SetSwordAvailability(true);
            SetSwordReadyWeight(1f);
            Subscribe();
        }

        public bool RequestSlot(int slot)
        {
            if (rightHand == null ||
                leftHand == null ||
                rightUpperArm == null ||
                rightLowerArm == null)
            {
                ResolveRig();
            }

            int clampedSlot = Mathf.Clamp(slot, PrimarySlot, SecondarySlot);
            if (transitioning || clampedSlot == activeSlot)
            {
                return false;
            }

            if ((attackPresenter != null && attackPresenter.IsAttacking) ||
                (blockPresenter != null && blockPresenter.IsBlocking))
            {
                return false;
            }

            targetSlot = clampedSlot;
            if (targetSlot == PrimarySlot)
            {
                StowBow();
                bowPoseWeight = 0f;
            }
            transitioning = true;
            transitionProgress = 0f;
            transitionStartedAt = Time.time;
            transitionPhase = targetSlot == SecondarySlot
                ? TransitionPhase.SheatheWithSword
                : TransitionPhase.ReachForSword;
            if (targetSlot == SecondarySlot)
            {
                CaptureHandOffsetInSwordSpace();
                lockedHandLocalRotation = rightHand.localRotation;
                Quaternion sheatheFrameRotation =
                    GetSheatheFrameRotation();
                Vector3 sheatheFrameOrigin =
                    GetSheatheFrameOrigin();
                transitionStartHandSheatheLocalPosition =
                    Quaternion.Inverse(sheatheFrameRotation) *
                    (rightHand.position - sheatheFrameOrigin);
                transitionStartHandSheatheLocalRotation =
                    Quaternion.Inverse(sheatheFrameRotation) *
                    rightHand.rotation;
                transitionStartBladeDirectionSheatheLocal =
                    Quaternion.Inverse(sheatheFrameRotation) *
                    (swordRoot.rotation * Vector3.up);
                transitionStartElbowSheatheLocalPosition =
                    Quaternion.Inverse(sheatheFrameRotation) *
                    (rightLowerArm.position - sheatheFrameOrigin);
            }
            CaptureSheatheBendDirection();
            SetSwordAvailability(false);
            return true;
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            input ??= GetComponentInParent<PlayerInputSource>();
            bowWeapon ??= GetComponent<BowWeapon>();
            aimTarget ??= FindFirstObjectByType<CameraAimTarget>();
            characterRoot ??= input != null ? input.transform : transform.root;
            attackPresenter ??= GetComponent<ShortSwordAttackPresenter>();
            blockPresenter ??= GetComponent<ShortSwordBlockPresenter>();
            swordRoot ??= transform.Find("Equipped Short Sword");
            ResolveRig();
            CaptureCarryTransform();
            CaptureBowTransform();
            StowBow();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (transitioning)
            {
                CompleteTransition();
            }
        }

        private void Update()
        {
            float bowTargetWeight =
                !transitioning &&
                activeSlot == SecondarySlot &&
                bowEquipped
                    ? 1f
                    : 0f;
            bowPoseWeight = Mathf.MoveTowards(
                bowPoseWeight,
                bowTargetWeight,
                Time.deltaTime / Mathf.Max(0.05f, bowPoseBlendDuration));

            if (!transitioning)
            {
                return;
            }

            transitionProgress = Mathf.Clamp01(
                (Time.time - transitionStartedAt) /
                Mathf.Max(0.1f, transitionDuration));
            if (transitionProgress >= 1f)
            {
                AdvanceTransition();
            }
        }

        private void LateUpdate()
        {
            if (!transitioning)
            {
                if (bowEquipped && bowPoseWeight > 0f)
                {
                    ApplyBowIdlePose(bowPoseWeight);
                }
                return;
            }

            float pathProgress =
                transitionPhase == TransitionPhase.SheatheWithSword ||
                transitionPhase == TransitionPhase.ReachForSword
                    ? transitionProgress
                    : 1f - transitionProgress;
            ApplySheatheArmPose(pathProgress);
        }

        private void ApplyBowIdlePose(float weight)
        {
            if (leftUpperArm == null ||
                leftLowerArm == null ||
                leftHand == null ||
                rightUpperArm == null ||
                rightLowerArm == null ||
                rightHand == null)
            {
                return;
            }

            CaptureStableBowRigPose();
            float readyWeight =
                bowWeapon != null ? bowWeapon.ReadyWeight : 0f;
            float drawWeight =
                bowWeapon != null ? bowWeapon.DrawNormalized : 0f;
            float easedReady =
                readyWeight * readyWeight * (3f - 2f * readyWeight);
            StabilizeBowRigPose(easedReady);
            Quaternion animatedFrameRotation = GetSheatheFrameRotation();
            Quaternion stableAimFrameRotation =
                characterRoot != null
                    ? characterRoot.rotation
                    : animatedFrameRotation;
            Quaternion frameRotation = Quaternion.Slerp(
                animatedFrameRotation,
                stableAimFrameRotation,
                easedReady);
            Vector3 right = frameRotation * Vector3.right;
            Vector3 up = frameRotation * Vector3.up;
            Vector3 forward = frameRotation * Vector3.forward;
            Vector3 rootRight =
                characterRoot != null ? characterRoot.right : right;
            Vector3 rootForward =
                characterRoot != null ? characterRoot.forward : forward;
            Vector3 idleArrowDirection =
                (forward * 0.78f + right * 0.06f - up * 0.62f).normalized;
            aimTarget ??= FindFirstObjectByType<CameraAimTarget>();
            Vector3 cameraAimDirection =
                aimTarget != null
                    ? aimTarget.AimDirection
                    : Camera.main != null
                        ? Camera.main.transform.forward
                        : rootForward;
            Vector3 readyArrowDirection =
                cameraAimDirection.sqrMagnitude > 0.0001f
                    ? cameraAimDirection.normalized
                    : rootForward;
            Vector3 arrowDirection = Vector3.Slerp(
                idleArrowDirection,
                readyArrowDirection,
                easedReady).normalized;
            Vector3 idleBowUp = Vector3.ProjectOnPlane(
                right + up * 0.18f,
                idleArrowDirection).normalized;
            Quaternion idleBowRotation =
                Quaternion.LookRotation(
                    idleArrowDirection,
                    idleBowUp);
            Quaternion readyBowRotation =
                Quaternion.LookRotation(
                    readyArrowDirection,
                    up);
            Quaternion bowRotation = Quaternion.Slerp(
                idleBowRotation,
                readyBowRotation,
                easedReady);
            float leftArmLength =
                Vector3.Distance(leftUpperArm.position, leftLowerArm.position) +
                Vector3.Distance(leftLowerArm.position, leftHand.position);
            Vector3 idleLeftReach =
                (forward * 0.72f + right * 0.30f - up * 0.55f).normalized;
            Vector3 readyLeftReach =
                (forward * 0.985f - right * 0.06f + up * 0.16f).normalized;
            Vector3 leftReachDirection = Vector3.Slerp(
                idleLeftReach,
                readyLeftReach,
                easedReady).normalized;
            Vector3 idleLeftWristPosition =
                leftUpperArm.position +
                idleLeftReach * leftArmLength * 0.965f;
            Vector3 idleBowPosition =
                idleLeftWristPosition +
                idleLeftReach * 0.055f -
                up * 0.016f;
            float bowScale =
                bowRoot != null
                    ? Mathf.Abs(bowRoot.lossyScale.z)
                    : 1f;
            Vector3 cheekAnchor =
                head != null
                    ? head.position +
                        rootRight * 0.105f -
                        rootForward * 0.025f -
                        up * 0.075f
                    : rightUpperArm.position +
                        rootRight * 0.08f +
                        up * 0.14f;
            Vector3 readyBowPosition =
                cheekAnchor +
                readyArrowDirection *
                (BowBraceHeight + BowMaximumDrawDistance) *
                bowScale;
            Vector3 bowPosition = Vector3.Lerp(
                idleBowPosition,
                readyBowPosition,
                easedReady);
            float wristVerticalOffset =
                Mathf.Lerp(0.016f, 0.005f, easedReady);
            Vector3 leftWristPosition =
                bowPosition -
                leftReachDirection * 0.055f +
                up * wristVerticalOffset;
            float scaledNockDistance =
                (BowBraceHeight +
                    BowMaximumDrawDistance * drawWeight) *
                bowScale;
            Vector3 nockPosition =
                bowPosition -
                arrowDirection * scaledNockDistance;
            Vector3 rightFingerContact =
                GetRightFingerContactPosition();
            Vector3 rightWristPosition =
                nockPosition -
                (rightFingerContact - rightHand.position);
            Vector3 rightFingerContactLocal =
                rightHand.InverseTransformPoint(rightFingerContact);
            Quaternion leftHandRotation = leftHand.rotation;
            Quaternion rightHandRotation = rightHand.rotation;

            SolveArmPose(
                rightUpperArm,
                rightLowerArm,
                rightHand,
                rightWristPosition,
                rightHandRotation,
                Vector3.Lerp(
                    rightUpperArm.position +
                        rootRight * 0.36f -
                        rootForward * 0.02f -
                        up * 0.17f,
                    rightUpperArm.position +
                        rootRight * 0.36f -
                        rootForward * 0.26f +
                        up * 0.02f,
                    easedReady),
                weight,
                true,
                true,
                rightFingerContactLocal,
                nockPosition,
                rootRight);
            SolveArmPose(
                leftUpperArm,
                leftLowerArm,
                leftHand,
                leftWristPosition,
                leftHandRotation,
                leftUpperArm.position +
                    forward * 0.18f -
                    right * 0.08f -
                    up * 0.24f,
                weight);
            if (bowRoot != null)
            {
                bowRoot.position = Vector3.Lerp(
                    bowRoot.position,
                    bowPosition,
                    weight);
                bowRoot.rotation = Quaternion.Slerp(
                    bowRoot.rotation,
                    bowRotation,
                    weight);
            }

            UpdateBowGeometry(drawWeight);
        }

        private void CaptureStableBowRigPose()
        {
            if (hasStableBowRigPose)
            {
                return;
            }

            stableLeftShoulderLocalRotation =
                leftShoulder != null
                    ? leftShoulder.localRotation
                    : Quaternion.identity;
            stableRightShoulderLocalRotation =
                rightShoulder != null
                    ? rightShoulder.localRotation
                    : Quaternion.identity;
            stableLeftHandLocalRotation = leftHand.localRotation;
            stableRightHandLocalRotation = rightHand.localRotation;
            stableLeftUpperArmLocalRotation =
                leftUpperArm.localRotation;
            stableLeftLowerArmLocalRotation =
                leftLowerArm.localRotation;
            stableRightUpperArmLocalRotation =
                rightUpperArm.localRotation;
            stableRightLowerArmLocalRotation =
                rightLowerArm.localRotation;
            hasStableBowRigPose = true;
        }

        private void StabilizeBowRigPose(float weight)
        {
            if (!hasStableBowRigPose || weight <= 0f)
            {
                return;
            }

            if (leftShoulder != null)
            {
                leftShoulder.localRotation = Quaternion.Slerp(
                    leftShoulder.localRotation,
                    stableLeftShoulderLocalRotation,
                    weight);
            }

            if (rightShoulder != null)
            {
                rightShoulder.localRotation = Quaternion.Slerp(
                    rightShoulder.localRotation,
                    stableRightShoulderLocalRotation,
                    weight);
            }

            leftHand.localRotation = Quaternion.Slerp(
                leftHand.localRotation,
                stableLeftHandLocalRotation,
                weight);
            rightHand.localRotation = Quaternion.Slerp(
                rightHand.localRotation,
                stableRightHandLocalRotation,
                weight);
            leftUpperArm.localRotation = Quaternion.Slerp(
                leftUpperArm.localRotation,
                stableLeftUpperArmLocalRotation,
                weight);
            leftLowerArm.localRotation = Quaternion.Slerp(
                leftLowerArm.localRotation,
                stableLeftLowerArmLocalRotation,
                weight);
            rightUpperArm.localRotation = Quaternion.Slerp(
                rightUpperArm.localRotation,
                stableRightUpperArmLocalRotation,
                weight);
            rightLowerArm.localRotation = Quaternion.Slerp(
                rightLowerArm.localRotation,
                stableRightLowerArmLocalRotation,
                weight);
        }

        private void UpdateBowGeometry(float drawWeight)
        {
            float localNockZ =
                -BowBraceHeight -
                BowMaximumDrawDistance * Mathf.Clamp01(drawWeight);
            if (arrowRoot != null)
            {
                arrowRoot.localPosition =
                    new Vector3(0f, 0f, localNockZ + BowBraceHeight);
            }

            Vector3 nock = new Vector3(0f, 0f, localNockZ);
            SetCylinderBetween(
                upperBowString,
                new Vector3(0f, 0.52f, -BowBraceHeight),
                nock);
            SetCylinderBetween(
                lowerBowString,
                new Vector3(0f, -0.52f, -BowBraceHeight),
                nock);
        }

        private static void SetCylinderBetween(
            Transform cylinder,
            Vector3 start,
            Vector3 end)
        {
            if (cylinder == null)
            {
                return;
            }

            Vector3 direction = end - start;
            cylinder.localPosition = Vector3.Lerp(start, end, 0.5f);
            cylinder.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                direction.normalized);
            Vector3 scale = cylinder.localScale;
            scale.y = direction.magnitude * 0.5f;
            cylinder.localScale = scale;
        }

        private static void SolveArmPose(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 desiredHandPosition,
            Quaternion desiredHandRotation,
            Vector3 elbowGuide,
            float weight,
            bool lockHandToForearm = false,
            bool alignContactPoint = false,
            Vector3 localContactPoint = default,
            Vector3 desiredContactPoint = default,
            Vector3 preferredBendSide = default)
        {
            Quaternion upperStart = upperArm.rotation;
            Quaternion lowerStart = lowerArm.rotation;
            Quaternion handStart = hand.rotation;
            Quaternion handLocalStart = hand.localRotation;
            ApplyFullArmPose(
                upperArm,
                lowerArm,
                hand,
                desiredHandPosition,
                desiredHandRotation,
                elbowGuide,
                handLocalStart,
                lockHandToForearm,
                preferredBendSide);

            if (alignContactPoint)
            {
                Vector3 correctedHandPosition = desiredHandPosition;
                for (int iteration = 0; iteration < 6; iteration++)
                {
                    Vector3 contactCorrection =
                        desiredContactPoint -
                        hand.TransformPoint(localContactPoint);
                    if (contactCorrection.sqrMagnitude < 0.000001f)
                    {
                        break;
                    }

                    correctedHandPosition +=
                        Vector3.ClampMagnitude(
                            contactCorrection,
                            0.025f);
                    upperArm.rotation = upperStart;
                    lowerArm.rotation = lowerStart;
                    hand.rotation = handStart;
                    ApplyFullArmPose(
                        upperArm,
                        lowerArm,
                        hand,
                        correctedHandPosition,
                        desiredHandRotation,
                        elbowGuide,
                        handLocalStart,
                        lockHandToForearm,
                        preferredBendSide);
                }
            }

            Quaternion upperSolved = upperArm.rotation;
            Quaternion lowerSolved = lowerArm.rotation;
            Quaternion handSolved = hand.rotation;
            upperArm.rotation =
                Quaternion.Slerp(upperStart, upperSolved, weight);
            lowerArm.rotation =
                Quaternion.Slerp(lowerStart, lowerSolved, weight);
            hand.rotation =
                Quaternion.Slerp(handStart, handSolved, weight);
        }

        private static void ApplyFullArmPose(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 desiredHandPosition,
            Quaternion desiredHandRotation,
            Vector3 elbowGuide,
            Quaternion handLocalRotation,
            bool lockHandToForearm,
            Vector3 preferredBendSide)
        {
            Vector3 shoulderPosition = upperArm.position;
            float upperLength =
                Vector3.Distance(upperArm.position, lowerArm.position);
            float lowerLength =
                Vector3.Distance(lowerArm.position, hand.position);
            Vector3 shoulderToTarget =
                desiredHandPosition - shoulderPosition;
            float targetDistance = Mathf.Clamp(
                shoulderToTarget.magnitude,
                Mathf.Abs(upperLength - lowerLength) + 0.0001f,
                upperLength + lowerLength - 0.0001f);
            Vector3 targetDirection =
                shoulderToTarget.sqrMagnitude > 0.000001f
                    ? shoulderToTarget.normalized
                    : (hand.position - shoulderPosition).normalized;
            Vector3 bendDirection = Vector3.ProjectOnPlane(
                elbowGuide - shoulderPosition,
                targetDirection);
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = Vector3.ProjectOnPlane(
                    lowerArm.position - shoulderPosition,
                    targetDirection);
            }
            bendDirection.Normalize();
            Vector3 preferredDirection =
                Vector3.ProjectOnPlane(
                    preferredBendSide,
                    targetDirection);
            if (preferredDirection.sqrMagnitude > 0.000001f &&
                preferredBendSide.sqrMagnitude > 0.000001f)
            {
                preferredDirection.Normalize();
                float sideAlignment = Vector3.Dot(
                    bendDirection,
                    preferredDirection);
                if (sideAlignment < 0f)
                {
                    bendDirection = -bendDirection;
                    sideAlignment = -sideAlignment;
                }

                if (sideAlignment < 0.35f)
                {
                    bendDirection = Vector3.Slerp(
                        bendDirection,
                        preferredDirection,
                        Mathf.InverseLerp(
                            0.35f,
                            -1f,
                            sideAlignment) *
                        0.55f).normalized;
                }
            }

            float elbowAlongTarget =
                (upperLength * upperLength -
                    lowerLength * lowerLength +
                    targetDistance * targetDistance) /
                (2f * targetDistance);
            float elbowAwayFromTarget = Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    upperLength * upperLength -
                    elbowAlongTarget * elbowAlongTarget));
            Vector3 desiredElbowPosition =
                shoulderPosition +
                targetDirection * elbowAlongTarget +
                bendDirection * elbowAwayFromTarget;

            upperArm.rotation =
                Quaternion.FromToRotation(
                    lowerArm.position - shoulderPosition,
                    desiredElbowPosition - shoulderPosition) *
                upperArm.rotation;
            lowerArm.rotation =
                Quaternion.FromToRotation(
                    hand.position - lowerArm.position,
                    desiredHandPosition - lowerArm.position) *
                lowerArm.rotation;
            if (lockHandToForearm)
            {
                hand.localRotation = handLocalRotation;
            }
            else
            {
                hand.rotation = desiredHandRotation;
            }
        }

        private Vector3 GetRightFingerContactPosition()
        {
            if (rightIndexDistal == null || rightMiddleDistal == null)
            {
                return rightHand.position;
            }

            Vector3 indexContact = rightIndexDistal.position;
            if (rightIndexIntermediate != null)
            {
                indexContact +=
                    (rightIndexDistal.position -
                        rightIndexIntermediate.position) *
                    0.55f;
            }

            Vector3 middleContact = rightMiddleDistal.position;
            if (rightMiddleIntermediate != null)
            {
                middleContact +=
                    (rightMiddleDistal.position -
                        rightMiddleIntermediate.position) *
                    0.55f;
            }

            return Vector3.Lerp(indexContact, middleContact, 0.5f);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || layerIndex != 0)
            {
                return;
            }

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        }

        private void ApplySheatheArmPose(float pathProgress)
        {
            if (rightUpperArm == null ||
                rightLowerArm == null ||
                rightHand == null ||
                swordRoot == null)
            {
                return;
            }

            EvaluateSheatheHandPose(
                pathProgress,
                out Vector3 desiredHandPosition,
                out Quaternion desiredHandRotation);
            SolveRightArm(
                pathProgress,
                desiredHandPosition,
                desiredHandRotation);
        }

        private void EvaluateSheatheHandPose(
            float pathProgress,
            out Vector3 position,
            out Quaternion rotation)
        {
            Quaternion sheatheFrameRotation =
                GetSheatheFrameRotation();
            Vector3 sheatheFrameOrigin =
                GetSheatheFrameOrigin();
            Vector3 sheatheRight =
                sheatheFrameRotation * Vector3.right;
            Vector3 sheatheUp =
                sheatheFrameRotation * Vector3.up;
            Vector3 sheatheForward =
                sheatheFrameRotation * Vector3.forward;
            Vector3 startPosition =
                sheatheFrameOrigin +
                sheatheFrameRotation *
                transitionStartHandSheatheLocalPosition;
            Quaternion startRotation =
                sheatheFrameRotation *
                transitionStartHandSheatheLocalRotation;

            Vector3 shoulderPosition = rightUpperArm.position;
            float upperLength = Vector3.Distance(
                rightUpperArm.position,
                rightLowerArm.position);
            float lowerLength = Vector3.Distance(
                rightLowerArm.position,
                rightHand.position);
            Vector3 shoulderUpperArmDirection =
                (sheatheForward * 0.9848f +
                    sheatheRight * 0.1736f).normalized;
            Vector3 shoulderForearmDirection = sheatheUp;
            Vector3 liftedElbowPosition =
                shoulderPosition +
                shoulderUpperArmDirection * upperLength;
            Vector3 liftedPosition =
                liftedElbowPosition +
                shoulderForearmDirection * lowerLength;

            Vector3 raisedUpperArmDirection =
                (sheatheForward * 0.70f +
                    sheatheUp * 0.70f +
                    sheatheRight * 0.12f).normalized;
            Vector3 raisedForearmDirection =
                (sheatheUp * 0.76f -
                    sheatheForward * 0.64f -
                    sheatheRight * 0.05f).normalized;
            Vector3 raisedElbowPosition =
                shoulderPosition +
                raisedUpperArmDirection * upperLength;
            Vector3 overheadPosition =
                raisedElbowPosition +
                raisedForearmDirection * lowerLength;

            Matrix4x4 backSwordMatrix = Matrix4x4.TRS(
                backSocket.position,
                backSocket.rotation,
                swordRoot.lossyScale);
            Vector3 placedPosition =
                backSwordMatrix.MultiplyPoint3x4(
                    handInSwordLocalPosition);
            Quaternion placedRotation =
                backSocket.rotation * handInSwordLocalRotation;

            Vector3 startBladeDirection =
                sheatheFrameRotation *
                transitionStartBladeDirectionSheatheLocal;
            Vector3 liftedBladeDirection =
                (sheatheUp * 0.68f +
                    sheatheForward * 0.70f -
                    sheatheRight * 0.12f).normalized;
            Vector3 overheadBladeDirection =
                (sheatheUp * 0.22f -
                    sheatheForward * 0.96f -
                    sheatheRight * 0.12f).normalized;
            Quaternion liftedRotation =
                Quaternion.FromToRotation(
                    startBladeDirection,
                    liftedBladeDirection) *
                startRotation;
            Quaternion overheadRotation =
                Quaternion.FromToRotation(
                    liftedBladeDirection,
                    overheadBladeDirection) *
                liftedRotation;

            float progress = Mathf.SmoothStep(
                0f,
                1f,
                pathProgress);
            const float liftEnd = 0.44f;
            const float overheadEnd = 0.73f;
            if (progress <= liftEnd)
            {
                float phase = Mathf.SmoothStep(
                    0f,
                    1f,
                    progress / liftEnd);
                Vector3 control =
                    startPosition +
                    sheatheForward * 0.22f +
                    sheatheUp * 0.12f;
                position = QuadraticBezier(
                    startPosition,
                    control,
                    liftedPosition,
                    phase);
                rotation = Quaternion.Slerp(
                    startRotation,
                    liftedRotation,
                    phase);
                return;
            }

            if (progress <= overheadEnd)
            {
                float phase = Mathf.SmoothStep(
                    0f,
                    1f,
                    (progress - liftEnd) /
                    (overheadEnd - liftEnd));
                Vector3 control =
                    liftedPosition +
                    sheatheForward * 0.08f +
                    sheatheUp * 0.24f;
                position = QuadraticBezier(
                    liftedPosition,
                    control,
                    overheadPosition,
                    phase);
                rotation = Quaternion.Slerp(
                    liftedRotation,
                    overheadRotation,
                    phase);
                return;
            }

            float placementPhase = Mathf.SmoothStep(
                0f,
                1f,
                (progress - overheadEnd) /
                (1f - overheadEnd));
            Vector3 placementControl =
                overheadPosition -
                sheatheForward * 0.30f +
                sheatheRight * 0.10f;
            position = QuadraticBezier(
                overheadPosition,
                placementControl,
                placedPosition,
                placementPhase);
            rotation = Quaternion.Slerp(
                overheadRotation,
                placedRotation,
                placementPhase);
        }

        private void SolveRightArm(
            float pathProgress,
            Vector3 desiredHandPosition,
            Quaternion desiredHandRotation)
        {
            Vector3 shoulderPosition = rightUpperArm.position;
            Vector3 elbowPosition = rightLowerArm.position;
            Vector3 handPosition = rightHand.position;
            float upperLength = Vector3.Distance(
                shoulderPosition,
                elbowPosition);
            float lowerLength = Vector3.Distance(
                elbowPosition,
                handPosition);
            Vector3 shoulderToTarget =
                desiredHandPosition - shoulderPosition;
            float targetDistance = Mathf.Clamp(
                shoulderToTarget.magnitude,
                Mathf.Abs(upperLength - lowerLength) + 0.0001f,
                upperLength + lowerLength - 0.0001f);
            Vector3 targetDirection =
                shoulderToTarget.sqrMagnitude > 0.000001f
                    ? shoulderToTarget.normalized
                    : (handPosition - shoulderPosition).normalized;
            Vector3 elbowGuidePosition =
                GetSheatheElbowGuidePosition(
                    shoulderPosition,
                    pathProgress);
            Vector3 startingElbowDirection =
                (elbowGuidePosition - shoulderPosition).normalized;
            Quaternion sheatheFrameRotation =
                GetSheatheFrameRotation();
            Vector3 bendDirection = Vector3.ProjectOnPlane(
                startingElbowDirection,
                targetDirection);
            Vector3 previousBendDirection = Vector3.ProjectOnPlane(
                sheatheFrameRotation *
                    sheatheBendDirectionLocal,
                targetDirection);
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = previousBendDirection;
            }
            if (previousBendDirection.sqrMagnitude < 0.000001f)
            {
                previousBendDirection = bendDirection;
            }
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = Vector3.ProjectOnPlane(
                    sheatheFrameRotation * Vector3.forward,
                    targetDirection);
                if (bendDirection.sqrMagnitude < 0.000001f)
                {
                    bendDirection = Vector3.ProjectOnPlane(
                        sheatheFrameRotation * Vector3.right,
                        targetDirection);
                }
                previousBendDirection = bendDirection;
            }

            bendDirection.Normalize();
            previousBendDirection.Normalize();
            if (Vector3.Dot(
                    previousBendDirection,
                    bendDirection) < -0.25f)
            {
                previousBendDirection = bendDirection;
            }
            float bendDamping =
                1f - Mathf.Exp(-18f * Time.deltaTime);
            bendDirection = Vector3.Lerp(
                previousBendDirection,
                bendDirection,
                bendDamping);
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = Vector3.ProjectOnPlane(
                    startingElbowDirection,
                    targetDirection);
            }
            bendDirection.Normalize();
            sheatheBendDirectionLocal =
                Quaternion.Inverse(sheatheFrameRotation) *
                bendDirection;

            float elbowAlongTarget =
                (upperLength * upperLength -
                    lowerLength * lowerLength +
                    targetDistance * targetDistance) /
                (2f * targetDistance);
            float elbowAwayFromTarget = Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    upperLength * upperLength -
                    elbowAlongTarget * elbowAlongTarget));
            Vector3 desiredElbowPosition =
                shoulderPosition +
                targetDirection * elbowAlongTarget +
                bendDirection * elbowAwayFromTarget;

            Vector3 currentUpperDirection =
                rightLowerArm.position - shoulderPosition;
            Vector3 desiredUpperDirection =
                desiredElbowPosition - shoulderPosition;
            rightUpperArm.rotation =
                Quaternion.FromToRotation(
                    currentUpperDirection,
                    desiredUpperDirection) *
                rightUpperArm.rotation;

            Vector3 solvedElbowPosition = rightLowerArm.position;
            Vector3 currentLowerDirection =
                rightHand.position - solvedElbowPosition;
            Vector3 desiredLowerDirection =
                desiredHandPosition - solvedElbowPosition;
            rightLowerArm.rotation =
                Quaternion.FromToRotation(
                    currentLowerDirection,
                    desiredLowerDirection) *
                rightLowerArm.rotation;
            ApplyLockedWristRotation(
                pathProgress,
                desiredHandPosition,
                desiredHandRotation);
        }

        private void ApplyLockedWristRotation(
            float pathProgress,
            Vector3 desiredHandPosition,
            Quaternion desiredHandRotation)
        {
            Quaternion lockedWorldRotation =
                rightLowerArm.rotation *
                lockedHandLocalRotation;
            Quaternion desiredDelta =
                desiredHandRotation *
                Quaternion.Inverse(lockedWorldRotation);
            Vector3 forearmAxis =
                desiredHandPosition - rightLowerArm.position;
            if (forearmAxis.sqrMagnitude > 0.000001f)
            {
                Quaternion forearmTwist = ExtractTwist(
                    desiredDelta,
                    forearmAxis.normalized);
                rightLowerArm.rotation =
                    forearmTwist * rightLowerArm.rotation;
            }

            rightHand.localRotation = lockedHandLocalRotation;
            float wristRelease = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    wristReleaseStart,
                    1f,
                    pathProgress));
            if (wristRelease > 0f)
            {
                rightHand.rotation = Quaternion.Slerp(
                    rightHand.rotation,
                    desiredHandRotation,
                    wristRelease);
            }
        }

        private static Quaternion ExtractTwist(
            Quaternion rotation,
            Vector3 axis)
        {
            Vector3 imaginary = new Vector3(
                rotation.x,
                rotation.y,
                rotation.z);
            Vector3 projected = Vector3.Project(
                imaginary,
                axis);
            Quaternion twist = new Quaternion(
                projected.x,
                projected.y,
                projected.z,
                rotation.w);
            float magnitude = Mathf.Sqrt(
                twist.x * twist.x +
                twist.y * twist.y +
                twist.z * twist.z +
                twist.w * twist.w);
            if (magnitude < 0.000001f)
            {
                return Quaternion.identity;
            }

            float inverseMagnitude = 1f / magnitude;
            return new Quaternion(
                twist.x * inverseMagnitude,
                twist.y * inverseMagnitude,
                twist.z * inverseMagnitude,
                twist.w * inverseMagnitude);
        }

        private Vector3 GetSheatheElbowGuidePosition(
            Vector3 shoulderPosition,
            float pathProgress)
        {
            Quaternion sheatheFrameRotation =
                GetSheatheFrameRotation();
            Vector3 sheatheFrameOrigin =
                GetSheatheFrameOrigin();
            Vector3 sheatheRight =
                sheatheFrameRotation * Vector3.right;
            Vector3 sheatheUp =
                sheatheFrameRotation * Vector3.up;
            Vector3 sheatheForward =
                sheatheFrameRotation * Vector3.forward;
            Vector3 startPosition =
                sheatheFrameOrigin +
                sheatheFrameRotation *
                transitionStartElbowSheatheLocalPosition;
            float upperLength = Vector3.Distance(
                rightUpperArm.position,
                rightLowerArm.position);
            Vector3 forwardUpperArmDirection =
                (sheatheForward * 0.9848f +
                    sheatheRight * 0.1736f).normalized;
            Vector3 forwardPosition =
                shoulderPosition +
                forwardUpperArmDirection * upperLength;
            Vector3 raisedUpperArmDirection =
                (sheatheForward * 0.70f +
                    sheatheUp * 0.70f +
                    sheatheRight * 0.12f).normalized;
            Vector3 raisedPosition =
                shoulderPosition +
                raisedUpperArmDirection * upperLength;
            Vector3 placedPosition =
                shoulderPosition +
                (sheatheForward * 0.80f +
                    sheatheUp * 0.53f +
                    sheatheRight * 0.28f).normalized *
                upperLength;

            float progress = Mathf.SmoothStep(
                0f,
                1f,
                pathProgress);
            const float forwardEnd = 0.48f;
            const float raisedEnd = 0.76f;
            if (progress <= forwardEnd)
            {
                return Vector3.Lerp(
                    startPosition,
                    forwardPosition,
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        progress / forwardEnd));
            }

            if (progress <= raisedEnd)
            {
                return Vector3.Lerp(
                    forwardPosition,
                    raisedPosition,
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        (progress - forwardEnd) /
                        (raisedEnd - forwardEnd)));
            }

            return Vector3.Lerp(
                raisedPosition,
                placedPosition,
                Mathf.SmoothStep(
                    0f,
                    1f,
                (progress - raisedEnd) /
                    (1f - raisedEnd)));
        }

        private void CaptureSheatheBendDirection()
        {
            Quaternion sheatheFrameRotation =
                GetSheatheFrameRotation();
            Vector3 shoulderPosition = rightUpperArm.position;
            Vector3 shoulderToHand =
                rightHand.position - shoulderPosition;
            Vector3 shoulderToElbow =
                rightLowerArm.position - shoulderPosition;
            Vector3 bendDirection = Vector3.ProjectOnPlane(
                shoulderToElbow,
                shoulderToHand.normalized);
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection =
                    sheatheFrameRotation * Vector3.forward;
            }

            sheatheBendDirectionLocal =
                Quaternion.Inverse(sheatheFrameRotation) *
                bendDirection.normalized;
        }

        private Vector3 GetSheatheFrameOrigin()
        {
            if (leftUpperArm != null && rightUpperArm != null)
            {
                return Vector3.Lerp(
                    leftUpperArm.position,
                    rightUpperArm.position,
                    0.5f);
            }

            return upperChest != null
                ? upperChest.position
                : characterRoot.position;
        }

        private Quaternion GetSheatheFrameRotation()
        {
            if (leftUpperArm == null ||
                rightUpperArm == null ||
                characterRoot == null)
            {
                return characterRoot != null
                    ? characterRoot.rotation
                    : Quaternion.identity;
            }

            Vector3 right =
                (rightUpperArm.position -
                    leftUpperArm.position).normalized;
            Vector3 up = characterRoot.up;
            Vector3 forward = Vector3.Cross(right, up);
            if (forward.sqrMagnitude < 0.000001f)
            {
                return characterRoot.rotation;
            }

            forward.Normalize();
            right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
            return Quaternion.LookRotation(forward, up);
        }

        private static Vector3 QuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start +
                2f * inverse * progress * control +
                progress * progress * end;
        }

        private void AdvanceTransition()
        {
            switch (transitionPhase)
            {
                case TransitionPhase.SheatheWithSword:
                    swordRoot.SetParent(backSocket, false);
                    swordRoot.localPosition = Vector3.zero;
                    swordRoot.localRotation = Quaternion.identity;
                    swordRoot.localScale = carryLocalScale;
                    SetSwordReadyWeight(0f);
                    BeginPhase(TransitionPhase.ReturnEmptyHand);
                    break;

                case TransitionPhase.ReachForSword:
                    swordRoot.SetParent(carryParent, false);
                    swordRoot.localPosition = carryLocalPosition;
                    swordRoot.localRotation = carryLocalRotation;
                    swordRoot.localScale = carryLocalScale;
                    SetSwordReadyWeight(1f);
                    BeginPhase(TransitionPhase.DrawWithSword);
                    break;

                case TransitionPhase.ReturnEmptyHand:
                case TransitionPhase.DrawWithSword:
                    CompleteTransition();
                    break;

                default:
                    CompleteTransition();
                    break;
            }
        }

        private void BeginPhase(TransitionPhase phase)
        {
            transitionPhase = phase;
            transitionProgress = 0f;
            transitionStartedAt = Time.time;
        }

        private void CompleteTransition()
        {
            transitioning = false;
            transitionProgress = 1f;
            transitionPhase = TransitionPhase.None;
            activeSlot = targetSlot;
            if (activeSlot == SecondarySlot)
            {
                swordRoot.SetParent(backSocket, false);
                swordRoot.localPosition = Vector3.zero;
                swordRoot.localRotation = Quaternion.identity;
                swordRoot.localScale = carryLocalScale;
                SetSwordReadyWeight(0f);
                SetSwordAvailability(false);
                EquipBow();
            }
            else
            {
                swordRoot.SetParent(carryParent, false);
                swordRoot.localPosition = carryLocalPosition;
                swordRoot.localRotation = carryLocalRotation;
                swordRoot.localScale = carryLocalScale;
                SetSwordReadyWeight(1f);
                SetSwordAvailability(true);
                StowBow();
            }

            ActiveSlotChanged?.Invoke(activeSlot);
        }

        private void CaptureCarryTransform()
        {
            if (swordRoot == null || rightHand == null)
            {
                return;
            }

            carryParent = rightHand;
            carryLocalPosition = swordRoot.localPosition;
            carryLocalRotation = swordRoot.localRotation;
            carryLocalScale = swordRoot.localScale;
            lockedHandLocalRotation = rightHand.localRotation;
        }

        private void CaptureBowTransform()
        {
            if (bowRoot != null)
            {
                bowLocalScale = bowRoot.localScale;
            }
        }

        private void EquipBow()
        {
            if (bowRoot == null || leftHand == null)
            {
                return;
            }

            bowRoot.SetParent(leftHand, false);
            bowRoot.localPosition = Vector3.zero;
            bowRoot.localRotation = Quaternion.identity;
            bowRoot.localScale = bowLocalScale;
            bowEquipped = true;
            bowPoseWeight = 0f;
            UpdateBowGeometry(0f);
            bowWeapon?.SetWeaponEquipped(true);
        }

        private void StowBow()
        {
            if (bowRoot == null || bowBackSocket == null)
            {
                return;
            }

            bowRoot.SetParent(bowBackSocket, false);
            bowRoot.localPosition = Vector3.zero;
            bowRoot.localRotation = Quaternion.identity;
            bowRoot.localScale = bowLocalScale;
            UpdateBowGeometry(0f);
            bowWeapon?.SetWeaponEquipped(false);
            bowEquipped = false;
        }

        private void CaptureHandOffsetInSwordSpace()
        {
            if (swordRoot == null || rightHand == null)
            {
                return;
            }

            handInSwordLocalPosition =
                swordRoot.InverseTransformPoint(rightHand.position);
            handInSwordLocalRotation =
                Quaternion.Inverse(swordRoot.rotation) * rightHand.rotation;
        }

        private void ResolveRig()
        {
            if (animator == null)
            {
                return;
            }

            rightHand =
                animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand =
                animator.GetBoneTransform(HumanBodyBones.LeftHand);
            leftUpperArm =
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm =
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftShoulder =
                animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            rightUpperArm =
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm =
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightShoulder =
                animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightIndexIntermediate =
                animator.GetBoneTransform(
                    HumanBodyBones.RightIndexIntermediate);
            rightIndexDistal =
                animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
            rightMiddleIntermediate =
                animator.GetBoneTransform(
                    HumanBodyBones.RightMiddleIntermediate);
            rightMiddleDistal =
                animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
            upperChest =
                animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                animator.GetBoneTransform(HumanBodyBones.Chest);
            head =
                animator.GetBoneTransform(HumanBodyBones.Head);
            if (bowRoot != null)
            {
                Transform[] bowParts =
                    bowRoot.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < bowParts.Length; index++)
                {
                    if (bowParts[index].name == "Upper Bow String")
                    {
                        upperBowString = bowParts[index];
                    }
                    else if (bowParts[index].name == "Lower Bow String")
                    {
                        lowerBowString = bowParts[index];
                    }
                }
            }
            swordReadyLayerIndex =
                animator.GetLayerIndex(SwordReadyLayerName);
        }

        private void SetSwordAvailability(bool available)
        {
            attackPresenter?.SetWeaponEquipped(available);
            blockPresenter?.SetWeaponEquipped(available);
        }

        private void SetSwordReadyWeight(float weight)
        {
            if (animator != null && swordReadyLayerIndex >= 0)
            {
                animator.SetLayerWeight(
                    swordReadyLayerIndex,
                    Mathf.Clamp01(weight));
            }
        }

        private void OnSlotRequested(int slot)
        {
            RequestSlot(slot);
        }

        private void Subscribe()
        {
            if (input == null)
            {
                return;
            }

            input.WeaponSlotRequested -= OnSlotRequested;
            input.WeaponSlotRequested += OnSlotRequested;
        }

        private void Unsubscribe()
        {
            if (input != null)
            {
                input.WeaponSlotRequested -= OnSlotRequested;
            }
        }
    }
}
