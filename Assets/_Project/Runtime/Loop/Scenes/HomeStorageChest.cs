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
        private readonly HashSet<int> playerColliderIds =
            new HashSet<int>();

        public bool PlayerInside => playerColliderIds.Count > 0;

        public void Configure(HomeInventoryController controller)
        {
            inventory = controller;
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
                inventory.OpenChest();
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
                "[E]  OPEN CHEST",
                LoopSceneGui.Centered);
        }
    }
}
