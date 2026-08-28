using System;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class HitReactionPresenter : MonoBehaviour
    {
        public const string StaggerLayerName = "Sword Hit Stagger";
        public const string StaggerStateName = "Short Sword Chest Stagger";
        public const string StaggerClipName = "Rig|Hit_Chest";
        public const float SwordStaggerDuration = 0.25f;

        private static readonly int StaggerStateHash =
            Animator.StringToHash(StaggerStateName);

        [SerializeField] private Health health;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private string hitSoundSourceId =
            MeleeWeapon.PrototypeSwordSourceId;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.14f;
        [SerializeField, Min(0f)] private float shakeDistance = 0.14f;
        [SerializeField, Min(0f)] private float shakeAngle = 7f;
        [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.68f;
        [SerializeField, Min(0f)] private float hitSoundStartOffset = 0.138f;

        private AudioSource audioSource;
        private Vector3 restingLocalPosition;
        private Quaternion restingLocalRotation;
        private float reactionStartedAt = float.NegativeInfinity;
        private int reactionSequence;
        private int staggerLayerIndex = -1;
        private float staggerEndsAt = float.NegativeInfinity;
        private float hitPauseEndsAt = float.NegativeInfinity;
        private float activeImpactStrength = 1f;
        private float activeShakeDuration;
        private float activeStaggerPlaybackSpeed = 1f;
        private float animatorSpeedBeforeStagger = 1f;
        private bool staggerActive;
        private ShortSwordAttackPresenter swordAttackPresenter;
        private ShortSwordBlockPresenter swordBlockPresenter;
        private BowWeapon bowWeapon;
        private bool swordAttackWasEnabled;
        private bool swordBlockWasEnabled;
        private bool bowWasEnabled;

        public string HitSoundSourceId => hitSoundSourceId;
        public int HitSoundPlayCount { get; private set; }
        public int StaggerPlayCount { get; private set; }
        public bool IsStaggered => staggerActive;
        public float ActiveStaggerRemaining =>
            Mathf.Max(0f, staggerEndsAt - Time.time);
        public float ActiveImpactStrength => activeImpactStrength;
        public float ActiveShakeDuration => activeShakeDuration;

        public bool UsesHitSoundForSource(
            string sourceId)
        {
            return string.Equals(
                sourceId,
                hitSoundSourceId,
                StringComparison.Ordinal);
        }

        public bool UsesStaggerForSource(string sourceId)
        {
            return string.Equals(
                sourceId,
                MeleeWeapon.PrototypeSwordSourceId,
                StringComparison.Ordinal);
        }

        public void Configure(
            Health targetHealth,
            Transform targetVisual,
            AudioClip impactClip = null,
            float impactStartOffset = 0.138f)
        {
            Unsubscribe();
            health = targetHealth;
            visualRoot = targetVisual;
            animator = visualRoot != null
                ? visualRoot.GetComponent<Animator>()
                : GetComponentInChildren<Animator>(true);
            hitSound = impactClip;
            hitSoundStartOffset = Mathf.Max(0f, impactStartOffset);
            CaptureRestPose();
            ConfigureAudio();
            ResolveStaggerLayer();
            ResolveInterruptibleActions();
            Subscribe();
        }

        private void Awake()
        {
            health ??= GetComponent<Health>();
            if (visualRoot == null)
            {
                Renderer targetRenderer = GetComponentInChildren<Renderer>();
                visualRoot = targetRenderer != null ? targetRenderer.transform : transform;
            }

            CaptureRestPose();
            ConfigureAudio();
            animator ??= visualRoot != null
                ? visualRoot.GetComponent<Animator>()
                : GetComponentInChildren<Animator>(true);
            ResolveStaggerLayer();
            ResolveInterruptibleActions();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            EndStagger();
            RestoreVisual();
        }

        private void Update()
        {
            if (!staggerActive)
            {
                return;
            }

            if (Time.time >= staggerEndsAt)
            {
                EndStagger();
                return;
            }

            // Training dummies deliberately freeze their base animator every frame.
            // Keep the temporary reaction layer advancing until its short window ends.
            if (animator != null && staggerLayerIndex >= 0)
            {
                animator.speed = Time.time < hitPauseEndsAt
                    ? 0f
                    : activeStaggerPlaybackSpeed;
                animator.SetLayerWeight(staggerLayerIndex, 1f);
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            float duration = activeShakeDuration > 0f
                ? activeShakeDuration
                : shakeDuration;
            float progress = (Time.time - reactionStartedAt) / duration;
            if (progress < 0f || progress >= 1f)
            {
                RestoreVisual();
                return;
            }

            float envelope = (1f - progress) * (1f - progress);
            float phase = progress * Mathf.PI * 6f + reactionSequence * 1.37f;
            Vector3 offset = new Vector3(
                Mathf.Sin(phase),
                Mathf.Sin(phase * 1.71f) * 0.35f,
                Mathf.Cos(phase * 1.29f) * 0.55f) *
                (shakeDistance * activeImpactStrength * envelope);
            Vector3 angles = new Vector3(
                Mathf.Sin(phase * 1.43f),
                Mathf.Cos(phase * 1.11f) * 0.35f,
                Mathf.Sin(phase * 1.83f)) *
                (shakeAngle * activeImpactStrength * envelope);

            visualRoot.localPosition = restingLocalPosition + offset;
            visualRoot.localRotation = restingLocalRotation * Quaternion.Euler(angles);
        }

        private void HandleDamaged(DamageRequest request)
        {
            reactionSequence++;
            reactionStartedAt = Time.time;
            activeImpactStrength = Mathf.Max(
                0f,
                request.ImpactStrength);
            float impactWeight = Mathf.InverseLerp(
                0.32f,
                2.30f,
                activeImpactStrength);
            activeShakeDuration = shakeDuration * Mathf.Lerp(
                0.62f,
                1.65f,
                impactWeight);
            if (request.Amount > 0f &&
                health != null &&
                health.IsAlive &&
                UsesStaggerForSource(request.SourceId))
            {
                BeginStagger(
                    request.StaggerDuration > 0f
                        ? request.StaggerDuration
                        : SwordStaggerDuration,
                    request.HitPauseDuration);
            }
            if (audioSource != null &&
                hitSound != null &&
                UsesHitSoundForSource(request.SourceId))
            {
                // The supplied MP3 has 138 ms of leading silence. Starting at its
                // first audible transient keeps the sound on the damage/contact frame.
                audioSource.Stop();
                audioSource.clip = hitSound;
                audioSource.pitch = Mathf.Lerp(
                    1.20f,
                    0.72f,
                    impactWeight);
                audioSource.volume = Mathf.Clamp01(
                    hitSoundVolume * Mathf.Lerp(
                        0.72f,
                        1.32f,
                        impactWeight));
                audioSource.time = Mathf.Min(
                    hitSoundStartOffset,
                    Mathf.Max(0f, hitSound.length - 0.001f));
                audioSource.Play();
                HitSoundPlayCount++;
            }
        }

        private void BeginStagger(
            float duration,
            float hitPauseDuration)
        {
            if (animator == null)
            {
                animator = visualRoot != null
                    ? visualRoot.GetComponent<Animator>()
                    : GetComponentInChildren<Animator>(true);
            }
            if (animator == null ||
                !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (staggerLayerIndex < 0)
            {
                ResolveStaggerLayer();
            }
            if (staggerLayerIndex < 0)
            {
                return;
            }

            ThirdPersonMotor motor = GetComponentInParent<ThirdPersonMotor>();
            if (motor != null && motor.IsClimbingLadder)
            {
                motor.CancelLadderClimb();
            }

            if (!staggerActive)
            {
                animatorSpeedBeforeStagger = animator.speed;
                ResolveInterruptibleActions();
                swordAttackWasEnabled =
                    swordAttackPresenter != null &&
                    swordAttackPresenter.enabled;
                swordBlockWasEnabled =
                    swordBlockPresenter != null &&
                    swordBlockPresenter.enabled;
                bowWasEnabled = bowWeapon != null && bowWeapon.enabled;
            }

            swordAttackPresenter?.InterruptForHitStagger();
            if (swordAttackPresenter != null)
            {
                swordAttackPresenter.enabled = false;
            }
            if (swordBlockPresenter != null)
            {
                swordBlockPresenter.enabled = false;
            }
            if (bowWeapon != null)
            {
                bowWeapon.AbortDraw();
                bowWeapon.enabled = false;
            }

            staggerActive = true;
            float resolvedDuration = Mathf.Max(0.01f, duration);
            float resolvedHitPause = Mathf.Clamp(
                hitPauseDuration,
                0f,
                resolvedDuration - 0.01f);
            staggerEndsAt = Time.time + resolvedDuration;
            hitPauseEndsAt = Time.time + resolvedHitPause;
            activeStaggerPlaybackSpeed = SwordStaggerDuration /
                Mathf.Max(0.01f, resolvedDuration - resolvedHitPause);
            animator.speed = resolvedHitPause > 0f
                ? 0f
                : activeStaggerPlaybackSpeed;
            animator.SetLayerWeight(staggerLayerIndex, 1f);
            animator.Play(
                StaggerStateHash,
                staggerLayerIndex,
                0f);
            StaggerPlayCount++;
        }

        private void EndStagger()
        {
            if (!staggerActive)
            {
                return;
            }

            staggerActive = false;
            staggerEndsAt = float.NegativeInfinity;
            hitPauseEndsAt = float.NegativeInfinity;
            activeStaggerPlaybackSpeed = 1f;
            if (animator != null)
            {
                if (staggerLayerIndex >= 0)
                {
                    animator.SetLayerWeight(staggerLayerIndex, 0f);
                }
                animator.speed = animatorSpeedBeforeStagger;
            }
            if (swordAttackPresenter != null)
            {
                swordAttackPresenter.enabled = swordAttackWasEnabled;
            }
            if (swordBlockPresenter != null)
            {
                swordBlockPresenter.enabled = swordBlockWasEnabled;
            }
            if (bowWeapon != null)
            {
                bowWeapon.enabled = bowWasEnabled;
            }
        }

        private void ResolveStaggerLayer()
        {
            staggerLayerIndex = animator != null
                ? animator.GetLayerIndex(StaggerLayerName)
                : -1;
            if (animator != null && staggerLayerIndex >= 0)
            {
                animator.SetLayerWeight(staggerLayerIndex, 0f);
            }
        }

        private void ResolveInterruptibleActions()
        {
            swordAttackPresenter ??=
                GetComponentInChildren<ShortSwordAttackPresenter>(true);
            swordBlockPresenter ??=
                GetComponentInChildren<ShortSwordBlockPresenter>(true);
            bowWeapon ??= GetComponentInChildren<BowWeapon>(true);
        }

        private void ConfigureAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0.15f;
            audioSource.dopplerLevel = 0f;
        }

        private void CaptureRestPose()
        {
            if (visualRoot == null)
            {
                return;
            }

            restingLocalPosition = visualRoot.localPosition;
            restingLocalRotation = visualRoot.localRotation;
        }

        private void RestoreVisual()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = restingLocalPosition;
            visualRoot.localRotation = restingLocalRotation;
            activeImpactStrength = 1f;
            activeShakeDuration = 0f;
        }

        private void Subscribe()
        {
            if (health == null)
            {
                return;
            }

            health.Damaged -= HandleDamaged;
            health.Damaged += HandleDamaged;
        }

        private void Unsubscribe()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }
        }
    }
}
