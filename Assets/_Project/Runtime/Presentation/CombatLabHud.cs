using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class CombatLabHud : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private Health enemyHealth;
        [SerializeField] private TwoSlotWeaponPresenter weaponSlots;
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField] private EnemyBrain enemyBrain;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle centeredStyle;
        private Texture2D whiteTexture;
        private Texture2D bowCrosshairTexture;
        private Texture2D bowCrosshairDotTexture;

        public void Configure(Health player, Health enemy)
        {
            playerHealth = player;
            enemyHealth = enemy;
            weaponSlots =
                player != null
                    ? player.GetComponentInChildren<
                        TwoSlotWeaponPresenter>(true)
                    : null;
            bowWeapon =
                player != null
                    ? player.GetComponentInChildren<BowWeapon>(true)
                    : null;
            enemyBrain =
                enemy != null
                    ? enemy.GetComponent<EnemyBrain>()
                    : null;
        }

        private void OnGUI()
        {
            EnsureStyles();
            weaponSlots ??=
                Object.FindFirstObjectByType<TwoSlotWeaponPresenter>();
            bowWeapon ??=
                Object.FindFirstObjectByType<BowWeapon>();
            DrawHealthBar(
                new Rect(24f, 40f, 260f, 18f),
                playerHealth,
                new Color(0.25f, 0.68f, 0.45f),
                "PLAYER");
            if (weaponSlots != null &&
                weaponSlots.ActiveSlot ==
                    TwoSlotWeaponPresenter.SecondarySlot &&
                bowWeapon != null)
            {
                DrawBowCharge(new Rect(24f, 70f, 260f, 12f));
                if (bowWeapon.IsDrawing)
                {
                    DrawBowCrosshair();
                }
            }
            enemyBrain ??=
                enemyHealth != null
                    ? enemyHealth.GetComponent<EnemyBrain>()
                    : null;
            string enemyLabel =
                enemyBrain != null && enemyBrain.IsActivated
                    ? "AI COMBATANT"
                    : "TARGET DUMMY";
            DrawHealthBar(new Rect(Screen.width - 344f, 24f, 320f, 18f), enemyHealth, new Color(0.76f, 0.25f, 0.12f), enemyLabel);

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                DrawCenterMessage("YOU DIED");
            }
            else if (enemyHealth != null && !enemyHealth.IsAlive)
            {
                DrawCenterMessage("DUMMY DEFEATED");
            }
        }

        private void DrawHealthBar(Rect rect, Health health, Color fill, string label)
        {
            float normalized = health != null ? health.Normalized : 0f;
            Color previous = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.88f);
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * normalized, rect.height - 4f), whiteTexture);
            GUI.color = previous;
            string healthText = health != null
                ? $"{label}  {Mathf.CeilToInt(health.Current)} / " +
                  $"{Mathf.CeilToInt(health.Maximum)}"
                : label;
            GUI.Label(
                new Rect(
                    rect.x,
                    rect.y - 20f,
                    rect.width,
                    20f),
                healthText,
                textStyle);
        }

        private void DrawBowCharge(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.88f);
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = bowWeapon.CanFire
                ? new Color(0.90f, 0.64f, 0.20f)
                : new Color(0.42f, 0.43f, 0.45f);
            GUI.DrawTexture(
                new Rect(
                    rect.x + 2f,
                    rect.y + 2f,
                    (rect.width - 4f) * bowWeapon.DrawNormalized,
                    rect.height - 4f),
                whiteTexture);
            GUI.color = previous;
            GUI.Label(
                new Rect(rect.x, rect.y + 12f, rect.width, 20f),
                bowWeapon.IsDrawing
                    ? (bowWeapon.CanFire ? "RELEASE TO FIRE" : "SETTLING")
                    : "HOLD RMB TO DRAW",
                textStyle);
        }

        private void DrawCenterMessage(string message)
        {
            GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 44f), message, centeredStyle);
        }

        private void DrawBowCrosshair()
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
                new Rect(
                    centerX - shadowSize * 0.5f,
                    centerY - shadowSize * 0.5f,
                    shadowSize,
                    shadowSize),
                bowCrosshairTexture);
            GUI.color = bowWeapon.CanFire
                ? new Color(1f, 0.92f, 0.70f, 0.66f)
                : new Color(0.84f, 0.86f, 0.88f, 0.52f);
            GUI.DrawTexture(
                new Rect(
                    centerX - size * 0.5f,
                    centerY - size * 0.5f,
                    size,
                    size),
                bowCrosshairTexture);
            GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.68f);
            GUI.DrawTexture(
                new Rect(
                    centerX - centerRingSize * 0.5f,
                    centerY - centerRingSize * 0.5f,
                    centerRingSize,
                    centerRingSize),
                bowCrosshairDotTexture);
            GUI.color = bowWeapon.CanFire
                ? new Color(0.90f, 0.64f, 0.20f, 0.84f)
                : new Color(0.72f, 0.74f, 0.76f, 0.68f);
            GUI.DrawTexture(
                new Rect(
                    centerX - centerDotSize * 0.5f,
                    centerY - centerDotSize * 0.5f,
                    centerDotSize,
                    centerDotSize),
                bowCrosshairDotTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            GameTypography.ApplyToCurrentSkin();
            if (whiteTexture != null)
            {
                return;
            }

            whiteTexture = Texture2D.whiteTexture;
            bowCrosshairTexture = CreateCrosshairTexture(128);
            bowCrosshairDotTexture = CreateCircleTexture(24);
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.86f, 0.72f) }
            };
            textStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.82f, 0.84f) }
            };
            centeredStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24
            };
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
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(center, center));
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
    }
}
