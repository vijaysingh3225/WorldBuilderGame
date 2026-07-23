using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Diagnostics;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class DiagnosticSeamTests
    {
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
                    true);

                input.SetDiagnosticOverride(expected);

                Assert.That(input.DiagnosticOverrideActive, Is.True);
                Assert.That(input.CurrentIntent.Move, Is.EqualTo(expected.Move));
                Assert.That(input.CurrentIntent.SprintHeld, Is.True);
                Assert.That(input.CurrentIntent.JumpPressed, Is.True);
                Assert.That(input.CurrentIntent.AttackPressed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
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
        public void MeleeTelemetryDistinguishesAcceptedMissFromCooldownRejection()
        {
            GameObject owner = new GameObject("diagnostic-melee-test");
            owner.transform.position = new Vector3(10000f, 10000f, 10000f);
            try
            {
                owner.AddComponent<PlayerInputSource>();
                Health health = owner.AddComponent<Health>();
                health.Configure(100f);
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();
                MeleeAttackReport report = default;
                string rejection = null;
                int resolvedCount = 0;
                int rejectedCount = 0;
                weapon.AttackResolved += value =>
                {
                    resolvedCount++;
                    report = value;
                };
                weapon.AttackRejected += reason =>
                {
                    rejectedCount++;
                    rejection = reason;
                };

                bool accepted = weapon.TryAttack();
                bool repeated = weapon.TryAttack();

                Assert.That(accepted, Is.True);
                Assert.That(resolvedCount, Is.EqualTo(1), "An accepted attack must publish exactly one resolution report.");
                Assert.That(report.DamagedTargets, Is.EqualTo(0));
                Assert.That(repeated, Is.False);
                Assert.That(rejectedCount, Is.EqualTo(1), "The cooldown attempt must publish exactly one rejection.");
                Assert.That(rejection, Is.EqualTo("cooldown"));
            }
            finally
            {
                Object.DestroyImmediate(owner);
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
