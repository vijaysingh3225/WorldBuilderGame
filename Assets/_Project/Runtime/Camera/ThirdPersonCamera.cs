using UnityEngine;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.CameraSystem
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private Vector3 focusOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField, Min(0.5f)] private float distance = 4.8f;
        [SerializeField] private float shoulderOffset = 0.65f;
        [SerializeField] private float initialPitch = 14f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-25f, 65f);
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.045f;
        [SerializeField, Min(0f)] private float shoulderSwitchSmoothTime = 0.22f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private float yaw;
        private float pitch;
        private Vector3 positionVelocity;
        private float shoulderSideBlend = 1f;
        private float shoulderSideVelocity;

        public float DesiredDistance => distance;
        public float ShoulderOffset => shoulderOffset;
        public float PositionSmoothTime => positionSmoothTime;

        public void Configure(Transform followTarget, PlayerInputSource intentSource)
        {
            target = followTarget;
            input = intentSource;
            collisionMask = ~(1 << 2);
        }

        private void Awake()
        {
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            pitch = initialPitch;
            shoulderSideBlend = input != null ? input.ShoulderSide : 1f;
        }

        private void LateUpdate()
        {
            if (target == null || input == null)
            {
                return;
            }

            Vector2 look = input.CurrentIntent.Look;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, pitchLimits.x, pitchLimits.y);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + focusOffset;
            shoulderSideBlend = shoulderSwitchSmoothTime <= 0f
                ? input.ShoulderSide
                : Mathf.SmoothDamp(
                    shoulderSideBlend,
                    input.ShoulderSide,
                    ref shoulderSideVelocity,
                    shoulderSwitchSmoothTime);
            Vector3 offset = rotation * new Vector3(
                shoulderOffset * shoulderSideBlend,
                0f,
                -distance);
            Vector3 desiredPosition = focus + offset;
            Vector3 ray = desiredPosition - focus;

            if (Physics.SphereCast(focus, collisionRadius, ray.normalized, out RaycastHit hit, ray.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
            {
                desiredPosition = focus + ray.normalized * Mathf.Max(0.15f, hit.distance - collisionRadius);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);
            transform.rotation = rotation;
        }
    }
}
