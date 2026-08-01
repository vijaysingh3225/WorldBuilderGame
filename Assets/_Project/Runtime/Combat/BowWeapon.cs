using System;
using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Combat
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class BowWeapon : MonoBehaviour
    {
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Transform bowRoot;
        [SerializeField] private Transform nockedArrow;
        [SerializeField] private AudioClip pullbackClip;
        [SerializeField] private AudioClip arrowImpactClip;
        [SerializeField] private AudioClip enemyHitFeedbackClip;
        [SerializeField] private AudioClip headshotFeedbackClip;
        [SerializeField] private AudioSource bowAudioSource;
        [SerializeField] private AudioSource hitFeedbackAudioSource;
        [SerializeField, Range(0f, 1f)] private float pullbackVolume = 0.30f;
        [SerializeField, Range(0.5f, 1.5f)] private float pullbackPitch = 0.62f;
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

        public event Action<float> ArrowFired;

        public bool WeaponEquipped => weaponEquipped;
        public bool IsDrawing => weaponEquipped && drawHeldLastFrame;
        public bool ArrowReady => arrowReady;
        public bool CanFire =>
            arrowReady && heldDuration >= minimumHoldDuration;
        public float HeldDuration => heldDuration;
        public float ReadyWeight => readyWeight;
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
        public BowArrowProjectile LastFiredProjectile { get; private set; }
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
            arrowImpactClip != null &&
            enemyHitFeedbackClip != null &&
            headshotFeedbackClip != null &&
            bowAudioSource != null;
        public float PullbackVolume => pullbackVolume;
        public float PullbackPitch => pullbackPitch;
        public AudioClip EnemyHitFeedbackClip =>
            enemyHitFeedbackClip;
        public AudioClip HeadshotFeedbackClip =>
            headshotFeedbackClip;
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
        public float MaximumArrowSpeed => maximumArrowSpeed;
        public float RuntimeDamageBonus => runtimeDamageBonus;
        public float PartialVelocityExponent =>
            partialVelocityExponent;
        public float MinimumDamage => minimumDamage;
        public float MaximumDamage => maximumDamage;

        public void Configure(
            PlayerInputSource intentSource,
            Transform root,
            Transform equippedBow,
            Transform arrow,
            AudioClip drawClip = null,
            AudioClip impactClip = null,
            AudioClip enemyHitClip = null,
            AudioClip headshotClip = null)
        {
            input = intentSource;
            characterRoot = root;
            bowRoot = equippedBow;
            nockedArrow = arrow;
            pullbackClip = drawClip;
            arrowImpactClip = impactClip;
            enemyHitFeedbackClip = enemyHitClip;
            headshotFeedbackClip = headshotClip;
            ConfigureAudio();
            SetWeaponEquipped(false);
        }

        public void SetWeaponEquipped(bool equipped)
        {
            weaponEquipped = equipped;
            CancelDraw(false);
            reloadRemaining = 0f;
            arrowReady = equipped;
            SetNockedArrowVisible(equipped);
        }

        public void SetRuntimeDamageBonus(float bonus)
        {
            runtimeDamageBonus = bonus;
        }

        public void AbortDraw()
        {
            CancelDraw(false);
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
        }

        private void OnDisable()
        {
            pendingRelease = false;
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
                return;
            }

            bool drawHeld =
                weaponEquipped &&
                input != null &&
                input.CurrentIntent.BlockHeld;

            float targetReadyWeight = drawHeld ? 1f : 0f;
            readyWeight = Mathf.MoveTowards(
                readyWeight,
                targetReadyWeight,
                Time.deltaTime / Mathf.Max(0.01f, readyBlendDuration));

            if (!weaponEquipped)
            {
                drawHeldLastFrame = false;
                return;
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
                        "secondary");
                }

                heldDuration += Time.deltaTime;
            }
            else if (drawHeldLastFrame)
            {
                QueueRelease();
            }

            drawHeldLastFrame = drawHeld;
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
                arrowReady = true;
                SetNockedArrowVisible(true);
            }
        }

        private void QueueRelease()
        {
            StopPullbackAudio();
            if (CanFire)
            {
                // Preserve charge now, but resolve the rendered camera ray at
                // the end of the frame after Cinemachine has updated it.
                pendingRelease = true;
                pendingReleaseCharge = DrawNormalized;
            }
            else
            {
                GameplayEventLog.Publish(
                    "bow-draw-cancelled",
                    characterRoot != null
                        ? characterRoot.gameObject
                        : gameObject,
                    $"held={heldDuration:0.000}");
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

            float ballisticPower = Mathf.Pow(
                charge,
                Mathf.Max(1f, partialVelocityExponent));
            float shotSpeed = Mathf.Lerp(
                minimumArrowSpeed,
                maximumArrowSpeed,
                ballisticPower);
            Vector3 visibleTip = nockedArrow.TransformPoint(
                new Vector3(0f, 0f, 0.60f));
            Ray aimRay = ResolveAimRay();
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
                playerOwned);

            firedArrowCount++;
            lastShotCharge = charge;
            LastShotSpeed = shotSpeed;
            LastShotDirection = direction;
            arrowReady = false;
            reloadRemaining = reloadDuration;
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
            EnsureAudioDataLoaded(enemyHitFeedbackClip);
            EnsureAudioDataLoaded(headshotFeedbackClip);
            playerOwned =
                input != null ||
                IsPlayerTransform(characterRoot);
            minimumDamage = playerOwned ? 12f : 10f;
            maximumDamage = playerOwned ? 100f : 34f;
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
            bowAudioSource.spatialBlend =
                playerOwned ? 0f : 1f;
            bowAudioSource.minDistance = 1.4f;
            bowAudioSource.maxDistance = 20f;

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
    }
}
