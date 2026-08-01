using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageNumberOverlay : MonoBehaviour
    {
        private const float Lifetime = 1.05f;
        public const int MinimumFontSize = 14;
        public const int MaximumFontSize = 18;
        private const float HorizontalOffset = 38f;

        private sealed class Entry
        {
            public float amount;
            public Vector3 worldPoint;
            public float createdAt;
            public bool critical;
            public float horizontalDirection;
        }

        private readonly List<Entry> entries = new List<Entry>(8);
        private GUIStyle numberStyle;
        private GUIStyle shadowStyle;
        private int placementSequence;

        public int ActiveNumberCount => entries.Count;

        public static FloatingDamageNumberOverlay GetOrCreate()
        {
            FloatingDamageNumberOverlay existing =
                FindFirstObjectByType<FloatingDamageNumberOverlay>();
            if (existing != null)
            {
                return existing;
            }

            Camera camera = Camera.main;
            GameObject host = camera != null
                ? camera.gameObject
                : new GameObject("Floating Damage Numbers");
            return host.AddComponent<FloatingDamageNumberOverlay>();
        }

        public void Show(float amount, Vector3 worldPoint, bool critical)
        {
            entries.Add(new Entry
            {
                amount = amount,
                worldPoint = worldPoint,
                createdAt = Time.time,
                critical = critical,
                horizontalDirection = (placementSequence++ & 1) == 0
                    ? 1f
                    : -1f
            });
        }

        private void Update()
        {
            float now = Time.time;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (now - entries[index].createdAt >= Lifetime)
                {
                    entries.RemoveAt(index);
                }
            }
        }

        private void OnGUI()
        {
            if (entries.Count == 0 || Camera.main == null)
            {
                return;
            }

            Camera camera = Camera.main;
            EnsureStyles();
            float now = Time.time;
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                float normalizedAge = Mathf.Clamp01(
                    (now - entry.createdAt) / Lifetime);
                Vector3 screen = camera.WorldToScreenPoint(
                    entry.worldPoint +
                    Vector3.up * Mathf.Lerp(0.14f, 0.38f, normalizedAge));
                if (screen.z <= 0f)
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    camera.transform.position,
                    entry.worldPoint);
                int fontSize = Mathf.RoundToInt(Mathf.Lerp(
                    MaximumFontSize,
                    MinimumFontSize,
                    Mathf.InverseLerp(5f, 45f, distance)));
                float alpha = 0.88f * (1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.62f, 1f, normalizedAge)));

                numberStyle.fontSize = fontSize;
                shadowStyle.fontSize = fontSize;
                numberStyle.normal.textColor = entry.critical
                    ? new Color(1f, 0.38f, 0.20f, alpha)
                    : new Color(1f, 0.88f, 0.54f, alpha);
                shadowStyle.normal.textColor =
                    new Color(0f, 0f, 0f, alpha * 0.72f);

                float x = screen.x +
                    HorizontalOffset * entry.horizontalDirection;
                Rect rect = new Rect(
                    x - 35f,
                    Screen.height - screen.y - 13f,
                    70f,
                    26f);
                Rect shadow = rect;
                shadow.x += 1f;
                shadow.y += 1f;
                string label = Mathf.RoundToInt(entry.amount).ToString();
                GUI.Label(shadow, label, shadowStyle);
                GUI.Label(rect, label, numberStyle);
            }
        }

        private void EnsureStyles()
        {
            if (numberStyle != null)
            {
                return;
            }

            numberStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow
            };
            shadowStyle = new GUIStyle(numberStyle);
        }
    }
}
