using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Combat
{
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class MeleeWeapon : MonoBehaviour
    {
        private readonly Collider[] hits = new Collider[16];
        private readonly HashSet<IDamageable> damagedThisSwing = new HashSet<IDamageable>();

        [SerializeField] private string weaponId = "prototype-sword";
        [SerializeField, Min(0f)] private float damage = 34f;
        [SerializeField, Min(0.1f)] private float cooldown = 0.7f;
        [SerializeField, Min(0.1f)] private float reach = 1.65f;
        [SerializeField, Min(0.05f)] private float radius = 0.72f;
        [SerializeField] private Vector3 attackOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private LayerMask hitMask = ~0;

        private PlayerInputSource input;
        private Health ownerHealth;
        private float nextAttackTime;

        public event Action AttackStarted;

        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);

        private void Awake()
        {
            input = GetComponent<PlayerInputSource>();
            ownerHealth = GetComponent<Health>();
        }

        private void Update()
        {
            if (input.CurrentIntent.AttackPressed)
            {
                TryAttack();
            }
        }

        public bool TryAttack()
        {
            if (Time.time < nextAttackTime || ownerHealth != null && !ownerHealth.IsAlive)
            {
                return false;
            }

            nextAttackTime = Time.time + cooldown;
            AttackStarted?.Invoke();
            GameplayEventLog.Publish("attack", gameObject, weaponId);

            Vector3 center = transform.position + attackOffset + transform.forward * reach;
            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, hits, hitMask, QueryTriggerInteraction.Ignore);
            damagedThisSwing.Clear();

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = hits[index];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable target = FindDamageable(hit);
                if (target == null || ReferenceEquals(target, ownerHealth) || !damagedThisSwing.Add(target))
                {
                    continue;
                }

                Vector3 hitPoint = hit.ClosestPoint(center);
                DamageRequest request = new DamageRequest(gameObject, damage, hitPoint, transform.forward, weaponId);
                DamageService.TryApply(hit, request);
            }

            return true;
        }

        private static IDamageable FindDamageable(Collider hit)
        {
            MonoBehaviour[] components = hit.GetComponentsInParent<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.72f, 0.25f, 0.4f);
            Vector3 center = transform.position + attackOffset + transform.forward * reach;
            Gizmos.DrawSphere(center, radius);
        }
    }
}
