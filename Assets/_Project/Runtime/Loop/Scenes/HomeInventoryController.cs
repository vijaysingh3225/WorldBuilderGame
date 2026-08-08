using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public sealed class HomeInventoryController : MonoBehaviour
    {
        public const float InventoryHorizontalAlignmentOffset = 0f;
        private const int PackColumns = 4;
        private const int PackRows = 6;
        private const int ChestColumns = 5;
        private const int ChestRows = 10;
        private const int SecureColumns = PlayerProfile.SecureColumns;
        private const int SecureRows = PlayerProfile.SecureRows;
        private const float StorageCellGap = 2f;
        private const float PreviousStorageCellGap = 5f;
        public const float InventoryCellScale = 0.78f;
        public const float InventoryBackdropOpacity = 0.72f;

        private enum InventoryGridKind
        {
            Passive,
            Player,
            Loot,
            Chest,
            Secure
        }

        private enum StackPaintMode
        {
            None,
            OnePerCell,
            Evenly
        }

        private readonly struct StackPaintCell
        {
            public StackPaintCell(
                InventoryGridKind grid,
                int slot)
            {
                Grid = grid;
                Slot = slot;
            }

            public InventoryGridKind Grid { get; }
            public int Slot { get; }
        }

        [SerializeField] private HomeBaseController homeBase;
        [SerializeField] private PlayerInputSource playerInput;
        [SerializeField] private WeaponGridSandboxToolkit gridToolkit;
        [SerializeField] private InventoryPreviewRenderer previewRenderer;

        private bool isOpen;
        private bool chestOpen;
        private RaidLootContainer activeRaidLoot;
        private StorageEntry heldEntry;
        private InventoryGridKind heldOrigin;
        private RaidLootContainer heldLootSource;
        private int heldOriginSlot = -1;
        private Vector2Int heldGrabOffset;
        private float heldCellSize;
        private bool leftPressPickedUpItem;
        private StorageEntry inspectedLootWeapon;
        private LootWeaponData inspectedLootWeaponData;
        private WeaponGridState inspectedLootWeaponGrid;
        private StorageEntry weaponContextEntry;
        private InventoryGridKind weaponContextGrid;
        private Rect weaponContextMenuRect;
        private StackPaintMode stackPaintMode;
        private readonly List<StackPaintCell> stackPaintCells =
            new List<StackPaintCell>();
        private string activeChestId = PlayerProfile.DefaultChestId;
        private string activeChestName = "CHEST 1";
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousInputCapture;
        private Vector2 lootScrollPosition;
        private Vector2 playerStorageScrollPosition;
        private string statusMessage =
            "The equipped backpack owns this 4 x 6 inventory.";
        private GUIStyle cellStyle;
        private GUIStyle emptyCellStyle;
        private GUIStyle equipmentSlotStyle;
        private GUIStyle equippedSlotStyle;
        private GUIStyle weaponCardStyle;
        private GUIStyle centeredTitleStyle;
        private GUIStyle slotLabelStyle;
        private GUIStyle quantityStyle;
        private HomeAnvil anvil;

        public bool IsOpen => isOpen;
        public bool ChestOpen => chestOpen;
        public RaidLootContainer ActiveRaidLoot => activeRaidLoot;

        public void OpenInventory()
        {
            chestOpen = false;
            activeRaidLoot = null;
            Open();
        }

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
            OpenChest(PlayerProfile.DefaultChestId, "CHEST 1");
        }

        public void OpenChest(string chestId, string chestName)
        {
            activeRaidLoot = null;
            activeChestId = string.IsNullOrWhiteSpace(chestId)
                ? PlayerProfile.DefaultChestId
                : chestId.Trim();
            activeChestName = string.IsNullOrWhiteSpace(chestName)
                ? "CHEST"
                : chestName.Trim().ToUpperInvariant();
            lootScrollPosition = Vector2.zero;
            chestOpen = true;
            Open();
        }

        public void OpenLoot(RaidLootContainer source)
        {
            if (source == null || !source.IsAvailable)
            {
                return;
            }

            chestOpen = false;
            activeRaidLoot = source;
            ClearHeldItem();
            statusMessage =
                "Left click moves a stack. Right click splits or places one. R rotates a held item. Shift-click auto-stacks.";
            Open();
        }

        private void Update()
        {
            anvil ??= FindFirstObjectByType<HomeAnvil>();
            if (anvil != null && anvil.IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                if (PlayerControlBindings.WasPressedThisFrame(
                        keyboard,
                        PlayerControl.Inventory))
                {
                    Close();
                }
                return;
            }
            if (isOpen &&
                heldEntry != null &&
                PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.RotateInventoryItem))
            {
                RotateHeldItem();
                return;
            }
            if (isOpen &&
                PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Pause))
            {
                Close();
                return;
            }
            if (PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Inventory))
            {
                if (isOpen)
                {
                    Close();
                }
                else
                {
                    chestOpen = false;
                    activeRaidLoot = null;
                    Open();
                }
            }
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                if (!ReturnHeldItem())
                {
                    ClearHeldItem();
                }
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
            bool childGridOpen =
                gridToolkit != null && gridToolkit.IsOpen;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !childGridOpen;
            DrawInventoryScreen(
                CalculatePanelRect(Screen.width, Screen.height));
            if (!childGridOpen)
            {
                DrawHeldItem();
            }
            if (!childGridOpen && inspectedLootWeapon != null)
            {
                DrawLootWeaponInspector();
            }
            if (!childGridOpen && weaponContextEntry != null)
            {
                DrawWeaponContextMenu();
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawInventoryScreen(Rect panel)
        {
            LoopSceneGui.DrawTranslucentBackdrop(
                panel,
                InventoryBackdropOpacity);

            float sectionSpacing =
                CalculateInventorySectionSpacing(panel.width);
            float x =
                panel.x +
                sectionSpacing +
                InventoryHorizontalAlignmentOffset;
            float y = panel.y + sectionSpacing;
            GUI.Label(
                new Rect(
                    x,
                    y,
                    panel.width - sectionSpacing * 2f,
                    34f),
                chestOpen
                    ? "INVENTORY  /  BASE STORAGE"
                    : activeRaidLoot != null
                        ? $"INVENTORY  /  {activeRaidLoot.DisplayName.ToUpperInvariant()}"
                        : "INVENTORY",
                centeredTitleStyle);
            GUI.Label(
                new Rect(
                    x,
                    y + 30f,
                    panel.width - sectionSpacing * 2f - 42f,
                    20f),
                "Drag preview to rotate  |  Tab / I / Esc closes",
                LoopSceneGui.Muted);
            if (GUI.Button(
                new Rect(
                    panel.xMax - sectionSpacing - 26f,
                    y,
                    26f,
                    26f),
                "X"))
            {
                Close();
                return;
            }

            PlayerProfile profile = ResolveProfile();
            if (profile == null)
            {
                GUI.Label(
                    new Rect(x, y + 90f, panel.width - 48f, 42f),
                    "Profile data is not ready.",
                    LoopSceneGui.Body);
                return;
            }

            float contentTop = y + 52f;
            float contentBottom = panel.yMax - sectionSpacing;
            Rect contentArea = new Rect(
                x,
                contentTop,
                panel.width - sectionSpacing * 2f,
                contentBottom - contentTop);
            bool lootSectionVisible =
                ShouldDrawLootSection(
                    chestOpen,
                    activeRaidLoot != null);
            Rect characterArea = CalculateInventoryColumn(
                contentArea,
                0,
                sectionSpacing);
            Rect inventoryArea = CalculateInventoryColumn(
                contentArea,
                1,
                sectionSpacing);
            Rect lootArea = lootSectionVisible
                ? CalculateInventoryColumn(
                    contentArea,
                    2,
                    sectionSpacing)
                : default;
            float sharedCellSize = CalculateSharedStorageCellSize(
                inventoryArea.width,
                inventoryArea.height);
            DrawCharacterLoadout(characterArea, profile);

            IReadOnlyList<StorageEntry> packEntries = BuildPackEntries(profile);
            DrawPlayerStorageColumn(
                inventoryArea,
                profile,
                packEntries,
                sharedCellSize);

            if (chestOpen)
            {
                DrawContainer(
                    lootArea,
                    $"{activeChestName}  /  5 x 10",
                    BuildChestEntries(profile, activeChestId),
                    ChestColumns,
                    ChestRows,
                    sharedCellSize,
                    true,
                    entry =>
                    {
                        statusMessage =
                            profile.TryMoveToInventory(entry.EntryId)
                                ? $"{GameplaySceneRuntime.FriendlyId(entry.DefinitionId)} moved to backpack."
                                : "The 4 x 6 backpack is full.";
                        Persist();
                    },
                    InventoryGridKind.Chest);
            }
            else if (activeRaidLoot != null)
            {
                DrawContainer(
                    lootArea,
                    $"{activeRaidLoot.DisplayName.ToUpperInvariant()}  /  " +
                    $"{activeRaidLoot.Columns} x {activeRaidLoot.Rows}",
                    activeRaidLoot.Entries,
                    activeRaidLoot.Columns,
                    activeRaidLoot.Rows,
                    sharedCellSize,
                    false,
                    null,
                    InventoryGridKind.Loot);
            }
            GUI.Label(
                new Rect(
                    x,
                    panel.yMax - sectionSpacing + 8f,
                    panel.width - sectionSpacing * 2f,
                    20f),
                statusMessage,
                LoopSceneGui.Muted);
        }

        public static Rect CalculatePanelRect(float screenWidth, float screenHeight)
        {
            return new Rect(
                0f,
                0f,
                Mathf.Max(0f, screenWidth),
                Mathf.Max(0f, screenHeight));
        }

        public static Rect CalculateInventoryColumn(
            Rect contentArea,
            int columnIndex,
            float sectionSpacing)
        {
            int index = Mathf.Clamp(columnIndex, 0, 2);
            float columnGap = sectionSpacing * 0.25f;
            float width = Mathf.Max(
                0f,
                (contentArea.width - columnGap * 2f) / 3f);
            float x =
                contentArea.x +
                index * (width + columnGap);
            return new Rect(
                x,
                contentArea.y,
                width,
                contentArea.height);
        }

        public static bool ShouldDrawLootSection(
            bool isChestOpen,
            bool hasActiveRaidLoot)
        {
            return isChestOpen || hasActiveRaidLoot;
        }

        public static float CalculateInventorySectionSpacing(
            float screenWidth)
        {
            return Mathf.Clamp(screenWidth * 0.04f, 32f, 56f);
        }

        public static float CalculateSharedStorageCellSize(
            float columnWidth,
            float columnHeight)
        {
            const float horizontalPadding = 32f;
            const float verticalPadding = 56f;
            const float scrollBarAllowance = 8f;
            float widthLimit =
                (columnWidth - horizontalPadding - scrollBarAllowance -
                    PreviousStorageCellGap * (ChestColumns - 1)) /
                ChestColumns;
            float heightLimit =
                (columnHeight - verticalPadding -
                    PreviousStorageCellGap * (PackRows - 1)) /
                PackRows;
            return Mathf.Max(
                12f,
                Mathf.Floor(Mathf.Min(widthLimit, heightLimit)) *
                    InventoryCellScale);
        }

        public static float CalculatePlayerStorageContentHeight(
            float cellSize)
        {
            float backpackHeight = PackRows * cellSize +
                (PackRows - 1) * StorageCellGap;
            float secureHeight = SecureRows * cellSize +
                (SecureRows - 1) * StorageCellGap;
            return backpackHeight + 42f + secureHeight + 8f;
        }

        private void DrawCharacterLoadout(
            Rect area,
            PlayerProfile profile)
        {
            DrawInventorySection(area);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 10f, area.width - 32f, 24f),
                "CHARACTER AND EQUIPMENT",
                LoopSceneGui.Heading);

            float weaponHeight = Mathf.Clamp(area.height * 0.19f, 82f, 104f);
            Rect modelArea = new Rect(
                area.x + 74f,
                area.y + 32f,
                area.width - 148f,
                area.height - weaponHeight - 48f);
            if (previewRenderer != null &&
                previewRenderer.CharacterTexture != null)
            {
                GUI.DrawTexture(
                    modelArea,
                    previewRenderer.CharacterTexture,
                    ScaleMode.ScaleToFit,
                    true);
            }
            else
            {
                GUI.Box(modelArea, "PLAYER PREVIEW", emptyCellStyle);
            }
            HandlePreviewRotation(
                modelArea,
                delta => previewRenderer?.RotateCharacter(delta));

            float slotSize = Mathf.Clamp(area.width * 0.145f, 48f, 58f);
            float left = area.x + 9f;
            float right = area.xMax - slotSize - 9f;
            float top = area.y + 48f;
            float stride = slotSize + 24f;
            DrawEquipmentSlot(new Rect(left, top, slotSize, slotSize), "HEAD", false);
            DrawEquipmentSlot(new Rect(left, top + stride, slotSize, slotSize), "CHEST", false);
            DrawEquipmentSlot(new Rect(left, top + stride * 2f, slotSize, slotSize), "HANDS", false);
            DrawEquipmentSlot(new Rect(right, top, slotSize, slotSize), "LEGS", false);
            DrawEquipmentSlot(new Rect(right, top + stride, slotSize, slotSize), "FEET", false);
            DrawEquipmentSlot(new Rect(right, top + stride * 2f, slotSize, slotSize), "BACKPACK", true);

            Rect weaponRow = new Rect(
                area.x + 9f,
                area.yMax - weaponHeight - 8f,
                area.width - 18f,
                weaponHeight);
            float cardWidth = (weaponRow.width - 10f) * 0.5f;
            DrawWeaponCard(
                new Rect(weaponRow.x, weaponRow.y, cardWidth, weaponRow.height),
                0,
                "PRIMARY  /  1",
                profile.GetWeapon(1)?.DisplayName,
                previewRenderer != null ? previewRenderer.PrimaryThumbnail : null);
            DrawWeaponCard(
                new Rect(weaponRow.x + cardWidth + 10f, weaponRow.y, cardWidth, weaponRow.height),
                1,
                "SECONDARY  /  2",
                profile.GetWeapon(2)?.DisplayName,
                previewRenderer != null ? previewRenderer.SecondaryThumbnail : null);
        }

        private void DrawEquipmentSlot(Rect rect, string label, bool equipped)
        {
            DrawInventoryCellSurface(rect);
            if (equipped)
            {
                GUI.Label(
                    rect,
                    "PACK\n4 x 6",
                    equippedSlotStyle);
            }
            else
            {
                GUI.Label(
                    rect,
                    "EMPTY",
                    equipmentSlotStyle);
            }
            GUI.Label(
                new Rect(rect.x, rect.y - 18f, rect.width, 17f),
                label,
                slotLabelStyle);
        }

        private void DrawWeaponCard(
            Rect rect,
            int weaponIndex,
            string label,
            string weaponName,
            Texture thumbnail)
        {
            DrawInventoryCellSurface(rect);

            if (GUI.Button(rect, GUIContent.none, weaponCardStyle))
            {
                OpenWeaponGrid(weaponIndex);
                return;
            }
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f),
                label,
                LoopSceneGui.Muted);
            GUI.Label(
                new Rect(
                    rect.x + 10f,
                    rect.y + 23f,
                    rect.width - 20f,
                    20f),
                string.IsNullOrWhiteSpace(weaponName)
                    ? "EMPTY"
                    : weaponName.ToUpperInvariant(),
                slotLabelStyle);
            if (thumbnail != null)
            {
                GUI.DrawTexture(
                    new Rect(rect.x + 8f, rect.y + 39f, rect.width - 16f, rect.height - 45f),
                    thumbnail,
                    ScaleMode.ScaleToFit,
                    true);
            }
        }

        private void OpenWeaponGrid(int weaponIndex)
        {
            WeaponGridSandboxToolkit toolkit = ResolveGridToolkit();
            if (toolkit == null)
            {
                statusMessage =
                    "The weapon grid is unavailable. Reopen the inventory and try again.";
                return;
            }
            toolkit.OpenWeapon(weaponIndex);
        }

        private WeaponGridSandboxToolkit ResolveGridToolkit()
        {
            if (gridToolkit != null)
            {
                return gridToolkit;
            }
            gridToolkit = GetComponent<WeaponGridSandboxToolkit>() ??
                FindFirstObjectByType<WeaponGridSandboxToolkit>(
                    FindObjectsInactive.Include);
            return gridToolkit;
        }

        private void DrawPlayerStorageColumn(
            Rect area,
            PlayerProfile profile,
            IReadOnlyList<StorageEntry> packEntries,
            float cellSize)
        {
            DrawInventorySection(area);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 11f, area.width - 32f, 26f),
                "EQUIPPED BACKPACK  /  4 x 6",
                LoopSceneGui.Heading);
            Rect viewport = new Rect(
                area.x + 16f,
                area.y + 43f,
                area.width - 32f,
                area.height - 56f);
            float backpackWidth = PackColumns * cellSize +
                (PackColumns - 1) * StorageCellGap;
            float backpackHeight = PackRows * cellSize +
                (PackRows - 1) * StorageCellGap;
            float secureWidth = SecureColumns * cellSize +
                (SecureColumns - 1) * StorageCellGap;
            float secureLabelY = backpackHeight + 16f;
            float secureGridY = secureLabelY + 26f;
            float contentHeight = CalculatePlayerStorageContentHeight(
                cellSize);
            bool needsVerticalScroll = contentHeight > viewport.height;
            float viewWidth = Mathf.Max(
                backpackWidth,
                viewport.width -
                    (needsVerticalScroll
                        ? GameTypography.MinimalVerticalScrollbarWidth + 2f
                        : 0f));
            Rect scrollContent = new Rect(
                0f,
                0f,
                viewWidth,
                Mathf.Max(contentHeight, viewport.height));
            playerStorageScrollPosition =
                LoopSceneGui.BeginVerticalScrollView(
                viewport,
                playerStorageScrollPosition,
                scrollContent,
                needsVerticalScroll);
            DrawGrid(
                (viewWidth - backpackWidth) * 0.5f,
                0f,
                packEntries,
                PackColumns,
                PackRows,
                cellSize,
                InventoryGridKind.Player,
                null);
            GUI.Label(
                new Rect(0f, secureLabelY, viewWidth, 22f),
                "SECURE  /  2 x 2  /  KEPT ON DEATH",
                LoopSceneGui.Heading);
            DrawGrid(
                (viewWidth - secureWidth) * 0.5f,
                secureGridY,
                BuildSecureEntries(profile),
                SecureColumns,
                SecureRows,
                cellSize,
                InventoryGridKind.Secure,
                null);
            GUI.EndScrollView();
        }

        private void DrawContainer(
            Rect area,
            string heading,
            IReadOnlyList<StorageEntry> entries,
            int columns,
            int rows,
            float cellSize,
            bool scrollable,
            Action<StorageEntry> onItemPressed,
            InventoryGridKind gridKind = InventoryGridKind.Passive)
        {
            DrawInventorySection(area);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 11f, area.width - 32f, 26f),
                heading,
                LoopSceneGui.Heading);
            float boardWidth =
                columns * cellSize +
                (columns - 1) * StorageCellGap;
            float boardHeight =
                rows * cellSize +
                (rows - 1) * StorageCellGap;
            Rect viewport = new Rect(
                area.x + 16f,
                area.y + 43f,
                area.width - 32f,
                area.height - 56f);
            float startX;
            float startY;
            if (scrollable)
            {
                bool needsVerticalScroll =
                    boardHeight > viewport.height;
                float viewWidth = Mathf.Max(
                    boardWidth,
                    viewport.width -
                        (needsVerticalScroll
                            ? GameTypography.MinimalVerticalScrollbarWidth + 2f
                            : 0f));
                Rect scrollContent = new Rect(
                    0f,
                    0f,
                    viewWidth,
                    Mathf.Max(boardHeight, viewport.height));
                lootScrollPosition =
                    LoopSceneGui.BeginVerticalScrollView(
                    viewport,
                    lootScrollPosition,
                    scrollContent,
                    needsVerticalScroll);
                startX = (viewWidth - boardWidth) * 0.5f;
                startY = 0f;
            }
            else
            {
                startX =
                    viewport.x +
                    (viewport.width - boardWidth) * 0.5f;
                startY = viewport.y;
            }
            DrawGrid(
                startX,
                startY,
                entries,
                columns,
                rows,
                cellSize,
                gridKind,
                onItemPressed);
            if (scrollable)
            {
                GUI.EndScrollView();
            }
        }

        private void DrawGrid(
            float startX,
            float startY,
            IReadOnlyList<StorageEntry> entries,
            int columns,
            int rows,
            float cellSize,
            InventoryGridKind gridKind,
            Action<StorageEntry> onItemPressed)
        {
            int capacity = columns * rows;
            StorageEntry[] slots = BuildSlotMap(
                entries,
                columns,
                rows);
            var cellRects = new Rect[capacity];
            for (int index = 0; index < capacity; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect cell = new Rect(
                    startX + column * (cellSize + StorageCellGap),
                    startY + row * (cellSize + StorageCellGap),
                    cellSize,
                    cellSize);
                cellRects[index] = cell;
                StorageEntry entry = slots[index];
                bool hovered = cell.Contains(Event.current.mousePosition);
                if (gridKind != InventoryGridKind.Passive && hovered)
                {
                    HandleGridInput(
                        gridKind,
                        index,
                        entry,
                        cellSize,
                        columns);
                }

                DrawInventoryCellSurface(cell);
                if (entry != null &&
                    gridKind == InventoryGridKind.Passive &&
                    GUI.Button(cell, GUIContent.none, GUIStyle.none))
                {
                    onItemPressed?.Invoke(entry);
                }

                if (hovered)
                {
                    Color previous = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.14f);
                    GUI.DrawTexture(cell, Texture2D.whiteTexture);
                    GUI.color = previous;
                }
            }
            if (heldEntry != null)
            {
                for (int index = 0; index < cellRects.Length; index++)
                {
                    if (cellRects[index].Contains(Event.current.mousePosition))
                    {
                        DrawHeldPlacementPreview(
                            cellRects,
                            entries,
                            index,
                            columns,
                            rows);
                        break;
                    }
                }
            }
            var drawnEntryIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                StorageEntry entry = entries[index];
                if (entry == null ||
                    !drawnEntryIds.Add(entry.EntryId) ||
                    !TryGetItemRect(
                        entry,
                        columns,
                        rows,
                        cellRects,
                        out Rect itemRect))
                {
                    continue;
                }
                DrawItem(itemRect, entry, cellSize);
            }

            if (stackPaintMode == StackPaintMode.Evenly &&
                heldEntry != null)
            {
                for (int index = 0;
                     index < stackPaintCells.Count;
                     index++)
                {
                    StackPaintCell painted = stackPaintCells[index];
                    if (painted.Grid != gridKind ||
                        painted.Slot < 0 ||
                        painted.Slot >= cellRects.Length)
                    {
                        continue;
                    }

                    int previewQuantity =
                        CalculateEvenDistributionAmount(
                            heldEntry.Quantity,
                            stackPaintCells.Count,
                            index);
                    if (previewQuantity <= 0)
                    {
                        continue;
                    }

                    Color previous = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.78f);
                    DrawItem(
                        cellRects[painted.Slot],
                        heldEntry,
                        cellSize,
                        previewQuantity);
                    GUI.color = previous;
                }
            }
        }

        private void HandleGridInput(
            InventoryGridKind gridKind,
            int slotIndex,
            StorageEntry entry,
            float cellSize,
            int columns)
        {
            Event current = Event.current;
            if (stackPaintMode != StackPaintMode.None)
            {
                int paintButton = stackPaintMode ==
                    StackPaintMode.Evenly ? 0 : 1;
                if ((current.type == EventType.MouseDrag ||
                     current.type == EventType.MouseUp) &&
                    current.button == paintButton)
                {
                    PaintHeldStackAcrossCell(
                        gridKind,
                        slotIndex);
                    if (current.type == EventType.MouseUp)
                    {
                        FinishStackPaint();
                    }
                    current.Use();
                    return;
                }
            }
            if (current.type == EventType.MouseUp &&
                current.button == 0 &&
                leftPressPickedUpItem &&
                heldEntry != null)
            {
                int targetAnchor = CalculateHeldAnchorSlot(
                    slotIndex,
                    columns);
                if (gridKind != heldOrigin ||
                    targetAnchor != heldOriginSlot)
                {
                    PlaceHeld(gridKind, slotIndex, heldEntry.Quantity);
                }
                leftPressPickedUpItem = false;
                current.Use();
                return;
            }
            if (current.type != EventType.MouseDown)
            {
                return;
            }

            if (current.button == 0 &&
                current.clickCount == 2 &&
                heldEntry == null &&
                entry != null &&
                ItemDefinitionCatalog.IsWeapon(entry.DefinitionId))
            {
                OpenLootWeaponInspector(entry);
                current.Use();
                return;
            }

            if (heldEntry != null &&
                CanPaintHeldStack() &&
                (current.button == 0 || current.button == 1))
            {
                BeginStackPaint(
                    current.button == 0
                        ? StackPaintMode.Evenly
                        : StackPaintMode.OnePerCell);
                PaintHeldStackAcrossCell(
                    gridKind,
                    slotIndex);
                current.Use();
                return;
            }

            if (current.button == 0 && current.shift)
            {
                if (heldEntry == null && entry != null)
                {
                    ShiftTransfer(gridKind, entry);
                }
                current.Use();
                return;
            }

            if (current.button == 1)
            {
                if (heldEntry == null && entry != null &&
                    ItemDefinitionCatalog.IsWeapon(entry.DefinitionId))
                {
                    weaponContextEntry = entry;
                    weaponContextGrid = gridKind;
                    weaponContextMenuRect = new Rect(
                        current.mousePosition.x + 8f,
                        current.mousePosition.y + 8f,
                        150f,
                        78f);
                    current.Use();
                    return;
                }
                if (heldEntry == null && entry != null)
                {
                    PickUp(
                        gridKind,
                        entry,
                        Mathf.CeilToInt(entry.Quantity * 0.5f),
                        cellSize,
                        slotIndex,
                        columns);
                }
                else if (heldEntry != null)
                {
                    PlaceHeld(gridKind, slotIndex, 1);
                }
                current.Use();
                return;
            }

            if (current.button != 0)
            {
                return;
            }
            if (heldEntry == null && entry != null)
            {
                PickUp(
                    gridKind,
                    entry,
                    entry.Quantity,
                    cellSize,
                    slotIndex,
                    columns);
                leftPressPickedUpItem = heldEntry != null;
            }
            else if (heldEntry != null)
            {
                leftPressPickedUpItem = false;
                PlaceHeld(gridKind, slotIndex, heldEntry.Quantity);
            }
            current.Use();
        }

        private void DrawHeldItem()
        {
            if (heldEntry == null)
            {
                return;
            }

            IReadOnlyList<Vector2Int> footprint =
                ItemDefinitionCatalog.GetFootprint(
                    heldEntry.DefinitionId,
                    heldEntry.RotationQuarterTurns);
            GetFootprintDimensions(
                footprint,
                out int width,
                out int height);
            float stride = heldCellSize + StorageCellGap;
            Vector2 mouse = Event.current.mousePosition;
            Rect attached = new Rect(
                mouse.x - heldCellSize * 0.5f -
                    heldGrabOffset.x * stride,
                mouse.y - heldCellSize * 0.5f -
                    heldGrabOffset.y * stride,
                width * heldCellSize +
                    Mathf.Max(0, width - 1) * StorageCellGap,
                height * heldCellSize +
                    Mathf.Max(0, height - 1) * StorageCellGap);
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            DrawItem(attached, heldEntry, heldCellSize);
            GUI.color = previousColor;
            if (Event.current.type == EventType.MouseUp &&
                stackPaintMode != StackPaintMode.None)
            {
                int paintButton = stackPaintMode ==
                    StackPaintMode.Evenly ? 0 : 1;
                if (Event.current.button == paintButton)
                {
                    FinishStackPaint();
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.MouseUp &&
                Event.current.button == 0)
            {
                leftPressPickedUpItem = false;
            }
        }

        private void OpenLootWeaponInspector(StorageEntry entry)
        {
            if (entry == null ||
                !LootWeaponData.TryParse(
                    entry.CustomStateJson,
                    out LootWeaponData data))
            {
                statusMessage = "This weapon's data could not be read.";
                return;
            }

            inspectedLootWeapon = entry;
            inspectedLootWeaponData = data;
            if (data.WeaponDefinitionId != ItemDefinitionIds.LootShortSword)
            {
                previewRenderer?.SelectWeapon(1);
            }
            inspectedLootWeaponGrid = string.IsNullOrWhiteSpace(
                data.GridStateJson)
                    ? null
                    : JsonUtility.FromJson<WeaponGridState>(
                        data.GridStateJson);
            inspectedLootWeaponGrid?.EnsureInitialized(
                data.DisplayName,
                data.VisualSeed);
        }

        private void DrawLootWeaponInspector()
        {
            float windowWidth = Mathf.Min(820f, Screen.width - 30f);
            float windowHeight = Mathf.Min(620f, Screen.height - 30f);
            Rect window = new Rect(
                Screen.width * 0.5f - windowWidth * 0.5f,
                Screen.height * 0.5f - windowHeight * 0.5f,
                windowWidth,
                windowHeight);
            LoopSceneGui.DrawPanel(window, new Color(0.24f, 0.31f, 0.33f));
            if (GUI.Button(new Rect(window.xMax - 34f, window.y + 8f, 26f, 24f), "X"))
            {
                inspectedLootWeapon = null;
                inspectedLootWeaponData = null;
                inspectedLootWeaponGrid = null;
                return;
            }

            GUI.Label(
                new Rect(window.x + 18f, window.y + 14f, window.width - 60f, 28f),
                inspectedLootWeaponData.DisplayName.ToUpperInvariant(),
                LoopSceneGui.Heading);
            GUI.Label(
                new Rect(window.x + 18f, window.y + 44f, window.width - 36f, 22f),
                $"LEVEL {inspectedLootWeaponData.Level}  /  DOUBLE-CLICKED RAID WEAPON",
                LoopSceneGui.Muted);

            Rect modelRect = new Rect(
                window.x + 18f,
                window.y + 76f,
                window.width * 0.42f,
                window.height - 112f);
            DrawInventoryCellSurface(modelRect);
            Texture modelTexture = null;
            if (previewRenderer != null)
            {
                modelTexture = inspectedLootWeaponData.WeaponDefinitionId ==
                    ItemDefinitionIds.LootShortSword
                        ? previewRenderer.RenderLootShortSword(
                            inspectedLootWeaponData.VisualSeed)
                        : previewRenderer.RenderLootHuntingBow();
            }
            if (modelTexture != null)
            {
                GUI.DrawTexture(
                    new Rect(
                        modelRect.x + 7f,
                        modelRect.y + 7f,
                        modelRect.width - 14f,
                        modelRect.height - 36f),
                    modelTexture,
                    ScaleMode.ScaleToFit,
                    true);
                HandlePreviewRotation(
                    modelRect,
                    delta => previewRenderer.RotateWeapon(delta));
            }
            else
            {
                GUI.Label(modelRect, inspectedLootWeaponData.WeaponDefinitionId ==
                    ItemDefinitionIds.LootShortSword ? "SHORT\nSWORD" : "HUNTING\nBOW", cellStyle);
            }
            GUI.Label(
                new Rect(modelRect.x + 8f, modelRect.yMax - 28f, modelRect.width - 16f, 20f),
                "DRAG TO ROTATE",
                LoopSceneGui.Muted);

            Rect gridRect = new Rect(
                modelRect.xMax + 22f,
                window.y + 76f,
                window.xMax - modelRect.xMax - 40f,
                window.height - 112f);
            GUI.Label(gridRect, "WEAPON GRID", LoopSceneGui.Heading);
            DrawLootWeaponGrid(new Rect(gridRect.x, gridRect.y + 30f, gridRect.width, gridRect.height - 30f));
        }

        private void DrawWeaponContextMenu()
        {
            Rect menu = weaponContextMenuRect;
            LoopSceneGui.DrawPanel(menu, new Color(0.20f, 0.25f, 0.26f));
            if (GUI.Button(new Rect(menu.x + 8f, menu.y + 7f, menu.width - 16f, 28f), "INSPECT"))
            {
                OpenLootWeaponInspector(weaponContextEntry);
                weaponContextEntry = null;
                weaponContextMenuRect = default;
                return;
            }
            if (GUI.Button(new Rect(menu.x + 8f, menu.y + 42f, menu.width - 16f, 28f), "DISCARD"))
            {
                if (TryTakeEntry(
                        weaponContextGrid,
                        weaponContextEntry,
                        weaponContextEntry.Quantity,
                        out _))
                {
                    statusMessage = "Weapon discarded.";
                    PersistHomeTransaction();
                }
                weaponContextEntry = null;
                weaponContextMenuRect = default;
            }
        }

        private void DrawHeldPlacementPreview(
            IReadOnlyList<Rect> cellRects,
            IReadOnlyList<StorageEntry> entries,
            int hoveredSlot,
            int columns,
            int rows)
        {
            int anchor = CalculateHeldAnchorSlot(hoveredSlot, columns);
            IReadOnlyList<Vector2Int> footprint = ItemDefinitionCatalog.GetFootprint(
                heldEntry.DefinitionId,
                heldEntry.RotationQuarterTurns);
            bool fits = ItemGridPlacement.CanPlace(
                entries,
                heldEntry,
                anchor,
                columns,
                rows);
            Color previous = GUI.color;
            GUI.color = fits
                ? new Color(0.76f, 0.87f, 0.63f, 0.28f)
                : new Color(0.88f, 0.34f, 0.26f, 0.32f);
            for (int index = 0; index < footprint.Count; index++)
            {
                int column = anchor % columns + footprint[index].x;
                int row = anchor / columns + footprint[index].y;
                if (column < 0 || column >= columns || row < 0 || row >= rows)
                {
                    continue;
                }
                GUI.DrawTexture(
                    cellRects[row * columns + column],
                    Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private void DrawLootWeaponGrid(Rect area)
        {
            if (inspectedLootWeaponGrid == null)
            {
                GUI.Label(area, "GRID DATA UNAVAILABLE", LoopSceneGui.Muted);
                return;
            }

            const float cellSize = 42f;
            IReadOnlyList<GridCoordinate> cells = inspectedLootWeaponGrid.UnlockedCells;
            for (int index = 0; index < cells.Count; index++)
            {
                GridCoordinate cell = cells[index];
                Rect rect = new Rect(
                    area.center.x + cell.X * (cellSize + 2f) - cellSize * 0.5f,
                    area.center.y - cell.Y * (cellSize + 2f) - cellSize * 0.5f,
                    cellSize,
                    cellSize);
                LoopSceneGui.DrawWeaponGridCell(rect);
            }
        }

        private void PickUp(
            InventoryGridKind gridKind,
            StorageEntry entry,
            int quantity,
            float cellSize,
            int clickedSlot,
            int columns)
        {
            bool pickedUp = TryTakeEntry(
                gridKind,
                entry,
                quantity,
                out heldEntry);
            if (!pickedUp)
            {
                heldEntry = null;
                statusMessage = "That item is no longer available.";
                return;
            }

            heldOrigin = gridKind;
            heldLootSource = gridKind == InventoryGridKind.Loot
                ? activeRaidLoot
                : null;
            heldOriginSlot = entry.SlotIndex;
            heldGrabOffset = new Vector2Int(
                clickedSlot % columns - entry.SlotIndex % columns,
                clickedSlot / columns - entry.SlotIndex / columns);
            heldCellSize = cellSize;
            PersistHomeTransaction();
        }

        private int PlaceHeld(
            InventoryGridKind target,
            int targetSlot,
            int requestedQuantity)
        {
            if (heldEntry == null)
            {
                return 0;
            }
            if (requestedQuantity <= 0)
            {
                return 0;
            }

            int targetColumns = target == InventoryGridKind.Player
                ? PackColumns
                : target == InventoryGridKind.Chest
                    ? ChestColumns
                    : target == InventoryGridKind.Secure
                        ? SecureColumns
                : activeRaidLoot != null
                    ? activeRaidLoot.Columns
                    : PackColumns;
            int anchorSlot = CalculateHeldAnchorSlot(
                targetSlot,
                targetColumns);
            int amount = Mathf.Min(requestedQuantity, heldEntry.Quantity);
            StorageEntry portion = heldEntry.CreateSplitCopy(amount);
            int moved = TryAddEntry(
                target,
                portion,
                anchorSlot,
                false);
            if (moved <= 0)
            {
                statusMessage = "That slot cannot accept this item.";
                return 0;
            }

            heldEntry.RemoveQuantity(moved);
            statusMessage = moved == 1 && requestedQuantity == 1
                ? "Placed one item."
                : $"Placed {moved} item{(moved == 1 ? string.Empty : "s")}.";
            if (heldEntry.Quantity <= 0)
            {
                ClearHeldItem();
            }
            PersistHomeTransaction();
            return moved;
        }

        private bool CanPaintHeldStack()
        {
            return heldEntry != null &&
                heldEntry.Quantity > 1 &&
                ItemDefinitionCatalog.IsStackable(
                    heldEntry.DefinitionId) &&
                ItemDefinitionCatalog.GetFootprint(
                    heldEntry.DefinitionId,
                    heldEntry.RotationQuarterTurns).Count == 1;
        }

        private void BeginStackPaint(StackPaintMode mode)
        {
            stackPaintMode = mode;
            stackPaintCells.Clear();
            leftPressPickedUpItem = false;
        }

        private void PaintHeldStackAcrossCell(
            InventoryGridKind grid,
            int slot)
        {
            if (heldEntry == null ||
                FindStackPaintCell(grid, slot) >= 0 ||
                (stackPaintMode == StackPaintMode.Evenly &&
                 !CanAddEvenDistributionCell(
                     heldEntry.Quantity,
                     stackPaintCells.Count)))
            {
                return;
            }

            stackPaintCells.Add(new StackPaintCell(grid, slot));
            if (stackPaintMode == StackPaintMode.OnePerCell)
            {
                PlaceHeld(grid, slot, 1);
            }
        }

        private void FinishStackPaint()
        {
            StackPaintMode completedMode = stackPaintMode;
            StackPaintCell[] painted = stackPaintCells.ToArray();
            int startingQuantity = heldEntry != null
                ? heldEntry.Quantity
                : 0;
            stackPaintMode = StackPaintMode.None;
            stackPaintCells.Clear();

            if (completedMode == StackPaintMode.Evenly)
            {
                for (int index = 0;
                     index < painted.Length && heldEntry != null;
                     index++)
                {
                    int requested = CalculateEvenDistributionAmount(
                        startingQuantity,
                        painted.Length,
                        index);
                    PlaceHeld(
                        painted[index].Grid,
                        painted[index].Slot,
                        requested);
                }
            }
            leftPressPickedUpItem = false;
        }

        private int FindStackPaintCell(
            InventoryGridKind grid,
            int slot)
        {
            for (int index = 0;
                 index < stackPaintCells.Count;
                 index++)
            {
                if (stackPaintCells[index].Grid == grid &&
                    stackPaintCells[index].Slot == slot)
                {
                    return index;
                }
            }
            return -1;
        }

        public static int CalculateEvenDistributionAmount(
            int totalQuantity,
            int cellCount,
            int cellIndex)
        {
            if (totalQuantity <= 0 ||
                cellCount <= 0 ||
                cellIndex < 0 ||
                cellIndex >= cellCount)
            {
                return 0;
            }

            int baseAmount = totalQuantity / cellCount;
            int remainder = totalQuantity % cellCount;
            return baseAmount + (cellIndex < remainder ? 1 : 0);
        }

        public static bool CanAddEvenDistributionCell(
            int heldQuantity,
            int selectedCellCount)
        {
            return heldQuantity > 0 &&
                selectedCellCount >= 0 &&
                selectedCellCount < heldQuantity;
        }

        private void ShiftTransfer(
            InventoryGridKind sourceKind,
            StorageEntry entry)
        {
            InventoryGridKind destination =
                sourceKind == InventoryGridKind.Player
                    ? chestOpen
                        ? InventoryGridKind.Chest
                        : activeRaidLoot != null
                            ? InventoryGridKind.Loot
                            : InventoryGridKind.Secure
                    : InventoryGridKind.Player;
            if (destination == InventoryGridKind.Passive)
            {
                statusMessage = "Open a chest or loot source to transfer items.";
                return;
            }

            StorageEntry moving;
            bool taken = TryTakeEntry(
                sourceKind,
                entry,
                entry.Quantity,
                out moving);
            if (!taken)
            {
                statusMessage = "That item is no longer available.";
                return;
            }

            int moved = TryAddEntry(destination, moving, -1, true);
            if (moved < moving.Quantity)
            {
                StorageEntry remainder = moving.CreateSplitCopy(
                    moving.Quantity - moved);
                int returned = TryAddEntry(
                    sourceKind,
                    remainder,
                    entry.SlotIndex,
                    false);
                if (returned < remainder.Quantity)
                {
                    TryAddEntry(sourceKind, remainder, -1, true);
                }
            }

            string itemName = ItemDefinitionCatalog.DisplayName(
                moving.DefinitionId);
            statusMessage = moved > 0
                ? $"Moved {moved} {itemName} with smart stacking."
                : "The destination has no room for that item.";
            PersistHomeTransaction();
        }

        private RaidPrototypeController ResolveRaidController()
        {
            return FindFirstObjectByType<RaidPrototypeController>();
        }

        private bool ReturnHeldItem()
        {
            if (heldEntry == null)
            {
                return true;
            }
            RaidLootContainer currentLoot = activeRaidLoot;
            if (heldOrigin == InventoryGridKind.Loot)
            {
                activeRaidLoot = heldLootSource;
            }
            int returned = TryAddEntry(
                heldOrigin,
                heldEntry,
                heldOriginSlot,
                false);
            if (returned < heldEntry.Quantity)
            {
                StorageEntry remainder = heldEntry.CreateSplitCopy(
                    heldEntry.Quantity - returned);
                returned += TryAddEntry(
                    heldOrigin,
                    remainder,
                    -1,
                    true);
            }
            activeRaidLoot = currentLoot;
            if (returned < heldEntry.Quantity)
            {
                return false;
            }
            ClearHeldItem();
            PersistHomeTransaction();
            return true;
        }

        private bool TryTakeEntry(
            InventoryGridKind source,
            StorageEntry entry,
            int quantity,
            out StorageEntry taken)
        {
            taken = null;
            RaidPrototypeController raid = ResolveRaidController();
            if (raid != null && raid.RaidActive &&
                (source == InventoryGridKind.Player ||
                 source == InventoryGridKind.Loot))
            {
                return source == InventoryGridKind.Player
                    ? raid.TryTakeInventoryEntry(
                        entry,
                        quantity,
                        out taken)
                    : source == InventoryGridKind.Loot &&
                        raid.TryTakeLootEntry(
                            activeRaidLoot,
                            entry,
                            quantity,
                            out taken);
            }

            PlayerProfile profile = ResolveProfile();
            return source == InventoryGridKind.Player
                ? ProfileInventoryTransactions.TryTakeInventory(
                    profile,
                    entry,
                    quantity,
                    out taken)
                : source == InventoryGridKind.Chest &&
                    ProfileInventoryTransactions.TryTakeChest(
                        profile,
                        activeChestId,
                        entry,
                        quantity,
                        out taken) ||
                    source == InventoryGridKind.Secure &&
                    ProfileInventoryTransactions.TryTakeSecure(
                        profile,
                        entry,
                        quantity,
                        out taken);
        }

        private int TryAddEntry(
            InventoryGridKind target,
            StorageEntry entry,
            int targetSlot,
            bool autoStack)
        {
            RaidPrototypeController raid = ResolveRaidController();
            if (raid != null && raid.RaidActive &&
                (target == InventoryGridKind.Player ||
                 target == InventoryGridKind.Loot))
            {
                return target == InventoryGridKind.Player
                    ? raid.TryPlaceInInventory(entry, targetSlot, autoStack)
                    : target == InventoryGridKind.Loot
                        ? raid.TryPlaceInLoot(
                            activeRaidLoot,
                            entry,
                            targetSlot,
                            autoStack)
                        : 0;
            }

            PlayerProfile profile = ResolveProfile();
            return target == InventoryGridKind.Player
                ? ProfileInventoryTransactions.TryAddInventory(
                    profile,
                    entry,
                    targetSlot,
                    autoStack)
                : target == InventoryGridKind.Chest
                    ? ProfileInventoryTransactions.TryAddChest(
                        profile,
                        activeChestId,
                        entry,
                        targetSlot,
                        autoStack)
                    : target == InventoryGridKind.Secure
                        ? ProfileInventoryTransactions.TryAddSecure(
                            profile,
                            entry,
                            targetSlot,
                            autoStack)
                    : 0;
        }

        private void PersistHomeTransaction()
        {
            RaidPrototypeController raid = ResolveRaidController();
            if (raid == null || !raid.RaidActive)
            {
                Persist();
            }
        }

        private void ClearHeldItem()
        {
            heldEntry = null;
            heldOrigin = InventoryGridKind.Passive;
            heldLootSource = null;
            heldOriginSlot = -1;
            heldGrabOffset = Vector2Int.zero;
            heldCellSize = 0f;
            leftPressPickedUpItem = false;
            stackPaintMode = StackPaintMode.None;
            stackPaintCells.Clear();
        }

        private void RotateHeldItem()
        {
            if (heldEntry == null)
            {
                return;
            }
            IReadOnlyList<Vector2Int> currentFootprint =
                ItemDefinitionCatalog.GetFootprint(
                    heldEntry.DefinitionId,
                    heldEntry.RotationQuarterTurns);
            heldGrabOffset =
                ItemDefinitionCatalog.RotateFootprintOffsetClockwise(
                    currentFootprint,
                    heldGrabOffset);
            heldEntry.RotateClockwise();
            statusMessage =
                $"Rotated {ItemDefinitionCatalog.DisplayName(heldEntry.DefinitionId)} 90 degrees.";
        }

        private int CalculateHeldAnchorSlot(
            int hoveredSlot,
            int columns)
        {
            int targetColumn = hoveredSlot % columns -
                heldGrabOffset.x;
            int targetRow = hoveredSlot / columns -
                heldGrabOffset.y;
            return targetRow * columns + targetColumn;
        }

        private void DrawItem(
            Rect footprintRect,
            StorageEntry entry,
            float cellSize,
            int displayedQuantity = -1)
        {
            if (ItemDefinitionCatalog.IsWeapon(entry.DefinitionId))
            {
                DrawWeaponFootprintIcon(footprintRect, entry);
                return;
            }
            Texture2D icon = ItemDefinitionCatalog.LoadIcon(
                entry.DefinitionId);
            if (icon != null)
            {
                IReadOnlyList<Vector2Int> baseFootprint =
                    ItemDefinitionCatalog.GetFootprint(
                        entry.DefinitionId,
                        0);
                GetFootprintDimensions(
                    baseFootprint,
                    out int baseWidth,
                    out int baseHeight);
                Rect iconRect = new Rect(
                    footprintRect.center.x -
                        (baseWidth * cellSize +
                         Mathf.Max(0, baseWidth - 1) * StorageCellGap) * 0.5f,
                    footprintRect.center.y -
                        (baseHeight * cellSize +
                         Mathf.Max(0, baseHeight - 1) * StorageCellGap) * 0.5f,
                    baseWidth * cellSize +
                        Mathf.Max(0, baseWidth - 1) * StorageCellGap,
                    baseHeight * cellSize +
                        Mathf.Max(0, baseHeight - 1) * StorageCellGap);
                Matrix4x4 previousMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(
                    entry.RotationQuarterTurns * 90f,
                    footprintRect.center);
                GUI.DrawTexture(
                    new Rect(
                        iconRect.x + 3f,
                        iconRect.y + 3f,
                        iconRect.width - 6f,
                        iconRect.height - 6f),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
                GUI.matrix = previousMatrix;
            }
            else
            {
                GUI.Label(
                    footprintRect,
                    GetInitials(entry.DefinitionId),
                    cellStyle);
            }

            int quantity = displayedQuantity >= 0
                ? displayedQuantity
                : entry.Quantity;
            if (quantity > 1)
            {
                GUI.Label(
                    new Rect(
                        footprintRect.x + 3f,
                        footprintRect.y + 3f,
                        footprintRect.width - 7f,
                        footprintRect.height - 6f),
                    quantity.ToString(),
                    quantityStyle);
            }
        }

        private void DrawWeaponFootprintIcon(
            Rect rect,
            StorageEntry entry)
        {
            if (previewRenderer != null)
            {
                Texture preview = null;
                if (entry.DefinitionId == ItemDefinitionIds.LootShortSword &&
                    LootWeaponData.TryParse(entry.CustomStateJson, out LootWeaponData data))
                {
                    preview = previewRenderer.RenderLootShortSword(
                        data.VisualSeed,
                        entry.RotationQuarterTurns);
                }
                else
                {
                    preview = entry.DefinitionId == ItemDefinitionIds.LootHuntingBow
                        ? previewRenderer.RenderLootHuntingBow(
                            entry.RotationQuarterTurns)
                        : previewRenderer.WeaponTexture;
                }
                if (preview != null)
                {
                    GUI.DrawTexture(
                        new Rect(
                            rect.x + 3f,
                            rect.y + 3f,
                            rect.width - 6f,
                            rect.height - 6f),
                        preview,
                        ScaleMode.ScaleToFit,
                        true);
                    return;
                }
            }
            Color previous = GUI.color;
            bool sword = entry.DefinitionId == ItemDefinitionIds.LootShortSword;
            GUI.color = sword
                ? new Color(0.80f, 0.85f, 0.86f, 0.94f)
                : new Color(0.68f, 0.47f, 0.24f, 0.94f);
            if (sword)
            {
                float bladeWidth = Mathf.Max(4f, rect.width * 0.24f);
                GUI.DrawTexture(new Rect(
                    rect.center.x - bladeWidth * 0.5f,
                    rect.y + 7f,
                    bladeWidth,
                    rect.height * 0.66f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(
                    rect.x + rect.width * 0.17f,
                    rect.y + rect.height * 0.68f,
                    rect.width * 0.66f,
                    Mathf.Max(3f, rect.height * 0.045f)), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(
                    rect.center.x - bladeWidth * 0.32f,
                    rect.y + rect.height * 0.73f,
                    bladeWidth * 0.64f,
                    rect.height * 0.18f), Texture2D.blackTexture);
            }
            else
            {
                GUI.DrawTexture(new Rect(
                    rect.center.x - 2f,
                    rect.y + 6f,
                    4f,
                    rect.height - 12f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(
                    rect.x + rect.width * 0.16f,
                    rect.y + 8f,
                    3f,
                    rect.height - 16f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(
                    rect.x + rect.width * 0.84f - 3f,
                    rect.y + 8f,
                    3f,
                    rect.height - 16f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private static void DrawInventorySection(Rect area)
        {
            LoopSceneGui.DrawSection(area);
        }

        private void DrawInventoryCellSurface(Rect rect)
        {
            LoopSceneGui.DrawCell(rect);
        }

        private List<StorageEntry> BuildPackEntries(PlayerProfile profile)
        {
            var entries = new List<StorageEntry>(profile.InventoryEntryIds.Count);
            for (int index = 0; index < profile.InventoryEntryIds.Count; index++)
            {
                StorageEntry entry = profile.FindStorageEntry(
                    profile.InventoryEntryIds[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            GameplayLoopBootstrap bootstrap =
                GameplaySceneRuntime.ResolveBootstrap();
            RaidSession raid = bootstrap != null &&
                bootstrap.Session != null
                    ? bootstrap.Session.ActiveRaid
                    : null;
            if (raid != null && raid.IsActive)
            {
                for (int index = 0;
                     index < raid.CollectedStorageEntries.Count;
                     index++)
                {
                    StorageEntry entry = raid.CollectedStorageEntries[index];
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }

            }
            return entries;
        }

        private static List<StorageEntry> BuildSecureEntries(
            PlayerProfile profile)
        {
            var entries = new List<StorageEntry>(
                profile.SecureEntryIds.Count);
            for (int index = 0;
                 index < profile.SecureEntryIds.Count;
                 index++)
            {
                StorageEntry entry = profile.FindStorageEntry(
                    profile.SecureEntryIds[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private static StorageEntry[] BuildSlotMap(
            IReadOnlyList<StorageEntry> entries,
            int columns,
            int rows)
        {
            int capacity = columns * rows;
            var slots = new StorageEntry[capacity];
            for (int slot = 0; slot < capacity; slot++)
            {
                slots[slot] = ItemGridPlacement.GetEntryAtSlot(
                    entries,
                    slot,
                    columns,
                    rows);
            }
            return slots;
        }

        private static bool TryGetItemRect(
            StorageEntry entry,
            int columns,
            int rows,
            IReadOnlyList<Rect> cellRects,
            out Rect itemRect)
        {
            itemRect = default;
            IReadOnlyList<Vector2Int> footprint =
                ItemDefinitionCatalog.GetFootprint(
                    entry.DefinitionId,
                    entry.RotationQuarterTurns);
            if (!ItemGridPlacement.TryGetOccupiedSlots(
                    footprint,
                    entry.SlotIndex,
                    columns,
                    rows,
                    out int[] occupiedSlots))
            {
                return false;
            }
            Rect bounds = cellRects[occupiedSlots[0]];
            for (int index = 1; index < occupiedSlots.Length; index++)
            {
                Rect cell = cellRects[occupiedSlots[index]];
                bounds.xMin = Mathf.Min(bounds.xMin, cell.xMin);
                bounds.yMin = Mathf.Min(bounds.yMin, cell.yMin);
                bounds.xMax = Mathf.Max(bounds.xMax, cell.xMax);
                bounds.yMax = Mathf.Max(bounds.yMax, cell.yMax);
            }
            itemRect = bounds;
            return true;
        }

        private static void GetFootprintDimensions(
            IReadOnlyList<Vector2Int> footprint,
            out int width,
            out int height)
        {
            width = 1;
            height = 1;
            for (int index = 0; index < footprint.Count; index++)
            {
                width = Mathf.Max(width, footprint[index].x + 1);
                height = Mathf.Max(height, footprint[index].y + 1);
            }
        }

        private static List<StorageEntry> BuildChestEntries(
            PlayerProfile profile,
            string chestId)
        {
            var entries = new List<StorageEntry>();
            IReadOnlyList<string> entryIds = profile.GetChestEntryIds(chestId);
            for (int index = 0; index < entryIds.Count; index++)
            {
                StorageEntry entry = profile.FindStorageEntry(entryIds[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private PlayerProfile ResolveProfile()
        {
            if (homeBase != null && homeBase.Profile != null)
            {
                return homeBase.Profile;
            }
            GameplayLoopBootstrap bootstrap = GameplaySceneRuntime.ResolveBootstrap();
            return bootstrap != null && bootstrap.Session != null
                ? bootstrap.Session.ActiveProfile
                : null;
        }

        private void Open()
        {
            if (isOpen)
            {
                return;
            }
            if (playerInput == null)
            {
                GameObject playerObject =
                    GameObject.FindGameObjectWithTag("Player");
                playerInput = playerObject != null
                    ? playerObject.GetComponent<PlayerInputSource>()
                    : null;
            }
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (playerInput != null)
            {
                previousInputCapture = playerInput.UserInterfaceCaptureActive;
                playerInput.SetUserInterfaceCapture(true);
            }
            previewRenderer ??=
                GetComponent<InventoryPreviewRenderer>() ??
                gameObject.AddComponent<InventoryPreviewRenderer>();
            previewRenderer.Configure(
                playerInput != null ? playerInput.transform : null,
                rebuild: true);
            previewRenderer.ResetCharacterView();
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
            if (!ReturnHeldItem())
            {
                statusMessage =
                    "Place the held item before closing the inventory.";
                return;
            }
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                gridToolkit.Close();
            }
            Persist();
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (playerInput != null)
            {
                playerInput.SetUserInterfaceCapture(previousInputCapture);
                if (!previousInputCapture &&
                    previousCursorLock == CursorLockMode.Locked)
                {
                    playerInput.RequestGameplayCursorCapture();
                }
            }
            chestOpen = false;
            activeRaidLoot = null;
            ClearHeldItem();
            activeChestId = PlayerProfile.DefaultChestId;
            activeChestName = "CHEST 1";
            isOpen = false;
        }

        private void Persist()
        {
            try
            {
                if (homeBase != null)
                {
                    homeBase.SaveProfile();
                }
                else
                {
                    GameplayLoopBootstrap bootstrap =
                        GameplaySceneRuntime.ResolveBootstrap();
                    bootstrap.Session?.SaveProfile();
                }
            }
            catch (Exception exception)
            {
                statusMessage = $"Could not save inventory: {exception.Message}";
            }
        }

        private void EnsureStyles()
        {
            GameTypography.ApplyToCurrentSkin();
            cellStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                normal = { textColor = new Color(0.92f, 0.91f, 0.83f) }
            };
            emptyCellStyle ??= new GUIStyle(GUI.skin.box)
            {
                normal = { background = GameTypography.CellTexture }
            };
            equipmentSlotStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.47f, 0.52f, 0.54f) }
            };
            equippedSlotStyle ??= new GUIStyle(equipmentSlotStyle)
            {
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.92f, 0.79f, 0.48f) }
            };
            weaponCardStyle ??= new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.clear }
            };
            centeredTitleStyle ??= new GUIStyle(LoopSceneGui.Title)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1f, 0.91f, 0.68f, 1f) }
            };
            slotLabelStyle ??= new GUIStyle(LoopSceneGui.Muted)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 10,
                normal = { textColor = Color.white }
            };
            quantityStyle ??= new GUIStyle(LoopSceneGui.Body)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };
        }

        private static void HandlePreviewRotation(Rect area, Action<float> rotate)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDrag &&
                current.button == 0 &&
                area.Contains(current.mousePosition))
            {
                rotate?.Invoke(-current.delta.x * 0.8f);
                current.Use();
            }
        }

        private static string GetInitials(string value)
        {
            string friendly = GameplaySceneRuntime.FriendlyId(value);
            string[] words = friendly.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return "?";
            }
            return words.Length == 1
                ? words[0].Substring(0, Mathf.Min(2, words[0].Length))
                    .ToUpperInvariant()
                : string.Concat(words[0][0], words[words.Length - 1][0])
                    .ToUpperInvariant();
        }
    }
}
