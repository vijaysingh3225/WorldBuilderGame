using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.CameraSystem
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraAimTarget : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.45f, 0f);
        [SerializeField, Min(0f)] private float crouchCameraDrop = 0.85f;
        [SerializeField, Min(0f)] private float heightSmoothTime = 0.075f;
        [SerializeField, Min(0f)] private float ceilingPadding = 0.1f;
        [SerializeField] private LayerMask cameraClearanceMask = ~(1 << 2);
        [SerializeField] private float initialPitch = 12f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-30f, 65f);
        [SerializeField, Min(0f)] private float rotationSmoothTime = 0.035f;
        [SerializeField, Min(0f)] private float shoulderSwitchSmoothTime = 0.22f;
        [Header("Bow Aim")]
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField, Min(0f)] private float bowAimRightOffset = 0f;
        [SerializeField, Min(0f)] private float bowAimOffsetSmoothTime = 0.10f;
        [SerializeField] private CinemachineThirdPersonFollow thirdPersonFollow;
        [SerializeField] private Vector3 closeDrawShoulderOffset =
            new Vector3(0.72f, -0.16f, 0f);
        [SerializeField, Min(0.1f)] private float closeDrawCameraDistance = 2.45f;
        [SerializeField, Min(0f)] private float fastBowCameraBlendInTime = 0.075f;
        [SerializeField, Min(0f)] private float bowCameraBlendOutTime = 0.22f;
        [Header("Model Inspection")]
        [SerializeField] private Vector2 inspectionPitchLimits =
            new Vector2(-75f, 80f);

        private float desiredYaw;
        private float desiredPitch;
        private float currentYaw;
        private float currentPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private float currentFollowHeight;
        private float followHeightVelocity;
        private float currentBowAimOffset;
        private float bowAimOffsetVelocity;
        private Vector3 defaultShoulderOffset;
        private float defaultCameraDistance;
        private float bowCameraWeight;
        private float bowCameraWeightVelocity;
        private float shoulderSideBlend = 1f;
        private float shoulderSideVelocity;
        private bool bowCameraDefaultsCaptured;
        private bool inspectionOrbitActive;
        private float inspectionYaw;
        private float inspectionPitch;
        private float resumeDesiredYaw;
        private float resumeDesiredPitch;
        private float resumeCurrentYaw;
        private float resumeCurrentPitch;
        private Vector3 inspectionAimDirection;
        private Vector3 inspectionAimOrigin;
        private Vector3 inspectionFacingDirection;
        private bool inspectionDiagnosticOverride;
        private bool inspectionDiagnosticHeld;
        private Vector2 inspectionDiagnosticLook;

        public Vector3 AimDirection =>
            inspectionOrbitActive
                ? inspectionAimDirection
                : transform.forward;
        public bool InspectionOrbitActive =>
            inspectionOrbitActive;
        public Vector3 InspectionFacingDirection =>
            inspectionFacingDirection;
        public Vector3 InspectionAimOrigin =>
            inspectionAimOrigin;
        public float CurrentBowAimOffset => currentBowAimOffset;
        public float BowCameraWeight => bowCameraWeight;
        public float CurrentShoulderSideBlend => shoulderSideBlend;
        public Vector3 CurrentShoulderOffset =>
            thirdPersonFollow != null
                ? thirdPersonFollow.ShoulderOffset
                : default;
        public float CurrentCameraDistance =>
            thirdPersonFollow != null
                ? thirdPersonFollow.CameraDistance
                : 0f;
        public bool IsBowAiming =>
            bowWeapon != null &&
            bowWeapon.DrawInputHeld;

        public static float CalculateBowCameraTargetWeight(
            bool isBowAiming,
            bool isInspecting)
        {
            return isBowAiming && !isInspecting
                ? 1f
                : 0f;
        }

        public static float CalculateCrouchFollowHeight(
            float standingFollowHeight,
            float crouchDrop,
            float crouchAmount)
        {
            return standingFollowHeight -
                Mathf.Clamp01(crouchAmount) *
                Mathf.Max(0f, crouchDrop);
        }

        public static void CalculateBowCameraComposition(
            Vector3 normalShoulderOffset,
            float normalDistance,
            Vector3 aimedShoulderOffset,
            float aimedDistance,
            float weight,
            out Vector3 shoulderOffset,
            out float cameraDistance)
        {
            float clampedWeight = Mathf.Clamp01(weight);
            shoulderOffset = Vector3.Lerp(
                normalShoulderOffset,
                aimedShoulderOffset,
                clampedWeight);
            cameraDistance = Mathf.Lerp(
                normalDistance,
                aimedDistance,
                clampedWeight);
        }

        public static Vector3 MirrorShoulderOffset(
            Vector3 rightShoulderOffset,
            float shoulderSideBlend)
        {
            rightShoulderOffset.x *= Mathf.Clamp(
                shoulderSideBlend,
                -1f,
                1f);
            return rightShoulderOffset;
        }

        public void SetInspectionDiagnosticOverride(
            bool held,
            Vector2 look)
        {
            inspectionDiagnosticOverride = true;
            inspectionDiagnosticHeld = held;
            inspectionDiagnosticLook = look;
        }

        public void ClearInspectionDiagnosticOverride()
        {
            inspectionDiagnosticOverride = false;
            inspectionDiagnosticHeld = false;
            inspectionDiagnosticLook = Vector2.zero;
            if (inspectionOrbitActive)
            {
                EndInspectionOrbit();
                SnapToTarget();
            }
        }

        public void Configure(Transform target, PlayerInputSource intentSource)
        {
            followTarget = target;
            input = intentSource;
            motor = target != null ? target.GetComponent<ThirdPersonMotor>() : null;
            bowWeapon = target != null
                ? target.GetComponentInChildren<BowWeapon>()
                : null;
            desiredYaw = target != null ? target.eulerAngles.y : 0f;
            currentYaw = desiredYaw;
            desiredPitch = initialPitch;
            currentPitch = desiredPitch;
            currentFollowHeight = followOffset.y;
            shoulderSideBlend = input != null ? input.ShoulderSide : 1f;
            shoulderSideVelocity = 0f;
            SnapToTarget();
        }

        private void Start()
        {
            ResolveBowCameraRig();
        }

        private void Awake()
        {
            if (followTarget == null)
            {
                return;
            }

            desiredYaw = transform.eulerAngles.y;
            currentYaw = desiredYaw;
            desiredPitch = initialPitch;
            currentPitch = desiredPitch;
            currentFollowHeight = followOffset.y;
            shoulderSideBlend = input != null ? input.ShoulderSide : 1f;
            if (motor == null)
            {
                motor = followTarget.GetComponent<ThirdPersonMotor>();
            }
            bowWeapon ??= followTarget.GetComponentInChildren<BowWeapon>();
        }

        private void Update()
        {
            if (followTarget == null || input == null)
            {
                return;
            }

            Vector2 look =
                inspectionDiagnosticOverride
                    ? inspectionDiagnosticLook
                    : input.CurrentIntent.Look;
            bool inspectionRequested =
                inspectionDiagnosticOverride
                    ? inspectionDiagnosticHeld
                    : !input.DiagnosticOverrideActive &&
                        Mouse.current != null &&
                        Mouse.current.middleButton.isPressed;
            bool inspectionEndedThisFrame = false;
            if (inspectionRequested &&
                !inspectionOrbitActive)
            {
                BeginInspectionOrbit();
            }
            else if (!inspectionRequested &&
                inspectionOrbitActive)
            {
                EndInspectionOrbit();
                inspectionEndedThisFrame = true;
            }

            if (inspectionOrbitActive)
            {
                inspectionYaw += look.x;
                inspectionPitch = Mathf.Clamp(
                    inspectionPitch - look.y,
                    inspectionPitchLimits.x,
                    inspectionPitchLimits.y);
                currentYaw = inspectionYaw;
                currentPitch = inspectionPitch;
            }
            else if (!inspectionEndedThisFrame)
            {
                desiredYaw += look.x;
                desiredPitch = Mathf.Clamp(
                    desiredPitch - look.y,
                    pitchLimits.x,
                    pitchLimits.y);

                if (rotationSmoothTime <= 0f)
                {
                    currentYaw = desiredYaw;
                    currentPitch = desiredPitch;
                }
                else
                {
                    currentYaw = Mathf.SmoothDampAngle(
                        currentYaw,
                        desiredYaw,
                        ref yawVelocity,
                        rotationSmoothTime);
                    currentPitch = Mathf.SmoothDampAngle(
                        currentPitch,
                        desiredPitch,
                        ref pitchVelocity,
                        rotationSmoothTime);
                }
            }

            float targetFollowHeight = CalculateCrouchFollowHeight(
                followOffset.y,
                crouchCameraDrop,
                motor != null ? motor.CrouchAmount : 0f);
            targetFollowHeight = ResolveHeightUnderCeiling(targetFollowHeight);
            currentFollowHeight = heightSmoothTime <= 0f
                ? targetFollowHeight
                : Mathf.SmoothDamp(currentFollowHeight, targetFollowHeight, ref followHeightVelocity, heightSmoothTime);
            float targetShoulderSide = input.ShoulderSide;
            shoulderSideBlend = shoulderSwitchSmoothTime <= 0f
                ? targetShoulderSide
                : Mathf.SmoothDamp(
                    shoulderSideBlend,
                    targetShoulderSide,
                    ref shoulderSideVelocity,
                    shoulderSwitchSmoothTime);
            float targetBowAimOffset = IsBowAiming
                ? bowAimRightOffset * shoulderSideBlend
                : 0f;
            currentBowAimOffset = bowAimOffsetSmoothTime <= 0f
                ? targetBowAimOffset
                : Mathf.SmoothDamp(
                    currentBowAimOffset,
                    targetBowAimOffset,
                    ref bowAimOffsetVelocity,
                    bowAimOffsetSmoothTime);

            UpdateBowCameraComposition();
            SnapToTarget();
        }

        private void OnDisable()
        {
            RestoreDefaultCameraComposition();
            inspectionOrbitActive = false;
            inspectionDiagnosticOverride = false;
            inspectionDiagnosticHeld = false;
            inspectionDiagnosticLook = Vector2.zero;
        }

        private void UpdateBowCameraComposition()
        {
            ResolveBowCameraRig();
            if (!bowCameraDefaultsCaptured)
            {
                return;
            }

            float targetWeight =
                CalculateBowCameraTargetWeight(
                    IsBowAiming,
                    inspectionOrbitActive);
            float smoothTime =
                targetWeight > bowCameraWeight
                    ? fastBowCameraBlendInTime
                    : bowCameraBlendOutTime;
            bowCameraWeight =
                smoothTime <= 0f
                    ? targetWeight
                    : Mathf.SmoothDamp(
                        bowCameraWeight,
                        targetWeight,
                        ref bowCameraWeightVelocity,
                        smoothTime);

            CalculateBowCameraComposition(
                defaultShoulderOffset,
                defaultCameraDistance,
                closeDrawShoulderOffset,
                closeDrawCameraDistance,
                bowCameraWeight,
                out Vector3 shoulderOffset,
                out float cameraDistance);
            thirdPersonFollow.ShoulderOffset = MirrorShoulderOffset(
                shoulderOffset,
                shoulderSideBlend);
            thirdPersonFollow.CameraDistance = cameraDistance;
        }

        private void ResolveBowCameraRig()
        {
            if (thirdPersonFollow == null)
            {
                thirdPersonFollow =
                    FindFirstObjectByType<CinemachineThirdPersonFollow>();
            }

            if (thirdPersonFollow == null ||
                bowCameraDefaultsCaptured)
            {
                return;
            }

            defaultShoulderOffset =
                thirdPersonFollow.ShoulderOffset;
            defaultCameraDistance =
                thirdPersonFollow.CameraDistance;
            bowCameraDefaultsCaptured = true;
        }

        private void RestoreDefaultCameraComposition()
        {
            if (!bowCameraDefaultsCaptured ||
                thirdPersonFollow == null)
            {
                return;
            }

            thirdPersonFollow.ShoulderOffset =
                defaultShoulderOffset;
            thirdPersonFollow.CameraDistance =
                defaultCameraDistance;
            bowCameraWeight = 0f;
            bowCameraWeightVelocity = 0f;
            shoulderSideBlend = 1f;
            shoulderSideVelocity = 0f;
        }

        private void BeginInspectionOrbit()
        {
            inspectionOrbitActive = true;
            inspectionYaw = currentYaw;
            inspectionPitch = currentPitch;
            resumeDesiredYaw = desiredYaw;
            resumeDesiredPitch = desiredPitch;
            resumeCurrentYaw = currentYaw;
            resumeCurrentPitch = currentPitch;
            Camera activeCamera = Camera.main;
            inspectionAimOrigin =
                activeCamera != null
                    ? activeCamera.transform.position
                    : transform.position;
            Vector3 activeAimDirection =
                activeCamera != null
                    ? activeCamera.transform.forward
                    : transform.forward;
            inspectionAimDirection =
                activeAimDirection.sqrMagnitude > 0.001f
                    ? activeAimDirection.normalized
                    : followTarget.forward;
            inspectionFacingDirection =
                Vector3.ProjectOnPlane(
                    followTarget.forward,
                    Vector3.up).normalized;
            if (inspectionFacingDirection.sqrMagnitude <
                0.001f)
            {
                inspectionFacingDirection =
                    Vector3.forward;
            }

            yawVelocity = 0f;
            pitchVelocity = 0f;
        }

        private void EndInspectionOrbit()
        {
            inspectionOrbitActive = false;
            desiredYaw = resumeDesiredYaw;
            desiredPitch = resumeDesiredPitch;
            currentYaw = resumeCurrentYaw;
            currentPitch = resumeCurrentPitch;
            yawVelocity = 0f;
            pitchVelocity = 0f;
        }

        private float ResolveHeightUnderCeiling(float desiredHeight)
        {
            Vector3 origin = followTarget.position + Vector3.up * 0.05f;
            float rayDistance = Mathf.Max(0f, desiredHeight - 0.05f);
            if (Physics.Raycast(
                    origin,
                    Vector3.up,
                    out RaycastHit hit,
                    rayDistance,
                    cameraClearanceMask,
                    QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0.25f, hit.distance + 0.05f - ceilingPadding);
            }

            return desiredHeight;
        }

        private void SnapToTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            Quaternion horizontalRotation =
                Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 horizontalOffset =
                horizontalRotation *
                new Vector3(
                    followOffset.x + currentBowAimOffset,
                    0f,
                    followOffset.z);
            transform.SetPositionAndRotation(
                followTarget.position +
                    horizontalOffset +
                    Vector3.up * currentFollowHeight,
                Quaternion.Euler(currentPitch, currentYaw, 0f));
        }
    }
}
