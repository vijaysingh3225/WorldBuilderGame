using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests
{
    public sealed class EnemyPerceptionTests
    {
        private GameObject player;
        private GameObject enemy;
        private GameObject obstruction;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(obstruction);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void DormantTrainingDummyRequiresExplicitActivation()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -20f));
            enemy = CreateEnemy(Vector3.zero);
            Health enemyHealth = enemy.GetComponent<Health>();
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();

            enemyHealth.ReceiveDamage(
                new DamageRequest(
                    player,
                    10f,
                    enemy.transform.position,
                    Vector3.forward,
                    "prototype-bow"));

            Assert.That(brain.IsActivated, Is.False);
            Assert.That(brain.IsAlerted, Is.False);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(
                enemyHealth.Current,
                Is.EqualTo(100f),
                "The manually activated Combat Lab dummy remains invulnerable while dormant.");

            SetPrivateField(
                brain,
                "target",
                player.transform);
            brain.ActivateForDiagnostics();

            Assert.That(brain.IsActivated, Is.True);
            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(
                brain.LastKnownPosition,
                Is.EqualTo(player.transform.position));
        }

        [Test]
        public void DormantArenaEnemyAlertsWhenDamaged()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -20f));
            enemy = CreateEnemy(Vector3.zero);
            Health enemyHealth = enemy.GetComponent<Health>();
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.ConfigureAsTrainingDummy(
                requireManualActivation: false);

            enemyHealth.ReceiveDamage(
                new DamageRequest(
                    player,
                    10f,
                    enemy.transform.position,
                    Vector3.forward,
                    "prototype-bow"));

            Assert.That(brain.IsActivated, Is.True);
            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(enemyHealth.Current, Is.EqualTo(90f));
            Assert.That(
                brain.LastKnownPosition.z,
                Is.EqualTo(-10f).Within(0.01f));
        }

        [Test]
        public void SolidCoverBlocksCurrentVisualContact()
        {
            player = CreateTarget(
                "Player",
                new Vector3(500f, 0f, 506f));
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            Physics.SyncTransforms();

            bool initialSight = EvaluateSight(brain);
            Assert.That(
                initialSight,
                Is.True,
                DescribeSightRay(brain));

            obstruction = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            obstruction.name = "Sight Obstruction";
            obstruction.transform.position =
                new Vector3(500f, 0.75f, 503f);
            obstruction.transform.localScale =
                new Vector3(2f, 2f, 0.6f);
            Physics.SyncTransforms();

            Assert.That(EvaluateSight(brain), Is.False);
        }

        [Test]
        public void DistantPassiveSightRequiresSustainedExposure()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            Physics.SyncTransforms();

            UpdatePerception(brain);

            Assert.That(brain.IsAlerted, Is.False);
            Assert.That(
                brain.HasVisualContact,
                Is.False,
                "One clear passive sight sample at range must not become instant combat awareness.");
            Assert.That(
                GetPrivateField<float>(brain, "passiveAwareness"),
                Is.GreaterThan(0f));
        }

        [Test]
        public void ForestAndCrouchIncreaseRecognitionTime()
        {
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            MethodInfo calculateDuration =
                ResolvePrivateMethod(
                    "CalculatePassiveRecognitionDuration");

            float trailDuration = (float)
                calculateDuration.Invoke(
                    brain,
                    new object[] { 14f, false, false });
            float forestDuration = (float)
                calculateDuration.Invoke(
                    brain,
                    new object[] { 14f, true, false });
            float crouchedForestDuration = (float)
                calculateDuration.Invoke(
                    brain,
                    new object[] { 14f, true, true });

            Assert.That(
                forestDuration,
                Is.GreaterThan(trailDuration * 2f));
            Assert.That(
                crouchedForestDuration,
                Is.GreaterThan(forestDuration));
        }

        [Test]
        public void WalkingBehindGuardStaysQuietButNearbyRunningAlerts()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -10f));
            ThirdPersonMotor playerMotor =
                player.AddComponent<ThirdPersonMotor>();
            playerMotor.ConfigureWalkSpeed(
                ThirdPersonMotor.DefaultPlayerWalkSpeed);
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);

            SetPrivateField(
                playerMotor,
                "horizontalVelocity",
                Vector3.forward * playerMotor.WalkSpeed);
            SetPrivateField(
                playerMotor,
                "targetHorizontalSpeed",
                playerMotor.WalkSpeed);
            Physics.SyncTransforms();
            UpdatePerception(brain);

            Assert.That(
                brain.IsAlerted,
                Is.False,
                "A guard must not detect ordinary walking through its back-facing vision cone.");

            SetPrivateField(
                playerMotor,
                "horizontalVelocity",
                Vector3.forward * playerMotor.SprintSpeed);
            SetPrivateField(
                playerMotor,
                "targetHorizontalSpeed",
                playerMotor.SprintSpeed);
            UpdatePerception(brain);

            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(
                GetPrivateField<float>(
                    brain,
                    "alertReactionTimer"),
                Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(
                brain.LastKnownPosition,
                Is.EqualTo(player.transform.position));
        }

        [Test]
        public void AnyExposedUpperBodyGlimpseRestoresVisualContact()
        {
            player = CreateTarget(
                "Player",
                new Vector3(500f, 0f, 506f));
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            obstruction = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            obstruction.transform.position =
                new Vector3(500f, 0.50f, 503f);
            obstruction.transform.localScale =
                new Vector3(2f, 1.00f, 0.6f);
            Physics.SyncTransforms();

            Assert.That(
                EvaluateSight(brain),
                Is.True,
                "An exposed head or shoulder should be enough for an engaged archer to shoot.");
        }

        [Test]
        public void LosingSightPreservesTheLastVisiblePosition()
        {
            player = CreateTarget(
                "Player",
                new Vector3(500f, 0f, 506f));
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            Physics.SyncTransforms();

            UpdatePerception(brain);
            Assert.That(brain.HasVisualContact, Is.True);
            Vector3 lastVisible = brain.LastKnownPosition;

            obstruction = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            obstruction.transform.position =
                new Vector3(500f, 0.75f, 503f);
            obstruction.transform.localScale =
                new Vector3(2f, 2f, 0.6f);
            player.transform.position =
                new Vector3(501f, 0f, 506f);
            Physics.SyncTransforms();

            UpdatePerception(brain);

            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(brain.HasVisualContact, Is.False);
            Assert.That(brain.LastKnownPosition, Is.EqualTo(lastVisible));
        }

        [Test]
        public void InvestigationCannotExpireBeforeEnemyReachesLastSeenPoint()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            SetPrivateField(brain, "alerted", true);
            SetPrivateField(
                brain,
                "lastKnownPosition",
                player.transform.position);
            SetPrivateField(brain, "lostSightWaitTimer", 0f);
            SetPrivateField(brain, "investigationTimer", 0.01f);
            SetPrivateField(
                brain,
                "reachedLastKnownPosition",
                false);

            InvokePrivate(brain, "UpdateInvestigation");

            Assert.That(brain.IsAlerted, Is.True);
            Assert.That(
                GetPrivateField<float>(brain, "investigationTimer"),
                Is.EqualTo(0.01f).Within(0.0001f),
                "Travel time must not consume the confirmed-empty search window.");

            enemy.transform.position = player.transform.position;
            InvokePrivate(brain, "UpdateInvestigation");

            Assert.That(
                GetPrivateField<bool>(
                    brain,
                    "reachedLastKnownPosition"),
                Is.True);
            Assert.That(
                GetPrivateField<float>(brain, "investigationTimer"),
                Is.GreaterThan(3f),
                "The search countdown should begin only after reaching the exact last-seen point.");
        }

        [Test]
        public void NavigationSteersAroundSolidSceneryInsteadOfStalling()
        {
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            obstruction = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            obstruction.transform.position =
                new Vector3(500f, 0.72f, 501f);
            obstruction.transform.localScale =
                new Vector3(0.8f, 1.4f, 0.35f);
            Physics.SyncTransforms();

            Vector3 movement = (Vector3)
                ResolvePrivateMethod(
                        "ResolveObstacleAwareDirection")
                    .Invoke(
                        enemy.GetComponent<EnemyBrain>(),
                        new object[] { Vector3.forward });

            Assert.That(movement.z, Is.GreaterThan(0.15f));
            Assert.That(
                Mathf.Abs(movement.x),
                Is.GreaterThan(0.45f),
                "A blocked guard should choose a clear side path rather than walking indefinitely into scenery.");
        }

        [Test]
        public void BridgeGeometryDoesNotPushNavigationOffTheDeck()
        {
            enemy = CreateEnemy(
                new Vector3(500f, 0f, 500f));
            obstruction = new GameObject("Road Bridge Test");
            GameObject bridgeRail = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            bridgeRail.transform.SetParent(
                obstruction.transform,
                false);
            bridgeRail.transform.position =
                new Vector3(500f, 0.72f, 501f);
            bridgeRail.transform.localScale =
                new Vector3(0.8f, 1.4f, 0.35f);
            Physics.SyncTransforms();

            Vector3 movement = (Vector3)
                ResolvePrivateMethod(
                        "ResolveObstacleAwareDirection")
                    .Invoke(
                        enemy.GetComponent<EnemyBrain>(),
                        new object[] { Vector3.forward });

            Assert.That(
                Vector3.Angle(movement, Vector3.forward),
                Is.LessThan(0.01f),
                "Decorative bridge collision must not make an AI dodge sideways into the river.");
        }

        [Test]
        public void CloseArrowPassImmediatelyAlertsTowardItsSourceDirection()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.ConfigureAsTrainingDummy(
                requireManualActivation: false);
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "bowWeapon", bow);
            SetPrivateField(brain, "drawingBow", true);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(bow, "heldDuration", 0.55f);
            var arrowObject = new GameObject("Passing Arrow");

            try
            {
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                var signal = new BowArrowProjectile.FlightSignal(
                    arrow,
                    player,
                    new Vector3(0.4f, 0.75f, -2f),
                    new Vector3(0.4f, 0.75f, 2f),
                    Vector3.forward);

                ResolvePrivateMethod("HandleArrowInFlight").Invoke(
                    brain,
                    new object[] { signal });

                Assert.That(brain.IsActivated, Is.True);
                Assert.That(brain.IsAlerted, Is.True);
                Assert.That(
                    brain.LastKnownPosition.z,
                    Is.EqualTo(-10f).Within(0.01f));
                Assert.That(
                    GetPrivateField<float>(brain, "alertReactionTimer"),
                    Is.EqualTo(1.5f).Within(0.01f));
                bow.CommitPendingReleaseAtRenderedCamera();
                Assert.That(bow.IsDrawing, Is.False);
                Assert.That(bow.FiredArrowCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
            }
        }

        [Test]
        public void NearbyArrowImpactAlertsAtNoiseBeforeTracingItsDirection()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, -20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.ConfigureAsTrainingDummy(
                requireManualActivation: false);
            var arrowObject = new GameObject("Impact Arrow");

            try
            {
                BowArrowProjectile arrow =
                    arrowObject.AddComponent<BowArrowProjectile>();
                Vector3 impactPoint = new Vector3(2f, 0f, 3f);
                var signal = new BowArrowProjectile.ImpactSignal(
                    arrow,
                    player,
                    impactPoint,
                    Vector3.forward);

                ResolvePrivateMethod("HandleArrowImpacted").Invoke(
                    brain,
                    new object[] { signal });

                Assert.That(brain.IsActivated, Is.True);
                Assert.That(brain.IsAlerted, Is.True);
                Assert.That(
                    GetPrivateField<Vector3>(brain, "alertFocusPoint"),
                    Is.EqualTo(impactPoint));
                Assert.That(
                    GetPrivateField<float>(brain, "impactLookTimer"),
                    Is.EqualTo(0.3f).Within(0.01f));
                Assert.That(
                    GetPrivateField<float>(brain, "alertReactionTimer"),
                    Is.EqualTo(0.9f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(arrowObject);
            }
        }

        [Test]
        public void RangedCombatChoosesARestrainedLateralStrafe()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);

            Vector2 intent = (Vector2)
                ResolvePrivateMethod("ResolveRangedStrafeIntent")
                    .Invoke(brain, null);

            Assert.That(intent.magnitude, Is.InRange(0.35f, 0.5f));
            Assert.That(
                Mathf.Abs(intent.x),
                Is.GreaterThan(Mathf.Abs(intent.y) * 3f),
                "Ranged movement should primarily strafe across the target rather than rush straight toward it.");
        }

        [Test]
        public void OccludedDrawingArcherAdvancesTowardLastSeenCoverEdge()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            SetPrivateField(
                brain,
                "lastKnownPosition",
                player.transform.position);

            Vector2 intent = (Vector2)
                ResolvePrivateMethod("ResolveOccludedBowMovement")
                    .Invoke(brain, null);

            Assert.That(intent.magnitude, Is.GreaterThan(0.7f));
            Assert.That(
                intent.y,
                Is.GreaterThan(Mathf.Abs(intent.x) * 3f),
                "An occluded archer should close on the last seen position instead of endlessly strafing in place.");
        }

        [Test]
        public void RaidArcherCannotCommitAPartialDraw()
        {
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain =
                enemy.GetComponent<EnemyBrain>();
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "bowWeapon", bow);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(bow, "arrowReady", true);

            SetPrivateField(
                bow,
                "heldDuration",
                bow.FullDrawDuration - 0.02f);
            Assert.That(
                ResolvePrivateMethod(
                        "IsCommittedBowShotReady")
                    .Invoke(brain, null),
                Is.False,
                "An AI release must wait for the actual bow charge instead of trusting only its windup timer.");

            SetPrivateField(
                bow,
                "heldDuration",
                bow.FullDrawDuration);
            Assert.That(
                ResolvePrivateMethod(
                        "IsCommittedBowShotReady")
                    .Invoke(brain, null),
                Is.True);
        }

        [Test]
        public void SwordOnlyGuardChargesFullComboThenBacksOffBlocking()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 8f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            PlayerInputSource input =
                enemy.AddComponent<PlayerInputSource>();
            SetPrivateField(brain, "input", input);
            brain.Configure(player.transform);
            brain.ConfigureCampGuardLoadout(
                EnemyBrain.WeaponLoadout.SwordOnly);

            ResolvePrivateMethod("EnterSwordEngagement")
                .Invoke(brain, null);
            Assert.That(
                GetPrivateField<object>(brain, "meleePhase").ToString(),
                Is.EqualTo("Closing"),
                "A newly alerted sword guard should charge instead of opening with a guard orbit.");

            ResolvePrivateMethod("UpdateMeleeClosing").Invoke(
                brain,
                new object[] { Vector3.forward, 5f });
            Assert.That(input.CurrentIntent.SprintHeld, Is.True);
            Assert.That(input.CurrentIntent.BlockHeld, Is.False);
            Assert.That(input.CurrentIntent.Move.y, Is.GreaterThan(0.9f));

            ResolvePrivateMethod("UpdateMeleeClosing").Invoke(
                brain,
                new object[] { Vector3.forward, 0.8f });
            Assert.That(
                GetPrivateField<int>(brain, "comboPulsesRemaining"),
                Is.EqualTo(3));

            for (int strike = 0; strike < 3; strike++)
            {
                SetPrivateField(brain, "nextAttackPulse", 0f);
                ResolvePrivateMethod("UpdateMeleeAttack").Invoke(
                    brain,
                    new object[] { Vector3.forward, 0.9f });
                Assert.That(
                    input.CurrentIntent.AttackPressed,
                    Is.True,
                    $"Sword combo strike {strike + 1} should be authorized.");
                Assert.That(input.CurrentIntent.SprintHeld, Is.True);
                Assert.That(input.CurrentIntent.Move.y, Is.GreaterThan(0.9f));
            }

            Assert.That(
                GetPrivateField<object>(brain, "meleePhase").ToString(),
                Is.EqualTo("Disengaging"));
            ResolvePrivateMethod("UpdateMeleeDisengage").Invoke(
                brain,
                new object[] { Vector3.forward, 0.7f });
            Assert.That(input.CurrentIntent.BlockHeld, Is.True);
            Assert.That(input.CurrentIntent.SprintHeld, Is.False);
            Assert.That(
                input.CurrentIntent.Move.y,
                Is.LessThan(-0.5f),
                "The sword guard should make space behind its block after the combo.");
        }

        [Test]
        public void OccludedRaidArcherKeepsCommittedShotReadyForFirstGlimpse()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "bowWeapon", bow);
            SetPrivateField(brain, "drawingBow", true);
            SetPrivateField(
                brain,
                "heldAimPoint",
                player.transform.position + Vector3.up * 1.2f);
            SetPrivateField(brain, "actionTimer", 0f);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(bow, "arrowReady", true);
            SetPrivateField(
                bow,
                "heldDuration",
                bow.FullDrawDuration);

            ResolvePrivateMethod("UpdateBowDraw").Invoke(
                brain,
                new object[] { false });

            Assert.That(
                GetPrivateField<bool>(brain, "drawingBow"),
                Is.True,
                "Cover must pause release without cancelling an engaged archer's shot.");
            Assert.That(bow.IsDrawing, Is.True);
            Assert.That(
                brain.CurrentState,
                Is.EqualTo(EnemyBrain.EnemyState.Windup));

            ResolvePrivateMethod("UpdateBowDraw").Invoke(
                brain,
                new object[] { true });

            Assert.That(
                GetPrivateField<bool>(brain, "drawingBow"),
                Is.False,
                "A fully drawn archer should commit immediately when sight returns.");
            Assert.That(
                brain.CurrentState,
                Is.EqualTo(EnemyBrain.EnemyState.Recovering));
            bow.CommitPendingReleaseAtRenderedCamera();
            Assert.That(
                bow.FiredArrowCount,
                Is.EqualTo(1),
                "The first valid glimpse must produce a projectile, not only a state transition.");
            Object.DestroyImmediate(
                bow.LastFiredProjectile.gameObject);
        }

        [Test]
        public void DamageDoesNotCancelAnEngagedArchersCommittedDraw()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 20f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "bowWeapon", bow);
            SetPrivateField(brain, "alerted", true);
            SetPrivateField(brain, "drawingBow", true);
            SetPrivateField(brain, "actionTimer", 0.3f);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(bow, "heldDuration", 0.72f);

            enemy.GetComponent<Health>().ReceiveDamage(
                new DamageRequest(
                    player,
                    2f,
                    enemy.transform.position,
                    Vector3.back,
                    "prototype-bow"));

            Assert.That(
                GetPrivateField<bool>(brain, "drawingBow"),
                Is.True,
                "Taking a survivable hit must not disarm an engaged archer.");
            Assert.That(bow.IsDrawing, Is.True);
            Assert.That(
                GetPrivateField<float>(brain, "actionTimer"),
                Is.EqualTo(0.3f).Within(0.001f));
        }

        [Test]
        public void RaidArcherWeaponForcesEveryReleaseToFullPower()
        {
            enemy = CreateEnemy(Vector3.zero);
            BowWeapon bow = CreateTestBow(enemy);

            Assert.That(
                enemy.GetComponent<PlayerInputSource>(),
                Is.Not.Null,
                "Raid AI uses PlayerInputSource as an intent adapter.");
            Assert.That(
                bow.IsPlayerOwned,
                Is.False,
                "Input availability must not make an AI bow use player crosshair aiming.");

            MethodInfo fireArrow = typeof(BowWeapon).GetMethod(
                "FireArrow",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(fireArrow, Is.Not.Null);
            fireArrow.Invoke(bow, new object[] { 0.05f });

            Assert.That(bow.LastShotCharge, Is.EqualTo(1f));
            Assert.That(
                bow.LastShotSpeed,
                Is.EqualTo(bow.MaximumArrowSpeed));
            Assert.That(bow.LastFiredProjectile, Is.Not.Null);
            Object.DestroyImmediate(
                bow.LastFiredProjectile.gameObject);
        }

        [Test]
        public void RaidArcherPartialReleaseRequestIsCancelled()
        {
            enemy = CreateEnemy(Vector3.zero);
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(bow, "heldDuration", 0.55f);
            Assert.That(bow.DrawNormalized, Is.InRange(0.1f, 0.9f));

            MethodInfo queueRelease = typeof(BowWeapon).GetMethod(
                "QueueRelease",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(queueRelease, Is.Not.Null);
            queueRelease.Invoke(bow, null);
            bow.CommitPendingReleaseAtRenderedCamera();

            Assert.That(
                bow.FiredArrowCount,
                Is.Zero,
                "An interrupted or early NPC draw must cancel instead of emitting a projectile.");
            Assert.That(bow.ArrowReady, Is.True);
            Assert.That(bow.LastFiredProjectile, Is.Null);
        }

        [Test]
        public void AlertDuringDrawCancelsWithoutReleasingArrow()
        {
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "bowWeapon", bow);
            SetPrivateField(brain, "drawingBow", true);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(bow, "heldDuration", 0.55f);

            ResolvePrivateMethod("AlertAt").Invoke(
                brain,
                new object[]
                {
                    new Vector3(0f, 0f, -10f),
                    "arrow-near-miss"
                });
            bow.CommitPendingReleaseAtRenderedCamera();

            Assert.That(
                GetPrivateField<bool>(brain, "drawingBow"),
                Is.False);
            Assert.That(bow.IsDrawing, Is.False);
            Assert.That(bow.FiredArrowCount, Is.Zero);
            Assert.That(bow.LastFiredProjectile, Is.Null);
        }

        [Test]
        public void RaidArcherReleaseKeepsItsBallisticAimUntilLateUpdate()
        {
            enemy = CreateEnemy(Vector3.zero);
            CharacterAimSource aimSource =
                enemy.AddComponent<CharacterAimSource>();
            BowWeapon bow = CreateTestBow(enemy);
            Vector3 launchPoint = bow.PresentedArrowTip;
            Vector3 ballisticDirection =
                new Vector3(0.04f, 0.12f, 1f).normalized;
            aimSource.SetOverride(
                launchPoint,
                ballisticDirection);
            SetPrivateField(bow, "drawHeldLastFrame", true);
            SetPrivateField(
                bow,
                "heldDuration",
                bow.FullDrawDuration);

            MethodInfo queueRelease = typeof(BowWeapon).GetMethod(
                "QueueRelease",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(queueRelease, Is.Not.Null);
            queueRelease.Invoke(bow, null);

            aimSource.SetOverride(
                launchPoint,
                new Vector3(-0.8f, -0.5f, 0.1f));
            bow.CommitPendingReleaseAtRenderedCamera();

            Assert.That(
                Vector3.Angle(
                    bow.LastShotDirection,
                    ballisticDirection),
                Is.LessThan(0.05f),
                "Recovery-frame look changes must not replace the drop-compensated release direction.");
            Object.DestroyImmediate(
                bow.LastFiredProjectile.gameObject);
        }

        [Test]
        public void StationaryLongRangeTargetIsOnTheBallisticTrajectory()
        {
            player = CreateTarget(
                "Player",
                new Vector3(0f, 0f, 90f));
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.Configure(player.transform);
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "bowWeapon", bow);

            Vector3 aimPoint = (Vector3)
                ResolvePrivateMethod("ResolvePerfectAimPoint")
                    .Invoke(brain, null);
            Vector3 origin = bow.PresentedArrowTip;
            Vector3 direction = (aimPoint - origin).normalized;
            Vector3 targetChest =
                player.transform.position + Vector3.up * 1.20f;
            float flightTime =
                (targetChest.z - origin.z) /
                (direction.z * bow.MaximumArrowSpeed);
            Vector3 simulatedImpact =
                origin +
                direction * bow.MaximumArrowSpeed * flightTime +
                Physics.gravity *
                (0.5f * flightTime * flightTime);

            Assert.That(flightTime, Is.GreaterThan(0f));
            Assert.That(
                Vector3.Distance(simulatedImpact, targetChest),
                Is.LessThan(0.08f),
                "A stationary exposed player at 90 metres should be intersected by a full-speed AI arrow trajectory.");
        }

        [Test]
        public void RaidArcherAimRayBeginsAtThePresentedArrowTip()
        {
            enemy = CreateEnemy(Vector3.zero);
            EnemyBrain brain =
                enemy.GetComponent<EnemyBrain>();
            CharacterAimSource aimSource =
                enemy.AddComponent<CharacterAimSource>();
            BowWeapon bow = CreateTestBow(enemy);
            SetPrivateField(brain, "aimSource", aimSource);
            SetPrivateField(brain, "bowWeapon", bow);
            Vector3 targetPoint =
                new Vector3(4f, 1.2f, 35f);

            ResolvePrivateMethod("SetAim").Invoke(
                brain,
                new object[] { targetPoint });

            Assert.That(
                aimSource.Origin,
                Is.EqualTo(bow.PresentedArrowTip),
                "AI parallax compensation must originate where the projectile actually launches.");
            Assert.That(
                aimSource.Direction,
                Is.EqualTo(
                    (targetPoint -
                        bow.PresentedArrowTip).normalized));
        }

        [Test]
        public void PatrolStartsOnALongLegAndUsesExtendedStops()
        {
            enemy = CreateEnemy(
                new Vector3(0f, 0f, 10f));
            EnemyBrain brain =
                enemy.GetComponent<EnemyBrain>();
            Vector3[] route =
            {
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 21f),
                new Vector3(0f, 0f, 32f),
                new Vector3(0f, 0f, 43f)
            };

            brain.ConfigurePatrolRoute(route, 1);

            Assert.That(
                GetPrivateField<int>(
                    brain,
                    "patrolRouteIndex"),
                Is.EqualTo(2),
                "A guard spawned on a route node should immediately choose the next travel leg instead of beginning with a stop.");
            float pause = (float)
                ResolvePrivateMethod(
                        "ResolvePatrolPauseDuration")
                    .Invoke(brain, null);
            Assert.That(pause, Is.InRange(3.2f, 6.4f));
            Assert.That(
                Vector3.Distance(
                    route[1],
                    route[2]),
                Is.GreaterThanOrEqualTo(10f));
        }

        private static BowWeapon CreateTestBow(
            GameObject owner)
        {
            PlayerInputSource input =
                owner.GetComponent<PlayerInputSource>() ??
                owner.AddComponent<PlayerInputSource>();
            GameObject arrow =
                new GameObject("Test Nocked Arrow");
            arrow.transform.SetParent(owner.transform, false);
            arrow.transform.localPosition =
                new Vector3(0.35f, 1.1f, 0.2f);
            BowWeapon bow = owner.AddComponent<BowWeapon>();
            bow.Configure(
                input,
                owner.transform,
                owner.transform,
                arrow.transform);
            bow.SetWeaponEquipped(true);
            return bow;
        }

        private static GameObject CreateTarget(
            string name,
            Vector3 position)
        {
            var target = new GameObject(name);
            if (name == "Player")
            {
                target.tag = "Player";
                target.layer = 2;
            }
            target.transform.position = position;
            CapsuleCollider collider =
                target.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.3f;
            collider.center = Vector3.up;
            return target;
        }

        private static GameObject CreateEnemy(Vector3 position)
        {
            var root = new GameObject("Enemy");
            root.transform.position = position;
            CharacterController controller =
                root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.25f;
            Health health = root.AddComponent<Health>();
            health.ConfigureWithFloor(88f, 0f);
            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            InvokePrivate(brain, "Awake");
            return root;
        }

        private static bool EvaluateSight(EnemyBrain brain)
        {
            MethodInfo targetPointMethod =
                ResolvePrivateMethod(
                    "ResolveTargetChestPoint");
            MethodInfo sightMethod =
                ResolvePrivateMethod("CanSeeTarget");
            Vector3 targetPoint = (Vector3)
                targetPointMethod.Invoke(brain, null);
            return (bool)sightMethod.Invoke(
                brain,
                new object[] { targetPoint });
        }

        private static void UpdatePerception(EnemyBrain brain)
        {
            MethodInfo targetPointMethod =
                ResolvePrivateMethod(
                    "ResolveTargetChestPoint");
            Vector3 targetPoint = (Vector3)
                targetPointMethod.Invoke(brain, null);
            ResolvePrivateMethod("UpdatePerception").Invoke(
                brain,
                new object[] { targetPoint });
        }

        private static void InvokePrivate(
            EnemyBrain brain,
            string methodName)
        {
            ResolvePrivateMethod(methodName).Invoke(
                brain,
                null);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            Assert.That(target, Is.Not.Null);
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(
            EnemyBrain brain,
            string fieldName)
        {
            FieldInfo field = typeof(EnemyBrain).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(brain);
        }

        private static MethodInfo ResolvePrivateMethod(
            string methodName)
        {
            MethodInfo method = typeof(EnemyBrain).GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static string DescribeSightRay(EnemyBrain brain)
        {
            Vector3 origin = (Vector3)
                ResolvePrivateMethod("ResolveSightOrigin")
                    .Invoke(brain, null);
            Vector3 targetPoint = (Vector3)
                ResolvePrivateMethod("ResolveTargetChestPoint")
                    .Invoke(brain, null);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                (targetPoint - origin).normalized,
                Vector3.Distance(origin, targetPoint) + 0.15f,
                ~0,
                QueryTriggerInteraction.Ignore);
            string names = string.Empty;
            for (int index = 0; index < hits.Length; index++)
            {
                names +=
                    $"{hits[index].collider.name}:" +
                    $"{hits[index].distance:0.00} ";
            }

            return
                $"origin={origin}; target={targetPoint}; " +
                $"forward={brain.transform.forward}; hits={names}";
        }
    }
}
