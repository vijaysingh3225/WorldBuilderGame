using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public static class GameplaySceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string HomeBase = "HomeBase";
        public const string RaidPrototype = "RaidPrototype";
        public const string CombatLab = "CombatLab";
    }

    internal static class GameplaySceneRuntime
    {
        public static GameplayLoopBootstrap ResolveBootstrap()
        {
            GameplayLoopBootstrap bootstrap =
                GameplayLoopBootstrap.Current ??
                Object.FindFirstObjectByType<GameplayLoopBootstrap>();
            if (bootstrap != null)
            {
                return bootstrap;
            }

            GameObject bootstrapObject =
                new GameObject("[Gameplay Loop]");
            return bootstrapObject.AddComponent<GameplayLoopBootstrap>();
        }

        public static bool TryLoadScene(
            string sceneName,
            out string error)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                error =
                    $"Scene '{sceneName}' is not registered in Build Settings.";
                return false;
            }

            error = string.Empty;
            SceneManager.LoadScene(sceneName);
            return true;
        }

        public static void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            Transform candidate = other.transform;
            while (candidate != null)
            {
                if (candidate.CompareTag("Player"))
                {
                    return true;
                }

                candidate = candidate.parent;
            }

            return false;
        }

        public static string FriendlyId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }

            string[] words = value.Replace('_', '-').Split('-');
            for (int index = 0; index < words.Length; index++)
            {
                if (words[index].Length == 0)
                {
                    continue;
                }

                words[index] =
                    char.ToUpperInvariant(words[index][0]) +
                    words[index].Substring(1);
            }

            return string.Join(" ", words);
        }
    }

    internal static class LoopSceneGui
    {
        private static GUIStyle title;
        private static GUIStyle heading;
        private static GUIStyle body;
        private static GUIStyle muted;
        private static GUIStyle button;
        private static GUIStyle centered;

        public static GUIStyle Title
        {
            get
            {
                EnsureStyles();
                return title;
            }
        }

        public static GUIStyle Heading
        {
            get
            {
                EnsureStyles();
                return heading;
            }
        }

        public static GUIStyle Body
        {
            get
            {
                EnsureStyles();
                return body;
            }
        }

        public static GUIStyle Muted
        {
            get
            {
                EnsureStyles();
                return muted;
            }
        }

        public static GUIStyle Button
        {
            get
            {
                EnsureStyles();
                return button;
            }
        }

        public static GUIStyle Centered
        {
            get
            {
                EnsureStyles();
                return centered;
            }
        }

        public static void DrawPanel(Rect rect, Color accent)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.035f, 0.042f, 0.048f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(
                accent.r,
                accent.g,
                accent.b,
                0.95f);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, 4f, rect.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void DrawDimmer(float alpha = 0.58f)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.01f, 0.014f, 0.018f, alpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void EnsureStyles()
        {
            if (title != null)
            {
                return;
            }

            title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal =
                {
                    textColor =
                        new Color(0.94f, 0.88f, 0.72f)
                }
            };
            heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor =
                        new Color(0.86f, 0.88f, 0.90f)
                }
            };
            body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal =
                {
                    textColor =
                        new Color(0.82f, 0.84f, 0.85f)
                }
            };
            muted = new GUIStyle(body)
            {
                fontSize = 12,
                normal =
                {
                    textColor =
                        new Color(0.58f, 0.62f, 0.64f)
                }
            };
            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(18, 14, 5, 5),
                normal =
                {
                    textColor =
                        new Color(0.90f, 0.91f, 0.91f)
                },
                hover =
                {
                    textColor = Color.white
                }
            };
            centered = new GUIStyle(body)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
