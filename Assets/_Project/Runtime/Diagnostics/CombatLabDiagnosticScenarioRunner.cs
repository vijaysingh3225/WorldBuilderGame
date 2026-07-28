using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Diagnostics
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class CombatLabDiagnosticScenarioRunner : MonoBehaviour
    {
        private const int FramesPerSecond = 60;
        private const int MaximumStepTransitionsPerUpdate = 8;
        private static readonly Vector3 PlayerStart = new Vector3(0f, 1f, -5.5f);
        private static readonly Vector3 DummyStart = new Vector3(0f, 1f, 5f);

        public static readonly string[] RequiredPhaseKeys =
        {
            "movement/idle",
            "movement/walk-forward",
            "movement/walk-stop",
            "movement/sprint-forward",
            "movement/sprint-release",
            "movement/sprint-stop",
            "movement/reversal-approach",
            "movement/sprint-reversal",
            "movement/sprint-right-approach",
            "movement/sprint-right-turn",
            "movement/sprint-left-approach",
            "movement/sprint-left-turn",
            "movement/sprint-alternating-approach",
            "movement/sprint-alternating-turns",
            "movement/crouch-idle",
            "movement/crouch-move",
            "movement/crouch-exit",
            "movement/idle-jump",
            "movement/running-jump-approach",
            "movement/running-jump",
            "combat/block-jump-ready",
            "combat/block-jump",
            "combat/block-toggle-stress",
            "combat/block-entry-rest",
            "combat/block-hold",
            "combat/block-release",
            "combat/sheathe-sword",
            "combat/unarmed-slot",
            "combat/draw-sword",
            "suite/complete"
        };

        private readonly Queue<DiagnosticStep> steps = new Queue<DiagnosticStep>(48);
        private GameplayDiagnosticRecorder recorder;
        private ThirdPersonMotor motor;
        private PlayerInputSource input;
        private Health playerHealth;
        private Health enemyHealth;
        private EnemyBrain enemyBrain;
        private CharacterController enemyController;
        private TwoSlotWeaponPresenter weaponSlots;
        private DiagnosticStep currentStep;
        private int currentStepFrame;
        private bool running;
        private bool finalizing;
        private bool abortRequested;
        private string abortReason;
        private bool timingConfigured;
        private float previousCaptureDeltaTime;
        private float previousFixedDeltaTime;
        private int previousTargetFrameRate;
        private int previousVSyncCount;
        private float previousTimeScale;

        public static event Action<GameplayDiagnosticCompletion> SuiteCompleted;

        public bool IsRunning => running;

        private sealed class DiagnosticStep
        {
            public string Scenario;
            public string Phase;
            public int FrameCount;
            public bool Screenshot;
            public Func<int, PlayerIntent> Intent;
            public Action OnStart;
            public Action<int> BeforeFrame;
            public Func<int, bool> CompleteBeforeFrame;
            public Action OnEnd;
        }

        public void Configure(GameplayDiagnosticRecorder diagnosticRecorder)
        {
            recorder = diagnosticRecorder;
        }

        public void StartSuite()
        {
            if (running)
            {
                throw new InvalidOperationException("The Combat Lab diagnostic suite is already running.");
            }

            ResolveReferences();
            if (recorder == null || motor == null || input == null)
            {
                throw new InvalidOperationException(
                    "The Combat Lab diagnostic suite could not find every required production component.");
            }

            SaveTimingSettings();
            ConfigureDeterministicTiming();
            try
            {
                abortRequested = false;
                abortReason = string.Empty;
                finalizing = false;
                currentStep = null;
                currentStepFrame = 0;
                steps.Clear();
                enemyBrain.ConfigureAsTrainingDummy();
                playerHealth.Configure(100f);
                BuildScenarioSteps();
                recorder.BeginCapture("deterministic-full-suite");
                running = true;
            }
            catch
            {
                RestoreTimingSettings();
                throw;
            }
        }

        public void RequestAbort(string reason)
        {
            if (!running)
            {
                return;
            }

            abortRequested = true;
            abortReason = string.IsNullOrWhiteSpace(reason) ? "The suite was aborted." : reason;
        }

        public void AbortImmediately(string reason)
        {
            if (!running)
            {
                return;
            }

            FinalizeSuite(false, string.IsNullOrWhiteSpace(reason) ? "The suite was aborted." : reason);
        }

        private void Update()
        {
            if (!running || finalizing)
            {
                return;
            }

            if (abortRequested)
            {
                FinalizeSuite(false, abortReason);
                return;
            }

            try
            {
                int transitions = 0;
                while (currentStep == null || IsCurrentStepComplete())
                {
                    if (currentStep != null)
                    {
                        currentStep.OnEnd?.Invoke();
                        currentStep = null;
                    }

                    if (steps.Count == 0)
                    {
                        FinalizeSuite(true, string.Empty);
                        return;
                    }

                    currentStep = steps.Dequeue();
                    currentStepFrame = 0;
                    currentStep.OnStart?.Invoke();
                    recorder.BeginPhase(currentStep.Scenario, currentStep.Phase, currentStep.Screenshot);
                    transitions++;
                    if (transitions > MaximumStepTransitionsPerUpdate)
                    {
                        throw new InvalidOperationException("Diagnostic steps advanced without consuming a frame.");
                    }
                }

                currentStep.BeforeFrame?.Invoke(currentStepFrame);
                PlayerIntent intent = currentStep.Intent != null ? currentStep.Intent(currentStepFrame) : default;
                input.SetDiagnosticOverride(intent);
                currentStepFrame++;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinalizeSuite(false, exception.Message);
            }
        }

        private bool IsCurrentStepComplete()
        {
            if (currentStep.CompleteBeforeFrame != null && currentStep.CompleteBeforeFrame(currentStepFrame))
            {
                return true;
            }

            return currentStepFrame >= currentStep.FrameCount;
        }

        private void OnDisable()
        {
            if (running && !finalizing)
            {
                FinalizeSuite(false, "The diagnostic runner was disabled before completion.");
            }
        }

        private void ResolveReferences()
        {
            recorder ??= FindFirstObjectByType<GameplayDiagnosticRecorder>();
            motor = FindFirstObjectByType<ThirdPersonMotor>();
            input = motor != null ? motor.GetComponent<PlayerInputSource>() : null;
            playerHealth = motor != null ? motor.GetComponent<Health>() : null;
            enemyBrain = FindFirstObjectByType<EnemyBrain>();
            enemyHealth = enemyBrain != null ? enemyBrain.GetComponent<Health>() : null;
            enemyController = enemyBrain != null ? enemyBrain.GetComponent<CharacterController>() : null;
            weaponSlots =
                FindFirstObjectByType<TwoSlotWeaponPresenter>();
        }

        private void BuildScenarioSteps()
        {
            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4, resetDummyHealth: 88f);
            EnqueueFixed("movement", "idle", 30, default, screenshot: true);
            EnqueueFixed("movement", "walk-forward", 120, Intent(Vector2.up), screenshot: true,
                onEnd: () => recorder.MarkLastFrame("steady-walk", true));
            EnqueueFixed("movement", "walk-stop", 45, default);

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4);
            EnqueueFixed("movement", "sprint-forward", 90, Intent(Vector2.up, sprint: true), screenshot: true,
                onEnd: () => recorder.MarkLastFrame("steady-sprint", true));
            EnqueueFixed("movement", "sprint-release", 45, Intent(Vector2.up));
            EnqueueFixed("movement", "sprint-stop", 30, default);

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4);
            EnqueueFixed("movement", "reversal-approach", 48, Intent(Vector2.up, sprint: true));
            EnqueueFixed("movement", "sprint-reversal", 100, Intent(Vector2.down, sprint: true), screenshot: true,
                onEnd: MarkReversalOutcome);

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4);
            EnqueueFixed("movement", "sprint-right-approach", 48, Intent(Vector2.up, sprint: true));
            EnqueueFixed("movement", "sprint-right-turn", 90, Intent(Vector2.right, sprint: true), screenshot: true,
                onEnd: () => recorder.MarkLastFrame("right-turn-end", true));

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4);
            EnqueueFixed("movement", "sprint-left-approach", 48, Intent(Vector2.up, sprint: true));
            EnqueueFixed("movement", "sprint-left-turn", 90, Intent(Vector2.left, sprint: true), screenshot: true,
                onEnd: () => recorder.MarkLastFrame("left-turn-end", true));

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4);
            EnqueueFixed("movement", "sprint-alternating-approach", 48, Intent(Vector2.up, sprint: true));
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "movement",
                Phase = "sprint-alternating-turns",
                FrameCount = 96,
                Screenshot = true,
                Intent = frame => Intent(frame < 24 || frame >= 72 ? Vector2.right : Vector2.left, sprint: true),
                OnEnd = () => recorder.MarkLastFrame("alternating-turns-end", true)
            });

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 4);
            EnqueueFixed("movement", "crouch-idle", 60, Intent(Vector2.zero, crouch: true), screenshot: true,
                onEnd: () => recorder.MarkLastFrame("settled-crouch-idle", true));
            EnqueueFixed("movement", "crouch-move", 75, Intent(Vector2.up, crouch: true), screenshot: true);
            EnqueueFixed("movement", "crouch-exit", 30, default,
                onEnd: () => recorder.MarkLastFrame("standing-after-crouch", true));

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 6);
            EnqueueJump("idle-jump", Vector2.zero, false, screenshot: true);

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 6);
            EnqueueFixed("movement", "running-jump-approach", 48, Intent(Vector2.up, sprint: true));
            EnqueueJump("running-jump", Vector2.up, true, screenshot: true);

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 6);
            EnqueueFixed(
                "combat",
                "block-jump-ready",
                18,
                Intent(Vector2.zero, blockHeld: true));
            EnqueueJump(
                "block-jump",
                Vector2.zero,
                false,
                screenshot: true,
                blockHeld: true,
                scenario: "combat");

            EnqueuePlayerReset(PlayerStart, Quaternion.identity, 6);
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "block-toggle-stress",
                FrameCount = 72,
                Intent = frame => Intent(
                    Vector2.zero,
                    blockHeld: (frame / 3) % 2 == 0),
                OnEnd = () => recorder.MarkLastFrame("rapid-block-toggle-complete", true)
            });
            EnqueueFixed("combat", "block-entry-rest", 30, default);
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "block-hold",
                FrameCount = 60,
                Screenshot = true,
                Intent = _ => Intent(Vector2.zero, blockHeld: true),
                BeforeFrame = frame =>
                {
                    if (frame == 3 || frame == 6 || frame == 9 || frame == 12)
                    {
                        recorder.MarkLastFrame(
                            "block-entry-" + frame.ToString("00"),
                            true);
                    }
                },
                OnEnd = () => recorder.MarkLastFrame("two-handed-block-held", true)
            });
            EnqueueFixed(
                "combat",
                "block-release",
                30,
                default,
                screenshot: true,
                onEnd: () => recorder.MarkLastFrame("one-handed-carry-restored", true));

            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "sheathe-sword",
                FrameCount = 120,
                Screenshot = true,
                OnStart = () =>
                {
                    if (weaponSlots == null ||
                        !weaponSlots.RequestSlot(
                            TwoSlotWeaponPresenter.SecondarySlot))
                    {
                        throw new InvalidOperationException(
                            "The two-slot presenter rejected the diagnostic sheathe request.");
                    }
                },
                Intent = _ => default,
                BeforeFrame = frame =>
                {
                    if (frame == 10 ||
                        frame == 22 ||
                        frame == 34 ||
                        frame == 50 ||
                        frame == 70 ||
                        frame == 90)
                    {
                        recorder.MarkLastFrame(
                            "sheathe-progress-" + frame.ToString("00"),
                            true);
                    }
                },
                CompleteBeforeFrame = frame =>
                    frame > 2 &&
                    !weaponSlots.IsTransitioning &&
                    weaponSlots.ActiveSlot ==
                        TwoSlotWeaponPresenter.SecondarySlot,
                OnEnd = () =>
                    recorder.MarkLastFrame("sword-sheathed-on-back", true)
            });
            EnqueueFixed(
                "combat",
                "unarmed-slot",
                30,
                default,
                screenshot: true,
                onEnd: () => recorder.MarkLastFrame("slot-two-unarmed", true));
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "draw-sword",
                FrameCount = 120,
                Screenshot = true,
                OnStart = () =>
                {
                    if (!weaponSlots.RequestSlot(
                            TwoSlotWeaponPresenter.PrimarySlot))
                    {
                        throw new InvalidOperationException(
                            "The two-slot presenter rejected the diagnostic draw request.");
                    }
                },
                Intent = _ => default,
                BeforeFrame = frame =>
                {
                    if (frame == 10 ||
                        frame == 22 ||
                        frame == 34 ||
                        frame == 50 ||
                        frame == 70 ||
                        frame == 90)
                    {
                        recorder.MarkLastFrame(
                            "draw-progress-" + frame.ToString("00"),
                            true);
                    }
                },
                CompleteBeforeFrame = frame =>
                    frame > 2 &&
                    !weaponSlots.IsTransitioning &&
                    weaponSlots.ActiveSlot ==
                        TwoSlotWeaponPresenter.PrimarySlot,
                OnEnd = () =>
                    recorder.MarkLastFrame("sword-redrawn", true)
            });

            EnqueueFixed("suite", "complete", 3, default, screenshot: true);
        }

        private void EnqueuePlayerReset(
            Vector3 position,
            Quaternion rotation,
            int settleFrames,
            float? resetDummyHealth = null)
        {
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "setup",
                Phase = "player-reset",
                FrameCount = settleFrames,
                Intent = _ => default,
                OnStart = () =>
                {
                    ResetPlayer(position, rotation);
                    if (resetDummyHealth.HasValue)
                    {
                        ResetDummy(resetDummyHealth.Value);
                    }
                }
            });
        }

        private void EnqueueFixed(
            string scenario,
            string phase,
            int frames,
            PlayerIntent intent,
            bool screenshot = false,
            Action onEnd = null)
        {
            EnqueueStep(new DiagnosticStep
            {
                Scenario = scenario,
                Phase = phase,
                FrameCount = frames,
                Screenshot = screenshot,
                Intent = _ => intent,
                OnEnd = onEnd
            });
        }

        private void EnqueueJump(
            string phase,
            Vector2 move,
            bool sprint,
            bool screenshot,
            bool blockHeld = false,
            string scenario = "movement")
        {
            bool leftGround = false;
            bool markedAirborne = false;
            bool markedApex = false;
            float previousVerticalVelocity = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = scenario,
                Phase = phase,
                FrameCount = 192,
                Screenshot = screenshot,
                OnStart = () =>
                {
                    leftGround = false;
                    markedAirborne = false;
                    markedApex = false;
                    previousVerticalVelocity = motor.VerticalVelocity;
                },
                Intent = frame => frame == 0
                    ? Intent(
                        move,
                        sprint,
                        jumpPressed: true,
                        jumpHeld: true,
                        blockHeld: blockHeld)
                    : frame <= 11
                        ? Intent(
                            move,
                            sprint,
                            jumpHeld: true,
                            blockHeld: blockHeld)
                        : Intent(move, sprint, blockHeld: blockHeld),
                BeforeFrame = frame =>
                {
                    if (frame > 0 && !motor.IsGrounded)
                    {
                        leftGround = true;
                        if (!markedAirborne)
                        {
                            recorder.MarkLastFrame("airborne", true);
                            markedAirborne = true;
                        }
                    }

                    if (frame > 0 && leftGround && !markedApex &&
                        previousVerticalVelocity > 0f && motor.VerticalVelocity <= 0f)
                    {
                        recorder.MarkLastFrame("apex", true);
                        markedApex = true;
                    }

                    previousVerticalVelocity = motor.VerticalVelocity;
                },
                CompleteBeforeFrame = frame => frame > 8 && leftGround && motor.IsGrounded,
                OnEnd = () => recorder.MarkLastFrame(
                    leftGround && motor.IsGrounded ? "landed" : "landing-not-detected",
                    true)
            });
        }

        private void EnqueueStep(DiagnosticStep step)
        {
            steps.Enqueue(step);
        }

        private void MarkReversalOutcome()
        {
            float facingError = motor.HorizontalVelocity.sqrMagnitude > 0.0001f
                ? Mathf.Abs(Vector3.SignedAngle(motor.transform.forward, motor.HorizontalVelocity, Vector3.up))
                : 180f;
            bool recovered = motor.HorizontalSpeed >= motor.SprintSpeed * 0.85f && facingError <= 12f;
            recorder.MarkLastFrame(recovered ? "reversal-recovered" : "reversal-end-needs-review", true);
        }

        private void MarkLethalOutcome()
        {
            recorder.MarkLastFrame(enemyHealth.IsAlive ? "lethal-chain-end-needs-review" : "dummy-dead", true);
        }

        private void ResetPlayer(Vector3 position, Quaternion rotation)
        {
            recorder.ResetContinuity();
            input.SetDiagnosticOverride(default);
            playerHealth.Configure(100f);
            motor.ResetForDiagnostics(position, rotation);
            Physics.SyncTransforms();
        }

        private void ResetDummy(float health)
        {
            if (enemyController != null)
            {
                enemyController.enabled = false;
            }

            enemyBrain.transform.SetPositionAndRotation(DummyStart, Quaternion.Euler(0f, 180f, 0f));
            if (enemyController != null)
            {
                enemyController.enabled = true;
            }

            enemyBrain.ConfigureAsTrainingDummy();
            enemyHealth.Configure(health);
            Physics.SyncTransforms();
        }

        private static PlayerIntent Intent(
            Vector2 move,
            bool sprint = false,
            bool jumpPressed = false,
            bool jumpHeld = false,
            bool crouch = false,
            bool attackPressed = false,
            bool blockHeld = false)
        {
            return new PlayerIntent(
                move,
                Vector2.zero,
                sprint,
                jumpPressed,
                jumpHeld,
                crouch,
                attackPressed,
                blockHeld);
        }

        private void FinalizeSuite(bool completed, string reason)
        {
            if (!running || finalizing)
            {
                return;
            }

            finalizing = true;
            GameplayDiagnosticCompletion completion = default;
            try
            {
                input?.ClearDiagnosticOverride();
                if (recorder != null && recorder.IsRecording)
                {
                    completion = recorder.CompleteCapture(completed, reason, captureCurrentFrame: false);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                RestoreTimingSettings();
                steps.Clear();
                currentStep = null;
                running = false;
                finalizing = false;
                SuiteCompleted?.Invoke(completion);
            }
        }

        private void SaveTimingSettings()
        {
            previousCaptureDeltaTime = Time.captureDeltaTime;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            previousTimeScale = Time.timeScale;
            timingConfigured = true;
        }

        private static void ConfigureDeterministicTiming()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / FramesPerSecond;
            Time.fixedDeltaTime = 1f / FramesPerSecond;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FramesPerSecond;
        }

        private void RestoreTimingSettings()
        {
            if (!timingConfigured)
            {
                return;
            }

            Time.captureDeltaTime = previousCaptureDeltaTime;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
            Time.timeScale = previousTimeScale;
            timingConfigured = false;
        }
    }
}
