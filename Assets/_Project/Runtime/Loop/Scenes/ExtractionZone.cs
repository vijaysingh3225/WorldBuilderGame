using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ExtractionZone : MonoBehaviour
    {
        [SerializeField] private string displayName =
            "Forest Extraction";
        [SerializeField] private RaidPrototypeController raidController;
        [SerializeField] private WeaponGridSandboxToolkit gridToolkit;

        private readonly HashSet<int> playerColliderIds =
            new HashSet<int>();
        private bool extractionRequested;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? "Extraction"
                : displayName.Trim();
        public bool PlayerInside =>
            playerColliderIds.Count > 0;

        public void Configure(
            RaidPrototypeController controller,
            string zoneDisplayName = "Extraction")
        {
            raidController = controller;
            displayName = zoneDisplayName;
        }

        private void Awake()
        {
            Collider[] colliders =
                GetComponents<Collider>();
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                colliders[index].isTrigger = true;
            }
        }

        private void OnDisable()
        {
            playerColliderIds.Clear();
        }

        private void Update()
        {
            if (!PlayerInside ||
                extractionRequested)
            {
                return;
            }

            gridToolkit ??=
                FindFirstObjectByType<WeaponGridSandboxToolkit>();
            if (gridToolkit != null && gridToolkit.IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Interact))
            {
                raidController ??=
                    Object.FindFirstObjectByType<
                        RaidPrototypeController>();
                extractionRequested =
                    raidController != null &&
                    raidController.TryExtract();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (GameplaySceneRuntime.IsPlayerCollider(other))
            {
                playerColliderIds.Add(
                    other.GetInstanceID());
            }
        }

        private void OnTriggerExit(Collider other)
        {
            playerColliderIds.Remove(
                other.GetInstanceID());
        }

        private void OnGUI()
        {
            if (!PlayerInside ||
                extractionRequested)
            {
                return;
            }

            Rect panel = new Rect(
                Screen.width * 0.5f - 190f,
                Screen.height - 104f,
                380f,
                54f);
            LoopSceneGui.DrawPanel(
                panel,
                new Color(0.36f, 0.72f, 0.48f));
            GUI.Label(
                panel,
                $"[{PlayerControlBindings.KeyName(PlayerControlBindings.GetKey(PlayerControl.Interact))}]  EXTRACT  /  {DisplayName}",
                LoopSceneGui.Centered);
        }
    }
}
