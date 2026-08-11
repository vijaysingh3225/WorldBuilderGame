using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Presentation
{
    /// <summary>
    /// Shared bow-only aiming reticle. It intentionally owns no combat state so
    /// Combat Lab and raid scenes can use the same presentation independently.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BowAimCrosshairPresenter : MonoBehaviour
    {
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField] private WeaponGridSandboxToolkit gridToolkit;

        private Texture2D crosshairTexture;
        private Texture2D centerDotTexture;

        public BowWeapon BowWeapon => bowWeapon;

        public void Configure(BowWeapon weapon)
        {
            bowWeapon = weapon;
        }

        private void OnGUI()
        {
            bowWeapon ??= Object.FindFirstObjectByType<BowWeapon>();
            if (bowWeapon == null || !bowWeapon.IsDrawing)
            {
                return;
            }

            gridToolkit ??=
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>();
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                return;
            }

            EnsureTextures();
            DrawCrosshair();
        }

        private void OnDestroy()
        {
            DestroyTexture(crosshairTexture);
            DestroyTexture(centerDotTexture);
        }

        private void DrawCrosshair()
        {
            const float size = 23f;
            const float shadowSize = 25f;
            const float centerRingSize = 5.5f;
            const float centerDotSize = 2.5f;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            Color previous = GUI.color;
            GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.28f);
            GUI.DrawTexture(
                CenteredRect(centerX, centerY, shadowSize),
                crosshairTexture);
            GUI.color = bowWeapon.CanFire
                ? new Color(1f, 0.92f, 0.70f, 0.66f)
                : new Color(0.84f, 0.86f, 0.88f, 0.52f);
            GUI.DrawTexture(
                CenteredRect(centerX, centerY, size),
                crosshairTexture);
            GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.68f);
            GUI.DrawTexture(
                CenteredRect(centerX, centerY, centerRingSize),
                centerDotTexture);
            GUI.color = bowWeapon.CanFire
                ? new Color(0.90f, 0.64f, 0.20f, 0.84f)
                : new Color(0.72f, 0.74f, 0.76f, 0.68f);
            GUI.DrawTexture(
                CenteredRect(centerX, centerY, centerDotSize),
                centerDotTexture);
            GUI.color = previous;
        }

        private void EnsureTextures()
        {
            crosshairTexture ??= CreateCrosshairTexture(128);
            centerDotTexture ??= CreateCircleTexture(24);
        }

        private static Rect CenteredRect(
            float centerX,
            float centerY,
            float size)
        {
            return new Rect(
                centerX - size * 0.5f,
                centerY - size * 0.5f,
                size,
                size);
        }

        private static Texture2D CreateCrosshairTexture(int size)
        {
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "Bow Crosshair",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float halfSize = size * 0.5f;
            float antialias = 1.5f / halfSize;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedX =
                        (x - center) / halfSize;
                    float normalizedY =
                        (y - center) / halfSize;
                    float absoluteX = Mathf.Abs(normalizedX);
                    float absoluteY = Mathf.Abs(normalizedY);
                    float verticalProgress =
                        absoluteY / 0.88f;
                    float horizontalProgress =
                        absoluteX / 0.88f;
                    float verticalWidth =
                        CrosshairArmWidth(verticalProgress);
                    float horizontalWidth =
                        CrosshairArmWidth(horizontalProgress);
                    float verticalAlpha =
                        absoluteY <= 0.88f
                            ? 1f - Smooth01(
                                Mathf.InverseLerp(
                                    verticalWidth,
                                    verticalWidth + antialias,
                                    absoluteX))
                            : 0f;
                    float horizontalAlpha =
                        absoluteX <= 0.88f
                            ? 1f - Smooth01(
                                Mathf.InverseLerp(
                                    horizontalWidth,
                                    horizontalWidth + antialias,
                                    absoluteY))
                            : 0f;
                    float centerDistance = Mathf.Sqrt(
                        normalizedX * normalizedX +
                        normalizedY * normalizedY);
                    float centerCutout = Smooth01(
                        Mathf.InverseLerp(
                            0.10f,
                            0.13f,
                            centerDistance));
                    float alpha =
                        Mathf.Max(
                            verticalAlpha,
                            horizontalAlpha) *
                        centerCutout;
                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateCircleTexture(int size)
        {
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "Bow Crosshair Center Dot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;
            float antialias = size * 0.08f;
            Vector2 centerPoint = new Vector2(center, center);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        centerPoint);
                    float alpha = 1f - Smooth01(
                        Mathf.InverseLerp(
                            radius - antialias,
                            radius,
                            distance));
                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float CrosshairArmWidth(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0.42f)
            {
                return Mathf.Lerp(
                    0.10f,
                    0.072f,
                    progress / 0.42f);
            }

            return 0.072f *
                (1f - Mathf.InverseLerp(
                    0.42f,
                    1f,
                    progress));
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
