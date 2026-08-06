using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class SceneNavigationMenu : MonoBehaviour
    {
        [SerializeField] private PlayerInputSource playerInput;
        [SerializeField] private WeaponGridSandboxToolkit gridToolkit;
        [SerializeField] private HomeInventoryController homeInventory;

        private bool isOpen;
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousInputCapture;
        private string statusMessage = string.Empty;
        private bool showControls;
        private PlayerControl? awaitingBinding;
        private Vector2 controlsScroll;

        public static bool IsAnyOpen { get; private set; }
        public bool IsOpen => isOpen;

        public void Configure(PlayerInputSource input)
        {
            playerInput = input;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (isOpen && awaitingBinding.HasValue)
            {
                CaptureBinding(keyboard);
                return;
            }
            if (!PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Pause))
            {
                return;
            }

            gridToolkit ??=
                FindFirstObjectByType<WeaponGridSandboxToolkit>();
            homeInventory ??=
                FindFirstObjectByType<HomeInventoryController>();
            if ((gridToolkit != null && gridToolkit.IsOpen) ||
                (homeInventory != null && homeInventory.IsOpen))
            {
                return;
            }

            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (!isOpen)
            {
                return;
            }

            LoopSceneGui.DrawDimmer(0.72f);
            float width = Mathf.Min(520f, Screen.width - 32f);
            float height = Mathf.Min(680f, Screen.height - 32f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            LoopSceneGui.DrawPanel(
                panel,
                new Color(0.72f, 0.57f, 0.30f));

            float x = panel.x + 28f;
            float y = panel.y + 24f;
            float contentWidth = panel.width - 56f;
            GUI.Label(
                new Rect(x, y, contentWidth, 38f),
                "PAUSED",
                LoopSceneGui.Title);
            y += 52f;

            float tabWidth = (contentWidth - 8f) * 0.5f;
            if (GUI.Button(
                    new Rect(x, y, tabWidth, 38f),
                    "MENU",
                    LoopSceneGui.Button))
            {
                showControls = false;
                awaitingBinding = null;
            }
            if (GUI.Button(
                    new Rect(x + tabWidth + 8f, y, tabWidth, 38f),
                    "CONTROLS",
                    LoopSceneGui.Button))
            {
                showControls = true;
            }
            y += 50f;

            if (showControls)
            {
                DrawControls(x, y, contentWidth, panel.yMax - y - 20f);
                return;
            }

            if (DrawButton(x, ref y, contentWidth, "RESUME"))
            {
                Close();
                return;
            }

            y += 12f;
            GUI.Label(
                new Rect(x, y, contentWidth, 24f),
                "MANUAL SCENE NAVIGATION",
                LoopSceneGui.Heading);
            y += 32f;
            if (DrawButton(x, ref y, contentWidth, "HOME BASE"))
            {
                LoadScene(GameplaySceneNames.HomeBase);
                return;
            }

            if (DrawButton(x, ref y, contentWidth, "RAID PROTOTYPE"))
            {
                LoadScene(GameplaySceneNames.RaidPrototype);
                return;
            }

            if (DrawButton(x, ref y, contentWidth, "COMBAT LAB"))
            {
                LoadScene(GameplaySceneNames.CombatLab);
                return;
            }

            if (DrawButton(x, ref y, contentWidth, "LAUNCH MENU"))
            {
                LoadScene(GameplaySceneNames.Bootstrap);
                return;
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                GUI.Label(
                    new Rect(
                        x,
                        panel.yMax - 54f,
                        contentWidth,
                        36f),
                    statusMessage,
                    LoopSceneGui.Muted);
            }
        }

        private static bool DrawButton(
            float x,
            ref float y,
            float width,
            string label)
        {
            bool pressed = GUI.Button(
                new Rect(x, y, width, 44f),
                label,
                LoopSceneGui.Button);
            y += 52f;
            return pressed;
        }

        private void DrawControls(
            float x,
            float y,
            float width,
            float height)
        {
            GUI.Label(
                new Rect(x, y, width, 24f),
                awaitingBinding.HasValue
                    ? $"PRESS ANY KEY FOR {PlayerControlBindings.ActionName(awaitingBinding.Value).ToUpperInvariant()}"
                    : "SELECT A KEY TO REBIND",
                LoopSceneGui.Heading);
            y += 30f;

            Rect viewport = new Rect(x, y, width, height - 48f);
            float contentHeight =
                PlayerControlBindings.AllControls.Length * 38f +
                176f;
            controlsScroll = GUI.BeginScrollView(
                viewport,
                controlsScroll,
                new Rect(0f, 0f, width - 18f, contentHeight));
            float rowY = 0f;
            for (int index = 0;
                 index < PlayerControlBindings.AllControls.Length;
                 index++)
            {
                PlayerControl control =
                    PlayerControlBindings.AllControls[index];
                GUI.Label(
                    new Rect(4f, rowY + 5f, width * 0.56f, 28f),
                    PlayerControlBindings.ActionName(control),
                    LoopSceneGui.Body);
                string keyLabel = awaitingBinding == control
                    ? "PRESS KEY..."
                    : PlayerControlBindings.KeyName(
                        PlayerControlBindings.GetKey(control));
                if (GUI.Button(
                        new Rect(
                            width * 0.57f,
                            rowY,
                            width * 0.36f,
                            32f),
                        keyLabel,
                        LoopSceneGui.Button))
                {
                    awaitingBinding = control;
                    statusMessage = string.Empty;
                }
                rowY += 38f;
            }

            GUI.Label(
                new Rect(4f, rowY + 4f, width * 0.56f, 26f),
                "Attack / Draw Bow",
                LoopSceneGui.Body);
            GUI.Label(
                new Rect(width * 0.62f, rowY + 4f, width * 0.30f, 26f),
                "Left Mouse",
                LoopSceneGui.Muted);
            rowY += 30f;
            GUI.Label(
                new Rect(4f, rowY + 4f, width * 0.56f, 26f),
                "Block",
                LoopSceneGui.Body);
            GUI.Label(
                new Rect(width * 0.62f, rowY + 4f, width * 0.30f, 26f),
                "Right Mouse",
                LoopSceneGui.Muted);
            rowY += 30f;
            GUI.Label(
                new Rect(4f, rowY + 4f, width * 0.56f, 26f),
                "Orbit / Inspect",
                LoopSceneGui.Body);
            GUI.Label(
                new Rect(width * 0.62f, rowY + 4f, width * 0.30f, 26f),
                "Middle Mouse",
                LoopSceneGui.Muted);
            rowY += 30f;
            GUI.Label(
                new Rect(4f, rowY + 4f, width * 0.56f, 26f),
                "Cycle Weapons",
                LoopSceneGui.Body);
            GUI.Label(
                new Rect(width * 0.62f, rowY + 4f, width * 0.30f, 26f),
                "Mouse Wheel",
                LoopSceneGui.Muted);
            rowY += 36f;
            if (GUI.Button(
                    new Rect(4f, rowY, width - 36f, 34f),
                    "RESET DEFAULTS",
                    LoopSceneGui.Button))
            {
                PlayerControlBindings.ResetToDefaults();
                awaitingBinding = null;
                statusMessage = "Controls restored to defaults.";
            }
            GUI.EndScrollView();
        }

        private void CaptureBinding(Keyboard keyboard)
        {
            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                if (!keyControl.wasPressedThisFrame)
                {
                    continue;
                }
                PlayerControl control = awaitingBinding.Value;
                PlayerControlBindings.Rebind(
                    control,
                    keyControl.keyCode);
                awaitingBinding = null;
                statusMessage =
                    $"{PlayerControlBindings.ActionName(control)}: " +
                    PlayerControlBindings.KeyName(keyControl.keyCode);
                return;
            }
        }

        private void Open()
        {
            if (isOpen)
            {
                return;
            }

            ResolvePlayerInput();
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (playerInput != null)
            {
                previousInputCapture =
                    playerInput.UserInterfaceCaptureActive;
                playerInput.SetUserInterfaceCapture(true);
            }

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isOpen = true;
            IsAnyOpen = true;
        }

        private void Close()
        {
            if (!isOpen)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (playerInput != null)
            {
                playerInput.SetUserInterfaceCapture(
                    previousInputCapture);
            }

            isOpen = false;
            IsAnyOpen = false;
            awaitingBinding = null;
            showControls = false;
        }

        private void LoadScene(string sceneName)
        {
            GameplayLoopBootstrap bootstrap =
                GameplaySceneRuntime.ResolveBootstrap();
            GameSession session = bootstrap.Session;
            try
            {
                if (session != null &&
                    session.HasActiveRaid &&
                    !string.Equals(
                        sceneName,
                        GameplaySceneNames.RaidPrototype,
                        StringComparison.Ordinal))
                {
                    session.CompleteActiveRaid(
                        RaidCompletionReason.Abandoned,
                        out _);
                }

                if (session != null &&
                    !session.HasActiveRaid &&
                    string.Equals(
                        sceneName,
                        GameplaySceneNames.RaidPrototype,
                        StringComparison.Ordinal))
                {
                    session.BeginRaid(
                        carriedStorageEntryIds:
                            session.ActiveProfile.InventoryEntryIds);
                }
            }
            catch (Exception exception)
            {
                statusMessage = exception.Message;
                return;
            }

            Close();
            GameplaySceneRuntime.TryLoadScene(
                sceneName,
                out statusMessage);
        }

        private void ResolvePlayerInput()
        {
            if (playerInput != null)
            {
                return;
            }

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerInput =
                    player.GetComponent<PlayerInputSource>() ??
                    player.GetComponentInChildren<PlayerInputSource>(true);
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsAnyOpen = false;
        }
    }
}
