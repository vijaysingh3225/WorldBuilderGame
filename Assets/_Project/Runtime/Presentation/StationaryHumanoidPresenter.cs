using UnityEngine;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class StationaryHumanoidPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
            ApplyStationaryPose();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            ApplyStationaryPose();
        }

        private void OnEnable()
        {
            ApplyStationaryPose();
        }

        private void ApplyStationaryPose()
        {
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
            animator.SetFloat(HumanoidAnimatorPresenter.SpeedParameter, 0f);
            animator.SetFloat(HumanoidAnimatorPresenter.MoveXParameter, 0f);
            animator.SetFloat(HumanoidAnimatorPresenter.MoveZParameter, 0f);
            animator.SetFloat(HumanoidAnimatorPresenter.VerticalSpeedParameter, 0f);
            animator.SetBool(HumanoidAnimatorPresenter.GroundedParameter, true);
            animator.SetBool(HumanoidAnimatorPresenter.CrouchedParameter, false);

            SetLayerWeight(ShortSwordAttackPresenter.AttackLayerName, 0f);
            SetLayerWeight("Short Sword Ready", 0f);
        }

        private void SetLayerWeight(string layerName, float weight)
        {
            int layerIndex = animator.GetLayerIndex(layerName);
            if (layerIndex >= 0)
            {
                animator.SetLayerWeight(layerIndex, weight);
            }
        }
    }
}
