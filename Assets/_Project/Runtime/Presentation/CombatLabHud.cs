using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class CombatLabHud : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField] private Health enemyHealth;
        [SerializeField] private TwoSlotWeaponPresenter weaponSlots;
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField] private ShortSwordAttackPresenter shortSwordAttack;
        [SerializeField] private EnemyBrain enemyBrain;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle centeredStyle;
        private GUIStyle compactBarStyle;
        private Texture2D whiteTexture;
        private Texture2D bowCrosshairTexture;
        private Texture2D bowCrosshairDotTexture;

        public void Configure(Health player, Health enemy)
        {
            playerHealth = player;
            if (player != null)
            {
                playerStamina = player.GetComponent<PlayerStamina>();
                if (playerStamina == null)
                {
                    playerStamina = player.gameObject
                        .AddComponent<PlayerStamina>();
                }
            }
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
            shortSwordAttack =
                player != null
                    ? player.GetComponentInChildren<
                        ShortSwordAttackPresenter>(true)
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
            if (shortSwordAttack == null && playerHealth != null)
            {
                shortSwordAttack =
                    playerHealth.GetComponentInChildren<
                        ShortSwordAttackPresenter>(true);
            }
            CalculatePlayerBarRects(
                Screen.height,
                300f,
                out Rect healthRect,
                out Rect staminaRect,
                out Rect chargeRect);
            DrawHealthBar(
                healthRect,
                playerHealth,
                new Color(0.25f, 0.68f, 0.45f),
                "HEALTH",
                true);
            DrawResourceBar(
                staminaRect,
                playerStamina != null
                    ? playerStamina.Normalized
                    : 1f,
                new Color(0.12f, 0.10f, 0.045f, 0.95f),
                new Color(0.78f, 0.70f, 0.30f, 0.96f),
                "STAMINA");
            if (weaponSlots != null &&
                weaponSlots.ActiveSlot ==
                    TwoSlotWeaponPresenter.SecondarySlot &&
                bowWeapon != null &&
                (bowWeapon.IsDrawing || bowWeapon.DrawNormalized > 0f))
            {
                DrawBowCharge(chargeRect);
                if (bowWeapon.IsDrawing)
                {
                    DrawBowCrosshair();
                }
            }
            else if (shortSwordAttack != null &&
                     shortSwordAttack.IsHeavyCharging)
            {
                DrawHeavyCharge(chargeRect);
            }
            enemyBrain ??=
                enemyHealth != null
                    ? enemyHealth.GetComponent<EnemyBrain>()
                    : null;
            string enemyLabel =
                enemyBrain != null && enemyBrain.IsActivated
                    ? "AI COMBATANT"
                    : "TARGET DUMMY";
            DrawHealthBar(new Rect(Screen.width - 344f, 24f, 320f, 18f), enemyHealth, new Color(0.76f, 0.25f, 0.12f), enemyLabel, false);

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                DrawCenterMessage("YOU DIED");
            }
            else if (enemyHealth != null && !enemyHealth.IsAlive)
            {
                DrawCenterMessage("DUMMY DEFEATED");
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedTexture(ref bowCrosshairTexture);
            DestroyGeneratedTexture(ref bowCrosshairDotTexture);
        }

        public static void CalculatePlayerBarRects(
            float screenHeight,
            float width,
            out Rect health,
            out Rect stamina,
            out Rect charge)
        {
            const float LeftMargin = 24f;
            const float BottomMargin = 20f;
            const float HealthHeight = 18f;
            const float CompactHeight = 11f;
            const float Gap = 5f;
            health = new Rect(
                LeftMargin,
                screenHeight - BottomMargin - HealthHeight,
                width,
                HealthHeight);
            stamina = new Rect(
                LeftMargin,
                health.y - Gap - CompactHeight,
                width,
                CompactHeight);
            charge = new Rect(
                LeftMargin,
                stamina.y - Gap - CompactHeight,
                width,
                CompactHeight);
        }

        private void DrawHealthBar(
            Rect rect,
            Health health,
            Color fill,
            string label,
            bool labelInside)
        {
            float normalized = health != null ? health.Normalized : 0f;
            Color previous = GUI.color;
            GUI.color = labelInside
                ? new Color(0.30f, 0.075f, 0.065f, 0.96f)
                : new Color(0.04f, 0.05f, 0.06f, 0.88f);
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * normalized, rect.height - 4f), whiteTexture);
            GUI.color = previous;
            string healthText = health != null
                ? $"{label}  {Mathf.CeilToInt(health.Current)} / " +
                  $"{Mathf.CeilToInt(health.Maximum)}"
                : label;
            GUI.Label(
                labelInside
                    ? new Rect(
                        rect.x + 7f,
                        rect.y - 1f,
                        rect.width - 14f,
                        rect.height + 2f)
                    : new Rect(
                        rect.x,
                        rect.y - 20f,
                        rect.width,
                        20f),
                healthText,
                labelInside ? compactBarStyle : textStyle);
        }

        private void DrawResourceBar(
            Rect rect,
            float normalized,
            Color missing,
            Color fill,
            string label)
        {
            Color previous = GUI.color;
            GUI.color = missing;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(
                new Rect(
                    rect.x + 1f,
                    rect.y + 1f,
                    (rect.width - 2f) * Mathf.Clamp01(normalized),
                    rect.height - 2f),
                whiteTexture);
            GUI.color = previous;
            GUI.Label(
                new Rect(
                    rect.x + 5f,
                    rect.y - 1f,
                    rect.width - 10f,
                    rect.height + 2f),
                label,
                compactBarStyle);
        }

        private void DrawBowCharge(Rect rect)
        {
            DrawResourceBar(
                rect,
                bowWeapon.DrawNormalized,
                new Color(0.075f, 0.065f, 0.045f, 0.96f),
                bowWeapon.CanFire
                    ? new Color(0.90f, 0.64f, 0.20f)
                    : new Color(0.42f, 0.43f, 0.45f),
                bowWeapon.CanFire ? "BOW  READY" : "BOW  DRAW");
        }

        private void DrawHeavyCharge(Rect rect)
        {
            DrawResourceBar(
                rect,
                shortSwordAttack.HeavyChargeNormalized,
                new Color(0.11f, 0.045f, 0.035f, 0.96f),
                new Color(0.76f, 0.30f, 0.18f),
                "HEAVY STRIKE");
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
            compactBarStyle = new GUIStyle(textStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.94f, 0.94f, 0.90f) }
            };
        }

        private static void DestroyGeneratedTexture(
            ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if ((texture.hideFlags & HideFlags.DontSave) != 0)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(texture);
                }
                else
                {
                    Object.DestroyImmediate(texture);
                }
            }

            texture = null;
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
