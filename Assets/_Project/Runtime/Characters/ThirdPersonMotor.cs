using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float walkSpeed = 3.4f;
        [SerializeField, Min(0f)] private float sprintSpeed = 6.1f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.8f;
        [SerializeField, Min(0f)] private float acceleration = 24f;
        [SerializeField, Min(0f)] private float airAcceleration = 14f;
        [SerializeField, Min(0f)] private float standingJumpAirSpeedLimit = 2.2f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField, Min(0f)] private float gravity = 28f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.35f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(1f)] private float jumpReleaseGravityMultiplier = 2.35f;
        [SerializeField, Min(0.9f)] private float crouchingHeight = 1.2f;
        [SerializeField, Min(0.1f)] private float crouchTransitionSpeed = 5.5f;
        [SerializeField] private LayerMask overheadObstructionMask = ~(1 << 2);
        [SerializeField] private LayerMask groundSupportMask = ~(1 << 2);
        [SerializeField, Min(0.05f)] private float groundProbeRadius = 0.18f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.15f;

        private CharacterController controller;
        private PlayerInputSource input;
        private Health health;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private Transform cameraTransform;
        private float standingHeight;
        private Vector3 standingCenter;
        private Vector3 crouchingCenter;
        private float lastGroundedTime = float.NegativeInfinity;
        private float lastJumpRequestedTime = float.NegativeInfinity;
        private float airborneSpeedLimit;
        private bool isGrounded;
        private bool isCrouched;

        public Vector3 HorizontalVelocity => horizontalVelocity;
        public Vector3 LocalHorizontalVelocity => transform.InverseTransformDirection(horizontalVelocity);
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        public float MaximumSpeed => sprintSpeed;
        public float VerticalVelocity => verticalVelocity;
        public float CrouchAmount => controller == null || Mathf.Approximately(standingHeight, crouchingHeight)
            ? 0f
            : Mathf.Clamp01((standingHeight - controller.height) / (standingHeight - crouchingHeight));
        public bool IsGrounded => isGrounded;
        public bool IsCrouched => isCrouched;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputSource>();
            health = GetComponent<Health>();
            standingHeight = controller.height;
            standingCenter = controller.center;
            crouchingHeight = Mathf.Clamp(crouchingHeight, controller.radius * 2f, standingHeight);
            crouchingCenter = standingCenter + Vector3.down * ((standingHeight - crouchingHeight) * 0.5f);
            airborneSpeedLimit = standingJumpAirSpeedLimit;
            isGrounded = HasSupportedGroundContact();
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
            UpdateCrouch(intent.CrouchHeld);
            Vector3 desiredDirection = ToWorldDirection(intent.Move);
            bool hasGroundControl = HasSupportedGroundContact();
            if (hasGroundControl)
            {
                float targetSpeed = isCrouched ? crouchSpeed : intent.SprintHeld ? sprintSpeed : walkSpeed;
                Vector3 desiredVelocity = desiredDirection * targetSpeed;
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    desiredVelocity,
                    acceleration * Time.deltaTime);
                airborneSpeedLimit = Mathf.Max(standingJumpAirSpeedLimit, horizontalVelocity.magnitude);
            }
            else if (desiredDirection.sqrMagnitude > 0.001f)
            {
                Vector3 desiredAirVelocity = desiredDirection * airborneSpeedLimit;
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    desiredAirVelocity,
                    airAcceleration * Time.deltaTime);
            }

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            UpdateVerticalMotion(intent);
            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
            isGrounded = HasSupportedGroundContact();
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
            UpdatePassiveGravity();
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            isGrounded = HasSupportedGroundContact();
        }

        private void UpdateVerticalMotion(PlayerIntent intent)
        {
            bool groundedBeforeMove = HasSupportedGroundContact();
            if (groundedBeforeMove)
            {
                lastGroundedTime = Time.time;
            }

            if (intent.JumpPressed)
            {
                lastJumpRequestedTime = Time.time;
            }

            bool hasBufferedJump = Time.time - lastJumpRequestedTime <= jumpBufferTime;
            bool hasGroundGrace = Time.time - lastGroundedTime <= coyoteTime;
            if (!isCrouched && hasBufferedJump && hasGroundGrace)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                lastJumpRequestedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                isGrounded = false;
                return;
            }

            if (groundedBeforeMove && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                return;
            }

            float gravityMultiplier = !intent.JumpHeld && verticalVelocity > 0f
                ? jumpReleaseGravityMultiplier
                : 1f;
            verticalVelocity = Mathf.Max(-45f, verticalVelocity - gravity * gravityMultiplier * Time.deltaTime);
        }

        private void UpdatePassiveGravity()
        {
            if (HasSupportedGroundContact() && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity = Mathf.Max(-45f, verticalVelocity - gravity * Time.deltaTime);
            }
        }

        private void UpdateCrouch(bool crouchHeld)
        {
            if (crouchHeld)
            {
                isCrouched = true;
            }
            else if (isCrouched && CanStand())
            {
                isCrouched = false;
            }

            float targetHeight = isCrouched ? crouchingHeight : standingHeight;
            Vector3 targetCenter = isCrouched ? crouchingCenter : standingCenter;
            controller.height = Mathf.MoveTowards(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            controller.center = Vector3.MoveTowards(controller.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
        }

        private bool CanStand()
        {
            float radius = controller.radius * 0.95f;
            Vector3 worldCenter = transform.TransformPoint(standingCenter);
            float halfSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
            Vector3 bottom = worldCenter - Vector3.up * halfSegment + Vector3.up * 0.03f;
            Vector3 top = worldCenter + Vector3.up * halfSegment;
            return !Physics.CheckCapsule(bottom, top, radius, overheadObstructionMask, QueryTriggerInteraction.Ignore);
        }

        private bool HasSupportedGroundContact()
        {
            if (!controller.isGrounded)
            {
                return false;
            }

            Vector3 worldCenter = transform.TransformPoint(controller.center);
            Vector3 controllerBottom = worldCenter - Vector3.up * (controller.height * 0.5f);
            Vector3 probeOrigin = controllerBottom + Vector3.up * (groundProbeRadius + 0.05f);
            if (!Physics.SphereCast(
                    probeOrigin,
                    groundProbeRadius,
                    Vector3.down,
                    out RaycastHit hit,
                    groundProbeDistance,
                    groundSupportMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.normal.y >= 0.55f;
        }
    }
}
