using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Combat
{
    public readonly struct MeleeAttackReport
    {
        public MeleeAttackReport(
            float time,
            Vector3 center,
            int overlappingColliders,
            int uniqueDamageables,
            int damagedTargets)
        {
            Time = time;
            Center = center;
            OverlappingColliders = overlappingColliders;
            UniqueDamageables = uniqueDamageables;
            DamagedTargets = damagedTargets;
        }

        public float Time { get; }
        public Vector3 Center { get; }
        public int OverlappingColliders { get; }
        public int UniqueDamageables { get; }
        public int DamagedTargets { get; }
    }

    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(PlayerInputSource))]
    public sealed class MeleeWeapon : MonoBehaviour
    {
        private const int MaximumSweepSteps = 24;

        private readonly Collider[] hits = new Collider[24];
        private readonly HashSet<IDamageable> damagedThisSwing = new HashSet<IDamageable>();

        [SerializeField] private string weaponId = "prototype-sword";
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0.05f)] private float cooldown = 0.15f;
        [SerializeField] private Transform bladeTransform;
        [SerializeField, Min(0.1f)] private float bladeLength = 0.78f;
        [SerializeField, Min(0.005f)] private float bladeRadius = 0.06f;
        [SerializeField] private LayerMask hitMask = ~0;

        private PlayerInputSource input;
        private Health ownerHealth;
        private float runtimeDamageBonus;
        private float nextAttackTime;
        private bool swingStarted;
        private bool damageWindowOpen;
        private bool hasPreviousBladePose;
        private Vector3 previousBladeBase;
        private Vector3 previousBladeTip;

        public event Action AttackRequested;
        public event Action AttackStarted;
        public event Action<string> AttackRejected;
        public event Action<MeleeAttackReport> AttackResolved;

        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
        public float Damage =>
            Mathf.Max(0f, damage + runtimeDamageBonus);
        public float Cooldown => cooldown;
        public float Reach => bladeLength;
        public float Radius => bladeRadius;
        public Vector3 AttackDirection
        {
            get
            {
                Vector3 up = transform.up;
                Camera aimCamera = Camera.main;
                Vector3 direction = aimCamera != null
                    ? Vector3.ProjectOnPlane(aimCamera.transform.forward, up)
                    : Vector3.ProjectOnPlane(transform.forward, up);
                return direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : transform.forward;
            }
        }
        public Vector3 AttackCenter
        {
            get
            {
                GetBladeSegment(out Vector3 bladeBase, out Vector3 bladeTip);
                return (bladeBase + bladeTip) * 0.5f;
            }
        }

        public void ConfigureBlade(
            Transform visibleBlade,
            float visibleBladeLength = 0.78f,
            float visibleBladeRadius = 0.06f)
        {
            bladeTransform = visibleBlade;
            bladeLength = Mathf.Max(0.1f, visibleBladeLength);
            bladeRadius = Mathf.Max(0.005f, visibleBladeRadius);
            CaptureBladePose();
        }

        public void SetRuntimeDamageBonus(float bonus)
        {
            runtimeDamageBonus = bonus;
        }

        private void Awake()
        {
            input = GetComponent<PlayerInputSource>();
            ownerHealth = GetComponent<Health>();
            CaptureBladePose();
        }

        private void Update()
        {
            if (input.CurrentIntent.AttackPressed)
            {
                if (AttackRequested != null)
                {
                    AttackRequested.Invoke();
                }
                else
                {
                    TryAttack();
                }
            }
        }

        private void LateUpdate()
        {
            if (bladeTransform == null)
            {
                return;
            }

            GetBladeSegment(out Vector3 currentBase, out Vector3 currentTip);
            if (damageWindowOpen)
            {
                EvaluateBladeSweep(currentBase, currentTip);
            }

            previousBladeBase = currentBase;
            previousBladeTip = currentTip;
            hasPreviousBladePose = true;
        }

        public bool BeginSwing()
        {
            if (Time.time < nextAttackTime)
            {
                AttackRejected?.Invoke("cooldown");
                return false;
            }

            if (ownerHealth != null && !ownerHealth.IsAlive)
            {
                AttackRejected?.Invoke("owner-dead");
                return false;
            }

            if (bladeTransform == null)
            {
                AttackRejected?.Invoke("blade-missing");
                return false;
            }

            nextAttackTime = Time.time + cooldown;
            swingStarted = true;
            damageWindowOpen = false;
            damagedThisSwing.Clear();
            CaptureBladePose();
            AttackStarted?.Invoke();
            GameplayEventLog.Publish("attack", gameObject, weaponId);
            return true;
        }

        public void OpenBladeDamageWindow()
        {
            if (!swingStarted || bladeTransform == null)
            {
                return;
            }

            damageWindowOpen = true;
            CaptureBladePose();
        }

        public void CloseBladeDamageWindow()
        {
            damageWindowOpen = false;
        }

        public void EndSwing()
        {
            damageWindowOpen = false;
            swingStarted = false;
            hasPreviousBladePose = false;
        }

        // Retained for isolated callers and diagnostics. Gameplay presentation uses
        // BeginSwing/OpenBladeDamageWindow so the live animation owns the window.
        public bool TryAttack()
        {
            if (!BeginSwing())
            {
                return false;
            }

            OpenBladeDamageWindow();
            GetBladeSegment(out Vector3 bladeBase, out Vector3 bladeTip);
            EvaluateBladeSweep(bladeBase, bladeTip);
            EndSwing();
            return true;
        }

        private void EvaluateBladeSweep(Vector3 currentBase, Vector3 currentTip)
        {
            if (!hasPreviousBladePose)
            {
                previousBladeBase = currentBase;
                previousBladeTip = currentTip;
                hasPreviousBladePose = true;
            }

            float maximumTravel = Mathf.Max(
                Vector3.Distance(previousBladeBase, currentBase),
                Vector3.Distance(previousBladeTip, currentTip));
            float stepLength = Mathf.Max(bladeRadius * 0.75f, 0.015f);
            int steps = Mathf.Clamp(
                Mathf.CeilToInt(maximumTravel / stepLength),
                1,
                MaximumSweepSteps);

            for (int step = 1; step <= steps; step++)
            {
                float progress = step / (float)steps;
                Vector3 sampleBase = Vector3.Lerp(previousBladeBase, currentBase, progress);
                Vector3 sampleTip = Vector3.Lerp(previousBladeTip, currentTip, progress);
                EvaluateBladeAt(sampleBase, sampleTip, currentBase, currentTip);
            }
        }

        private void EvaluateBladeAt(
            Vector3 bladeBase,
            Vector3 bladeTip,
            Vector3 currentBase,
            Vector3 currentTip)
        {
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bladeBase,
                bladeTip,
                bladeRadius,
                hits,
                hitMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount == 0)
            {
                return;
            }

            int uniqueDamageables = 0;
            int damagedTargets = 0;
            Vector3 bladeCenter = (bladeBase + bladeTip) * 0.5f;
            Vector3 bladeMotion =
                (currentBase - previousBladeBase) + (currentTip - previousBladeTip);
            Vector3 damageDirection = bladeMotion.sqrMagnitude > 0.0001f
                ? bladeMotion.normalized
                : AttackDirection;

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = hits[index];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable target = FindDamageable(hit);
                if (target == null ||
                    ReferenceEquals(target, ownerHealth) ||
                    !damagedThisSwing.Add(target))
                {
                    continue;
                }

                uniqueDamageables++;
                Vector3 hitPoint = hit.ClosestPoint(bladeCenter);
                DamageRequest request =
                    new DamageRequest(gameObject, Damage, hitPoint, damageDirection, weaponId);
                if (DamageService.TryApply(hit, request))
                {
                    damagedTargets++;
                }
            }

            if (uniqueDamageables > 0)
            {
                AttackResolved?.Invoke(new MeleeAttackReport(
                    Time.time,
                    bladeCenter,
                    hitCount,
                    uniqueDamageables,
                    damagedTargets));
            }
        }

        private void CaptureBladePose()
        {
            if (bladeTransform == null)
            {
                hasPreviousBladePose = false;
                return;
            }

            GetBladeSegment(out previousBladeBase, out previousBladeTip);
            hasPreviousBladePose = true;
        }

        private void GetBladeSegment(out Vector3 bladeBase, out Vector3 bladeTip)
        {
            if (bladeTransform != null)
            {
                bladeBase = bladeTransform.TransformPoint(Vector3.zero);
                bladeTip = bladeTransform.TransformPoint(Vector3.up * bladeLength);
                return;
            }

            bladeBase = transform.position;
            bladeTip = transform.position + AttackDirection * bladeLength;
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
            GetBladeSegment(out Vector3 bladeBase, out Vector3 bladeTip);
            Gizmos.color = new Color(0.95f, 0.72f, 0.25f, 0.5f);
            Gizmos.DrawWireSphere(bladeBase, bladeRadius);
            Gizmos.DrawWireSphere(bladeTip, bladeRadius);
            Gizmos.DrawLine(bladeBase, bladeTip);
        }
    }
}
