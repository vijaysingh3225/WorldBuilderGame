using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class RaidPickup : MonoBehaviour
    {
        [SerializeField] private string definitionId =
            "artifact-power-shard";
        [SerializeField] private string displayName =
            "Power Shard";
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Min(0f)] private float rotationSpeed =
            42f;
        [SerializeField, Min(0f)] private float bobHeight =
            0.12f;
        [SerializeField, Min(0f)] private float bobSpeed =
            1.8f;
        [SerializeField] private RaidPrototypeController raidController;

        private Vector3 restingLocalPosition;
        private bool collected;

        public string DefinitionId =>
            string.IsNullOrWhiteSpace(definitionId)
                ? "unknown-artifact"
                : definitionId.Trim();
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? GameplaySceneRuntime.FriendlyId(DefinitionId)
                : displayName.Trim();
        public int Quantity => Mathf.Max(1, quantity);
        public bool IsCollected => collected;

        public void Configure(
            string artifactDefinitionId,
            string artifactDisplayName,
            int artifactQuantity = 1)
        {
            definitionId = artifactDefinitionId;
            displayName = artifactDisplayName;
            quantity = Mathf.Max(1, artifactQuantity);
        }

        private void Awake()
        {
            restingLocalPosition = transform.localPosition;
            Collider[] colliders =
                GetComponentsInChildren<Collider>(true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                colliders[index].isTrigger = true;
            }
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            transform.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.World);
            Vector3 position = restingLocalPosition;
            position.y +=
                Mathf.Sin(Time.time * bobSpeed) *
                bobHeight;
            transform.localPosition = position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected ||
                !GameplaySceneRuntime.IsPlayerCollider(other))
            {
                return;
            }

            raidController ??=
                Object.FindFirstObjectByType<
                    RaidPrototypeController>();
            raidController?.TryCollect(this);
        }

        internal void MarkCollected()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            Collider[] colliders =
                GetComponentsInChildren<Collider>(true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                colliders[index].enabled = false;
            }

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                renderers[index].enabled = false;
            }

            Destroy(gameObject);
        }
    }
}
