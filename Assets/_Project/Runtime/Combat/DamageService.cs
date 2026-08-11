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

            PlayerHitFeedbackEmitter.TryPlay(
                target,
                request);
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
            HumanoidDamageZone zone =
                target.GetComponentInParent<HumanoidDamageZone>(true);
            if (zone != null)
            {
                return zone;
            }

            // Root colliders such as CharacterController can receive the arrow
            // collision before a precise hitbox. Keep those hits on the enemy
            // profile instead of bypassing the anatomical damage rules via Health.
            EnemyDamageProfile enemyProfile =
                target.GetComponentInParent<EnemyDamageProfile>(true);
            if (enemyProfile != null)
            {
                return enemyProfile;
            }

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
