using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HomeStorageChest : MonoBehaviour
    {
        [SerializeField] private HomeInventoryController inventory;
        [SerializeField] private string chestId =
            PlayerProfile.DefaultChestId;
        [SerializeField] private string displayName = "Chest 1";
        private readonly HashSet<int> playerColliderIds =
            new HashSet<int>();

        public bool PlayerInside => playerColliderIds.Count > 0;
        public string ChestId => chestId;
        public string DisplayName => displayName;

        public void Configure(
            HomeInventoryController controller,
            string storageId,
            string label)
        {
            inventory = controller;
            chestId =
                string.IsNullOrWhiteSpace(storageId)
                    ? PlayerProfile.DefaultChestId
                    : storageId.Trim();
            displayName =
                string.IsNullOrWhiteSpace(label)
                    ? "Chest"
                    : label.Trim();
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Update()
        {
            if (PlayerInside &&
                inventory != null &&
                !inventory.IsOpen &&
                Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                inventory.OpenChest(
                    chestId,
                    displayName);
            }
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
            if (!PlayerInside ||
                inventory == null ||
                inventory.IsOpen)
            {
                return;
            }

            Rect prompt = new Rect(
                Screen.width * 0.5f - 160f,
                Screen.height - 92f,
                320f,
                46f);
            LoopSceneGui.DrawPanel(
                prompt,
                new Color(0.56f, 0.39f, 0.20f));
            GUI.Label(
                prompt,
                $"[E]  OPEN {displayName.ToUpperInvariant()}",
                LoopSceneGui.Centered);
        }
    }
}
