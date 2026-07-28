using UnityEngine;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class BowArrowProjectile : MonoBehaviour
    {
        private const float FlyingLifetime = 20f;
        private const float StuckLifetime = 45f;

        private GameObject owner;
        private Rigidbody body;
        private CapsuleCollider arrowCollider;
        private float damage;
        private AudioClip impactClip;
        private float launchedAt;
        private bool stuck;

        public bool IsStuck => stuck;
        public Vector3 HitPoint { get; private set; }

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
            }

            if (Time.time - launchedAt >= FlyingLifetime)
            {
                Destroy(gameObject);
            }
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
            DamageService.TryApply(
                collision.collider,
                new DamageRequest(
                    owner,
                    damage,
                    hitPoint,
                    transform.forward,
                    "prototype-bow"));
            PlayImpactAudio();
            StickTo(collision.collider.transform);
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

        private void StickTo(Transform hitTransform)
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

            transform.SetParent(hitTransform, true);
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
