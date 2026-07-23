using UnityEngine;
using WorldBuilder.Gameplay.Characters;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(100)]
    public sealed class HumanoidAnimatorPresenter : MonoBehaviour
    {
        public const string SpeedParameter = "Speed";
        public const string MoveXParameter = "MoveX";
        public const string MoveZParameter = "MoveZ";
        public const string VerticalSpeedParameter = "VerticalSpeed";
        public const string GroundedParameter = "Grounded";
        public const string CrouchedParameter = "Crouched";

        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);
        private static readonly int MoveXHash = Animator.StringToHash(MoveXParameter);
        private static readonly int MoveZHash = Animator.StringToHash(MoveZParameter);
        private static readonly int VerticalSpeedHash = Animator.StringToHash(VerticalSpeedParameter);
        private static readonly int GroundedHash = Animator.StringToHash(GroundedParameter);
        private static readonly int CrouchedHash = Animator.StringToHash(CrouchedParameter);

        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float locomotionDampTime = 0.1f;

        public void Configure(ThirdPersonMotor movementMotor, Animator targetAnimator)
        {
            motor = movementMotor;
            animator = targetAnimator;

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

#if UNITY_EDITOR
            LocomotionDebugOverlay diagnostics = GetComponent<LocomotionDebugOverlay>();
            if (diagnostics == null)
            {
                diagnostics = gameObject.AddComponent<LocomotionDebugOverlay>();
            }

            diagnostics.Configure(motor, animator);
#endif
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ThirdPersonMotor>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void Update()
        {
            if (motor == null || animator == null)
            {
                return;
            }

            Vector3 localVelocity = motor.LocalHorizontalVelocity;
            animator.SetFloat(SpeedHash, motor.HorizontalSpeed, locomotionDampTime, Time.deltaTime);
            animator.SetFloat(MoveXHash, localVelocity.x, locomotionDampTime, Time.deltaTime);
            animator.SetFloat(MoveZHash, localVelocity.z, locomotionDampTime, Time.deltaTime);
            animator.SetFloat(VerticalSpeedHash, motor.VerticalVelocity);
            animator.SetBool(GroundedHash, motor.IsGrounded);
            animator.SetBool(CrouchedHash, motor.IsCrouched);
        }
    }
}
