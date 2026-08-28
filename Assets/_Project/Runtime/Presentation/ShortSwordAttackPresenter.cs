using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Weapons;

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
        public const string AttackSpeedParameterName =
            "ShortSwordAttackSpeed";
        public const float MinimumAttackTransitionDuration = 0.075f;
        public const float MinimumAttackReturnDuration = 0.10f;
        public const float AttackEntryBlendDuration = 0.12f;
        public const float RunningRecoveryHandoffStart = 0.50f;
        public const float RunningFinisherHandoffStart = 0.58f;
        public const float RunningReturnGaitThreshold = 0.25f;
        public const float HeavyChargeThreshold = 0.28f;
        public const float HeavyMaximumChargeDuration = 1.35f;
        public const float HeavyMinimumDamageMultiplier = 1f;
        public const float HeavyMaximumDamageMultiplier = 1.5f;
        public const float HeavyMinimumLungeDistance = 0f;
        public const float HeavyMaximumLungeDistance = 1.80f;
        public const float HeavyDashDuration = 0.12f;
        public const float HeavyChargeStartNormalizedTime = 0.08f;
        public const float HeavyChargeHoldNormalizedTime = 0.22f;

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
        private static readonly int AttackSpeedParameterHash =
            Animator.StringToHash(AttackSpeedParameterName);

        [SerializeField] private Animator animator;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private Transform swordRoot;
        [SerializeField] private ShortSwordSwingTrail swingTrail;
        [SerializeField] private AudioClip swordSwingClip;
        [SerializeField] private AudioSource swordAudioSource;
        [SerializeField, Range(0f, 1f)] private float swordSwingVolume = 0.36f;
        [SerializeField, Range(0.1f, 0.8f)] private float comboInputOpen = 0.20f;
        [SerializeField, Range(0.3f, 1f)] private float comboInputClose = 0.99f;
        [SerializeField, Range(0f, 0.9f)] private float comboRecoveryGrace = 0.50f;
        [SerializeField, Range(0.3f, 0.9f)] private float comboChainPoint = 0.68f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.035f;
        [SerializeField, Min(0.01f)] private float finalReturnBlendDuration = 0.12f;
        [Header("Heavy Opening Attack")]
        [SerializeField, Min(0.05f)] private float heavyChargeThreshold =
            HeavyChargeThreshold;
        [SerializeField, Min(0.1f)] private float heavyMaximumChargeDuration =
            HeavyMaximumChargeDuration;
        [SerializeField, Range(0.02f, 1f)] private float heavyChargeAnimationSpeed =
            0.48f;
        [SerializeField, Range(0.05f, 0.9f)] private float heavyChargeHoldNormalizedTime =
            HeavyChargeHoldNormalizedTime;
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
        private bool heavyChargeActive;
        private float heavyChargeStartedAt;
        private float heavyChargeAnimationPosition;
        private bool heavyAttackActive;
        private float heavyChargeNormalized;
        private bool heavyLungeApplied;
        private bool heavyHoldGraceQueued;
        private ShortSwordCombatProfile combatProfile =
            ShortSwordCombatProfile.Default;
        private float hitPauseEndsAt = float.NegativeInfinity;
        private bool hasAttackSpeedParameter;

        public bool IsAttacking => attackActive || heavyChargeActive;
        public bool IsHeavyCharging => heavyChargeActive;
        public float HeavyChargeNormalized => heavyChargeNormalized;
        public float HeavyChargePoseNormalized => Mathf.InverseLerp(
            HeavyChargeStartNormalizedTime,
            Mathf.Max(
                HeavyChargeStartNormalizedTime + 0.01f,
                heavyChargeHoldNormalizedTime),
            heavyChargeAnimationPosition);
        public float PresentationWeight => presentationWeight;
        public bool WeaponEquipped => weaponEquipped;
        public int CurrentComboHit => attackActive ? currentHit + 1 : 0;
        public Vector3 SwordDirection => swordRoot != null ? swordRoot.up : Vector3.zero;
        public Vector3 BladePlaneNormal => swordRoot != null ? swordRoot.forward : Vector3.zero;
        public float BladePlaneAlignmentError => 0f;
        public ShortSwordCombatProfile CombatProfile =>
            combatProfile.IsValid
                ? combatProfile
                : ShortSwordCombatProfile.Default;

        public void ConfigureGeneratedCombatProfile(
            ShortSwordCombatProfile profile)
        {
            combatProfile = profile.IsValid
                ? profile
                : ShortSwordCombatProfile.Default;
            ApplyAttackPlaybackSpeed();
            swingTrail?.ConfigureGeneratedCombatProfile(combatProfile);
        }

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
                weapon.SetOpeningHoldEnabled(true);
            }
            EnsureSwingTrail();
            swingTrail?.Configure(weapon);
            swingTrail?.ConfigureGeneratedCombatProfile(CombatProfile);
            ConfigureAudio();
            ResolveAnimatorState();
            EnsureChargePosePresenter();
            Subscribe();
        }

        public void SetWeaponEquipped(bool equipped)
        {
            bool beginHeldHeavyCharge =
                ShouldBeginHeldHeavyChargeOnEquip(
                    weaponEquipped,
                    equipped,
                    weapon != null && weapon.OpeningHoldInputHeld);
            weaponEquipped = equipped;
            if (!weaponEquipped)
            {
                ResetPresentation();
                return;
            }

            if (beginHeldHeavyCharge)
            {
                OnAttackHoldStarted();
            }
        }

        public void InterruptForHitStagger()
        {
            ResetPresentation();
        }

        public void InterruptForWeaponReplacement()
        {
            ResetPresentation();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            weapon ??= GetComponentInParent<MeleeWeapon>();
            motor ??= GetComponentInParent<ThirdPersonMotor>();
            playerRoot ??= weapon != null ? weapon.transform : transform.root;
            weapon?.SetOpeningHoldEnabled(true);
            EnsureSwingTrail();
            swingTrail?.Configure(weapon);
            swingTrail?.ConfigureGeneratedCombatProfile(CombatProfile);
            ConfigureAudio();
            ResolveAnimatorState();
            EnsureChargePosePresenter();
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
            if (ApplyAttackPlaybackSpeed())
            {
                return;
            }

            if (heavyChargeActive)
            {
                UpdateHeavyCharge();
                return;
            }

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

            if (!recovering &&
                !heavyAttackActive &&
                currentHit < RecoveryStateHashes.Length)
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

        private void LateUpdate()
        {
            if (!heavyChargeActive || animator == null || attackLayerIndex < 0)
            {
                return;
            }

            // Sample only the sword layer after normal locomotion animation
            // has updated. The legs therefore keep their regular walk/run
            // cadence while the upper-body strike pose remains held.
            animator.Play(
                HitStateHashes[0],
                attackLayerIndex,
                heavyChargeAnimationPosition);
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

        private void OnAttackHoldStarted()
        {
            if (!weaponEquipped || animator == null || weapon == null)
            {
                return;
            }

            if (heavyChargeActive)
            {
                return;
            }

            if (attackActive)
            {
                heavyHoldGraceQueued = true;
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

            BeginHeavyCharge();
        }

        private void BeginHeavyCharge()
        {
            heavyChargeActive = true;
            heavyChargeStartedAt = Time.time;
            heavyChargeNormalized = 0f;
            heavyAttackActive = false;
            heavyLungeApplied = false;
            heavyChargeAnimationPosition = Mathf.Clamp01(
                HeavyChargeStartNormalizedTime);
            animator.Play(
                HitStateHashes[0],
                attackLayerIndex,
                heavyChargeAnimationPosition);
            SetAttackLayerWeight(1f);
        }

        private void OnAttackHoldReleased(float releasedAt)
        {
            if (!heavyChargeActive)
            {
                if (heavyHoldGraceQueued && attackActive)
                {
                    heavyHoldGraceQueued = false;
                    QueueFollowUp();
                }
                return;
            }

            float chargeDuration = Mathf.Max(
                0f,
                releasedAt - heavyChargeStartedAt);
            heavyChargeActive = false;
            if (chargeDuration < heavyChargeThreshold)
            {
                SetAttackLayerWeight(0f);
                StartHit(0, false);
                return;
            }

            heavyChargeNormalized = CalculateHeavyChargeNormalized(
                chargeDuration,
                heavyChargeThreshold,
                heavyMaximumChargeDuration);
            heavyAttackActive = true;
            heavyLungeApplied = false;
            StartHit(
                0,
                false,
                CalculateHeavyDamageMultiplier(
                    heavyChargeNormalized,
                    HeavyMinimumDamageMultiplier,
                    HeavyMaximumDamageMultiplier),
                heavyChargeAnimationPosition);
        }

        private void UpdateHeavyCharge()
        {
            float heldDuration = Time.time - heavyChargeStartedAt;
            heavyChargeNormalized = CalculateHeavyChargeNormalized(
                heldDuration,
                heavyChargeThreshold,
                heavyMaximumChargeDuration);
            float holdPosition = Mathf.Max(
                HeavyChargeStartNormalizedTime + 0.01f,
                heavyChargeHoldNormalizedTime);
            float approachRate = Mathf.Max(0.01f, heavyChargeAnimationSpeed) /
                Mathf.Max(
                    0.01f,
                    holdPosition - HeavyChargeStartNormalizedTime);
            heavyChargeAnimationPosition = CalculateHeavyChargeAnimationTime(
                heldDuration,
                HeavyChargeStartNormalizedTime,
                holdPosition,
                approachRate);
        }

        private void StartHit(
            int hitIndex,
            bool chained,
            float damageMultiplier = 1f,
            float resumeNormalizedTime = -1f)
        {
            if (chained)
            {
                heavyAttackActive = false;
                heavyLungeApplied = false;
                heavyChargeNormalized = 0f;
            }
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
                animator.Play(
                    HitStateHashes[currentHit],
                    attackLayerIndex,
                    Mathf.Clamp01(
                        resumeNormalizedTime < 0f
                            ? 0f
                            : resumeNormalizedTime));
                entryBlending = true;
                entryBlendStartedAt = Time.time;
                SetAttackLayerWeight(0f);
            }

            weapon.BeginSwing(damageMultiplier);
            swingTrail?.EndSwing();
        }

        private void UpdateStrikeEvents(float normalizedTime)
        {
            if (!swingSoundPlayed && normalizedTime >= SwingSoundTimes[currentHit])
            {
                swingSoundPlayed = true;
                PlaySwingSound();
                if (heavyAttackActive && !heavyLungeApplied)
                {
                    heavyLungeApplied = true;
                    motor?.ApplyPlanarDash(
                        weapon.AttackDirection,
                        CalculateHeavyLungeDistance(
                            heavyChargeNormalized,
                            HeavyMinimumLungeDistance,
                            HeavyMaximumLungeDistance),
                        HeavyDashDuration);
                }
                weapon.OpenBladeDamageWindow();
                damageWindowOpened = true;
                swingTrail?.BeginSlice();
            }

            if (damageWindowOpened && normalizedTime >= DamageWindowEndTimes[currentHit])
            {
                damageWindowOpened = false;
                weapon.CloseBladeDamageWindow();
                swingTrail?.EndSwing();
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
            float basePitch = heavyAttackActive
                ? SwingPitches[currentHit] * 0.78f
                : SwingPitches[currentHit];
            swordAudioSource.pitch = Mathf.Clamp(
                basePitch * CombatProfile.SwingPitchMultiplier,
                0.55f,
                1.45f);
            swordAudioSource.PlayOneShot(
                swordSwingClip,
                Mathf.Clamp01(
                    swordSwingVolume *
                    CombatProfile.SwingVolumeMultiplier));
        }

        private void HandleAttackResolved(MeleeAttackReport report)
        {
            if (report.DamagedTargets <= 0 || !attackActive)
            {
                return;
            }

            float chargedImpact = heavyAttackActive
                ? Mathf.Lerp(1f, 1.25f, heavyChargeNormalized)
                : 1f;
            hitPauseEndsAt = Mathf.Max(
                hitPauseEndsAt,
                Time.time + CombatProfile.HitPauseDuration * chargedImpact);
            ApplyAttackPlaybackSpeed();
        }

        private bool ApplyAttackPlaybackSpeed()
        {
            bool paused = attackActive && Time.time < hitPauseEndsAt;
            if (animator != null && hasAttackSpeedParameter)
            {
                animator.SetFloat(
                    AttackSpeedParameterHash,
                    paused
                        ? 0f
                        : CombatProfile.AttackSpeedMultiplier);
            }
            return paused;
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

        private void EnsureChargePosePresenter()
        {
            if (GetComponent<ShortSwordChargePosePresenter>() == null)
            {
                gameObject.AddComponent<ShortSwordChargePosePresenter>();
            }
        }

        private void EnsureSwingTrail()
        {
            swingTrail ??= GetComponent<ShortSwordSwingTrail>();
            if (swingTrail == null)
            {
                swingTrail = gameObject.AddComponent<ShortSwordSwingTrail>();
            }
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

        public static float CalculateHeavyChargeNormalized(
            float chargeDuration,
            float threshold,
            float maximumDuration)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(
                Mathf.Max(0f, threshold),
                Mathf.Max(threshold + 0.01f, maximumDuration),
                Mathf.Max(0f, chargeDuration)));
        }

        public static float CalculateHeavyChargeAnimationTime(
            float heldDuration,
            float startNormalizedTime,
            float holdNormalizedTime,
            float approachRate)
        {
            float start = Mathf.Clamp01(startNormalizedTime);
            float hold = Mathf.Clamp(holdNormalizedTime, start, 1f);
            float approach = 1f - Mathf.Exp(
                -Mathf.Max(0f, heldDuration) * Mathf.Max(0.01f, approachRate));
            return Mathf.Lerp(start, hold, approach);
        }

        public static float CalculateHeavyDamageMultiplier(
            float chargeNormalized,
            float minimumMultiplier,
            float maximumMultiplier)
        {
            return Mathf.Lerp(
                Mathf.Max(1f, minimumMultiplier),
                Mathf.Max(minimumMultiplier, maximumMultiplier),
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chargeNormalized)));
        }

        public static float CalculateHeavyLungeDistance(
            float chargeNormalized,
            float minimumDistance,
            float maximumDistance)
        {
            return Mathf.Lerp(
                Mathf.Max(0f, minimumDistance),
                Mathf.Max(minimumDistance, maximumDistance),
                Mathf.Clamp01(chargeNormalized));
        }

        public static bool ShouldBeginQueuedHeavyCharge(
            bool queuedHold,
            bool inputHeld,
            bool swordEquipped)
        {
            return queuedHold && inputHeld && swordEquipped;
        }

        public static bool ShouldBeginHeldHeavyChargeOnEquip(
            bool wasEquipped,
            bool isEquipped,
            bool inputHeld)
        {
            return !wasEquipped && isEquipped && inputHeld;
        }

        private void FinishCurrentAttack()
        {
            bool beginQueuedHeavyCharge =
                ShouldBeginQueuedHeavyCharge(
                    heavyHoldGraceQueued,
                    weapon != null && weapon.OpeningHoldInputHeld,
                    weaponEquipped);
            attackActive = false;
            entryBlending = false;
            recovering = false;
            returnBlending = false;
            returnBlendStartWeight = 0f;
            SetAttackLayerWeight(0f);
            comboFollowUpQueued = false;
            swingSoundPlayed = false;
            damageWindowOpened = false;
            heavyAttackActive = false;
            heavyLungeApplied = false;
            heavyHoldGraceQueued = false;
            heavyChargeNormalized = 0f;
            heavyChargeAnimationPosition = 0f;
            hitPauseEndsAt = float.NegativeInfinity;
            weapon.EndSwing();
            swingTrail?.EndSwing();
            if (beginQueuedHeavyCharge)
            {
                BeginHeavyCharge();
            }
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
            heavyChargeActive = false;
            heavyAttackActive = false;
            heavyLungeApplied = false;
            heavyHoldGraceQueued = false;
            heavyChargeNormalized = 0f;
            heavyChargeAnimationPosition = 0f;
            hitPauseEndsAt = float.NegativeInfinity;
            weapon?.EndSwing();
            swingTrail?.EndSwing();
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
            hasAttackSpeedParameter = false;
            foreach (AnimatorControllerParameter parameter in
                     animator.parameters)
            {
                if (parameter.nameHash == AttackSpeedParameterHash &&
                    parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasAttackSpeedParameter = true;
                    break;
                }
            }
            ApplyAttackPlaybackSpeed();
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
            weapon.AttackHoldStarted += OnAttackHoldStarted;
            weapon.AttackHoldReleased += OnAttackHoldReleased;
            weapon.AttackResolved += HandleAttackResolved;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || weapon == null)
            {
                return;
            }

            weapon.AttackRequested -= OnAttackRequested;
            weapon.AttackHoldStarted -= OnAttackHoldStarted;
            weapon.AttackHoldReleased -= OnAttackHoldReleased;
            weapon.AttackResolved -= HandleAttackResolved;
            weapon.SetOpeningHoldEnabled(false);
            subscribed = false;
        }
    }
}
