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
        public const float SwordRunIntensityStart = 0.25f;
        public const float SwordRunIntensityFull = 0.90f;

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
        [SerializeField, Range(0f, 12f)] private float swordWalkLean = 5.5f;
        [SerializeField, Range(4f, 18f)] private float swordRunLean = 11f;
        [SerializeField, Range(0f, 15f)] private float swordShoulderClose = 6f;
        [SerializeField, Range(0f, 20f)] private float swordArmBack = 11f;
        [SerializeField, Range(0f, 1f)] private float swordWalkArmStability = 0.72f;
        [SerializeField, Range(0f, 1f)] private float swordRunArmStability = 0.92f;
        [SerializeField, Range(0f, 10f)] private float swordRunLeanBoost = 5f;
        [SerializeField, Range(0f, 10f)] private float swordRunShoulderCloseBoost = 5f;
        [SerializeField, Range(0f, 15f)] private float swordRunArmOut = 9f;
        [SerializeField, Min(0.1f)] private float swordRunPoseBlendDuration = 0.28f;

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
        private Transform rightThigh;
        private float bowWeight;
        private float swordReadyWeight;
        private float swordRunPoseWeight;
        private float alertMovementWeight;
        private float currentBowYaw;
        private float gaitPlayback = 1f;
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
                    bowWeight));
        public float BowStanceWeight => bowWeight;
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
            float playbackMagnitude =
                motor.WalkGaitPlaybackScale;
            float targetPlayback = playbackMagnitude;
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
                        ? -playbackMagnitude
                        : playbackMagnitude;
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
            bowWeight = MoveWeight(
                bowWeight,
                bowActive ? 1f : 0f);
            bool swordReadyActive =
                SwordEquipped &&
                !SwordGuardActive &&
                !aimPresenter.BowAimLocked &&
                motor.IsGrounded &&
                !motor.IsCrouched &&
                motor.HorizontalSpeed > 0.08f;
            bool alertMovementActive =
                (SwordEquipped || BowEquipped) &&
                !SwordGuardActive &&
                !aimPresenter.BowAimLocked &&
                motor.IsGrounded &&
                !motor.IsCrouched &&
                motor.HorizontalSpeed > 0.08f;
            float attackPresentationWeight =
                swordAttackPresenter != null
                    ? swordAttackPresenter.PresentationWeight
                    : 0f;
            float swordLocomotionWeight =
                1f - attackPresentationWeight;
            alertMovementWeight = MoveWeight(
                alertMovementWeight,
                alertMovementActive
                    ? SwordEquipped
                        ? swordLocomotionWeight
                        : 1f
                    : 0f);
            swordReadyWeight = MoveWeight(
                swordReadyWeight,
                swordReadyActive
                    ? swordLocomotionWeight
                    : 0f);
            float targetSwordRunPose =
                SwordEquipped &&
                motor.IsGrounded &&
                !motor.IsCrouched &&
                motor.HorizontalSpeed > 0.08f
                    ? CalculateSwordRunIntensity(
                        Mathf.InverseLerp(
                            motor.WalkSpeed,
                            motor.SprintSpeed,
                            motor.HorizontalSpeed))
                    : 0f;
            swordRunPoseWeight = Mathf.MoveTowards(
                swordRunPoseWeight,
                targetSwordRunPose,
                Time.deltaTime /
                    Mathf.Max(
                        0.1f,
                        swordRunPoseBlendDuration));

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
                float shoulderMagnitude = Mathf.Abs(
                    aimPresenter.CurrentShoulderSideBlend);
                currentBowYaw = shoulderMagnitude < 0.999f
                    ? targetYaw
                    : Mathf.MoveTowardsAngle(
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

            ApplySwordGuardTravelFacing();

        }

        private void ApplySwordGuardTravelFacing()
        {
            if (!SwordGuardActive ||
                !motor.IsGrounded ||
                swordBlockPresenter == null ||
                swordBlockPresenter.BlockWeight <= 0.001f)
            {
                return;
            }

            Vector3 localTravel = motor.LocalHorizontalVelocity;
            localTravel.y = 0f;
            if (localTravel.sqrMagnitude <= 0.0064f)
            {
                return;
            }

            // The guard layer and upper-body aim remain facing the threat.
            // Only rotate the hips; restoring the spine's world rotation
            // leaves the torso and sword locked on the aiming direction.
            Quaternion preservedSpineRotation = spine.rotation;
            float travelYaw = CalculateGuardTravelYaw(localTravel);
            hips.rotation = Quaternion.AngleAxis(
                travelYaw * swordBlockPresenter.BlockWeight,
                characterRoot.up) *
                hips.rotation;
            spine.rotation = preservedSpineRotation;
        }

        private float GetBowYaw(Vector3 localVelocity)
        {
            float total =
                Mathf.Abs(localVelocity.x) +
                Mathf.Abs(localVelocity.z);
            float forwardBias = total > 0.02f
                ? Mathf.Abs(localVelocity.z) / total
                : 0f;
            return CalculateShoulderSynchronizedBowYaw(
                Mathf.Lerp(
                    bowSideYaw,
                    bowCrossStepYaw,
                    forwardBias),
                aimPresenter.CurrentShoulderSideBlend);
        }

        public static float CalculateShoulderSynchronizedBowYaw(
            float canonicalYaw,
            float shoulderSideBlend)
        {
            return canonicalYaw * Mathf.Abs(Mathf.Clamp(
                shoulderSideBlend,
                -1f,
                1f));
        }

        public static float CalculateGuardTravelYaw(Vector3 localTravel)
        {
            Vector3 planarTravel = Vector3.ProjectOnPlane(
                localTravel,
                Vector3.up);
            if (planarTravel.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(
                planarTravel.x,
                planarTravel.z) * Mathf.Rad2Deg;
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
            float runIntensity =
                swordRunPoseWeight *
                swordReadyWeight;
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
            Quaternion armOutRotation =
                Quaternion.AngleAxis(
                    swordRunArmOut * runIntensity,
                    characterRoot.forward);
            Quaternion rightUpperArmTarget =
                armOutRotation *
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
            Transform sword = weaponSlots != null
                ? weaponSlots.PrimaryWeaponRoot
                : null;
            if (sword != null &&
                sword.gameObject.activeInHierarchy &&
                runIntensity > 0.001f)
            {
                Quaternion swordInHand =
                    Quaternion.Inverse(rightHand.rotation) *
                    sword.rotation;
                Vector3 predictedBladeDirection =
                    rightHandTarget *
                    swordInHand *
                    Vector3.up;
                Vector3 brandishDirection =
                    CalculateSwordRunBrandishDirection(
                        characterRoot.right,
                        characterRoot.up,
                        characterRoot.forward);
                Quaternion brandishCorrection =
                    Quaternion.FromToRotation(
                        predictedBladeDirection,
                        brandishDirection);
                rightHandTarget =
                    Quaternion.Slerp(
                        Quaternion.identity,
                        brandishCorrection,
                        runIntensity) *
                    rightHandTarget;
            }
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
            float swordRunIntensity = SwordEquipped
                ? swordRunPoseWeight
                : 0f;
            float lean = CalculateAlertLean(
                gait,
                Mathf.Max(AlertWalkLean, swordWalkLean),
                Mathf.Max(AlertRunLean, swordRunLean)) *
                alertMovementWeight;
            lean +=
                swordRunLeanBoost *
                swordRunIntensity *
                alertMovementWeight;
            float shoulderClose = Mathf.Lerp(
                Mathf.Max(
                    AlertWalkShoulderClose,
                    swordShoulderClose),
                AlertRunShoulderClose,
                gait) * alertMovementWeight;
            shoulderClose +=
                swordRunShoulderCloseBoost *
                swordRunIntensity *
                alertMovementWeight;
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

        public static float CalculateSwordRunIntensity(float gait)
        {
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    SwordRunIntensityStart,
                    SwordRunIntensityFull,
                    gait));
        }

        public static Vector3 CalculateSwordRunBrandishDirection(
            Vector3 characterRight,
            Vector3 characterUp,
            Vector3 characterForward)
        {
            return (
                characterRight * 0.32f +
                characterUp * 0.10f +
                characterForward * 1f).normalized;
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
            rightThigh = animator.GetBoneTransform(
                HumanBodyBones.RightUpperLeg);
            if (HasCompleteRig())
            {
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

        private bool HasCompleteRig()
        {
            return animator != null &&
                motor != null &&
                aimPresenter != null &&
                characterRoot != null &&
                hips != null &&
                spine != null &&
                leftThigh != null &&
                rightThigh != null;
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
