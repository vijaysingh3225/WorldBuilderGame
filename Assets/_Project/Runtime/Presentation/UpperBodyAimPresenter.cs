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

        private Transform spine;
        private Transform chest;
        private Transform upperChest;
        private Transform head;
        private ThirdPersonMotor motor;
        private float currentYaw;
        private float yawVelocity;
        private float bowDrawTorsoYaw;
        private float bowDrawTorsoYawVelocity;
        private AimStanceLocomotionPresenter stancePresenter;
        private bool presentationAimOverrideActive;
        private Vector3 presentationAimDirection;

        public float CurrentYaw => currentYaw;
        public float MaximumYaw => maximumYaw;
        public float BowDrawTorsoYaw => bowDrawTorsoYaw;
        public float CurrentShoulderSideBlend =>
            aimTarget != null
                ? aimTarget.CurrentShoulderSideBlend
                : 1f;
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
                fullDrawTorsoYaw *
                    Mathf.Abs(CurrentShoulderSideBlend) -
                bowDrawTorsoYaw;
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
            bowWeapon.DrawInputHeld;
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

        // Presentation-only aim is for ambient behavior such as a guard
        // scanning while talking. Unlike CharacterAimSource, it never drives
        // the motor's root-facing override.
        public void SetPresentationAimDirection(Vector3 direction)
        {
            presentationAimDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            presentationAimOverrideActive =
                direction.sqrMagnitude > 0.0001f;
        }

        public void ClearPresentationAimDirection()
        {
            presentationAimOverrideActive = false;
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
                !characterAimSource.CameraFallbackAllowed &&
                !presentationAimOverrideActive)
            {
                currentYaw = 0f;
                yawVelocity = 0f;
                bowDrawTorsoYaw = 0f;
                bowDrawTorsoYawVelocity = 0f;
                return;
            }

            if ((characterAimSource == null ||
                 !characterAimSource.OverrideActive) &&
                !presentationAimOverrideActive &&
                aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if ((characterAimSource == null ||
                 !characterAimSource.OverrideActive) &&
                !presentationAimOverrideActive &&
                aimCamera == null)
            {
                return;
            }

            Vector3 up = characterRoot.up;
            Vector3 rootForward =
                Vector3.ProjectOnPlane(characterRoot.forward, up).normalized;
            Vector3 aimDirection =
                presentationAimOverrideActive
                    ? presentationAimDirection
                    : characterAimSource != null &&
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

            UpdateBowDrawTorsoYaw();
            float sharedYaw = CalculateShoulderCompensatedAimYaw(
                currentYaw,
                CurrentShoulderSideBlend) +
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
        }

        private void UpdateBowDrawTorsoYaw()
        {
            float drawProgress =
                BowAimLocked && bowWeapon != null
                    ? bowWeapon.DrawNormalized
                    : 0f;
            float targetYaw = CalculateBowTorsoYaw(
                fullDrawTorsoYaw,
                drawProgress) *
                Mathf.Abs(CurrentShoulderSideBlend);
            bowDrawTorsoYaw = Mathf.SmoothDampAngle(
                bowDrawTorsoYaw,
                targetYaw,
                ref bowDrawTorsoYawVelocity,
                bowTorsoYawSmoothTime);
        }

        public static float CalculateBowTorsoYaw(
            float fullDrawYaw,
            float drawProgress)
        {
            float clampedDraw = Mathf.Clamp01(drawProgress);
            float easedDraw = clampedDraw * clampedDraw *
                (3f - 2f * clampedDraw);
            return fullDrawYaw * easedDraw;
        }

        public static float CalculateShoulderCompensatedAimYaw(
            float aimYaw,
            float shoulderSideBlend)
        {
            return aimYaw * Mathf.Clamp(
                shoulderSideBlend,
                -1f,
                1f);
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
