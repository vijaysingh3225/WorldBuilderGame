using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public sealed class HomeInventoryController : MonoBehaviour
    {
        private const int PackColumns = 4;
        private const int PackRows = 6;
        private const int ChestColumns = 10;
        private const int ChestRows = 5;

        [SerializeField] private HomeBaseController homeBase;
        [SerializeField] private PlayerInputSource playerInput;
        [SerializeField] private WeaponGridSandboxToolkit gridToolkit;

        private bool isOpen;
        private bool chestOpen;
        private string activeChestId =
            PlayerProfile.DefaultChestId;
        private string activeChestName = "CHEST 1";
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousInputCapture;
        private string statusMessage =
            "Click an item to move it between the pack and chest.";
        private GUIStyle cellStyle;
        private GUIStyle emptyCellStyle;

        public bool IsOpen => isOpen;
        public bool ChestOpen => chestOpen;

        public void Configure(
            HomeBaseController controller,
            PlayerInputSource input,
            WeaponGridSandboxToolkit toolkit)
        {
            homeBase = controller;
            playerInput = input;
            gridToolkit = toolkit;
        }

        public void OpenChest()
        {
            OpenChest(
                PlayerProfile.DefaultChestId,
                "CHEST 1");
        }

        public void OpenChest(
            string chestId,
            string chestName)
        {
            activeChestId =
                string.IsNullOrWhiteSpace(chestId)
                    ? PlayerProfile.DefaultChestId
                    : chestId.Trim();
            activeChestName =
                string.IsNullOrWhiteSpace(chestName)
                    ? "CHEST"
                    : chestName.Trim().ToUpperInvariant();
            chestOpen = true;
            Open();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (isOpen &&
                keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame ||
                keyboard.iKey.wasPressedThisFrame)
            {
                if (isOpen)
                {
                    Close();
                }
                else
                {
                    chestOpen = false;
                    Open();
                }
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

            EnsureStyles();
            LoopSceneGui.DrawDimmer(0.68f);
            float width = Mathf.Min(
                chestOpen ? 1120f : 620f,
                Screen.width - 28f);
            float height = Mathf.Min(700f, Screen.height - 28f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            LoopSceneGui.DrawPanel(
                panel,
                new Color(0.38f, 0.60f, 0.42f));

            float x = panel.x + 26f;
            float y = panel.y + 20f;
            GUI.Label(
                new Rect(x, y, panel.width - 100f, 36f),
                chestOpen ? "BASE STORAGE" : "PLAYER INVENTORY",
                LoopSceneGui.Title);
            GUI.Label(
                new Rect(
                    x,
                    y + 36f,
                    panel.width - 100f,
                    24f),
                "Tab / I / Esc closes",
                LoopSceneGui.Muted);
            if (GUI.Button(
                new Rect(panel.xMax - 54f, y, 28f, 28f),
                "X"))
            {
                Close();
                return;
            }

            PlayerProfile profile =
                homeBase != null ? homeBase.Profile : null;
            if (profile == null)
            {
                GUI.Label(
                    new Rect(x, y + 90f, panel.width - 52f, 42f),
                    "Profile data is not ready.",
                    LoopSceneGui.Body);
                return;
            }

            float gridTop = y + 86f;
            float packWidth = chestOpen ? 350f : panel.width - 52f;
            Rect packArea = new Rect(
                x,
                gridTop,
                packWidth,
                panel.height - 180f);
            DrawContainer(
                packArea,
                "PACK  /  4 × 6",
                BuildPackEntries(profile),
                PackColumns,
                PackRows,
                entry =>
                {
                    if (chestOpen)
                    {
                        statusMessage =
                            profile.MoveToChest(
                                entry.EntryId,
                                activeChestId)
                                ? $"{GameplaySceneRuntime.FriendlyId(entry.DefinitionId)} moved to {activeChestName.ToLowerInvariant()}."
                                : $"{activeChestName} is full.";
                        Persist();
                    }
                });

            if (chestOpen)
            {
                Rect chestArea = new Rect(
                    packArea.xMax + 22f,
                    gridTop,
                    panel.xMax - packArea.xMax - 48f,
                    packArea.height);
                DrawContainer(
                    chestArea,
                    $"{activeChestName}  /  5 × 10",
                    BuildChestEntries(
                        profile,
                        activeChestId),
                    ChestColumns,
                    ChestRows,
                    entry =>
                    {
                        statusMessage =
                            profile.TryMoveToInventory(entry.EntryId)
                                ? $"{GameplaySceneRuntime.FriendlyId(entry.DefinitionId)} moved to pack."
                                : "The 4 × 6 pack is full.";
                        Persist();
                    });
            }

            GUI.Label(
                new Rect(
                    x,
                    panel.yMax - 66f,
                    panel.width - 200f,
                    34f),
                statusMessage,
                LoopSceneGui.Muted);
            if (gridToolkit != null &&
                GUI.Button(
                    new Rect(
                        panel.xMax - 190f,
                        panel.yMax - 72f,
                        164f,
                        42f),
                    "WEAPON GRIDS",
                    LoopSceneGui.Button))
            {
                Close();
                gridToolkit.Open();
            }
        }

        private void DrawContainer(
            Rect area,
            string heading,
            IReadOnlyList<StorageEntry> entries,
            int columns,
            int rows,
            Action<StorageEntry> onItemPressed)
        {
            GUI.Label(
                new Rect(area.x, area.y, area.width, 26f),
                heading,
                LoopSceneGui.Heading);
            float gap = 5f;
            float cellSize = Mathf.Min(
                (area.width - gap * (columns - 1)) / columns,
                (area.height - 42f - gap * (rows - 1)) / rows);
            float boardWidth =
                columns * cellSize + (columns - 1) * gap;
            float startX = area.x + (area.width - boardWidth) * 0.5f;
            float startY = area.y + 36f;
            int capacity = columns * rows;
            for (int index = 0; index < capacity; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect cell = new Rect(
                    startX + column * (cellSize + gap),
                    startY + row * (cellSize + gap),
                    cellSize,
                    cellSize);
                StorageEntry entry =
                    index < entries.Count ? entries[index] : null;
                if (entry == null)
                {
                    GUI.Box(cell, GUIContent.none, emptyCellStyle);
                    continue;
                }

                string label =
                    GetInitials(entry.DefinitionId) +
                    (entry.Quantity > 1 ? $"\n×{entry.Quantity}" : "");
                if (GUI.Button(cell, label, cellStyle))
                {
                    onItemPressed?.Invoke(entry);
                }
            }
        }

        private static List<StorageEntry> BuildPackEntries(
            PlayerProfile profile)
        {
            var entries = new List<StorageEntry>(
                profile.InventoryEntryIds.Count);
            for (int index = 0;
                 index < profile.InventoryEntryIds.Count;
                 index++)
            {
                StorageEntry entry = profile.FindStorageEntry(
                    profile.InventoryEntryIds[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        private static List<StorageEntry> BuildChestEntries(
            PlayerProfile profile,
            string chestId)
        {
            var entries = new List<StorageEntry>();
            IReadOnlyList<string> entryIds =
                profile.GetChestEntryIds(chestId);
            for (int index = 0;
                 index < entryIds.Count;
                 index++)
            {
                StorageEntry entry =
                    profile.FindStorageEntry(
                        entryIds[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        private void Open()
        {
            if (isOpen)
            {
                return;
            }

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
        }

        private void Close()
        {
            if (!isOpen)
            {
                return;
            }

            Persist();
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (playerInput != null)
            {
                playerInput.SetUserInterfaceCapture(
                    previousInputCapture);
            }

            chestOpen = false;
            activeChestId = PlayerProfile.DefaultChestId;
            activeChestName = "CHEST 1";
            isOpen = false;
        }

        private void Persist()
        {
            try
            {
                homeBase?.SaveProfile();
            }
            catch (Exception exception)
            {
                statusMessage =
                    $"Could not save inventory: {exception.Message}";
            }
        }

        private void EnsureStyles()
        {
            cellStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal =
                {
                    textColor = new Color(0.92f, 0.91f, 0.83f)
                }
            };
            emptyCellStyle ??= new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = Texture2D.grayTexture
                }
            };
        }

        private static string GetInitials(string value)
        {
            string friendly =
                GameplaySceneRuntime.FriendlyId(value);
            string[] words = friendly.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return "?";
            }

            return words.Length == 1
                ? words[0].Substring(
                    0,
                    Mathf.Min(2, words[0].Length)).ToUpperInvariant()
                : string.Concat(
                    words[0][0],
                    words[words.Length - 1][0]).ToUpperInvariant();
        }
    }
}
