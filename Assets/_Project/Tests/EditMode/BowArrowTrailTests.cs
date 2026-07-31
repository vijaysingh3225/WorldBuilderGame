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
    }
}
