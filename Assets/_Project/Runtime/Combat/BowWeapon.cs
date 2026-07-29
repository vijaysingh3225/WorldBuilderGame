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
        [SerializeField] private AudioSource bowAudioSource;
        [SerializeField, Range(0f, 1f)] private float pullbackVolume = 0.30f;
        [SerializeField] private CameraAimTarget aimTarget;
        [SerializeField] private CharacterAimSource characterAimSource;
        [SerializeField] private LayerMask aimCollisionMask = ~(1 << 2);
        [SerializeField, Min(10f)] private float maximumAimDistance = 150f;
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
        private int firedArrowCount;
        private float lastShotCharge;

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
        public Vector3 LastCrosshairPoint { get; private set; }
        public Vector3 LastZeroGravityImpactPoint { get; private set; }
        public bool AudioConfigured =>
            pullbackClip != null &&
            arrowImpactClip != null &&
            bowAudioSource != null;
        public float PullbackVolume => pullbackVolume;
        public float FullDrawDuration => fullDrawDuration;
        public float MaximumArrowSpeed => maximumArrowSpeed;
        public float PartialVelocityExponent =>
            partialVelocityExponent;

        public void Configure(
            PlayerInputSource intentSource,
            Transform root,
            Transform equippedBow,
            Transform arrow,
            AudioClip drawClip = null,
            AudioClip impactClip = null)
        {
            input = intentSource;
            characterRoot = root;
            bowRoot = equippedBow;
            nockedArrow = arrow;
            pullbackClip = drawClip;
            arrowImpactClip = impactClip;
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
            ConfigureAudio();
        }

        private void OnDisable()
        {
            CancelDraw(false);
        }

        private void Update()
        {
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
                ReleaseDraw();
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

        private void ReleaseDraw()
        {
            StopPullbackAudio();
            if (CanFire)
            {
                FireArrow();
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

        private void FireArrow()
        {
            if (nockedArrow == null)
            {
                return;
            }

            float charge = DrawNormalized;
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
            Vector3 direction = ResolveShotDirection(
                visibleTip,
                aimRay);
            Quaternion rotation = Quaternion.LookRotation(
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
            Camera aimCamera = Camera.main;
            LastAimRight = aimCamera != null
                ? aimCamera.transform.right
                : characterRoot != null
                    ? characterRoot.right
                    : Vector3.right;
            arrow.Launch(
                characterRoot != null
                    ? characterRoot.gameObject
                    : gameObject,
                direction * shotSpeed,
                Mathf.Lerp(
                    minimumDamage,
                    maximumDamage,
                    ballisticPower),
                arrowImpactClip);

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

        private Vector3 ResolveShotDirection(
            Vector3 launchOrigin,
            Ray aimRay)
        {
            Vector3 aimPoint = aimRay.GetPoint(maximumAimDistance);
            if (TryGetFirstAimHit(
                    aimRay.origin,
                    aimRay.direction,
                    0f,
                    maximumAimDistance,
                    out RaycastHit crosshairHit))
            {
                aimPoint = crosshairHit.point;
            }

            LastCrosshairPoint = aimPoint;
            Vector3 correctedAimPoint = aimPoint;
            Vector3 direction = aimRay.direction;
            LastZeroGravityImpactPoint = aimPoint;
            for (int iteration = 0; iteration < 6; iteration++)
            {
                direction = correctedAimPoint - launchOrigin;
                direction = direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : aimRay.direction;
                if (!TryGetFirstAimHit(
                        launchOrigin,
                        direction,
                        0.025f,
                        maximumAimDistance,
                        out RaycastHit projectileHit))
                {
                    LastZeroGravityImpactPoint =
                        launchOrigin +
                        direction * maximumAimDistance;
                    break;
                }

                LastZeroGravityImpactPoint = projectileHit.point;
                Vector3 surfaceError =
                    aimPoint - projectileHit.point;
                Vector3 crosshairPlaneError =
                    Vector3.ProjectOnPlane(
                        surfaceError,
                        aimRay.direction);
                if (crosshairPlaneError.sqrMagnitude <= 0.000004f)
                {
                    break;
                }

                correctedAimPoint += Vector3.ClampMagnitude(
                    crosshairPlaneError,
                    0.30f);
            }

            return direction;
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

        private bool TryGetFirstAimHit(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            out RaycastHit closestHit)
        {
            RaycastHit[] hits = radius > 0f
                ? Physics.SphereCastAll(
                    origin,
                    radius,
                    direction,
                    distance,
                    aimCollisionMask,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(
                    origin,
                    direction,
                    distance,
                    aimCollisionMask,
                    QueryTriggerInteraction.Ignore);
            closestHit = default;
            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null ||
                    (characterRoot != null &&
                        collider.transform.IsChildOf(
                            characterRoot)))
                {
                    continue;
                }

                if (hits[index].distance < closestDistance)
                {
                    closestDistance = hits[index].distance;
                    closestHit = hits[index];
                }
            }

            return closestDistance < float.PositiveInfinity;
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
            if (bowAudioSource == null)
            {
                bowAudioSource =
                    gameObject.AddComponent<AudioSource>();
            }

            bowAudioSource.playOnAwake = false;
            bowAudioSource.loop = false;
            bowAudioSource.spatialBlend = 0.20f;
            bowAudioSource.dopplerLevel = 0f;
        }

        private void PlayPullbackAudio()
        {
            if (bowAudioSource == null ||
                pullbackClip == null)
            {
                return;
            }

            bowAudioSource.Stop();
            bowAudioSource.pitch = 1f;
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
