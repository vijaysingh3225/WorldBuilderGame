using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class WeaponSwitchPresentationTests
    {
        [Test]
        public void SheatheBladeDampingConvergesWithoutOvershoot()
        {
            MethodInfo damping =
                typeof(TwoSlotWeaponPresenter).GetMethod(
                    "DampSheatheBladeRotation",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            Assert.That(damping, Is.Not.Null);

            Quaternion target =
                Quaternion.Euler(75f, -110f, 42f);
            Quaternion current = Quaternion.identity;
            float priorError =
                Quaternion.Angle(current, target);
            for (int frame = 0;
                 frame < 24;
                 frame++)
            {
                current =
                    (Quaternion)damping.Invoke(
                        null,
                        new object[]
                        {
                            current,
                            target,
                            1f / 60f,
                            0f
                        });
                float error =
                    Quaternion.Angle(
                        current,
                        target);
                Assert.That(
                    error,
                    Is.LessThanOrEqualTo(
                        priorError + 0.0001f));
                if (priorError > 0.01f)
                {
                    Assert.That(
                        error,
                        Is.LessThan(priorError));
                }
                priorError = error;
            }

            Quaternion exactEndpoint =
                (Quaternion)damping.Invoke(
                    null,
                    new object[]
                    {
                        current,
                        target,
                        1f / 60f,
                        1f
                    });
            Assert.That(
                Quaternion.Angle(
                    exactEndpoint,
                    target),
                Is.LessThan(0.001f));
        }

        [Test]
        public void BowHoldingHandFrameTracksHandleAndStableBowSide()
        {
            MethodInfo calculate =
                typeof(TwoSlotWeaponPresenter).GetMethod(
                    "CalculateBowHoldingHandRotation",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);

            Vector3 localFingerAxis =
                new Vector3(0.91f, 0.32f, -0.26f).normalized;
            Vector3 localPalmNormal =
                Vector3.ProjectOnPlane(
                    new Vector3(-0.08f, 0.72f, 0.69f),
                    localFingerAxis).normalized;
            Quaternion bowRotation =
                Quaternion.Euler(-17f, 42f, 11f);
            Vector3 characterRight = Vector3.right;
            Vector3 reachDirection =
                new Vector3(-0.08f, 0.11f, 0.99f).normalized;
            Quaternion handRotation =
                (Quaternion)calculate.Invoke(
                    null,
                    new object[]
                    {
                        localFingerAxis,
                        localPalmNormal,
                        bowRotation,
                        characterRight,
                        reachDirection
                    });

            Vector3 expectedGripAxis =
                reachDirection;
            Vector3 expectedPalmSide =
                Vector3.ProjectOnPlane(
                    bowRotation * Vector3.right,
                    expectedGripAxis).normalized;
            if (Vector3.Dot(
                    expectedPalmSide,
                    characterRight) > 0f)
            {
                expectedPalmSide = -expectedPalmSide;
            }

            Assert.That(
                Vector3.Angle(
                    handRotation * localFingerAxis,
                    expectedGripAxis),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Angle(
                    handRotation * localPalmNormal,
                    expectedPalmSide),
                Is.LessThan(0.01f));
        }
    }
}
