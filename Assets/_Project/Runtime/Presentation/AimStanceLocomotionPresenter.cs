using UnityEngine;
using WorldBuilder.Gameplay.Characters;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(700)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class AimStanceLocomotionPresenter :
        MonoBehaviour
    {
        public const float AlertWalkLean = 8f;
        public const float AlertRunLean = 16f;
        public const float AlertWalkShoulderClose = 7.5f;
        public const float AlertRunShoulderClose = 11f;

        private static readonly int GaitPlaybackHash =
            Animator.StringToHash(
                HumanoidAnimatorPresenter.GaitPlaybackParameter);

        [SerializeField] private Animator animator;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private UpperBodyAimPresenter aimPresenter;
        [SerializeField, Range(60f, 90f)] private float bowSideYaw = 78f;
        [SerializeField, Range(30f, 60f)] private float bowCrossStepYaw = 45f;
        [SerializeField, Min(0.01f)] private float blendDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float playbackReverseDuration = 0.16f;
        [SerializeField, Min(0.2f)] private float swordShuffleCycleDistance = 1.35f;
        [SerializeField, Min(0.05f)] private float swordShuffleTravel = 0.62f;
        [SerializeField, Min(0f)] private float swordShuffleLift = 0.06f;
        [SerializeField, Range(0.5f, 0.85f)] private float swordShufflePlantFraction = 0.68f;
        [SerializeField, Range(0f, 12f)] private float swordWalkLean = 5.5f;
        [SerializeField, Range(4f, 18f)] private float swordRunLean = 11f;
        [SerializeField, Range(0f, 15f)] private float swordShoulderClose = 6f;
        [SerializeField, Range(0f, 20f)] private float swordArmBack = 11f;
        [SerializeField, Range(0f, 1f)] private float swordWalkArmStability = 0.72f;
        [SerializeField, Range(0f, 1f)] private float swordRunArmStability = 0.92f;

        private Transform characterRoot;
        private Transform hips;
        private Transform spine;
        private Transform chest;
        private Transform upperChest;
        private Transform head;
        private Transform leftShoulder;
        private Transform leftUpperArm;
        private Transform rightShoulder;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Transform leftThigh;
        private Transform leftKnee;
        private Transform leftFoot;
        private Transform rightThigh;
        private Transform rightKnee;
        private Transform rightFoot;
        private Vector3 leftFootReference;
        private Vector3 rightFootReference;
        private Quaternion leftFootRootRotation;
        private Quaternion rightFootRootRotation;
        private float leftUpperLength;
        private float leftLowerLength;
        private float rightUpperLength;
        private float rightLowerLength;
        private float bowWeight;
        private float swordShuffleWeight;
        private float swordReadyWeight;
        private float alertMovementWeight;
        private float currentBowYaw;
        private float gaitPlayback = 1f;
        private float shuffleCycle;
        private bool hasReferencePose;
        private bool hasSwordReferencePose;
        private Quaternion leftShoulderReference;
        private Quaternion leftUpperArmReference;
        private Quaternion rightShoulderReference;
        private Quaternion rightUpperArmReference;
        private Quaternion rightLowerArmReference;
        private Quaternion rightHandReference;
        private ShortSwordAttackPresenter swordAttackPresenter;
        private ShortSwordBlockPresenter swordBlockPresenter;
        private TwoSlotWeaponPresenter weaponSlots;

        public float StanceWeight =>
            Mathf.Max(
                swordReadyWeight,
                Mathf.Max(
                    alertMovementWeight,
                    Mathf.Max(bowWeight, swordShuffleWeight)));
        public float BowStanceWeight => bowWeight;
        public float SwordShuffleWeight => swordShuffleWeight;
        public float SwordReadyWeight => swordReadyWeight;
        public float CurrentStanceYaw => currentBowYaw * bowWeight;
        public float GaitPlaybackDirection => gaitPlayback;
        public bool UsesAuthoredWalk => true;

        public void Configure(
            Animator targetAnimator,
            ThirdPersonMotor targetMotor,
            UpperBodyAimPresenter targetAimPresenter)
        {
            animator = targetAnimator;
            motor = targetMotor;
            aimPresenter = targetAimPresenter;
            characterRoot = motor != null
                ? motor.transform
                : transform.root;
            ResolveRig();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            motor ??= GetComponentInParent<ThirdPersonMotor>();
            aimPresenter ??= GetComponent<UpperBodyAimPresenter>();
            characterRoot = motor != null
                ? motor.transform
                : transform.root;
            ResolveRig();
        }

        private void Update()
        {
            if (animator == null ||
                motor == null ||
                aimPresenter == null)
            {
                return;
            }

            Vector3 localVelocity = motor.LocalHorizontalVelocity;
            float targetPlayback = 1f;
            if (aimPresenter.BowAimLocked &&
                localVelocity.sqrMagnitude > 0.0025f)
            {
                float targetYaw = GetBowYaw(localVelocity);
                Vector3 stanceForward =
                    Quaternion.Euler(0f, targetYaw, 0f) *
                    Vector3.forward;
                targetPlayback =
                    Vector3.Dot(
                        localVelocity.normalized,
                        stanceForward) < 0f
                        ? -1f
                        : 1f;
            }
            else if (SwordGuardActive &&
                Mathf.Abs(localVelocity.z) > 0.05f)
            {
                targetPlayback =
                    localVelocity.z < 0f
                        ? -1f
                        : 1f;
            }

            gaitPlayback = Mathf.MoveTowards(
                gaitPlayback,
                targetPlayback,
                2f * Time.deltaTime /
                    Mathf.Max(0.01f, playbackReverseDuration));
            animator.SetFloat(
                GaitPlaybackHash,
                gaitPlayback);
        }

        private void LateUpdate()
        {
            if (!HasCompleteRig())
            {
                ResolveRig();
                if (!HasCompleteRig())
                {
                    return;
                }
            }

            if (!aimPresenter.AimLocked &&
                motor.IsGrounded &&
                motor.HorizontalSpeed < 0.08f)
            {
                CaptureReferencePose();
                if (SwordEquipped &&
                    (swordAttackPresenter == null ||
                        !swordAttackPresenter.IsAttacking))
                {
                    CaptureSwordReferencePose();
                }
            }

            Vector3 localVelocity = motor.LocalHorizontalVelocity;
            bool bowActive =
                aimPresenter.BowAimLocked &&
                motor.IsGrounded;
            bool swordLateral =
                SwordGuardActive &&
                motor.IsGrounded &&
                motor.HorizontalSpeed > 0.05f &&
                Mathf.Abs(localVelocity.x) >
                    Mathf.Abs(localVelocity.z) * 1.15f;
            bowWeight = MoveWeight(
                bowWeight,
                bowActive ? 1f : 0f);
            swordShuffleWeight = MoveWeight(
                swordShuffleWeight,
                swordLateral ? 1f : 0f);
            bool swordReadyActive =
                SwordEquipped &&
                !SwordGuardActive &&
                !aimPresenter.BowAimLocked &&
                (swordAttackPresenter == null ||
                    !swordAttackPresenter.IsAttacking) &&
                motor.IsGrounded &&
                !motor.IsCrouched &&
                motor.HorizontalSpeed > 0.08f;
            bool alertMovementActive =
                (SwordEquipped || BowEquipped) &&
                !SwordGuardActive &&
                !aimPresenter.BowAimLocked &&
                (swordAttackPresenter == null ||
                    !swordAttackPresenter.IsAttacking) &&
                motor.IsGrounded &&
                !motor.IsCrouched &&
                motor.HorizontalSpeed > 0.08f;
            alertMovementWeight = MoveWeight(
                alertMovementWeight,
                alertMovementActive ? 1f : 0f);
            if (!swordReadyActive &&
                (SwordGuardActive ||
                    aimPresenter.BowAimLocked ||
                    (swordAttackPresenter != null &&
                        swordAttackPresenter.IsAttacking)))
            {
                swordReadyWeight = 0f;
            }
            else
            {
                swordReadyWeight = MoveWeight(
                    swordReadyWeight,
                    swordReadyActive ? 1f : 0f);
            }

            if (alertMovementWeight > 0.001f)
            {
                ApplyAlertMovementPosture();
            }

            if (swordReadyWeight > 0.001f &&
                hasSwordReferencePose)
            {
                ApplySwordReadyStance();
            }

            if (bowWeight > 0.001f)
            {
                float targetYaw = GetBowYaw(localVelocity);
                currentBowYaw = Mathf.MoveTowardsAngle(
                    currentBowYaw,
                    targetYaw,
                    240f * Time.deltaTime);
                Quaternion preservedSpineRotation =
                    spine.rotation;
                hips.rotation =
                    Quaternion.AngleAxis(
                        currentBowYaw * bowWeight,
                        characterRoot.up) *
                    hips.rotation;
                spine.rotation = preservedSpineRotation;
            }

            if (!hasReferencePose ||
                swordShuffleWeight <= 0.001f)
            {
                return;
            }

            float lateralSpeed = Mathf.Abs(localVelocity.x);
            if (lateralSpeed > 0.02f)
            {
                float direction =
                    Mathf.Sign(localVelocity.x);
                shuffleCycle = Mathf.Repeat(
                    shuffleCycle +
                    direction *
                    lateralSpeed *
                    Time.deltaTime /
                    Mathf.Max(
                        0.2f,
                        swordShuffleCycleDistance),
                    1f);
            }

            Vector3 center = Vector3.Lerp(
                leftFootReference,
                rightFootReference,
                0.5f);
            float width = Mathf.Max(
                0.22f,
                Mathf.Abs(
                    rightFootReference.x -
                    leftFootReference.x));
            Vector3 leftBase =
                center + Vector3.left * width * 0.5f;
            Vector3 rightBase =
                center + Vector3.right * width * 0.5f;
            Vector3 leftTarget = EvaluateShuffleFoot(
                leftBase,
                shuffleCycle);
            Vector3 rightTarget = EvaluateShuffleFoot(
                rightBase,
                Mathf.Repeat(shuffleCycle + 0.5f, 1f));
            SolveLeg(
                leftThigh,
                leftKnee,
                leftFoot,
                leftTarget,
                leftUpperLength,
                leftLowerLength,
                leftFootRootRotation,
                -1f,
                swordShuffleWeight);
            SolveLeg(
                rightThigh,
                rightKnee,
                rightFoot,
                rightTarget,
                rightUpperLength,
                rightLowerLength,
                rightFootRootRotation,
                1f,
                swordShuffleWeight);
        }

        private float GetBowYaw(Vector3 localVelocity)
        {
            float total =
                Mathf.Abs(localVelocity.x) +
                Mathf.Abs(localVelocity.z);
            float forwardBias = total > 0.02f
                ? Mathf.Abs(localVelocity.z) / total
                : 0f;
            return Mathf.Lerp(
                bowSideYaw,
                bowCrossStepYaw,
                forwardBias);
        }

        private float MoveWeight(
            float current,
            float target)
        {
            return Mathf.MoveTowards(
                current,
                target,
                Time.deltaTime /
                    Mathf.Max(0.01f, blendDuration));
        }

        private void ApplySwordReadyStance()
        {
            float gait = Mathf.InverseLerp(
                motor.WalkSpeed,
                motor.SprintSpeed,
                motor.HorizontalSpeed);
            float close =
                swordShoulderClose * swordReadyWeight;

            float rightArmWeight =
                Mathf.Lerp(
                    swordWalkArmStability,
                    swordRunArmStability,
                    gait) *
                swordReadyWeight;
            float leftArmWeight =
                rightArmWeight * 0.42f;
            Quaternion leftUpperArmTarget =
                leftUpperArm.parent.rotation *
                leftUpperArmReference;
            if (leftShoulder == null)
            {
                leftUpperArmTarget =
                    Quaternion.AngleAxis(
                        close,
                        characterRoot.up) *
                    leftUpperArmTarget;
            }
            leftUpperArm.rotation = Quaternion.Slerp(
                leftUpperArm.rotation,
                leftUpperArmTarget,
                leftArmWeight);

            Quaternion armBackRotation =
                Quaternion.AngleAxis(
                    swordArmBack *
                    Mathf.Lerp(0.7f, 1f, gait),
                    characterRoot.right);
            Quaternion rightUpperArmTarget =
                armBackRotation *
                (rightUpperArm.parent.rotation *
                    rightUpperArmReference);
            if (rightShoulder == null)
            {
                rightUpperArmTarget =
                    Quaternion.AngleAxis(
                        -close,
                        characterRoot.up) *
                    rightUpperArmTarget;
            }
            rightUpperArm.rotation = Quaternion.Slerp(
                rightUpperArm.rotation,
                rightUpperArmTarget,
                rightArmWeight);
            Quaternion rightLowerArmTarget =
                rightLowerArm.parent.rotation *
                rightLowerArmReference;
            rightLowerArm.rotation = Quaternion.Slerp(
                rightLowerArm.rotation,
                rightLowerArmTarget,
                rightArmWeight);
            Quaternion rightHandTarget =
                rightHand.parent.rotation *
                rightHandReference;
            rightHand.rotation = Quaternion.Slerp(
                rightHand.rotation,
                rightHandTarget,
                rightArmWeight);
        }

        private void ApplyAlertMovementPosture()
        {
            float gait = Mathf.InverseLerp(
                motor.WalkSpeed,
                motor.SprintSpeed,
                motor.HorizontalSpeed);
            float lean = CalculateAlertLean(
                gait,
                Mathf.Max(AlertWalkLean, swordWalkLean),
                Mathf.Max(AlertRunLean, swordRunLean)) *
                alertMovementWeight;
            float shoulderClose = Mathf.Lerp(
                Mathf.Max(
                    AlertWalkShoulderClose,
                    swordShoulderClose),
                AlertRunShoulderClose,
                gait) * alertMovementWeight;
            Quaternion animatedHeadRotation =
                head != null
                    ? head.rotation
                    : Quaternion.identity;
            Quaternion leftThighRotation =
                leftThigh.rotation;
            Quaternion rightThighRotation =
                rightThigh.rotation;
            Vector3 pitchAxis = characterRoot.right;

            ApplyWorldPitch(hips, lean * 0.42f, pitchAxis);
            leftThigh.rotation = leftThighRotation;
            rightThigh.rotation = rightThighRotation;
            ApplyWorldPitch(spine, lean * 0.25f, pitchAxis);
            ApplyWorldPitch(chest, lean * 0.20f, pitchAxis);
            ApplyWorldPitch(upperChest, lean * 0.13f, pitchAxis);

            if (leftShoulder != null)
            {
                leftShoulder.rotation =
                    Quaternion.AngleAxis(
                        shoulderClose,
                        characterRoot.up) *
                    leftShoulder.rotation;
            }
            if (rightShoulder != null)
            {
                rightShoulder.rotation =
                    Quaternion.AngleAxis(
                        -shoulderClose,
                        characterRoot.up) *
                    rightShoulder.rotation;
            }

            if (head != null)
            {
                float awarenessRecovery = Mathf.Lerp(
                    0.58f,
                    0.76f,
                    gait) * alertMovementWeight;
                head.rotation = Quaternion.Slerp(
                    head.rotation,
                    animatedHeadRotation,
                    awarenessRecovery);
            }
        }

        public static float CalculateAlertLean(
            float gait,
            float walkLean,
            float runLean)
        {
            return Mathf.Lerp(
                walkLean,
                runLean,
                Mathf.Clamp01(gait));
        }

        private static void ApplyWorldPitch(
            Transform bone,
            float degrees,
            Vector3 axis)
        {
            if (bone == null)
            {
                return;
            }

            bone.rotation =
                Quaternion.AngleAxis(degrees, axis) *
                bone.rotation;
        }

        private Vector3 EvaluateShuffleFoot(
            Vector3 basePosition,
            float phase)
        {
            float travel;
            float lift = 0f;
            if (phase < swordShufflePlantFraction)
            {
                float progress =
                    phase /
                    Mathf.Max(
                        0.001f,
                        swordShufflePlantFraction);
                travel = Mathf.Lerp(
                    swordShuffleTravel * 0.5f,
                    -swordShuffleTravel * 0.5f,
                    progress);
            }
            else
            {
                float progress = Mathf.InverseLerp(
                    swordShufflePlantFraction,
                    1f,
                    phase);
                travel = Mathf.Lerp(
                    -swordShuffleTravel * 0.5f,
                    swordShuffleTravel * 0.5f,
                    Mathf.SmoothStep(0f, 1f, progress));
                lift =
                    Mathf.Sin(progress * Mathf.PI) *
                    swordShuffleLift;
            }

            return basePosition +
                Vector3.right * travel +
                Vector3.up * lift;
        }

        private void SolveLeg(
            Transform thigh,
            Transform knee,
            Transform foot,
            Vector3 targetLocal,
            float upperLength,
            float lowerLength,
            Quaternion targetFootRootRotation,
            float side,
            float weight)
        {
            Vector3 hip = thigh.position;
            Vector3 target =
                characterRoot.TransformPoint(targetLocal);
            Vector3 toTarget = target - hip;
            float rawDistance = Mathf.Max(
                0.001f,
                toTarget.magnitude);
            Vector3 targetDirection =
                toTarget / rawDistance;
            float targetDistance = Mathf.Clamp(
                rawDistance,
                Mathf.Abs(
                    upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            Vector3 solvedTarget =
                hip + targetDirection * targetDistance;
            float along =
                (upperLength * upperLength -
                    lowerLength * lowerLength +
                    targetDistance * targetDistance) /
                (2f * targetDistance);
            float bendDistance = Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    upperLength * upperLength -
                    along * along));
            Vector3 bendGuide =
                characterRoot.TransformDirection(
                    Vector3.forward +
                    Vector3.right * side * 0.08f);
            Vector3 bendDirection =
                Vector3.ProjectOnPlane(
                    bendGuide,
                    targetDirection).normalized;
            if (bendDirection.sqrMagnitude < 0.001f)
            {
                bendDirection =
                    Vector3.ProjectOnPlane(
                        characterRoot.up,
                        targetDirection).normalized;
            }

            Vector3 solvedKnee =
                hip +
                targetDirection * along +
                bendDirection * bendDistance;
            Quaternion thighStart = thigh.rotation;
            Quaternion kneeStart = knee.rotation;
            Quaternion footStart = foot.rotation;
            thigh.rotation =
                Quaternion.FromToRotation(
                    knee.position - hip,
                    solvedKnee - hip) *
                thigh.rotation;
            knee.rotation =
                Quaternion.FromToRotation(
                    foot.position - knee.position,
                    solvedTarget - knee.position) *
                knee.rotation;
            foot.rotation =
                characterRoot.rotation *
                targetFootRootRotation;
            thigh.rotation = Quaternion.Slerp(
                thighStart,
                thigh.rotation,
                weight);
            knee.rotation = Quaternion.Slerp(
                kneeStart,
                knee.rotation,
                weight);
            foot.rotation = Quaternion.Slerp(
                footStart,
                foot.rotation,
                weight);
        }

        private void ResolveRig()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            hips = animator.GetBoneTransform(
                HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(
                HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(
                HumanBodyBones.Chest);
            upperChest =
                animator.GetBoneTransform(
                    HumanBodyBones.UpperChest) ??
                chest;
            head = animator.GetBoneTransform(
                HumanBodyBones.Head);
            leftShoulder = animator.GetBoneTransform(
                HumanBodyBones.LeftShoulder);
            leftUpperArm = animator.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            rightShoulder = animator.GetBoneTransform(
                HumanBodyBones.RightShoulder);
            rightUpperArm = animator.GetBoneTransform(
                HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(
                HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(
                HumanBodyBones.RightHand);
            swordAttackPresenter ??=
                GetComponent<ShortSwordAttackPresenter>();
            swordBlockPresenter ??=
                GetComponent<ShortSwordBlockPresenter>();
            weaponSlots ??=
                GetComponent<TwoSlotWeaponPresenter>();
            leftThigh = animator.GetBoneTransform(
                HumanBodyBones.LeftUpperLeg);
            leftKnee = animator.GetBoneTransform(
                HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(
                HumanBodyBones.LeftFoot);
            rightThigh = animator.GetBoneTransform(
                HumanBodyBones.RightUpperLeg);
            rightKnee = animator.GetBoneTransform(
                HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(
                HumanBodyBones.RightFoot);
            if (HasCompleteRig())
            {
                leftUpperLength = Vector3.Distance(
                    leftThigh.position,
                    leftKnee.position);
                leftLowerLength = Vector3.Distance(
                    leftKnee.position,
                    leftFoot.position);
                rightUpperLength = Vector3.Distance(
                    rightThigh.position,
                    rightKnee.position);
                rightLowerLength = Vector3.Distance(
                    rightKnee.position,
                    rightFoot.position);
                CaptureReferencePose();
                CaptureSwordReferencePose();
            }
        }

        private void CaptureSwordReferencePose()
        {
            if (leftUpperArm == null ||
                rightUpperArm == null ||
                rightLowerArm == null ||
                rightHand == null)
            {
                return;
            }

            if (leftShoulder != null)
            {
                leftShoulderReference =
                    leftShoulder.localRotation;
            }
            leftUpperArmReference =
                leftUpperArm.localRotation;
            if (rightShoulder != null)
            {
                rightShoulderReference =
                    rightShoulder.localRotation;
            }
            rightUpperArmReference =
                rightUpperArm.localRotation;
            rightLowerArmReference =
                rightLowerArm.localRotation;
            rightHandReference =
                rightHand.localRotation;
            hasSwordReferencePose = true;
        }

        private void CaptureReferencePose()
        {
            leftFootReference =
                characterRoot.InverseTransformPoint(
                    leftFoot.position);
            rightFootReference =
                characterRoot.InverseTransformPoint(
                    rightFoot.position);
            leftFootRootRotation =
                Quaternion.Inverse(
                    characterRoot.rotation) *
                leftFoot.rotation;
            rightFootRootRotation =
                Quaternion.Inverse(
                    characterRoot.rotation) *
                rightFoot.rotation;
            hasReferencePose = true;
        }

        private bool HasCompleteRig()
        {
            return animator != null &&
                motor != null &&
                aimPresenter != null &&
                characterRoot != null &&
                hips != null &&
                spine != null &&
                leftThigh != null &&
                leftKnee != null &&
                leftFoot != null &&
                rightThigh != null &&
                rightKnee != null &&
                rightFoot != null;
        }

        private bool SwordEquipped =>
            swordAttackPresenter != null
                ? swordAttackPresenter.WeaponEquipped
                : swordBlockPresenter != null &&
                    swordBlockPresenter.WeaponEquipped;

        private bool BowEquipped =>
            weaponSlots != null &&
            weaponSlots.BowIsEquipped;

        private bool SwordGuardActive =>
            aimPresenter.SwordGuardLocked ||
            (swordBlockPresenter != null &&
                swordBlockPresenter.WeaponEquipped &&
                swordBlockPresenter.IsBlocking);
    }
}
