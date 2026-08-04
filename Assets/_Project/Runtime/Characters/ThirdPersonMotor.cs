using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Characters
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        public const float DefaultWalkSpeed = 1.85f;
        public const float PlayerWalkSpeedMultiplier = 1.5f;
        public const float DefaultPlayerWalkSpeed =
            DefaultWalkSpeed * PlayerWalkSpeedMultiplier;
        public const float DefaultJogSpeed = 3.1f;
        public const float DefaultSprintSpeed = 4.6f;
        public const float DefaultCrouchSpeed = 1.0f;

        [SerializeField, Min(0f)] private float walkSpeed = DefaultWalkSpeed;
        [SerializeField, Min(0f)] private float sprintSpeed = DefaultSprintSpeed;
        [SerializeField, Min(0f)] private float crouchSpeed = DefaultCrouchSpeed;
        [SerializeField, Min(0f)] private float acceleration = 24f;
        [SerializeField, Min(0f)] private float airAcceleration = 14f;
        [SerializeField, Min(0f)] private float standingJumpAirSpeedLimit = 2.2f;
        [SerializeField, Min(0f)] private float turnSpeed = 360f;
        [SerializeField, Min(0f)] private float aimFacingTurnSpeed = 720f;
        [SerializeField, Range(-1f, 0f)] private float reversalBrakeDot = -0.35f;
        [SerializeField, Range(0f, 90f)] private float reversalRestartAngle = 38f;
        [SerializeField, Min(0f)] private float reversalStopSpeed = 0.08f;
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
        private bool isBrakingForReversal;
        private bool hasGroundControl;
        private Vector3 desiredWorldDirection;
        private float targetHorizontalSpeed;
        private float runtimeSpeedBonus;
        private MonoBehaviour[] facingOverrideBehaviours;
        private bool movementCameraBasisLocked;
        private Vector3 lockedMovementCameraForward;
        private Vector3 lockedMovementCameraRight;
        private bool movementCameraUnlockPending;

        public Vector3 HorizontalVelocity => horizontalVelocity;
        public Vector3 LocalHorizontalVelocity => transform.InverseTransformDirection(horizontalVelocity);
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        public float MaximumSpeed =>
            sprintSpeed + runtimeSpeedBonus;
        public float VerticalVelocity => verticalVelocity;
        public float CrouchAmount => controller == null || Mathf.Approximately(standingHeight, crouchingHeight)
            ? 0f
            : Mathf.Clamp01((standingHeight - controller.height) / (standingHeight - crouchingHeight));
        public bool IsGrounded => isGrounded;
        public bool IsCrouched => isCrouched;
        public bool HasGroundControl => hasGroundControl;
        public bool IsBrakingForReversal => isBrakingForReversal;
        public Vector3 DesiredWorldDirection => desiredWorldDirection;
        public float TargetHorizontalSpeed => targetHorizontalSpeed;
        public float CharacterHeight => controller != null ? controller.height : 0f;
        public float AccelerationRate => acceleration;
        public float AirAccelerationRate => airAcceleration;
        public float TurnSpeed => turnSpeed;
        public float WalkSpeed =>
            walkSpeed + runtimeSpeedBonus;
        public float AnimationHorizontalSpeed =>
            UsesWalkGait
                ? HorizontalSpeed *
                    DefaultWalkSpeed /
                    Mathf.Max(0.001f, WalkSpeed)
                : HorizontalSpeed;
        public float WalkGaitPlaybackScale =>
            UsesWalkGait
                ? WalkSpeed /
                    Mathf.Max(0.001f, DefaultWalkSpeed)
                : 1f;
        public float SprintSpeed =>
            sprintSpeed + runtimeSpeedBonus;
        public float CrouchSpeed =>
            crouchSpeed + runtimeSpeedBonus;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float ReversalBrakeDot => reversalBrakeDot;
        public float ReversalRestartAngle => reversalRestartAngle;
        public float CrouchTransitionSpeed => crouchTransitionSpeed;
        public bool MovementCameraBasisLocked =>
            movementCameraBasisLocked;

        private bool UsesWalkGait
        {
            get
            {
                if (isCrouched || WalkSpeed <= 0.001f)
                {
                    return false;
                }

                bool targetingWalk =
                    targetHorizontalSpeed > 0.001f &&
                    Mathf.Abs(
                        targetHorizontalSpeed - WalkSpeed) <= 0.05f;
                bool coastingFromWalk =
                    targetHorizontalSpeed <= 0.001f &&
                    HorizontalSpeed > 0.03f &&
                    HorizontalSpeed <= WalkSpeed + 0.05f;
                return targetingWalk || coastingFromWalk;
            }
        }

        public void ConfigureWalkSpeed(float speed)
        {
            walkSpeed = Mathf.Max(0f, speed);
        }

        public void SetRuntimeSpeedBonus(float bonus)
        {
            runtimeSpeedBonus = Mathf.Max(
                -Mathf.Min(
                    walkSpeed,
                    crouchSpeed) + 0.1f,
                bonus);
        }

        public void StopMotion()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = -2f;
            desiredWorldDirection = Vector3.zero;
            targetHorizontalSpeed = 0f;
            airborneSpeedLimit = standingJumpAirSpeedLimit;
            isBrakingForReversal = false;
        }

        public void ResetForDiagnostics(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            controller.enabled = wasEnabled;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = -2f;
            desiredWorldDirection = Vector3.zero;
            targetHorizontalSpeed = 0f;
            airborneSpeedLimit = standingJumpAirSpeedLimit;
            isBrakingForReversal = false;
            movementCameraBasisLocked = false;
            movementCameraUnlockPending = false;
            lastGroundedTime = Time.time;
            lastJumpRequestedTime = float.NegativeInfinity;
            isGrounded = HasSupportedGroundContact();
            facingOverrideBehaviours =
                GetComponentsInChildren<MonoBehaviour>(true);
            hasGroundControl = isGrounded;
        }

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
            facingOverrideBehaviours =
                GetComponentsInChildren<MonoBehaviour>(true);
        }

        private void Update()
        {
            if (health != null && !health.IsAlive)
            {
                desiredWorldDirection = Vector3.zero;
                targetHorizontalSpeed = 0f;
                hasGroundControl = false;
                ApplyGravityOnly();
                return;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            PlayerIntent intent = input.CurrentIntent;
            UpdateCrouch(intent.CrouchHeld);
            UpdateMovementCameraBasis(input.CameraOrbitHeld);
            Vector3 desiredDirection = ToWorldDirection(intent.Move);
            bool facingOverridden =
                TryGetFacingOverride(out Vector3 overrideFacingDirection);
            desiredWorldDirection = desiredDirection;
            hasGroundControl = HasSupportedGroundContact();
            if (hasGroundControl)
            {
                // Aim-locked movement keeps the deliberate walk gait even
                // when the player is holding the sprint input.
                bool inspectionMovementOrbit =
                    input.CameraOrbitHeld ||
                    movementCameraBasisLocked;
                bool sprintAllowed = CalculateSprintAllowed(
                    facingOverridden,
                    inspectionMovementOrbit,
                    intent.BlockHeld,
                    intent.SprintHeld);
                float targetSpeed = isCrouched
                    ? CrouchSpeed
                    : sprintAllowed
                        ? SprintSpeed
                        : WalkSpeed;
                targetHorizontalSpeed = desiredDirection.sqrMagnitude > 0.001f ? targetSpeed : 0f;
                Vector3 desiredVelocity = GetGroundTargetVelocity(
                    desiredDirection,
                    targetSpeed,
                    facingOverridden);
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    desiredVelocity,
                    acceleration * Time.deltaTime);
                airborneSpeedLimit = Mathf.Max(standingJumpAirSpeedLimit, horizontalVelocity.magnitude);
            }
            else if (desiredDirection.sqrMagnitude > 0.001f)
            {
                targetHorizontalSpeed = airborneSpeedLimit;
                Vector3 desiredAirVelocity = desiredDirection * airborneSpeedLimit;
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    desiredAirVelocity,
                    airAcceleration * Time.deltaTime);
            }
            else
            {
                targetHorizontalSpeed = 0f;
            }

            UpdateFacing(
                desiredDirection,
                facingOverridden,
                overrideFacingDirection);

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

            Vector3 forward = movementCameraBasisLocked
                ? lockedMovementCameraForward
                : Vector3.ProjectOnPlane(
                    cameraTransform.forward,
                    Vector3.up).normalized;
            Vector3 right = movementCameraBasisLocked
                ? lockedMovementCameraRight
                : Vector3.ProjectOnPlane(
                    cameraTransform.right,
                    Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
        }

        public static bool CalculateSprintAllowed(
            bool facingOverridden,
            bool inspectionOrbitHeld,
            bool combatAimHeld,
            bool sprintHeld)
        {
            bool inspectionOnlyFacingLock =
                inspectionOrbitHeld &&
                !combatAimHeld;
            return sprintHeld &&
                (!facingOverridden || inspectionOnlyFacingLock);
        }

        private void UpdateMovementCameraBasis(bool orbitHeld)
        {
            if (!orbitHeld)
            {
                if (movementCameraBasisLocked &&
                    !movementCameraUnlockPending)
                {
                    movementCameraUnlockPending = true;
                    return;
                }

                movementCameraBasisLocked = false;
                movementCameraUnlockPending = false;
                return;
            }

            movementCameraUnlockPending = false;

            if (movementCameraBasisLocked ||
                cameraTransform == null)
            {
                return;
            }

            lockedMovementCameraForward =
                Vector3.ProjectOnPlane(
                    cameraTransform.forward,
                    Vector3.up).normalized;
            lockedMovementCameraRight =
                Vector3.ProjectOnPlane(
                    cameraTransform.right,
                    Vector3.up).normalized;
            if (lockedMovementCameraForward.sqrMagnitude < 0.001f)
            {
                lockedMovementCameraForward = transform.forward;
            }
            if (lockedMovementCameraRight.sqrMagnitude < 0.001f)
            {
                lockedMovementCameraRight = transform.right;
            }
            movementCameraBasisLocked = true;
        }

        private Vector3 GetGroundTargetVelocity(
            Vector3 desiredDirection,
            float targetSpeed,
            bool facingOverridden)
        {
            if (desiredDirection.sqrMagnitude <= 0.001f)
            {
                isBrakingForReversal = false;
                return Vector3.zero;
            }

            float currentSpeed = horizontalVelocity.magnitude;
            if (!isBrakingForReversal && currentSpeed > reversalStopSpeed)
            {
                float directionAlignment = Vector3.Dot(horizontalVelocity / currentSpeed, desiredDirection);
                isBrakingForReversal = directionAlignment < reversalBrakeDot;
            }

            if (!isBrakingForReversal)
            {
                return desiredDirection * targetSpeed;
            }

            float facingAngle = Vector3.Angle(transform.forward, desiredDirection);
            if (currentSpeed <= reversalStopSpeed &&
                (facingOverridden ||
                    facingAngle <= reversalRestartAngle))
            {
                isBrakingForReversal = false;
                return desiredDirection * targetSpeed;
            }

            return Vector3.zero;
        }

        private void UpdateFacing(
            Vector3 desiredDirection,
            bool facingOverridden,
            Vector3 overrideFacingDirection)
        {
            Vector3 facingDirection = facingOverridden
                ? overrideFacingDirection
                : horizontalVelocity;
            if (!facingOverridden &&
                facingDirection.sqrMagnitude <=
                    reversalStopSpeed * reversalStopSpeed)
            {
                facingDirection = desiredDirection;
            }

            facingDirection.y = 0f;

            if (facingDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                (facingOverridden
                    ? aimFacingTurnSpeed
                    : turnSpeed) *
                Time.deltaTime);
        }

        private bool TryGetFacingOverride(
            out Vector3 facingDirection)
        {
            facingDirection = Vector3.zero;
            if (facingOverrideBehaviours == null)
            {
                facingOverrideBehaviours =
                    GetComponentsInChildren<MonoBehaviour>(true);
            }

            for (int index = 0;
                 index < facingOverrideBehaviours.Length;
                 index++)
            {
                MonoBehaviour behaviour =
                    facingOverrideBehaviours[index];
                if (behaviour == null ||
                    !behaviour.isActiveAndEnabled ||
                    !(behaviour is ICharacterFacingOverride source) ||
                    !source.TryGetFacingDirection(
                        out Vector3 candidate))
                {
                    continue;
                }

                candidate.y = 0f;
                if (candidate.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                facingDirection = candidate.normalized;
                return true;
            }

            return false;
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
            RaycastHit[] hits = Physics.SphereCastAll(
                probeOrigin,
                groundProbeRadius,
                Vector3.down,
                groundProbeDistance,
                groundSupportMask,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            bool supported = false;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider hitCollider = hits[index].collider;
                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(transform) ||
                    hits[index].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[index].distance;
                supported = hits[index].normal.y >= 0.55f;
            }

            return supported;
        }
    }
}
