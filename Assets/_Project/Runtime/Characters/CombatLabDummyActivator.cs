using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class CombatLabDummyActivator : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float interactionDistance =
            4.2f;
        [SerializeField] private EnemyBrain enemyBrain;
        [SerializeField] private Transform player;

        private readonly RaycastHit[] focusHits = new RaycastHit[24];
        private PlayerInputSource playerInput;

        public void Configure(
            EnemyBrain brain,
            Transform playerTransform)
        {
            enemyBrain = brain;
            player = playerTransform;
            playerInput = player != null
                ? player.GetComponent<PlayerInputSource>()
                : null;
        }

        private void Awake()
        {
            enemyBrain ??= GetComponent<EnemyBrain>();
        }

        private void Update()
        {
            if (enemyBrain == null || enemyBrain.IsActivated ||
                !CanInteract())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Interact))
            {
                enemyBrain.ActivateForDiagnostics();
            }
        }

        private void OnGUI()
        {
            if (enemyBrain != null && !enemyBrain.IsActivated &&
                Event.current.type == EventType.Repaint &&
                CanInteract())
            {
                LootInteractionPresentation.DrawPrompt(
                    "Activate Training Dummy");
            }
        }

        private bool CanInteract()
        {
            ResolvePlayer();
            Camera camera = Camera.main;
            if (player == null || camera == null ||
                (playerInput != null &&
                 playerInput.UserInterfaceCaptureActive) ||
                !LootInteractionPresentation.IsWithinInteractionDistance(
                    player,
                    transform,
                    interactionDistance))
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(
                LootInteractionPresentation.CalculateAimPoint(
                    camera,
                    player,
                    Screen.width,
                    Screen.height));
            int count = Physics.RaycastNonAlloc(
                ray,
                focusHits,
                camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (int index = 0; index < count; index++)
            {
                Collider candidate = focusHits[index].collider;
                if (candidate == null ||
                    candidate.transform.IsChildOf(player) ||
                    focusHits[index].distance >= nearest)
                {
                    continue;
                }

                nearest = focusHits[index].distance;
                nearestCollider = candidate;
            }

            return nearestCollider != null &&
                (nearestCollider.transform == transform ||
                 nearestCollider.transform.IsChildOf(transform));
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null
                ? playerObject.transform
                : null;
            playerInput = playerObject != null
                ? playerObject.GetComponent<PlayerInputSource>()
                : null;
        }
    }
}
