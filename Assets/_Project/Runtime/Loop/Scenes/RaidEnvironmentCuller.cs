using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class RaidEnvironmentCuller : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            public Vector3 Center;
            public Renderer[] Renderers;
            public bool[] RendererDefaults;
            public Collider[] Colliders;
            public bool[] ColliderDefaults;
            public bool RenderersEnabled = true;
            public bool CollidersEnabled = true;
        }

        [SerializeField, Min(1f)]
        private float renderDistance = 98f;
        [SerializeField]
        private bool cullRenderersByDistance;
        [SerializeField, Min(1f)]
        private float colliderDistance = 112f;
        [SerializeField, Min(0f)]
        private float hysteresis = 3f;
        [SerializeField, Min(0.05f)]
        private float updateInterval = 0.25f;

        private readonly List<Entry> entries =
            new List<Entry>();
        private Transform anchor;
        private float nextUpdateAt;

        public float RenderDistance => renderDistance;
        public float ColliderDistance => colliderDistance;
        public bool RendererDistanceCullingEnabled =>
            cullRenderersByDistance;
        public int EntryCount => entries.Count;

        public void Configure(
            Transform distanceAnchor,
            params Transform[] environmentRoots)
        {
            anchor = distanceAnchor;
            entries.Clear();
            if (environmentRoots != null)
            {
                foreach (Transform root in environmentRoots)
                {
                    AddRootEntries(root);
                }
            }

            nextUpdateAt = 0f;
            if (Application.isPlaying)
            {
                RefreshImmediately();
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextUpdateAt)
            {
                return;
            }

            RefreshImmediately();
            nextUpdateAt =
                Time.unscaledTime + updateInterval;
        }

        public void RefreshImmediately()
        {
            if (anchor == null)
            {
                return;
            }

            Vector3 anchorPosition = anchor.position;
            float renderEnableSquared =
                Mathf.Max(1f, renderDistance - hysteresis);
            renderEnableSquared *= renderEnableSquared;
            float renderDisableSquared =
                renderDistance + hysteresis;
            renderDisableSquared *= renderDisableSquared;
            float colliderEnableSquared =
                Mathf.Max(1f, colliderDistance - hysteresis);
            colliderEnableSquared *= colliderEnableSquared;
            float colliderDisableSquared =
                colliderDistance + hysteresis;
            colliderDisableSquared *= colliderDisableSquared;

            for (int index = entries.Count - 1;
                 index >= 0;
                 index--)
            {
                Entry entry = entries[index];
                if (!HasLiveObjects(entry))
                {
                    entries.RemoveAt(index);
                    continue;
                }

                Vector3 delta = entry.Center - anchorPosition;
                delta.y = 0f;
                float distanceSquared = delta.sqrMagnitude;
                bool renderersEnabled =
                    !cullRenderersByDistance ||
                    (entry.RenderersEnabled
                        ? distanceSquared <= renderDisableSquared
                        : distanceSquared <= renderEnableSquared);
                bool collidersEnabled = entry.CollidersEnabled
                    ? distanceSquared <= colliderDisableSquared
                    : distanceSquared <= colliderEnableSquared;
                SetRenderers(entry, renderersEnabled);
                SetColliders(entry, collidersEnabled);
            }
        }

        private void AddRootEntries(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int childIndex = 0;
                 childIndex < root.childCount;
                 childIndex++)
            {
                Transform child = root.GetChild(childIndex);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }
                Renderer[] renderers =
                    child.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders =
                    child.GetComponentsInChildren<Collider>(true);
                if (renderers.Length == 0 &&
                    colliders.Length == 0)
                {
                    continue;
                }

                Vector3 center = child.position;
                if (TryGetBounds(renderers, out Bounds bounds))
                {
                    center = bounds.center;
                }
                entries.Add(
                    new Entry
                    {
                        Center = center,
                        Renderers = renderers,
                        RendererDefaults = CaptureDefaults(
                            renderers),
                        Colliders = colliders,
                        ColliderDefaults = CaptureDefaults(
                            colliders)
                    });
            }
        }

        private static bool[] CaptureDefaults(
            Renderer[] renderers)
        {
            var defaults = new bool[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                defaults[index] =
                    renderers[index] != null &&
                    renderers[index].enabled;
            }
            return defaults;
        }

        private static bool[] CaptureDefaults(
            Collider[] colliders)
        {
            var defaults = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                defaults[index] =
                    colliders[index] != null &&
                    colliders[index].enabled;
            }
            return defaults;
        }

        private static bool TryGetBounds(
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
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
            return found;
        }

        private static bool HasLiveObjects(Entry entry)
        {
            foreach (Renderer renderer in entry.Renderers)
            {
                if (renderer != null)
                {
                    return true;
                }
            }
            foreach (Collider collider in entry.Colliders)
            {
                if (collider != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static void SetRenderers(
            Entry entry,
            bool enabled)
        {
            if (entry.RenderersEnabled == enabled)
            {
                return;
            }
            entry.RenderersEnabled = enabled;
            for (int index = 0;
                 index < entry.Renderers.Length;
                 index++)
            {
                Renderer renderer = entry.Renderers[index];
                if (renderer != null)
                {
                    renderer.enabled =
                        enabled && entry.RendererDefaults[index];
                }
            }
        }

        private static void SetColliders(
            Entry entry,
            bool enabled)
        {
            if (entry.CollidersEnabled == enabled)
            {
                return;
            }
            entry.CollidersEnabled = enabled;
            for (int index = 0;
                 index < entry.Colliders.Length;
                 index++)
            {
                Collider collider = entry.Colliders[index];
                if (collider != null)
                {
                    collider.enabled =
                        enabled && entry.ColliderDefaults[index];
                }
            }
        }
    }
}
