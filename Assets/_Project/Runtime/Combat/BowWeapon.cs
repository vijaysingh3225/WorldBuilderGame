using System;
using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Gameplay.Combat
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class BowWeapon : MonoBehaviour
    {
        private const float MinimumNpcReleaseCharge = 0.995f;
        public const float PlayerPostShotHoldDuration = 0.235f;
        private const string ReleaseAudioResourcePath =
            "Audio/SFX/Bow Release";
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Transform bowRoot;
        [SerializeField] private Transform nockedArrow;
        [SerializeField] private AudioClip pullbackClip;
        [SerializeField] private AudioClip releaseClip;
        [SerializeField] private AudioClip arrowImpactClip;
        [SerializeField] private AudioClip enemyHitFeedbackClip;
        [SerializeField] private AudioClip headshotFeedbackClip;
        [SerializeField] private AudioClip arrowFlybyClip;
        [SerializeField] private AudioSource bowAudioSource;
        [SerializeField] private AudioSource releaseAudioSource;
        [SerializeField] private AudioSource hitFeedbackAudioSource;
        [SerializeField, Range(0f, 1f)] private float pullbackVolume = 0.30f;
        [SerializeField, Range(0.5f, 1.5f)] private float pullbackPitch = 0.62f;
        [SerializeField, Range(0f, 1f)] private float releaseVolume = 0.35f;
        [SerializeField] private CameraAimTarget aimTarget;
        [SerializeField] private CharacterAimSource characterAimSource;
        [SerializeField, Min(10f)] private float maximumAimDistance = 150f;
        [SerializeField, Range(0.05f, 0.45f)]
        private float elevatedTargetDepthSearch = 0.30f;
        [SerializeField, Min(0.05f)] private float minimumHoldDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float fullDrawDuration = 1.08f;
        [SerializeField, Min(1f)] private float minimumArrowSpeed = 6f;
        [SerializeField, Min(1f)] private float maximumArrowSpeed = 75f;
        [SerializeField, Min(1f)] private float partialVelocityExponent = 2.4f;
        [SerializeField, Min(0f)] private float minimumDamage = 10f;
        [SerializeField, Min(0f)] private float maximumDamage = 34f;
        [SerializeField, Min(0.05f)] private float reloadDuration = 0.38f;
        [SerializeField, Min(0.05f)] private float playerReloadDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float readyBlendDuration = 0.18f;

        private bool weaponEquipped;
        private bool drawHeldLastFrame;
        private bool arrowReady;
        private float heldDuration;
        private float reloadRemaining;
        private float readyWeight;
        private float runtimeDamageBonus;
        private int firedArrowCount;
        private float lastShotCharge;
        private bool playerOwned;
        private bool pendingRelease;
        private float pendingReleaseCharge;
        private float releasedDrawPresentationWeight;
        private bool pendingReleaseAimLocked;
        private Ray pendingReleaseAimRay;
        private int releasePlaybackCount;
        private RaidPrototypeController raidController;

        public event Action<float> ArrowFired;

        public bool WeaponEquipped => weaponEquipped;
        public bool IsDrawing => weaponEquipped && drawHeldLastFrame;
        public bool DrawInputHeld =>
            weaponEquipped &&
            arrowReady &&
            input != null &&
            (playerOwned
                ? input.CurrentIntent.AttackHeld
                : input.CurrentIntent.BlockHeld);
        public bool ArrowReady => arrowReady;
        public bool CanFire =>
            arrowReady &&
            HasAmmunition &&
            heldDuration >= minimumHoldDuration;
        public bool HasAmmunition =>
            !playerOwned ||
            raidController == null ||
            raidController.ArrowCount > 0;
        public float HeldDuration => heldDuration;
        public float ReadyWeight => readyWeight;
        private bool PlayerShotRecoveryPending =>
            weaponEquipped &&
            playerOwned &&
            (pendingRelease ||
             (!arrowReady && reloadRemaining > 0f) ||
             (drawHeldLastFrame && !DrawInputHeld && CanFire));
        public float PostShotPoseDuration =>
            PlayerPostShotHoldDuration + readyBlendDuration;
        public float PostShotPoseRemaining =>
            PlayerShotRecoveryPending
                ? CalculatePostShotPoseRemaining(
                    pendingRelease || arrowReady
                        ? EffectiveReloadDuration
                        : reloadRemaining,
                    EffectiveReloadDuration,
                    PostShotPoseDuration)
                : 0f;
        public bool PostShotPresentationActive =>
            PostShotPoseRemaining > 0f;
        public float PostShotFollowThroughWeight =>
            PostShotPresentationActive
                ? CalculatePostShotReadyWeight(
                    PostShotPoseRemaining,
                    PostShotPoseDuration,
                    readyBlendDuration)
                : 0f;
        public float PresentedDrawNormalized =>
            IsDrawing
                ? DrawNormalized
                : releasedDrawPresentationWeight *
                  PostShotFollowThroughWeight;
        public bool PresentationAimLocked =>
            DrawInputHeld || PostShotPresentationActive;
        public Vector3 PresentationAimDirection =>
            PostShotPresentationActive &&
            LastAimDirection.sqrMagnitude > 0.0001f
                ? LastAimDirection
                : CurrentAimDirection;
        public float DrawNormalized =>
            weaponEquipped && drawHeldLastFrame
                ? Mathf.InverseLerp(
                    minimumHoldDuration,
                    Mathf.Max(minimumHoldDuration + 0.01f, fullDrawDuration),
                    heldDuration)
                : 0f;
        public int FiredArrowCount => firedArrowCount;
        public float LastShotCharge => lastShotCharge;
        public float LastShotSpeed { get; private set; }
        public Vector3 LastShotDirection { get; private set; }
        public Vector3 CurrentAimDirection => ResolveAimRay().direction;
        public Vector3 PresentedArrowDirection =>
            nockedArrow != null
                ? nockedArrow.forward
                : bowRoot != null
                    ? bowRoot.forward
                    : transform.forward;
        public Vector3 PresentedBowPosition =>
            bowRoot != null ? bowRoot.position : transform.position;
        public Vector3 PresentedArrowTip =>
            nockedArrow != null
                ? nockedArrow.TransformPoint(
                    new Vector3(0f, 0f, 0.60f))
                : PresentedBowPosition;
        public BowArrowProjectile LastFiredProjectile { get; private set; }
        public Vector3 LastLaunchOrigin { get; private set; }
        public Vector3 LastAimOrigin { get; private set; }
        public Vector3 LastAimDirection { get; private set; }
        public Vector3 LastAimRight { get; private set; }
        public float LastHorizontalAimError { get; private set; }
        public float LastHorizontalLaunchOffset { get; private set; }
        public float LastCrosshairAlignmentDistance { get; private set; }
        public Vector3 LastCrosshairPoint { get; private set; }
        public Vector3 LastZeroGravityImpactPoint { get; private set; }
        public bool LastUsedElevatedTargetDepth { get; private set; }
        public bool AudioConfigured =>
            pullbackClip != null &&
            releaseClip != null &&
            arrowImpactClip != null &&
            enemyHitFeedbackClip != null &&
            headshotFeedbackClip != null &&
            bowAudioSource != null &&
            releaseAudioSource != null;
        public float PullbackVolume => pullbackVolume;
        public float PullbackPitch => pullbackPitch;
        public AudioClip ReleaseClip => releaseClip;
        public float ReleaseVolume => releaseVolume;
        public int ReleasePlaybackCount => releasePlaybackCount;
        public AudioSource LastReleaseAudioSource { get; private set; }
        public float ReleaseSpatialBlend =>
            releaseAudioSource != null
                ? releaseAudioSource.spatialBlend
                : 0f;
        public GameObject ReleaseAudioHost =>
            releaseAudioSource != null
                ? releaseAudioSource.gameObject
                : null;
        public AudioClip EnemyHitFeedbackClip =>
            enemyHitFeedbackClip;
        public AudioClip HeadshotFeedbackClip =>
            headshotFeedbackClip;
        public AudioClip ArrowFlybyClip => arrowFlybyClip;
        public float PullbackSpatialBlend =>
            bowAudioSource != null
                ? bowAudioSource.spatialBlend
                : 0f;
        public float PullbackMaxDistance =>
            bowAudioSource != null
                ? bowAudioSource.maxDistance
                : 0f;
        public float HitFeedbackSpatialBlend =>
            hitFeedbackAudioSource != null
                ? hitFeedbackAudioSource.spatialBlend
                : 1f;
        public GameObject HitFeedbackAudioHost =>
            hitFeedbackAudioSource != null
                ? hitFeedbackAudioSource.gameObject
                : null;
        public float FullDrawDuration => fullDrawDuration;
        public float EffectiveReloadDuration =>
            playerOwned
                ? Mathf.Max(reloadDuration, playerReloadDuration)
                : reloadDuration;
        public float ReloadRemaining => reloadRemaining;
        public float CurrentReadyBlendDuration =>
            playerOwned &&
            (pendingRelease || reloadRemaining > 0f)
                ? EffectiveReloadDuration
                : readyBlendDuration;
        public float MaximumArrowSpeed => maximumArrowSpeed;
        public float RuntimeDamageBonus => runtimeDamageBonus;
        public float PartialVelocityExponent =>
            partialVelocityExponent;
        public float MinimumDamage => minimumDamage;
        public float MaximumDamage => maximumDamage;
        public bool IsPlayerOwned => playerOwned;

        public void Configure(
            PlayerInputSource intentSource,
            Transform root,
            Transform equippedBow,
            Transform arrow,
            AudioClip drawClip = null,
            AudioClip impactClip = null,
            AudioClip enemyHitClip = null,
            AudioClip headshotClip = null,
            AudioClip flybyClip = null,
            AudioClip bowReleaseClip = null)
        {
            input = intentSource;
            characterRoot = root;
            bowRoot = equippedBow;
            nockedArrow = arrow;
            pullbackClip = drawClip;
            arrowImpactClip = impactClip;
            enemyHitFeedbackClip = enemyHitClip;
            headshotFeedbackClip = headshotClip;
            arrowFlybyClip = flybyClip;
            releaseClip = bowReleaseClip;
            ConfigureAudio();
            ResolveRaidController();
            SetWeaponEquipped(false);
        }

        public void SetWeaponEquipped(bool equipped)
        {
            weaponEquipped = equipped;
            CancelDraw(false);
            reloadRemaining = 0f;
            releasedDrawPresentationWeight = 0f;
            ResolveRaidController();
            arrowReady = equipped && HasAmmunition;
            SetNockedArrowVisible(arrowReady);
        }

        public void SetRuntimeDamageBonus(float bonus)
        {
            runtimeDamageBonus = bonus;
        }

        public void AbortDraw()
        {
            CancelDraw(false);
        }

        public bool CommitNpcFullDrawRelease()
        {
            if (playerOwned ||
                !weaponEquipped ||
                !drawHeldLastFrame ||
                !arrowReady ||
                DrawNormalized < MinimumNpcReleaseCharge)
            {
                return false;
            }

            QueueRelease();
            drawHeldLastFrame = false;
            return pendingRelease;
        }

        private void Awake()
        {
            input ??= GetComponentInParent<PlayerInputSource>();
            characterRoot ??=
                input != null ? input.transform : transform.root;
            characterAimSource ??=
                characterRoot != null
                    ? characterRoot.GetComponent<CharacterAimSource>()
                    : GetComponentInParent<CharacterAimSource>();
            aimTarget ??= FindFirstObjectByType<CameraAimTarget>();
            BowShotReleaseCommitter releaseCommitter =
                GetComponent<BowShotReleaseCommitter>();
            if (releaseCommitter == null)
            {
                releaseCommitter =
                    gameObject.AddComponent<BowShotReleaseCommitter>();
            }
            releaseCommitter.Configure(this);
            ConfigureAudio();
            ResolveRaidController();
        }

        private void OnDisable()
        {
            pendingRelease = false;
            pendingReleaseAimLocked = false;
            CancelDraw(false);
        }

        private void Update()
        {
            // UI capture is cancellation, not a physical release of the string.
            // The grid toolkit runs between PlayerInputSource and BowWeapon, so
            // this branch is reached on the same frame that Tab opens.
            if (input != null && input.UserInterfaceCaptureActive)
            {
                CancelDraw(false);
                UpdateReadyPresentation(false);
                return;
            }

            bool drawHeld =
                DrawInputHeld;

            if (!weaponEquipped)
            {
                drawHeldLastFrame = false;
                UpdateReadyPresentation(false);
                return;
            }

            ResolveRaidController();
            if (!HasAmmunition)
            {
                if (drawHeldLastFrame)
                {
                    CancelDraw(false);
                }
                arrowReady = false;
                reloadRemaining = 0f;
                SetNockedArrowVisible(false);
                UpdateReadyPresentation(false);
                return;
            }
            if (!arrowReady && reloadRemaining <= 0f)
            {
                arrowReady = true;
                SetNockedArrowVisible(true);
            }

            UpdateReload();
            if (drawHeld)
            {
                if (!drawHeldLastFrame)
                {
                    heldDuration = 0f;
                    PlayPullbackAudio();
                    GameplayEventLog.Publish(
                        "bow-draw-started",
                        characterRoot != null
                            ? characterRoot.gameObject
                            : gameObject,
                        playerOwned ? "primary" : "ai-hold");
                }

                heldDuration += Time.deltaTime;
            }
            else if (drawHeldLastFrame)
            {
                QueueRelease();
            }

            drawHeldLastFrame = drawHeld;
            UpdateReadyPresentation(drawHeld);
        }

        private void UpdateReadyPresentation(bool drawHeld)
        {
            bool recoveringFromPlayerShot =
                playerOwned &&
                (pendingRelease || reloadRemaining > 0f);
            readyWeight = CalculateReadyWeight(
                readyWeight,
                drawHeld,
                Time.deltaTime,
                readyBlendDuration,
                recoveringFromPlayerShot
                    ? PostShotPoseDuration
                    : EffectiveReloadDuration,
                recoveringFromPlayerShot
                    ? PostShotPoseRemaining
                    : pendingRelease
                        ? EffectiveReloadDuration
                        : reloadRemaining,
                recoveringFromPlayerShot);
        }

        public static float CalculatePostShotPoseRemaining(
            float reloadRemaining,
            float reloadDuration,
            float poseDuration)
        {
            float elapsed = Mathf.Max(
                0f,
                Mathf.Max(0.01f, reloadDuration) -
                Mathf.Clamp(
                    reloadRemaining,
                    0f,
                    Mathf.Max(0.01f, reloadDuration)));
            return Mathf.Max(0f, poseDuration - elapsed);
        }

        public static float CalculateReadyWeight(
            float currentWeight,
            bool drawHeld,
            float deltaTime,
            float normalBlendDuration,
            float recoveryDuration,
            float recoveryRemaining,
            bool recoveringFromPlayerShot)
        {
            if (!drawHeld && recoveringFromPlayerShot)
            {
                return CalculatePostShotReadyWeight(
                    recoveryRemaining,
                    recoveryDuration,
                    normalBlendDuration);
            }

            return Mathf.MoveTowards(
                Mathf.Clamp01(currentWeight),
                drawHeld ? 1f : 0f,
                Mathf.Max(0f, deltaTime) /
                    Mathf.Max(0.01f, normalBlendDuration));
        }

        public static float CalculatePostShotReadyWeight(
            float recoveryRemaining,
            float recoveryDuration,
            float returnDuration)
        {
            float duration = Mathf.Max(0.01f, recoveryDuration);
            float finalReturnDuration = Mathf.Clamp(
                returnDuration,
                0.01f,
                duration);
            float remaining = Mathf.Clamp(
                recoveryRemaining,
                0f,
                duration);
            return remaining >= finalReturnDuration
                ? 1f
                : remaining / finalReturnDuration;
        }

        private void UpdateReload()
        {
            if (arrowReady || reloadRemaining <= 0f)
            {
                return;
            }

            reloadRemaining = Mathf.Max(0f, reloadRemaining - Time.deltaTime);
            if (reloadRemaining <= 0f)
            {
                arrowReady = HasAmmunition;
                releasedDrawPresentationWeight = 0f;
                SetNockedArrowVisible(arrowReady);
            }
        }

        private void QueueRelease()
        {
            StopPullbackAudio();
            float releaseCharge = DrawNormalized;
            bool npcCommittedRelease =
                playerOwned ||
                releaseCharge >= MinimumNpcReleaseCharge;
            if (CanFire && npcCommittedRelease)
            {
                releasedDrawPresentationWeight = releaseCharge;
                PlayReleaseAudio();
                // Preserve charge now, but resolve the rendered camera ray at
                // the end of the frame after Cinemachine has updated it.
                pendingRelease = true;
                pendingReleaseCharge = releaseCharge;
                pendingReleaseAimLocked = !playerOwned;
                if (pendingReleaseAimLocked)
                {
                    // EnemyBrain releases from Update, while the projectile is
                    // committed in LateUpdate. Preserve the compensated AI ray
                    // before recovery logic can replace it with a direct look
                    // ray on the following frame.
                    pendingReleaseAimRay = ResolveAimRay();
                }
            }
            else
            {
                GameplayEventLog.Publish(
                    "bow-draw-cancelled",
                    characterRoot != null
                        ? characterRoot.gameObject
                        : gameObject,
                    $"held={heldDuration:0.000};" +
                    $"draw={releaseCharge:0.000};" +
                    $"npcCommitted={npcCommittedRelease}");
            }

            heldDuration = 0f;
        }

        public void CommitPendingReleaseAtRenderedCamera()
        {
            if (!pendingRelease)
            {
                return;
            }

            pendingRelease = false;
            if (!isActiveAndEnabled || !weaponEquipped || !arrowReady)
            {
                pendingReleaseAimLocked = false;
                return;
            }

            FireArrow(pendingReleaseCharge);
        }

        private void FireArrow(float charge)
        {
            if (nockedArrow == null)
            {
                return;
            }

            ResolveRaidController();
            if (playerOwned &&
                raidController != null &&
                !raidController.TryConsumePlayerArrow())
            {
                arrowReady = false;
                reloadRemaining = 0f;
                SetNockedArrowVisible(false);
                return;
            }

            // AI commits only lethal, full-power shots. EnemyBrain also waits
            // for the presented draw to complete, but this weapon-level gate
            // prevents any alternate release path or frame-order edge case
            // from producing a weak NPC projectile.
            charge = playerOwned
                ? Mathf.Clamp01(charge)
                : 1f;

            float ballisticPower = Mathf.Pow(
                charge,
                Mathf.Max(1f, partialVelocityExponent));
            float shotSpeed = Mathf.Lerp(
                minimumArrowSpeed,
                maximumArrowSpeed,
                ballisticPower);
            Vector3 visibleTip = nockedArrow.TransformPoint(
                new Vector3(0f, 0f, 0.60f));
            Ray aimRay = pendingReleaseAimLocked
                ? pendingReleaseAimRay
                : ResolveAimRay();
            pendingReleaseAimLocked = false;
            Camera aimCamera = Camera.main;
            Vector3 aimRight = aimCamera != null
                ? aimCamera.transform.right.normalized
                : characterRoot != null
                    ? characterRoot.right.normalized
                    : Vector3.right;
            Vector3 direction = ResolveShotDirection(
                visibleTip,
                aimRay,
                aimRight,
                playerOwned);
            Quaternion rotation =
                BowArrowProjectile.CalculateFlightRotation(
                direction,
                bowRoot != null ? bowRoot.up : Vector3.up);
            Vector3 scale = nockedArrow.lossyScale;
            Vector3 spawnPosition =
                visibleTip -
                rotation * Vector3.forward * (0.60f * scale.z);

            GameObject projectile = Instantiate(
                nockedArrow.gameObject,
                spawnPosition,
                rotation);
            projectile.name = "Fired Arrow";
            projectile.transform.localScale = scale;
            projectile.SetActive(true);
            BowArrowProjectile arrow =
                projectile.AddComponent<BowArrowProjectile>();
            LastFiredProjectile = arrow;
            LastLaunchOrigin = visibleTip;
            LastAimOrigin = aimRay.origin;
            LastAimDirection = aimRay.direction;
            LastAimRight = aimRight;
            LastHorizontalAimError = Mathf.Abs(
                Vector3.Dot(direction, aimRight));
            LastHorizontalLaunchOffset = Mathf.Abs(
                Vector3.Dot(visibleTip - aimRay.origin, aimRight));
            arrow.Launch(
                characterRoot != null
                    ? characterRoot.gameObject
                    : gameObject,
                direction * shotSpeed,
                Mathf.Lerp(
                    minimumDamage,
                    maximumDamage,
                    ballisticPower) +
                    runtimeDamageBonus,
                arrowImpactClip,
                enemyHitFeedbackClip,
                headshotFeedbackClip,
                hitFeedbackAudioSource,
                playerOwned,
                arrowFlybyClip);
            firedArrowCount++;
            lastShotCharge = charge;
            LastShotSpeed = shotSpeed;
            LastShotDirection = direction;
            arrowReady = false;
            reloadRemaining = EffectiveReloadDuration;
            releasedDrawPresentationWeight = charge;
            SetNockedArrowVisible(false);
            GameplayEventLog.Publish(
                "bow-arrow-fired",
                characterRoot != null
                    ? characterRoot.gameObject
                    : gameObject,
                $"draw={charge:0.000};power={ballisticPower:0.000};speed={shotSpeed:0.00}");
            ArrowFired?.Invoke(charge);
        }

        public static Vector3 CalculateStraightShotDirection(
            Vector3 launchPoint,
            Ray crosshairRay,
            float alignmentDistance)
        {
            Vector3 rayDirection =
                crosshairRay.direction.sqrMagnitude > 0.000001f
                    ? crosshairRay.direction.normalized
                    : Vector3.forward;
            float launchDepth = Vector3.Dot(
                launchPoint - crosshairRay.origin,
                rayDirection);
            float safeAlignmentDistance = Mathf.Max(
                Mathf.Max(0.1f, alignmentDistance),
                launchDepth + 0.25f);
            Vector3 direction =
                crosshairRay.origin +
                rayDirection * safeAlignmentDistance -
                launchPoint;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : rayDirection;
        }

        public static bool IsAimHitAheadOfLaunch(
            Vector3 launchPoint,
            Ray aimRay,
            Vector3 hitPoint)
        {
            Vector3 rayDirection =
                aimRay.direction.sqrMagnitude > 0.000001f
                    ? aimRay.direction.normalized
                    : Vector3.forward;
            float launchDepth = Vector3.Dot(
                launchPoint - aimRay.origin,
                rayDirection);
            float hitDepth = Vector3.Dot(
                hitPoint - aimRay.origin,
                rayDirection);
            return hitDepth > launchDepth + 0.25f;
        }

        private Vector3 ResolveShotDirection(
            Vector3 launchOrigin,
            Ray aimRay,
            Vector3 aimRight,
            bool useCrosshairSurfaceDepth)
        {
            float alignmentDistance = maximumAimDistance;
            LastUsedElevatedTargetDepth = false;
            if (useCrosshairSurfaceDepth)
            {
                TryResolveCrosshairSurfaceDistance(
                    launchOrigin,
                    aimRay,
                    out alignmentDistance);

                // When the player intentionally aims above a humanoid to
                // compensate for gravity, the center ray no longer hits that
                // humanoid. Keep the vertical aim untouched, but borrow the
                // depth of a body intersecting the same screen-space vertical
                // column so over-shoulder parallax converges at its range.
                if (TryResolveElevatedHumanoidDepth(
                        launchOrigin,
                        aimRay,
                        out float humanoidDepth) &&
                    humanoidDepth < alignmentDistance - 0.25f)
                {
                    alignmentDistance = humanoidDepth;
                    LastUsedElevatedTargetDepth = true;
                }
            }

            LastCrosshairAlignmentDistance = alignmentDistance;
            Vector3 aimPoint = aimRay.GetPoint(alignmentDistance);
            LastCrosshairPoint = aimPoint;
            LastZeroGravityImpactPoint = aimPoint;
            return CalculateStraightShotDirection(
                launchOrigin,
                aimRay,
                alignmentDistance);
        }

        private bool TryResolveElevatedHumanoidDepth(
            Vector3 launchOrigin,
            Ray aimRay,
            out float alignmentDistance)
        {
            alignmentDistance = maximumAimDistance;
            Camera aimCamera = Camera.main;
            if (aimCamera == null)
            {
                return false;
            }

            Ray renderedCenterRay = aimCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));
            if (Vector3.Distance(
                    renderedCenterRay.origin,
                    aimRay.origin) > 0.05f ||
                Vector3.Angle(
                    renderedCenterRay.direction,
                    aimRay.direction) > 0.1f)
            {
                return false;
            }

            HumanoidDamageZone[] zones =
                FindObjectsByType<HumanoidDamageZone>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            float closestDepth = float.PositiveInfinity;
            for (int index = 0; index < zones.Length; index++)
            {
                HumanoidDamageZone zone = zones[index];
                Collider candidate =
                    zone != null ? zone.GetComponent<Collider>() : null;
                if (candidate == null ||
                    !candidate.enabled ||
                    (characterRoot != null &&
                        candidate.transform.IsChildOf(characterRoot)) ||
                    !TryGetVerticalColumnBounds(
                        aimCamera,
                        candidate.bounds,
                        out float minimumX,
                        out float maximumX,
                        out float maximumY) ||
                    0.5f < minimumX - 0.0015f ||
                    0.5f > maximumX + 0.0015f ||
                    maximumY > 0.505f ||
                    0.5f - maximumY > elevatedTargetDepthSearch)
                {
                    continue;
                }

                float candidateDepth = Vector3.Dot(
                    candidate.bounds.center - aimRay.origin,
                    aimRay.direction.normalized);
                if (candidateDepth >= closestDepth ||
                    candidateDepth > maximumAimDistance ||
                    !IsAimHitAheadOfLaunch(
                        launchOrigin,
                        aimRay,
                        aimRay.GetPoint(candidateDepth)))
                {
                    continue;
                }

                closestDepth = candidateDepth;
            }

            if (float.IsPositiveInfinity(closestDepth))
            {
                return false;
            }

            alignmentDistance = closestDepth;
            return true;
        }

        private static bool TryGetVerticalColumnBounds(
            Camera camera,
            Bounds bounds,
            out float minimumX,
            out float maximumX,
            out float maximumY)
        {
            minimumX = float.PositiveInfinity;
            maximumX = float.NegativeInfinity;
            maximumY = float.NegativeInfinity;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            bool foundForwardCorner = false;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 viewport = camera.WorldToViewportPoint(point);
                if (viewport.z <= 0f)
                {
                    continue;
                }

                foundForwardCorner = true;
                minimumX = Mathf.Min(minimumX, viewport.x);
                maximumX = Mathf.Max(maximumX, viewport.x);
                maximumY = Mathf.Max(maximumY, viewport.y);
            }

            return foundForwardCorner;
        }

        private bool TryResolveCrosshairSurfaceDistance(
            Vector3 launchOrigin,
            Ray aimRay,
            out float alignmentDistance)
        {
            alignmentDistance = maximumAimDistance;
            RaycastHit[] hits = Physics.RaycastAll(
                aimRay,
                maximumAimDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider candidate = hits[index].collider;
                if (candidate == null ||
                    HumanoidDamageHitboxRig.
                        IsRedundantMovementCollider(candidate) ||
                    hits[index].distance <= 0.001f ||
                    hits[index].distance >= closestDistance ||
                    (characterRoot != null &&
                        candidate.transform.IsChildOf(
                            characterRoot)) ||
                    !IsAimHitAheadOfLaunch(
                        launchOrigin,
                        aimRay,
                        hits[index].point))
                {
                    continue;
                }

                closestDistance = hits[index].distance;
            }

            if (float.IsPositiveInfinity(closestDistance))
            {
                return false;
            }

            alignmentDistance = closestDistance;
            return true;
        }

        private Ray ResolveAimRay()
        {
            characterAimSource ??=
                characterRoot != null
                    ? characterRoot.GetComponent<CharacterAimSource>()
                    : GetComponentInParent<CharacterAimSource>();
            if (characterAimSource != null &&
                characterAimSource.TryGetRay(out Ray characterAimRay))
            {
                return characterAimRay;
            }

            aimTarget ??= FindFirstObjectByType<CameraAimTarget>();
            if (aimTarget != null &&
                aimTarget.InspectionOrbitActive)
            {
                return new Ray(
                    aimTarget.InspectionAimOrigin,
                    aimTarget.AimDirection);
            }

            Camera aimCamera = Camera.main;
            if (aimCamera != null)
            {
                return aimCamera.ViewportPointToRay(
                    new Vector3(0.5f, 0.5f, 0f));
            }

            Vector3 origin = aimCamera != null
                ? aimCamera.transform.position
                : characterRoot != null
                    ? characterRoot.position + Vector3.up * 1.5f
                    : transform.position;
            Vector3 direction = aimTarget != null
                ? aimTarget.AimDirection
                : aimCamera != null
                    ? aimCamera.transform.forward
                    : bowRoot != null
                        ? bowRoot.forward
                        : transform.forward;
            return new Ray(
                origin,
                direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : Vector3.forward);
        }

        private void CancelDraw(bool publish)
        {
            if (publish && drawHeldLastFrame)
            {
                GameplayEventLog.Publish(
                    "bow-draw-cancelled",
                    characterRoot != null
                        ? characterRoot.gameObject
                        : gameObject,
                    "weapon-disabled");
            }

            drawHeldLastFrame = false;
            pendingRelease = false;
            pendingReleaseAimLocked = false;
            releasedDrawPresentationWeight = 0f;
            heldDuration = 0f;
            readyWeight = 0f;
            StopPullbackAudio();
        }

        private void SetNockedArrowVisible(bool visible)
        {
            if (nockedArrow != null)
            {
                nockedArrow.gameObject.SetActive(visible);
            }
        }

        private void ConfigureAudio()
        {
            releaseClip ??=
                Resources.Load<AudioClip>(ReleaseAudioResourcePath);
            EnsureAudioDataLoaded(releaseClip);
            EnsureAudioDataLoaded(enemyHitFeedbackClip);
            EnsureAudioDataLoaded(headshotFeedbackClip);
            playerOwned =
                IsPlayerTransform(characterRoot);
            minimumDamage = playerOwned ? 12f : 10f;
            maximumDamage = playerOwned
                ? 100f
                : EnemyDamageProfile.FullDrawTorsoDamage;
            if (bowAudioSource == null)
            {
                bowAudioSource =
                    gameObject.AddComponent<AudioSource>();
            }

            bowAudioSource.playOnAwake = false;
            bowAudioSource.loop = false;
            bowAudioSource.dopplerLevel = 0f;
            bowAudioSource.rolloffMode =
                AudioRolloffMode.Logarithmic;
            pullbackVolume = playerOwned
                ? 0.09f
                : 0.14f;
            releaseVolume = 0.30f;
            bowAudioSource.spatialBlend =
                playerOwned ? 0f : 1f;
            bowAudioSource.minDistance = 1.4f;
            bowAudioSource.maxDistance = 20f;

            if (releaseAudioSource == null)
            {
                GameObject releaseHost =
                    characterRoot != null
                        ? characterRoot.gameObject
                        : gameObject;
                releaseAudioSource =
                    releaseHost.AddComponent<AudioSource>();
            }
            releaseAudioSource.enabled = true;
            releaseAudioSource.playOnAwake = false;
            releaseAudioSource.loop = false;
            releaseAudioSource.pitch = 1f;
            releaseAudioSource.volume = 1f;
            releaseAudioSource.mute = false;
            releaseAudioSource.dopplerLevel = 0f;
            releaseAudioSource.priority = 0;
            releaseAudioSource.ignoreListenerPause = true;
            releaseAudioSource.bypassEffects = true;
            releaseAudioSource.bypassListenerEffects = true;
            releaseAudioSource.bypassReverbZones = true;
            releaseAudioSource.spatialBlend =
                playerOwned ? 0f : 1f;
            releaseAudioSource.rolloffMode =
                AudioRolloffMode.Logarithmic;
            releaseAudioSource.minDistance = 1.5f;
            releaseAudioSource.maxDistance = 28f;

            if (hitFeedbackAudioSource == null)
            {
                GameObject feedbackHost =
                    playerOwned && characterRoot != null
                        ? characterRoot.gameObject
                        : gameObject;
                hitFeedbackAudioSource =
                    feedbackHost.AddComponent<AudioSource>();
            }
            hitFeedbackAudioSource.enabled = true;
            hitFeedbackAudioSource.playOnAwake = false;
            hitFeedbackAudioSource.loop = false;
            hitFeedbackAudioSource.spatialBlend = 0f;
            hitFeedbackAudioSource.dopplerLevel = 0f;
            hitFeedbackAudioSource.priority = 32;
            hitFeedbackAudioSource.volume = 1f;
            hitFeedbackAudioSource.mute = false;
            hitFeedbackAudioSource.ignoreListenerPause = true;
        }

        private void ResolveRaidController()
        {
            if (!playerOwned || raidController != null)
            {
                return;
            }
            raidController = FindFirstObjectByType<RaidPrototypeController>();
        }

        private static bool IsPlayerTransform(
            Transform candidate)
        {
            for (Transform current = candidate;
                 current != null;
                 current = current.parent)
            {
                if (current.CompareTag("Player"))
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureAudioDataLoaded(
            AudioClip clip)
        {
            if (clip != null &&
                clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }

        private void PlayPullbackAudio()
        {
            if (bowAudioSource == null ||
                pullbackClip == null)
            {
                return;
            }

            bowAudioSource.Stop();
            // The source clip is a short rising string-creak. Slowing it
            // slightly keeps its peak aligned with the visible draw motion.
            bowAudioSource.pitch = pullbackPitch;
            bowAudioSource.clip = pullbackClip;
            bowAudioSource.volume = pullbackVolume;
            bowAudioSource.Play();
        }

        private void StopPullbackAudio()
        {
            if (bowAudioSource != null &&
                bowAudioSource.clip == pullbackClip)
            {
                bowAudioSource.Stop();
                bowAudioSource.clip = null;
            }
        }

        private void PlayReleaseAudio()
        {
            if (bowAudioSource == null || releaseClip == null)
            {
                return;
            }

            bowAudioSource.Stop();
            bowAudioSource.clip = releaseClip;
            bowAudioSource.pitch = 1f;
            bowAudioSource.volume = releaseVolume;
            bowAudioSource.mute = false;
            bowAudioSource.Play();
            LastReleaseAudioSource = bowAudioSource;
            releasePlaybackCount++;
        }
    }
}
