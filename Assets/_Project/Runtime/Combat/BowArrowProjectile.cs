using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Combat
{
    // Humanoid pose presenters finish through order 900 and their anatomical
    // hitboxes synchronize at 950. Resolve flight contacts and embedded-arrow
    // following afterward so both use the final visible pose for this frame.
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class BowArrowProjectile : MonoBehaviour
    {
        public readonly struct FlightSignal
        {
            public FlightSignal(BowArrowProjectile projectile, GameObject owner, Vector3 start, Vector3 end, Vector3 direction)
            {
                Projectile = projectile;
                Owner = owner;
                Start = start;
                End = end;
                Direction = direction;
            }

            public BowArrowProjectile Projectile { get; }
            public GameObject Owner { get; }
            public Vector3 Start { get; }
            public Vector3 End { get; }
            public Vector3 Direction { get; }
        }

        public readonly struct ImpactSignal
        {
            public ImpactSignal(BowArrowProjectile projectile, GameObject owner, Vector3 point, Vector3 direction)
            {
                Projectile = projectile;
                Owner = owner;
                Point = point;
                Direction = direction;
            }

            public BowArrowProjectile Projectile { get; }
            public GameObject Owner { get; }
            public Vector3 Point { get; }
            public Vector3 Direction { get; }
        }

        public static event Action<FlightSignal> ArrowInFlight;
        public static event Action<ImpactSignal> ArrowImpacted;

        private const float FlyingLifetime = 20f;
        private const float StuckLifetime = 45f;
        private const float SurfaceIntersectionLocalZ = 0.605f;
        private const float TrailLifetime = 0.14f;
        private const float FlybyTriggerRadius = 7f;
        private const float FlybyMinimumDistance = 1.1f;
        private const float FlybyMaximumDistance = 11f;

        private static Material sharedTrailMaterial;
        private GameObject owner;
        private Rigidbody body;
        private CapsuleCollider arrowCollider;
        private TrailRenderer flightTrail;
        private Transform stuckTo;
        private Vector3 stuckLocalPosition;
        private Vector3 stuckLocalHitPoint;
        private Quaternion stuckLocalRotation;
        private Vector3 launchWorldScale;
        private Quaternion lastFlightRotation;
        private Vector3 flightVelocity;
        private Vector3 flightTipPosition;
        private float damage;
        private AudioClip impactClip;
        private AudioClip enemyHitFeedbackClip;
        private AudioClip headshotFeedbackClip;
        private AudioClip flybyClip;
        private AudioSource playerFeedbackAudioSource;
        private Transform flybyTarget;
        private GameObject flybyEmitter;
        private AudioSource flybyAudioSource;
        private bool playerHitFeedbackEnabled;
        private bool flybyPlayed;
        private bool flybyStoppedByImpact;
        private float launchedAt;
        private bool stuck;
        private readonly RaycastHit[] flightHitBuffer =
            new RaycastHit[16];

        public bool IsStuck => stuck;
        public Vector3 HitPoint { get; private set; }
        public Vector3 ImpactDirection { get; private set; }
        public Vector3 FlightTipPosition => flightTipPosition;
        public Vector3 LaunchWorldScale => launchWorldScale;
        public float SurfaceIntersectionDistance { get; private set; }
        public TrailRenderer FlightTrail => flightTrail;
        public Transform StuckTo => stuckTo;
        public AudioClip EnemyHitFeedbackClip =>
            enemyHitFeedbackClip;
        public AudioClip HeadshotFeedbackClip =>
            headshotFeedbackClip;
        public AudioClip FlybyClip => flybyClip;
        public bool FlybyPlayed => flybyPlayed;
        public AudioSource ActiveFlybyAudioSource =>
            flybyAudioSource;
        public bool FlybyStoppedByImpact =>
            flybyStoppedByImpact;
        public bool LastImpactDamagedEnemy
        {
            get;
            private set;
        }
        public bool LastImpactWasHeadshot
        {
            get;
            private set;
        }


        public void Launch(
            GameObject instigator,
            Vector3 velocity,
            float shotDamage,
            AudioClip hitClip = null,
            AudioClip enemyHitClip = null,
            AudioClip headshotClip = null,
            AudioSource feedbackSource = null,
            bool enablePlayerHitFeedback = false,
            AudioClip arrowFlybyClip = null)
        {
            owner = instigator;
            damage = Mathf.Max(0f, shotDamage);
            impactClip = hitClip;
            enemyHitFeedbackClip = enemyHitClip;
            headshotFeedbackClip = headshotClip;
            flybyClip = arrowFlybyClip;
            playerFeedbackAudioSource =
                feedbackSource;
            playerHitFeedbackEnabled =
                enablePlayerHitFeedback ||
                IsOwnedByPlayer(instigator);
            GameObject playerObject =
                !playerHitFeedbackEnabled && flybyClip != null
                    ? GameObject.FindGameObjectWithTag("Player")
                    : null;
            flybyTarget = playerObject != null
                ? playerObject.transform
                : null;
            flybyPlayed = false;
            flybyStoppedByImpact = false;
            launchedAt = Time.time;
            launchWorldScale = transform.lossyScale;
            lastFlightRotation = transform.rotation;
            ImpactDirection =
                velocity.sqrMagnitude > 0.0001f
                    ? velocity.normalized
                    : transform.forward;
            flightVelocity = velocity;
            flightTipPosition = transform.TransformPoint(
                Vector3.forward * SurfaceIntersectionLocalZ);

            CreateFlightTrail();

            arrowCollider = gameObject.AddComponent<CapsuleCollider>();
            arrowCollider.direction = 2;
            arrowCollider.center = new Vector3(0f, 0f, 0.53f);
            arrowCollider.height = 0.16f;
            arrowCollider.radius = 0.025f;
            arrowCollider.enabled = false;

            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.075f;
            body.useGravity = false;
            body.linearDamping = 0.01f;
            body.angularDamping = 0.05f;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (owner != null)
            {
                Collider[] ownerColliders =
                    owner.GetComponentsInChildren<Collider>(true);
                for (int index = 0;
                     index < ownerColliders.Length;
                     index++)
                {
                    Physics.IgnoreCollision(
                        arrowCollider,
                        ownerColliders[index],
                        true);
                }
            }
        }

        private void AdvanceFlight(float step)
        {
            if (stuck ||
                body == null ||
                step <= 0f)
            {
                return;
            }

            Vector3 startTip = flightTipPosition;
            Vector3 displacement =
                flightVelocity * step +
                Physics.gravity * (0.5f * step * step);
            Vector3 segmentDirection =
                displacement.sqrMagnitude > 0.000001f
                    ? displacement.normalized
                    : ImpactDirection;
            Quaternion segmentRotation =
                CalculateFlightRotation(
                    segmentDirection,
                    lastFlightRotation * Vector3.up);
            if (TryGetFirstFlightHit(
                    startTip,
                    displacement,
                    out RaycastHit hit))
            {
                flightTipPosition = hit.point;
                lastFlightRotation = segmentRotation;
                ImpactDirection = segmentDirection;
                PublishFlightSignal(startTip, flightTipPosition, segmentDirection);
                ResolveImpact(
                    hit.collider,
                    hit.point);
                return;
            }

            flightTipPosition += displacement;
            TryPlayFlyby(startTip, flightTipPosition);
            PublishFlightSignal(startTip, flightTipPosition, segmentDirection);
            flightVelocity += Physics.gravity * step;
            if (flightVelocity.sqrMagnitude > 0.04f)
            {
                Quaternion flightRotation = CalculateFlightRotation(
                    flightVelocity.normalized,
                    segmentRotation * Vector3.up);
                PlaceTipAt(
                    flightTipPosition,
                    flightRotation);
                lastFlightRotation = flightRotation;
                ImpactDirection = flightVelocity.normalized;
            }

            if (Time.time - launchedAt >= FlyingLifetime)
            {
                Destroy(gameObject);
            }
        }

        private bool TryGetFirstFlightHit(
            Vector3 origin,
            Vector3 displacement,
            out RaycastHit closestHit)
        {
            closestHit = default;
            float distance = displacement.magnitude;
            if (distance < 0.000001f)
            {
                return false;
            }

            RaycastHit[] hits = flightHitBuffer;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                displacement / distance,
                hits,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            // A saturated non-alloc buffer does not guarantee it contains the
            // nearest valid impact. Fall back to the complete query only then
            // so arrow collision behavior remains unchanged in dense scenes.
            if (hitCount == hits.Length)
            {
                hits = Physics.RaycastAll(
                    origin,
                    displacement / distance,
                    distance,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = hits[index].collider;
                if (candidate == null ||
                    HumanoidDamageHitboxRig.
                        IsRedundantMovementCollider(candidate) ||
                    candidate.transform.IsChildOf(transform) ||
                    (owner != null &&
                        candidate.transform.IsChildOf(
                            owner.transform)) ||
                    hits[index].distance <= 0.001f ||
                    hits[index].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[index].distance;
                closestHit = hits[index];
            }

            return closestDistance < float.PositiveInfinity;
        }

        public static Quaternion CalculateFlightRotation(
            Vector3 direction,
            Vector3 preferredUp)
        {
            Vector3 forward = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 stableUp = Vector3.ProjectOnPlane(
                preferredUp,
                forward);
            if (stableUp.sqrMagnitude < 0.000001f)
            {
                stableUp = Vector3.ProjectOnPlane(
                    Vector3.forward,
                    forward);
            }
            if (stableUp.sqrMagnitude < 0.000001f)
            {
                stableUp = Vector3.ProjectOnPlane(
                    Vector3.right,
                    forward);
            }

            return Quaternion.LookRotation(
                forward,
                stableUp.normalized);
        }

        private void PlaceTipAt(
            Vector3 tipPosition,
            Quaternion rotation)
        {
            float tipDistance =
                SurfaceIntersectionLocalZ *
                Mathf.Abs(launchWorldScale.z);
            Vector3 rootPosition =
                tipPosition -
                rotation * Vector3.forward * tipDistance;
            transform.SetPositionAndRotation(
                rootPosition,
                rotation);
        }

        private void LateUpdate()
        {
            if (!stuck)
            {
                AdvanceFlight(Time.deltaTime);
                return;
            }

            if (stuckTo == null)
            {
                return;
            }

            transform.SetPositionAndRotation(
                stuckTo.TransformPoint(stuckLocalPosition),
                stuckTo.rotation * stuckLocalRotation);
            transform.localScale = launchWorldScale;
            HitPoint =
                stuckTo.TransformPoint(stuckLocalHitPoint);
        }

        private void ResolveImpact(
            Collider hitCollider,
            Vector3 hitPoint)
        {
            StopFlybyAudioForImpact();
            HitPoint = hitPoint;
            ImpactDirection =
                lastFlightRotation * Vector3.forward;
            ArrowImpacted?.Invoke(
                new ImpactSignal(this, owner, hitPoint, ImpactDirection));
            HumanoidDamageZone damageZone =
                hitCollider.GetComponentInParent<
                    HumanoidDamageZone>(true);
            EnemyDamageProfile enemyProfile =
                hitCollider.GetComponentInParent<
                    EnemyDamageProfile>(true);
            HumanoidRagdoll ragdoll =
                hitCollider.GetComponentInParent<
                    HumanoidRagdoll>(true);
            HumanoidHitRegion hitRegion =
                damageZone != null
                    ? damageZone.Region
                    : enemyProfile != null
                        ? enemyProfile.ResolveHitRegion(
                            hitPoint)
                        : HumanoidHitRegion.Torso;
            Transform attachment =
                damageZone != null
                    ? damageZone.ResolveAttachmentTransform(
                        hitPoint)
                    : enemyProfile != null
                        ? enemyProfile.ResolveAttachmentTransform(
                            hitPoint)
                        : ragdoll != null
                            ? ragdoll.ResolveAttachmentTransform(
                                hitPoint)
                        : hitCollider.transform;
            // Start the world impact and player confirmation before damage/death
            // callbacks can build a ragdoll. Both sounds now begin in this same
            // collision callback at the arrow contact time.
            PlayImpactAudio();
            bool damageApplied = DamageService.TryApply(
                hitCollider,
                new DamageRequest(
                    owner,
                    damage,
                    hitPoint,
                    ImpactDirection,
                    "prototype-bow"));
            LastImpactDamagedEnemy =
                damageApplied &&
                enemyProfile != null;
            LastImpactWasHeadshot =
                LastImpactDamagedEnemy &&
                hitRegion == HumanoidHitRegion.Head;
            StickTo(
                attachment,
                hitPoint,
                lastFlightRotation);
        }

        private void PublishFlightSignal(Vector3 start, Vector3 end, Vector3 direction)
        {
            ArrowInFlight?.Invoke(
                new FlightSignal(this, owner, start, end, direction));
        }

        private void TryPlayFlyby(
            Vector3 segmentStart,
            Vector3 segmentEnd)
        {
            if (flybyPlayed ||
                flybyClip == null ||
                flybyTarget == null ||
                playerHitFeedbackEnabled)
            {
                return;
            }

            Vector3 listenerPoint =
                flybyTarget.position + Vector3.up * 1.2f;
            Vector3 closestPoint = ClosestPointOnSegment(
                segmentStart,
                segmentEnd,
                listenerPoint);
            if (Vector3.Distance(
                    closestPoint,
                    listenerPoint) > FlybyTriggerRadius)
            {
                return;
            }

            flybyPlayed = true;
            EnsureAudioDataLoaded(flybyClip);
            flybyEmitter = new GameObject(
                "Arrow Flyby Audio");
            flybyEmitter.transform.position = closestPoint;
            flybyAudioSource =
                flybyEmitter.AddComponent<AudioSource>();
            AudioSource source = flybyAudioSource;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode =
                AudioRolloffMode.Logarithmic;
            source.minDistance = FlybyMinimumDistance;
            source.maxDistance = FlybyMaximumDistance;
            source.priority = 48;
            source.PlayOneShot(flybyClip, 0.90f);
            if (Application.isPlaying)
            {
                Destroy(
                    flybyEmitter,
                    Mathf.Max(
                        0.25f,
                        flybyClip.length + 0.1f));
            }
        }

        private void StopFlybyAudioForImpact()
        {
            if (flybyAudioSource == null && flybyEmitter == null)
            {
                return;
            }

            flybyAudioSource?.Stop();
            flybyStoppedByImpact = true;
            if (flybyEmitter != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(flybyEmitter);
                }
                else
                {
                    DestroyImmediate(flybyEmitter);
                }
            }
            flybyAudioSource = null;
            flybyEmitter = null;
        }

        private static Vector3 ClosestPointOnSegment(
            Vector3 start,
            Vector3 end,
            Vector3 point)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return start;
            }

            float progress = Mathf.Clamp01(
                Vector3.Dot(point - start, segment) /
                lengthSquared);
            return start + segment * progress;
        }

        private void PlayPlayerHitFeedback(
            bool headshot)
        {
            AudioClip selectedClip =
                headshot &&
                headshotFeedbackClip != null
                    ? headshotFeedbackClip
                    : enemyHitFeedbackClip;
            if (selectedClip == null)
            {
                return;
            }

            AudioSource source =
                playerFeedbackAudioSource;
            if (source == null)
            {
                source =
                    gameObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.mute = false;
            source.ignoreListenerPause = true;
            EnsureAudioDataLoaded(enemyHitFeedbackClip);
            EnsureAudioDataLoaded(headshotFeedbackClip);
            source.PlayOneShot(
                selectedClip,
                PlayerHitFeedbackEmitter.
                    FeedbackVolume);
        }

        private static bool IsOwnedByPlayer(
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

        private void PlayImpactAudio()
        {
            if (impactClip == null)
            {
                return;
            }

            AudioSource source =
                gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 2f;
            source.maxDistance = 28f;
            source.rolloffMode =
                AudioRolloffMode.Logarithmic;
            source.PlayOneShot(impactClip, 0.88f);
        }

        private void CreateFlightTrail()
        {
            GameObject trailObject =
                new GameObject("Arrow Flight Trail");
            trailObject.transform.SetParent(
                transform,
                false);
            trailObject.transform.localPosition =
                new Vector3(0f, 0f, -0.035f);
            flightTrail =
                trailObject.AddComponent<TrailRenderer>();
            flightTrail.emitting = false;
            flightTrail.time = TrailLifetime;
            flightTrail.minVertexDistance = 0.035f;
            flightTrail.widthCurve =
                new AnimationCurve(
                    new Keyframe(0f, 0.018f),
                    new Keyframe(1f, 0.0025f));
            flightTrail.colorGradient =
                new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(
                            Color.white,
                            0f),
                        new GradientColorKey(
                            new Color(
                                0.82f,
                                0.90f,
                                1f),
                            1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(0.82f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    }
                };
            flightTrail.alignment =
                LineAlignment.View;
            flightTrail.textureMode =
                LineTextureMode.Stretch;
            flightTrail.numCornerVertices = 2;
            flightTrail.numCapVertices = 2;
            flightTrail.shadowCastingMode =
                ShadowCastingMode.Off;
            flightTrail.receiveShadows = false;
            flightTrail.generateLightingData = false;
            flightTrail.sharedMaterial =
                GetTrailMaterial();
            flightTrail.Clear();
            flightTrail.emitting = true;
        }

        private static Material GetTrailMaterial()
        {
            if (sharedTrailMaterial != null)
            {
                return sharedTrailMaterial;
            }

            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find(
                    "Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            sharedTrailMaterial =
                new Material(shader)
                {
                    name = "Runtime Arrow Trail",
                    hideFlags = HideFlags.HideAndDontSave
                };
            if (sharedTrailMaterial.HasProperty("_Color"))
            {
                sharedTrailMaterial.SetColor(
                    "_Color",
                    Color.white);
            }
            if (sharedTrailMaterial.HasProperty("_BaseColor"))
            {
                sharedTrailMaterial.SetColor(
                    "_BaseColor",
                    Color.white);
            }
            return sharedTrailMaterial;
        }

        private void StickTo(
            Transform hitTransform,
            Vector3 hitPoint,
            Quaternion impactRotation)
        {
            stuck = true;
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            if (arrowCollider != null)
            {
                arrowCollider.enabled = false;
            }
            if (flightTrail != null)
            {
                flightTrail.emitting = false;
            }

            SurfaceIntersectionDistance =
                SurfaceIntersectionLocalZ *
                Mathf.Abs(launchWorldScale.z);
            Vector3 impactForward =
                impactRotation * Vector3.forward;
            Vector3 embeddedRootPosition =
                hitPoint -
                impactForward * SurfaceIntersectionDistance;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(
                embeddedRootPosition,
                impactRotation);
            transform.localScale = launchWorldScale;
            stuckTo = hitTransform;
            if (stuckTo != null)
            {
                stuckLocalPosition =
                    stuckTo.InverseTransformPoint(
                        embeddedRootPosition);
                stuckLocalHitPoint =
                    stuckTo.InverseTransformPoint(hitPoint);
                stuckLocalRotation =
                    Quaternion.Inverse(stuckTo.rotation) *
                    impactRotation;
            }

            GameplayEventLog.Publish(
                "bow-arrow-stuck",
                owner,
                hitTransform != null
                    ? hitTransform.name
                    : "unknown");
            if (Application.isPlaying)
            {
                Destroy(gameObject, StuckLifetime);
            }
        }
    }
}
