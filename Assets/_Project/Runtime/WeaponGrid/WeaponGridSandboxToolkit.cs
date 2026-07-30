using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;

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
        private enum GridTool
        {
            Place,
            Remove
        }

        [SerializeField] private WeaponGridRuntime gridRuntime;
        [SerializeField] private PlayerInputSource inputSource;
        [SerializeField] private bool toggleWithTab = true;
        [SerializeField] private bool startOpen;
        [SerializeField] private bool pauseWhileOpen = true;
        [SerializeField] private bool initializeSandboxIfPristine = true;
        [SerializeField] private Rect windowRect = new Rect(0f, 0f, 1080f, 700f);

        private readonly Dictionary<GridCoordinate, Rect> cellRects =
            new Dictionary<GridCoordinate, Rect>();

        private bool isOpen;
        private bool hasCapturedPresentationState;
        private float capturedTimeScale = 1f;
        private CursorLockMode capturedCursorLock;
        private bool capturedCursorVisible;
        private bool capturedInputCaptureState;
        private bool sandboxInitializationChecked;
        private string selectedDefinitionId;
        private int selectedRotation;
        private GridTool activeTool;
        private string seedText = "1337";
        private string statusMessage =
            "Choose an artifact and click a cell. Shift-click rotates; right-click removes.";
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle centeredStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private Texture2D whiteTexture;

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
                Keyboard.current != null &&
                Keyboard.current.tabKey.wasPressedThisFrame)
            {
                Toggle();
            }

            if (!isOpen || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
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
            float width = Mathf.Min(windowRect.width, Screen.width - 24f);
            float height = Mathf.Min(windowRect.height, Screen.height - 24f);
            windowRect.width = Mathf.Max(760f, width);
            windowRect.height = Mathf.Max(560f, height);
            windowRect.x = (Screen.width - windowRect.width) * 0.5f;
            windowRect.y = (Screen.height - windowRect.height) * 0.5f;

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
            isOpen = true;
            CapturePresentationState();
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
            DrawPanelBackground(new Rect(0f, 0f, windowRect.width, windowRect.height));
            GUI.Label(
                new Rect(24f, 18f, 500f, 34f),
                "WEAPON GRID  /  SANDBOX TOOLKIT",
                titleStyle);
            GUI.Label(
                new Rect(24f, 51f, 680f, 22f),
                "Developer artifact override | changes resolve immediately | Tab / Esc closes",
                mutedStyle);

            if (GUI.Button(
                new Rect(windowRect.width - 52f, 18f, 28f, 28f),
                "X"))
            {
                Close();
                return;
            }

            DrawWeaponTabs(new Rect(24f, 82f, windowRect.width - 48f, 42f));
            float sidebarWidth = 326f;
            var gridArea = new Rect(
                24f,
                136f,
                windowRect.width - sidebarWidth - 62f,
                windowRect.height - 170f);
            var sidebarArea = new Rect(
                gridArea.xMax + 14f,
                136f,
                sidebarWidth,
                windowRect.height - 170f);

            DrawGridPanel(gridArea);
            DrawSidebar(sidebarArea);
        }

        private void DrawWeaponTabs(Rect area)
        {
            WeaponGridLoadoutState loadout = gridRuntime.Loadout;
            float tabWidth = (area.width - 8f) * 0.5f;
            if (GUI.Button(
                new Rect(area.x, area.y, tabWidth, area.height),
                loadout.Primary.DisplayName,
                gridRuntime.ActiveWeaponIndex == 0
                    ? selectedTabStyle
                    : tabStyle))
            {
                gridRuntime.SelectWeapon(0);
                seedText = loadout.Primary.Seed.ToString();
            }

            if (GUI.Button(
                new Rect(area.x + tabWidth + 8f, area.y, tabWidth, area.height),
                loadout.Secondary.DisplayName,
                gridRuntime.ActiveWeaponIndex == 1
                    ? selectedTabStyle
                    : tabStyle))
            {
                gridRuntime.SelectWeapon(1);
                seedText = loadout.Secondary.Seed.ToString();
            }
        }

        private void DrawGridPanel(Rect area)
        {
            DrawPanelBackground(area, new Color(0.075f, 0.085f, 0.095f, 0.98f));
            GUI.Label(
                new Rect(area.x + 18f, area.y + 14f, 280f, 26f),
                gridRuntime.ActiveGrid.DisplayName,
                headingStyle);
            GUI.Label(
                new Rect(area.xMax - 260f, area.y + 16f, 242f, 22f),
                $"CELLS  {gridRuntime.ActiveGrid.UnlockedCells.Count}   |   GROWTH  {gridRuntime.ActiveGrid.GrowthStep}",
                mutedStyle);

            var boardArea = new Rect(
                area.x + 18f,
                area.y + 52f,
                area.width - 36f,
                area.height - 106f);
            DrawBoard(boardArea);

            GUI.Label(
                new Rect(area.x + 18f, area.yMax - 38f, area.width - 36f, 22f),
                statusMessage,
                mutedStyle);
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
            float gap = Mathf.Clamp(cellSize * 0.08f, 2f, 5f);
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

            Color previousBackground = GUI.backgroundColor;
            Color cellColor = definition != null
                ? definition.DisplayColor
                : coordinate == GridCoordinate.Root
                    ? new Color(0.38f, 0.5f, 0.58f)
                    : new Color(0.2f, 0.23f, 0.26f);
            GUI.backgroundColor = cellColor;
            string label = definition != null
                ? GetInitials(definition.DisplayName)
                : coordinate == GridCoordinate.Root ? "ROOT" : string.Empty;

            Event currentEvent = Event.current;
            bool shiftClick = currentEvent.shift;
            bool rightClick =
                currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 1 &&
                rect.Contains(currentEvent.mousePosition);
            bool leftClick = GUI.Button(rect, label, centeredStyle);
            GUI.backgroundColor = previousBackground;

            if (rightClick)
            {
                RemoveAt(coordinate);
                currentEvent.Use();
            }
            else if (leftClick)
            {
                if (shiftClick && placement != null)
                {
                    RotatePlacedAt(coordinate);
                }
                else if (activeTool == GridTool.Remove)
                {
                    RemoveAt(coordinate);
                }
                else
                {
                    PlaceAt(coordinate);
                }
            }
        }

        private void DrawSidebar(Rect area)
        {
            DrawPanelBackground(area, new Color(0.09f, 0.1f, 0.115f, 0.99f));
            float x = area.x + 16f;
            float width = area.width - 32f;
            float y = area.y + 14f;

            GUI.Label(new Rect(x, y, width, 24f), "GRID GROWTH", headingStyle);
            y += 31f;
            GUI.Label(new Rect(x, y + 3f, 40f, 22f), "Seed", mutedStyle);
            seedText = GUI.TextField(
                new Rect(x + 42f, y, 92f, 26f),
                seedText,
                11);
            if (GUI.Button(new Rect(x + 142f, y, 70f, 26f), "+ CELL"))
            {
                GridCoordinate added = gridRuntime.GrowActive();
                statusMessage = $"Unlocked {added}.";
            }

            if (GUI.Button(new Rect(x + 218f, y, width - 218f, 26f), "+ 5"))
            {
                gridRuntime.GrowWeapon(gridRuntime.ActiveWeaponIndex, 5);
                statusMessage = "Unlocked five deterministic frontier cells.";
            }

            y += 34f;
            if (GUI.Button(new Rect(x, y, width, 27f), "RESET GRID FROM SEED"))
            {
                int seed = ParseSeed();
                gridRuntime.ResetActive(seed);
                statusMessage = $"Grid reset to root with seed {seed}.";
            }

            y += 43f;
            GUI.Label(new Rect(x, y, width, 24f), "ARTIFACT PALETTE", headingStyle);
            y += 30f;
            IReadOnlyList<ArtifactDefinitionData> definitions =
                gridRuntime.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                ArtifactDefinitionData definition = definitions[index];
                bool selected =
                    activeTool == GridTool.Place &&
                    string.Equals(
                        selectedDefinitionId,
                        definition.DefinitionId,
                        StringComparison.Ordinal);
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = selected
                    ? definition.DisplayColor
                    : Color.Lerp(definition.DisplayColor, Color.gray, 0.52f);
                if (GUI.Button(
                    new Rect(x, y, width, 30f),
                    $"{definition.DisplayName}     {FormatModifiers(definition)}",
                    selected ? selectedTabStyle : tabStyle))
                {
                    selectedDefinitionId = definition.DefinitionId;
                    activeTool = GridTool.Place;
                    statusMessage =
                        $"{definition.DisplayName} selected. Click a cell to place.";
                }

                GUI.backgroundColor = previousBackground;
                y += 34f;
            }

            y += 3f;
            float half = (width - 6f) * 0.5f;
            if (GUI.Button(
                new Rect(x, y, half, 28f),
                $"ROTATE  {selectedRotation * 90} DEG"))
            {
                RotateSelection();
            }

            Color priorBackground = GUI.backgroundColor;
            if (activeTool == GridTool.Remove)
            {
                GUI.backgroundColor = new Color(0.75f, 0.28f, 0.22f);
            }

            if (GUI.Button(
                new Rect(x + half + 6f, y, half, 28f),
                "REMOVE TOOL"))
            {
                activeTool = GridTool.Remove;
                statusMessage = "Remove tool active. Click an occupied cell.";
            }

            GUI.backgroundColor = priorBackground;
            y += 43f;
            DrawStats(new Rect(x, y, width, area.yMax - y - 12f));
        }

        private void DrawStats(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 24f), "RESOLVED MODIFIERS", headingStyle);
            WeaponGridModifierSummary summary =
                gridRuntime.GetModifierSummary();
            float y = area.y + 29f;
            DrawStatLine(area.x, ref y, area.width, "WEAPON 1", summary.Primary);
            DrawStatLine(area.x, ref y, area.width, "WEAPON 2", summary.Secondary);
            y += 4f;
            GUI.Label(
                new Rect(area.x, y, area.width, 20f),
                "EFFECTIVE LOADOUT",
                mutedStyle);
            y += 20f;
            WeaponGridModifiers effective = summary.Effective;
            GUI.Label(
                new Rect(area.x, y, area.width, 22f),
                $"+{effective.Damage:0.##} active damage   +{effective.MaxHealth:0.##} health   +{effective.MoveSpeed:0.##} move",
                bodyStyle);
        }

        private void DrawStatLine(
            float x,
            ref float y,
            float width,
            string label,
            WeaponGridModifiers modifiers)
        {
            GUI.Label(
                new Rect(x, y, width, 20f),
                $"{label}     DMG +{modifiers.Damage:0.##}   HP +{modifiers.MaxHealth:0.##}   SPD +{modifiers.MoveSpeed:0.##}",
                bodyStyle);
            y += 22f;
        }

        private void PlaceAt(GridCoordinate coordinate)
        {
            if (activeTool != GridTool.Place ||
                string.IsNullOrEmpty(selectedDefinitionId))
            {
                statusMessage = "Choose an artifact from the palette first.";
                return;
            }

            if (gridRuntime.TryPlaceActive(
                selectedDefinitionId,
                coordinate,
                selectedRotation,
                out string reason))
            {
                gridRuntime.TryGetDefinition(
                    selectedDefinitionId,
                    out ArtifactDefinitionData definition);
                statusMessage =
                    $"{definition?.DisplayName ?? "Artifact"} placed at {coordinate}.";
            }
            else
            {
                statusMessage = reason;
            }
        }

        private void RemoveAt(GridCoordinate coordinate)
        {
            if (gridRuntime.TryRemoveActiveAt(
                coordinate,
                out ArtifactInstance removed))
            {
                string shortId = removed.InstanceId.Substring(
                    0,
                    Mathf.Min(6, removed.InstanceId.Length));
                statusMessage = $"Removed artifact {shortId}.";
            }
            else
            {
                statusMessage = $"No artifact occupies {coordinate}.";
            }
        }

        private void RotatePlacedAt(GridCoordinate coordinate)
        {
            if (gridRuntime.TryRotateActiveAt(
                coordinate,
                1,
                out string reason))
            {
                statusMessage = $"Rotated artifact at {coordinate}.";
            }
            else
            {
                statusMessage = reason;
            }
        }

        private void RotateSelection()
        {
            selectedRotation = (selectedRotation + 1) % 4;
            statusMessage = $"Artifact rotation: {selectedRotation * 90} degrees.";
        }

        private int ParseSeed()
        {
            if (int.TryParse(seedText, out int parsed))
            {
                return parsed;
            }

            int fallback = gridRuntime.ActiveGrid.Seed;
            seedText = fallback.ToString();
            return fallback;
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

            if (string.IsNullOrEmpty(seedText))
            {
                seedText = gridRuntime.ActiveGrid.Seed.ToString();
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

        private static bool IsPristine(WeaponGridState state)
        {
            return state != null &&
                state.GrowthStep == 0 &&
                state.UnlockedCells.Count == 1 &&
                state.Placements.Count == 0;
        }

        private void EnsureStyles()
        {
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }

            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.93f, 0.9f, 0.79f) }
            };
            headingStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.84f, 0.72f) }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.83f, 0.85f, 0.86f) }
            };
            mutedStyle ??= new GUIStyle(bodyStyle)
            {
                normal = { textColor = new Color(0.57f, 0.61f, 0.64f) }
            };
            centeredStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            tabStyle ??= new GUIStyle(GUI.skin.button)
            {
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

        private void DrawPanelBackground(Rect area)
        {
            DrawPanelBackground(area, new Color(0.055f, 0.062f, 0.07f, 0.99f));
        }

        private void DrawPanelBackground(Rect area, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, whiteTexture);
            GUI.color = previous;
        }

        private static string FormatModifiers(ArtifactDefinitionData definition)
        {
            var parts = new List<string>();
            IReadOnlyList<ArtifactStatModifier> modifiers = definition.Modifiers;
            for (int index = 0; index < modifiers.Count; index++)
            {
                ArtifactStatModifier modifier = modifiers[index];
                string stat = modifier.Stat == ArtifactStat.Damage
                    ? "DMG"
                    : modifier.Stat == ArtifactStat.MaxHealth
                        ? "HP"
                        : "SPD";
                parts.Add($"+{modifier.Amount:0.##} {stat}");
            }

            return string.Join("  ", parts);
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
