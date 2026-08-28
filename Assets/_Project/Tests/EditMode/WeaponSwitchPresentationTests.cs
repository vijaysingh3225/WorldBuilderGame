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
        public void SheatheHandDampingConvergesAndMeetsBothEndpoints()
        {
            MethodInfo damping =
                typeof(TwoSlotWeaponPresenter).GetMethod(
                    "DampSheatheHandRotation",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            Assert.That(damping, Is.Not.Null);

            Quaternion target = Quaternion.Euler(-62f, 118f, 37f);
            Quaternion current = Quaternion.identity;
            float priorError = Quaternion.Angle(current, target);
            for (int frame = 0; frame < 30; frame++)
            {
                current = (Quaternion)damping.Invoke(
                    null,
                    new object[]
                    {
                        current,
                        target,
                        1f / 60f,
                        0f
                    });
                float error = Quaternion.Angle(current, target);
                Assert.That(
                    error,
                    Is.LessThanOrEqualTo(priorError + 0.0001f),
                    "Wrist filtering must converge without a reversal or rotational twitch.");
                if (priorError > 0.01f)
                {
                    Assert.That(error, Is.LessThan(priorError));
                }
                priorError = error;
            }

            Quaternion exactEndpoint = (Quaternion)damping.Invoke(
                null,
                new object[]
                {
                    current,
                    target,
                    1f / 60f,
                    1f
                });
            Assert.That(
                Quaternion.Angle(exactEndpoint, target),
                Is.LessThan(0.001f),
                "Both traversal directions must still meet their authored endpoint exactly.");
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
                    -(bowRotation * Vector3.right),
                    expectedGripAxis).normalized;

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

        [Test]
        public void BowHoldingHandDoesNotInvertAcrossRightAim()
        {
            MethodInfo calculate =
                typeof(TwoSlotWeaponPresenter).GetMethod(
                    "CalculateBowHoldingHandRotation",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);
            MethodInfo lockToBow =
                typeof(TwoSlotWeaponPresenter).GetMethod(
                    "CalculateBowLockedHandRotation",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            Assert.That(lockToBow, Is.Not.Null);

            Vector3 localFingerAxis =
                new Vector3(0.91f, 0.32f, -0.26f).normalized;
            Vector3 localPalmNormal =
                Vector3.ProjectOnPlane(
                    new Vector3(-0.08f, 0.72f, 0.69f),
                    localFingerAxis).normalized;
            Vector3 characterRight = Vector3.right;
            Vector3 reachDirection =
                new Vector3(-0.08f, 0.11f, 0.99f).normalized;

            Quaternion leftBowRotation =
                Quaternion.Euler(-17f, -68f, 11f);
            Quaternion rightBowRotation =
                Quaternion.Euler(-17f, 68f, 11f);
            Quaternion leftAimRotation =
                (Quaternion)calculate.Invoke(
                    null,
                    new object[]
                    {
                        localFingerAxis,
                        localPalmNormal,
                        leftBowRotation,
                        characterRight,
                        reachDirection
                    });
            Quaternion rightAimRotation =
                (Quaternion)lockToBow.Invoke(
                    null,
                    new object[]
                    {
                        rightBowRotation,
                        leftBowRotation,
                        leftAimRotation
                    });
            Vector3 expectedLeftPalmSide =
                Vector3.ProjectOnPlane(
                    -(leftBowRotation * Vector3.right),
                    reachDirection).normalized;
            Assert.That(
                Vector3.Angle(
                    leftAimRotation * localPalmNormal,
                    expectedLeftPalmSide),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Dot(
                    leftAimRotation * localPalmNormal,
                    leftBowRotation * Vector3.right),
                Is.LessThan(0f));
            Assert.That(
                Vector3.Dot(
                    rightAimRotation * localPalmNormal,
                    rightBowRotation * Vector3.right),
                Is.LessThan(0f));
            Assert.That(
                Vector3.Angle(
                    Quaternion.Inverse(leftBowRotation) *
                        (leftAimRotation * localPalmNormal),
                    Quaternion.Inverse(rightBowRotation) *
                        (rightAimRotation * localPalmNormal)),
                Is.LessThan(0.01f));
        }

        [Test]
        public void ArrowSlidesAcrossFixedRightHandShelfDuringDraw()
        {
            Vector3 undrawn =
                TwoSlotWeaponPresenter.
                    CalculateArrowNockLocalPosition(0f);
            Vector3 fullDraw =
                TwoSlotWeaponPresenter.
                    CalculateArrowNockLocalPosition(1f);

            Assert.That(undrawn.x, Is.GreaterThan(0f));
            Assert.That(undrawn.y, Is.GreaterThan(0f));
            Assert.That(
                fullDraw.x,
                Is.EqualTo(undrawn.x).Within(0.000001f));
            Assert.That(
                fullDraw.y,
                Is.EqualTo(undrawn.y).Within(0.000001f));
            Assert.That(
                fullDraw.z - undrawn.z,
                Is.EqualTo(-TwoSlotWeaponPresenter.
                    BowMaximumDrawDistance).Within(0.000001f));
        }

        [Test]
        public void BowContactSolveRunsAfterAimedStrafePose()
        {
            DefaultExecutionOrder bowOrder =
                typeof(TwoSlotWeaponPresenter).GetCustomAttribute<
                    DefaultExecutionOrder>();
            DefaultExecutionOrder stanceOrder =
                typeof(AimStanceLocomotionPresenter).GetCustomAttribute<
                    DefaultExecutionOrder>();

            Assert.That(bowOrder, Is.Not.Null);
            Assert.That(stanceOrder, Is.Not.Null);
            Assert.That(
                bowOrder.order,
                Is.GreaterThan(stanceOrder.order),
                "Both bow hand contacts must be solved after strafing rotates the stance hierarchy.");
        }

        [Test]
        public void AlertWeaponRunUsesStrongerForwardIntentThanWalk()
        {
            float walkLean =
                AimStanceLocomotionPresenter.CalculateAlertLean(
                    0f,
                    AimStanceLocomotionPresenter.AlertWalkLean,
                    AimStanceLocomotionPresenter.AlertRunLean);
            float runLean =
                AimStanceLocomotionPresenter.CalculateAlertLean(
                    1f,
                    AimStanceLocomotionPresenter.AlertWalkLean,
                    AimStanceLocomotionPresenter.AlertRunLean);

            Assert.That(
                walkLean,
                Is.EqualTo(8f).Within(0.001f));
            Assert.That(
                runLean,
                Is.EqualTo(16f).Within(0.001f));
            Assert.That(runLean, Is.GreaterThan(walkLean));
            Assert.That(
                AimStanceLocomotionPresenter.
                    AlertRunShoulderClose,
                Is.GreaterThan(
                    AimStanceLocomotionPresenter.
                        AlertWalkShoulderClose));
        }

        [Test]
        public void SwordGuardLowerBodyFollowsTravelWhileUpperBodyRemainsAimed()
        {
            Assert.That(
                AimStanceLocomotionPresenter.CalculateGuardTravelYaw(
                    Vector3.forward),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                AimStanceLocomotionPresenter.CalculateGuardTravelYaw(
                    Vector3.right),
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                AimStanceLocomotionPresenter.CalculateGuardTravelYaw(
                    Vector3.left),
                Is.EqualTo(-90f).Within(0.001f));
            Assert.That(
                Mathf.Abs(
                    AimStanceLocomotionPresenter.CalculateGuardTravelYaw(
                        Vector3.back)),
                Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void SwordRunIntensityLeavesWalkUntouchedAndReachesFullSprintPose()
        {
            Assert.That(
                AimStanceLocomotionPresenter.
                    CalculateSwordRunIntensity(0f),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                AimStanceLocomotionPresenter.
                    CalculateSwordRunIntensity(
                        AimStanceLocomotionPresenter.
                            SwordRunIntensityStart),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                AimStanceLocomotionPresenter.
                    CalculateSwordRunIntensity(1f),
                Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SwordRunBrandishDirectionPointsMostlyForwardAndSlightlyRight()
        {
            Vector3 direction =
                AimStanceLocomotionPresenter.
                    CalculateSwordRunBrandishDirection(
                        Vector3.right,
                        Vector3.up,
                        Vector3.forward);

            Assert.That(
                Vector3.Dot(direction, Vector3.forward),
                Is.GreaterThan(0.9f));
            Assert.That(direction.y, Is.GreaterThan(0f));
            Assert.That(direction.x, Is.GreaterThan(0.25f));
            Assert.That(
                Vector3.Dot(direction, Vector3.forward),
                Is.GreaterThan(direction.x));
        }

        [Test]
        public void SwordAttackHandoffsReserveVisibleBlendTime()
        {
            Assert.That(
                ShortSwordAttackPresenter.
                    AttackEntryBlendDuration,
                Is.GreaterThanOrEqualTo(0.08f));
            Assert.That(
                ShortSwordAttackPresenter.
                    MinimumAttackTransitionDuration,
                Is.GreaterThanOrEqualTo(0.07f));
            Assert.That(
                ShortSwordAttackPresenter.
                    MinimumAttackReturnDuration,
                Is.GreaterThanOrEqualTo(0.08f));
            Assert.That(
                HumanoidAnimatorPresenter.
                    MinimumLocomotionDampTime,
                Is.GreaterThanOrEqualTo(0.15f));
        }

        [Test]
        public void RunningSwordRecoveryHandsDirectlyToLocomotionPose()
        {
            Assert.That(
                ShortSwordAttackPresenter.
                    CalculateRunningReturnProgress(
                        ShortSwordAttackPresenter.
                            RunningRecoveryHandoffStart,
                        true),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.
                    CalculateRunningReturnProgress(
                        0.75f,
                        true),
                Is.InRange(0.45f, 0.55f));
            Assert.That(
                ShortSwordAttackPresenter.
                    CalculateRunningReturnProgress(
                        1f,
                        true),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                ShortSwordAttackPresenter.
                    CalculateRunningReturnProgress(
                        1f,
                        false),
                Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void BowStringHandOpensAfterReleaseAndClaspsForRenock()
        {
            const float ReloadDuration = 0.65f;

            Assert.That(
                TwoSlotWeaponPresenter.CalculateBowStringHandClaspWeight(
                    true,
                    0f,
                    ReloadDuration),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                TwoSlotWeaponPresenter.CalculateBowStringHandClaspWeight(
                    false,
                    ReloadDuration,
                    ReloadDuration),
                Is.EqualTo(1f).Within(0.001f),
                "The fingers should begin opening from their exact release pose.");
            Assert.That(
                TwoSlotWeaponPresenter.CalculateBowStringHandClaspWeight(
                    false,
                    ReloadDuration - 0.08f,
                    ReloadDuration),
                Is.EqualTo(0f).Within(0.001f),
                "The string hand should visibly open immediately after release.");
            Assert.That(
                TwoSlotWeaponPresenter.CalculateBowStringHandClaspWeight(
                    false,
                    0.07f,
                    ReloadDuration),
                Is.InRange(0.45f, 0.55f),
                "The fingers should be halfway back to their clasp midway through the renock blend.");
            Assert.That(
                TwoSlotWeaponPresenter.CalculateBowStringHandClaspWeight(
                    false,
                    0f,
                    ReloadDuration),
                Is.EqualTo(1f).Within(0.001f),
                "The hand must be clasped when the replacement arrow becomes ready.");
        }

        [Test]
        public void BowStringHandFingerBlendMovesBetweenOpenAndClaspedPoses()
        {
            GameObject fingerObject = new GameObject("Bow String Finger");
            try
            {
                Quaternion open = Quaternion.Euler(0f, 0f, 0f);
                Quaternion clasped = Quaternion.Euler(0f, 0f, 60f);

                TwoSlotWeaponPresenter.ApplyBlendedFingerPose(
                    new[] { fingerObject.transform },
                    new[] { open },
                    new[] { clasped },
                    0f,
                    1f);
                Assert.That(
                    Quaternion.Angle(
                        fingerObject.transform.localRotation,
                        open),
                    Is.LessThan(0.001f));

                TwoSlotWeaponPresenter.ApplyBlendedFingerPose(
                    new[] { fingerObject.transform },
                    new[] { open },
                    new[] { clasped },
                    1f,
                    1f);
                Assert.That(
                    Quaternion.Angle(
                        fingerObject.transform.localRotation,
                        clasped),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(fingerObject);
            }
        }

        [Test]
        public void FullBowFingerLockRestoresCapturedGrip()
        {
            GameObject fingerObject = new GameObject("Bow Finger");
            try
            {
                Quaternion capturedGrip =
                    Quaternion.Euler(34f, -18f, 51f);
                fingerObject.transform.localRotation =
                    Quaternion.Euler(-25f, 42f, -9f);

                TwoSlotWeaponPresenter.ApplyFingerPose(
                    new[] { fingerObject.transform },
                    new[] { capturedGrip },
                    1f);

                Assert.That(
                    Quaternion.Angle(
                        fingerObject.transform.localRotation,
                        capturedGrip),
                    Is.LessThan(0.001f),
                    "A fully aimed bow must overwrite locomotion-driven finger uncurling.");
            }
            finally
            {
                Object.DestroyImmediate(fingerObject);
            }
        }

        [Test]
        public void ClosedBowGripCurlsFingersTowardHandleAndRestoresSourcePose()
        {
            GameObject handObject = new GameObject("Bow Hand");
            GameObject proximalObject = new GameObject("Index Proximal");
            GameObject intermediateObject = new GameObject("Index Intermediate");
            GameObject distalObject = new GameObject("Index Distal");
            try
            {
                proximalObject.transform.SetParent(
                    handObject.transform,
                    false);
                intermediateObject.transform.SetParent(
                    proximalObject.transform,
                    false);
                distalObject.transform.SetParent(
                    intermediateObject.transform,
                    false);
                intermediateObject.transform.localPosition =
                    Vector3.forward * 0.10f;
                distalObject.transform.localPosition =
                    Vector3.forward * 0.10f;
                Transform[] fingers =
                {
                    null,
                    null,
                    null,
                    proximalObject.transform,
                    intermediateObject.transform,
                    distalObject.transform
                };
                Vector3 gripCenter =
                    new Vector3(0.10f, 0f, 0.10f);
                float openDistance = Vector3.Distance(
                    distalObject.transform.position,
                    gripCenter);

                Quaternion[] closedPose =
                    TwoSlotWeaponPresenter.
                        CaptureClosedFingerGripPose(
                            handObject.transform,
                            fingers,
                            gripCenter);

                Assert.That(
                    Quaternion.Angle(
                        proximalObject.transform.localRotation,
                        Quaternion.identity),
                    Is.LessThan(0.001f),
                    "Authoring the grip must not leave the live rig mutated.");

                TwoSlotWeaponPresenter.ApplyFingerPose(
                    fingers,
                    closedPose,
                    1f);
                float closedDistance = Vector3.Distance(
                    distalObject.transform.position,
                    gripCenter);
                Assert.That(
                    closedDistance,
                    Is.LessThan(openDistance),
                    "The authored fingers must curl toward the bow handle.");
            }
            finally
            {
                Object.DestroyImmediate(handObject);
            }
        }
    }
}
