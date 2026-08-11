using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class ShortSwordGeneratorLabController : MonoBehaviour
    {
        [SerializeField] private ProceduralShortSwordGenerator generator;
        [SerializeField] private Transform presentationRoot;
        [SerializeField, Min(0.01f)] private float rotationSensitivity = 0.22f;
        [SerializeField, Min(0.0001f)] private float zoomSensitivity = 0.0045f;
        [SerializeField, Range(10f, 80f)] private float minimumFieldOfView = 12f;
        [SerializeField, Range(10f, 80f)] private float maximumFieldOfView = 58f;

        private float rotationYaw = -18f;
        private float rotationPitch = -4f;
        private bool draggingPreview;
        private Camera presentationCamera;

        public ProceduralShortSwordGenerator Generator => generator;

        public void Configure(
            ProceduralShortSwordGenerator swordGenerator,
            Transform swordPresentationRoot)
        {
            generator = swordGenerator;
            presentationRoot = swordPresentationRoot;
        }

        private void Start()
        {
            GameplaySceneRuntime.ShowCursor();
            presentationCamera = Camera.main;
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
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                generator?.CrackBlade();
            }
        }

        private void OnGUI()
        {
            LoopSceneGui.DrawTranslucentBackdrop(
                new Rect(0f, 0f, Screen.width, Screen.height),
                0.20f);

            float margin = 28f;
            float panelWidth = Mathf.Min(390f, Screen.width * 0.31f);
            Rect panel = new Rect(
                margin,
                margin,
                panelWidth,
                Mathf.Min(640f, Screen.height - margin * 2f));
            LoopSceneGui.DrawSection(panel);

            float x = panel.x + 24f;
            float width = panel.width - 48f;
            float y = panel.y + 22f;
            GUI.Label(
                new Rect(x, y, width, 34f),
                "SHORT SWORD GENERATOR",
                LoopSceneGui.Title);
            y += 42f;
            GUI.Label(
                new Rect(x, y, width, 52f),
                "A reusable four-part runtime prototype. Variation is intentionally restrained so every result remains a practical short sword.",
                LoopSceneGui.Body);
            y += 58f;

            GUI.Label(
                new Rect(x, y, width, 22f),
                "Drag the preview to rotate  /  Mouse wheel to zoom",
                LoopSceneGui.Muted);
            y += 28f;

            float generateWidth = width * 0.64f;
            if (GUI.Button(
                    new Rect(x, y, generateWidth - 5f, 52f),
                    "GENERATE NEW SWORD   [G / SPACE]",
                    LoopSceneGui.Button))
            {
                GenerateNext();
            }
            if (GUI.Button(
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

        private void GenerateNext()
        {
            if (generator == null)
            {
                return;
            }
            generator.GenerateNext();
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
                    .Contains(guiPointer);

            if (mouse.leftButton.wasPressedThisFrame &&
                pointerInPreview)
            {
                draggingPreview = true;
            }
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                draggingPreview = false;
            }
            if (draggingPreview && mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                rotationYaw = Mathf.Repeat(
                    rotationYaw - delta.x * rotationSensitivity,
                    360f);
                rotationPitch = Mathf.Clamp(
                    rotationPitch + delta.y * rotationSensitivity,
                    -72f,
                    72f);
                ApplyPresentationRotation();
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
                presentationCamera.fieldOfView = CalculateZoomFieldOfView(
                    presentationCamera.fieldOfView,
                    scroll,
                    zoomSensitivity,
                    minimumFieldOfView,
                    maximumFieldOfView);
            }
        }

        private void ApplyPresentationRotation()
        {
            if (presentationRoot == null)
            {
                return;
            }
            presentationRoot.localRotation = Quaternion.Euler(
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
            float zoomed = currentFieldOfView * Mathf.Exp(
                -scrollDelta * Mathf.Max(0f, sensitivity));
            return Mathf.Clamp(zoomed, minimum, maximum);
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
                Mathf.Min(640f, screenHeight - margin * 2f));
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
