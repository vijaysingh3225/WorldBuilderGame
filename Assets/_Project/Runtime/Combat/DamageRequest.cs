using UnityEngine;

namespace WorldBuilder.Gameplay.Combat
{
    public readonly struct DamageRequest
    {
        public DamageRequest(
            GameObject instigator,
            float amount,
            Vector3 hitPoint,
            Vector3 direction,
            string sourceId,
            float staggerDuration = 0f,
            float hitPauseDuration = 0f,
            float impactStrength = 1f)
        {
            Instigator = instigator;
            Amount = amount;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
            SourceId = sourceId;
            StaggerDuration = Mathf.Max(0f, staggerDuration);
            HitPauseDuration = Mathf.Max(0f, hitPauseDuration);
            ImpactStrength = Mathf.Max(0f, impactStrength);
        }

        public GameObject Instigator { get; }
        public float Amount { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Direction { get; }
        public string SourceId { get; }
        public float StaggerDuration { get; }
        public float HitPauseDuration { get; }
        public float ImpactStrength { get; }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void ReceiveDamage(in DamageRequest request);
    }
}
