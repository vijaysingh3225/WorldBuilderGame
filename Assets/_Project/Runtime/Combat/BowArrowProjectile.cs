using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class BowArrowProjectile : MonoBehaviour
    {
        private const float FlyingLifetime = 20f;
        private const float StuckLifetime = 45f;
        private const float SurfaceIntersectionLocalZ = 0.605f;
        private const float TrailLifetime = 0.14f;

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
        private float damage;
        private AudioClip impactClip;
        private float launchedAt;
        private bool stuck;

        public bool IsStuck => stuck;
        public Vector3 HitPoint { get; private set; }
        public Vector3 ImpactDirection { get; private set; }
        public Vector3 LaunchWorldScale => launchWorldScale;
        public float SurfaceIntersectionDistance { get; private set; }
        public TrailRenderer FlightTrail => flightTrail;

        public void Launch(
            GameObject instigator,
            Vector3 velocity,
            float shotDamage,
            AudioClip hitClip = null)
        {
            owner = instigator;
            damage = Mathf.Max(0f, shotDamage);
            impactClip = hitClip;
            launchedAt = Time.time;
            launchWorldScale = transform.lossyScale;
            lastFlightRotation = transform.rotation;
            ImpactDirection =
                velocity.sqrMagnitude > 0.0001f
                    ? velocity.normalized
                    : transform.forward;

            CreateFlightTrail();

            arrowCollider = gameObject.AddComponent<CapsuleCollider>();
            arrowCollider.direction = 2;
            arrowCollider.center = new Vector3(0f, 0f, 0.53f);
            arrowCollider.height = 0.16f;
            arrowCollider.radius = 0.025f;

            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.075f;
            body.useGravity = true;
            body.linearDamping = 0.01f;
            body.angularDamping = 0.05f;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = velocity;

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

        private void FixedUpdate()
        {
            if (stuck || body == null)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude > 0.04f)
            {
                transform.rotation = Quaternion.LookRotation(
                    velocity.normalized,
                    transform.up);
                lastFlightRotation = transform.rotation;
                ImpactDirection = velocity.normalized;
            }

            if (Time.time - launchedAt >= FlyingLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (!stuck || stuckTo == null)
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

        private void OnCollisionEnter(Collision collision)
        {
            if (stuck ||
                collision.collider == null ||
                (owner != null &&
                    collision.collider.transform.IsChildOf(owner.transform)))
            {
                return;
            }

            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;
            Vector3 hitPoint = collision.contactCount > 0
                ? contact.point
                : transform.position + transform.forward * 0.60f;
            HitPoint = hitPoint;
            ImpactDirection =
                lastFlightRotation * Vector3.forward;
            DamageService.TryApply(
                collision.collider,
                new DamageRequest(
                    owner,
                    damage,
                    hitPoint,
                    ImpactDirection,
                    "prototype-bow"));
            PlayImpactAudio();
            StickTo(
                collision.collider.transform,
                hitPoint,
                lastFlightRotation);
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
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
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
            Destroy(gameObject, StuckLifetime);
        }
    }
}
