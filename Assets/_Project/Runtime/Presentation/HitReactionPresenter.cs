using UnityEngine;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class HitReactionPresenter : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private AudioClip hitSound;
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

        public void Configure(
            Health targetHealth,
            Transform targetVisual,
            AudioClip impactClip = null,
            float impactStartOffset = 0.138f)
        {
            Unsubscribe();
            health = targetHealth;
            visualRoot = targetVisual;
            hitSound = impactClip;
            hitSoundStartOffset = Mathf.Max(0f, impactStartOffset);
            CaptureRestPose();
            ConfigureAudio();
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
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreVisual();
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            float progress = (Time.time - reactionStartedAt) / shakeDuration;
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
                Mathf.Cos(phase * 1.29f) * 0.55f) * (shakeDistance * envelope);
            Vector3 angles = new Vector3(
                Mathf.Sin(phase * 1.43f),
                Mathf.Cos(phase * 1.11f) * 0.35f,
                Mathf.Sin(phase * 1.83f)) * (shakeAngle * envelope);

            visualRoot.localPosition = restingLocalPosition + offset;
            visualRoot.localRotation = restingLocalRotation * Quaternion.Euler(angles);
        }

        private void HandleDamaged(DamageRequest request)
        {
            reactionSequence++;
            reactionStartedAt = Time.time;
            if (audioSource != null && hitSound != null)
            {
                // The supplied MP3 has 138 ms of leading silence. Starting at its
                // first audible transient keeps the sound on the damage/contact frame.
                audioSource.Stop();
                audioSource.clip = hitSound;
                audioSource.pitch = 1f;
                audioSource.volume = hitSoundVolume;
                audioSource.time = Mathf.Min(
                    hitSoundStartOffset,
                    Mathf.Max(0f, hitSound.length - 0.001f));
                audioSource.Play();
            }
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
