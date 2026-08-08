using UnityEngine;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ShortSwordChargePosePresenter : MonoBehaviour
    {
        [SerializeField, Range(0f, 30f)] private float chargeLean = 6f;
        [SerializeField, Range(0f, 30f)] private float chargeTurn = 3f;

        private ShortSwordAttackPresenter attackPresenter;
        private Animator animator;
        private Transform hips;
        private Transform spine;
        private Transform chest;

        private void Awake()
        {
            attackPresenter = GetComponent<ShortSwordAttackPresenter>();
            animator = GetComponent<Animator>();
            ResolveBones();
        }

        private void LateUpdate()
        {
            if (attackPresenter == null || animator == null)
            {
                return;
            }

            if (hips == null)
            {
                ResolveBones();
                if (hips == null)
                {
                    return;
                }
            }

            float chargeWeight = attackPresenter.IsHeavyCharging
                ? Mathf.SmoothStep(
                    0f,
                    1f,
                    attackPresenter.HeavyChargePoseNormalized)
                : 0f;
            // The release immediately hands the sword back to its attack
            // clip, while a small carry keeps the torso driving forward into
            // the dash instead of popping upright on the first swipe frame.
            if (chargeWeight <= 0.001f)
            {
                return;
            }

            float turnTowardSwordArm = chargeTurn * chargeWeight;
            float forwardLean = chargeLean * chargeWeight;
            hips.localRotation *= Quaternion.Euler(
                forwardLean * 0.42f,
                turnTowardSwordArm * 0.35f,
                0f);

            ApplyLocalRotation(spine, forwardLean * 0.42f,
                turnTowardSwordArm * 0.72f);
            ApplyLocalRotation(chest, forwardLean * 0.30f,
                turnTowardSwordArm * 0.28f);
        }

        private void ResolveBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        private static void ApplyLocalRotation(
            Transform bone,
            float pitch,
            float yaw)
        {
            if (bone != null)
            {
                bone.localRotation *= Quaternion.Euler(pitch, yaw, 0f);
            }
        }

    }
}
