using UnityEngine;
using WorldBuilder.Gameplay.Characters;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class ProceduralHumanoidPresenter : MonoBehaviour
    {
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Transform pelvis;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform rightShoulder;
        [SerializeField, Min(0f)] private float locomotionBlendSpeed = 8f;
        [SerializeField, Min(0f)] private float traversalBlendSpeed = 9f;

        private Vector3 pelvisRestPosition;
        private Quaternion pelvisRestRotation;
        private Quaternion chestRestRotation;
        private Quaternion leftThighRestRotation;
        private Quaternion rightThighRestRotation;
        private Quaternion leftKneeRestRotation;
        private Quaternion rightKneeRestRotation;
        private Quaternion leftShoulderRestRotation;
        private Quaternion rightShoulderRestRotation;
        private float locomotionWeight;
        private float crouchWeight;
        private float airborneWeight;
        private float landingResponse;
        private float stridePhase;
        private bool wasGrounded;
        private bool poseCached;

        public void Configure(
            ThirdPersonMotor movementMotor,
            Transform pelvisTransform,
            Transform chestTransform,
            Transform leftThighTransform,
            Transform rightThighTransform,
            Transform leftKneeTransform,
            Transform rightKneeTransform,
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
            if (!poseCached || motor == null)
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
            }

            landingResponse = Mathf.MoveTowards(landingResponse, 0f, 6f * Time.deltaTime);
            wasGrounded = motor.IsGrounded;

            float runBlend = Mathf.InverseLerp(0.58f, 1f, normalizedSpeed);
            float cyclesPerSecond = Mathf.Lerp(1.55f, 2.65f, runBlend);
            stridePhase += Time.deltaTime * cyclesPerSecond * Mathf.PI * 2f * Mathf.Lerp(0.35f, 1f, locomotionWeight);
            ApplyPose(normalizedSpeed, runBlend);
        }

        private void OnDisable()
        {
            if (poseCached)
            {
                ResetPose();
            }
        }

        private void ApplyPose(float normalizedSpeed, float runBlend)
        {
            float stride = Mathf.Sin(stridePhase);
            float oppositeStride = -stride;
            float groundedLocomotion = locomotionWeight * (1f - airborneWeight);
            float strideAngle = Mathf.Lerp(21f, 38f, runBlend) * groundedLocomotion;
            float armAngle = Mathf.Lerp(16f, 30f, runBlend) * groundedLocomotion;
            float leftKneeBend = Mathf.Max(0f, -stride) * Mathf.Lerp(20f, 48f, runBlend) * groundedLocomotion;
            float rightKneeBend = Mathf.Max(0f, stride) * Mathf.Lerp(20f, 48f, runBlend) * groundedLocomotion;
            float bob = Mathf.Abs(Mathf.Sin(stridePhase * 2f)) * Mathf.Lerp(0.018f, 0.045f, runBlend) * groundedLocomotion;
            float idleBreath = Mathf.Sin(Time.time * 1.7f) * 0.012f * (1f - locomotionWeight);
            float rising = Mathf.Clamp01(motor.VerticalVelocity / 5f) * airborneWeight;
            float falling = Mathf.Clamp01(-motor.VerticalVelocity / 7f) * airborneWeight;
            float crouchStride = stride * 9f * groundedLocomotion * crouchWeight;
            float crouchPelvisDrop = 0.31f * crouchWeight;
            float landingDrop = 0.075f * landingResponse;

            pelvis.localPosition = pelvisRestPosition + Vector3.up * (bob + idleBreath - crouchPelvisDrop - landingDrop);
            pelvis.localRotation = pelvisRestRotation * Quaternion.Euler(
                -5f * crouchWeight,
                0f,
                stride * 2.4f * groundedLocomotion);
            chest.localRotation = chestRestRotation * Quaternion.Euler(
                -idleBreath * 35f + 8f * crouchWeight - 7f * rising + 5f * falling,
                stride * Mathf.Lerp(1.5f, 3.5f, runBlend) * groundedLocomotion,
                -stride * 1.8f * groundedLocomotion);

            leftThigh.localRotation = leftThighRestRotation * Quaternion.Euler(
                stride * strideAngle + 29f * crouchWeight + 12f * rising - 12f * falling + crouchStride,
                0f,
                0f);
            rightThigh.localRotation = rightThighRestRotation * Quaternion.Euler(
                oppositeStride * strideAngle + 29f * crouchWeight - 8f * rising - 12f * falling - crouchStride,
                0f,
                0f);
            leftKnee.localRotation = leftKneeRestRotation * Quaternion.Euler(
                leftKneeBend + 56f * crouchWeight + 24f * rising + 30f * falling + 18f * landingResponse,
                0f,
                0f);
            rightKnee.localRotation = rightKneeRestRotation * Quaternion.Euler(
                rightKneeBend + 56f * crouchWeight + 34f * rising + 30f * falling + 18f * landingResponse,
                0f,
                0f);
            leftShoulder.localRotation = leftShoulderRestRotation * Quaternion.Euler(
                oppositeStride * armAngle - 9f * crouchWeight - 16f * rising + 12f * falling,
                0f,
                0f);
            rightShoulder.localRotation = rightShoulderRestRotation * Quaternion.Euler(
                stride * armAngle - 9f * crouchWeight - 16f * rising + 12f * falling,
                0f,
                0f);
        }

        private void CacheRestPose()
        {
            if (pelvis == null || chest == null || leftThigh == null || rightThigh == null ||
                leftKnee == null || rightKnee == null || leftShoulder == null || rightShoulder == null)
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
            leftShoulderRestRotation = leftShoulder.localRotation;
            rightShoulderRestRotation = rightShoulder.localRotation;
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
            leftShoulder.localRotation = leftShoulderRestRotation;
            rightShoulder.localRotation = rightShoulderRestRotation;
        }
    }
}
