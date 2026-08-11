using UnityEngine;

namespace WorldBuilder.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterAimSource : MonoBehaviour
    {
        private bool overrideActive;
        [SerializeField] private bool cameraFallbackAllowed = true;
        private Vector3 origin;
        private Vector3 direction = Vector3.forward;

        public bool OverrideActive => overrideActive;
        public Vector3 Origin => origin;
        public Vector3 Direction => direction;
        public bool CameraFallbackAllowed => cameraFallbackAllowed;

        public void SetCameraFallbackAllowed(bool allowed)
        {
            cameraFallbackAllowed = allowed;
        }

        public void SetOverride(Vector3 worldOrigin, Vector3 worldDirection)
        {
            origin = worldOrigin;
            direction = worldDirection.sqrMagnitude > 0.0001f
                ? worldDirection.normalized
                : transform.forward;
            overrideActive = true;
        }

        public void ClearOverride()
        {
            overrideActive = false;
        }

        public bool TryGetRay(out Ray ray)
        {
            ray = new Ray(origin, direction);
            return overrideActive;
        }
    }
}
