using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaidObelisk : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] private int quadrantIndex;
        [SerializeField] private string displayName = "Obsidian Obelisk";
        [SerializeField] private Color monumentColor =
            new Color(0.055f, 0.035f, 0.075f, 1f);
        [SerializeField] private Renderer monumentRenderer;
        [SerializeField] private Light activationLight;
        [SerializeField] private RaidPrototypeController raidController;
        [SerializeField, Min(0f)] private float activatedLightIntensity = 18f;
        [SerializeField, Min(0f)] private float activatedLightRange = 20f;
        [SerializeField, Min(0f)] private float activatedEmissionMultiplier = 22f;

        private readonly HashSet<int> playerColliderIds =
            new HashSet<int>();
        private MaterialPropertyBlock propertyBlock;
        private bool activated;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId =
            Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");

        public int QuadrantIndex => quadrantIndex;
        public string DisplayName => displayName;
        public Color MonumentColor => monumentColor;
        public bool IsActivated => activated;
        public bool PlayerInside => playerColliderIds.Count > 0;

        public void Configure(
            int index,
            string label,
            Color color,
            RaidPrototypeController controller,
            Renderer renderer,
            Light glow)
        {
            quadrantIndex = Mathf.Clamp(index, 0, 3);
            displayName = string.IsNullOrWhiteSpace(label)
                ? $"Obelisk {quadrantIndex + 1}"
                : label.Trim();
            monumentColor = color;
            raidController = controller;
            monumentRenderer = renderer;
            activationLight = glow;
            ApplyVisualState();
        }

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            ApplyVisualState();
        }

        private void Update()
        {
            if (activated ||
                !PlayerInside ||
                Keyboard.current == null ||
                !Keyboard.current.fKey.wasPressedThisFrame)
            {
                return;
            }

            raidController ??=
                FindFirstObjectByType<RaidPrototypeController>();
            raidController?.TryActivateObelisk(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (GameplaySceneRuntime.IsPlayerCollider(other))
            {
                playerColliderIds.Add(other.GetInstanceID());
            }
        }

        private void OnTriggerExit(Collider other)
        {
            playerColliderIds.Remove(other.GetInstanceID());
        }

        private void OnDisable()
        {
            playerColliderIds.Clear();
        }

        private void OnGUI()
        {
            if (!PlayerInside || activated)
            {
                return;
            }

            Rect prompt = new Rect(
                Screen.width * 0.5f - 190f,
                Screen.height - 92f,
                380f,
                46f);
            LoopSceneGui.DrawPanel(prompt, monumentColor);
            GUI.Label(
                prompt,
                $"[F]  ACTIVATE {displayName.ToUpperInvariant()}",
                LoopSceneGui.Centered);
        }

        internal void MarkActivated()
        {
            if (activated)
            {
                return;
            }

            activated = true;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (monumentRenderer != null)
            {
                propertyBlock ??= new MaterialPropertyBlock();
                monumentRenderer.GetPropertyBlock(propertyBlock);
                Color surface = activated
                    ? Color.Lerp(monumentColor, Color.white, 0.18f)
                    : Color.Lerp(monumentColor, Color.black, 0.22f);
                Color emission = activated
                    ? monumentColor *
                        activatedEmissionMultiplier
                    : monumentColor * 0.08f;
                surface.a = 1f;
                emission.a = 1f;
                propertyBlock.SetColor(BaseColorId, surface);
                propertyBlock.SetColor(LegacyColorId, surface);
                propertyBlock.SetColor(EmissionColorId, emission);
                monumentRenderer.SetPropertyBlock(propertyBlock);
            }

            if (activationLight != null)
            {
                activationLight.color = Color.Lerp(
                    monumentColor,
                    Color.white,
                    0.14f);
                activationLight.intensity =
                    activatedLightIntensity;
                activationLight.range = activatedLightRange;
                activationLight.enabled = activated;
            }
        }
    }
}
