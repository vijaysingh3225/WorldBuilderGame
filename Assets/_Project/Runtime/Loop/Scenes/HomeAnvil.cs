using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HomeAnvil : MonoBehaviour
    {
        private enum ArtifactSortMode
        {
            Function,
            Name,
            Size,
            Source
        }

        private readonly struct AvailableArtifact
        {
            public AvailableArtifact(
                StorageEntry entry,
                string source,
                string function,
                int size)
            {
                Entry = entry;
                definitionId = entry.DefinitionId;
                Source = source;
                Function = function;
                Size = size;
            }

            public AvailableArtifact(
                string artifactDefinitionId,
                string source,
                string function,
                int size)
            {
                Entry = null;
                definitionId = artifactDefinitionId;
                Source = source;
                Function = function;
                Size = size;
            }

            public StorageEntry Entry { get; }
            public string DefinitionId => definitionId;
            public string Source { get; }
            public string Function { get; }
            public int Size { get; }

            private readonly string definitionId;
        }

        private const float InteractionDistance = 4.2f;
        public const float ForgeCellSize = 52f;
        public const float MinimumGridZoom = 0.65f;
        public const float MaximumGridZoom = 1.8f;
        public const float ArtifactLibraryWidthFraction = 0.24f;
        private const float ForgeHeaderHeight = 58f;
        private const float ForgeFooterHeight = 24f;
        private readonly RaycastHit[] focusHits = new RaycastHit[24];
        private readonly Dictionary<GridCoordinate, Rect> gridRects =
            new Dictionary<GridCoordinate, Rect>();
        private readonly Vector2[] weaponGridPan =
            { Vector2.zero, Vector2.zero };
        private readonly float[] weaponGridZoom = { 1f, 1f };
        private readonly Vector2[] weaponDetailsScroll =
            { Vector2.zero, Vector2.zero };

        [SerializeField] private HomeBaseController homeBase;
        [SerializeField] private HomeInventoryController inventory;
        [SerializeField] private PlayerInputSource playerInput;
        [SerializeField] private WeaponGridRuntime weaponGrid;
        [SerializeField] private InventoryPreviewRenderer previewRenderer;
        [SerializeField] private bool unlimitedArtifactCatalog;

        private Transform player;
        private bool isOpen;
        private int weaponIndex;
        private string heldEntryId;
        private string heldDefinitionId;
        private int heldRotation;
        private string status = "Drag an artifact into an unlocked weapon cell.";
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool previousInputCapture;
        private Vector2 libraryScroll;
        private string searchText = string.Empty;
        private ArtifactSortMode sortMode = ArtifactSortMode.Function;
        private GUIStyle artifactStyle;
        private GUIStyle cellStyle;
        private GUIStyle statStyle;
        private GUIStyle centeredTitleStyle;
        private GUIStyle slotLabelStyle;
        private MeleeWeapon meleeWeapon;
        private BowWeapon bowWeapon;
        private bool gridPanning;
        private int previewWeaponIndex = -1;

        public bool IsOpen => isOpen;
        public bool UsesUnlimitedArtifactCatalog =>
            unlimitedArtifactCatalog;
        public int UnlimitedArtifactDefinitionCount =>
            unlimitedArtifactCatalog && weaponGrid != null
                ? weaponGrid.Definitions.Count
                : 0;
        public string AdjacentChestId => ResolveAdjacentChest()?.ChestId;

        public void Configure(
            HomeBaseController controller,
            HomeInventoryController inventoryController,
            PlayerInputSource input,
            WeaponGridRuntime runtime)
        {
            homeBase = controller;
            inventory = inventoryController;
            playerInput = input;
            weaponGrid = runtime;
        }

        public void ConfigureUnlimitedArtifactCatalog(
            PlayerInputSource input,
            WeaponGridRuntime runtime)
        {
            playerInput = input;
            weaponGrid = runtime;
            unlimitedArtifactCatalog = true;
            status =
                "Every artifact is available in unlimited quantities.";
        }

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void Update()
        {
            ResolveDependencies();
            Keyboard keyboard = Keyboard.current;
            if (isOpen)
            {
                if (keyboard != null &&
                    PlayerControlBindings.WasPressedThisFrame(
                        keyboard,
                        PlayerControl.Pause))
                {
                    Close();
                }
                if (keyboard != null &&
                    (heldEntryId != null || heldDefinitionId != null) &&
                    PlayerControlBindings.WasPressedThisFrame(
                        keyboard,
                        PlayerControl.RotateInventoryItem))
                {
                    heldRotation = GridCoordinate.NormalizeRotation(
                        heldRotation + 1);
                }
                return;
            }

            if (keyboard != null && CanInteract() &&
                (inventory == null || !inventory.IsOpen) &&
                PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Interact))
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
                if (Event.current.type == EventType.Repaint && CanInteract() &&
                    (inventory == null || !inventory.IsOpen))
                {
                    LootInteractionPresentation.DrawPrompt("Use Anvil");
                }
                return;
            }

            EnsureStyles();
            Rect panel = HomeInventoryController.CalculatePanelRect(
                Screen.width,
                Screen.height);
            LoopSceneGui.DrawPanel(panel, new Color(0.43f, 0.52f, 0.48f));
            DrawHeader(panel);

            float spacing = HomeInventoryController.
                CalculateInventorySectionSpacing(panel.width);
            float gap = spacing * 0.25f;
            Rect content = CalculateForgeContentRect(panel);
            float libraryWidth = CalculateArtifactLibraryWidth(content.width);
            Rect workspace = new Rect(
                content.x,
                content.y,
                Mathf.Max(0f, content.width - libraryWidth - gap),
                content.height);
            Rect library = new Rect(
                workspace.xMax + gap,
                content.y,
                libraryWidth,
                content.height);
            PlayerProfile profile = ResolveProfile();
            DrawWeaponWorkspace(workspace, profile);
            DrawArtifactLibrary(library, profile, ResolveAdjacentChest());

            GUI.Label(
                new Rect(
                    panel.x + spacing,
                    panel.yMax - CalculateForgeVerticalMargin(panel.height) - 18f,
                    panel.width - spacing * 2f,
                    18f),
                status,
                LoopSceneGui.Muted);
            DrawHeldArtifact(profile);
        }

        private void DrawHeader(Rect panel)
        {
            float spacing = HomeInventoryController.
                CalculateInventorySectionSpacing(panel.width);
            float top = panel.y +
                CalculateForgeVerticalMargin(panel.height);
            GUI.Label(
                new Rect(
                    panel.x + spacing,
                    top,
                    panel.width - spacing * 2f,
                    34f),
                "WEAPON FORGE  /  ARTIFACT ANVIL",
                centeredTitleStyle);
            GUI.Label(
                new Rect(
                    panel.x + spacing,
                    top + 30f,
                    panel.width - spacing * 2f - 42f,
                    20f),
                "Drag an artifact into the weapon grid  |  R rotates  |  right-click removes  |  Esc closes",
                LoopSceneGui.Muted);
            if (LoopSceneGui.DrawMinimalCloseButton(
                    new Rect(
                        panel.xMax - spacing - 26f,
                        top,
                        26f,
                        26f)))
            {
                Close();
            }
        }

        private void DrawWeaponWorkspace(Rect rect, PlayerProfile profile)
        {
            DrawInventorySection(rect);
            if (weaponGrid == null || profile == null)
            {
                return;
            }
            if (GUI.Button(
                    new Rect(rect.x + 16f, rect.y + 12f, 118f, 30f),
                    "1  SHORT SWORD",
                    LoopSceneGui.Button))
            {
                weaponIndex = 0;
            }
            if (GUI.Button(
                    new Rect(rect.x + 140f, rect.y + 12f, 92f, 30f),
                    "2  BOW",
                    LoopSceneGui.Button))
            {
                weaponIndex = 1;
            }
            SelectPreviewWeapon(weaponIndex);
            WeaponInstanceRecord weapon = profile.GetWeapon(weaponIndex + 1);
            GUI.Label(
                new Rect(rect.x + 250f, rect.y + 14f, rect.width - 266f, 26f),
                weapon.DisplayName.ToUpperInvariant(),
                LoopSceneGui.Heading);
            Rect body = new Rect(
                rect.x + 14f,
                rect.y + 52f,
                rect.width - 28f,
                rect.height - 66f);
            float sideWidth = Mathf.Clamp(body.width * 0.27f, 220f, 286f);
            Rect sidePanel = new Rect(
                body.x,
                body.y,
                sideWidth,
                body.height);
            Rect gridPanel = new Rect(
                sidePanel.xMax + 10f,
                body.y,
                body.width - sideWidth - 10f,
                body.height);
            DrawInventorySection(sidePanel);
            DrawInventorySection(gridPanel);
            WeaponGridState selectedState =
                weaponGrid.Loadout.GetWeapon(weaponIndex);
            IReadOnlyList<string> completedPatterns =
                ArtifactPatternResolver.ResolveCompleted(
                    selectedState,
                    weaponGrid.Definitions);
            float previewHeight = Mathf.Clamp(
                sidePanel.height * 0.43f,
                170f,
                260f);
            Rect previewPanel = new Rect(
                sidePanel.x + 3f,
                sidePanel.y + 3f,
                sidePanel.width - 6f,
                previewHeight);
            DrawWeaponPreview(previewPanel);
            Rect detailsViewport = new Rect(
                sidePanel.x + 3f,
                previewPanel.yMax + 6f,
                sidePanel.width - 6f,
                Mathf.Max(0f, sidePanel.yMax - previewPanel.yMax - 9f));
            float requiredDetailsHeight =
                CalculateWeaponDetailsContentHeight(
                    completedPatterns.Count);
            bool showDetailsScrollbar =
                ShouldScrollWeaponDetails(
                    detailsViewport.height,
                    completedPatterns.Count);
            Rect detailsContent = new Rect(
                0f,
                0f,
                detailsViewport.width -
                    (showDetailsScrollbar
                        ? GameTypography.MinimalVerticalScrollbarWidth + 2f
                        : 0f),
                Mathf.Max(
                    detailsViewport.height,
                    requiredDetailsHeight));
            weaponDetailsScroll[weaponIndex] =
                LoopSceneGui.BeginVerticalScrollView(
                detailsViewport,
                weaponDetailsScroll[weaponIndex],
                detailsContent,
                showDetailsScrollbar);
            DrawWeaponDetails(
                detailsContent,
                weapon,
                completedPatterns);
            GUI.EndScrollView();
            GUI.Label(
                new Rect(gridPanel.x + 14f, gridPanel.y + 11f, gridPanel.width - 28f, 22f),
                "WEAPON GRID",
                LoopSceneGui.Heading);
            GUI.Label(
                new Rect(gridPanel.x + 122f, gridPanel.y + 13f, gridPanel.width - 246f, 18f),
                "DRAG TO PAN  /  WHEEL TO ZOOM",
                LoopSceneGui.Muted);
            if (GUI.Button(
                    new Rect(gridPanel.xMax - 112f, gridPanel.y + 9f, 96f, 26f),
                    "RESET VIEW"))
            {
                weaponGridPan[weaponIndex] = Vector2.zero;
                weaponGridZoom[weaponIndex] = 1f;
            }
            DrawGridCells(
                new Rect(
                    gridPanel.x + 18f,
                    gridPanel.y + 42f,
                    gridPanel.width - 36f,
                    gridPanel.height - 58f),
                selectedState);
        }

        private void DrawWeaponPreview(Rect rect)
        {
            DrawInventorySection(rect);
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 20f),
                "WEAPON MODEL",
                LoopSceneGui.Heading);
            Rect modelArea = new Rect(
                rect.x + 8f,
                rect.y + 30f,
                rect.width - 16f,
                Mathf.Max(0f, rect.height - 38f));
            if (previewRenderer != null && previewRenderer.WeaponTexture != null)
            {
                GUI.DrawTexture(
                    modelArea,
                    previewRenderer.WeaponTexture,
                    ScaleMode.ScaleToFit,
                    true);
            }
            else
            {
                GUI.Label(modelArea, "WEAPON PREVIEW", LoopSceneGui.Centered);
            }
            GUI.Label(
                new Rect(modelArea.x, modelArea.yMax - 19f, modelArea.width, 18f),
                "DRAG TO ROTATE",
                LoopSceneGui.Muted);
            HandleWeaponPreviewRotation(modelArea);
        }

        private void HandleWeaponPreviewRotation(Rect area)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDrag &&
                current.button == 0 &&
                area.Contains(current.mousePosition))
            {
                previewRenderer?.RotateWeapon(-current.delta.x * 0.8f);
                current.Use();
            }
        }

        private void SelectPreviewWeapon(int index)
        {
            if (previewRenderer == null || previewWeaponIndex == index)
            {
                return;
            }
            previewRenderer.SelectWeapon(index);
            previewWeaponIndex = index;
        }

        private void DrawWeaponDetails(
            Rect rect,
            WeaponInstanceRecord weapon,
            IReadOnlyList<string> patterns)
        {
            float x = rect.x + 14f;
            float width = rect.width - 28f;
            float y = rect.y + 11f;
            GUI.Label(
                new Rect(x, y, width, 22f),
                "WEAPON STATS",
                LoopSceneGui.Heading);
            y += 30f;
            DrawStatLine(x, ref y, width, "LEVEL", weapon.Level.ToString());
            DrawStatLine(x, ref y, width, "EXPERIENCE", weapon.Experience.ToString());
            ResolveCombatWeapons();
            WeaponGridModifiers bonuses = weaponGrid.ResolveWeapon(weaponIndex);
            if (weaponIndex == 0 && meleeWeapon != null)
            {
                DrawStatLine(x, ref y, width, "BASE DAMAGE", Mathf.Max(0f, meleeWeapon.Damage - bonuses.Damage).ToString("0.#"));
                DrawStatLine(x, ref y, width, "COOLDOWN", $"{meleeWeapon.Cooldown:0.00} s");
                DrawStatLine(x, ref y, width, "ATTACK RATE", $"×{meleeWeapon.AttackSpeedMultiplier:0.000}");
                DrawStatLine(x, ref y, width, "BLADE REACH", $"{meleeWeapon.Reach:0.00} m");
                DrawStatLine(x, ref y, width, "HEFT / HANDLING", $"{meleeWeapon.Heft * 100f:0} / {meleeWeapon.Handling * 100f:0}");
                DrawStatLine(x, ref y, width, "IMPACT / STAGGER", $"{meleeWeapon.HitPauseDuration * 1000f:0} ms / {meleeWeapon.StaggerDuration:0.000} s");
            }
            else if (weaponIndex == 1 && bowWeapon != null)
            {
                DrawStatLine(x, ref y, width, "BASE DAMAGE", $"{bowWeapon.MinimumDamage:0.#} - {bowWeapon.MaximumDamage:0.#}");
                DrawStatLine(x, ref y, width, "ARROW SPEED", $"{bowWeapon.MaximumArrowSpeed:0.#} m/s");
                DrawStatLine(x, ref y, width, "FULL DRAW", $"{bowWeapon.FullDrawDuration:0.00} s");
            }
            y += 10f;
            GUI.Label(
                new Rect(x, y, width, 22f),
                "ARTIFACT BONUSES",
                LoopSceneGui.Heading);
            y += 28f;
            DrawStatLine(x, ref y, width, "DAMAGE", $"+{bonuses.Damage:0.#}");
            DrawStatLine(x, ref y, width, "HEALTH", $"+{bonuses.MaxHealth:0.#}");
            DrawStatLine(x, ref y, width, "MOVE SPEED", $"+{bonuses.MoveSpeed:0.##}");
            y += 10f;
            GUI.Label(new Rect(x, y, width, 22f), "COMPLETED PATTERNS", LoopSceneGui.Heading);
            y += 27f;
            int patternRows = Mathf.Max(3, patterns.Count);
            for (int index = 0; index < patternRows; index++)
            {
                Rect row = new Rect(x, y, width, 30f);
                DrawCellSurface(row);
                if (index < patterns.Count)
                {
                    GUI.Label(new Rect(row.x + 9f, row.y, row.width - 18f, row.height), patterns[index], LoopSceneGui.Body);
                }
                y += 34f;
            }
        }

        public static Rect CalculateForgeContentRect(Rect panel)
        {
            float horizontalMargin = HomeInventoryController.
                CalculateInventorySectionSpacing(panel.width);
            float verticalMargin =
                CalculateForgeVerticalMargin(panel.height);
            float top = panel.y + verticalMargin + ForgeHeaderHeight;
            float bottom = panel.yMax - verticalMargin - ForgeFooterHeight;
            return new Rect(
                panel.x + horizontalMargin,
                top,
                Mathf.Max(0f, panel.width - horizontalMargin * 2f),
                Mathf.Max(0f, bottom - top));
        }

        public static float CalculateArtifactLibraryWidth(float contentWidth)
        {
            return Mathf.Min(
                Mathf.Max(0f, contentWidth),
                Mathf.Max(
                    190f,
                    contentWidth * ArtifactLibraryWidthFraction));
        }

        public static float CalculateWeaponDetailsContentHeight(
            int completedPatternCount)
        {
            const float fixedContentHeight = 506f;
            const float patternRowHeight = 34f;
            return fixedContentHeight +
                Mathf.Max(3, completedPatternCount) * patternRowHeight;
        }

        public static bool ShouldScrollWeaponDetails(
            float viewportHeight,
            int completedPatternCount)
        {
            return CalculateWeaponDetailsContentHeight(
                    completedPatternCount) >
                Mathf.Max(0f, viewportHeight);
        }

        private static float CalculateForgeVerticalMargin(
            float panelHeight)
        {
            return Mathf.Clamp(panelHeight * 0.025f, 12f, 24f);
        }

        private void DrawArtifactLibrary(
            Rect rect,
            PlayerProfile profile,
            HomeStorageChest adjacentChest)
        {
            DrawInventorySection(rect);
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 11f, rect.width - 32f, 22f),
                "AVAILABLE ARTIFACTS",
                LoopSceneGui.Heading);
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 32f, rect.width - 32f, 18f),
                unlimitedArtifactCatalog
                    ? "UNLIMITED TEST CATALOG"
                    : adjacentChest != null
                        ? "BACKPACK + ADJACENT CHEST"
                        : "BACKPACK",
                LoopSceneGui.Muted);
            float controlsY = rect.y + 57f;
            GUI.Label(new Rect(rect.x + 16f, controlsY, 54f, 24f), "SEARCH", slotLabelStyle);
            searchText = GUI.TextField(
                new Rect(rect.x + 70f, controlsY, rect.width - 86f, 24f),
                searchText ?? string.Empty);
            controlsY += 31f;
            if (GUI.Button(
                    new Rect(rect.x + 16f, controlsY, rect.width - 32f, 28f),
                    $"SORT  /  {sortMode.ToString().ToUpperInvariant()}",
                    LoopSceneGui.Button))
            {
                sortMode = (ArtifactSortMode)(((int)sortMode + 1) % 4);
            }

            List<AvailableArtifact> artifacts = BuildAvailableArtifacts(
                profile,
                adjacentChest);
            Rect viewport = new Rect(
                rect.x + 14f,
                controlsY + 38f,
                rect.width - 28f,
                rect.yMax - controlsY - 52f);
            int columns = unlimitedArtifactCatalog ? 3 : 5;
            const float gap = 2f;
            int rows = unlimitedArtifactCatalog
                ? 1
                : Mathf.Max(
                    10,
                    Mathf.CeilToInt(
                        artifacts.Count / (float)columns));
            float cellSize = Mathf.Clamp(
                Mathf.Floor(
                    (viewport.width - gap * (columns - 1)) /
                    columns),
                12f,
                ForgeCellSize);
            bool showLibraryScrollbar =
                rows * cellSize + gap * (rows - 1) >
                    viewport.height;
            float scrollbarAllowance = showLibraryScrollbar
                ? GameTypography.MinimalVerticalScrollbarWidth + 2f
                : 0f;
            cellSize = Mathf.Clamp(
                Mathf.Floor(
                    (viewport.width - scrollbarAllowance -
                        gap * (columns - 1)) /
                    columns),
                12f,
                ForgeCellSize);
            float gridWidth = columns * cellSize + gap * (columns - 1);
            Rect scrollContent = new Rect(
                0f,
                0f,
                Mathf.Max(
                    0f,
                    viewport.width - scrollbarAllowance),
                rows * cellSize + gap * (rows - 1));
            float gridStartX = Mathf.Max(0f, (scrollContent.width - gridWidth) * 0.5f);
            libraryScroll = LoopSceneGui.BeginVerticalScrollView(
                viewport,
                libraryScroll,
                scrollContent,
                scrollContent.height > viewport.height);
            for (int slot = 0; slot < rows * columns; slot++)
            {
                int column = slot % columns;
                int row = slot / columns;
                Rect cell = new Rect(
                    gridStartX + column * (cellSize + gap),
                    row * (cellSize + gap),
                    cellSize,
                    cellSize);
                DrawCellSurface(cell);
                if (slot < artifacts.Count)
                {
                    DrawLibraryArtifact(cell, artifacts[slot]);
                }
            }
            GUI.EndScrollView();
        }

        private List<AvailableArtifact> BuildAvailableArtifacts(
            PlayerProfile profile,
            HomeStorageChest adjacentChest)
        {
            var result = new List<AvailableArtifact>();
            if (profile == null)
            {
                return result;
            }
            if (unlimitedArtifactCatalog)
            {
                AddUnlimitedArtifacts(result);
                result.Sort(CompareAvailableArtifacts);
                return result;
            }
            AddAvailableArtifacts(result, profile, profile.InventoryEntryIds, "PACK");
            if (adjacentChest != null)
            {
                AddAvailableArtifacts(
                    result,
                    profile,
                    profile.GetChestEntryIds(adjacentChest.ChestId),
                    "CHEST");
            }
            result.Sort(CompareAvailableArtifacts);
            return result;
        }

        private void AddUnlimitedArtifacts(
            ICollection<AvailableArtifact> result)
        {
            if (weaponGrid == null)
            {
                return;
            }

            string search = searchText?.Trim() ?? string.Empty;
            for (int index = 0;
                 index < weaponGrid.Definitions.Count;
                 index++)
            {
                ArtifactDefinitionData definition =
                    weaponGrid.Definitions[index];
                string function = ResolveArtifactFunction(definition);
                string displayName = ItemDefinitionCatalog.DisplayName(
                    definition.DefinitionId);
                if (search.Length > 0 &&
                    displayName.IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) < 0 &&
                    function.IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.Add(new AvailableArtifact(
                    definition.DefinitionId,
                    "INFINITE",
                    function,
                    definition.Shape.Count));
            }
        }

        private void AddAvailableArtifacts(
            ICollection<AvailableArtifact> result,
            PlayerProfile profile,
            IReadOnlyList<string> ids,
            string source)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                StorageEntry entry = profile.FindStorageEntry(ids[index]);
                if (entry == null || !ItemDefinitionCatalog.IsArtifact(entry.DefinitionId) ||
                    !weaponGrid.TryGetDefinition(entry.DefinitionId, out ArtifactDefinitionData definition))
                {
                    continue;
                }
                string function = ResolveArtifactFunction(definition);
                string displayName = ItemDefinitionCatalog.DisplayName(entry.DefinitionId);
                string search = searchText?.Trim() ?? string.Empty;
                if (search.Length > 0 &&
                    displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    function.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                result.Add(new AvailableArtifact(entry, source, function, definition.Shape.Count));
            }
        }

        private int CompareAvailableArtifacts(AvailableArtifact left, AvailableArtifact right)
        {
            int comparison = sortMode switch
            {
                ArtifactSortMode.Name => string.Compare(
                    ItemDefinitionCatalog.DisplayName(left.DefinitionId),
                    ItemDefinitionCatalog.DisplayName(right.DefinitionId),
                    StringComparison.OrdinalIgnoreCase),
                ArtifactSortMode.Size => left.Size.CompareTo(right.Size),
                ArtifactSortMode.Source => string.Compare(left.Source, right.Source, StringComparison.Ordinal),
                _ => string.Compare(left.Function, right.Function, StringComparison.Ordinal)
            };
            return comparison != 0
                ? comparison
                : string.Compare(
                    left.DefinitionId,
                    right.DefinitionId,
                    StringComparison.Ordinal);
        }

        private void DrawLibraryArtifact(Rect rect, AvailableArtifact artifact)
        {
            bool drewIcon =
                InventoryItemPresentation.DrawSingleCellIcon(
                    rect,
                    artifact.DefinitionId);
            Event current = Event.current;
            if (current.type == EventType.MouseDown &&
                current.button == 0 &&
                rect.Contains(current.mousePosition))
            {
                if (unlimitedArtifactCatalog)
                {
                    BeginHolding(artifact.DefinitionId);
                }
                else
                {
                    BeginHolding(artifact.Entry);
                }
                current.Use();
            }
            if (!drewIcon)
            {
                GUI.Label(
                    rect,
                    GetInitials(artifact.DefinitionId),
                    cellStyle);
            }
        }

        private static string ShortFunction(string function)
        {
            return function switch
            {
                "Damage" => "DMG",
                "Defense" => "DEF",
                "Mobility" => "MOV",
                "Hybrid" => "HYB",
                _ => "UTL"
            };
        }

        private void BeginHolding(string definitionId)
        {
            heldEntryId = null;
            heldDefinitionId = definitionId;
            heldRotation = 0;
            status =
                $"Place {ItemDefinitionCatalog.DisplayName(definitionId)} " +
                "on the weapon grid. R rotates.";
        }

        private void BeginHolding(StorageEntry entry)
        {
            heldDefinitionId = null;
            heldEntryId = entry.EntryId;
            heldRotation = 0;
            status = $"Place {ItemDefinitionCatalog.DisplayName(entry.DefinitionId)} on the weapon grid. R rotates.";
        }

        private static string ResolveArtifactFunction(ArtifactDefinitionData definition)
        {
            bool damage = false;
            bool defense = false;
            bool mobility = false;
            for (int index = 0; index < definition.Modifiers.Count; index++)
            {
                switch (definition.Modifiers[index].Stat)
                {
                    case ArtifactStat.Damage: damage = true; break;
                    case ArtifactStat.MaxHealth: defense = true; break;
                    case ArtifactStat.MoveSpeed: mobility = true; break;
                }
            }
            int count = (damage ? 1 : 0) + (defense ? 1 : 0) + (mobility ? 1 : 0);
            if (count > 1) return "Hybrid";
            if (damage) return "Damage";
            if (defense) return "Defense";
            if (mobility) return "Mobility";
            return "Utility";
        }

        private void DrawGridCells(Rect area, WeaponGridState state)
        {
            GUI.BeginGroup(area);
            Rect view = new Rect(0f, 0f, area.width, area.height);
            HandleGridNavigation(view);
            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            for (int index = 0; index < state.UnlockedCells.Count; index++)
            {
                GridCoordinate cell = state.UnlockedCells[index];
                minX = Mathf.Min(minX, cell.X);
                maxX = Mathf.Max(maxX, cell.X);
                minY = Mathf.Min(minY, cell.Y);
                maxY = Mathf.Max(maxY, cell.Y);
            }
            int columns = maxX - minX + 1;
            int rows = maxY - minY + 1;
            float size = ForgeCellSize * weaponGridZoom[weaponIndex];
            float originX = (view.width - columns * size) * 0.5f +
                weaponGridPan[weaponIndex].x;
            float originY = (view.height - rows * size) * 0.5f +
                weaponGridPan[weaponIndex].y;
            gridRects.Clear();
            for (int index = 0; index < state.UnlockedCells.Count; index++)
            {
                GridCoordinate cell = state.UnlockedCells[index];
                Rect cellRect = new Rect(
                    originX + (cell.X - minX) * size,
                    originY + (maxY - cell.Y) * size,
                    size - 2f,
                    size - 2f);
                gridRects[cell] = cellRect;
                LoopSceneGui.DrawWeaponGridCell(cellRect);
            }

            for (int index = 0; index < state.Placements.Count; index++)
            {
                ArtifactPlacement placement = state.Placements[index];
                if (!weaponGrid.TryGetDefinition(placement.Artifact.DefinitionId, out ArtifactDefinitionData definition))
                {
                    continue;
                }
                foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                {
                    if (!gridRects.TryGetValue(cell, out Rect occupiedRect))
                    {
                        continue;
                    }
                    if (!InventoryItemPresentation.DrawSingleCellIcon(
                            occupiedRect,
                            placement.Artifact.DefinitionId))
                    {
                        GUI.Label(
                            occupiedRect,
                            definition.DisplayName.Substring(0, 1),
                            LoopSceneGui.Centered);
                    }
                }
            }

            DrawHeldGridPreview(state);

            Event current = Event.current;
            if (current.type == EventType.MouseUp)
            {
                foreach (KeyValuePair<GridCoordinate, Rect> pair in gridRects)
                {
                    if (!pair.Value.Contains(current.mousePosition))
                    {
                        continue;
                    }
                    if (current.button == 0 && IsHoldingArtifact(
                            heldEntryId,
                            heldDefinitionId))
                    {
                        TryInstallHeld(pair.Key);
                        current.Use();
                    }
                    else if (current.button == 1)
                    {
                        TryRemoveAt(state, pair.Key);
                        current.Use();
                    }
                    break;
                }
            }
            GUI.EndGroup();
        }

        private void DrawHeldGridPreview(WeaponGridState state)
        {
            string definitionId = ResolveHeldDefinitionId(
                ResolveProfile());
            if (definitionId == null)
            {
                return;
            }

            foreach (KeyValuePair<GridCoordinate, Rect> pair in gridRects)
            {
                if (!pair.Value.Contains(Event.current.mousePosition))
                {
                    continue;
                }

                bool occupied = state.FindPlacementAt(
                    pair.Key,
                    BuildCatalog()) != null;
                Color previous = GUI.color;
                GUI.color = occupied
                    ? new Color(1f, 0.30f, 0.24f, 0.48f)
                    : new Color(1f, 1f, 1f, 0.58f);
                InventoryItemPresentation.DrawSingleCellIcon(
                    pair.Value,
                    definitionId);
                GUI.color = previous;
                break;
            }
        }

        private void HandleGridNavigation(Rect view)
        {
            Event current = Event.current;
            if (current.type == EventType.ScrollWheel &&
                view.Contains(current.mousePosition))
            {
                float previous = weaponGridZoom[weaponIndex];
                float next = CalculateGridZoom(
                    previous,
                    current.delta.y);
                if (!Mathf.Approximately(previous, next))
                {
                    Vector2 fromCenter = current.mousePosition -
                        view.center - weaponGridPan[weaponIndex];
                    weaponGridPan[weaponIndex] +=
                        fromCenter * (1f - next / previous);
                    weaponGridZoom[weaponIndex] = next;
                }
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDown &&
                current.button == 0 &&
                !IsHoldingArtifact(
                    heldEntryId,
                    heldDefinitionId) &&
                view.Contains(current.mousePosition))
            {
                gridPanning = true;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDrag &&
                current.button == 0 && gridPanning)
            {
                weaponGridPan[weaponIndex] += current.delta;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseUp &&
                current.button == 0 && gridPanning)
            {
                gridPanning = false;
                current.Use();
            }
        }

        public static float CalculateGridZoom(
            float currentZoom,
            float wheelDelta)
        {
            return Mathf.Clamp(
                currentZoom * (1f - wheelDelta * 0.08f),
                MinimumGridZoom,
                MaximumGridZoom);
        }

        public static bool IsHoldingArtifact(
            string entryId,
            string unlimitedDefinitionId)
        {
            return !string.IsNullOrWhiteSpace(entryId) ||
                !string.IsNullOrWhiteSpace(unlimitedDefinitionId);
        }

        private void TryInstallHeld(GridCoordinate coordinate)
        {
            if (unlimitedArtifactCatalog)
            {
                TryInstallUnlimitedArtifact(coordinate);
                return;
            }

            PlayerProfile profile = ResolveProfile();
            StorageEntry entry = profile?.FindStorageEntry(heldEntryId);
            if (ArtifactInstallationService.TryInstall(
                    profile,
                    weaponGrid,
                    weaponIndex,
                    entry,
                    AdjacentChestId,
                    coordinate,
                    heldRotation,
                    out string reason))
            {
                status = $"Installed {ItemDefinitionCatalog.DisplayName(entry.DefinitionId)}.";
                heldEntryId = null;
                Persist();
            }
            else
            {
                status = reason;
            }
        }

        private void TryInstallUnlimitedArtifact(
            GridCoordinate coordinate)
        {
            if (string.IsNullOrWhiteSpace(heldDefinitionId))
            {
                return;
            }

            string definitionId = heldDefinitionId;
            var artifact = new ArtifactInstance(
                Guid.NewGuid().ToString("N"),
                definitionId);
            if (weaponGrid.TryPlace(
                    weaponIndex,
                    artifact,
                    coordinate,
                    heldRotation,
                    out string reason))
            {
                status =
                    $"Installed {ItemDefinitionCatalog.DisplayName(definitionId)}. " +
                    "The catalog copy remains available.";
                heldDefinitionId = null;
                Persist();
            }
            else
            {
                status = reason;
            }
        }

        private void TryRemoveAt(WeaponGridState state, GridCoordinate coordinate)
        {
            ArtifactPlacement placement = state.FindPlacementAt(
                coordinate,
                BuildCatalog());
            if (placement == null)
            {
                return;
            }
            if (unlimitedArtifactCatalog)
            {
                if (weaponGrid.TryRemoveInstance(
                        weaponIndex,
                        placement.Artifact.InstanceId,
                        out _))
                {
                    status =
                        "Artifact removed. Unlimited copies remain in the catalog.";
                    Persist();
                }
                return;
            }
            if (ArtifactInstallationService.TryReturnToStorage(
                    ResolveProfile(),
                    weaponGrid,
                    weaponIndex,
                    placement,
                    AdjacentChestId,
                    out string reason))
            {
                status = "Artifact returned to your backpack (or the adjacent chest if full).";
                Persist();
            }
            else
            {
                status = reason;
            }
        }

        private Dictionary<string, ArtifactDefinitionData> BuildCatalog()
        {
            var result = new Dictionary<string, ArtifactDefinitionData>(StringComparer.Ordinal);
            for (int index = 0; index < weaponGrid.Definitions.Count; index++)
            {
                ArtifactDefinitionData definition = weaponGrid.Definitions[index];
                result[definition.DefinitionId] = definition;
            }
            return result;
        }

        private void DrawHeldArtifact(PlayerProfile profile)
        {
            string definitionId = ResolveHeldDefinitionId(profile);
            if (definitionId == null)
            {
                return;
            }
            Vector2 mouse = Event.current.mousePosition;
            Rect rect =
                InventoryItemPresentation.CalculateSingleCellCursorRect(
                    mouse,
                    ForgeCellSize);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            if (!InventoryItemPresentation.DrawSingleCellIcon(
                    rect,
                    definitionId))
            {
                GUI.Box(
                    rect,
                    ItemDefinitionCatalog.DisplayName(definitionId),
                    artifactStyle);
            }
            GUI.color = previous;
        }

        private string ResolveHeldDefinitionId(PlayerProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(heldDefinitionId))
            {
                return heldDefinitionId;
            }
            if (string.IsNullOrWhiteSpace(heldEntryId))
            {
                return null;
            }

            StorageEntry entry = profile?.FindStorageEntry(heldEntryId);
            if (entry != null)
            {
                return entry.DefinitionId;
            }

            heldEntryId = null;
            return null;
        }

        private void DrawStatLine(
            float x,
            ref float y,
            float width,
            string label,
            string value)
        {
            Rect row = new Rect(x, y, width, 32f);
            DrawCellSurface(row);
            GUI.Label(
                new Rect(row.x + 9f, row.y, row.width * 0.58f, row.height),
                label,
                LoopSceneGui.Muted);
            GUI.Label(
                new Rect(row.x + row.width * 0.55f, row.y, row.width * 0.45f - 9f, row.height),
                value,
                statStyle);
            y += 36f;
        }

        private void ResolveCombatWeapons()
        {
            if (player == null)
            {
                ResolveDependencies();
            }
            if (player == null)
            {
                return;
            }
            meleeWeapon ??=
                player.GetComponent<MeleeWeapon>() ??
                player.GetComponentInChildren<MeleeWeapon>(true);
            bowWeapon ??=
                player.GetComponent<BowWeapon>() ??
                player.GetComponentInChildren<BowWeapon>(true);
        }

        private HomeStorageChest ResolveAdjacentChest()
        {
            HomeGridOccupant anvilOccupant =
                GetComponentInParent<HomeGridOccupant>();
            if (anvilOccupant == null || anvilOccupant.Grid == null)
            {
                return null;
            }
            HomeStorageChest[] chests = FindObjectsByType<HomeStorageChest>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int chestIndex = 0; chestIndex < chests.Length; chestIndex++)
            {
                HomeGridOccupant chestOccupant =
                    chests[chestIndex].GetComponentInParent<HomeGridOccupant>();
                if (chestOccupant == null ||
                    chestOccupant.Grid != anvilOccupant.Grid)
                {
                    continue;
                }
                foreach (Vector3Int anvilCell in anvilOccupant.OccupiedCells())
                {
                    foreach (Vector3Int chestCell in chestOccupant.OccupiedCells())
                    {
                        Vector3Int delta = anvilCell - chestCell;
                        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) +
                            Mathf.Abs(delta.z) == 1)
                        {
                            return chests[chestIndex];
                        }
                    }
                }
            }
            return null;
        }

        private bool CanInteract()
        {
            ResolveDependencies();
            Camera camera = Camera.main;
            if (player == null || camera == null)
            {
                return false;
            }
            Ray ray = camera.ScreenPointToRay(
                LootInteractionPresentation.CalculateAimPoint(
                    camera,
                    player,
                    Screen.width,
                    Screen.height));
            int count = Physics.RaycastNonAlloc(
                ray,
                focusHits,
                camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (int index = 0; index < count; index++)
            {
                if (focusHits[index].collider.transform.IsChildOf(player) ||
                    focusHits[index].distance >= nearest)
                {
                    continue;
                }
                nearest = focusHits[index].distance;
                nearestCollider = focusHits[index].collider;
            }
            return nearestCollider != null &&
                (nearestCollider.transform == transform || nearestCollider.transform.IsChildOf(transform)) &&
                LootInteractionPresentation.IsWithinInteractionDistance(
                    player,
                    transform.parent != null ? transform.parent : transform,
                    InteractionDistance);
        }

        private void Open()
        {
            if (isOpen || weaponGrid == null || ResolveProfile() == null)
            {
                return;
            }
            ResetGridViewportState();
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (playerInput != null)
            {
                previousInputCapture = playerInput.UserInterfaceCaptureActive;
                playerInput.SetUserInterfaceCapture(true);
            }
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            heldEntryId = null;
            heldDefinitionId = null;
            previewRenderer ??= GetComponent<InventoryPreviewRenderer>() ??
                gameObject.AddComponent<InventoryPreviewRenderer>();
            previewRenderer.Configure(playerInput != null
                ? playerInput.transform
                : player, rebuild: true);
            previewWeaponIndex = -1;
            SelectPreviewWeapon(weaponIndex);
            isOpen = true;
        }

        private void ResetGridViewportState()
        {
            for (int index = 0; index < weaponGridPan.Length; index++)
            {
                weaponGridPan[index] = Vector2.zero;
                weaponGridZoom[index] = 1f;
            }
            gridPanning = false;
        }

        public void Close()
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
                playerInput.SetUserInterfaceCapture(previousInputCapture);
                if (!previousInputCapture && previousCursorLock == CursorLockMode.Locked)
                {
                    playerInput.RequestGameplayCursorCapture();
                }
            }
            heldEntryId = null;
            heldDefinitionId = null;
            isOpen = false;
        }

        private void Persist()
        {
            FindFirstObjectByType<WeaponGridProfileBinding>()?.SyncNow();
            homeBase?.SaveProfile();
        }

        private PlayerProfile ResolveProfile()
        {
            if (homeBase != null)
            {
                return homeBase.Profile;
            }

            GameplayLoopBootstrap bootstrap =
                GameplayLoopBootstrap.Current ??
                FindFirstObjectByType<GameplayLoopBootstrap>();
            return bootstrap?.Session?.ActiveProfile;
        }

        private void ResolveDependencies()
        {
            homeBase ??= FindFirstObjectByType<HomeBaseController>();
            inventory ??= FindFirstObjectByType<HomeInventoryController>();
            weaponGrid ??= FindFirstObjectByType<WeaponGridRuntime>();
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null ? playerObject.transform : null;
                playerInput ??= playerObject != null
                    ? playerObject.GetComponent<PlayerInputSource>()
                    : null;
            }
        }

        private void EnsureStyles()
        {
            GameTypography.ApplyToCurrentSkin();
            if (artifactStyle != null)
            {
                return;
            }
            artifactStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2),
                fontSize = 11,
                normal = { textColor = Color.clear },
                hover = { textColor = Color.clear }
            };
            cellStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.92f, 0.91f, 0.83f) }
            };
            statStyle = new GUIStyle(LoopSceneGui.Body)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 13
            };
            centeredTitleStyle = new GUIStyle(LoopSceneGui.Title)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1f, 0.91f, 0.68f, 1f) }
            };
            slotLabelStyle = new GUIStyle(LoopSceneGui.Muted)
            {
                alignment = TextAnchor.LowerLeft,
                fontSize = 9,
                normal = { textColor = Color.white }
            };
        }

        private void DrawCellSurface(Rect rect)
        {
            LoopSceneGui.DrawCell(rect);
        }

        private static void DrawInventorySection(Rect area)
        {
            LoopSceneGui.DrawSection(area);
        }

        private static string GetInitials(string definitionId)
        {
            string value = ItemDefinitionCatalog.DisplayName(definitionId);
            string[] words = value.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return "?";
            }
            return words.Length == 1
                ? words[0].Substring(0, Mathf.Min(2, words[0].Length)).ToUpperInvariant()
                : string.Concat(words[0][0], words[words.Length - 1][0]).ToUpperInvariant();
        }
    }
}
