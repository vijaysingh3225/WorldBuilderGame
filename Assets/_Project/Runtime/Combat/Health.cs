using System;
using UnityEngine;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maximum = 100f;
        [SerializeField] private float current = 100f;

        public event Action<float, float> Changed;
        public event Action<DamageRequest> Died;

        public float Current => current;
        public float Maximum => maximum;
        public float Normalized => maximum > 0f ? current / maximum : 0f;
        public bool IsAlive => current > 0f;

        public void Configure(float maximumHealth)
        {
            maximum = Mathf.Max(1f, maximumHealth);
            current = maximum;
            Changed?.Invoke(current, maximum);
        }

        public void ReceiveDamage(in DamageRequest request)
        {
            if (!IsAlive || request.Amount <= 0f)
            {
                return;
            }

            current = Mathf.Max(0f, current - request.Amount);
            Changed?.Invoke(current, maximum);

            if (current <= 0f)
            {
                GameplayEventLog.Publish("death", gameObject, request.SourceId);
                Died?.Invoke(request);
            }
        }
    }
}
