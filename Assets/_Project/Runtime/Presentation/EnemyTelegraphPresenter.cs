using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [RequireComponent(typeof(EnemyBrain))]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyTelegraphPresenter : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color idleColor = new Color(0.34f, 0.18f, 0.13f);
        [SerializeField] private Color pursuitColor = new Color(0.56f, 0.25f, 0.13f);
        [SerializeField] private Color windupColor = new Color(1f, 0.22f, 0.08f);
        [SerializeField] private Color recoveryColor = new Color(0.35f, 0.32f, 0.29f);
        [SerializeField] private Color deadColor = new Color(0.08f, 0.08f, 0.08f);

        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private EnemyBrain brain;
        private Health health;

        public void Configure(Renderer renderer)
        {
            targetRenderer = renderer;
            Refresh();
        }

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            health = GetComponent<Health>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void OnEnable()
        {
            brain.StateChanged += HandleStateChanged;
            Refresh();
        }

        private void OnDisable()
        {
            brain.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(EnemyBrain.EnemyState state)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (targetRenderer == null || brain == null || health == null)
            {
                return;
            }

            Color color;
            if (!health.IsAlive || brain.CurrentState == EnemyBrain.EnemyState.Dead)
            {
                color = deadColor;
            }
            else
            {
                switch (brain.CurrentState)
                {
                    case EnemyBrain.EnemyState.Pursuing:
                        color = pursuitColor;
                        break;
                    case EnemyBrain.EnemyState.Windup:
                        color = windupColor;
                        break;
                    case EnemyBrain.EnemyState.Recovering:
                        color = recoveryColor;
                        break;
                    default:
                        color = idleColor;
                        break;
                }
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
