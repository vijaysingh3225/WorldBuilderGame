using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    /// <summary>
    /// Dependency-light prototype UI for exercising the complete grid model. It can
    /// be attached to any scene object now and replaced by a retained-mode UI later.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class WeaponGridSandboxToolkit : MonoBehaviour
    {
        [SerializeField] private WeaponGridRuntime gridRuntime;
        [SerializeField] private PlayerInputSource inputSource;
        [SerializeField] private InventoryPreviewRenderer previewRenderer;
        [SerializeField] private bool toggleWithTab = true;
        [SerializeField] private bool startOpen;
        [SerializeField] private bool pauseWhileOpen = true;
        [SerializeField] private bool initializeSandboxIfPristine = true;
        [SerializeField] private Rect windowRect = new Rect(0f, 0f, 1080f, 620f);

        private readonly Dictionary<GridCoordinate, Rect> cellRects =
            new Dictionary<GridCoordinate, Rect>();

        private bool isOpen;
        private bool windowPositionInitialized;
        private bool hasCapturedPresentationState;
        private float capturedTimeScale = 1f;
        private CursorLockMode capturedCursorLock;
        private bool capturedCursorVisible;
        private bool capturedInputCaptureState;
        private bool sandboxInitializationChecked;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle centeredStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private Texture2D whiteTexture;
        private MeleeWeapon meleeWeapon;
        private BowWeapon bowWeapon;

        public bool IsOpen => isOpen;
        public WeaponGridRuntime Runtime => gridRuntime;

        private void Awake()
        {
            EnsureRuntime();
            isOpen = false;
            if (startOpen)
            {
                Open();
            }
        }

        private void OnDisable()
        {
            if (hasCapturedPresentationState)
            {
                RestorePresentationState();
            }
        }

        private void Update()
        {
            if (toggleWithTab &&
                !SceneNavigationMenu.IsAnyOpen &&
                PlayerControlBindings.WasPressedThisFrame(
                    Keyboard.current,
                    PlayerControl.Inventory))
            {
                Toggle();
            }

            if (!isOpen || Keyboard.current == null)
            {
                return;
            }

            if (PlayerControlBindings.WasPressedThisFrame(
                    Keyboard.current,
                    PlayerControl.Pause))
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

            EnsureRuntime();
            EnsureStyles();
            EnsureWindowRect();

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.48f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), whiteTexture);
            GUI.color = previousColor;
            windowRect = GUI.ModalWindow(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                GUIContent.none);
        }

        public void SetRuntime(WeaponGridRuntime runtime)
        {
            gridRuntime = runtime;
            sandboxInitializationChecked = true;
            EnsureRuntime();
        }

        public void SetInputSource(PlayerInputSource source)
        {
            if (inputSource == source)
            {
                return;
            }

            if (hasCapturedPresentationState && inputSource != null)
            {
                inputSource.SetUserInterfaceCapture(capturedInputCaptureState);
            }

            inputSource = source;
            if (hasCapturedPresentationState && inputSource != null)
            {
                capturedInputCaptureState =
                    inputSource.UserInterfaceCaptureActive;
                inputSource.SetUserInterfaceCapture(true);
            }
        }

        public void SetToggleWithTab(bool enabled)
        {
            toggleWithTab = enabled;
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (isOpen)
            {
                return;
            }

            EnsureRuntime();
            EnsurePreview();
            isOpen = true;
            CapturePresentationState();
        }

        public void OpenWeapon(int weaponIndex)
        {
            EnsureRuntime();
            int selected = Mathf.Clamp(weaponIndex, 0, 1);
            gridRuntime.SelectWeapon(selected);
            EnsurePreview();
            previewRenderer?.SelectWeapon(selected);
            Open();
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            RestorePresentationState();
        }

        private void DrawWindow(int windowId)
        {
            DrawInventorySection(
                new Rect(0f, 0f, windowRect.width, windowRect.height));
            GUI.Label(
                new Rect(18f, 10f, 420f, 30f),
                "WEAPON GRID",
                titleStyle);
            GUI.Label(
                new Rect(18f, 38f, windowRect.width - 76f, 20f),
                "Drag title bar to move  |  drag weapon to rotate  |  Esc closes",
                mutedStyle);

            var closeRect = new Rect(windowRect.width - 40f, 10f, 26f, 26f);
            DrawInventoryButtonSurface(closeRect, false);
            if (GUI.Button(closeRect, "X", tabStyle))
            {
                Close();
                return;
            }

            DrawWeaponTabs(new Rect(18f, 64f, windowRect.width - 36f, 34f));
            float previewWidth = Mathf.Clamp(
                windowRect.width * 0.24f,
                200f,
                250f);
            float sidebarWidth = Mathf.Clamp(
                windowRect.width * 0.23f,
                210f,
                250f);
            var previewArea = new Rect(
                18f,
                108f,
                previewWidth,
                windowRect.height - 124f);
            var gridArea = new Rect(
                previewArea.xMax + 8f,
                108f,
                windowRect.width - previewWidth - sidebarWidth - 60f,
                windowRect.height - 124f);
            var sidebarArea = new Rect(
                gridArea.xMax + 8f,
                108f,
                sidebarWidth,
                windowRect.height - 124f);

            DrawWeaponPreview(previewArea);
            DrawGridPanel(gridArea);
            DrawSidebar(sidebarArea);
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 48f, 56f));
        }

        public static Rect CalculateWindowRect(float screenWidth, float screenHeight)
        {
            const float margin = 16f;
            float width = Mathf.Max(0f, Mathf.Min(1080f, screenWidth - margin * 2f));
            float height = Mathf.Max(0f, Mathf.Min(620f, screenHeight - margin * 2f));
            return new Rect(
                (screenWidth - width) * 0.5f,
                (screenHeight - height) * 0.5f,
                width,
                height);
        }

        private void EnsureWindowRect()
        {
            Rect fitted = CalculateWindowRect(Screen.width, Screen.height);
            if (!windowPositionInitialized)
            {
                windowRect = fitted;
                windowPositionInitialized = true;
                return;
            }

            windowRect.width = fitted.width;
            windowRect.height = fitted.height;
            windowRect.x = Mathf.Clamp(
                windowRect.x,
                16f,
                Mathf.Max(16f, Screen.width - fitted.width - 16f));
            windowRect.y = Mathf.Clamp(
                windowRect.y,
                16f,
                Mathf.Max(16f, Screen.height - fitted.height - 16f));
        }

        private void DrawWeaponPreview(Rect area)
        {
            DrawInventorySection(area);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 14f, area.width - 32f, 26f),
                gridRuntime.ActiveGrid.DisplayName,
                headingStyle);
            Rect preview = new Rect(
                area.x + 12f,
                area.y + 48f,
                area.width - 24f,
                area.height - 104f);
            if (previewRenderer != null && previewRenderer.WeaponTexture != null)
            {
                GUI.DrawTexture(
                    preview,
                    previewRenderer.WeaponTexture,
                    ScaleMode.ScaleToFit,
                    false);
            }
            else
            {
                GUI.Box(preview, "WEAPON PREVIEW");
            }
            Event current = Event.current;
            if (current.type == EventType.MouseDrag &&
                current.button == 0 &&
                preview.Contains(current.mousePosition))
            {
                previewRenderer?.RotateWeapon(-current.delta.x * 0.8f);
                current.Use();
            }
            GUI.Label(
                new Rect(area.x + 16f, area.yMax - 42f, area.width - 32f, 24f),
                "CLICK + DRAG TO ROTATE",
                mutedStyle);
        }

        private void DrawWeaponTabs(Rect area)
        {
            WeaponGridLoadoutState loadout = gridRuntime.Loadout;
            const float gap = 2f;
            float tabWidth = (area.width - gap) * 0.5f;
            var primaryRect = new Rect(area.x, area.y, tabWidth, area.height);
            DrawInventoryButtonSurface(
                primaryRect,
                gridRuntime.ActiveWeaponIndex == 0);
            if (GUI.Button(
                primaryRect,
                loadout.Primary.DisplayName,
                gridRuntime.ActiveWeaponIndex == 0
                    ? selectedTabStyle
                    : tabStyle))
            {
                gridRuntime.SelectWeapon(0);
                previewRenderer?.SelectWeapon(0);
            }

            var secondaryRect = new Rect(
                area.x + tabWidth + gap,
                area.y,
                tabWidth,
                area.height);
            DrawInventoryButtonSurface(
                secondaryRect,
                gridRuntime.ActiveWeaponIndex == 1);
            if (GUI.Button(
                secondaryRect,
                loadout.Secondary.DisplayName,
                gridRuntime.ActiveWeaponIndex == 1
                    ? selectedTabStyle
                    : tabStyle))
            {
                gridRuntime.SelectWeapon(1);
                previewRenderer?.SelectWeapon(1);
            }
        }

        private void DrawGridPanel(Rect area)
        {
            DrawInventorySection(area);
            GUI.Label(
                new Rect(area.x + 18f, area.y + 14f, 280f, 26f),
                gridRuntime.ActiveGrid.DisplayName,
                headingStyle);
            GUI.Label(
                new Rect(area.xMax - 180f, area.y + 16f, 162f, 22f),
                $"{gridRuntime.ActiveGrid.UnlockedCells.Count} CELLS",
                mutedStyle);

            var boardArea = new Rect(
                area.x + 18f,
                area.y + 52f,
                area.width - 36f,
                area.height - 70f);
            DrawBoard(boardArea);
        }

        private void DrawBoard(Rect area)
        {
            WeaponGridState grid = gridRuntime.ActiveGrid;
            if (grid.UnlockedCells.Count == 0)
            {
                return;
            }

            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;
            for (int index = 0; index < grid.UnlockedCells.Count; index++)
            {
                GridCoordinate coordinate = grid.UnlockedCells[index];
                minX = Mathf.Min(minX, coordinate.X);
                maxX = Mathf.Max(maxX, coordinate.X);
                minY = Mathf.Min(minY, coordinate.Y);
                maxY = Mathf.Max(maxY, coordinate.Y);
            }

            int columns = maxX - minX + 1;
            int rows = maxY - minY + 1;
            float cellSize = Mathf.Clamp(
                Mathf.Min(
                    (area.width - 30f) / Mathf.Max(3, columns + 1),
                    (area.height - 30f) / Mathf.Max(3, rows + 1)),
                27f,
                58f);
            const float gap = 2f;
            float boardWidth = columns * cellSize;
            float boardHeight = rows * cellSize;
            float startX = area.center.x - boardWidth * 0.5f;
            float startY = area.center.y - boardHeight * 0.5f;

            cellRects.Clear();
            for (int index = 0; index < grid.UnlockedCells.Count; index++)
            {
                GridCoordinate coordinate = grid.UnlockedCells[index];
                float x = startX + (coordinate.X - minX) * cellSize;
                float y = startY + (maxY - coordinate.Y) * cellSize;
                var rect = new Rect(
                    x + gap * 0.5f,
                    y + gap * 0.5f,
                    cellSize - gap,
                    cellSize - gap);
                cellRects[coordinate] = rect;
                DrawCell(coordinate, rect);
            }
        }

        private void DrawCell(GridCoordinate coordinate, Rect rect)
        {
            ArtifactPlacement placement =
                gridRuntime.FindActivePlacementAt(coordinate);
            ArtifactDefinitionData definition = null;
            if (placement?.Artifact != null)
            {
                gridRuntime.TryGetDefinition(
                    placement.Artifact.DefinitionId,
                    out definition);
            }

            LoopSceneGui.DrawWeaponGridCell(rect);
            string label = definition != null
                ? GetInitials(definition.DisplayName)
                : string.Empty;
            if (definition != null)
            {
                Color previous = GUI.color;
                GUI.color = new Color(
                    definition.DisplayColor.r,
                    definition.DisplayColor.g,
                    definition.DisplayColor.b,
                    0.78f);
                GUI.DrawTexture(Inset(rect, 4f), whiteTexture);
                GUI.color = previous;
            }

            GUI.Label(rect, label, centeredStyle);
        }

        private void DrawSidebar(Rect area)
        {
            DrawInventorySection(area);
            float x = area.x + 16f;
            float width = area.width - 32f;
            float y = area.y + 14f;
            DrawStats(new Rect(x, y, width, area.height - 28f));
        }

        private void DrawStats(Rect area)
        {
            ResolveWeapons();
            GUI.Label(new Rect(area.x, area.y, area.width, 24f), "WEAPON STATS", headingStyle);
            float y = area.y + 38f;
            if (gridRuntime.ActiveWeaponIndex == 0)
            {
                if (meleeWeapon == null)
                {
                    DrawUnavailableStats(area.x, ref y, area.width);
                    return;
                }

                DrawStatLine(area.x, ref y, area.width, "DAMAGE", $"{meleeWeapon.Damage:0.#}");
                DrawStatLine(area.x, ref y, area.width, "ATTACK COOLDOWN", $"{meleeWeapon.Cooldown:0.00} s");
                DrawStatLine(area.x, ref y, area.width, "BLADE REACH", $"{meleeWeapon.Reach:0.00} m");
                DrawStatLine(area.x, ref y, area.width, "HIT RADIUS", $"{meleeWeapon.Radius:0.00} m");
                return;
            }

            if (bowWeapon == null)
            {
                DrawUnavailableStats(area.x, ref y, area.width);
                return;
            }

            float minimumDamage = bowWeapon.MinimumDamage + bowWeapon.RuntimeDamageBonus;
            float maximumDamage = bowWeapon.MaximumDamage + bowWeapon.RuntimeDamageBonus;
            DrawStatLine(area.x, ref y, area.width, "DAMAGE", $"{minimumDamage:0.#} - {maximumDamage:0.#}");
            DrawStatLine(area.x, ref y, area.width, "MAX ARROW SPEED", $"{bowWeapon.MaximumArrowSpeed:0.#} m/s");
            DrawStatLine(area.x, ref y, area.width, "FULL DRAW", $"{bowWeapon.FullDrawDuration:0.00} s");
            DrawStatLine(area.x, ref y, area.width, "SHOT RECOVERY", $"{bowWeapon.EffectiveReloadDuration:0.00} s");
        }

        private void DrawStatLine(
            float x,
            ref float y,
            float width,
            string label,
            string value)
        {
            DrawInventoryCellSurface(new Rect(x, y, width, 38f));
            GUI.Label(
                new Rect(x + 10f, y, width * 0.62f, 38f),
                label,
                mutedStyle);
            GUI.Label(
                new Rect(x + width * 0.48f, y, width * 0.52f - 10f, 38f),
                value,
                centeredStyle);
            y += 42f;
        }

        private void DrawUnavailableStats(float x, ref float y, float width)
        {
            DrawInventoryCellSurface(new Rect(x, y, width, 38f));
            GUI.Label(
                new Rect(x + 10f, y, width - 20f, 38f),
                "Weapon is not present in this scene",
                mutedStyle);
            y += 42f;
        }

        private void EnsureRuntime()
        {
            if (gridRuntime == null)
            {
                gridRuntime = GetComponent<WeaponGridRuntime>();
            }

            if (gridRuntime == null)
            {
                gridRuntime = gameObject.AddComponent<WeaponGridRuntime>();
                gridRuntime.InitializeSandboxDefaults();
            }
            else
            {
                gridRuntime.EnsureInitialized();
            }

            if (!sandboxInitializationChecked)
            {
                sandboxInitializationChecked = true;
                if (initializeSandboxIfPristine &&
                    IsPristine(gridRuntime.Loadout.Primary) &&
                    IsPristine(gridRuntime.Loadout.Secondary))
                {
                    gridRuntime.InitializeSandboxDefaults();
                }
            }

        }

        private void CapturePresentationState()
        {
            if (hasCapturedPresentationState)
            {
                return;
            }

            hasCapturedPresentationState = true;
            capturedTimeScale = Time.timeScale;
            capturedCursorLock = Cursor.lockState;
            capturedCursorVisible = Cursor.visible;
            EnsureInputSource();
            if (inputSource != null)
            {
                capturedInputCaptureState =
                    inputSource.UserInterfaceCaptureActive;
                inputSource.SetUserInterfaceCapture(true);
            }

            if (pauseWhileOpen)
            {
                Time.timeScale = 0f;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestorePresentationState()
        {
            if (!hasCapturedPresentationState)
            {
                return;
            }

            if (pauseWhileOpen)
            {
                Time.timeScale = capturedTimeScale;
            }

            if (inputSource != null)
            {
                inputSource.SetUserInterfaceCapture(
                    capturedInputCaptureState);
            }

            Cursor.lockState = capturedCursorLock;
            Cursor.visible = capturedCursorVisible;
            hasCapturedPresentationState = false;
        }

        private void EnsureInputSource()
        {
            if (inputSource != null)
            {
                return;
            }

            try
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    inputSource =
                        player.GetComponent<PlayerInputSource>() ??
                        player.GetComponentInChildren<PlayerInputSource>(true);
                }
            }
            catch (UnityException)
            {
                // The toolkit remains usable in isolated scenes without a Player tag.
            }
        }

        private void ResolveWeapons()
        {
            if (meleeWeapon != null && bowWeapon != null)
            {
                return;
            }

            EnsureInputSource();
            Transform character = inputSource != null
                ? inputSource.transform
                : null;
            if (character == null)
            {
                return;
            }

            meleeWeapon ??=
                character.GetComponent<MeleeWeapon>() ??
                character.GetComponentInParent<MeleeWeapon>();
            bowWeapon ??=
                character.GetComponentInChildren<BowWeapon>(true) ??
                character.GetComponentInParent<BowWeapon>();
        }

        private void EnsurePreview()
        {
            EnsureInputSource();
            previewRenderer ??=
                GetComponent<InventoryPreviewRenderer>() ??
                gameObject.AddComponent<InventoryPreviewRenderer>();
            previewRenderer.Configure(
                inputSource != null ? inputSource.transform : null);
            previewRenderer.SelectWeapon(
                gridRuntime != null
                    ? gridRuntime.ActiveWeaponIndex
                    : 0);
        }

        private static bool IsPristine(WeaponGridState state)
        {
            return state != null &&
                state.GrowthStep == 0 &&
                state.UnlockedCells.Count == 1 &&
                state.Placements.Count == 0;
        }

        private void EnsureStyles()
        {
            GameTypography.ApplyToCurrentSkin();
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }

            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.93f, 0.9f, 0.79f) }
            };
            headingStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.84f, 0.72f) }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.UiFont,
                fontSize = 12,
                normal = { textColor = new Color(0.83f, 0.85f, 0.86f) }
            };
            mutedStyle ??= new GUIStyle(bodyStyle)
            {
                normal = { textColor = new Color(0.57f, 0.61f, 0.64f) }
            };
            centeredStyle ??= new GUIStyle(GUI.skin.button)
            {
                font = GameTypography.UiFont,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    background = null,
                    textColor = Color.white
                }
            };
            tabStyle ??= new GUIStyle()
            {
                font = GameTypography.UiFont,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.82f, 0.83f) }
            };
            selectedTabStyle ??= new GUIStyle(tabStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private void DrawInventorySection(Rect area)
        {
            LoopSceneGui.DrawSection(area);
        }

        private void DrawInventoryCellSurface(
            Rect area,
            bool highlighted = false)
        {
            LoopSceneGui.DrawCell(area);
            if (highlighted)
            {
                DrawBorder(
                    area,
                    GameTypography.BorderColor,
                    2f);
            }
        }

        private void DrawInventoryButtonSurface(Rect area, bool selected)
        {
            DrawInventoryCellSurface(area, selected);
        }

        private void DrawBorder(Rect area, Color color, float thickness)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(area.x, area.y, area.width, thickness),
                whiteTexture);
            GUI.DrawTexture(
                new Rect(area.x, area.yMax - thickness, area.width, thickness),
                whiteTexture);
            GUI.DrawTexture(
                new Rect(area.x, area.y, thickness, area.height),
                whiteTexture);
            GUI.DrawTexture(
                new Rect(area.xMax - thickness, area.y, thickness, area.height),
                whiteTexture);
            GUI.color = previous;
        }

        private static Rect Inset(Rect area, float amount)
        {
            return new Rect(
                area.x + amount,
                area.y + amount,
                Mathf.Max(0f, area.width - amount * 2f),
                Mathf.Max(0f, area.height - amount * 2f));
        }

        private static string GetInitials(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            string[] words = value.Split(
                new[] { ' ', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                return words[0].Substring(0, Mathf.Min(2, words[0].Length))
                    .ToUpperInvariant();
            }

            return string.Concat(
                words[0][0],
                words[words.Length - 1][0]).ToUpperInvariant();
        }
    }
}
