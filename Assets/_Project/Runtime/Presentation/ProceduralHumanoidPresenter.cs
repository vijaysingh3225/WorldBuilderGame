using UnityEngine;
using WorldBuilder.Gameplay.Characters;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class ProceduralHumanoidPresenter : MonoBehaviour
    {
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Transform pelvis;
        [SerializeField] private Transform body;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform rightShoulder;
        [SerializeField] private Transform leftElbow;
        [SerializeField] private Transform rightElbow;
        [SerializeField, Min(0f)] private float locomotionBlendSpeed = 8f;
        [SerializeField, Min(0f)] private float traversalBlendSpeed = 9f;
        [SerializeField, Min(0f)] private float localGaitBlendSpeed = 12f;

        private Vector3 pelvisRestPosition;
        private Quaternion pelvisRestRotation;
        private Vector3 bodyRestPosition;
        private Quaternion bodyRestRotation;
        private Quaternion chestRestRotation;
        private Quaternion leftThighRestRotation;
        private Quaternion rightThighRestRotation;
        private Quaternion leftKneeRestRotation;
        private Quaternion rightKneeRestRotation;
        private Quaternion leftFootRestRotation;
        private Quaternion rightFootRestRotation;
        private Quaternion leftShoulderRestRotation;
        private Quaternion rightShoulderRestRotation;
        private Quaternion leftElbowRestRotation;
        private Quaternion rightElbowRestRotation;
        private float leftFootLateralOffset;
        private float rightFootLateralOffset;
        private float leftFootRestHeight;
        private float rightFootRestHeight;
        private float leftUpperLegLength;
        private float rightUpperLegLength;
        private float leftLowerLegLength;
        private float rightLowerLegLength;
        private float locomotionWeight;
        private float crouchWeight;
        private float airborneWeight;
        private float landingResponse;
        private float localGaitWeight;
        private float strideCycle;
        private bool wasGrounded;
        private bool poseCached;

        private bool HasCompleteRig => pelvis != null && body != null && chest != null && leftThigh != null && rightThigh != null &&
            leftKnee != null && rightKnee != null && leftFoot != null && rightFoot != null &&
            leftShoulder != null && rightShoulder != null && leftElbow != null && rightElbow != null;

        public void Configure(
            ThirdPersonMotor movementMotor,
            Transform pelvisTransform,
            Transform bodyTransform,
            Transform chestTransform,
            Transform leftThighTransform,
            Transform rightThighTransform,
            Transform leftKneeTransform,
            Transform rightKneeTransform,
            Transform leftFootTransform,
            Transform rightFootTransform,
            Transform leftShoulderTransform,
            Transform rightShoulderTransform,
            Transform leftElbowTransform,
            Transform rightElbowTransform)
        {
            motor = movementMotor;
            pelvis = pelvisTransform;
            body = bodyTransform;
            chest = chestTransform;
            leftThigh = leftThighTransform;
            rightThigh = rightThighTransform;
            leftKnee = leftKneeTransform;
            rightKnee = rightKneeTransform;
            leftFoot = leftFootTransform;
            rightFoot = rightFootTransform;
            leftShoulder = leftShoulderTransform;
            rightShoulder = rightShoulderTransform;
            leftElbow = leftElbowTransform;
            rightElbow = rightElbowTransform;
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
            float targetLocomotionWeight = Mathf.InverseLerp(0.02f, 0.18f, normalizedSpeed);
            locomotionWeight = Mathf.MoveTowards(
                locomotionWeight,
                targetLocomotionWeight,
                locomotionBlendSpeed * Time.deltaTime);
            crouchWeight = Mathf.MoveTowards(crouchWeight, motor.CrouchAmount, traversalBlendSpeed * Time.deltaTime);
            airborneWeight = Mathf.MoveTowards(
                airborneWeight,
                motor.IsGrounded ? 0f : 1f,
                traversalBlendSpeed * Time.deltaTime);

            if (!wasGrounded && motor.IsGrounded)
            {
                landingResponse = 1f;
            }

            landingResponse = Mathf.MoveTowards(landingResponse, 0f, 6f * Time.deltaTime);
            wasGrounded = motor.IsGrounded;

            float runBlend = Mathf.InverseLerp(0.58f, 1f, normalizedSpeed);
            float cadenceDistance = Mathf.Lerp(2.60f, 2.65f, runBlend);
            cadenceDistance = Mathf.Lerp(cadenceDistance, 1.35f, crouchWeight);
            float gaitTravelDistance = Mathf.Lerp(1.45f, 2.05f, runBlend);
            gaitTravelDistance = Mathf.Lerp(gaitTravelDistance, 0.82f, crouchWeight);
            if (motor.IsGrounded && motor.HorizontalSpeed > 0.02f)
            {
                strideCycle = Mathf.Repeat(
                    strideCycle + motor.HorizontalSpeed * Time.deltaTime / Mathf.Max(0.01f, cadenceDistance),
                    1f);
            }

            ApplyBasePose(runBlend);

            float desiredGaitWeight = motor.IsGrounded ? locomotionWeight : 0f;
            localGaitWeight = Mathf.MoveTowards(
                localGaitWeight,
                desiredGaitWeight,
                localGaitBlendSpeed * Time.deltaTime);
            if (motor.IsGrounded && localGaitWeight > 0.001f)
            {
                ApplyLocalGait(runBlend, gaitTravelDistance, localGaitWeight);
            }
        }

        private void OnDisable()
        {
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
            float stationaryCrouch = crouchWeight * (1f - groundedLocomotion);
            float movingCrouch = crouchWeight * groundedLocomotion;
            float strideAngle = Mathf.Lerp(22f, 37f, runBlend) * groundedLocomotion;
            float armAngle = Mathf.Lerp(17f, 25f, runBlend) * groundedLocomotion;
            float leftWalkingKnee = Mathf.Max(0f, -stride) * Mathf.Lerp(24f, 52f, runBlend) * groundedLocomotion;
            float rightWalkingKnee = Mathf.Max(0f, stride) * Mathf.Lerp(24f, 52f, runBlend) * groundedLocomotion;
            float bob = Mathf.Abs(Mathf.Sin(strideRadians * 2f)) * Mathf.Lerp(0.012f, 0.035f, runBlend) * groundedLocomotion;
            float idleBreath = Mathf.Sin(Time.time * 1.7f) * 0.012f * (1f - locomotionWeight);
            float rising = Mathf.Clamp01(motor.VerticalVelocity / 5f) * airborneWeight;
            float falling = Mathf.Clamp01(-motor.VerticalVelocity / 7f) * airborneWeight;
            float crouchPelvisDrop = Mathf.Lerp(0.45f, 0.28f, groundedLocomotion) * crouchWeight;
            float gaitPelvisDrop = Mathf.Lerp(0.12f, 0.14f, runBlend) * groundedLocomotion * (1f - crouchWeight);
            float landingDrop = 0.075f * landingResponse;

            pelvis.localPosition = pelvisRestPosition +
                Vector3.up * (bob + idleBreath - crouchPelvisDrop - gaitPelvisDrop - landingDrop);
            pelvis.localRotation = pelvisRestRotation * Quaternion.Euler(
                3f * movingCrouch,
                0f,
                stride * 2.2f * groundedLocomotion);
            body.localPosition = bodyRestPosition + Vector3.back * (0.13f * stationaryCrouch);
            body.localRotation = bodyRestRotation * Quaternion.Euler(
                -5f * stationaryCrouch + 3f * movingCrouch,
                0f,
                0f);
            chest.localRotation = chestRestRotation * Quaternion.Euler(
                -idleBreath * 35f + 5f * stationaryCrouch + 8f * movingCrouch + 6f * runBlend * groundedLocomotion -
                    7f * rising + 5f * falling,
                stride * Mathf.Lerp(1.5f, 4f, runBlend) * groundedLocomotion,
                -stride * 1.8f * groundedLocomotion);

            float leftStandingThigh = stride * strideAngle + 12f * rising - 12f * falling;
            float rightStandingThigh = -stride * strideAngle - 8f * rising - 12f * falling;
            float leftCrouchThigh = Mathf.Lerp(-12f, -25f, groundedLocomotion);
            float rightCrouchThigh = Mathf.Lerp(-58f, -25f, groundedLocomotion);
            float leftThighAngle = Mathf.Lerp(leftStandingThigh, leftCrouchThigh, crouchWeight);
            float rightThighAngle = Mathf.Lerp(rightStandingThigh, rightCrouchThigh, crouchWeight);

            float leftStandingKnee = leftWalkingKnee + 24f * rising + 30f * falling + 18f * landingResponse;
            float rightStandingKnee = rightWalkingKnee + 34f * rising + 30f * falling + 18f * landingResponse;
            float leftCrouchKnee = Mathf.Lerp(92f, 72f, groundedLocomotion);
            float rightCrouchKnee = Mathf.Lerp(117f, 72f, groundedLocomotion);
            float leftKneeAngle = Mathf.Lerp(leftStandingKnee, leftCrouchKnee, crouchWeight);
            float rightKneeAngle = Mathf.Lerp(rightStandingKnee, rightCrouchKnee, crouchWeight);

            leftThigh.localRotation = leftThighRestRotation * Quaternion.Euler(leftThighAngle, 0f, 0f);
            rightThigh.localRotation = rightThighRestRotation * Quaternion.Euler(rightThighAngle, 0f, 0f);
            leftKnee.localRotation = leftKneeRestRotation * Quaternion.Euler(leftKneeAngle, 0f, 0f);
            rightKnee.localRotation = rightKneeRestRotation * Quaternion.Euler(rightKneeAngle, 0f, 0f);
            leftFoot.localRotation = leftFootRestRotation * Quaternion.Euler(-(leftThighAngle + leftKneeAngle), 0f, 0f);
            rightFoot.localRotation = rightFootRestRotation * Quaternion.Euler(-(rightThighAngle + rightKneeAngle), 0f, 0f);

            float leftShoulderAngle = -stride * armAngle - 20f * stationaryCrouch - 10f * movingCrouch -
                16f * rising + 12f * falling;
            float rightShoulderAngle = stride * armAngle - 36f * stationaryCrouch - 10f * movingCrouch -
                16f * rising + 12f * falling;
            float joggingElbowBend = Mathf.Lerp(6f, 68f, runBlend) * groundedLocomotion;
            float crouchElbowBend = 30f * stationaryCrouch + 45f * movingCrouch;
            float elbowBend = Mathf.Max(joggingElbowBend, crouchElbowBend);

            leftShoulder.localRotation = leftShoulderRestRotation * Quaternion.Euler(leftShoulderAngle, 0f, 0f);
            rightShoulder.localRotation = rightShoulderRestRotation * Quaternion.Euler(rightShoulderAngle, 0f, 0f);
            leftElbow.localRotation = leftElbowRestRotation * Quaternion.Euler(-elbowBend, 0f, 0f);
            rightElbow.localRotation = rightElbowRestRotation * Quaternion.Euler(-elbowBend, 0f, 0f);
        }

        private void ApplyLocalGait(float runBlend, float cycleDistance, float weight)
        {
            float stanceFraction = Mathf.Lerp(0.62f, 0.45f, runBlend);
            stanceFraction = Mathf.Lerp(stanceFraction, 0.64f, crouchWeight);
            float footLift = Mathf.Lerp(0.12f, 0.20f, runBlend);
            footLift = Mathf.Lerp(footLift, 0.09f, crouchWeight);

            Vector3 leftTarget = CalculateLocalFootTarget(
                strideCycle,
                leftFootLateralOffset,
                leftFootRestHeight,
                cycleDistance,
                stanceFraction,
                footLift);
            Vector3 rightTarget = CalculateLocalFootTarget(
                Mathf.Repeat(strideCycle + 0.5f, 1f),
                rightFootLateralOffset,
                rightFootRestHeight,
                cycleDistance,
                stanceFraction,
                footLift);

            ApplyLocalLegIk(
                leftThigh,
                leftKnee,
                leftFoot,
                leftThighRestRotation,
                leftKneeRestRotation,
                leftTarget,
                leftUpperLegLength,
                leftLowerLegLength,
                weight);
            ApplyLocalLegIk(
                rightThigh,
                rightKnee,
                rightFoot,
                rightThighRestRotation,
                rightKneeRestRotation,
                rightTarget,
                rightUpperLegLength,
                rightLowerLegLength,
                weight);
        }

        private static Vector3 CalculateLocalFootTarget(
            float phase,
            float lateralOffset,
            float footHeight,
            float cycleDistance,
            float stanceFraction,
            float footLift)
        {
            float halfStanceTravel = cycleDistance * stanceFraction * 0.5f;
            if (phase < stanceFraction)
            {
                float stanceProgress = phase / Mathf.Max(0.001f, stanceFraction);
                float forwardPosition = Mathf.Lerp(halfStanceTravel, -halfStanceTravel, stanceProgress);
                return new Vector3(lateralOffset, footHeight, forwardPosition);
            }

            float swingProgress = Mathf.InverseLerp(stanceFraction, 1f, phase);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, swingProgress);
            float swingPosition = Mathf.Lerp(-halfStanceTravel, halfStanceTravel, smoothProgress);
            float lift = Mathf.Sin(swingProgress * Mathf.PI) * footLift;
            return new Vector3(lateralOffset, footHeight + lift, swingPosition);
        }

        private void ApplyLocalLegIk(
            Transform thigh,
            Transform knee,
            Transform foot,
            Quaternion thighRestLocalRotation,
            Quaternion kneeRestLocalRotation,
            Vector3 localFootTarget,
            float upperLength,
            float lowerLength,
            float weight)
        {
            Vector3 hipPosition = thigh.position;
            Vector3 requestedTarget = transform.TransformPoint(localFootTarget);
            Vector3 toTarget = requestedTarget - hipPosition;
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
            Quaternion desiredFootWorld = Quaternion.LookRotation(transform.forward, transform.up);

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
            bodyRestPosition = body.localPosition;
            bodyRestRotation = body.localRotation;
            chestRestRotation = chest.localRotation;
            leftThighRestRotation = leftThigh.localRotation;
            rightThighRestRotation = rightThigh.localRotation;
            leftKneeRestRotation = leftKnee.localRotation;
            rightKneeRestRotation = rightKnee.localRotation;
            leftFootRestRotation = leftFoot.localRotation;
            rightFootRestRotation = rightFoot.localRotation;
            leftShoulderRestRotation = leftShoulder.localRotation;
            rightShoulderRestRotation = rightShoulder.localRotation;
            leftElbowRestRotation = leftElbow.localRotation;
            rightElbowRestRotation = rightElbow.localRotation;
            leftFootLateralOffset = leftThigh.localPosition.x;
            rightFootLateralOffset = rightThigh.localPosition.x;
            leftFootRestHeight = transform.InverseTransformPoint(leftFoot.position).y;
            rightFootRestHeight = transform.InverseTransformPoint(rightFoot.position).y;
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
            body.localPosition = bodyRestPosition;
            body.localRotation = bodyRestRotation;
            chest.localRotation = chestRestRotation;
            leftThigh.localRotation = leftThighRestRotation;
            rightThigh.localRotation = rightThighRestRotation;
            leftKnee.localRotation = leftKneeRestRotation;
            rightKnee.localRotation = rightKneeRestRotation;
            leftFoot.localRotation = leftFootRestRotation;
            rightFoot.localRotation = rightFootRestRotation;
            leftShoulder.localRotation = leftShoulderRestRotation;
            rightShoulder.localRotation = rightShoulderRestRotation;
            leftElbow.localRotation = leftElbowRestRotation;
            rightElbow.localRotation = rightElbowRestRotation;
        }
    }
}
