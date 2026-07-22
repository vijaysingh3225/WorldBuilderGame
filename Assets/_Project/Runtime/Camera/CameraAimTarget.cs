using UnityEngine;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.CameraSystem
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraAimTarget : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.45f, 0f);
        [SerializeField] private float initialPitch = 12f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-30f, 65f);
        [SerializeField, Min(0f)] private float rotationSmoothTime = 0.035f;

        private float desiredYaw;
        private float desiredPitch;
        private float currentYaw;
        private float currentPitch;
        private float yawVelocity;
        private float pitchVelocity;

        public void Configure(Transform target, PlayerInputSource intentSource)
        {
            followTarget = target;
            input = intentSource;
            desiredYaw = target != null ? target.eulerAngles.y : 0f;
            currentYaw = desiredYaw;
            desiredPitch = initialPitch;
            currentPitch = desiredPitch;
            SnapToTarget();
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
        }

        private void Update()
        {
            if (followTarget == null || input == null)
            {
                return;
            }

            Vector2 look = input.CurrentIntent.Look;
            desiredYaw += look.x;
            desiredPitch = Mathf.Clamp(desiredPitch - look.y, pitchLimits.x, pitchLimits.y);

            if (rotationSmoothTime <= 0f)
            {
                currentYaw = desiredYaw;
                currentPitch = desiredPitch;
            }
            else
            {
                currentYaw = Mathf.SmoothDampAngle(currentYaw, desiredYaw, ref yawVelocity, rotationSmoothTime);
                currentPitch = Mathf.SmoothDampAngle(currentPitch, desiredPitch, ref pitchVelocity, rotationSmoothTime);
            }

            SnapToTarget();
        }

        private void SnapToTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            transform.SetPositionAndRotation(
                followTarget.position + followOffset,
                Quaternion.Euler(currentPitch, currentYaw, 0f));
        }
    }
}
