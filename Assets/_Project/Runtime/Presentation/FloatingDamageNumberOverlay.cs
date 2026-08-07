using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Presentation
{
    /// <summary>
    /// Supplies one clean, dynamically rasterized typeface to player-facing IMGUI.
    /// The ordered fallbacks keep the same classical serif character across desktop
    /// platforms without shipping a platform font inside the game.
    /// </summary>
    public static class GameTypography
    {
        private const int RasterizationSize = 24;

        private static readonly string[] PreferredFontNames =
        {
            "Georgia",
            "Palatino Linotype",
            "Book Antiqua",
            "Times New Roman",
            "Noto Serif",
            "Liberation Serif"
        };

        private static Font uiFont;
        private static Texture2D cellTexture;
        private static Texture2D panelTexture;
        private static Texture2D borderedCellTexture;
        private static Texture2D borderedPanelTexture;
        private static Texture2D weaponGridCellTexture;
        private static Texture2D sectionTexture;
        private static Texture2D scrollTrackTexture;
        private static Texture2D scrollThumbTexture;
        private static Texture2D scrollThumbHoverTexture;
        private static Texture2D clearTexture;

        public const float MinimalVerticalScrollbarWidth = 6f;

        public static Color CellColor => new Color32(0x27, 0x29, 0x28, 0xff);
        public static Color BorderColor => new Color32(0x82, 0x7b, 0x6c, 0xff);
        public static Color InventoryBackgroundColor =>
            new Color32(0x14, 0x19, 0x1b, 0xff);

        public static Texture2D CellTexture
        {
            get
            {
                EnsurePaletteTextures();
                return cellTexture;
            }
        }

        public static Texture2D PanelTexture
        {
            get
            {
                EnsurePaletteTextures();
                return panelTexture;
            }
        }

        public static Texture2D BorderedCellTexture
        {
            get
            {
                EnsurePaletteTextures();
                return borderedCellTexture;
            }
        }

        public static Texture2D BorderedPanelTexture
        {
            get
            {
                EnsurePaletteTextures();
                return borderedPanelTexture;
            }
        }

        public static Texture2D WeaponGridCellTexture
        {
            get
            {
                EnsurePaletteTextures();
                return weaponGridCellTexture;
            }
        }

        public static Texture2D SectionTexture
        {
            get
            {
                EnsurePaletteTextures();
                return sectionTexture;
            }
        }

        public static Font UiFont
        {
            get
            {
                if (uiFont == null)
                {
                    uiFont = CreateUiFont();
                }

                return uiFont;
            }
        }

        public static void ApplyToCurrentSkin()
        {
            if (GUI.skin != null)
            {
                GUI.skin.font = UiFont;
                EnsurePaletteTextures();
                ApplyPaletteToSkin(GUI.skin);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCachedFont()
        {
            Font.textureRebuilt -= HandleFontTextureRebuilt;
            uiFont = null;
            cellTexture = null;
            panelTexture = null;
            borderedCellTexture = null;
            borderedPanelTexture = null;
            weaponGridCellTexture = null;
            sectionTexture = null;
            scrollTrackTexture = null;
            scrollThumbTexture = null;
            scrollThumbHoverTexture = null;
            clearTexture = null;
        }

        private static void ApplyPaletteToSkin(GUISkin skin)
        {
            skin.box.normal.background = cellTexture;
            skin.window.normal.background = panelTexture;
            skin.button.normal.background = borderedCellTexture;
            skin.button.hover.background = borderedPanelTexture;
            skin.button.active.background = borderedPanelTexture;
            skin.button.focused.background = borderedCellTexture;
            skin.button.border = new RectOffset(2, 2, 2, 2);
            skin.textField.normal.background = borderedPanelTexture;
            skin.textField.hover.background = borderedPanelTexture;
            skin.textField.focused.background = borderedPanelTexture;
            skin.textField.border = new RectOffset(2, 2, 2, 2);
            skin.scrollView.normal.background = panelTexture;
            ApplyScrollbarState(
                skin.verticalScrollbar,
                scrollTrackTexture);
            skin.verticalScrollbar.fixedWidth =
                MinimalVerticalScrollbarWidth;
            skin.verticalScrollbar.border =
                new RectOffset(0, 0, 0, 0);
            ApplyScrollbarState(
                skin.verticalScrollbarThumb,
                scrollThumbTexture,
                scrollThumbHoverTexture);
            skin.verticalScrollbarThumb.fixedWidth = 4f;
            skin.verticalScrollbarThumb.margin =
                new RectOffset(1, 1, 2, 2);
            HideScrollbarButton(skin.verticalScrollbarUpButton);
            HideScrollbarButton(skin.verticalScrollbarDownButton);
            ApplyScrollbarState(
                skin.horizontalScrollbar,
                clearTexture);
            ApplyScrollbarState(
                skin.horizontalScrollbarThumb,
                clearTexture);
            HideScrollbarButton(skin.horizontalScrollbarLeftButton);
            HideScrollbarButton(skin.horizontalScrollbarRightButton);
        }

        private static void ApplyScrollbarState(
            GUIStyle style,
            Texture2D normal,
            Texture2D hover = null)
        {
            Texture2D emphasized = hover != null ? hover : normal;
            style.normal.background = normal;
            style.hover.background = emphasized;
            style.active.background = emphasized;
            style.focused.background = normal;
        }

        private static void HideScrollbarButton(GUIStyle style)
        {
            ApplyScrollbarState(style, clearTexture);
            style.fixedWidth = 0f;
            style.fixedHeight = 0f;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
        }

        private static void EnsurePaletteTextures()
        {
            if (cellTexture != null)
            {
                return;
            }

            cellTexture = CreateSolidTexture("UI Cell 272928", CellColor);
            panelTexture = CreateSolidTexture(
                "UI Inventory Background 14191B",
                InventoryBackgroundColor);
            borderedCellTexture = CreateBorderedTexture(
                "UI Button 272928 827B6C",
                CellColor);
            borderedPanelTexture = CreateBorderedTexture(
                "UI Field 14191B 827B6C",
                InventoryBackgroundColor);
            weaponGridCellTexture = CreateChamferedTexture(
                "UI Weapon Cell Chamfer",
                CellColor);
            sectionTexture = CreateChamferedTexture(
                "UI Section Chamfer",
                InventoryBackgroundColor);
            scrollTrackTexture = CreateSolidTexture(
                "UI Minimal Scroll Track",
                new Color(
                    BorderColor.r,
                    BorderColor.g,
                    BorderColor.b,
                    0.10f));
            scrollThumbTexture = CreateSolidTexture(
                "UI Minimal Scroll Thumb",
                new Color(
                    BorderColor.r,
                    BorderColor.g,
                    BorderColor.b,
                    0.46f));
            scrollThumbHoverTexture = CreateSolidTexture(
                "UI Minimal Scroll Thumb Hover",
                new Color(
                    BorderColor.r,
                    BorderColor.g,
                    BorderColor.b,
                    0.68f));
            clearTexture = CreateSolidTexture(
                "UI Clear Scroll Surface",
                new Color(0f, 0f, 0f, 0f));
        }

        private static Texture2D CreateSolidTexture(
            string name,
            Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateBorderedTexture(
            string name,
            Color fill)
        {
            const int size = 5;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 ||
                        x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, border ? BorderColor : fill);
                }
            }
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateChamferedTexture(
            string name,
            Color fill)
        {
            const int size = 13;
            const int cut = 4;
            const int border = 2;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool insideOuter = IsInsideChamfer(
                        x,
                        y,
                        size,
                        cut);
                    bool insideInner = IsInsideChamfer(
                        x - border,
                        y - border,
                        size - border * 2,
                        cut - border);
                    texture.SetPixel(
                        x,
                        y,
                        !insideOuter
                            ? clear
                            : insideInner
                                ? fill
                                : BorderColor);
                }
            }
            texture.Apply(false, false);
            return texture;
        }

        private static bool IsInsideChamfer(
            int x,
            int y,
            int size,
            int cut)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
            {
                return false;
            }

            int farX = size - 1 - x;
            int farY = size - 1 - y;
            return x + y >= cut &&
                farX + y >= cut &&
                x + farY >= cut &&
                farX + farY >= cut;
        }

        private static Font CreateUiFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                PreferredFontNames,
                RasterizationSize);
            if (font != null)
            {
                font.hideFlags = HideFlags.HideAndDontSave;
                ConfigureFontTexture(font);
                Font.textureRebuilt -= HandleFontTextureRebuilt;
                Font.textureRebuilt += HandleFontTextureRebuilt;
                return font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void HandleFontTextureRebuilt(Font rebuiltFont)
        {
            if (rebuiltFont == uiFont)
            {
                ConfigureFontTexture(rebuiltFont);
            }
        }

        private static void ConfigureFontTexture(Font font)
        {
            Texture texture = font != null && font.material != null
                ? font.material.mainTexture
                : null;
            if (texture == null)
            {
                return;
            }

            texture.filterMode = FilterMode.Point;
            texture.anisoLevel = 0;
        }
    }

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
            public bool receivedByPlayer;
            public float horizontalDirection;
        }

        private readonly List<Entry> entries = new List<Entry>(8);
        private GUIStyle numberStyle;
        private GUIStyle shadowStyle;
        private int placementSequence;

        public int ActiveNumberCount => entries.Count;
        public int PlayerNumberCount => entries.FindAll(
            entry => entry.receivedByPlayer).Count;

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

        public void Show(
            float amount,
            Vector3 worldPoint,
            bool critical,
            bool receivedByPlayer = false)
        {
            entries.Add(new Entry
            {
                amount = amount,
                worldPoint = worldPoint,
                createdAt = Time.time,
                critical = critical,
                receivedByPlayer = receivedByPlayer,
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
                numberStyle.normal.textColor = entry.receivedByPlayer
                    ? new Color(1f, 0.30f, 0.22f, alpha)
                    : entry.critical
                        ? new Color(1f, 0.38f, 0.20f, alpha)
                        : new Color(1f, 0.88f, 0.54f, alpha);
                shadowStyle.normal.textColor =
                    new Color(0f, 0f, 0f, alpha * 0.72f);

                float horizontalOffset = entry.receivedByPlayer
                    ? 64f
                    : HorizontalOffset;
                float x = screen.x +
                    horizontalOffset * entry.horizontalDirection;
                if (entry.receivedByPlayer)
                {
                    x = Mathf.Clamp(x, 42f, Screen.width - 42f);
                    screen.y = Mathf.Clamp(
                        screen.y,
                        42f,
                        Screen.height - 42f);
                }
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
            GameTypography.ApplyToCurrentSkin();
            if (numberStyle != null)
            {
                return;
            }

            numberStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                clipping = TextClipping.Overflow
            };
            shadowStyle = new GUIStyle(numberStyle);
        }
    }
}
