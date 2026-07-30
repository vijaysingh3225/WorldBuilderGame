using System;
using UnityEngine;
using UnityEngine.InputSystem;
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

        public static bool IsAnyOpen { get; private set; }
        public bool IsOpen => isOpen;

        public void Configure(PlayerInputSource input)
        {
            playerInput = input;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null ||
                !keyboard.escapeKey.wasPressedThisFrame)
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
            float width = Mathf.Min(420f, Screen.width - 32f);
            float height = Mathf.Min(500f, Screen.height - 32f);
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
