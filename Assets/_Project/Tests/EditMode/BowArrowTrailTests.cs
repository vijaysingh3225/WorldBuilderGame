using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class BowArrowTrailTests
    {
        [Test]
        public void LaunchedArrowGetsShortWhiteRearTrail()
        {
            GameObject arrowObject =
                new GameObject("trail-test-arrow");
            try
            {
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<
                        BowArrowProjectile>();
                arrow.Launch(
                    null,
                    Vector3.forward * 28f,
                    10f);

                TrailRenderer trail =
                    arrow.FlightTrail;
                Assert.That(trail, Is.Not.Null);
                Assert.That(trail.transform.parent, Is.SameAs(
                    arrow.transform));
                Assert.That(
                    trail.transform.localPosition.z,
                    Is.LessThan(0f));
                Assert.That(trail.emitting, Is.True);
                Assert.That(
                    trail.time,
                    Is.InRange(0.1f, 0.2f));
                Assert.That(
                    trail.widthCurve.Evaluate(0f),
                    Is.LessThanOrEqualTo(0.025f));
                Assert.That(
                    trail.widthCurve.Evaluate(1f),
                    Is.LessThan(
                        trail.widthCurve.Evaluate(0f)));
                Assert.That(
                    trail.colorGradient.alphaKeys[0].alpha,
                    Is.GreaterThan(0.7f));
                Assert.That(
                    trail.colorGradient.alphaKeys[
                        trail.colorGradient.alphaKeys.Length - 1]
                        .alpha,
                    Is.Zero);
                Assert.That(
                    trail.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(
                    trail.sharedMaterial,
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
            }
        }

        [Test]
        public void ArrowDoesNotReverseWhenShaftStartsBesideScenery()
        {
            GameObject arrowObject =
                new GameObject("clear-tip-arrow");
            GameObject nearbyScenery =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                nearbyScenery.name = "Nearby Boulder Edge";
                nearbyScenery.transform.position = Vector3.zero;
                nearbyScenery.transform.localScale =
                    new Vector3(1f, 1f, 1.18f);
                Physics.SyncTransforms();

                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                arrow.Launch(
                    null,
                    Vector3.forward * 28f,
                    10f);

                InvokeFixedUpdate(arrow);

                Assert.That(arrow.IsStuck, Is.False);
                Assert.That(
                    arrow.transform.position.z,
                    Is.GreaterThan(0.5f));
                Assert.That(
                    arrow.ImpactDirection.z,
                    Is.GreaterThan(0.99f),
                    "A clear arrow-tip path must never be reflected by an overlap behind the tip.");
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
                Object.DestroyImmediate(nearbyScenery);
            }
        }

        [Test]
        public void ArrowStopsAtFirstSurfaceAlongTipPath()
        {
            GameObject arrowObject =
                new GameObject("first-hit-arrow");
            GameObject surface =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                surface.name = "First Real Surface";
                surface.transform.position =
                    Vector3.forward * 1.1f;
                surface.transform.localScale =
                    Vector3.one * 0.2f;
                Physics.SyncTransforms();

                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                arrow.Launch(
                    null,
                    Vector3.forward * 28f,
                    10f);

                InvokeFixedUpdate(arrow);

                Assert.That(arrow.IsStuck, Is.True);
                Assert.That(
                    arrow.HitPoint.z,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    Vector3.Distance(
                        arrow.transform.TransformPoint(
                            Vector3.forward * 0.605f),
                        arrow.HitPoint),
                    Is.LessThan(0.00001f),
                    "The visible arrow tip must embed at the exact raycast contact without an impact snap.");
                Assert.That(
                    arrow.ImpactDirection.z,
                    Is.GreaterThan(0.99f));
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void GrazingColliderCannotPullArrowOffCenterline()
        {
            GameObject arrowObject =
                new GameObject("centerline-impact-arrow");
            GameObject grazingSurface =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                grazingSurface.name = "Grazing Surface";
                grazingSurface.transform.position =
                    new Vector3(0.115f, 0f, 1.1f);
                grazingSurface.transform.localScale =
                    Vector3.one * 0.2f;
                Physics.SyncTransforms();

                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                arrow.Launch(
                    null,
                    Vector3.forward * 28f,
                    10f);

                InvokeFixedUpdate(arrow);

                Assert.That(arrow.IsStuck, Is.False);
                Assert.That(
                    Mathf.Abs(arrow.FlightTipPosition.x),
                    Is.LessThan(0.001f),
                    "A surface beside the exact tip path must not pull the arrow sideways.");
                Assert.That(
                    Mathf.Abs(arrow.transform.position.x),
                    Is.LessThan(0.001f));
                Assert.That(
                    Mathf.Abs(arrow.transform.forward.x),
                    Is.LessThan(0.00001f),
                    "A nearby surface must not create horizontal steering; the small vertical change is gravity.");
                Assert.That(
                    arrow.transform.forward.z,
                    Is.GreaterThan(0.99f));
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
                Object.DestroyImmediate(grazingSurface);
            }
        }

        [Test]
        public void ArrowTipFollowsOneContinuousBallisticPathWhileShaftRotates()
        {
            GameObject arrowObject =
                new GameObject("continuous-tip-arrow");
            try
            {
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                Vector3 initialVelocity =
                    new Vector3(4f, 3f, 28f);
                arrow.Launch(
                    null,
                    initialVelocity,
                    10f);
                Vector3 initialTip = arrow.FlightTipPosition;
                const int StepCount = 4;

                for (int index = 0; index < StepCount; index++)
                {
                    InvokeFixedUpdate(arrow);
                }

                float elapsed =
                    Time.fixedDeltaTime * StepCount;
                Vector3 expectedTip =
                    initialTip +
                    initialVelocity * elapsed +
                    Physics.gravity *
                    (0.5f * elapsed * elapsed);
                Vector3 renderedTip =
                    arrow.transform.TransformPoint(
                        Vector3.forward * 0.605f);

                Assert.That(
                    Vector3.Distance(
                        arrow.FlightTipPosition,
                        expectedTip),
                    Is.LessThan(0.00001f),
                    "Gravity must be the only change to the immutable launch velocity.");
                Assert.That(
                    Vector3.Distance(
                        renderedTip,
                        expectedTip),
                    Is.LessThan(0.00001f),
                    "Rotating the shaft must not move the collision tip away from the ballistic path.");
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
            }
        }

        [Test]
        public void EnemyUsesTheSameFirstSurfaceRuleAsWorldGeometry()
        {
            GameObject enemy = new GameObject("uniform-path-enemy");
            GameObject firstHitbox = new GameObject("first-enemy-hitbox");
            GameObject laterHitbox = new GameObject("later-enemy-hitbox");
            GameObject arrowObject = new GameObject("uniform-path-arrow");
            try
            {
                enemy.AddComponent<Health>();
                EnemyDamageProfile profile =
                    enemy.AddComponent<EnemyDamageProfile>();
                profile.Configure(EnemyCombatVariant.RaidEnemy);
                firstHitbox.transform.SetParent(enemy.transform, false);
                laterHitbox.transform.SetParent(enemy.transform, false);
                firstHitbox.transform.position =
                    new Vector3(0f, 0f, 1.1f);
                laterHitbox.transform.position =
                    new Vector3(0f, 0f, 2.1f);
                firstHitbox.transform.localScale = Vector3.one * 0.2f;
                laterHitbox.transform.localScale = Vector3.one * 0.2f;
                firstHitbox.AddComponent<BoxCollider>();
                laterHitbox.AddComponent<BoxCollider>();
                Physics.SyncTransforms();

                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                arrow.Launch(
                    null,
                    Vector3.forward * 28f,
                    10f);

                for (int index = 0;
                     index < 5 && !arrow.IsStuck;
                     index++)
                {
                    InvokeFixedUpdate(arrow);
                }

                Assert.That(arrow.IsStuck, Is.True);
                Assert.That(
                    arrow.HitPoint.z,
                    Is.EqualTo(1f).Within(0.001f),
                    "An enemy must accept the first physical centerline contact exactly like a wall.");
                Assert.That(
                    Mathf.Abs(arrow.HitPoint.x),
                    Is.LessThan(0.00001f));
                Assert.That(
                    Vector3.Distance(
                        arrow.transform.TransformPoint(
                            Vector3.forward * 0.605f),
                        arrow.HitPoint),
                    Is.LessThan(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void VerticalShotKeepsArrowForwardPointedUp()
        {
            Quaternion verticalRotation =
                BowArrowProjectile.CalculateFlightRotation(
                    Vector3.up,
                    Vector3.up);
            Quaternion nearVerticalRotation =
                BowArrowProjectile.CalculateFlightRotation(
                    new Vector3(0.0001f, 1f, 0f),
                    Vector3.up);

            Assert.That(
                Vector3.Angle(
                    verticalRotation * Vector3.forward,
                    Vector3.up),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Dot(
                    nearVerticalRotation * Vector3.forward,
                    new Vector3(0.0001f, 1f, 0f).normalized),
                Is.GreaterThan(0.99999f),
                "Near-vertical aiming must never flip the arrow backward.");
        }

        [Test]
        public void VerticalProjectileTravelsUpWithoutSnapBack()
        {
            GameObject arrowObject =
                new GameObject("vertical-flight-arrow");
            try
            {
                arrowObject.transform.rotation =
                    BowArrowProjectile.CalculateFlightRotation(
                        Vector3.up,
                        Vector3.up);
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                arrow.Launch(
                    null,
                    Vector3.up * 28f,
                    10f);

                InvokeFixedUpdate(arrow);

                Assert.That(arrow.IsStuck, Is.False);
                Assert.That(
                    arrow.transform.position.y,
                    Is.GreaterThan(0.5f));
                Assert.That(
                    Vector3.Angle(
                        arrow.transform.forward,
                        Vector3.up),
                    Is.LessThan(0.01f));
                Assert.That(
                    arrow.ImpactDirection.y,
                    Is.GreaterThan(0.99f));
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
            }
        }

        private static void InvokeFixedUpdate(
            BowArrowProjectile arrow)
        {
            MethodInfo advanceFlight =
                typeof(BowArrowProjectile).GetMethod(
                    "AdvanceFlight",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(advanceFlight, Is.Not.Null);
            advanceFlight.Invoke(
                arrow,
                new object[] { Time.fixedDeltaTime });
        }
    }
}
