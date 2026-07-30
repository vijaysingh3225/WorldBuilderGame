using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class HomeBaseController : MonoBehaviour
    {
        [SerializeField] private PlayerInputSource playerInput;

        private GameplayLoopBootstrap bootstrap;
        private GameSession session;
        private WeaponGridSandboxToolkit gridToolkit;
        private bool ownsPlayerInputCapture;
        private string statusMessage =
            "Prepare your weapons, then enter the raid.";

        public void Configure(PlayerInputSource input)
        {
            ReleasePlayerInput();
            playerInput = input;
            CapturePlayerInput();
        }

        private void OnEnable()
        {
            GameplaySceneRuntime.ShowCursor();
            CapturePlayerInput();
        }

        private void Start()
        {
            CapturePlayerInput();
            InitializeSession();
        }

        private void Update()
        {
            GameplaySceneRuntime.ShowCursor();
            CapturePlayerInput();
            if (session == null)
            {
                InitializeSession();
            }

            gridToolkit ??=
                FindFirstObjectByType<WeaponGridSandboxToolkit>();
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.rKey.wasPressedThisFrame)
            {
                LaunchRaid();
            }
            else if (keyboard.mKey.wasPressedThisFrame)
            {
                ReturnToMenu();
            }
        }

        private void OnDisable()
        {
            ReleasePlayerInput();
        }

        private void OnGUI()
        {
            Rect panel = new Rect(
                24f,
                24f,
                Mathf.Min(460f, Screen.width - 48f),
                Mathf.Min(620f, Screen.height - 48f));
            LoopSceneGui.DrawPanel(
                panel,
                new Color(0.36f, 0.62f, 0.40f));

            float x = panel.x + 26f;
            float width = panel.width - 52f;
            float y = panel.y + 22f;
            GUI.Label(
                new Rect(x, y, width, 36f),
                "HOME BASE",
                LoopSceneGui.Title);
            y += 40f;

            if (session == null)
            {
                GUI.Label(
                    new Rect(x, y, width, 60f),
                    statusMessage,
                    LoopSceneGui.Body);
                return;
            }

            PlayerProfile profile = session.ActiveProfile;
            GameLaunchContext context = session.LaunchContext;
            string sessionKind = context.PersistenceEnabled
                ? "PERSISTENT PROFILE"
                : "MEMORY-ONLY SANDBOX";
            GUI.Label(
                new Rect(x, y, width, 22f),
                $"{profile.DisplayName}  /  {sessionKind}",
                LoopSceneGui.Heading);
            y += 26f;
            GUI.Label(
                new Rect(x, y, width, 22f),
                $"Mode: {context.Mode}    Storage entries: " +
                $"{profile.Storage.Count}",
                LoopSceneGui.Muted);
            y += 38f;

            GUI.Label(
                new Rect(x, y, width, 22f),
                "EQUIPPED WEAPONS",
                LoopSceneGui.Heading);
            y += 27f;
            y = DrawWeapon(
                x,
                y,
                width,
                1,
                profile.WeaponOne);
            y = DrawWeapon(
                x,
                y,
                width,
                2,
                profile.WeaponTwo);
            y += 14f;

            GUI.Label(
                new Rect(x, y, width, 22f),
                "STORAGE",
                LoopSceneGui.Heading);
            y += 26f;
            if (profile.Storage.Count == 0)
            {
                GUI.Label(
                    new Rect(x, y, width, 22f),
                    "Empty — extract an artifact to bring it home.",
                    LoopSceneGui.Muted);
                y += 26f;
            }
            else
            {
                int visibleCount = Mathf.Min(
                    profile.Storage.Count,
                    7);
                for (int index = 0;
                     index < visibleCount;
                     index++)
                {
                    StorageEntry entry = profile.Storage[index];
                    GUI.Label(
                        new Rect(x, y, width, 21f),
                        $"• {GameplaySceneRuntime.FriendlyId(entry.DefinitionId)}" +
                        $"  x{entry.Quantity}",
                        LoopSceneGui.Body);
                    y += 22f;
                }

                if (profile.Storage.Count > visibleCount)
                {
                    GUI.Label(
                        new Rect(x, y, width, 21f),
                        $"+ {profile.Storage.Count - visibleCount} more",
                        LoopSceneGui.Muted);
                    y += 22f;
                }
            }

            y += 12f;
            GUI.Label(
                new Rect(x, y, width, 38f),
                "Press Tab to open the shared Weapon Grid toolkit.",
                LoopSceneGui.Muted);
            y += 44f;
            if (GUI.Button(
                    new Rect(x, y, width, 48f),
                    "[ENTER / R]  BEGIN RAID",
                    LoopSceneGui.Button))
            {
                LaunchRaid();
            }

            y += 58f;
            if (GUI.Button(
                    new Rect(x, y, width, 42f),
                    "[M]  RETURN TO MENU",
                    LoopSceneGui.Button))
            {
                ReturnToMenu();
            }

            y += 52f;
            GUI.Label(
                new Rect(x, y, width, 42f),
                statusMessage,
                LoopSceneGui.Body);
        }

        private static float DrawWeapon(
            float x,
            float y,
            float width,
            int slot,
            WeaponInstanceRecord weapon)
        {
            string label = weapon == null
                ? "Unassigned"
                : $"{weapon.DisplayName}  /  Level {weapon.Level}" +
                  $"  /  {weapon.Experience} XP";
            GUI.Label(
                new Rect(x, y, width, 22f),
                $"{slot}.  {label}",
                LoopSceneGui.Body);
            return y + 24f;
        }

        private void InitializeSession()
        {
            bootstrap = GameplaySceneRuntime.ResolveBootstrap();
            session = bootstrap.Session;
            if (session == null ||
                session.LaunchContext.Mode ==
                    GameLaunchMode.CombatLab)
            {
                if (!bootstrap.StartHomeSandbox("direct-home"))
                {
                    statusMessage =
                        bootstrap.LastInitializationError;
                    session = null;
                    return;
                }

                session = bootstrap.Session;
                statusMessage =
                    "Direct scene play: using a disposable Home sandbox.";
            }

            if (session.HasActiveRaid)
            {
                try
                {
                    session.CompleteActiveRaid(
                        RaidCompletionReason.Abandoned,
                        out _);
                }
                catch (InvalidOperationException exception)
                {
                    statusMessage = exception.Message;
                }
            }
        }

        private void LaunchRaid()
        {
            if (session == null)
            {
                InitializeSession();
            }

            if (session == null)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(
                    GameplaySceneNames.RaidPrototype))
            {
                statusMessage =
                    $"Scene '{GameplaySceneNames.RaidPrototype}' is not " +
                    "registered in Build Settings.";
                return;
            }

            try
            {
                if (!session.HasActiveRaid)
                {
                    session.BeginRaid();
                }
            }
            catch (Exception exception)
            {
                statusMessage =
                    $"Could not begin raid: {exception.Message}";
                return;
            }

            GameplaySceneRuntime.TryLoadScene(
                GameplaySceneNames.RaidPrototype,
                out statusMessage);
        }

        private void ReturnToMenu()
        {
            GameplaySceneRuntime.TryLoadScene(
                GameplaySceneNames.Bootstrap,
                out statusMessage);
        }

        private void CapturePlayerInput()
        {
            if (!Application.isPlaying ||
                ownsPlayerInputCapture)
            {
                return;
            }

            if (playerInput == null)
            {
                GameObject player =
                    GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerInput =
                        player.GetComponent<PlayerInputSource>();
                }
            }

            if (playerInput == null)
            {
                return;
            }

            playerInput.SetUserInterfaceCapture(true);
            ownsPlayerInputCapture = true;
        }

        private void ReleasePlayerInput()
        {
            if (ownsPlayerInputCapture &&
                playerInput != null)
            {
                playerInput.SetUserInterfaceCapture(false);
            }

            ownsPlayerInputCapture = false;
        }
    }
}
