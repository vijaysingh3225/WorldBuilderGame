using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class CombatLabHud : MonoBehaviour
    {
        private sealed class DamagePopup
        {
            public float Amount;
            public Vector3 WorldPosition;
            public float CreatedAt;
            public float HorizontalOffset;
        }

        private const float DamagePopupDuration = 1.05f;

        [SerializeField] private Health playerHealth;
        [SerializeField] private Health enemyHealth;
        [SerializeField] private TwoSlotWeaponPresenter weaponSlots;

        private readonly List<DamagePopup> damagePopups = new List<DamagePopup>();
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle centeredStyle;
        private GUIStyle damageStyle;
        private GUIStyle damageShadowStyle;
        private Texture2D whiteTexture;
        private Health subscribedEnemy;
        private int damagePopupSequence;

        public void Configure(Health player, Health enemy)
        {
            playerHealth = player;
            enemyHealth = enemy;
            SubscribeToEnemyDamage();
        }

        private void OnEnable()
        {
            SubscribeToEnemyDamage();
        }

        private void OnDisable()
        {
            if (subscribedEnemy != null)
            {
                subscribedEnemy.Damaged -= HandleEnemyDamaged;
                subscribedEnemy = null;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            weaponSlots ??=
                Object.FindFirstObjectByType<TwoSlotWeaponPresenter>();
            GUI.Label(new Rect(24f, 20f, 580f, 30f), "MOVEMENT LAB  /  GAIT TUNING CHECKPOINT", titleStyle);
            GUI.Label(new Rect(24f, 54f, 1100f, 24f), "WASD move   Shift sprint   Space jump   Ctrl/C crouch   Mouse look   LMB attack   Hold RMB block   1/2 or wheel switch   R restart", textStyle);

            DrawHealthBar(new Rect(24f, 88f, 260f, 18f), playerHealth, new Color(0.25f, 0.68f, 0.45f), "PLAYER");
            string slotLabel = weaponSlots == null ||
                weaponSlots.ActiveSlot == TwoSlotWeaponPresenter.PrimarySlot
                    ? "1  SHORT SWORD"
                    : "2  BOW";
            GUI.Label(new Rect(24f, 116f, 260f, 22f), slotLabel, titleStyle);
            DrawHealthBar(new Rect(Screen.width - 284f, 24f, 260f, 18f), enemyHealth, new Color(0.76f, 0.25f, 0.12f), "TARGET DUMMY");
            DrawDamagePopups();

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                DrawCenterMessage("YOU DIED  /  PRESS R TO RESET");
            }
            else if (enemyHealth != null && !enemyHealth.IsAlive)
            {
                DrawCenterMessage("DUMMY BROKEN  /  PRESS R TO RESET");
            }
        }

        private void SubscribeToEnemyDamage()
        {
            if (ReferenceEquals(subscribedEnemy, enemyHealth))
            {
                return;
            }

            if (subscribedEnemy != null)
            {
                subscribedEnemy.Damaged -= HandleEnemyDamaged;
            }

            subscribedEnemy = enemyHealth;
            if (subscribedEnemy != null)
            {
                subscribedEnemy.Damaged += HandleEnemyDamaged;
            }
        }

        private void HandleEnemyDamaged(DamageRequest request)
        {
            Vector3 position = request.HitPoint;
            if (position == Vector3.zero && enemyHealth != null)
            {
                position = enemyHealth.transform.position + Vector3.up;
            }

            float horizontalOffset = (damagePopupSequence % 3 - 1) * 18f;
            damagePopupSequence++;
            damagePopups.Add(new DamagePopup
            {
                Amount = request.Amount,
                WorldPosition = position + Vector3.up * 0.35f,
                CreatedAt = Time.time,
                HorizontalOffset = horizontalOffset
            });
        }

        private void DrawDamagePopups()
        {
            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            for (int index = damagePopups.Count - 1; index >= 0; index--)
            {
                DamagePopup popup = damagePopups[index];
                float age = Time.time - popup.CreatedAt;
                if (age >= DamagePopupDuration)
                {
                    damagePopups.RemoveAt(index);
                    continue;
                }

                float progress = age / DamagePopupDuration;
                Vector3 worldPosition = popup.WorldPosition + Vector3.up * (0.35f + progress * 0.9f);
                Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
                if (screenPosition.z <= 0f)
                {
                    continue;
                }

                float alpha = 1f - Mathf.SmoothStep(0.6f, 1f, progress);
                float x = screenPosition.x - 55f + popup.HorizontalOffset;
                float y = Screen.height - screenPosition.y - 20f;
                Rect shadowRect = new Rect(x + 2f, y + 2f, 110f, 40f);
                Rect textRect = new Rect(x, y, 110f, 40f);
                string amount = Mathf.CeilToInt(popup.Amount).ToString();

                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
                GUI.Label(shadowRect, amount, damageShadowStyle);
                GUI.color = new Color(1f, 0.77f, 0.28f, alpha);
                GUI.Label(textRect, amount, damageStyle);
                GUI.color = previous;
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
            GUI.Label(new Rect(rect.x, rect.y - 20f, rect.width, 20f), label, textStyle);
        }

        private void DrawCenterMessage(string message)
        {
            GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 44f), message, centeredStyle);
        }

        private void EnsureStyles()
        {
            if (whiteTexture != null)
            {
                return;
            }

            whiteTexture = Texture2D.whiteTexture;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.86f, 0.72f) }
            };
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.82f, 0.84f) }
            };
            centeredStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24
            };
            damageStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            damageShadowStyle = new GUIStyle(damageStyle)
            {
                normal = { textColor = Color.white }
            };
        }
    }
}
