using UnityEngine;

namespace WorldBuilder.Gameplay.Combat
{
    public readonly struct DamageRequest
    {
        public DamageRequest(GameObject instigator, float amount, Vector3 hitPoint, Vector3 direction, string sourceId)
        {
            Instigator = instigator;
            Amount = amount;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
            SourceId = sourceId;
        }

        public GameObject Instigator { get; }
        public float Amount { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Direction { get; }
        public string SourceId { get; }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void ReceiveDamage(in DamageRequest request);
    }
}
