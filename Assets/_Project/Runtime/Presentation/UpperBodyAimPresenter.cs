using UnityEngine;
using WorldBuilder.Gameplay.Characters;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class UpperBodyAimPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Camera aimCamera;
        [SerializeField, Range(15f, 85f)] private float maximumYaw = 68f;
        [SerializeField, Range(0.02f, 0.5f)] private float yawSmoothTime = 0.11f;
        [SerializeField, Range(0f, 1f)] private float spineShare = 0.20f;
        [SerializeField, Range(0f, 1f)] private float chestShare = 0.34f;
        [SerializeField, Range(0f, 1f)] private float upperChestShare = 0.46f;
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
        private float idleHandSeparation;
        private bool hasIdleHandSeparation;
        private Quaternion idleHeadRootRotation;
        private bool hasIdleHeadRotation;
        private Quaternion lockedHeadWorldRotation;
        private bool headLockActive;

        public float CurrentYaw => currentYaw;

        public void Configure(
            Animator targetAnimator,
            Transform root,
            Camera targetCamera = null)
        {
            animator = targetAnimator;
            characterRoot = root;
            aimCamera = targetCamera;
            motor = root != null ? root.GetComponent<ThirdPersonMotor>() : null;
            ResolveBones();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            characterRoot ??= GetComponentInParent<WorldBuilder.Gameplay.Characters.ThirdPersonMotor>()
                ?.transform;
            motor ??= characterRoot != null
                ? characterRoot.GetComponent<ThirdPersonMotor>()
                : null;
            ResolveBones();
        }

        private void LateUpdate()
        {
            if (animator == null || characterRoot == null)
            {
                return;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (aimCamera == null)
            {
                return;
            }

            Vector3 up = characterRoot.up;
            Vector3 rootForward =
                Vector3.ProjectOnPlane(characterRoot.forward, up).normalized;
            Vector3 aimForward =
                Vector3.ProjectOnPlane(aimCamera.transform.forward, up).normalized;
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
            ApplyWorldYaw(spine, (currentYaw + locomotionYaw) * spineShare, up);
            ApplyWorldYaw(chest, (currentYaw + locomotionYaw) * chestShare, up);
            ApplyWorldYaw(upperChest, (currentYaw + locomotionYaw) * upperChestShare, up);
            StabilizeHeadRotation(up);
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
                        motor.HorizontalSpeed);
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

        private static void ApplyWorldYaw(Transform bone, float yaw, Vector3 axis)
        {
            if (bone != null && Mathf.Abs(yaw) > 0.001f)
            {
                bone.rotation = Quaternion.AngleAxis(yaw, axis) * bone.rotation;
            }
        }
    }
}
