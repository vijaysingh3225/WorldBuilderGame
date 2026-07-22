using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float walkSpeed = 4.2f;
        [SerializeField, Min(0f)] private float sprintSpeed = 6.1f;
        [SerializeField, Min(0f)] private float acceleration = 24f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField, Min(0f)] private float gravity = 28f;

        private CharacterController controller;
        private PlayerInputSource input;
        private Health health;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private Transform cameraTransform;

        public Vector3 HorizontalVelocity => horizontalVelocity;
        public Vector3 LocalHorizontalVelocity => transform.InverseTransformDirection(horizontalVelocity);
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        public float MaximumSpeed => sprintSpeed;
        public bool IsGrounded => controller != null && controller.isGrounded;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputSource>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (health != null && !health.IsAlive)
            {
                ApplyGravityOnly();
                return;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            PlayerIntent intent = input.CurrentIntent;
            Vector3 desiredDirection = ToWorldDirection(intent.Move);
            float targetSpeed = intent.SprintHeld ? sprintSpeed : walkSpeed;
            Vector3 desiredVelocity = desiredDirection * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, acceleration * Time.deltaTime);

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            UpdateGravity();
            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private Vector3 ToWorldDirection(Vector2 move)
        {
            if (cameraTransform == null)
            {
                return new Vector3(move.x, 0f, move.y);
            }

            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
        }

        private void ApplyGravityOnly()
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, acceleration * Time.deltaTime);
            UpdateGravity();
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void UpdateGravity()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }
        }
    }
}
