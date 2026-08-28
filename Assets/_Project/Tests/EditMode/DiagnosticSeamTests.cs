using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Diagnostics;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class DiagnosticSeamTests
    {
        [Test]
        public void SteepTerrainForcesDownhillMotion()
        {
            Vector3 steepNormal = Quaternion.AngleAxis(
                62f,
                Vector3.forward) * Vector3.up;
            Vector3 motion = ThirdPersonMotor.ApplySteepSlopeSlide(
                Vector3.left * 3f + Vector3.down * 2f,
                steepNormal,
                45f,
                ThirdPersonMotor.DefaultSteepSlopeSlideSpeed);
            Vector3 downhill = Vector3.ProjectOnPlane(
                Vector3.down,
                steepNormal).normalized;

            Assert.That(
                ThirdPersonMotor.IsSteepSlope(steepNormal, 45f),
                Is.True);
            Assert.That(
                Vector3.Dot(motion, downhill),
                Is.GreaterThan(0.5f),
                "Movement on an unwalkable ramp must carry the player downhill instead of holding a jump pose in place.");
            Assert.That(
                ThirdPersonMotor.IsSteepSlope(Vector3.up, 45f),
                Is.False);
            Assert.That(
                ThirdPersonMotor.CalculateGroundedPresentation(
                    true,
                    -2f),
                Is.True,
                // Steep contact blocks uphill traversal; it is not airtime.
                "Contact with an unwalkable slope must remain grounded for animation even though uphill control is rejected.");
            Assert.That(
                ThirdPersonMotor.CalculateGroundedPresentation(
                    true,
                    4f),
                Is.False,
                "A real upward jump must remain airborne even while the ground probe is still in range at takeoff.");
            Assert.That(
                ThirdPersonMotor.MinimumStableGroundProbeDistance,
                Is.LessThan(ThirdPersonMotor.MinimumTraversalStepOffset),
                "Ground tolerance must bridge terrain seams without treating a full climbable step as continuous floor.");
        }

        [Test]
        public void CorpseCollidersRemainRaycastableButDoNotBlockPlayer()
        {
            GameObject player = new GameObject("Traversal Test Player");
            GameObject corpse = new GameObject("Traversal Test Corpse");
            try
            {
                CharacterController controller =
                    player.AddComponent<CharacterController>();
                BoxCollider corpseCollider =
                    corpse.AddComponent<BoxCollider>();

                HumanoidRagdoll.IgnoreControllerCollision(
                    controller,
                    new[] { corpseCollider });

                Assert.That(corpseCollider.enabled, Is.True);
                Assert.That(corpseCollider.isTrigger, Is.False);
                Assert.That(
                    Physics.GetIgnoreCollision(
                        controller,
                        corpseCollider),
                    Is.True,
                    "A corpse must stay targetable for looting without snagging the player controller.");
            }
            finally
            {
                Object.DestroyImmediate(corpse);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void GameplayInteractionDefaultsToF()
        {
            Assert.That(
                PlayerControlBindings.DefaultKeyName(
                    PlayerControl.Interact),
                Is.EqualTo("F"));
        }

        [Test]
        public void DiagnosticIntentOverrideFeedsTheProductionInputBoundary()
        {
            GameObject owner = new GameObject("diagnostic-input-test");
            try
            {
                PlayerInputSource input = owner.AddComponent<PlayerInputSource>();
                PlayerIntent expected = new PlayerIntent(
                    new Vector2(0.4f, 0.8f),
                    new Vector2(2f, -3f),
                    true,
                    true,
                    true,
                    false,
                    true,
                    true,
                    true);

                input.SetDiagnosticOverride(expected);

                Assert.That(input.DiagnosticOverrideActive, Is.True);
                Assert.That(input.CurrentIntent.Move, Is.EqualTo(expected.Move));
                Assert.That(input.CurrentIntent.SprintHeld, Is.True);
                Assert.That(input.CurrentIntent.JumpPressed, Is.True);
                Assert.That(input.CurrentIntent.AttackPressed, Is.True);
                Assert.That(input.CurrentIntent.AttackHeld, Is.True);
                Assert.That(input.CurrentIntent.BlockHeld, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlayerBowDrawUsesHeldLeftClickNotRightClick()
        {
            GameObject player = new GameObject("left-click-bow-player");
            try
            {
                player.tag = "Player";
                PlayerInputSource input =
                    player.AddComponent<PlayerInputSource>();
                GameObject bowObject = new GameObject("left-click-bow");
                bowObject.transform.SetParent(player.transform, false);
                GameObject arrowObject = new GameObject("nocked-arrow");
                arrowObject.transform.SetParent(bowObject.transform, false);
                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                bow.Configure(
                    input,
                    player.transform,
                    bowObject.transform,
                    arrowObject.transform);
                bow.SetWeaponEquipped(true);

                input.SetDiagnosticOverride(new PlayerIntent(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    true,
                    false,
                    true));
                InvokeBowUpdate(bow);

                Assert.That(bow.DrawInputHeld, Is.True);
                Assert.That(bow.IsDrawing, Is.True);

                input.SetDiagnosticOverride(new PlayerIntent(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    false));
                InvokeBowUpdate(bow);

                Assert.That(bow.DrawInputHeld, Is.False);
                Assert.That(
                    bow.IsDrawing,
                    Is.False,
                    "Right click must not begin or sustain a player bow draw.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void EnemyBowDrawStillUsesItsAiHoldSignal()
        {
            GameObject enemy = new GameObject("ai-bow-enemy");
            try
            {
                PlayerInputSource input =
                    enemy.AddComponent<PlayerInputSource>();
                GameObject bowObject = new GameObject("ai-bow");
                bowObject.transform.SetParent(enemy.transform, false);
                GameObject arrowObject = new GameObject("nocked-arrow");
                arrowObject.transform.SetParent(bowObject.transform, false);
                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                bow.Configure(
                    input,
                    enemy.transform,
                    bowObject.transform,
                    arrowObject.transform);
                bow.SetWeaponEquipped(true);

                input.SetDiagnosticOverride(new PlayerIntent(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    false));
                InvokeBowUpdate(bow);

                Assert.That(bow.IsPlayerOwned, Is.False);
                Assert.That(bow.DrawInputHeld, Is.True);
                Assert.That(bow.IsDrawing, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void PlayerBowWaitsForRenockBeforeAnotherDraw()
        {
            GameObject player = new GameObject("player-bow-recovery");
            try
            {
                player.tag = "Player";
                PlayerInputSource input =
                    player.AddComponent<PlayerInputSource>();
                GameObject bowObject = new GameObject("player-bow");
                bowObject.transform.SetParent(player.transform, false);
                GameObject arrowObject = new GameObject("nocked-arrow");
                arrowObject.transform.SetParent(bowObject.transform, false);
                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                bow.Configure(
                    input,
                    player.transform,
                    bowObject.transform,
                    arrowObject.transform);
                bow.SetWeaponEquipped(true);

                MethodInfo fireArrow = typeof(BowWeapon).GetMethod(
                    "FireArrow",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(fireArrow, Is.Not.Null);
                fireArrow.Invoke(bow, new object[] { 1f });

                Assert.That(bow.ArrowReady, Is.False);
                Assert.That(
                    bow.EffectiveReloadDuration,
                    Is.EqualTo(0.65f).Within(0.001f));
                Assert.That(
                    bow.ReloadRemaining,
                    Is.EqualTo(bow.EffectiveReloadDuration).Within(0.001f));
                Assert.That(bow.PostShotPresentationActive, Is.True);
                Assert.That(bow.PresentationAimLocked, Is.True);
                Assert.That(
                    bow.PresentedDrawNormalized,
                    Is.EqualTo(1f).Within(0.001f),
                    "The drawing hand should retain the released full-draw pose while the physical string resets.");

                input.SetDiagnosticOverride(new PlayerIntent(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true));
                InvokeBowUpdate(bow);

                Assert.That(
                    bow.IsDrawing,
                    Is.False,
                    "Holding left click during recovery must wait until the replacement arrow is nocked.");
                Assert.That(bow.DrawInputHeld, Is.False);

                if (bow.LastFiredProjectile != null)
                {
                    Object.DestroyImmediate(
                        bow.LastFiredProjectile.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayerBowRecoveryPoseUsesHalfLengthHoldBeforeReturning()
        {
            const float ReloadDuration = 0.65f;
            const float ReturnDuration = 0.18f;
            float poseDuration =
                BowWeapon.PlayerPostShotHoldDuration +
                ReturnDuration;
            float heldFollowThrough = BowWeapon.CalculateReadyWeight(
                1f,
                false,
                ReturnDuration,
                ReturnDuration,
                poseDuration,
                ReturnDuration,
                true);
            float ordinaryReturnWeight =
                BowWeapon.CalculateReadyWeight(
                    1f,
                    false,
                    ReturnDuration,
                    ReturnDuration,
                    ReloadDuration,
                    0f,
                    false);

            Assert.That(
                BowWeapon.PlayerPostShotHoldDuration,
                Is.EqualTo(0.47f * 0.5f).Within(0.001f));
            Assert.That(
                heldFollowThrough,
                Is.EqualTo(1f).Within(0.001f),
                "The released bow should remain raised through the shortened hold.");
            Assert.That(
                ordinaryReturnWeight,
                Is.Zero,
                "Cancelled draws and NPC behavior should retain the ordinary quick return.");
            Assert.That(
                BowWeapon.CalculatePostShotPoseRemaining(
                    ReloadDuration - poseDuration,
                    ReloadDuration,
                    poseDuration),
                Is.Zero,
                "The release pose should finish before re-nocking completes.");
            Assert.That(
                BowWeapon.CalculatePostShotReadyWeight(
                    ReturnDuration * 0.5f,
                    poseDuration,
                    ReturnDuration),
                Is.EqualTo(0.5f).Within(0.001f),
                "The bow should retain the same quick return after the shorter hold.");
        }

        private static void InvokeBowUpdate(BowWeapon bow)
        {
            MethodInfo update = typeof(BowWeapon).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(bow, null);
        }

        [Test]
        public void ClearingDiagnosticIntentReturnsInputToDeviceSampling()
        {
            GameObject owner = new GameObject("diagnostic-input-clear-test");
            try
            {
                PlayerInputSource input = owner.AddComponent<PlayerInputSource>();
                input.SetDiagnosticOverride(new PlayerIntent(Vector2.up, Vector2.zero, true, false, false, false, false));

                input.ClearDiagnosticOverride();

                Assert.That(input.DiagnosticOverrideActive, Is.False);
                Assert.That(input.CurrentIntent.Move, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void GameplayEventsCarryMonotonicSequenceAndFrameContext()
        {
            GameplayEventLog.Clear();
            GameplayEventRecord first = default;
            GameplayEventRecord second = default;
            int count = 0;
            void Capture(GameplayEventRecord record)
            {
                count++;
                if (count == 1)
                {
                    first = record;
                }
                else
                {
                    second = record;
                }
            }

            GameplayEventLog.Published += Capture;
            try
            {
                GameplayEventLog.Publish("test", null, "first");
                GameplayEventLog.Publish("test", null, "second");

                Assert.That(first.Sequence, Is.EqualTo(1));
                Assert.That(second.Sequence, Is.EqualTo(2));
                Assert.That(second.Frame, Is.GreaterThanOrEqualTo(first.Frame));
                Assert.That(second.Realtime, Is.GreaterThanOrEqualTo(first.Realtime));
            }
            finally
            {
                GameplayEventLog.Published -= Capture;
                GameplayEventLog.Clear();
            }
        }

        [Test]
        public void DiagnosticSchemaV2FrameRoundTripPreservesDeterministicAndWallClocks()
        {
            GameplayDiagnosticFrame expected = new GameplayDiagnosticFrame
            {
                sample = 42,
                unityFrame = 314,
                time = 0.7f,
                deltaTime = 1f / 60f,
                wallTime = 9.25f,
                wallDeltaTime = 0.31f,
                scenario = "sprint-turn-right",
                phase = "turn",
                soleCalibrationValid = true
            };

            GameplayDiagnosticFrame actual =
                JsonUtility.FromJson<GameplayDiagnosticFrame>(JsonUtility.ToJson(expected));

            Assert.That(GameplayDiagnosticSchema.Version, Is.EqualTo(2));
            Assert.That(actual.sample, Is.EqualTo(expected.sample));
            Assert.That(actual.unityFrame, Is.EqualTo(expected.unityFrame));
            Assert.That(actual.time, Is.EqualTo(expected.time).Within(0.0001f));
            Assert.That(actual.deltaTime, Is.EqualTo(expected.deltaTime).Within(0.0001f));
            Assert.That(actual.wallTime, Is.EqualTo(expected.wallTime).Within(0.0001f));
            Assert.That(actual.wallDeltaTime, Is.EqualTo(expected.wallDeltaTime).Within(0.0001f));
            Assert.That(actual.scenario, Is.EqualTo(expected.scenario));
            Assert.That(actual.phase, Is.EqualTo(expected.phase));
            Assert.That(actual.soleCalibrationValid, Is.True);
        }

        [Test]
        public void DiagnosticSchemaV2ReportRoundTripPreservesCompletionAndConfigurationContract()
        {
            GameplayDiagnosticReport expected = new GameplayDiagnosticReport
            {
                schemaVersion = GameplayDiagnosticSchema.Version,
                runId = "contract-test",
                completed = false,
                abortReason = "timeout",
                passed = false,
                capabilities = new GameplayDiagnosticCapabilities
                {
                    input = true,
                    humanoidPoseBones = true,
                    screenshots = false
                },
                configuration = new GameplayDiagnosticConfiguration
                {
                    walkSpeed = 2.4f,
                    sprintSpeed = 5.8f,
                    animatorController = "CombatLabLocomotion",
                    cameraDistance = 4.5f
                },
                phases = new[]
                {
                    new GameplayDiagnosticPhaseSummary
                    {
                        scenario = "crouch-idle",
                        phase = "settled",
                        leftContactSamples = 12,
                        settledCrouchSamples = 18,
                        settledRearKneeSurfaceGapP90 = 0.04f
                    }
                }
            };

            GameplayDiagnosticReport actual =
                JsonUtility.FromJson<GameplayDiagnosticReport>(JsonUtility.ToJson(expected));

            Assert.That(actual.schemaVersion, Is.EqualTo(GameplayDiagnosticSchema.Version));
            Assert.That(actual.runId, Is.EqualTo(expected.runId));
            Assert.That(actual.completed, Is.False);
            Assert.That(actual.abortReason, Is.EqualTo(expected.abortReason));
            Assert.That(actual.passed, Is.False);
            Assert.That(actual.capabilities, Is.Not.Null);
            Assert.That(actual.capabilities.input, Is.True);
            Assert.That(actual.capabilities.humanoidPoseBones, Is.True);
            Assert.That(actual.capabilities.screenshots, Is.False);
            Assert.That(actual.configuration, Is.Not.Null);
            Assert.That(actual.configuration.walkSpeed, Is.EqualTo(2.4f).Within(0.0001f));
            Assert.That(actual.configuration.sprintSpeed, Is.EqualTo(5.8f).Within(0.0001f));
            Assert.That(actual.configuration.animatorController, Is.EqualTo("CombatLabLocomotion"));
            Assert.That(actual.configuration.cameraDistance, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(actual.phases, Has.Length.EqualTo(1));
            Assert.That(actual.phases[0].leftContactSamples, Is.EqualTo(12));
            Assert.That(actual.phases[0].settledCrouchSamples, Is.EqualTo(18));
            Assert.That(actual.phases[0].settledRearKneeSurfaceGapP90, Is.EqualTo(0.04f).Within(0.0001f));
        }
    }
}
