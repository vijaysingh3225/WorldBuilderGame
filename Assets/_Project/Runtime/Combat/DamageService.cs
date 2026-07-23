using System;
using UnityEngine;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Combat
{
    public static class DamageService
    {
        public static event Action<GameObject, DamageRequest> Resolved;

        public static bool TryApply(GameObject target, in DamageRequest request)
        {
            if (target == null || request.Amount <= 0f)
            {
                return false;
            }

            IDamageable damageable = FindDamageable(target);
            if (damageable == null || !damageable.IsAlive)
            {
                return false;
            }

            damageable.ReceiveDamage(request);
            GameplayEventLog.Publish("damage", request.Instigator, $"{request.SourceId}:{request.Amount:0.##}->{target.name}");
            Resolved?.Invoke(target, request);
            return true;
        }

        public static bool TryApply(Collider target, in DamageRequest request)
        {
            return target != null && TryApply(target.gameObject, request);
        }

        private static IDamageable FindDamageable(GameObject target)
        {
            MonoBehaviour[] components = target.GetComponentsInParent<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }
    }
}
