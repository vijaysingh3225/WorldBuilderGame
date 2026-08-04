using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ShortSwordAttackPresenter : MonoBehaviour
    {
        public const string AttackLayerName = "Short Sword Combo";
        public const string Hit1StateName = "Sword Cycle Hit 1";
        public const string Hit1RecoveryStateName = "Sword Cycle Hit 1 Recovery";
        public const string Hit2StateName = "Sword Cycle Hit 2";
        public const string Hit2RecoveryStateName = "Sword Cycle Hit 2 Recovery";
        public const string Hit3StateName = "Sword Cycle Hit 3";
        public const string AttackStateName = Hit1StateName;
        public const float MinimumAttackTransitionDuration = 0.075f;
        public const float MinimumAttackReturnDuration = 0.24f;
        public const float AttackEntryBlendDuration = 0.12f;
        public const float RunningRecoveryHandoffStart = 0.50f;
        public const float RunningFinisherHandoffStart = 0.58f;
        public const float RunningReturnGaitThreshold = 0.25f;

        private static readonly int[] HitStateHashes =
        {
            Animator.StringToHash(Hit1StateName),
            Animator.StringToHash(Hit2StateName),
            Animator.StringToHash(Hit3StateName)
        };

        private static readonly int[] RecoveryStateHashes =
        {
            Animator.StringToHash(Hit1RecoveryStateName),
            Animator.StringToHash(Hit2RecoveryStateName)
        };

        private static readonly float[] SwingSoundTimes = { 0.30f, 0.25f, 0.27f };
        private static readonly float[] DamageWindowEndTimes = { 0.68f, 0.66f, 0.46f };
        private static readonly float[] SwingPitches = { 1f, 1f, 0.86f };

        [SerializeField] private Animator animator;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private Transform swordRoot;
        [SerializeField] private AudioClip swordSwingClip;
        [SerializeField] private AudioSource swordAudioSource;
        [SerializeField, Range(0f, 1f)] private float swordSwingVolume = 0.36f;
        [SerializeField, Range(0.1f, 0.8f)] private float comboInputOpen = 0.20f;
        [SerializeField, Range(0.3f, 1f)] private float comboInputClose = 0.99f;
        [SerializeField, Range(0f, 0.9f)] private float comboRecoveryGrace = 0.50f;
        [SerializeField, Range(0.3f, 0.9f)] private float comboChainPoint = 0.68f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.035f;
        [SerializeField, Min(0.01f)] private float finalReturnBlendDuration = 0.12f;

        private int attackLayerIndex = -1;
        private int currentHit;
        private bool attackActive;
        private bool entryBlending;
        private bool recovering;
        private bool returnBlending;
        private bool comboFollowUpQueued;
        private bool swingSoundPlayed;
        private bool damageWindowOpened;
        private bool subscribed;
        private float returnBlendStartedAt;
        private float returnBlendStartWeight;
        private float entryBlendStartedAt;
        private float presentationWeight;
        private bool weaponEquipped = true;

        public bool IsAttacking => attackActive;
        public float PresentationWeight => presentationWeight;
        public bool WeaponEquipped => weaponEquipped;
        public int CurrentComboHit => attackActive ? currentHit + 1 : 0;
        public Vector3 SwordDirection => swordRoot != null ? swordRoot.up : Vector3.zero;
        public Vector3 BladePlaneNormal => swordRoot != null ? swordRoot.forward : Vector3.zero;
        public float BladePlaneAlignmentError => 0f;

        public void Configure(
            Animator targetAnimator,
            Transform root,
            ThirdPersonMotor movementMotor,
            MeleeWeapon meleeWeapon,
            Transform equippedSwordRoot,
            AudioClip swingClip = null)
        {
            Unsubscribe();
            animator = targetAnimator;
            playerRoot = root;
            motor = movementMotor;
            weapon = meleeWeapon;
            swordRoot = equippedSwordRoot;
            swordSwingClip = swingClip;
            if (weapon != null && swordRoot != null)
            {
                weapon.ConfigureBlade(swordRoot.Find("Pointed Blade"));
            }
            ConfigureAudio();
            ResolveAnimatorState();
            Subscribe();
        }

        public void SetWeaponEquipped(bool equipped)
        {
            weaponEquipped = equipped;
            if (!weaponEquipped)
            {
                ResetPresentation();
            }
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            weapon ??= GetComponentInParent<MeleeWeapon>();
            motor ??= GetComponentInParent<ThirdPersonMotor>();
            playerRoot ??= weapon != null ? weapon.transform : transform.root;
            ConfigureAudio();
            ResolveAnimatorState();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetPresentation();
        }

        private void Update()
        {
            if (!attackActive || animator == null || attackLayerIndex < 0)
            {
                return;
            }

            if (entryBlending)
            {
                UpdateEntryBlend();
            }

            if (returnBlending)
            {
                UpdateFinalReturnBlend();
                return;
            }

            int expectedHash = recovering
                ? RecoveryStateHashes[currentHit]
                : HitStateHashes[currentHit];
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
            if (state.shortNameHash != expectedHash)
            {
                return;
            }

            if (!recovering)
            {
                UpdateStrikeEvents(state.normalizedTime);
            }

            if (recovering &&
                currentHit < HitStateHashes.Length - 1 &&
                comboFollowUpQueued &&
                state.normalizedTime <= comboRecoveryGrace)
            {
                comboFollowUpQueued = false;
                StartHit(currentHit + 1, true);
                return;
            }

            if (!recovering &&
                currentHit < HitStateHashes.Length - 1 &&
                comboFollowUpQueued &&
                state.normalizedTime >= comboChainPoint)
            {
                comboFollowUpQueued = false;
                StartHit(currentHit + 1, true);
                return;
            }

            bool runningHandoff =
                UpdateRunningLocomotionHandoff(
                    state.normalizedTime);

            if (state.normalizedTime < 1f)
            {
                return;
            }

            if (!recovering && currentHit < RecoveryStateHashes.Length)
            {
                recovering = true;
                animator.CrossFadeInFixedTime(
                    RecoveryStateHashes[currentHit],
                    EffectiveAttackTransitionDuration,
                    attackLayerIndex,
                    0f);
                return;
            }

            if (runningHandoff &&
                presentationWeight <= 0.001f)
            {
                FinishCurrentAttack();
                return;
            }

            BeginReturnBlend();
        }

        private void OnAttackRequested()
        {
            if (!weaponEquipped || animator == null || weapon == null)
            {
                return;
            }

            if (attackLayerIndex < 0)
            {
                ResolveAnimatorState();
            }

            if (attackLayerIndex < 0)
            {
                return;
            }

            if (attackActive)
            {
                QueueFollowUp();
                return;
            }

            StartHit(0, false);
        }

        private void StartHit(int hitIndex, bool chained)
        {
            currentHit = Mathf.Clamp(hitIndex, 0, HitStateHashes.Length - 1);
            recovering = false;
            returnBlending = false;
            attackActive = true;
            swingSoundPlayed = false;
            damageWindowOpened = false;
            if (chained)
            {
                entryBlending = false;
                animator.CrossFadeInFixedTime(
                    HitStateHashes[currentHit],
                    EffectiveAttackTransitionDuration,
                    attackLayerIndex,
                    0f);
                SetAttackLayerWeight(1f);
            }
            else
            {
                animator.Play(HitStateHashes[currentHit], attackLayerIndex, 0f);
                entryBlending = true;
                entryBlendStartedAt = Time.time;
                SetAttackLayerWeight(0f);
            }

            weapon.BeginSwing();
        }

        private void UpdateStrikeEvents(float normalizedTime)
        {
            if (!swingSoundPlayed && normalizedTime >= SwingSoundTimes[currentHit])
            {
                swingSoundPlayed = true;
                PlaySwingSound();
                weapon.OpenBladeDamageWindow();
                damageWindowOpened = true;
            }

            if (damageWindowOpened && normalizedTime >= DamageWindowEndTimes[currentHit])
            {
                damageWindowOpened = false;
                weapon.CloseBladeDamageWindow();
            }
        }

        private void PlaySwingSound()
        {
            if (swordAudioSource == null || swordSwingClip == null)
            {
                return;
            }

            // A new cut supersedes the tail of the previous whoosh, which keeps a
            // rapid combo crisp. The slower finisher gets the same source at a
            // lower pitch to follow its longer acceleration.
            swordAudioSource.Stop();
            swordAudioSource.pitch = SwingPitches[currentHit];
            swordAudioSource.PlayOneShot(swordSwingClip, swordSwingVolume);
        }

        private void ConfigureAudio()
        {
            swordAudioSource ??= GetComponent<AudioSource>();
            if (swordAudioSource == null)
            {
                swordAudioSource = gameObject.AddComponent<AudioSource>();
            }

            swordAudioSource.playOnAwake = false;
            swordAudioSource.loop = false;
            swordAudioSource.spatialBlend = 0.15f;
            swordAudioSource.dopplerLevel = 0f;
        }

        private void QueueFollowUp()
        {
            // Inputs never carry across a recovery or into a future combo. A new
            // strike must be authorized by a fresh click in the current hit's
            // continuation window.
            if (returnBlending ||
                comboFollowUpQueued ||
                currentHit >= HitStateHashes.Length - 1)
            {
                return;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
            if (recovering)
            {
                bool enteringRecovery = animator.IsInTransition(attackLayerIndex);
                bool insideRecoveryGrace =
                    state.shortNameHash == RecoveryStateHashes[currentHit] &&
                    state.normalizedTime <= comboRecoveryGrace;
                if (enteringRecovery || insideRecoveryGrace)
                {
                    comboFollowUpQueued = true;
                }

                return;
            }

            bool correctAttackState = state.shortNameHash == HitStateHashes[currentHit];
            bool insideComboWindow =
                correctAttackState &&
                state.normalizedTime >= comboInputOpen &&
                state.normalizedTime <= comboInputClose;
            if (insideComboWindow)
            {
                comboFollowUpQueued = true;
            }
        }

        private void UpdateFinalReturnBlend()
        {
            float progress = Mathf.Clamp01(
                (Time.time - returnBlendStartedAt) /
                EffectiveAttackReturnDuration);
            SetAttackLayerWeight(
                returnBlendStartWeight *
                (1f - progress));
            if (progress >= 1f)
            {
                FinishCurrentAttack();
            }
        }

        private void UpdateEntryBlend()
        {
            float progress = Mathf.Clamp01(
                (Time.time - entryBlendStartedAt) /
                AttackEntryBlendDuration);
            SetAttackLayerWeight(progress);
            if (progress >= 1f)
            {
                entryBlending = false;
            }
        }

        private void BeginReturnBlend()
        {
            entryBlending = false;
            returnBlending = true;
            returnBlendStartedAt = Time.time;
            returnBlendStartWeight =
                presentationWeight;
        }

        private bool UpdateRunningLocomotionHandoff(
            float normalizedTime)
        {
            bool canHandoff =
                recovering ||
                currentHit == HitStateHashes.Length - 1;
            if (!canHandoff ||
                !ShouldReturnDirectlyToRunningPose())
            {
                return false;
            }

            float progress =
                CalculateRunningReturnProgress(
                    normalizedTime,
                    recovering);
            if (progress <= 0f)
            {
                return false;
            }

            SetAttackLayerWeight(
                Mathf.Min(
                    presentationWeight,
                    1f - progress));
            return true;
        }

        private bool ShouldReturnDirectlyToRunningPose()
        {
            if (motor == null ||
                !motor.IsGrounded ||
                motor.IsCrouched)
            {
                return false;
            }

            float runningThreshold = Mathf.Lerp(
                motor.WalkSpeed,
                motor.SprintSpeed,
                RunningReturnGaitThreshold);
            return Mathf.Max(
                    motor.HorizontalSpeed,
                    motor.TargetHorizontalSpeed) >=
                runningThreshold;
        }

        public static float CalculateRunningReturnProgress(
            float normalizedTime,
            bool recoveryState)
        {
            float handoffStart = recoveryState
                ? RunningRecoveryHandoffStart
                : RunningFinisherHandoffStart;
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    handoffStart,
                    1f,
                    normalizedTime));
        }

        private void FinishCurrentAttack()
        {
            attackActive = false;
            entryBlending = false;
            recovering = false;
            returnBlending = false;
            returnBlendStartWeight = 0f;
            SetAttackLayerWeight(0f);
            comboFollowUpQueued = false;
            swingSoundPlayed = false;
            damageWindowOpened = false;
            weapon.EndSwing();
        }

        private void ResetPresentation()
        {
            attackActive = false;
            entryBlending = false;
            recovering = false;
            returnBlending = false;
            returnBlendStartWeight = 0f;
            comboFollowUpQueued = false;
            swingSoundPlayed = false;
            damageWindowOpened = false;
            weapon?.EndSwing();
            if (swordAudioSource != null)
            {
                swordAudioSource.Stop();
                swordAudioSource.pitch = 1f;
            }
            if (animator != null &&
                animator.isActiveAndEnabled &&
                animator.runtimeAnimatorController != null &&
                attackLayerIndex >= 0)
            {
                SetAttackLayerWeight(0f);
            }
            else
            {
                presentationWeight = 0f;
            }
        }

        private void ResolveAnimatorState()
        {
            if (animator == null)
            {
                return;
            }

            attackLayerIndex = animator.GetLayerIndex(AttackLayerName);
            if (attackLayerIndex >= 0)
            {
                SetAttackLayerWeight(0f);
            }
        }

        private void SetAttackLayerWeight(float weight)
        {
            presentationWeight = Mathf.Clamp01(weight);
            if (animator != null && attackLayerIndex >= 0)
            {
                animator.SetLayerWeight(
                    attackLayerIndex,
                    presentationWeight);
            }
        }

        private float EffectiveAttackTransitionDuration =>
            Mathf.Max(
                MinimumAttackTransitionDuration,
                transitionDuration);

        private float EffectiveAttackReturnDuration =>
            Mathf.Max(
                MinimumAttackReturnDuration,
                finalReturnBlendDuration);

        private void Subscribe()
        {
            if (subscribed || weapon == null || !isActiveAndEnabled)
            {
                return;
            }

            weapon.AttackRequested += OnAttackRequested;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || weapon == null)
            {
                return;
            }

            weapon.AttackRequested -= OnAttackRequested;
            subscribed = false;
        }
    }
}
