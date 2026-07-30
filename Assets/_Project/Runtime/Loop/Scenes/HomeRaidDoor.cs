using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HomeRaidDoor : MonoBehaviour
    {
        [SerializeField] private HomeBaseController homeBase;
        private readonly HashSet<int> playerColliderIds =
            new HashSet<int>();
        private bool launchRequested;

        public bool PlayerInside => playerColliderIds.Count > 0;

        public void Configure(HomeBaseController controller)
        {
            homeBase = controller;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Update()
        {
            if (!launchRequested &&
                PlayerInside &&
                Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                launchRequested =
                    homeBase != null &&
                    homeBase.TryLaunchRaid();
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
            if (!PlayerInside || launchRequested)
            {
                return;
            }

            Rect prompt = new Rect(
                Screen.width * 0.5f - 190f,
                Screen.height - 92f,
                380f,
                46f);
            LoopSceneGui.DrawPanel(
                prompt,
                new Color(0.30f, 0.66f, 0.44f));
            GUI.Label(
                prompt,
                "[E]  ENTER RAID",
                LoopSceneGui.Centered);
        }
    }
}
