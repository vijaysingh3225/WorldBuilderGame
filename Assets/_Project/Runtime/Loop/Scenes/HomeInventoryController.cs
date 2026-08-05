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
        private const float StorageCellGap = 5f;

        private enum InventoryGridKind
        {
            Passive,
            Player,
            Loot
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
        private string activeChestId = PlayerProfile.DefaultChestId;
        private string activeChestName = "CHEST 1";
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousInputCapture;
        private Vector2 lootScrollPosition;
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                if (keyboard.tabKey.wasPressedThisFrame ||
                    keyboard.iKey.wasPressedThisFrame)
                {
                    Close();
                }
                return;
            }
            if (isOpen &&
                heldEntry != null &&
                keyboard.rKey.wasPressedThisFrame)
            {
                RotateHeldItem();
                return;
            }
            if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
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
            GUI.enabled = previousEnabled;
        }

        private void DrawInventoryScreen(Rect panel)
        {
            LoopSceneGui.DrawPanel(panel, new Color(0.43f, 0.52f, 0.48f));

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
            Rect characterArea = CalculateInventoryColumn(
                contentArea,
                0,
                sectionSpacing);
            Rect inventoryArea = CalculateInventoryColumn(
                contentArea,
                1,
                sectionSpacing);
            Rect lootArea = CalculateInventoryColumn(
                contentArea,
                2,
                sectionSpacing);
            float sharedCellSize = CalculateSharedStorageCellSize(
                inventoryArea.width,
                inventoryArea.height);
            DrawCharacterLoadout(characterArea);

            RaidPrototypeController raidController =
                ResolveRaidController();
            bool raidInventoryActive =
                raidController != null && raidController.RaidActive;
            IReadOnlyList<StorageEntry> packEntries = BuildPackEntries(profile);
            DrawContainer(
                inventoryArea,
                "EQUIPPED BACKPACK  /  4 x 6",
                packEntries,
                PackColumns,
                PackRows,
                sharedCellSize,
                false,
                entry =>
                {
                    if (!chestOpen)
                    {
                        statusMessage =
                            "Item rearranging will use spatial shapes in a later pass.";
                        return;
                    }
                    statusMessage = profile.MoveToChest(
                        entry.EntryId,
                        activeChestId)
                        ? $"{GameplaySceneRuntime.FriendlyId(entry.DefinitionId)} moved to {activeChestName.ToLowerInvariant()}."
                        : $"{activeChestName} is full.";
                    Persist();
                },
                raidInventoryActive
                    ? InventoryGridKind.Player
                    : InventoryGridKind.Passive);

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
                    InventoryGridKind.Passive);
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
            else
            {
                DrawInventorySection(lootArea);
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
            const float scrollBarAllowance = 14f;
            float widthLimit =
                (columnWidth - horizontalPadding - scrollBarAllowance -
                    StorageCellGap * (ChestColumns - 1)) /
                ChestColumns;
            float heightLimit =
                (columnHeight - verticalPadding -
                    StorageCellGap * (PackRows - 1)) /
                PackRows;
            return Mathf.Max(
                18f,
                Mathf.Floor(Mathf.Min(widthLimit, heightLimit)));
        }

        private void DrawCharacterLoadout(Rect area)
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
                previewRenderer != null ? previewRenderer.PrimaryThumbnail : null);
            DrawWeaponCard(
                new Rect(weaponRow.x + cardWidth + 10f, weaponRow.y, cardWidth, weaponRow.height),
                1,
                "SECONDARY  /  2",
                previewRenderer != null ? previewRenderer.SecondaryThumbnail : null);
        }

        private void DrawEquipmentSlot(Rect rect, string label, bool equipped)
        {
            GUI.Box(
                rect,
                equipped ? "PACK\n4 x 6" : string.Empty,
                equipped ? equippedSlotStyle : equipmentSlotStyle);
            GUI.Label(
                new Rect(rect.x, rect.y - 18f, rect.width, 17f),
                label,
                slotLabelStyle);
        }

        private void DrawWeaponCard(
            Rect rect,
            int weaponIndex,
            string label,
            Texture thumbnail)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.18f, 0.19f, 0.20f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.40f, 0.42f, 0.43f, 1f);
            const float border = 2f;
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width, border),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(rect.x, rect.yMax - border, rect.width, border),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, border, rect.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(rect.xMax - border, rect.y, border, rect.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (GUI.Button(rect, GUIContent.none, weaponCardStyle))
            {
                OpenWeaponGrid(weaponIndex);
                return;
            }
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f),
                label,
                LoopSceneGui.Muted);
            if (thumbnail != null)
            {
                GUI.DrawTexture(
                    new Rect(rect.x + 8f, rect.y + 25f, rect.width - 16f, rect.height - 31f),
                    thumbnail,
                    ScaleMode.ScaleToFit,
                    true);
            }
        }

        private void OpenWeaponGrid(int weaponIndex)
        {
            if (gridToolkit == null)
            {
                return;
            }
            gridToolkit.OpenWeapon(weaponIndex);
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
                        (needsVerticalScroll ? 14f : 0f));
                Rect scrollContent = new Rect(
                    0f,
                    0f,
                    viewWidth,
                    Mathf.Max(boardHeight, viewport.height));
                lootScrollPosition = GUI.BeginScrollView(
                    viewport,
                    lootScrollPosition,
                    scrollContent,
                    false,
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

                GUI.Box(cell, GUIContent.none, emptyCellStyle);
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
            if (scrollable)
            {
                GUI.EndScrollView();
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
                Event.current.button == 0)
            {
                leftPressPickedUpItem = false;
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
            RaidPrototypeController raid = ResolveRaidController();
            if (raid == null)
            {
                statusMessage = "Raid inventory is not ready.";
                return;
            }

            bool pickedUp = gridKind == InventoryGridKind.Player
                ? raid.TryTakeInventoryEntry(
                    entry,
                    quantity,
                    out heldEntry)
                : raid.TryTakeLootEntry(
                    activeRaidLoot,
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
        }

        private void PlaceHeld(
            InventoryGridKind target,
            int targetSlot,
            int requestedQuantity)
        {
            RaidPrototypeController raid = ResolveRaidController();
            if (raid == null || heldEntry == null)
            {
                return;
            }

            int targetColumns = target == InventoryGridKind.Player
                ? PackColumns
                : activeRaidLoot != null
                    ? activeRaidLoot.Columns
                    : PackColumns;
            int anchorSlot = CalculateHeldAnchorSlot(
                targetSlot,
                targetColumns);
            int amount = Mathf.Min(requestedQuantity, heldEntry.Quantity);
            StorageEntry portion = heldEntry.CreateSplitCopy(amount);
            int moved = target == InventoryGridKind.Player
                ? raid.TryPlaceInInventory(portion, anchorSlot, false)
                : raid.TryPlaceInLoot(
                    activeRaidLoot,
                    portion,
                    anchorSlot,
                    false);
            if (moved <= 0)
            {
                statusMessage = "That slot cannot accept this item.";
                return;
            }

            heldEntry.RemoveQuantity(moved);
            statusMessage = moved == 1 && requestedQuantity == 1
                ? "Placed one item."
                : $"Placed {moved} item{(moved == 1 ? string.Empty : "s")}.";
            if (heldEntry.Quantity <= 0)
            {
                ClearHeldItem();
            }
        }

        private void ShiftTransfer(
            InventoryGridKind sourceKind,
            StorageEntry entry)
        {
            RaidPrototypeController raid = ResolveRaidController();
            if (raid == null)
            {
                statusMessage = "Raid inventory is not ready.";
                return;
            }

            StorageEntry moving;
            bool taken = sourceKind == InventoryGridKind.Player
                ? raid.TryTakeInventoryEntry(
                    entry,
                    entry.Quantity,
                    out moving)
                : raid.TryTakeLootEntry(
                    activeRaidLoot,
                    entry,
                    entry.Quantity,
                    out moving);
            if (!taken)
            {
                statusMessage = "That item is no longer available.";
                return;
            }

            int moved = sourceKind == InventoryGridKind.Player
                ? raid.TryPlaceInLoot(activeRaidLoot, moving, -1, true)
                : raid.TryPlaceInInventory(moving, -1, true);
            if (moved < moving.Quantity)
            {
                StorageEntry remainder = moving.CreateSplitCopy(
                    moving.Quantity - moved);
                int returned = sourceKind == InventoryGridKind.Player
                    ? raid.TryPlaceInInventory(
                        remainder,
                        entry.SlotIndex,
                        false)
                    : raid.TryPlaceInLoot(
                        activeRaidLoot,
                        remainder,
                        entry.SlotIndex,
                        false);
                if (returned < remainder.Quantity)
                {
                    if (sourceKind == InventoryGridKind.Player)
                    {
                        raid.TryPlaceInInventory(remainder, -1, true);
                    }
                    else
                    {
                        raid.TryPlaceInLoot(
                            activeRaidLoot,
                            remainder,
                            -1,
                            true);
                    }
                }
            }

            string itemName = ItemDefinitionCatalog.DisplayName(
                moving.DefinitionId);
            statusMessage = moved > 0
                ? $"Moved {moved} {itemName} with smart stacking."
                : "The destination has no room for that item.";
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
            RaidPrototypeController raid = ResolveRaidController();
            if (raid == null)
            {
                return false;
            }

            int returned = heldOrigin == InventoryGridKind.Player
                ? raid.TryPlaceInInventory(
                    heldEntry,
                    heldOriginSlot,
                    false)
                : raid.TryPlaceInLoot(
                    heldLootSource,
                    heldEntry,
                    heldOriginSlot,
                    false);
            if (returned < heldEntry.Quantity)
            {
                StorageEntry remainder = heldEntry.CreateSplitCopy(
                    heldEntry.Quantity - returned);
                returned += heldOrigin == InventoryGridKind.Player
                    ? raid.TryPlaceInInventory(remainder, -1, true)
                    : raid.TryPlaceInLoot(
                        heldLootSource,
                        remainder,
                        -1,
                        true);
            }
            if (returned < heldEntry.Quantity)
            {
                return false;
            }
            ClearHeldItem();
            return true;
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
            float cellSize)
        {
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

            if (entry.Quantity > 1)
            {
                GUI.Label(
                    new Rect(
                        footprintRect.x + 3f,
                        footprintRect.y + 3f,
                        footprintRect.width - 7f,
                        footprintRect.height - 6f),
                    entry.Quantity.ToString(),
                    quantityStyle);
            }
        }

        private static void DrawInventorySection(Rect area)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.24f, 0.25f, 0.27f, 0.98f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);

            GUI.color = new Color(0.56f, 0.59f, 0.61f, 1f);
            const float border = 2f;
            GUI.DrawTexture(
                new Rect(area.x, area.y, area.width, border),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(area.x, area.yMax - border, area.width, border),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(area.x, area.y, border, area.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(area.xMax - border, area.y, border, area.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
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
                playerInput != null ? playerInput.transform : null);
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
                normal = { background = Texture2D.grayTexture }
            };
            equipmentSlotStyle ??= new GUIStyle(GUI.skin.box)
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
