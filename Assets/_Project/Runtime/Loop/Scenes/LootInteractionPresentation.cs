using UnityEngine;
using System.Collections.Generic;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public static class LootInteractionPresentation
    {
        public const float DefaultDistance = 2.25f;
        public const float AimPointViewportX = 0.5f;
        public const float AimPointViewportY = 0.43f;
        public const float TorsoHeightOffset = 0.08f;
        private static readonly RaycastHit[] FocusHits =
            new RaycastHit[32];
        private static readonly List<Renderer> RendererBuffer =
            new List<Renderer>(16);

        public static bool IsFocused(
            Transform player,
            Transform target,
            float interactionDistance,
            bool allowRendererBoundsFallback = false)
        {
            return TryGetFocusScore(
                player,
                target,
                interactionDistance,
                out _,
                allowRendererBoundsFallback);
        }

        public static bool IsWithinInteractionDistance(
            Transform player,
            Transform target,
            float interactionDistance)
        {
            if (player == null || target == null)
            {
                return false;
            }

            Bounds bounds = ResolveBounds(target);
            float effectiveDistance = Mathf.Min(
                interactionDistance,
                DefaultDistance);
            return Vector3.Distance(
                    player.position,
                    bounds.ClosestPoint(player.position)) <=
                effectiveDistance;
        }

        public static bool TryGetFocusScore(
            Transform player,
            Transform target,
            float interactionDistance,
            out float score,
            bool allowRendererBoundsFallback = false)
        {
            score = float.PositiveInfinity;
            Camera camera = Camera.main;
            if (player == null || target == null || camera == null)
            {
                return false;
            }

            if (!IsWithinInteractionDistance(
                    player,
                    target,
                    interactionDistance))
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(
                CalculateAimPoint(
                    camera,
                    player,
                    Screen.width,
                    Screen.height));
            RaycastHit[] hits = FocusHits;
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hits,
                camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            // Physics does not guarantee that a full non-alloc buffer contains
            // the nearest hit. Use the original complete query only when the
            // bounded buffer is saturated so focus behavior stays unchanged.
            if (hitCount == hits.Length)
            {
                hits = Physics.RaycastAll(
                    ray,
                    camera.farClipPlane,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            float firstBlockingDistance = float.PositiveInfinity;
            Transform firstBlockingTransform = null;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = hits[index].collider;
                Transform hit = collider != null
                    ? collider.transform
                    : null;
                if (hit == null || hit.IsChildOf(player))
                {
                    continue;
                }

                if (hits[index].distance >= firstBlockingDistance)
                {
                    continue;
                }

                firstBlockingDistance = hits[index].distance;
                firstBlockingTransform = hit;
            }

            if (firstBlockingTransform != null &&
                (firstBlockingTransform == target ||
                 firstBlockingTransform.IsChildOf(target)))
            {
                score = firstBlockingDistance;
                return true;
            }

            if (allowRendererBoundsFallback &&
                TryIntersectRendererBounds(
                    ray,
                    target,
                    out float rendererDistance) &&
                rendererDistance <= firstBlockingDistance + 0.05f)
            {
                score = rendererDistance;
                return true;
            }
            return false;
        }

        public static bool TryIntersectRendererBounds(
            Ray ray,
            Transform target,
            out float nearestDistance)
        {
            nearestDistance = float.PositiveInfinity;
            if (target == null)
            {
                return false;
            }
            target.GetComponentsInChildren(false, RendererBuffer);
            for (int index = 0; index < RendererBuffer.Count; index++)
            {
                Renderer renderer = RendererBuffer[index];
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    !renderer.bounds.IntersectRay(
                        ray,
                        out float distance) ||
                    distance >= nearestDistance)
                {
                    continue;
                }
                nearestDistance = distance;
            }
            RendererBuffer.Clear();
            return nearestDistance < float.PositiveInfinity;
        }

        public static Vector3 CalculateAimPoint(
            float screenWidth,
            float screenHeight)
        {
            return new Vector3(
                screenWidth * AimPointViewportX,
                screenHeight * AimPointViewportY,
                0f);
        }

        public static Vector3 CalculateAimPoint(
            Camera camera,
            Transform player,
            float screenWidth,
            float screenHeight)
        {
            if (camera == null || player == null)
            {
                return CalculateAimPoint(screenWidth, screenHeight);
            }

            CharacterController controller =
                player.GetComponent<CharacterController>();
            Vector3 torsoPoint = controller != null
                ? player.TransformPoint(
                    controller.center +
                    Vector3.up * controller.height * TorsoHeightOffset)
                : player.position + player.up * 1.05f;
            Vector3 viewportPoint = camera.WorldToViewportPoint(torsoPoint);
            if (viewportPoint.z <= 0f)
            {
                return CalculateAimPoint(screenWidth, screenHeight);
            }

            return new Vector3(
                viewportPoint.x * screenWidth,
                viewportPoint.y * screenHeight,
                0f);
        }

        public static void DrawPrompt(string action)
        {
            Rect prompt = new Rect(
                Screen.width * 0.5f - 82f,
                Mathf.Min(
                    Screen.height - 36f,
                    Screen.height * 0.78f),
                164f,
                28f);
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.03f, 0.035f, 0.52f);
            GUI.DrawTexture(prompt, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.88f);
            GUI.Label(
                prompt,
                $"{PlayerControlBindings.KeyName(PlayerControlBindings.GetKey(PlayerControl.Interact))}  \u2014  {action}",
                LoopSceneGui.Centered);
            GUI.color = previous;
        }

        private static Bounds ResolveBounds(Transform target)
        {
            target.GetComponentsInChildren(false, RendererBuffer);
            bool found = false;
            Bounds bounds = new Bounds(target.position, Vector3.one * 0.2f);
            for (int index = 0; index < RendererBuffer.Count; index++)
            {
                Renderer renderer = RendererBuffer[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            RendererBuffer.Clear();
            return bounds;
        }
    }
}
