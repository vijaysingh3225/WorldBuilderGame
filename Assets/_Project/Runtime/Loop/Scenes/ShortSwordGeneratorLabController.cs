using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public enum SwordGeneratorFamily
    {
        ShortSword = 0,
        ColumnBlade = 1
    }

    public enum UnifiedSwordCategory
    {
        Cruciform = 0,
        Leafblade = 1,
        Legionary = 2,
        Piercer = 3,
        ColumnSquare = 4,
        ColumnFlatThin = 5,
        ColumnWideFlat = 6
    }

    [DisallowMultipleComponent]
    public sealed class ShortSwordGeneratorLabController : MonoBehaviour
    {
        private const int GenerationLockColumns = 2;
        private const float GenerationLockButtonHeight = 28f;
        private const float GenerationLockButtonGap = 6f;

        [SerializeField] private ProceduralShortSwordGenerator generator;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private ProceduralColumnBladeGenerator
            columnBladeGenerator;
        [SerializeField] private Transform columnBladePresentationRoot;
        [SerializeField] private SwordGeneratorFamily selectedFamily =
            SwordGeneratorFamily.ShortSword;
        [SerializeField] private bool categoryLocked;
        [SerializeField] private UnifiedSwordCategory lockedCategory;
        [SerializeField, Min(0.01f)] private float rotationSensitivity = 0.22f;
        [SerializeField, Min(0.0001f)] private float zoomSensitivity = 0.225f;
        [SerializeField, Min(0.0001f)] private float panSensitivity = 0.0018f;
        [SerializeField, Min(0.01f)] private float minimumCameraDistance = 0.18f;
        [SerializeField, Min(0.1f)] private float maximumCameraDistance = 8f;

        private float rotationYaw = -18f;
        private float rotationPitch = -4f;
        private bool draggingPreview;
        private Camera presentationCamera;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;
        private float initialCameraFieldOfView;
        private bool hasInitialCameraPose;
        private Vector2 generationLocksScroll;
        private Vector2 columnBladeAttributesScroll;
        private int currentUnifiedSeed;
        private bool hasUnifiedGeneration;

        public ProceduralShortSwordGenerator Generator => generator;
        public ProceduralColumnBladeGenerator ColumnBladeGenerator =>
            columnBladeGenerator;
        public SwordGeneratorFamily SelectedFamily => selectedFamily;
        public UnifiedSwordCategory CurrentCategory =>
            ResolveCurrentCategory();
        public UnifiedSwordCategory? LockedCategory =>
            categoryLocked ? lockedCategory : null;

        public void Configure(
            ProceduralShortSwordGenerator swordGenerator,
            Transform swordPresentationRoot)
        {
            generator = swordGenerator;
            presentationRoot = swordPresentationRoot;
        }

        public void Configure(
            ProceduralShortSwordGenerator swordGenerator,
            Transform swordPresentationRoot,
            ProceduralColumnBladeGenerator newColumnBladeGenerator,
            Transform newColumnBladePresentationRoot)
        {
            Configure(swordGenerator, swordPresentationRoot);
            columnBladeGenerator = newColumnBladeGenerator;
            columnBladePresentationRoot = newColumnBladePresentationRoot;
            generator?.SetGenerateOnStart(false);
            columnBladeGenerator?.SetGenerateOnStart(false);
        }

        private void Awake()
        {
            generator?.SetGenerateOnStart(false);
            columnBladeGenerator?.SetGenerateOnStart(false);
        }

        private void Start()
        {
            GameplaySceneRuntime.ShowCursor();
            presentationCamera = Camera.main;
            CaptureInitialCameraPose();
            GenerateUnified(1201);
            ApplyPresentationRotation();
        }

        private void Update()
        {
            GameplaySceneRuntime.ShowCursor();
            UpdatePreviewControls();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.gKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame))
            {
                GenerateNext();
            }
            if (keyboard != null &&
                selectedFamily == SwordGeneratorFamily.ShortSword &&
                keyboard.cKey.wasPressedThisFrame)
            {
                generator?.CrackBlade();
            }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                FramePreview();
            }
        }

        private void OnGUI()
        {
            LoopSceneGui.DrawTranslucentBackdrop(
                new Rect(0f, 0f, Screen.width, Screen.height),
                0.20f);
            DrawActiveAttributesPanel();

            float margin = 28f;
            float panelWidth = Mathf.Min(390f, Screen.width * 0.31f);
            Rect panel = new Rect(
                margin,
                margin,
                panelWidth,
                Screen.height - margin * 2f);
            LoopSceneGui.DrawSection(panel);

            float x = panel.x + 24f;
            float width = panel.width - 48f;
            float y = panel.y + 18f;
            bool columnBlade =
                selectedFamily == SwordGeneratorFamily.ColumnBlade;
            GUI.Label(
                new Rect(x, y, width, 34f),
                "UNIFIED SWORD GENERATOR",
                LoopSceneGui.Title);
            y += 42f;
            GUI.Label(
                new Rect(x, y, width, 52f),
                "One seeded tree spanning pointed short swords and planar column blades. The selected category controls which child traits are available.",
                LoopSceneGui.Body);
            y += 58f;

            GUI.Label(
                new Rect(x, y, width, 38f),
                "Drag: rotate  /  Alt-drag or middle-drag: pan\n" +
                "Wheel: dolly  /  F: frame",
                LoopSceneGui.Muted);
            y += 44f;

            float generateWidth = columnBlade ? width : width * 0.64f;
            if (GUI.Button(
                    new Rect(
                        x,
                        y,
                        columnBlade ? width : generateWidth - 5f,
                        52f),
                    "GENERATE NEW SWORD   [G / SPACE]",
                    LoopSceneGui.Button))
            {
                GenerateNext();
            }
            if (!columnBlade && GUI.Button(
                    new Rect(
                        x + generateWidth + 5f,
                        y,
                        width - generateWidth - 5f,
                        52f),
                    generator != null && generator.IsBladeCracked
                        ? "REROLL CRACK  [C]"
                        : "CRACK SWORD  [C]",
                    LoopSceneGui.Button))
            {
                generator?.CrackBlade();
            }
            y += 70f;

            if (columnBlade)
            {
                DrawColumnBladeOverview(x, ref y, width);
                return;
            }

            DrawShortSwordOverview(x, ref y, width);
        }

        public void SelectFamily(SwordGeneratorFamily family)
        {
            if (family != SwordGeneratorFamily.ShortSword &&
                family != SwordGeneratorFamily.ColumnBlade)
            {
                return;
            }
            if (family == SwordGeneratorFamily.ColumnBlade &&
                columnBladeGenerator == null)
            {
                return;
            }

            selectedFamily = family;
            ApplyFamilyVisibility(generateIfNeeded: true);
            ApplyPresentationRotation();
        }

        private void DrawFamilyTabs(
            float x,
            ref float y,
            float width)
        {
            const float gap = 8f;
            float tabWidth = (width - gap) * 0.5f;
            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor =
                selectedFamily == SwordGeneratorFamily.ShortSword
                    ? new Color(0.72f, 0.64f, 0.43f, 1f)
                    : new Color(0.48f, 0.50f, 0.48f, 1f);
            if (GUI.Button(
                    new Rect(x, y, tabWidth, 36f),
                    "SHORT SWORD",
                    LoopSceneGui.Button))
            {
                SelectFamily(SwordGeneratorFamily.ShortSword);
            }

            GUI.backgroundColor =
                selectedFamily == SwordGeneratorFamily.ColumnBlade
                    ? new Color(0.58f, 0.66f, 0.55f, 1f)
                    : new Color(0.48f, 0.50f, 0.48f, 1f);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = columnBladeGenerator != null;
            if (GUI.Button(
                    new Rect(x + tabWidth + gap, y, tabWidth, 36f),
                    "COLUMN BLADE",
                    LoopSceneGui.Button))
            {
                SelectFamily(SwordGeneratorFamily.ColumnBlade);
            }
            GUI.enabled = previousEnabled;
            GUI.backgroundColor = previousBackground;
            y += 48f;
        }

        private void DrawShortSwordOverview(
            float x,
            ref float y,
            float width)
        {
            if (generator == null || !generator.HasGeneratedSword)
            {
                GUI.Label(
                    new Rect(x, y, width, 40f),
                    "Enter Play Mode to generate the first sword.",
                    LoopSceneGui.Muted);
                return;
            }

            ProceduralShortSwordDefinition sword =
                generator.CurrentDefinition;
            GUI.Label(
                new Rect(x, y, width, 26f),
                $"SEED  {sword.Seed}",
                LoopSceneGui.Heading);
            y += 34f;
            DrawSectionRow(
                x,
                ref y,
                width,
                "CATEGORY",
                sword.Family.ToString());
            DrawSectionRow(
                x,
                ref y,
                width,
                "BLADE",
                $"{sword.BladeProfile} / {sword.BladeBackStyle}");
            DrawSectionRow(
                x,
                ref y,
                width,
                "LENGTH",
                $"{sword.BladeLength:0.000} m");
            DrawSectionRow(
                x,
                ref y,
                width,
                "WIDTH / THICKNESS",
                $"{sword.BladeWidth:0.000} / {sword.BladeThickness:0.000} m");
            DrawSectionRow(
                x,
                ref y,
                width,
                "GUARD",
                $"{sword.GuardConstruction}  ·  " +
                $"{sword.GuardCurveSegments}×{sword.GuardCrossSectionSides} facets");
            DrawSectionRow(x, ref y, width, "METAL", sword.MetalFamily.ToString());
            DrawSectionRow(
                x,
                ref y,
                width,
                "HANDLE",
                $"{sword.HandleProfile} / {sword.GripStyle}");
            DrawSectionRow(x, ref y, width, "GRIP COLOR", sword.GripColor.ToString());
            DrawSectionRow(x, ref y, width, "HILT", sword.HiltProfile.ToString());
            string ornament = sword.OrnamentStyle is
                ShortSwordOrnamentStyle.GuardGem or
                ShortSwordOrnamentStyle.PommelGem
                    ? $"{sword.OrnamentStyle} / {sword.GemFamily} {sword.GemCut}"
                    : sword.OrnamentStyle.ToString();
            DrawSectionRow(x, ref y, width, "ORNAMENT", ornament);
            DrawSectionRow(
                x,
                ref y,
                width,
                "TOTAL LENGTH",
                $"{sword.TotalLength:0.000} m");
            ShortSwordCombatProfile feel = sword.CombatProfile.IsValid
                ? sword.CombatProfile
                : ProceduralShortSwordGenerator.CalculateCombatProfile(sword);
            DrawSectionRow(
                x,
                ref y,
                width,
                "HEFT / HANDLING",
                $"{feel.Heft * 100f:0} / {feel.Handling * 100f:0}");
            DrawSectionRow(
                x,
                ref y,
                width,
                "DAMAGE / ATTACK RATE",
                $"×{feel.DamageMultiplier:0.000} / ×{feel.AttackSpeedMultiplier:0.000}");
            DrawSectionRow(
                x,
                ref y,
                width,
                "IMPACT PAUSE / STAGGER",
                $"{feel.HitPauseDuration * 1000f:0} ms / {feel.StaggerDuration:0.000} s");

            y += 12f;
            GUI.Label(
                new Rect(x, y, width, 44f),
                generator.IsBladeCracked
                    ? $"Fracture preview {generator.FractureRevision}: " +
                        $"{generator.MainFractureCount} diagonal breaks / " +
                        $"{generator.MissingFracturePieceCount} missing chips"
                    : "Current parts: Blade  /  Guard  /  Handle  /  Hilt-Pommel",
                LoopSceneGui.Muted);
        }

        private void DrawColumnBladeOverview(
            float x,
            ref float y,
            float width)
        {
            if (columnBladeGenerator == null ||
                !columnBladeGenerator.HasGeneratedSword)
            {
                GUI.Label(
                    new Rect(x, y, width, 40f),
                    "Select the tab in Play Mode to generate the first column blade.",
                    LoopSceneGui.Muted);
                return;
            }

            ProceduralColumnBladeDefinition blade =
                columnBladeGenerator.CurrentDefinition;
            GUI.Label(
                new Rect(x, y, width, 26f),
                $"SEED  {blade.Seed}",
                LoopSceneGui.Heading);
            y += 34f;
            DrawSectionRow(
                x,
                ref y,
                width,
                "MATERIAL",
                blade.BladeMaterial.ToString());
            DrawSectionRow(
                x,
                ref y,
                width,
                "BLADE CATEGORY",
                blade.ShapeCategory.ToString());
            DrawSectionRow(
                x,
                ref y,
                width,
                "SECTION",
                $"{blade.SectionProfile} · core {blade.BladeCoreWidth:0.000} m");
            DrawSectionRow(
                x,
                ref y,
                width,
                "EDGES",
                blade.EdgeStyle == ColumnBladeEdgeStyle.TwinSideEdges
                    ? $"Twin wedges · {blade.BladeEdgeWidth:0.000} m / side"
                    : "Plain · transition chamfers only");
            DrawSectionRow(
                x,
                ref y,
                width,
                "ENGRAVING",
                blade.PrimaryEngraving == ColumnBladeEngravingStyle.None
                    ? "None"
                    : blade.PrimaryEngraving ==
                        ColumnBladeEngravingStyle.SilhouetteInset
                        ? $"Silhouette · {blade.SilhouetteWallProfile} · " +
                          $"{ProceduralColumnBladeGenerator.ResolveSilhouetteWallRun(blade) * 1000f:0.0} mm run"
                    : $"{(blade.EngravingPath == ColumnBladeEngravingPath.Forked ? "Forked Line" : "Straight Line")} · " +
                      $"{blade.EngravingTermination} · " +
                      $"{(blade.EngravingAllFourSides ? "Four Faces" : "Opposite Faces")} · " +
                      $"{ProceduralColumnBladeGenerator.ResolveEngravingWidth(blade):0.000} m · " +
                      blade.EngravingFill.ToString());
            DrawSectionRow(
                x,
                ref y,
                width,
                "MESH LENGTH",
                $"{blade.BladeLength:0.000} m");
            DrawSectionRow(
                x,
                ref y,
                width,
                "TOP CUT",
                blade.TopProfile == ColumnBladeTopProfile.Flat
                    ? "Flat"
                    : $"{blade.TopProfile} · {blade.TopSlantRise:0.000} m rise");
            DrawSectionRow(
                x,
                ref y,
                width,
                "BLADE W / T",
                $"{blade.BladeWidth:0.000} / {blade.BladeThickness:0.000} m");
            DrawSectionRow(
                x,
                ref y,
                width,
                "GUARD PLAN",
                blade.GuardProfile == ColumnBladeGuardProfile.Ring
                    ? $"Ring / {blade.GuardWidth:0.000} × " +
                      $"{blade.GuardHeight:0.000} m · palette " +
                      $"{blade.GuardColorVariant + 1}"
                    : $"{blade.GuardProfile} / " +
                      $"{blade.GuardWidth:0.000} × " +
                      $"{blade.GuardDepth:0.000} m");
            DrawSectionRow(
                x,
                ref y,
                width,
                "GUARD / GRIP",
                $"{blade.GuardHeight:0.000} m high · " +
                $"{blade.HandleProfile} / {blade.HandleCrossSection}");
            DrawSectionRow(
                x,
                ref y,
                width,
                "GRIP / POMMEL",
                $"{blade.GripStyle} / {blade.PommelProfile}");
            DrawSectionRow(
                x,
                ref y,
                width,
                "ACCENT",
                blade.AccentPalette.ToString());
            DrawSectionRow(
                x,
                ref y,
                width,
                "ASSEMBLED LENGTH",
                $"{blade.AssembledLength:0.000} m");

            y += 12f;
            GUI.Label(
                new Rect(x, y, width, 44f),
                "Current parts: Column Blade / Column Guard / Short Sword Handle / Short Sword Pommel",
                LoopSceneGui.Muted);
        }

        private void GenerateNext()
        {
            int seed = hasUnifiedGeneration
                ? unchecked(currentUnifiedSeed + 1)
                : 1201;
            GenerateUnified(seed);
        }

        public UnifiedSwordCategory Generate(int seed)
        {
            GenerateUnified(seed);
            return ResolveCurrentCategory();
        }

        public void ToggleCategoryLock(UnifiedSwordCategory category)
        {
            if (!System.Enum.IsDefined(typeof(UnifiedSwordCategory), category))
            {
                return;
            }
            bool active = categoryLocked && lockedCategory == category;
            categoryLocked = !active;
            lockedCategory = category;
            GenerateUnified(hasUnifiedGeneration
                ? currentUnifiedSeed
                : 1201);
        }

        private void ApplyFamilyVisibility(bool generateIfNeeded)
        {
            if (selectedFamily == SwordGeneratorFamily.ColumnBlade &&
                columnBladeGenerator == null)
            {
                selectedFamily = SwordGeneratorFamily.ShortSword;
            }

            bool showColumn =
                selectedFamily == SwordGeneratorFamily.ColumnBlade;
            if (presentationRoot != null &&
                presentationRoot.gameObject.activeSelf == showColumn)
            {
                presentationRoot.gameObject.SetActive(!showColumn);
            }
            if (columnBladePresentationRoot != null &&
                columnBladePresentationRoot.gameObject.activeSelf !=
                    showColumn)
            {
                columnBladePresentationRoot.gameObject.SetActive(showColumn);
            }

            if (!generateIfNeeded)
            {
                return;
            }
            if (showColumn &&
                columnBladeGenerator != null &&
                !columnBladeGenerator.HasGeneratedSword)
            {
                columnBladeGenerator.GenerateNext();
            }
            else if (!showColumn &&
                generator != null &&
                !generator.HasGeneratedSword)
            {
                generator.GenerateNext();
            }
        }

        private void UpdatePreviewControls()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            Vector2 guiPointer = new Vector2(
                pointer.x,
                Screen.height - pointer.y);
            bool pointerInPreview =
                !CalculatePanelRect(Screen.width, Screen.height)
                    .Contains(guiPointer) &&
                !CalculateGenerationLocksPanelRect(
                        Screen.width,
                        Screen.height)
                    .Contains(guiPointer);

            if ((mouse.leftButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame) &&
                pointerInPreview)
            {
                draggingPreview = true;
            }
            if (!mouse.leftButton.isPressed && !mouse.middleButton.isPressed)
            {
                draggingPreview = false;
            }
            if (draggingPreview &&
                (mouse.leftButton.isPressed || mouse.middleButton.isPressed))
            {
                Vector2 delta = mouse.delta.ReadValue();
                Keyboard keyboard = Keyboard.current;
                bool altHeld = keyboard != null &&
                    (keyboard.leftAltKey.isPressed ||
                     keyboard.rightAltKey.isPressed);
                if (mouse.middleButton.isPressed || altHeld)
                {
                    PanPreview(delta);
                }
                else
                {
                    rotationYaw = Mathf.Repeat(
                        rotationYaw - delta.x * rotationSensitivity,
                        360f);
                    rotationPitch = Mathf.Clamp(
                        rotationPitch + delta.y * rotationSensitivity,
                        -89f,
                        89f);
                    ApplyPresentationRotation();
                }
            }

            float scroll = pointerInPreview
                ? mouse.scroll.ReadValue().y
                : 0f;
            if (Mathf.Abs(scroll) <= 0.01f)
            {
                return;
            }
            presentationCamera ??= Camera.main;
            if (presentationCamera != null)
            {
                DollyPreview(scroll);
            }
        }

        private Transform ActivePresentationRoot =>
            selectedFamily == SwordGeneratorFamily.ColumnBlade
                ? columnBladePresentationRoot
                : presentationRoot;

        public static UnifiedSwordCategory ResolveGeneratedCategory(int seed)
        {
            var random = new System.Random(
                unchecked(seed * 4194301 + 1217));
            return (UnifiedSwordCategory)random.Next(0, 7);
        }

        private void GenerateUnified(int seed)
        {
            UnifiedSwordCategory category = categoryLocked
                ? lockedCategory
                : ResolveGeneratedCategory(seed);
            currentUnifiedSeed = seed;
            hasUnifiedGeneration = true;
            if (TryResolveShortSwordFamily(
                    category,
                    out ShortSwordFamily shortFamily))
            {
                selectedFamily = SwordGeneratorFamily.ShortSword;
                SetPresentationVisibility(showColumn: false);
                generator?.GenerateForFamily(seed, shortFamily);
            }
            else
            {
                selectedFamily = SwordGeneratorFamily.ColumnBlade;
                SetPresentationVisibility(showColumn: true);
                if (columnBladeGenerator != null)
                {
                    columnBladeGenerator.GenerateForShapeCategory(
                        seed,
                        ResolveColumnShapeCategory(category));
                }
            }
            ApplyPresentationRotation();
        }

        private void SetPresentationVisibility(bool showColumn)
        {
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(!showColumn);
            }
            if (columnBladePresentationRoot != null)
            {
                columnBladePresentationRoot.gameObject.SetActive(showColumn);
            }
        }

        private UnifiedSwordCategory ResolveCurrentCategory()
        {
            if (selectedFamily == SwordGeneratorFamily.ColumnBlade &&
                columnBladeGenerator != null &&
                columnBladeGenerator.HasGeneratedSword)
            {
                return columnBladeGenerator.CurrentDefinition.ShapeCategory
                    switch
                    {
                        ColumnBladeShapeCategory.FlatThin =>
                            UnifiedSwordCategory.ColumnFlatThin,
                        ColumnBladeShapeCategory.WideFlat =>
                            UnifiedSwordCategory.ColumnWideFlat,
                        _ => UnifiedSwordCategory.ColumnSquare
                    };
            }
            if (generator != null && generator.HasGeneratedSword)
            {
                return generator.CurrentDefinition.Family switch
                {
                    ShortSwordFamily.Leafblade =>
                        UnifiedSwordCategory.Leafblade,
                    ShortSwordFamily.Legionary =>
                        UnifiedSwordCategory.Legionary,
                    ShortSwordFamily.Piercer =>
                        UnifiedSwordCategory.Piercer,
                    _ => UnifiedSwordCategory.Cruciform
                };
            }
            return categoryLocked
                ? lockedCategory
                : UnifiedSwordCategory.Cruciform;
        }

        private static bool TryResolveShortSwordFamily(
            UnifiedSwordCategory category,
            out ShortSwordFamily family)
        {
            switch (category)
            {
                case UnifiedSwordCategory.Cruciform:
                    family = ShortSwordFamily.Cruciform;
                    return true;
                case UnifiedSwordCategory.Leafblade:
                    family = ShortSwordFamily.Leafblade;
                    return true;
                case UnifiedSwordCategory.Legionary:
                    family = ShortSwordFamily.Legionary;
                    return true;
                case UnifiedSwordCategory.Piercer:
                    family = ShortSwordFamily.Piercer;
                    return true;
                default:
                    family = default;
                    return false;
            }
        }

        private static ColumnBladeShapeCategory ResolveColumnShapeCategory(
            UnifiedSwordCategory category)
        {
            return category switch
            {
                UnifiedSwordCategory.ColumnFlatThin =>
                    ColumnBladeShapeCategory.FlatThin,
                UnifiedSwordCategory.ColumnWideFlat =>
                    ColumnBladeShapeCategory.WideFlat,
                _ => ColumnBladeShapeCategory.SquareBlock
            };
        }

        private void CaptureInitialCameraPose()
        {
            if (presentationCamera == null || hasInitialCameraPose)
            {
                return;
            }
            initialCameraPosition = presentationCamera.transform.position;
            initialCameraRotation = presentationCamera.transform.rotation;
            initialCameraFieldOfView = presentationCamera.fieldOfView;
            hasInitialCameraPose = true;
        }

        private void FramePreview()
        {
            presentationCamera ??= Camera.main;
            CaptureInitialCameraPose();
            if (!hasInitialCameraPose || presentationCamera == null)
            {
                return;
            }
            presentationCamera.transform.SetPositionAndRotation(
                initialCameraPosition,
                initialCameraRotation);
            presentationCamera.fieldOfView = initialCameraFieldOfView;
        }

        private void PanPreview(Vector2 pointerDelta)
        {
            presentationCamera ??= Camera.main;
            Transform activeRoot = ActivePresentationRoot;
            if (presentationCamera == null || activeRoot == null)
            {
                return;
            }
            float distance = Mathf.Max(
                minimumCameraDistance,
                Vector3.Dot(
                    activeRoot.position - presentationCamera.transform.position,
                    presentationCamera.transform.forward));
            float scale = panSensitivity * distance;
            presentationCamera.transform.position +=
                (-presentationCamera.transform.right * pointerDelta.x -
                 presentationCamera.transform.up * pointerDelta.y) * scale;
        }

        private void DollyPreview(float scrollDelta)
        {
            Transform activeRoot = ActivePresentationRoot;
            if (presentationCamera == null || activeRoot == null)
            {
                return;
            }
            Transform cameraTransform = presentationCamera.transform;
            float currentDistance = Mathf.Max(
                minimumCameraDistance,
                Vector3.Dot(
                    activeRoot.position - cameraTransform.position,
                    cameraTransform.forward));
            float nextDistance = CalculateZoomDistance(
                currentDistance,
                scrollDelta,
                zoomSensitivity,
                minimumCameraDistance,
                maximumCameraDistance);
            cameraTransform.position += cameraTransform.forward *
                (currentDistance - nextDistance);
        }

        private void ApplyPresentationRotation()
        {
            Transform activeRoot = ActivePresentationRoot;
            if (activeRoot == null)
            {
                return;
            }
            activeRoot.localRotation = Quaternion.Euler(
                rotationPitch,
                rotationYaw,
                0f);
        }

        public static float CalculateZoomFieldOfView(
            float currentFieldOfView,
            float scrollDelta,
            float sensitivity,
            float minimum,
            float maximum)
        {
            if (Mathf.Abs(scrollDelta) <= 0.0001f)
            {
                return Mathf.Clamp(currentFieldOfView, minimum, maximum);
            }

            // Input System scroll magnitudes vary substantially by mouse,
            // driver, and platform. Treat an observed wheel action as one
            // deterministic zoom step so tiny device deltas are not sluggish.
            float zoomed = currentFieldOfView * Mathf.Exp(
                -Mathf.Sign(scrollDelta) * Mathf.Max(0f, sensitivity));
            return Mathf.Clamp(zoomed, minimum, maximum);
        }

        public static float CalculateZoomDistance(
            float currentDistance,
            float scrollDelta,
            float sensitivity,
            float minimum,
            float maximum)
        {
            return CalculateZoomFieldOfView(
                currentDistance,
                scrollDelta,
                sensitivity,
                minimum,
                maximum);
        }

        private static Rect CalculatePanelRect(
            float screenWidth,
            float screenHeight)
        {
            const float margin = 28f;
            float panelWidth = Mathf.Min(390f, screenWidth * 0.31f);
            return new Rect(
                margin,
                margin,
                panelWidth,
                Mathf.Min(790f, screenHeight - margin * 2f));
        }

        private void DrawActiveAttributesPanel()
        {
            if (selectedFamily == SwordGeneratorFamily.ColumnBlade)
            {
                DrawColumnBladeAttributesPanel();
                return;
            }
            DrawGenerationLocksPanel();
        }

        private void DrawUnifiedCategorySection(
            float x,
            ref float y,
            float width)
        {
            GUI.Label(
                new Rect(x, y, width, 24f),
                "SWORD CATEGORY",
                LoopSceneGui.Heading);
            y += 27f;
            GUI.Label(
                new Rect(x, y, width, 34f),
                "No lock rolls across every category. Locking one reveals only its reachable child branches.",
                LoopSceneGui.Muted);
            y += 40f;
            const float gap = 6f;
            const float height = 34f;
            float buttonWidth = (width - gap) * 0.5f;
            UnifiedSwordCategory[] categories =
            {
                UnifiedSwordCategory.Cruciform,
                UnifiedSwordCategory.Leafblade,
                UnifiedSwordCategory.Legionary,
                UnifiedSwordCategory.Piercer,
                UnifiedSwordCategory.ColumnSquare,
                UnifiedSwordCategory.ColumnFlatThin,
                UnifiedSwordCategory.ColumnWideFlat
            };
            string[] labels =
            {
                "CRUCIFORM",
                "LEAFBLADE",
                "LEGIONARY",
                "PIERCER",
                "COLUMN SQUARE",
                "COLUMN THIN",
                "COLUMN WIDE"
            };
            UnifiedSwordCategory current = ResolveCurrentCategory();
            for (int index = 0; index < categories.Length; index++)
            {
                UnifiedSwordCategory category = categories[index];
                bool locked = categoryLocked && lockedCategory == category;
                bool generated = current == category;
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = locked
                    ? new Color(0.78f, 0.61f, 0.24f, 1f)
                    : generated
                        ? new Color(0.58f, 0.67f, 0.56f, 1f)
                        : new Color(0.52f, 0.54f, 0.52f, 1f);
                int column = index % 2;
                int row = index / 2;
                if (GUI.Button(
                        new Rect(
                            x + column * (buttonWidth + gap),
                            y + row * (height + gap),
                            buttonWidth,
                            height),
                        locked ? $"ON  {labels[index]}" : labels[index],
                        LoopSceneGui.Button))
                {
                    ToggleCategoryLock(category);
                }
                GUI.backgroundColor = previousBackground;
            }
            y += 4f * (height + gap) + 14f;
        }

        private void DrawColumnBladeAttributesPanel()
        {
            Rect panel = CalculateGenerationLocksPanelRect(
                Screen.width,
                Screen.height);
            LoopSceneGui.DrawSection(panel);

            const float inset = 20f;
            Rect viewport = new Rect(
                panel.x + inset,
                panel.y + 18f,
                panel.width - inset * 2f,
                panel.height - 36f);
            const float contentHeight = 1280f;
            float contentWidth = viewport.width - 16f;
            columnBladeAttributesScroll.y = Mathf.Clamp(
                columnBladeAttributesScroll.y,
                0f,
                Mathf.Max(0f, contentHeight - viewport.height));
            columnBladeAttributesScroll = GUI.BeginScrollView(
                viewport,
                columnBladeAttributesScroll,
                new Rect(0f, 0f, contentWidth, contentHeight));
            float x = 0f;
            float width = contentWidth;
            float y = 0f;
            GUI.Label(
                new Rect(x, y, width, 30f),
                "UNIFIED GENERATION TREE",
                LoopSceneGui.Title);
            y += 36f;
            GUI.Label(
                new Rect(x, y, width, 58f),
                "The current category is a Column Blade, so its material, section, top-cut, and engraving branches are available below.",
                LoopSceneGui.Body);
            y += 70f;
            DrawUnifiedCategorySection(x, ref y, width);

            GUI.Label(
                new Rect(x, y, width, 24f),
                "BLADE MATERIAL",
                LoopSceneGui.Heading);
            y += 26f;
            GUI.Label(
                new Rect(x, y, width, 32f),
                "No selection rolls stone, wood, or obsidian. Select one to lock it; select it again to return to random.",
                LoopSceneGui.Muted);
            y += 38f;
            const float gap = 7f;
            float buttonWidth = (width - gap * 2f) / 3f;
            ColumnBladeMaterial[] materials =
            {
                ColumnBladeMaterial.Stone,
                ColumnBladeMaterial.Wood,
                ColumnBladeMaterial.Obsidian
            };
            for (int index = 0; index < materials.Length; index++)
            {
                ColumnBladeMaterial material = materials[index];
                bool active = columnBladeGenerator != null &&
                    columnBladeGenerator.SelectedBladeMaterial == material;
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = active
                    ? new Color(0.58f, 0.67f, 0.56f, 1f)
                    : new Color(0.52f, 0.54f, 0.52f, 1f);
                if (GUI.Button(
                        new Rect(
                            x + index * (buttonWidth + gap),
                            y,
                            buttonWidth,
                            38f),
                        active
                            ? $"ON  {material.ToString().ToUpperInvariant()}"
                            : material.ToString().ToUpperInvariant(),
                        LoopSceneGui.Button) &&
                    columnBladeGenerator != null)
                {
                    columnBladeGenerator.ToggleBladeMaterialLock(material);
                }
                GUI.backgroundColor = previousBackground;
            }
            y += 58f;

            GUI.Label(
                new Rect(x, y, width, 24f),
                "GUARD PROFILE LOCK",
                LoopSceneGui.Heading);
            y += 28f;
            ColumnBladeGuardProfile[] guardProfiles =
            {
                ColumnBladeGuardProfile.WideBar,
                ColumnBladeGuardProfile.CompactBlock,
                ColumnBladeGuardProfile.Octagonal,
                ColumnBladeGuardProfile.Ring
            };
            string[] guardProfileLabels =
                { "WIDE BAR", "COMPACT", "OCTAGON", "RING" };
            float guardButtonWidth = (width - gap) * 0.5f;
            for (int index = 0; index < guardProfiles.Length; index++)
            {
                ColumnBladeGuardProfile profile = guardProfiles[index];
                bool active = columnBladeGenerator != null &&
                    columnBladeGenerator.IsGuardProfileLocked(profile);
                bool squareBlade = columnBladeGenerator != null &&
                    (columnBladeGenerator.LockedShapeCategory ==
                        ColumnBladeShapeCategory.SquareBlock ||
                     (!columnBladeGenerator.LockedShapeCategory.HasValue &&
                      columnBladeGenerator.HasGeneratedSword &&
                      columnBladeGenerator.CurrentDefinition.ShapeCategory ==
                        ColumnBladeShapeCategory.SquareBlock));
                Color previousBackground = GUI.backgroundColor;
                bool previousEnabled = GUI.enabled;
                GUI.backgroundColor = active
                    ? new Color(0.78f, 0.61f, 0.24f, 1f)
                    : new Color(0.52f, 0.54f, 0.52f, 1f);
                GUI.enabled = !(squareBlade &&
                    profile == ColumnBladeGuardProfile.Ring);
                int column = index % 2;
                int row = index / 2;
                if (GUI.Button(
                        new Rect(
                            x + column * (guardButtonWidth + gap),
                            y + row * 43f,
                            guardButtonWidth,
                            38f),
                        active
                            ? $"ON  {guardProfileLabels[index]}"
                            : guardProfileLabels[index],
                        LoopSceneGui.Button) &&
                    columnBladeGenerator != null)
                {
                    columnBladeGenerator.ToggleGuardProfileLock(profile);
                    RegenerateCurrentColumnBlade();
                }
                GUI.enabled = previousEnabled;
                GUI.backgroundColor = previousBackground;
            }
            y += 99f;

            GUI.Label(
                new Rect(x, y, width, 24f),
                "EDGE STYLE LOCK",
                LoopSceneGui.Heading);
            y += 28f;
            ColumnBladeEdgeStyle[] edgeStyles =
            {
                ColumnBladeEdgeStyle.Plain,
                ColumnBladeEdgeStyle.TwinSideEdges
            };
            string[] edgeStyleLabels = { "PLAIN", "SIDE BEVEL" };
            float halfButtonWidth = (width - gap) * 0.5f;
            for (int index = 0; index < edgeStyles.Length; index++)
            {
                ColumnBladeEdgeStyle style = edgeStyles[index];
                bool active = columnBladeGenerator != null &&
                    columnBladeGenerator.IsEdgeStyleLocked(style);
                bool squareLocked = columnBladeGenerator != null &&
                    (columnBladeGenerator.LockedShapeCategory ==
                        ColumnBladeShapeCategory.SquareBlock ||
                     (columnBladeGenerator.HasGeneratedSword &&
                      columnBladeGenerator.CurrentDefinition.ShapeCategory ==
                        ColumnBladeShapeCategory.SquareBlock));
                Color previousBackground = GUI.backgroundColor;
                bool previousEnabled = GUI.enabled;
                GUI.backgroundColor = active
                    ? new Color(0.78f, 0.61f, 0.24f, 1f)
                    : new Color(0.52f, 0.54f, 0.52f, 1f);
                GUI.enabled = !(squareLocked &&
                    style == ColumnBladeEdgeStyle.TwinSideEdges);
                if (GUI.Button(
                        new Rect(
                            x + index * (halfButtonWidth + gap),
                            y,
                            halfButtonWidth,
                            38f),
                        active
                            ? $"ON  {edgeStyleLabels[index]}"
                            : edgeStyleLabels[index],
                        LoopSceneGui.Button) &&
                    columnBladeGenerator != null)
                {
                    columnBladeGenerator.ToggleEdgeStyleLock(style);
                    RegenerateCurrentColumnBlade();
                }
                GUI.enabled = previousEnabled;
                GUI.backgroundColor = previousBackground;
            }
            y += 56f;

            GUI.Label(
                new Rect(x, y, width, 24f),
                "TOP CUT LOCK",
                LoopSceneGui.Heading);
            y += 28f;
            ColumnBladeTopProfile[] topProfiles =
            {
                ColumnBladeTopProfile.Flat,
                ColumnBladeTopProfile.SlightSlant,
                ColumnBladeTopProfile.SteepSlant
            };
            string[] topLabels = { "FLAT", "SLIGHT", "STEEP" };
            for (int index = 0; index < topProfiles.Length; index++)
            {
                ColumnBladeTopProfile profile = topProfiles[index];
                bool active = columnBladeGenerator != null &&
                    columnBladeGenerator.IsTopProfileLocked(profile);
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = active
                    ? new Color(0.78f, 0.61f, 0.24f, 1f)
                    : new Color(0.52f, 0.54f, 0.52f, 1f);
                if (GUI.Button(
                        new Rect(
                            x + index * (buttonWidth + gap),
                            y,
                            buttonWidth,
                            38f),
                        active
                            ? $"ON  {topLabels[index]}"
                            : topLabels[index],
                        LoopSceneGui.Button) &&
                    columnBladeGenerator != null)
                {
                    columnBladeGenerator.ToggleTopProfileLock(profile);
                    RegenerateCurrentColumnBlade();
                }
                GUI.backgroundColor = previousBackground;
            }
            y += 56f;

            GUI.Label(
                new Rect(x, y, width, 24f),
                "ENGRAVING STYLE LOCK",
                LoopSceneGui.Heading);
            y += 28f;
            ColumnBladeEngravingStyle[] engravingStyles =
            {
                ColumnBladeEngravingStyle.None,
                ColumnBladeEngravingStyle.StraightLine,
                ColumnBladeEngravingStyle.SilhouetteInset
            };
            string[] engravingLabels =
                { "NONE", "STRAIGHT LINE", "SILHOUETTE" };
            float engravingButtonWidth = (width - gap * 2f) / 3f;
            for (int index = 0; index < engravingStyles.Length; index++)
            {
                ColumnBladeEngravingStyle style = engravingStyles[index];
                bool active = columnBladeGenerator != null &&
                    columnBladeGenerator.IsEngravingStyleLocked(style);
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = active
                    ? new Color(0.78f, 0.61f, 0.24f, 1f)
                    : new Color(0.52f, 0.54f, 0.52f, 1f);
                if (GUI.Button(
                        new Rect(
                            x + index * (engravingButtonWidth + gap),
                            y,
                            engravingButtonWidth,
                            38f),
                        active
                            ? $"ON  {engravingLabels[index]}"
                            : engravingLabels[index],
                        LoopSceneGui.Button) &&
                    columnBladeGenerator != null)
                {
                    columnBladeGenerator.ToggleEngravingStyleLock(style);
                    RegenerateCurrentColumnBlade();
                }
                GUI.backgroundColor = previousBackground;
            }

            y += 56f;
            if (columnBladeGenerator != null &&
                columnBladeGenerator.CurrentDefinition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.SilhouetteInset)
            {
                GUI.Label(
                    new Rect(x, y, width, 24f),
                    "SILHOUETTE WALL LOCK",
                    LoopSceneGui.Heading);
                y += 28f;
                ColumnBladeSilhouetteWallProfile[] wallProfiles =
                {
                    ColumnBladeSilhouetteWallProfile.Straight,
                    ColumnBladeSilhouetteWallProfile.Slanted,
                    ColumnBladeSilhouetteWallProfile.DramaticSlant
                };
                string[] wallProfileLabels =
                    { "STRAIGHT", "SLANTED", "DRAMATIC" };
                for (int index = 0; index < wallProfiles.Length; index++)
                {
                    ColumnBladeSilhouetteWallProfile profile =
                        wallProfiles[index];
                    bool active = columnBladeGenerator
                        .IsSilhouetteWallProfileLocked(profile);
                    Color previousBackground = GUI.backgroundColor;
                    GUI.backgroundColor = active
                        ? new Color(0.78f, 0.61f, 0.24f, 1f)
                        : new Color(0.52f, 0.54f, 0.52f, 1f);
                    if (GUI.Button(
                            new Rect(
                                x + index * (engravingButtonWidth + gap),
                                y,
                                engravingButtonWidth,
                                38f),
                            active
                                ? $"ON  {wallProfileLabels[index]}"
                                : wallProfileLabels[index],
                            LoopSceneGui.Button))
                    {
                        columnBladeGenerator
                            .ToggleSilhouetteWallProfileLock(profile);
                        RegenerateCurrentColumnBlade();
                    }
                    GUI.backgroundColor = previousBackground;
                }
                y += 56f;
            }
            else if (columnBladeGenerator != null &&
                columnBladeGenerator.CurrentDefinition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.StraightLine)
            {
                GUI.Label(
                    new Rect(x, y, width, 24f),
                    "LINE PATH LOCK",
                    LoopSceneGui.Heading);
                y += 28f;
                ColumnBladeEngravingPath[] engravingPaths =
                {
                    ColumnBladeEngravingPath.Single,
                    ColumnBladeEngravingPath.Forked
                };
                string[] engravingPathLabels = { "SINGLE", "FORKED" };
                for (int index = 0; index < engravingPaths.Length; index++)
                {
                    ColumnBladeEngravingPath path = engravingPaths[index];
                    bool active = columnBladeGenerator
                        .IsEngravingPathLocked(path);
                    Color previousBackground = GUI.backgroundColor;
                    GUI.backgroundColor = active
                        ? new Color(0.78f, 0.61f, 0.24f, 1f)
                        : new Color(0.52f, 0.54f, 0.52f, 1f);
                    if (GUI.Button(
                            new Rect(
                                x + index * (engravingButtonWidth + gap),
                                y,
                                engravingButtonWidth,
                                38f),
                            active
                                ? $"ON  {engravingPathLabels[index]}"
                                : engravingPathLabels[index],
                            LoopSceneGui.Button))
                    {
                        columnBladeGenerator.ToggleEngravingPathLock(path);
                        RegenerateCurrentColumnBlade();
                    }
                    GUI.backgroundColor = previousBackground;
                }
                y += 56f;
            }

            GUI.Label(
                new Rect(x, y, width, 70f),
                "Line engravings begin at the guard and either reach the top cut or turn into a circle. Plain full-height lines vary in width; fork and circle strokes keep the established width. Silhouette inset traces every eligible face and cap with a coherent recessed border.",
                LoopSceneGui.Muted);
            GUI.EndScrollView();
        }

        private void DrawGenerationLocksPanel()
        {
            Rect panel = CalculateGenerationLocksPanelRect(
                Screen.width,
                Screen.height);
            LoopSceneGui.DrawSection(panel);

            float inset = 20f;
            float x = panel.x + inset;
            float width = panel.width - inset * 2f;
            float y = panel.y + 18f;
            GUI.Label(
                new Rect(x, y, width, 30f),
                "UNIFIED GENERATION TREE",
                LoopSceneGui.Title);
            y += 34f;
            GUI.Label(
                new Rect(x, y, width, 38f),
                "The current category is a pointed Short Sword. Its compatible child branches expand below.",
                LoopSceneGui.Body);
            y += 44f;

            DrawUnifiedCategorySection(x, ref y, width);

            int activeCount = (generator?.ActiveGenerationLockCount ?? 0) +
                (categoryLocked ? 1 : 0);
            GUI.Label(
                new Rect(x, y, width * 0.52f, 30f),
                $"ACTIVE LOCKS  {activeCount}",
                LoopSceneGui.Heading);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = generator != null && activeCount > 0;
            if (GUI.Button(
                    new Rect(
                        x + width * 0.56f,
                        y,
                        width * 0.44f,
                        30f),
                    "CLEAR ALL",
                    LoopSceneGui.Button))
            {
                generator.ClearGenerationLocks();
                categoryLocked = false;
                GenerateUnified(hasUnifiedGeneration
                    ? currentUnifiedSeed
                    : 1201);
            }
            GUI.enabled = previousEnabled;
            y += 38f;

            Rect viewport = new Rect(
                x,
                y,
                width,
                panel.yMax - y - inset);
            float contentWidth = width - 16f;
            float contentHeight =
                ShortSwordGenerationBranchCatalog.CalculateContentHeight(
                    GenerationLockColumns,
                    GenerationLockButtonHeight,
                    GenerationLockButtonGap);
            generationLocksScroll.y = Mathf.Clamp(
                generationLocksScroll.y,
                0f,
                Mathf.Max(0f, contentHeight - viewport.height));
            Rect content = new Rect(
                0f,
                0f,
                contentWidth,
                contentHeight);
            generationLocksScroll = GUI.BeginScrollView(
                viewport,
                generationLocksScroll,
                content);
            float contentY = 0f;
            string previousCategory = null;
            foreach (var group in ShortSwordGenerationBranchCatalog.Groups)
            {
                if (!ShouldShowShortSwordGroup(group.Decision))
                {
                    continue;
                }
                string category = group.Category.ToString();
                if (category != previousCategory)
                {
                    if (previousCategory != null)
                    {
                        contentY += 6f;
                    }
                    GUI.Label(
                        new Rect(0f, contentY, content.width, 26f),
                        category.ToUpperInvariant(),
                        LoopSceneGui.Title);
                    contentY += 32f;
                    previousCategory = category;
                }

                DrawLockGroup(
                    0f,
                    ref contentY,
                    content.width,
                    group);
            }
            GUI.EndScrollView();
        }

        private void DrawLockGroup(
            float x,
            ref float y,
            float width,
            ShortSwordGenerationBranchGroup group)
        {
            int visibleOptionCount = 0;
            for (int index = 0; index < group.Options.Count; index++)
            {
                if (IsShortSwordOptionReachable(
                        group.Decision,
                        group.Options[index].Value))
                {
                    visibleOptionCount++;
                }
            }
            if (visibleOptionCount == 0)
            {
                return;
            }

            GUI.Label(
                new Rect(x, y, width, 22f),
                new GUIContent(group.Heading, group.Tooltip),
                LoopSceneGui.Heading);
            y += 24f;
            float buttonWidth =
                (width - GenerationLockButtonGap) / GenerationLockColumns;
            int visibleIndex = 0;
            for (int index = 0; index < group.Options.Count; index++)
            {
                ShortSwordGenerationBranchOption option =
                    group.Options[index];
                if (!IsShortSwordOptionReachable(
                        group.Decision,
                        option.Value))
                {
                    continue;
                }
                int column = visibleIndex % GenerationLockColumns;
                int row = visibleIndex / GenerationLockColumns;
                bool active = generator != null &&
                    generator.IsGenerationLocked(
                        group.Decision,
                        option.Value);
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = active
                    ? new Color(0.78f, 0.61f, 0.24f, 1f)
                    : new Color(0.58f, 0.60f, 0.58f, 1f);
                string label = active
                    ? $"ON  {option.Label}"
                    : option.Label;
                if (GUI.Button(
                        new Rect(
                            x + column *
                                (buttonWidth + GenerationLockButtonGap),
                            y + row *
                                (GenerationLockButtonHeight +
                                 GenerationLockButtonGap),
                            buttonWidth,
                            GenerationLockButtonHeight),
                        new GUIContent(label, option.Tooltip),
                        LoopSceneGui.Button) &&
                    generator != null)
                {
                    generator.ToggleGenerationLock(
                        group.Decision,
                        option.Value);
                    RegenerateCurrentSword();
                }
                GUI.backgroundColor = previousBackground;
                visibleIndex++;
            }

            int rowCount = Mathf.CeilToInt(
                visibleOptionCount / (float)GenerationLockColumns);
            y += rowCount *
                (GenerationLockButtonHeight + GenerationLockButtonGap) + 12f;
        }

        private bool ShouldShowShortSwordGroup(
            ShortSwordGenerationDecision decision)
        {
            if (decision == ShortSwordGenerationDecision.Family ||
                generator == null ||
                !generator.HasGeneratedSword)
            {
                return false;
            }
            ProceduralShortSwordDefinition definition =
                generator.CurrentDefinition;
            if (decision == ShortSwordGenerationDecision.DirectionSide)
            {
                return ShortSwordGenerationBranchCatalog
                    .IsDirectionalBladeProfile(definition.BladeProfile);
            }
            if (decision == ShortSwordGenerationDecision.GemFamily ||
                decision == ShortSwordGenerationDecision.GemCut)
            {
                return definition.OrnamentStyle !=
                    ShortSwordOrnamentStyle.Plain;
            }
            return true;
        }

        private bool IsShortSwordOptionReachable(
            ShortSwordGenerationDecision decision,
            int value)
        {
            return generator != null &&
                generator.HasGeneratedSword &&
                ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                    generator.CurrentDefinition.Family,
                    decision,
                    value);
        }

        private void RegenerateCurrentSword()
        {
            GenerateUnified(hasUnifiedGeneration
                ? currentUnifiedSeed
                : 1201);
        }

        private void RegenerateCurrentColumnBlade()
        {
            GenerateUnified(hasUnifiedGeneration
                ? currentUnifiedSeed
                : 1201);
        }

        private static Rect CalculateGenerationLocksPanelRect(
            float screenWidth,
            float screenHeight)
        {
            const float margin = 28f;
            float panelWidth = Mathf.Min(470f, screenWidth * 0.35f);
            return new Rect(
                screenWidth - margin - panelWidth,
                margin,
                panelWidth,
                screenHeight - margin * 2f);
        }

        private static void DrawSectionRow(
            float x,
            ref float y,
            float width,
            string label,
            string value)
        {
            Rect row = new Rect(x, y, width, 26f);
            LoopSceneGui.DrawCell(row);
            GUI.Label(
                new Rect(row.x + 10f, row.y + 3f, row.width * 0.42f, 20f),
                label,
                LoopSceneGui.Muted);
            GUI.Label(
                new Rect(row.x + row.width * 0.42f, row.y + 3f, row.width * 0.56f - 10f, 20f),
                value,
                LoopSceneGui.Body);
            y += 30f;
        }
    }
}
