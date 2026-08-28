using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class LadderClimbPoint : MonoBehaviour
    {
        public const float DefaultInteractionDistance = 1.8f;

        [SerializeField] private Vector3 bottomPosition;
        [SerializeField] private Vector3 topPosition;
        [SerializeField] private Vector3 climbFacing = Vector3.forward;
        [SerializeField, Min(0.5f)] private float interactionDistance =
            DefaultInteractionDistance;

        private ThirdPersonMotor playerMotor;
        private float nextPlayerResolveTime;

        public Vector3 BottomPosition => bottomPosition;
        public Vector3 TopPosition => topPosition;
        public Vector3 ClimbFacing => climbFacing;
        public float ClimbHeight => topPosition.y - bottomPosition.y;
        public bool CanInteract =>
            playerMotor != null &&
            !playerMotor.IsClimbingLadder &&
            Vector3.Distance(
                playerMotor.transform.position,
                bottomPosition) <= interactionDistance;

        public void Configure(
            Vector3 climbBottom,
            Vector3 climbTop,
            Vector3 facing)
        {
            bottomPosition = climbBottom;
            topPosition = climbTop;
            climbFacing = Vector3.ProjectOnPlane(
                facing,
                Vector3.up).normalized;
        }

        private void Update()
        {
            ResolvePlayer();
            if (!CanInteract ||
                !PlayerControlBindings.WasPressedThisFrame(
                    Keyboard.current,
                    PlayerControl.Interact))
            {
                return;
            }

            LadderClimbPresenter presenter =
                playerMotor.GetComponent<LadderClimbPresenter>();
            if (presenter == null)
            {
                presenter = playerMotor.gameObject.AddComponent<
                    LadderClimbPresenter>();
            }
            presenter.Configure(playerMotor);
            playerMotor.TryBeginLadderClimb(
                bottomPosition,
                topPosition,
                climbFacing);
        }

        private void OnGUI()
        {
            if (CanInteract)
            {
                LootInteractionPresentation.DrawPrompt("Climb Ladder");
            }
        }

        private void ResolvePlayer()
        {
            if (playerMotor != null || Time.unscaledTime < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime = Time.unscaledTime + 0.5f;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            playerMotor = player != null
                ? player.GetComponent<ThirdPersonMotor>()
                : null;
        }
    }
}
