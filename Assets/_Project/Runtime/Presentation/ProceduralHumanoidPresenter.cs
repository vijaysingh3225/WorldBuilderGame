using UnityEngine;
using WorldBuilder.Gameplay.Characters;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class ProceduralHumanoidPresenter : MonoBehaviour
    {
        private sealed class FootPlantState
        {
            public bool WasStance;
            public Vector3 PlantedPosition;
            public Vector3 SwingStart;
            public Vector3 SwingTarget;
            public Vector3 CurrentTarget;
            public Vector3 GroundNormal = Vector3.up;
        }

        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Transform pelvis;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform rightShoulder;
        [SerializeField, Min(0f)] private float locomotionBlendSpeed = 8f;
        [SerializeField, Min(0f)] private float traversalBlendSpeed = 9f;
        [SerializeField, Min(0f)] private float footPlantBlendSpeed = 12f;
        [SerializeField, Min(0f)] private float footLift = 0.16f;
        [SerializeField, Min(0f)] private float footGroundOffset = 0.015f;
        [SerializeField] private LayerMask footGroundMask = ~(1 << 2);

        private readonly FootPlantState leftFootPlant = new FootPlantState();
        private readonly FootPlantState rightFootPlant = new FootPlantState();
        private Vector3 pelvisRestPosition;
        private Quaternion pelvisRestRotation;
        private Quaternion chestRestRotation;
        private Quaternion leftThighRestRotation;
        private Quaternion rightThighRestRotation;
        private Quaternion leftKneeRestRotation;
        private Quaternion rightKneeRestRotation;
        private Quaternion leftFootRestRotation;
        private Quaternion rightFootRestRotation;
        private Quaternion leftShoulderRestRotation;
        private Quaternion rightShoulderRestRotation;
        private float leftFootLateralOffset;
        private float rightFootLateralOffset;
        private float leftUpperLegLength;
        private float rightUpperLegLength;
        private float leftLowerLegLength;
        private float rightLowerLegLength;
        private float locomotionWeight;
        private float crouchWeight;
        private float airborneWeight;
        private float landingResponse;
        private float footPlantWeight;
        private float strideCycle;
        private bool footPlantsInitialized;
        private bool wasGrounded;
        private bool poseCached;

        private bool HasCompleteRig => pelvis != null && chest != null && leftThigh != null && rightThigh != null &&
            leftKnee != null && rightKnee != null && leftFoot != null && rightFoot != null &&
            leftShoulder != null && rightShoulder != null;

        public void Configure(
            ThirdPersonMotor movementMotor,
            Transform pelvisTransform,
            Transform chestTransform,
            Transform leftThighTransform,
            Transform rightThighTransform,
            Transform leftKneeTransform,
            Transform rightKneeTransform,
            Transform leftFootTransform,
            Transform rightFootTransform,
            Transform leftShoulderTransform,
            Transform rightShoulderTransform)
        {
            motor = movementMotor;
            pelvis = pelvisTransform;
            chest = chestTransform;
            leftThigh = leftThighTransform;
            rightThigh = rightThighTransform;
            leftKnee = leftKneeTransform;
            rightKnee = rightKneeTransform;
            leftFoot = leftFootTransform;
            rightFoot = rightFootTransform;
            leftShoulder = leftShoulderTransform;
            rightShoulder = rightShoulderTransform;
            CacheRestPose();
            wasGrounded = motor != null && motor.IsGrounded;
        }

        private void Awake()
        {
            CacheRestPose();
        }

        private void LateUpdate()
        {
            if (!poseCached || motor == null || !HasCompleteRig)
            {
                return;
            }

            float maximumSpeed = Mathf.Max(0.01f, motor.MaximumSpeed);
            float normalizedSpeed = Mathf.Clamp01(motor.HorizontalSpeed / maximumSpeed);
            float targetWeight = Mathf.InverseLerp(0.02f, 0.18f, normalizedSpeed);
            locomotionWeight = Mathf.MoveTowards(locomotionWeight, targetWeight, locomotionBlendSpeed * Time.deltaTime);
            crouchWeight = Mathf.MoveTowards(crouchWeight, motor.CrouchAmount, traversalBlendSpeed * Time.deltaTime);
            airborneWeight = Mathf.MoveTowards(
                airborneWeight,
                motor.IsGrounded ? 0f : 1f,
                traversalBlendSpeed * Time.deltaTime);

            if (!wasGrounded && motor.IsGrounded)
            {
                landingResponse = 1f;
                footPlantsInitialized = false;
            }
            else if (!motor.IsGrounded)
            {
                footPlantsInitialized = false;
            }

            landingResponse = Mathf.MoveTowards(landingResponse, 0f, 6f * Time.deltaTime);
            wasGrounded = motor.IsGrounded;

            float runBlend = Mathf.InverseLerp(0.58f, 1f, normalizedSpeed);
            float cycleDistance = Mathf.Lerp(1.45f, 2.35f, runBlend);
            if (motor.IsGrounded && motor.HorizontalSpeed > 0.02f)
            {
                strideCycle = Mathf.Repeat(
                    strideCycle + motor.HorizontalSpeed * Time.deltaTime / Mathf.Max(0.01f, cycleDistance),
                    1f);
            }

            ApplyBasePose(runBlend);

            float desiredFootPlantWeight = motor.IsGrounded ? locomotionWeight : 0f;
            footPlantWeight = Mathf.MoveTowards(
                footPlantWeight,
                desiredFootPlantWeight,
                footPlantBlendSpeed * Time.deltaTime);

            if (motor.IsGrounded && footPlantWeight > 0.001f)
            {
                float stanceFraction = Mathf.Lerp(0.60f, 0.38f, runBlend);
                EnsureFootPlantsInitialized(stanceFraction);
                UpdateFootPlant(leftFootPlant, strideCycle, stanceFraction, cycleDistance, leftFootLateralOffset);
                UpdateFootPlant(rightFootPlant, Mathf.Repeat(strideCycle + 0.5f, 1f), stanceFraction, cycleDistance, rightFootLateralOffset);
                ApplyLegIk(
                    leftThigh,
                    leftKnee,
                    leftFoot,
                    leftThighRestRotation,
                    leftKneeRestRotation,
                    leftFootPlant,
                    leftUpperLegLength,
                    leftLowerLegLength,
                    footPlantWeight);
                ApplyLegIk(
                    rightThigh,
                    rightKnee,
                    rightFoot,
                    rightThighRestRotation,
                    rightKneeRestRotation,
                    rightFootPlant,
                    rightUpperLegLength,
                    rightLowerLegLength,
                    footPlantWeight);
            }
        }

        private void OnDisable()
        {
            footPlantsInitialized = false;
            if (poseCached && HasCompleteRig)
            {
                ResetPose();
            }
        }

        private void ApplyBasePose(float runBlend)
        {
            float strideRadians = strideCycle * Mathf.PI * 2f;
            float stride = Mathf.Sin(strideRadians);
            float groundedLocomotion = locomotionWeight * (1f - airborneWeight);
            float strideAngle = Mathf.Lerp(21f, 38f, runBlend) * groundedLocomotion;
            float armAngle = Mathf.Lerp(16f, 30f, runBlend) * groundedLocomotion;
            float leftWalkingKnee = Mathf.Max(0f, -stride) * Mathf.Lerp(20f, 48f, runBlend) * groundedLocomotion;
            float rightWalkingKnee = Mathf.Max(0f, stride) * Mathf.Lerp(20f, 48f, runBlend) * groundedLocomotion;
            float bob = Mathf.Abs(Mathf.Sin(strideRadians * 2f)) * Mathf.Lerp(0.012f, 0.025f, runBlend) * groundedLocomotion;
            float idleBreath = Mathf.Sin(Time.time * 1.7f) * 0.012f * (1f - locomotionWeight);
            float rising = Mathf.Clamp01(motor.VerticalVelocity / 5f) * airborneWeight;
            float falling = Mathf.Clamp01(-motor.VerticalVelocity / 7f) * airborneWeight;
            float movingCrouch = crouchWeight * groundedLocomotion;
            float crouchPelvisDrop = 0.34f * crouchWeight;
            float gaitPelvisDrop = Mathf.Lerp(0.12f, 0.14f, runBlend) * groundedLocomotion * (1f - crouchWeight);
            float landingDrop = 0.075f * landingResponse;

            pelvis.localPosition = pelvisRestPosition + Vector3.up * (
                bob + idleBreath - crouchPelvisDrop - gaitPelvisDrop - landingDrop);
            pelvis.localRotation = pelvisRestRotation * Quaternion.Euler(
                3f * crouchWeight,
                0f,
                stride * 2.4f * groundedLocomotion);
            chest.localRotation = chestRestRotation * Quaternion.Euler(
                -idleBreath * 35f + 10f * crouchWeight - 7f * rising + 5f * falling,
                stride * Mathf.Lerp(1.5f, 3.5f, runBlend) * groundedLocomotion,
                -stride * 1.8f * groundedLocomotion);

            float leftStandingThigh = stride * strideAngle + 12f * rising - 12f * falling;
            float rightStandingThigh = -stride * strideAngle - 8f * rising - 12f * falling;
            float leftCrouchThigh = Mathf.Lerp(-10f, -28f, movingCrouch);
            float rightCrouchThigh = Mathf.Lerp(-55f, -28f, movingCrouch);
            float leftThighAngle = Mathf.Lerp(leftStandingThigh, leftCrouchThigh, crouchWeight);
            float rightThighAngle = Mathf.Lerp(rightStandingThigh, rightCrouchThigh, crouchWeight);

            float leftStandingKnee = leftWalkingKnee + 24f * rising + 30f * falling + 18f * landingResponse;
            float rightStandingKnee = rightWalkingKnee + 34f * rising + 30f * falling + 18f * landingResponse;
            float leftCrouchKnee = Mathf.Lerp(90f, 72f, movingCrouch);
            float rightCrouchKnee = Mathf.Lerp(105f, 72f, movingCrouch);
            float leftKneeAngle = Mathf.Lerp(leftStandingKnee, leftCrouchKnee, crouchWeight);
            float rightKneeAngle = Mathf.Lerp(rightStandingKnee, rightCrouchKnee, crouchWeight);

            leftThigh.localRotation = leftThighRestRotation * Quaternion.Euler(leftThighAngle, 0f, 0f);
            rightThigh.localRotation = rightThighRestRotation * Quaternion.Euler(rightThighAngle, 0f, 0f);
            leftKnee.localRotation = leftKneeRestRotation * Quaternion.Euler(leftKneeAngle, 0f, 0f);
            rightKnee.localRotation = rightKneeRestRotation * Quaternion.Euler(rightKneeAngle, 0f, 0f);
            leftFoot.localRotation = leftFootRestRotation * Quaternion.Euler(-(leftThighAngle + leftKneeAngle), 0f, 0f);
            rightFoot.localRotation = rightFootRestRotation * Quaternion.Euler(-(rightThighAngle + rightKneeAngle), 0f, 0f);

            leftShoulder.localRotation = leftShoulderRestRotation * Quaternion.Euler(
                -stride * armAngle - Mathf.Lerp(20f, 9f, movingCrouch) * crouchWeight - 16f * rising + 12f * falling,
                0f,
                0f);
            rightShoulder.localRotation = rightShoulderRestRotation * Quaternion.Euler(
                stride * armAngle - Mathf.Lerp(35f, 9f, movingCrouch) * crouchWeight - 16f * rising + 12f * falling,
                0f,
                0f);
        }

        private void EnsureFootPlantsInitialized(float stanceFraction)
        {
            if (footPlantsInitialized)
            {
                return;
            }

            InitializeFootPlant(leftFootPlant, leftFoot.position, strideCycle < stanceFraction);
            InitializeFootPlant(
                rightFootPlant,
                rightFoot.position,
                Mathf.Repeat(strideCycle + 0.5f, 1f) < stanceFraction);
            footPlantsInitialized = true;
        }

        private void InitializeFootPlant(FootPlantState state, Vector3 currentPosition, bool isStance)
        {
            state.PlantedPosition = ProjectToGround(currentPosition, out Vector3 groundNormal);
            state.SwingStart = state.PlantedPosition;
            state.SwingTarget = state.PlantedPosition;
            state.CurrentTarget = state.PlantedPosition;
            state.GroundNormal = groundNormal;
            state.WasStance = isStance;
        }

        private void UpdateFootPlant(
            FootPlantState state,
            float phase,
            float stanceFraction,
            float cycleDistance,
            float lateralOffset)
        {
            bool isStance = phase < stanceFraction;
            if (isStance)
            {
                if (!state.WasStance)
                {
                    state.PlantedPosition = ProjectToGround(state.SwingTarget, out Vector3 landingNormal);
                    state.GroundNormal = landingNormal;
                }

                state.CurrentTarget = state.PlantedPosition;
            }
            else
            {
                if (state.WasStance)
                {
                    state.SwingStart = state.PlantedPosition;
                    state.SwingTarget = CalculateLandingTarget(phase, stanceFraction, cycleDistance, lateralOffset);
                }

                float swingProgress = Mathf.InverseLerp(stanceFraction, 1f, phase);
                float smoothProgress = Mathf.SmoothStep(0f, 1f, swingProgress);
                state.CurrentTarget = Vector3.Lerp(state.SwingStart, state.SwingTarget, smoothProgress) +
                    Vector3.up * (Mathf.Sin(swingProgress * Mathf.PI) * footLift);
                state.GroundNormal = Vector3.up;
            }

            state.WasStance = isStance;
        }

        private Vector3 CalculateLandingTarget(
            float phase,
            float stanceFraction,
            float cycleDistance,
            float lateralOffset)
        {
            Vector3 travelDirection = motor.HorizontalSpeed > 0.01f
                ? motor.HorizontalVelocity.normalized
                : transform.forward;
            float remainingTravel = cycleDistance * (1f - phase);
            float leadDistance = cycleDistance * stanceFraction * 0.5f;
            Vector3 candidate = transform.position + travelDirection * (remainingTravel + leadDistance) +
                transform.right * lateralOffset;
            return ProjectToGround(candidate, out _);
        }

        private Vector3 ProjectToGround(Vector3 candidate, out Vector3 groundNormal)
        {
            Vector3 origin = candidate + Vector3.up * 1.25f;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    3f,
                    footGroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                groundNormal = hit.normal;
                return hit.point + hit.normal * footGroundOffset;
            }

            groundNormal = Vector3.up;
            return candidate;
        }

        private void ApplyLegIk(
            Transform thigh,
            Transform knee,
            Transform foot,
            Quaternion thighRestLocalRotation,
            Quaternion kneeRestLocalRotation,
            FootPlantState state,
            float upperLength,
            float lowerLength,
            float weight)
        {
            Vector3 hipPosition = thigh.position;
            Vector3 toTarget = state.CurrentTarget - hipPosition;
            float rawDistance = Mathf.Max(0.001f, toTarget.magnitude);
            Vector3 targetDirection = toTarget / rawDistance;
            float minimumReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
            float maximumReach = upperLength + lowerLength - 0.001f;
            float targetDistance = Mathf.Clamp(rawDistance, minimumReach, maximumReach);
            Vector3 solvedTarget = hipPosition + targetDirection * targetDistance;

            float along = (
                upperLength * upperLength - lowerLength * lowerLength + targetDistance * targetDistance) /
                (2f * targetDistance);
            float bendDistance = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 bendDirection = Vector3.ProjectOnPlane(transform.forward, targetDirection).normalized;
            if (bendDirection.sqrMagnitude < 0.001f)
            {
                bendDirection = Vector3.ProjectOnPlane(transform.right, targetDirection).normalized;
            }

            Vector3 kneePosition = hipPosition + targetDirection * along + bendDirection * bendDistance;
            Vector3 upperDirection = (kneePosition - hipPosition).normalized;
            Vector3 lowerDirection = (solvedTarget - kneePosition).normalized;

            Quaternion baseThighWorld = thigh.rotation;
            Quaternion baseKneeWorld = knee.rotation;
            Quaternion baseFootWorld = foot.rotation;
            Quaternion thighRestWorld = thigh.parent.rotation * thighRestLocalRotation;
            Quaternion desiredThighWorld = Quaternion.FromToRotation(
                thighRestWorld * Vector3.down,
                upperDirection) * thighRestWorld;
            Quaternion kneeRestWorld = desiredThighWorld * kneeRestLocalRotation;
            Quaternion desiredKneeWorld = Quaternion.FromToRotation(
                kneeRestWorld * Vector3.down,
                lowerDirection) * kneeRestWorld;

            Vector3 footForward = Vector3.ProjectOnPlane(transform.forward, state.GroundNormal).normalized;
            if (footForward.sqrMagnitude < 0.001f)
            {
                footForward = transform.forward;
            }

            Quaternion desiredFootWorld = Quaternion.LookRotation(footForward, state.GroundNormal);
            thigh.rotation = Quaternion.Slerp(baseThighWorld, desiredThighWorld, weight);
            knee.rotation = Quaternion.Slerp(baseKneeWorld, desiredKneeWorld, weight);
            foot.rotation = Quaternion.Slerp(baseFootWorld, desiredFootWorld, weight);
        }

        private void CacheRestPose()
        {
            if (!HasCompleteRig)
            {
                poseCached = false;
                return;
            }

            pelvisRestPosition = pelvis.localPosition;
            pelvisRestRotation = pelvis.localRotation;
            chestRestRotation = chest.localRotation;
            leftThighRestRotation = leftThigh.localRotation;
            rightThighRestRotation = rightThigh.localRotation;
            leftKneeRestRotation = leftKnee.localRotation;
            rightKneeRestRotation = rightKnee.localRotation;
            leftFootRestRotation = leftFoot.localRotation;
            rightFootRestRotation = rightFoot.localRotation;
            leftShoulderRestRotation = leftShoulder.localRotation;
            rightShoulderRestRotation = rightShoulder.localRotation;
            leftFootLateralOffset = leftThigh.localPosition.x;
            rightFootLateralOffset = rightThigh.localPosition.x;
            leftUpperLegLength = leftKnee.localPosition.magnitude;
            rightUpperLegLength = rightKnee.localPosition.magnitude;
            leftLowerLegLength = leftFoot.localPosition.magnitude;
            rightLowerLegLength = rightFoot.localPosition.magnitude;
            poseCached = true;
        }

        private void ResetPose()
        {
            pelvis.localPosition = pelvisRestPosition;
            pelvis.localRotation = pelvisRestRotation;
            chest.localRotation = chestRestRotation;
            leftThigh.localRotation = leftThighRestRotation;
            rightThigh.localRotation = rightThighRestRotation;
            leftKnee.localRotation = leftKneeRestRotation;
            rightKnee.localRotation = rightKneeRestRotation;
            leftFoot.localRotation = leftFootRestRotation;
            rightFoot.localRotation = rightFootRestRotation;
            leftShoulder.localRotation = leftShoulderRestRotation;
            rightShoulder.localRotation = rightShoulderRestRotation;
        }
    }
}
