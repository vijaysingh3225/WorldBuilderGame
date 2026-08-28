using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

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
        public const float DefaultCrouchTransitionSpeed = 2.75f;
        // Generated bridge decks sit 0.35 m above their fitted bank height.
        // Keep a small controller margin above that lip so AI and player
        // capsules can mount the deck without relying on a jump.
        public const float MinimumTraversalStepOffset = 0.42f;
        public const float DefaultSteepSlopeSlideSpeed = 5.25f;
        public const float DefaultDamageSprintInterruption = 0.35f;
        public const float BowSprintBufferGrace = 0.20f;
        public const float MinimumStableGroundProbeDistance = 0.28f;
        public const float DefaultLadderClimbSpeed = 1.5f;
        public const float LadderAlignmentDuration = 0.18f;
        public const float LadderDismountDuration = 0.32f;

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
        [SerializeField, Min(0.1f)] private float crouchTransitionSpeed =
            DefaultCrouchTransitionSpeed;
        [SerializeField] private LayerMask overheadObstructionMask = ~(1 << 2);
        [SerializeField] private LayerMask groundSupportMask = ~(1 << 2);
        [SerializeField, Min(0.05f)] private float groundProbeRadius = 0.18f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.15f;
        [SerializeField, Min(0f)] private float steepSlopeSlideSpeed =
            DefaultSteepSlopeSlideSpeed;
        [SerializeField, Min(0f)] private float damageSprintInterruption =
            DefaultDamageSprintInterruption;

        private CharacterController controller;
        private PlayerInputSource input;
        private Health health;
        private BowWeapon bowWeapon;
        private Vector3 horizontalVelocity;
        private Vector3 planarDashDirection;
        private float verticalVelocity;
        private float planarDashDistanceRemaining;
        private float planarDashSpeed;
        private float planarDashEndsAt = float.NegativeInfinity;
        private float sprintInterruptedUntil = float.NegativeInfinity;
        private float bowSprintBufferedUntil = float.NegativeInfinity;
        private float hitStaggerUntil = float.NegativeInfinity;
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
        private bool isClimbingLadder;
        private bool ladderControllerWasEnabled;
        private float ladderClimbStartedAt;
        private float ladderAscentDuration;
        private Vector3 ladderApproachStart;
        private Vector3 ladderBottom;
        private Vector3 ladderVerticalTop;
        private Vector3 ladderDismountTop;
        private Quaternion ladderStartRotation;
        private Quaternion ladderFacingRotation;
        private readonly RaycastHit[] groundProbeHits =
            new RaycastHit[16];

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
        public bool IsSprintInterrupted =>
            Time.time < sprintInterruptedUntil;
        public bool BowSprintBuffered =>
            Time.time <= bowSprintBufferedUntil;
        public bool IsHitStaggered => Time.time < hitStaggerUntil;
        public bool IsClimbingLadder => isClimbingLadder;
        public float LadderClimbProgress { get; private set; }
        public event System.Action LadderClimbStarted;
        public event System.Action LadderClimbEnded;

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
            CancelLadderClimb();
            horizontalVelocity = Vector3.zero;
            verticalVelocity = -2f;
            desiredWorldDirection = Vector3.zero;
            targetHorizontalSpeed = 0f;
            airborneSpeedLimit = standingJumpAirSpeedLimit;
            isBrakingForReversal = false;
            sprintInterruptedUntil = float.NegativeInfinity;
            bowSprintBufferedUntil = float.NegativeInfinity;
            hitStaggerUntil = float.NegativeInfinity;
            ClearPlanarDash();
        }

        public void ApplyPlanarDash(
            Vector3 worldDirection,
            float distance,
            float duration = 0.12f)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(
                worldDirection,
                Vector3.up);
            if (planarDirection.sqrMagnitude <= 0.0001f ||
                distance <= 0f)
            {
                return;
            }

            planarDirection.Normalize();
            float safeDuration = Mathf.Max(0.01f, duration);
            planarDashDirection = planarDirection;
            planarDashDistanceRemaining = distance;
            planarDashSpeed = distance / safeDuration;
            planarDashEndsAt = Time.time + safeDuration;
            isBrakingForReversal = false;
        }

        public void ResetForDiagnostics(Vector3 worldPosition, Quaternion worldRotation)
        {
            CancelLadderClimb();
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
            sprintInterruptedUntil = float.NegativeInfinity;
            bowSprintBufferedUntil = float.NegativeInfinity;
            ClearPlanarDash();
            movementCameraBasisLocked = false;
            movementCameraUnlockPending = false;
            lastGroundedTime = Time.time;
            lastJumpRequestedTime = float.NegativeInfinity;
            isGrounded = HasGroundContact();
            facingOverrideBehaviours =
                GetComponentsInChildren<MonoBehaviour>(true);
            hasGroundControl = HasWalkableGroundContact();
        }

        public bool TryBeginLadderClimb(
            Vector3 bottomPosition,
            Vector3 topPosition,
            Vector3 ladderFacing)
        {
            if (isClimbingLadder ||
                !CompareTag("Player") ||
                health == null ||
                !health.IsAlive ||
                topPosition.y <= bottomPosition.y + 0.5f)
            {
                return false;
            }

            Vector3 planarFacing = Vector3.ProjectOnPlane(
                ladderFacing,
                Vector3.up);
            if (planarFacing.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            input ??= GetComponent<PlayerInputSource>();
            input?.CancelSprintToggle();
            UpdateCrouch(false);
            ClearPlanarDash();
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            desiredWorldDirection = Vector3.zero;
            targetHorizontalSpeed = 0f;
            isBrakingForReversal = false;
            hasGroundControl = false;
            isGrounded = false;

            isClimbingLadder = true;
            LadderClimbProgress = 0f;
            ladderClimbStartedAt = Time.time;
            ladderApproachStart = transform.position;
            ladderBottom = bottomPosition;
            ladderVerticalTop = new Vector3(
                bottomPosition.x,
                topPosition.y,
                bottomPosition.z);
            ladderDismountTop = topPosition;
            ladderAscentDuration = Mathf.Max(
                0.1f,
                (ladderVerticalTop.y - ladderBottom.y) /
                DefaultLadderClimbSpeed);
            ladderStartRotation = transform.rotation;
            ladderFacingRotation = Quaternion.LookRotation(
                planarFacing.normalized,
                Vector3.up);
            ladderControllerWasEnabled =
                controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            LadderClimbStarted?.Invoke();
            return true;
        }

        public void CancelLadderClimb()
        {
            FinishLadderClimb(false);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.stepOffset = Mathf.Max(
                controller.stepOffset,
                MinimumTraversalStepOffset);
            input = GetComponent<PlayerInputSource>();
            health = GetComponent<Health>();
            bowWeapon = GetComponentInChildren<BowWeapon>(true);
            EnsurePlayerDamageNumbers();
            EnsureAnatomicalDamageHitboxes();
            standingHeight = controller.height;
            standingCenter = controller.center;
            crouchingHeight = Mathf.Clamp(crouchingHeight, controller.radius * 2f, standingHeight);
            crouchingCenter = standingCenter + Vector3.down * ((standingHeight - crouchingHeight) * 0.5f);
            airborneSpeedLimit = standingJumpAirSpeedLimit;
            isGrounded = HasGroundContact();
            facingOverrideBehaviours =
                GetComponentsInChildren<MonoBehaviour>(true);
        }

        private void OnEnable()
        {
            health ??= GetComponent<Health>();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            CancelLadderClimb();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(DamageRequest request)
        {
            if (request.Amount <= 0f)
            {
                return;
            }

            if (string.Equals(
                    request.SourceId,
                    MeleeWeapon.PrototypeSwordSourceId,
                    System.StringComparison.Ordinal))
            {
                hitStaggerUntil = Mathf.Max(
                    hitStaggerUntil,
                    Time.time +
                        (request.StaggerDuration > 0f
                            ? request.StaggerDuration
                            : HitReactionPresenter.SwordStaggerDuration));
                ClearPlanarDash();
            }

            if (!CompareTag("Player"))
            {
                return;
            }

            input ??= GetComponent<PlayerInputSource>();
            input?.CancelSprintToggle();
            sprintInterruptedUntil = Mathf.Max(
                sprintInterruptedUntil,
                Time.time + damageSprintInterruption);
            if (targetHorizontalSpeed > WalkSpeed)
            {
                targetHorizontalSpeed = WalkSpeed;
            }
        }

        private void EnsureAnatomicalDamageHitboxes()
        {
            Animator animator =
                GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            HumanoidDamageHitboxRig hitboxes =
                GetComponent<HumanoidDamageHitboxRig>();
            if (hitboxes == null)
            {
                hitboxes = gameObject.AddComponent<
                    HumanoidDamageHitboxRig>();
            }
            hitboxes.Configure(animator);
        }

        private void EnsurePlayerDamageNumbers()
        {
            if (health == null)
            {
                return;
            }

            FloatingDamageNumberPresenter presenter =
                GetComponent<FloatingDamageNumberPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<
                    FloatingDamageNumberPresenter>();
            }
            presenter.Configure(
                health,
                null,
                true);
        }

        private void Update()
        {
            if (health != null && !health.IsAlive)
            {
                CancelLadderClimb();
                desiredWorldDirection = Vector3.zero;
                targetHorizontalSpeed = 0f;
                hasGroundControl = false;
                ApplyGravityOnly();
                return;
            }

            if (isClimbingLadder)
            {
                UpdateLadderClimb();
                return;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            PlayerIntent intent = IsHitStaggered
                ? default
                : input.CurrentIntent;
            bool bowPresentationLocked =
                bowWeapon != null &&
                bowWeapon.PresentationAimLocked;
            if (bowPresentationLocked && intent.SprintHeld)
            {
                bowSprintBufferedUntil = Mathf.Max(
                    bowSprintBufferedUntil,
                    Time.time +
                    bowWeapon.PostShotPoseRemaining +
                    BowSprintBufferGrace);
            }
            bool bufferedBowSprint =
                !bowPresentationLocked &&
                BowSprintBuffered;
            if (IsHitStaggered)
            {
                ClearPlanarDash();
            }
            UpdateCrouch(intent.CrouchHeld);
            UpdateMovementCameraBasis(input.CameraOrbitHeld);
            Vector3 desiredDirection = ToWorldDirection(intent.Move);
            bool facingOverridden =
                TryGetFacingOverride(out Vector3 overrideFacingDirection);
            desiredWorldDirection = desiredDirection;
            hasGroundControl = HasWalkableGroundContact();
            if (hasGroundControl)
            {
                // Aim-locked movement keeps the deliberate walk gait even
                // when the player's sprint toggle is active.
                bool inspectionMovementOrbit =
                    input.CameraOrbitHeld ||
                    movementCameraBasisLocked;
                // Charging the short sword does not aim-lock locomotion. A
                // drawn bow and blocking still deliberately keep the slower,
                // facing-locked gait.
                bool combatAimHeld =
                    intent.BlockHeld ||
                    bowPresentationLocked;
                bool sprintAllowed =
                    !IsSprintInterrupted &&
                    CalculateSprintAllowed(
                        facingOverridden,
                        inspectionMovementOrbit,
                        combatAimHeld,
                        intent.SprintHeld || bufferedBowSprint);
                if (sprintAllowed &&
                    desiredDirection.sqrMagnitude > 0.001f)
                {
                    bowSprintBufferedUntil = float.NegativeInfinity;
                }
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
            Vector3 motion = horizontalVelocity +
                Vector3.up * verticalVelocity +
                CalculatePlanarDashVelocity();
            if (!hasGroundControl &&
                TryGetGroundSurface(out RaycastHit groundHit) &&
                IsSteepSlope(groundHit.normal, controller.slopeLimit))
            {
                motion = ApplySteepSlopeSlide(
                    motion,
                    groundHit.normal,
                    controller.slopeLimit,
                    steepSlopeSlideSpeed);
            }
            controller.Move(motion * Time.deltaTime);
            isGrounded = CalculateGroundedPresentation(
                HasGroundContact(),
                verticalVelocity);
        }

        private void UpdateLadderClimb()
        {
            float elapsed = Mathf.Max(
                0f,
                Time.time - ladderClimbStartedAt);
            float ascentStart = LadderAlignmentDuration;
            float dismountStart = ascentStart + ladderAscentDuration;
            float totalDuration =
                dismountStart + LadderDismountDuration;

            Vector3 position;
            Quaternion rotation;
            if (elapsed < ascentStart)
            {
                float alignment = Mathf.SmoothStep(
                    0f,
                    1f,
                    elapsed / Mathf.Max(0.01f, ascentStart));
                position = Vector3.Lerp(
                    ladderApproachStart,
                    ladderBottom,
                    alignment);
                rotation = Quaternion.Slerp(
                    ladderStartRotation,
                    ladderFacingRotation,
                    alignment);
            }
            else if (elapsed < dismountStart)
            {
                float ascent = Mathf.Clamp01(
                    (elapsed - ascentStart) /
                    Mathf.Max(0.01f, ladderAscentDuration));
                position = Vector3.Lerp(
                    ladderBottom,
                    ladderVerticalTop,
                    ascent);
                rotation = ladderFacingRotation;
            }
            else
            {
                float dismount = Mathf.SmoothStep(
                    0f,
                    1f,
                    (elapsed - dismountStart) /
                    LadderDismountDuration);
                position = Vector3.Lerp(
                    ladderVerticalTop,
                    ladderDismountTop,
                    dismount);
                rotation = ladderFacingRotation;
            }

            transform.SetPositionAndRotation(position, rotation);
            LadderClimbProgress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, totalDuration));
            if (elapsed >= totalDuration)
            {
                FinishLadderClimb(true);
            }
        }

        private void FinishLadderClimb(bool completed)
        {
            if (!isClimbingLadder)
            {
                return;
            }

            isClimbingLadder = false;
            LadderClimbProgress = completed ? 1f : 0f;
            if (controller != null && ladderControllerWasEnabled)
            {
                controller.enabled = true;
            }
            ladderControllerWasEnabled = false;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = -2f;
            desiredWorldDirection = Vector3.zero;
            targetHorizontalSpeed = 0f;
            isGrounded = controller != null &&
                controller.enabled &&
                HasGroundContact();
            hasGroundControl = isGrounded;
            LadderClimbEnded?.Invoke();
        }

        private Vector3 CalculatePlanarDashVelocity()
        {
            if (planarDashDistanceRemaining <= 0.001f)
            {
                ClearPlanarDash();
                return Vector3.zero;
            }

            float stepDistance = Time.time >= planarDashEndsAt
                ? planarDashDistanceRemaining
                : Mathf.Min(
                    planarDashDistanceRemaining,
                    planarDashSpeed * Time.deltaTime);
            planarDashDistanceRemaining -= stepDistance;
            Vector3 velocity = planarDashDirection *
                (stepDistance / Mathf.Max(0.0001f, Time.deltaTime));
            if (planarDashDistanceRemaining <= 0.001f)
            {
                ClearPlanarDash();
            }
            return velocity;
        }

        private void ClearPlanarDash()
        {
            planarDashDirection = Vector3.zero;
            planarDashDistanceRemaining = 0f;
            planarDashSpeed = 0f;
            planarDashEndsAt = float.NegativeInfinity;
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
            isGrounded = CalculateGroundedPresentation(
                HasGroundContact(),
                verticalVelocity);
        }

        private void UpdateVerticalMotion(PlayerIntent intent)
        {
            bool groundedBeforeMove = HasWalkableGroundContact();
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
            if (HasGroundContact() && verticalVelocity < 0f)
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

        private bool HasGroundContact()
        {
            return TryGetGroundSurface(out _);
        }

        private bool HasWalkableGroundContact()
        {
            return TryGetGroundSurface(out RaycastHit hit) &&
                !IsSteepSlope(hit.normal, controller.slopeLimit);
        }

        public static bool CalculateGroundedPresentation(
            bool hasGroundContact,
            float verticalVelocity)
        {
            // Nearby terrain keeps locomotion grounded across seams and
            // slopes, but an authored jump always owns its upward takeoff.
            return hasGroundContact && verticalVelocity <= 0.05f;
        }

        private bool TryGetGroundSurface(out RaycastHit closestHit)
        {
            closestHit = default;
            Vector3 worldCenter = transform.TransformPoint(controller.center);
            Vector3 controllerBottom = worldCenter - Vector3.up * (controller.height * 0.5f);
            Vector3 probeOrigin = controllerBottom + Vector3.up * (groundProbeRadius + 0.05f);
            RaycastHit[] hits = groundProbeHits;
            int hitCount = Physics.SphereCastNonAlloc(
                probeOrigin,
                groundProbeRadius,
                Vector3.down,
                hits,
                Mathf.Max(
                    groundProbeDistance,
                    MinimumStableGroundProbeDistance),
                groundSupportMask,
                QueryTriggerInteraction.Ignore);

            // Physics non-alloc queries do not guarantee the nearest result
            // when their buffer is full. Fall back to the complete query in
            // that rare dense case so ground selection remains identical.
            if (hitCount == hits.Length)
            {
                hits = Physics.SphereCastAll(
                    probeOrigin,
                    groundProbeRadius,
                    Vector3.down,
                    Mathf.Max(
                        groundProbeDistance,
                        MinimumStableGroundProbeDistance),
                    groundSupportMask,
                    QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = hits[index].collider;
                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(transform) ||
                    hits[index].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[index].distance;
                closestHit = hits[index];
            }

            return closestDistance < float.PositiveInfinity;
        }

        public static bool IsSteepSlope(
            Vector3 surfaceNormal,
            float slopeLimit)
        {
            if (surfaceNormal.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            return Vector3.Angle(surfaceNormal, Vector3.up) >
                Mathf.Clamp(slopeLimit, 0f, 89.9f) + 0.05f;
        }

        public static Vector3 ApplySteepSlopeSlide(
            Vector3 requestedMotion,
            Vector3 surfaceNormal,
            float slopeLimit,
            float maximumSlideSpeed)
        {
            float slopeAngle = Vector3.Angle(
                surfaceNormal,
                Vector3.up);
            if (slopeAngle <= slopeLimit ||
                maximumSlideSpeed <= 0f)
            {
                return requestedMotion;
            }

            Vector3 downhill = Vector3.ProjectOnPlane(
                Vector3.down,
                surfaceNormal).normalized;
            Vector3 planarDownhill = Vector3.ProjectOnPlane(
                downhill,
                Vector3.up).normalized;
            Vector3 planarMotion = Vector3.ProjectOnPlane(
                requestedMotion,
                Vector3.up);
            float uphillSpeed = Vector3.Dot(
                planarMotion,
                -planarDownhill);
            if (uphillSpeed > 0f)
            {
                requestedMotion += planarDownhill * uphillSpeed;
            }

            float slideStrength = Mathf.InverseLerp(
                slopeLimit,
                90f,
                slopeAngle);
            return requestedMotion +
                downhill * Mathf.Lerp(
                    maximumSlideSpeed * 0.45f,
                    maximumSlideSpeed,
                    slideStrength);
        }
    }
}
