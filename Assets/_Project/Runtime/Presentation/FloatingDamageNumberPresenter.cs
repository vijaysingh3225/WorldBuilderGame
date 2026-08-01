using UnityEngine;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageNumberPresenter : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private EnemyDamageProfile profile;
        private FloatingDamageNumberOverlay overlay;

        public int ActiveNumberCount =>
            overlay != null ? overlay.ActiveNumberCount : 0;

        public void Configure(
            Health observedHealth,
            EnemyDamageProfile damageProfile)
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            health = observedHealth;
            profile = damageProfile;
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Damaged += HandleDamaged;
            }
        }

        private void Awake()
        {
            Configure(
                health != null ? health : GetComponent<Health>(),
                profile != null
                    ? profile
                    : GetComponent<EnemyDamageProfile>());
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(DamageRequest request)
        {
            bool critical =
                profile != null &&
                request.SourceId == "prototype-bow" &&
                profile.ResolveHitRegion(request.HitPoint) ==
                HumanoidHitRegion.Head;

            // The overlay lives outside the enemy so lethal Raid hits remain
            // visible while that enemy changes into its ragdoll/death state.
            overlay = FloatingDamageNumberOverlay.GetOrCreate();
            overlay.Show(request.Amount, request.HitPoint, critical);
        }
    }
}
