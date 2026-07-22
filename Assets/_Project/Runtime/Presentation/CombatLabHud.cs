using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class CombatLabHud : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private Health enemyHealth;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle centeredStyle;
        private Texture2D whiteTexture;

        public void Configure(Health player, Health enemy)
        {
            playerHealth = player;
            enemyHealth = enemy;
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
            GUI.Label(new Rect(24f, 20f, 400f, 30f), "COMBAT LAB  /  FIRST PLAYABLE SLICE", titleStyle);
            GUI.Label(new Rect(24f, 54f, 520f, 24f), "WASD move   Shift sprint   Mouse look   LMB attack   R restart", textStyle);

            DrawHealthBar(new Rect(24f, 88f, 260f, 18f), playerHealth, new Color(0.25f, 0.68f, 0.45f), "PLAYER");
            DrawHealthBar(new Rect(Screen.width - 284f, 24f, 260f, 18f), enemyHealth, new Color(0.76f, 0.25f, 0.12f), "RAIDER");

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                DrawCenterMessage("YOU DIED  /  PRESS R TO RESET");
            }
            else if (enemyHealth != null && !enemyHealth.IsAlive)
            {
                DrawCenterMessage("ENEMY DOWN  /  PRESS R TO RESET");
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
        }
    }
}
