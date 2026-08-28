using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class BrowserRaidDemoController : MonoBehaviour
    {
        public const int FixedRaidSeed = 30817;
        public const string EditorPreviewPreference =
            "WorldBuilder.BrowserRaidDemoPreview";

        private enum DemoState
        {
            MainMenu,
            LoadingScene,
            Generating,
            Ready,
            Playing,
            RaidEnded,
            Error
        }

        private static BrowserRaidDemoController current;
        private static bool editorPreviewEnabled;

        private DemoState state = DemoState.MainMenu;
        private GameplayLoopBootstrap bootstrap;
        private PlayerInputSource playerInput;
        private AsyncOperation sceneLoad;
        private float progress;
        private string phase = "Preparing the expedition.";
        private string error = string.Empty;
        private string raidEndTitle = string.Empty;
        private string raidEndDetail = string.Empty;

        public static BrowserRaidDemoController Current => current;
        public static bool IsEnabled
        {
            get
            {
#if WORLD_BUILDER_RAID_DEMO
                return true;
#else
                return editorPreviewEnabled ||
                    Application.platform == RuntimePlatform.WebGLPlayer;
#endif
            }
        }

        public bool IsPlaying => state == DemoState.Playing;
        public bool IsLoading =>
            state == DemoState.LoadingScene ||
            state == DemoState.Generating ||
            state == DemoState.Ready;
        public float Progress => progress;
        public string Phase => phase;

        private void Awake()
        {
            if (!IsEnabled)
            {
                enabled = false;
                return;
            }

            if (current != null && current != this)
            {
                Destroy(gameObject);
                return;
            }

            current = this;
            DontDestroyOnLoad(gameObject);
            BootstrapMenuController standardMenu =
                GetComponent<BootstrapMenuController>();
            if (standardMenu != null)
            {
                standardMenu.enabled = false;
            }
            GameplaySceneRuntime.ShowCursor();
        }

        private void Start()
        {
            if (!IsEnabled)
            {
                return;
            }

            bootstrap = GameplaySceneRuntime.ResolveBootstrap();
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (state == DemoState.LoadingScene && sceneLoad != null)
            {
                progress = Mathf.Clamp01(sceneLoad.progress / 0.9f) * 0.18f;
                phase = "Loading the raid scene.";
            }
        }

        public static bool TryBeginStagedGeneration(
            ProceduralRaidGenerator generator)
        {
            if (!IsEnabled || current == null || generator == null)
            {
                return false;
            }

            current.BeginStagedGeneration(generator);
            return true;
        }

        public static void NotifyRaidCompleted(
            RaidCompletionReason reason,
            int enemiesDefeated,
            int lootCollected)
        {
            if (!IsEnabled || current == null)
            {
                return;
            }

            current.CapturePlayerInput();
            Time.timeScale = 0f;
            GameplaySceneRuntime.ShowCursor();
            current.state = DemoState.RaidEnded;
            current.raidEndTitle = reason == RaidCompletionReason.PlayerDied
                ? "THE RAID IS OVER"
                : "RAID COMPLETE";
            current.raidEndDetail =
                $"Enemies defeated: {enemiesDefeated}   /   " +
                $"Loot found: {lootCollected}";
        }

        public void LaunchRaid()
        {
            if (state == DemoState.LoadingScene ||
                state == DemoState.Generating)
            {
                return;
            }

            bootstrap ??= GameplaySceneRuntime.ResolveBootstrap();
            if (!bootstrap.StartRaidSandbox(
                    "browser-demo",
                    FixedRaidSeed))
            {
                ShowError(bootstrap.LastInitializationError);
                return;
            }

            Time.timeScale = 1f;
            GameplaySceneRuntime.ShowCursor();
            playerInput = null;
            progress = 0f;
            phase = "Loading the raid scene.";
            state = DemoState.LoadingScene;
            StartCoroutine(LoadRaidScene());
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            GameplaySceneRuntime.ShowCursor();
            playerInput = null;
            state = DemoState.MainMenu;
            progress = 0f;
            phase = "Preparing the expedition.";
            if (SceneManager.GetActiveScene().name !=
                GameplaySceneNames.Bootstrap)
            {
                SceneManager.LoadScene(GameplaySceneNames.Bootstrap);
            }
        }

        private IEnumerator LoadRaidScene()
        {
            sceneLoad = SceneManager.LoadSceneAsync(
                GameplaySceneNames.RaidPrototype,
                LoadSceneMode.Single);
            if (sceneLoad == null)
            {
                ShowError("The Raid Prototype scene could not be loaded.");
                yield break;
            }

            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            sceneLoad = null;
        }

        private void BeginStagedGeneration(
            ProceduralRaidGenerator generator)
        {
            if (state == DemoState.Generating)
            {
                return;
            }

            state = DemoState.Generating;
            progress = 0.18f;
            phase = "Preparing world generation.";
            Time.timeScale = 0f;
            CapturePlayerInput();
            generator.SetGenerationQuality(
                ProceduralRaidGenerator.GenerationQuality.BrowserDemo);
            StartCoroutine(GenerateRaid(generator));
        }

        private IEnumerator GenerateRaid(
            ProceduralRaidGenerator generator)
        {
            yield return generator.GenerateStaged(
                HandleGenerationProgress);

            CapturePlayerInput();
            ExtractionZone extraction =
                FindFirstObjectByType<ExtractionZone>();
            if (extraction != null)
            {
                extraction.gameObject.SetActive(false);
            }

            progress = 1f;
            phase = "The raid is ready.";
            state = DemoState.Ready;
            GameplaySceneRuntime.ShowCursor();
        }

        private void HandleGenerationProgress(
            string generationPhase,
            float generationProgress)
        {
            phase = generationPhase;
            progress = Mathf.Lerp(
                0.18f,
                0.98f,
                Mathf.Clamp01(generationProgress));
            CapturePlayerInput();
        }

        private void EnterRaid()
        {
            CapturePlayerInput();
            if (playerInput == null)
            {
                ShowError("The generated raid has no player input source.");
                return;
            }

            state = DemoState.Playing;
            Time.timeScale = 1f;
            playerInput.SetUserInterfaceCapture(false);
            playerInput.RequestGameplayCursorCapture();
        }

        private void CapturePlayerInput()
        {
            if (playerInput == null)
            {
                GameObject player =
                    GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerInput =
                        player.GetComponent<PlayerInputSource>() ??
                        player.GetComponentInChildren<PlayerInputSource>(true);
                }
            }

            playerInput?.SetUserInterfaceCapture(true);
        }

        private void ShowMainMenu()
        {
            Time.timeScale = 1f;
            state = DemoState.MainMenu;
            progress = 0f;
            error = string.Empty;
            GameplaySceneRuntime.ShowCursor();
        }

        private void ShowError(string message)
        {
            Time.timeScale = 0f;
            GameplaySceneRuntime.ShowCursor();
            error = string.IsNullOrWhiteSpace(message)
                ? "The browser demo could not continue."
                : message;
            state = DemoState.Error;
        }

        private void OnGUI()
        {
            if (!IsEnabled || state == DemoState.Playing)
            {
                return;
            }

            LoopSceneGui.DrawDimmer(0.82f);
            float width = Mathf.Min(680f, Screen.width - 32f);
            float height = Mathf.Min(520f, Screen.height - 32f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            LoopSceneGui.DrawPanel(
                panel,
                new Color(0.67f, 0.52f, 0.27f));

            float x = panel.x + 42f;
            float y = panel.y + 38f;
            float contentWidth = panel.width - 84f;
            GUI.Label(
                new Rect(x, y, contentWidth, 42f),
                TitleForState(),
                LoopSceneGui.Title);
            y += 62f;

            switch (state)
            {
                case DemoState.MainMenu:
                    DrawMainMenu(x, y, contentWidth);
                    break;
                case DemoState.LoadingScene:
                case DemoState.Generating:
                    DrawLoading(x, y, contentWidth);
                    break;
                case DemoState.Ready:
                    DrawReady(x, y, contentWidth);
                    break;
                case DemoState.RaidEnded:
                    DrawRaidEnded(x, y, contentWidth);
                    break;
                case DemoState.Error:
                    DrawError(x, y, contentWidth);
                    break;
            }
        }

        private void DrawMainMenu(float x, float y, float width)
        {
            GUI.Label(
                new Rect(x, y, width, 72f),
                "A standalone prototype of one procedurally generated " +
                "forest raid. Nothing is saved and extraction is disabled.",
                LoopSceneGui.Body);
            y += 96f;
            if (GUI.Button(
                    new Rect(x, y, width, 58f),
                    "SPAWN INTO WORLD",
                    LoopSceneGui.Button))
            {
                LaunchRaid();
            }
            y += 82f;
            GUI.Label(
                new Rect(x, y, width, 90f),
                "WASD move   /   Mouse look   /   Left click attack\n" +
                "Right click block   /   Space jump   /   Escape menu",
                LoopSceneGui.Muted);
        }

        private void DrawLoading(float x, float y, float width)
        {
            GUI.Label(
                new Rect(x, y, width, 34f),
                phase,
                LoopSceneGui.Heading);
            y += 54f;
            Rect track = new Rect(x, y, width, 20f);
            GUI.color = new Color(0.08f, 0.09f, 0.09f, 0.96f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = new Color(0.67f, 0.52f, 0.27f, 1f);
            GUI.DrawTexture(
                new Rect(track.x, track.y, track.width * progress, track.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 44f;
            GUI.Label(
                new Rect(x, y, width, 40f),
                $"{Mathf.RoundToInt(progress * 100f)}%",
                LoopSceneGui.Muted);
        }

        private void DrawReady(float x, float y, float width)
        {
            GUI.Label(
                new Rect(x, y, width, 80f),
                "Generation complete. Click below to focus the game and " +
                "capture the mouse cursor.",
                LoopSceneGui.Body);
            y += 102f;
            if (GUI.Button(
                    new Rect(x, y, width, 58f),
                    "CLICK TO ENTER RAID",
                    LoopSceneGui.Button))
            {
                EnterRaid();
            }
        }

        private void DrawRaidEnded(float x, float y, float width)
        {
            GUI.Label(
                new Rect(x, y, width, 60f),
                raidEndDetail,
                LoopSceneGui.Body);
            y += 84f;
            if (GUI.Button(
                    new Rect(x, y, width, 52f),
                    "PLAY AGAIN",
                    LoopSceneGui.Button))
            {
                LaunchRaid();
                return;
            }
            y += 66f;
            if (GUI.Button(
                    new Rect(x, y, width, 52f),
                    "MAIN MENU",
                    LoopSceneGui.Button))
            {
                ReturnToMenu();
            }
        }

        private void DrawError(float x, float y, float width)
        {
            GUI.Label(
                new Rect(x, y, width, 110f),
                error,
                LoopSceneGui.Body);
            y += 132f;
            if (GUI.Button(
                    new Rect(x, y, width, 52f),
                    "RETURN TO MENU",
                    LoopSceneGui.Button))
            {
                ReturnToMenu();
            }
        }

        private string TitleForState()
        {
            switch (state)
            {
                case DemoState.MainMenu:
                    return "WORLD BUILDER  /  RAID PROTOTYPE";
                case DemoState.LoadingScene:
                case DemoState.Generating:
                    return "GENERATING RAID";
                case DemoState.Ready:
                    return "EXPEDITION READY";
                case DemoState.RaidEnded:
                    return raidEndTitle;
                case DemoState.Error:
                    return "DEMO ERROR";
                default:
                    return string.Empty;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            editorPreviewEnabled =
                PlayerPrefs.GetInt(EditorPreviewPreference, 0) == 1;
            if (editorPreviewEnabled)
            {
                PlayerPrefs.DeleteKey(EditorPreviewPreference);
            }
        }
    }
}
