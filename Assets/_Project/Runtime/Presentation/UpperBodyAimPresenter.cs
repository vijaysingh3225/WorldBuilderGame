using UnityEngine;

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

        private Transform spine;
        private Transform chest;
        private Transform upperChest;
        private float currentYaw;
        private float yawVelocity;

        public float CurrentYaw => currentYaw;

        public void Configure(
            Animator targetAnimator,
            Transform root,
            Camera targetCamera = null)
        {
            animator = targetAnimator;
            characterRoot = root;
            aimCamera = targetCamera;
            ResolveBones();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            characterRoot ??= GetComponentInParent<WorldBuilder.Gameplay.Characters.ThirdPersonMotor>()
                ?.transform;
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

            ApplyWorldYaw(spine, currentYaw * spineShare, up);
            ApplyWorldYaw(chest, currentYaw * chestShare, up);
            ApplyWorldYaw(upperChest, currentYaw * upperChestShare, up);
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
