using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HomeStorageChest : MonoBehaviour
    {
        private const int FocusHitCapacity = 32;
        private static readonly List<HomeStorageChest> ActiveChests =
            new List<HomeStorageChest>(4);
        private static readonly RaycastHit[] FocusHits =
            new RaycastHit[FocusHitCapacity];
        private static int focusedFrame = int.MinValue;
        private static HomeStorageChest focusedChest;
        private static int focusRefreshCount;

        [SerializeField] private HomeInventoryController inventory;
        [SerializeField] private string chestId =
            PlayerProfile.DefaultChestId;
        [SerializeField] private string displayName = "Chest 1";
        [SerializeField, Min(0.5f)] private float interactionDistance =
            LootInteractionPresentation.DefaultDistance;
        private Transform player;
        private HomeAnvil anvil;
        private float nextResolveAt;

        public bool PlayerInside => CanInteract;
        public bool CanInteract => ReferenceEquals(
            ResolveFocusedChest(),
            this);
        public string ChestId => chestId;
        public string DisplayName => displayName;
        public static int FocusRefreshCount => focusRefreshCount;

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

        private void OnEnable()
        {
            if (!ActiveChests.Contains(this))
            {
                ActiveChests.Add(this);
            }
            focusedFrame = int.MinValue;
        }

        private void OnDisable()
        {
            ActiveChests.Remove(this);
            if (ReferenceEquals(focusedChest, this))
            {
                focusedChest = null;
            }
            focusedFrame = int.MinValue;
        }

        private void Update()
        {
            ResolvePlayer();
            anvil ??= FindFirstObjectByType<HomeAnvil>();
            if (CanInteract &&
                inventory != null &&
                !inventory.IsOpen &&
                (anvil == null || !anvil.IsOpen) &&
                PlayerControlBindings.WasPressedThisFrame(
                    Keyboard.current,
                    PlayerControl.Interact))
            {
                inventory.OpenChest(
                    chestId,
                    displayName);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint ||
                !CanInteract ||
                inventory == null ||
                inventory.IsOpen ||
                (anvil != null && anvil.IsOpen))
            {
                return;
            }

            LootInteractionPresentation.DrawPrompt("Open Chest");
        }

        private void ResolvePlayer()
        {
            if (player != null || Time.unscaledTime < nextResolveAt)
            {
                return;
            }
            nextResolveAt = Time.unscaledTime + 0.5f;
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        private static HomeStorageChest ResolveFocusedChest()
        {
            int frame = Time.frameCount;
            if (focusedFrame == frame)
            {
                return focusedChest;
            }

            focusedFrame = frame;
            focusRefreshCount++;
            focusedChest = null;
            for (int index = ActiveChests.Count - 1;
                 index >= 0;
                 index--)
            {
                if (ActiveChests[index] == null)
                {
                    ActiveChests.RemoveAt(index);
                }
            }
            Transform sharedPlayer = null;
            for (int index = 0; index < ActiveChests.Count; index++)
            {
                ActiveChests[index].ResolvePlayer();
                sharedPlayer ??= ActiveChests[index].player;
            }

            Camera camera = Camera.main;
            if (sharedPlayer == null || camera == null)
            {
                return null;
            }

            Ray ray = camera.ScreenPointToRay(
                LootInteractionPresentation.CalculateAimPoint(
                    camera,
                    sharedPlayer,
                    Screen.width,
                    Screen.height));
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                FocusHits,
                camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            int nearestIndex = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                Transform hitTransform =
                    FocusHits[index].collider.transform;
                if (hitTransform.IsChildOf(sharedPlayer) ||
                    FocusHits[index].distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = FocusHits[index].distance;
                nearestIndex = index;
            }
            if (nearestIndex < 0)
            {
                return null;
            }

            Transform nearestHit =
                FocusHits[nearestIndex].collider.transform;
            for (int index = 0; index < ActiveChests.Count; index++)
            {
                HomeStorageChest chest = ActiveChests[index];
                Transform target = chest.transform.parent != null
                    ? chest.transform.parent
                    : chest.transform;
                if ((nearestHit == target ||
                     nearestHit.IsChildOf(target)) &&
                    LootInteractionPresentation.
                        IsWithinInteractionDistance(
                            sharedPlayer,
                            target,
                            chest.interactionDistance))
                {
                    focusedChest = chest;
                    break;
                }
            }
            return focusedChest;
        }

        public static void ResetFocusCacheForTests()
        {
            focusedFrame = int.MinValue;
            focusedChest = null;
            focusRefreshCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveChests.Clear();
            ResetFocusCacheForTests();
        }
    }
}
