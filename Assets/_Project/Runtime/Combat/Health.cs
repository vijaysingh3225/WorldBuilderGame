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
        [SerializeField, Min(0f)] private float minimum;
        private CombatGuard combatGuard;

        public event Action<float, float> Changed;
        public event Action<DamageRequest> Damaged;
        public event Action<DamageRequest> Died;

        public float Current => current;
        public float Maximum => maximum;
        public float Minimum => minimum;
        public float Normalized => maximum > 0f ? current / maximum : 0f;
        public bool IsAlive => current > 0f;

        public void Configure(float maximumHealth)
        {
            maximum = Mathf.Max(1f, maximumHealth);
            minimum = Mathf.Clamp(minimum, 0f, maximum);
            current = maximum;
            Changed?.Invoke(current, maximum);
        }

        public void ConfigureWithFloor(float maximumHealth, float minimumHealth)
        {
            maximum = Mathf.Max(1f, maximumHealth);
            minimum = Mathf.Clamp(minimumHealth, 0f, maximum);
            current = maximum;
            Changed?.Invoke(current, maximum);
        }

        public void ReceiveDamage(in DamageRequest request)
        {
            if (!IsAlive || request.Amount <= 0f)
            {
                return;
            }

            combatGuard ??= GetComponent<CombatGuard>();
            float appliedAmount =
                request.Amount *
                (combatGuard != null
                    ? combatGuard.GetDamageMultiplier(request.SourceId)
                    : 1f);
            DamageRequest appliedRequest = new DamageRequest(
                request.Instigator,
                appliedAmount,
                request.HitPoint,
                request.Direction,
                request.SourceId);
            current = Mathf.Max(minimum, current - appliedAmount);
            Changed?.Invoke(current, maximum);
            Damaged?.Invoke(appliedRequest);

            if (current <= 0f)
            {
                GameplayEventLog.Publish("death", gameObject, request.SourceId);
                Died?.Invoke(appliedRequest);
            }
        }
    }
}
