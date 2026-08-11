using UnityEngine;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public static class LootInteractionPresentation
    {
        public const float DefaultDistance = 2.25f;
        public const float AimPointViewportX = 0.5f;
        public const float AimPointViewportY = 0.43f;

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
                CalculateAimPoint(Screen.width, Screen.height));
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(
                hits,
                (left, right) => left.distance.CompareTo(right.distance));
            float firstBlockingDistance = float.PositiveInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                Transform hit = hits[index].collider.transform;
                if (hit.IsChildOf(player))
                {
                    continue;
                }

                if (hit == target || hit.IsChildOf(target))
                {
                    score = hits[index].distance;
                    return true;
                }

                firstBlockingDistance = hits[index].distance;
                break;
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
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
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
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            bool found = false;
            Bounds bounds = new Bounds(target.position, Vector3.one * 0.2f);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
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
            return bounds;
        }
    }
}
