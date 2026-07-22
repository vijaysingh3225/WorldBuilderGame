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
        private float stridePhase;
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
            float strideAngle = Mathf.Lerp(21f, 38f, runBlend) * locomotionWeight;
            float armAngle = Mathf.Lerp(16f, 30f, runBlend) * locomotionWeight;
            float leftKneeBend = Mathf.Max(0f, -stride) * Mathf.Lerp(20f, 48f, runBlend) * locomotionWeight;
            float rightKneeBend = Mathf.Max(0f, stride) * Mathf.Lerp(20f, 48f, runBlend) * locomotionWeight;
            float bob = Mathf.Abs(Mathf.Sin(stridePhase * 2f)) * Mathf.Lerp(0.018f, 0.045f, runBlend) * locomotionWeight;
            float idleBreath = Mathf.Sin(Time.time * 1.7f) * 0.012f * (1f - locomotionWeight);

            pelvis.localPosition = pelvisRestPosition + Vector3.up * (bob + idleBreath);
            pelvis.localRotation = pelvisRestRotation * Quaternion.Euler(0f, 0f, stride * 2.4f * locomotionWeight);
            chest.localRotation = chestRestRotation * Quaternion.Euler(
                -idleBreath * 35f,
                stride * Mathf.Lerp(1.5f, 3.5f, runBlend) * locomotionWeight,
                -stride * 1.8f * locomotionWeight);

            leftThigh.localRotation = leftThighRestRotation * Quaternion.Euler(stride * strideAngle, 0f, 0f);
            rightThigh.localRotation = rightThighRestRotation * Quaternion.Euler(oppositeStride * strideAngle, 0f, 0f);
            leftKnee.localRotation = leftKneeRestRotation * Quaternion.Euler(leftKneeBend, 0f, 0f);
            rightKnee.localRotation = rightKneeRestRotation * Quaternion.Euler(rightKneeBend, 0f, 0f);
            leftShoulder.localRotation = leftShoulderRestRotation * Quaternion.Euler(oppositeStride * armAngle, 0f, 0f);
            rightShoulder.localRotation = rightShoulderRestRotation * Quaternion.Euler(stride * armAngle, 0f, 0f);
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
