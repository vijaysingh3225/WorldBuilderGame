using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class BootstrapMenuController : MonoBehaviour
    {
        private GameplayLoopBootstrap bootstrap;
        private bool freshOverwriteConfirmationPending;
        private string statusMessage =
            "Choose a persistent game or an isolated development session.";

        private void OnEnable()
        {
            GameplaySceneRuntime.ShowCursor();
        }

        private void Start()
        {
            bootstrap = GameplaySceneRuntime.ResolveBootstrap();
        }

        private void Update()
        {
            GameplaySceneRuntime.ShowCursor();
            bootstrap ??= GameplaySceneRuntime.ResolveBootstrap();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                RequestFreshGame();
            }
            else if (keyboard.cKey.wasPressedThisFrame)
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.Continue);
            }
            else if (keyboard.hKey.wasPressedThisFrame)
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.HomeSandbox);
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.RaidSandbox);
            }
            else if (keyboard.lKey.wasPressedThisFrame)
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.CombatLab);
            }
            else if (keyboard.wKey.wasPressedThisFrame)
            {
                CancelFreshConfirmation();
                LaunchShortSwordGeneratorLab();
            }
        }

        private void OnGUI()
        {
            LoopSceneGui.DrawDimmer(0.72f);

            float width = Mathf.Min(620f, Screen.width - 40f);
            float height = Mathf.Min(660f, Screen.height - 40f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            LoopSceneGui.DrawPanel(
                panel,
                new Color(0.75f, 0.55f, 0.24f));

            float x = panel.x + 34f;
            float contentWidth = panel.width - 68f;
            float y = panel.y + 28f;
            GUI.Label(
                new Rect(x, y, contentWidth, 38f),
                "WORLD BUILDER",
                LoopSceneGui.Title);
            y += 42f;
            GUI.Label(
                new Rect(x, y, contentWidth, 40f),
                "Extraction loop prototype  /  choose how to play",
                LoopSceneGui.Muted);
            y += 52f;

            if (DrawLaunchButton(
                    new Rect(x, y, contentWidth, 48f),
                    freshOverwriteConfirmationPending
                        ? "[F]  CONFIRM REPLACE SAVE"
                        : "[F]  FRESH GAME",
                    freshOverwriteConfirmationPending
                        ? "Second press replaces the existing profile"
                        : "New persistent profile, starting at home"))
            {
                RequestFreshGame();
            }

            y += 58f;
            if (DrawLaunchButton(
                    new Rect(x, y, contentWidth, 48f),
                    "[C]  CONTINUE",
                    "Load the default persistent profile"))
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.Continue);
            }

            y += 72f;
            GUI.Label(
                new Rect(x, y, contentWidth, 24f),
                "DEVELOPMENT SESSIONS",
                LoopSceneGui.Heading);
            y += 30f;
            if (DrawLaunchButton(
                    new Rect(x, y, contentWidth, 48f),
                    "[H]  HOME SANDBOX",
                    "Disposable storage and preparation profile"))
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.HomeSandbox);
            }

            y += 58f;
            if (DrawLaunchButton(
                    new Rect(x, y, contentWidth, 48f),
                    "[R]  RAID SANDBOX",
                    "Disposable raid with a newly generated seed"))
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.RaidSandbox);
            }

            y += 58f;
            if (DrawLaunchButton(
                    new Rect(x, y, contentWidth, 48f),
                    "[L]  COMBAT LAB",
                    "Dedicated combat and Weapon Grid toolkit"))
            {
                CancelFreshConfirmation();
                Launch(GameLaunchMode.CombatLab);
            }

            y += 58f;
            if (DrawLaunchButton(
                    new Rect(x, y, contentWidth, 48f),
                    "[W]  WEAPON GENERATOR",
                    "Generate reusable procedural short swords"))
            {
                CancelFreshConfirmation();
                LaunchShortSwordGeneratorLab();
            }

            y += 62f;
            GUI.Label(
                new Rect(x, y, contentWidth, 42f),
                statusMessage,
                LoopSceneGui.Body);
        }

        private static bool DrawLaunchButton(
            Rect rect,
            string title,
            string description)
        {
            bool clicked = GUI.Button(
                rect,
                title,
                LoopSceneGui.Button);
            GUI.Label(
                new Rect(
                    rect.x + 250f,
                    rect.y + 14f,
                    rect.width - 268f,
                    rect.height - 16f),
                description,
                LoopSceneGui.Muted);
            return clicked;
        }

        private void RequestFreshGame()
        {
            bootstrap ??= GameplaySceneRuntime.ResolveBootstrap();
            if (freshOverwriteConfirmationPending)
            {
                freshOverwriteConfirmationPending = false;
                Launch(
                    GameLaunchMode.FreshGame,
                    allowFreshOverwrite: true);
                return;
            }

            if (!bootstrap.TryGetPersistentProfileExists(
                    GameLaunchContext.DefaultProfileSlot,
                    out bool saveExists))
            {
                statusMessage = bootstrap.LastInitializationError;
                return;
            }

            if (!saveExists)
            {
                Launch(GameLaunchMode.FreshGame);
                return;
            }

            freshOverwriteConfirmationPending = true;
            statusMessage =
                "A persistent save already exists. Press F again, or click " +
                "CONFIRM REPLACE SAVE, to replace it. Choose another mode to cancel.";
        }

        private void CancelFreshConfirmation()
        {
            freshOverwriteConfirmationPending = false;
        }

        private void LaunchShortSwordGeneratorLab()
        {
            if (!GameplaySceneRuntime.TryLoadScene(
                    GameplaySceneNames.ShortSwordGeneratorLab,
                    out string error))
            {
                statusMessage = error;
            }
        }

        private void Launch(
            GameLaunchMode mode,
            bool allowFreshOverwrite = false)
        {
            bootstrap ??= GameplaySceneRuntime.ResolveBootstrap();
            bool started;
            string destination;
            switch (mode)
            {
                case GameLaunchMode.FreshGame:
                    started = bootstrap.StartFreshGame(
                        allowOverwriteExisting: allowFreshOverwrite);
                    destination = GameplaySceneNames.HomeBase;
                    break;
                case GameLaunchMode.Continue:
                    started = bootstrap.ContinueGame();
                    destination = GameplaySceneNames.HomeBase;
                    break;
                case GameLaunchMode.HomeSandbox:
                    started = bootstrap.StartHomeSandbox();
                    destination = GameplaySceneNames.HomeBase;
                    break;
                case GameLaunchMode.RaidSandbox:
                    started = bootstrap.StartRaidSandbox();
                    destination = GameplaySceneNames.RaidPrototype;
                    break;
                case GameLaunchMode.CombatLab:
                    started = bootstrap.StartCombatLab();
                    destination = GameplaySceneNames.CombatLab;
                    break;
                default:
                    started = false;
                    destination = string.Empty;
                    break;
            }

            if (!started)
            {
                statusMessage =
                    string.IsNullOrWhiteSpace(
                        bootstrap.LastInitializationError)
                        ? "The selected session could not be started."
                        : bootstrap.LastInitializationError;
                return;
            }

            if (!GameplaySceneRuntime.TryLoadScene(
                    destination,
                    out string error))
            {
                statusMessage = error;
            }
        }
    }
}
