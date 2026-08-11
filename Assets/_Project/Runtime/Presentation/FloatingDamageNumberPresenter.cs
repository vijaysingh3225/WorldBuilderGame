using UnityEngine;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageNumberPresenter : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private EnemyDamageProfile profile;
        [SerializeField] private bool playerOwned;
        private FloatingDamageNumberOverlay overlay;

        public int ActiveNumberCount =>
            overlay != null ? overlay.ActiveNumberCount : 0;

        public void Configure(
            Health observedHealth,
            EnemyDamageProfile damageProfile,
            bool showBesidePlayer = false)
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            health = observedHealth;
            profile = damageProfile;
            playerOwned = showBesidePlayer;
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
            Vector3 presentationPoint = playerOwned
                ? transform.position + Vector3.up * 1.18f
                : request.HitPoint;
            overlay.Show(
                request.Amount,
                presentationPoint,
                critical,
                playerOwned);
        }
    }
}
