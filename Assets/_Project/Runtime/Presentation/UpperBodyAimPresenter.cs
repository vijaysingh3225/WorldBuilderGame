using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class UpperBodyAimPresenter :
        MonoBehaviour,
        ICharacterFacingOverride
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private CameraAimTarget aimTarget;
        [SerializeField] private CharacterAimSource characterAimSource;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField] private ShortSwordBlockPresenter blockPresenter;
        [SerializeField, Range(15f, 85f)] private float maximumYaw = 68f;
        [SerializeField, Range(0.02f, 0.5f)] private float yawSmoothTime = 0.11f;
        [SerializeField, Range(0f, 1f)] private float spineShare = 0.20f;
        [SerializeField, Range(0f, 1f)] private float chestShare = 0.34f;
        [SerializeField, Range(0f, 1f)] private float upperChestShare = 0.46f;
        [SerializeField, Range(45f, 90f)]
        private float fullDrawTorsoYaw = 78f;
        [SerializeField, Range(0.02f, 0.3f)]
        private float bowTorsoYawSmoothTime = 0.08f;
        [SerializeField, Range(0f, 1f)]
        private float bowHeadCounterRotation = 1f;
        [SerializeField, Range(0f, 20f)] private float walkTorsoYaw = 12f;
        [SerializeField, Range(0f, 24f)] private float runTorsoYaw = 18f;
        [SerializeField, Range(0f, 1f)] private float locomotionHeadLock = 1f;
        [SerializeField, Min(90f)] private float headLockAngularSpeed = 360f;

        private Transform spine;
        private Transform chest;
        private Transform upperChest;
        private Transform head;
        private Transform leftHand;
        private Transform rightHand;
        private ThirdPersonMotor motor;
        private float currentYaw;
        private float yawVelocity;
        private float locomotionYaw;
        private float locomotionYawVelocity;
        private float bowDrawTorsoYaw;
        private float bowDrawTorsoYawVelocity;
        private float idleHandSeparation;
        private bool hasIdleHandSeparation;
        private Quaternion idleHeadRootRotation;
        private bool hasIdleHeadRotation;
        private Quaternion lockedHeadWorldRotation;
        private bool headLockActive;
        private AimStanceLocomotionPresenter stancePresenter;

        public float CurrentYaw => currentYaw;
        public float MaximumYaw => maximumYaw;
        public float BowDrawTorsoYaw => bowDrawTorsoYaw;

        public Vector3 PredictFullDrawHeadPosition()
        {
            if (head == null || characterRoot == null)
            {
                return head != null
                    ? head.position
                    : transform.position;
            }

            Quaternion spineRotation =
                spine != null
                    ? spine.rotation
                    : Quaternion.identity;
            Quaternion chestRotation =
                chest != null
                    ? chest.rotation
                    : Quaternion.identity;
            Quaternion upperChestRotation =
                upperChest != null
                    ? upperChest.rotation
                    : Quaternion.identity;
            Quaternion headRotation = head.rotation;
            float remainingBowYaw =
                fullDrawTorsoYaw - bowDrawTorsoYaw;
            Vector3 up = characterRoot.up;
            ApplyWorldYaw(
                spine,
                remainingBowYaw * spineShare,
                up);
            ApplyWorldYaw(
                chest,
                remainingBowYaw * chestShare,
                up);
            ApplyWorldYaw(
                upperChest,
                remainingBowYaw * upperChestShare,
                up);
            Vector3 predictedPosition = head.position;

            if (spine != null)
            {
                spine.rotation = spineRotation;
            }

            if (chest != null)
            {
                chest.rotation = chestRotation;
            }

            if (upperChest != null)
            {
                upperChest.rotation = upperChestRotation;
            }

            head.rotation = headRotation;
            return predictedPosition;
        }

        public bool BowAimLocked =>
            bowWeapon != null &&
            bowWeapon.WeaponEquipped &&
            input != null &&
            input.CurrentIntent.BlockHeld;
        public bool SwordGuardLocked =>
            blockPresenter != null &&
            blockPresenter.WeaponEquipped &&
            input != null &&
            input.CurrentIntent.BlockHeld;
        public bool AimLocked
        {
            get
            {
                return BowAimLocked || SwordGuardLocked;
            }
        }

        public void Configure(
            Animator targetAnimator,
            Transform root,
            Camera targetCamera = null)
        {
            animator = targetAnimator;
            characterRoot = root;
            characterAimSource =
                root != null
                    ? root.GetComponent<CharacterAimSource>()
                    : null;
            aimCamera = targetCamera;
            aimTarget ??=
                FindFirstObjectByType<CameraAimTarget>();
            motor = root != null ? root.GetComponent<ThirdPersonMotor>() : null;
            ResolveCombatReferences();
            ResolveBones();
            EnsureStancePresenter();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            aimTarget ??=
                FindFirstObjectByType<CameraAimTarget>();
            characterRoot ??= GetComponentInParent<WorldBuilder.Gameplay.Characters.ThirdPersonMotor>()
                ?.transform;
            characterAimSource ??=
                characterRoot != null
                    ? characterRoot.GetComponent<CharacterAimSource>()
                    : GetComponentInParent<CharacterAimSource>();
            motor ??= characterRoot != null
                ? characterRoot.GetComponent<ThirdPersonMotor>()
                : null;
            ResolveCombatReferences();
            ResolveBones();
            EnsureStancePresenter();
        }

        public bool TryGetFacingDirection(
            out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;
            if (characterAimSource != null &&
                characterAimSource.OverrideActive)
            {
                worldDirection = Vector3.ProjectOnPlane(
                    characterAimSource.Direction,
                    Vector3.up);
                return worldDirection.sqrMagnitude > 0.001f;
            }

            if (characterAimSource != null &&
                !characterAimSource.CameraFallbackAllowed)
            {
                return false;
            }

            if (aimTarget != null &&
                aimTarget.InspectionOrbitActive)
            {
                worldDirection =
                    aimTarget.InspectionFacingDirection;
                return worldDirection.sqrMagnitude > 0.001f;
            }

            if (!AimLocked)
            {
                return false;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (aimCamera == null)
            {
                return false;
            }

            worldDirection = Vector3.ProjectOnPlane(
                aimCamera.transform.forward,
                Vector3.up);
            return worldDirection.sqrMagnitude > 0.001f;
        }

        private void LateUpdate()
        {
            if (animator == null || characterRoot == null)
            {
                return;
            }

            if (characterAimSource != null &&
                !characterAimSource.OverrideActive &&
                !characterAimSource.CameraFallbackAllowed)
            {
                currentYaw = 0f;
                yawVelocity = 0f;
                locomotionYaw = 0f;
                locomotionYawVelocity = 0f;
                bowDrawTorsoYaw = 0f;
                bowDrawTorsoYawVelocity = 0f;
                return;
            }

            if ((characterAimSource == null ||
                 !characterAimSource.OverrideActive) &&
                aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if ((characterAimSource == null ||
                 !characterAimSource.OverrideActive) &&
                aimCamera == null)
            {
                return;
            }

            Vector3 up = characterRoot.up;
            Vector3 rootForward =
                Vector3.ProjectOnPlane(characterRoot.forward, up).normalized;
            Vector3 aimDirection =
                characterAimSource != null &&
                characterAimSource.OverrideActive
                    ? characterAimSource.Direction
                    : aimTarget != null &&
                      aimTarget.InspectionOrbitActive
                        ? aimTarget.AimDirection
                        : aimCamera.transform.forward;
            Vector3 aimForward =
                Vector3.ProjectOnPlane(
                    aimDirection,
                    up).normalized;
            if (rootForward.sqrMagnitude < 0.9f || aimForward.sqrMagnitude < 0.9f)
            {
                return;
            }

            float targetYaw = Mathf.Clamp(
                Vector3.SignedAngle(rootForward, aimForward, up),
                -maximumYaw,
                maximumYaw);
            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawVelocity,
                yawSmoothTime);

            UpdateLocomotionYaw(rootForward);
            UpdateBowDrawTorsoYaw();
            float sharedYaw =
                currentYaw +
                locomotionYaw +
                bowDrawTorsoYaw;
            ApplyWorldYaw(spine, sharedYaw * spineShare, up);
            ApplyWorldYaw(chest, sharedYaw * chestShare, up);
            ApplyWorldYaw(
                upperChest,
                sharedYaw * upperChestShare,
                up);
            ApplyWorldYaw(
                head,
                -bowDrawTorsoYaw * bowHeadCounterRotation,
                up);
            StabilizeHeadRotation(up);
        }

        private void UpdateBowDrawTorsoYaw()
        {
            float drawProgress =
                BowAimLocked && bowWeapon != null
                    ? bowWeapon.DrawNormalized
                    : 0f;
            float easedDraw =
                drawProgress *
                drawProgress *
                (3f - 2f * drawProgress);
            float targetYaw =
                fullDrawTorsoYaw * easedDraw;
            bowDrawTorsoYaw = Mathf.SmoothDampAngle(
                bowDrawTorsoYaw,
                targetYaw,
                ref bowDrawTorsoYawVelocity,
                bowTorsoYawSmoothTime);
        }

        private void UpdateLocomotionYaw(Vector3 rootForward)
        {
            float targetYaw = 0f;
            if (motor != null && leftHand != null && rightHand != null)
            {
                float handSeparation = Vector3.Dot(
                    rightHand.position - leftHand.position,
                    rootForward);
                bool standingStill = motor.HorizontalSpeed < 0.1f;
                if (standingStill && motor.IsGrounded)
                {
                    idleHandSeparation = handSeparation;
                    hasIdleHandSeparation = true;
                    if (head != null && !hasIdleHeadRotation)
                    {
                        idleHeadRootRotation =
                            Quaternion.Inverse(characterRoot.rotation) *
                            Quaternion.Inverse(Quaternion.AngleAxis(currentYaw, characterRoot.up)) *
                            head.rotation;
                        hasIdleHeadRotation = true;
                    }
                }

                bool canSway =
                    hasIdleHandSeparation &&
                    !AimLocked &&
                    motor.IsGrounded &&
                    !motor.IsCrouched &&
                    !IsAttacking() &&
                    motor.HorizontalSpeed >= 0.1f;
                if (canSway)
                {
                    float armSwing = Mathf.Clamp(
                        (handSeparation - idleHandSeparation) / 0.35f,
                        -1f,
                        1f);
                    float locomotionSpeedBlend = Mathf.InverseLerp(
                        ThirdPersonMotor.DefaultWalkSpeed,
                        ThirdPersonMotor.DefaultSprintSpeed,
                        motor.AnimationHorizontalSpeed);
                    float maximumYaw = Mathf.Lerp(
                        walkTorsoYaw,
                        runTorsoYaw,
                        locomotionSpeedBlend);
                    // A forward right hand brings the right shoulder forward, which
                    // is a small turn to the character's left.
                    targetYaw = -armSwing * maximumYaw;
                }
            }

            locomotionYaw = Mathf.SmoothDampAngle(
                locomotionYaw,
                targetYaw,
                ref locomotionYawVelocity,
                0.06f);
        }

        private void StabilizeHeadRotation(Vector3 up)
        {
            bool shouldLock =
                head != null &&
                hasIdleHeadRotation &&
                !AimLocked &&
                motor != null &&
                motor.IsGrounded &&
                !motor.IsCrouched &&
                !IsAttacking() &&
                motor.HorizontalSpeed >= 0.1f;
            if (!shouldLock)
            {
                headLockActive = false;
                return;
            }

            Quaternion stableRotation =
                Quaternion.AngleAxis(currentYaw, up) *
                characterRoot.rotation *
                idleHeadRootRotation;
            if (!headLockActive)
            {
                lockedHeadWorldRotation = head.rotation;
                headLockActive = true;
            }

            lockedHeadWorldRotation = Quaternion.RotateTowards(
                lockedHeadWorldRotation,
                stableRotation,
                headLockAngularSpeed * Time.deltaTime);
            head.rotation = Quaternion.Slerp(
                head.rotation,
                lockedHeadWorldRotation,
                locomotionHeadLock);
        }

        private bool IsAttacking()
        {
            int attackLayer =
                animator.GetLayerIndex(ShortSwordAttackPresenter.AttackLayerName);
            return attackLayer >= 0 && animator.GetLayerWeight(attackLayer) > 0.01f;
        }

        private void ResolveBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void ResolveCombatReferences()
        {
            input ??=
                characterRoot != null
                    ? characterRoot.GetComponent<PlayerInputSource>()
                    : GetComponentInParent<PlayerInputSource>();
            bowWeapon ??= GetComponent<BowWeapon>();
            blockPresenter ??=
                GetComponent<ShortSwordBlockPresenter>();
        }

        private void EnsureStancePresenter()
        {
            stancePresenter ??=
                GetComponent<AimStanceLocomotionPresenter>();
            if (stancePresenter == null)
            {
                stancePresenter =
                    gameObject.AddComponent<
                        AimStanceLocomotionPresenter>();
            }

            stancePresenter.Configure(
                animator,
                motor,
                this);
        }

        private static void ApplyWorldYaw(Transform bone, float yaw, Vector3 axis)
        {
            if (bone != null && Mathf.Abs(yaw) > 0.001f)
            {
                bone.rotation = Quaternion.AngleAxis(yaw, axis) * bone.rotation;
            }
        }
    }
}
