using UnityEngine;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerHitFeedbackEmitter : MonoBehaviour
    {
        public const float FeedbackVolume = 0.12f;

        [SerializeField] private AudioClip enemyHitClip;
        [SerializeField] private AudioClip headshotClip;
        [SerializeField] private AudioSource audioSource;
        private AudioClip alignedEnemyHitClip;
        private AudioClip alignedHeadshotClip;

        public int PlaybackCount { get; private set; }
        public AudioClip LastPlayedClip { get; private set; }
        public AudioClip LastSourceClip { get; private set; }
        public float SpatialBlend =>
            audioSource != null
                ? audioSource.spatialBlend
                : 1f;

        private void Awake()
        {
            Configure();
        }

        public static bool TryPlay(
            GameObject target,
            in DamageRequest request)
        {
            if (target == null ||
                request.SourceId != "prototype-bow" ||
                target.GetComponentInParent<
                    EnemyDamageProfile>(true) == null)
            {
                return false;
            }

            GameObject playerRoot =
                ResolvePlayerRoot(request.Instigator);
            if (playerRoot == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    "Enemy bow damage resolved, but no active player root was available for hit feedback.");
#endif
                return false;
            }

            PlayerHitFeedbackEmitter emitter =
                playerRoot.GetComponent<
                    PlayerHitFeedbackEmitter>();
            if (emitter == null)
            {
                emitter = playerRoot.AddComponent<
                    PlayerHitFeedbackEmitter>();
            }
            BowWeapon playerBow =
                playerRoot.GetComponentInChildren<
                    BowWeapon>(true);
            if (playerBow != null)
            {
                emitter.ConfigureClips(
                    playerBow.EnemyHitFeedbackClip,
                    playerBow.HeadshotFeedbackClip);
            }

            HumanoidDamageZone zone =
                target.GetComponentInParent<
                    HumanoidDamageZone>(true);
            EnemyDamageProfile profile =
                target.GetComponentInParent<
                    EnemyDamageProfile>(true);
            HumanoidHitRegion region =
                zone != null
                    ? zone.Region
                    : profile.ResolveHitRegion(
                        request.HitPoint);
            emitter.Play(
                region == HumanoidHitRegion.Head);
            return true;
        }

        private static GameObject ResolvePlayerRoot(
            GameObject instigator)
        {
            for (Transform current =
                     instigator != null
                         ? instigator.transform
                         : null;
                 current != null;
                 current = current.parent)
            {
                if (current.CompareTag("Player"))
                {
                    return current.gameObject;
                }
                PlayerInputSource input =
                    current.GetComponent<
                        PlayerInputSource>();
                if (input != null)
                {
                    return current.gameObject;
                }
            }

            GameObject taggedPlayer =
                GameObject.FindGameObjectWithTag(
                    "Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }

            PlayerInputSource activeInput =
                Object.FindFirstObjectByType<
                    PlayerInputSource>(
                    FindObjectsInactive.Exclude);
            return activeInput != null
                ? activeInput.gameObject
                : null;
        }

        private void Configure()
        {
            EnsureLoaded(enemyHitClip);
            EnsureLoaded(headshotClip);

            if (audioSource == null)
            {
                audioSource =
                    gameObject.AddComponent<AudioSource>();
            }
            audioSource.enabled = true;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            audioSource.mute = false;
            audioSource.priority = 0;
            audioSource.dopplerLevel = 0f;
            audioSource.ignoreListenerPause = true;
            audioSource.bypassEffects = true;
            audioSource.bypassListenerEffects = true;
            audioSource.bypassReverbZones = true;
        }

        public void ConfigureClips(
            AudioClip hitClip,
            AudioClip criticalHitClip)
        {
            if (enemyHitClip != hitClip)
            {
                enemyHitClip = hitClip;
                ReplaceAlignedClip(
                    ref alignedEnemyHitClip,
                    CreateImpactAlignedClip(hitClip));
            }
            if (headshotClip != criticalHitClip)
            {
                headshotClip = criticalHitClip;
                ReplaceAlignedClip(
                    ref alignedHeadshotClip,
                    CreateImpactAlignedClip(
                        criticalHitClip));
            }
            Configure();
        }

        private void Play(bool headshot)
        {
            Configure();
            AudioClip sourceClip =
                headshot && headshotClip != null
                    ? headshotClip
                    : enemyHitClip;
            AudioClip selectedClip =
                headshot && alignedHeadshotClip != null
                    ? alignedHeadshotClip
                    : !headshot && alignedEnemyHitClip != null
                        ? alignedEnemyHitClip
                        : sourceClip;
            if (audioSource == null ||
                selectedClip == null)
            {
                return;
            }

            audioSource.Stop();
            audioSource.PlayOneShot(
                selectedClip,
                FeedbackVolume);
            LastPlayedClip = selectedClip;
            LastSourceClip = sourceClip;
            PlaybackCount++;
            GameplayEventLog.Publish(
                "player-hit-feedback",
                gameObject,
                headshot ? "headshot" : "enemy-hit");
#if UNITY_EDITOR
            Debug.Log(
                $"Player hit feedback emitter played " +
                $"{(headshot ? "headshot" : "enemy-hit")} " +
                $"on {gameObject.name}.",
                this);
#endif
        }

        private void OnDestroy()
        {
            ReplaceAlignedClip(ref alignedEnemyHitClip, null);
            ReplaceAlignedClip(ref alignedHeadshotClip, null);
        }

        private static void EnsureLoaded(
            AudioClip clip)
        {
            if (clip != null &&
                clip.loadState ==
                    AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }

        private static AudioClip CreateImpactAlignedClip(
            AudioClip source)
        {
            if (source == null)
            {
                return null;
            }
            EnsureLoaded(source);
            int channels = Mathf.Max(1, source.channels);
            int frameCount = source.samples;
            float[] samples =
                new float[frameCount * channels];
            if (!source.GetData(samples, 0))
            {
                return source;
            }

            float peak = 0f;
            for (int index = 0;
                 index < samples.Length;
                 index++)
            {
                peak = Mathf.Max(
                    peak,
                    Mathf.Abs(samples[index]));
            }
            float threshold =
                Mathf.Max(0.003f, peak * 0.04f);
            int firstAudibleSample = 0;
            while (firstAudibleSample < samples.Length &&
                   Mathf.Abs(samples[firstAudibleSample]) <
                       threshold)
            {
                firstAudibleSample++;
            }
            int firstAudibleFrame =
                firstAudibleSample / channels;
            int preRollFrames = Mathf.RoundToInt(
                source.frequency * 0.006f);
            int startFrame = Mathf.Max(
                0,
                firstAudibleFrame - preRollFrames);
            if (startFrame <=
                Mathf.RoundToInt(
                    source.frequency * 0.004f))
            {
                return source;
            }

            int trimmedFrameCount =
                frameCount - startFrame;
            float[] trimmedSamples =
                new float[trimmedFrameCount * channels];
            System.Array.Copy(
                samples,
                startFrame * channels,
                trimmedSamples,
                0,
                trimmedSamples.Length);
            AudioClip aligned = AudioClip.Create(
                source.name + " Impact Aligned",
                trimmedFrameCount,
                channels,
                source.frequency,
                false);
            aligned.hideFlags = HideFlags.DontSave;
            aligned.SetData(trimmedSamples, 0);
            return aligned;
        }

        private static void ReplaceAlignedClip(
            ref AudioClip current,
            AudioClip replacement)
        {
            if (current != null &&
                (current.hideFlags & HideFlags.DontSave) != 0)
            {
                if (Application.isPlaying)
                {
                    Destroy(current);
                }
                else
                {
                    DestroyImmediate(current);
                }
            }
            current = replacement;
        }
    }
}
