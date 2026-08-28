using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;
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
            "combat/block-strafe",
            "combat/block-backpedal",
            "combat/block-release",
            "combat/sheathe-sword",
            "combat/bow-slot",
            "combat/bow-grace-cancel",
            "combat/bow-partial-draw",
            "combat/bow-partial-release",
            "combat/bow-full-draw",
            "combat/model-inspection-orbit",
            "combat/model-inspection-restore",
            "combat/bow-aim-yaw-sweep",
            "combat/bow-aim-pitch-sweep",
            "combat/bow-aim-strafe",
            "combat/bow-aim-backpedal",
            "combat/bow-aim-forward-walk",
            "combat/bow-full-release",
            "combat/bow-flight-impact",
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
        private HitReactionPresenter enemyHitReaction;
        private CharacterController enemyController;
        private TwoSlotWeaponPresenter weaponSlots;
        private BowWeapon bowWeapon;
        private CameraAimTarget cameraAimTarget;
        private UpperBodyAimPresenter aimPresenter;
        private AimStanceLocomotionPresenter stancePresenter;
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
            if (recorder == null ||
                motor == null ||
                input == null ||
                bowWeapon == null ||
                cameraAimTarget == null ||
                aimPresenter == null)
            {
                throw new InvalidOperationException(
                    "The Combat Lab diagnostic suite could not find every required production component.");
            }

            recorder.Configure(
                motor,
                input,
                motor.GetComponentInChildren<Animator>(true),
                playerHealth,
                enemyHealth,
                enemyBrain,
                Camera.main);
            if (!bowWeapon.AudioConfigured)
            {
                throw new InvalidOperationException(
                    "The Combat Lab bow is missing its trimmed pullback, release, or impact audio.");
            }
            if (bowWeapon.PullbackVolume > 0.40f ||
                bowWeapon.FullDrawDuration < 1.05f ||
                bowWeapon.PartialVelocityExponent < 2f)
            {
                throw new InvalidOperationException(
                    "The bow did not retain the quieter, slower, nonlinear draw tuning: " +
                    $"volume={bowWeapon.PullbackVolume:0.00}, " +
                    $"fullDraw={bowWeapon.FullDrawDuration:0.00}, " +
                    $"powerExponent={bowWeapon.PartialVelocityExponent:0.00}.");
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
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            motor = player != null
                ? player.GetComponent<ThirdPersonMotor>()
                : FindFirstObjectByType<ThirdPersonMotor>();
            input = motor != null ? motor.GetComponent<PlayerInputSource>() : null;
            playerHealth = motor != null ? motor.GetComponent<Health>() : null;
            enemyBrain = FindFirstObjectByType<EnemyBrain>();
            enemyHealth = enemyBrain != null ? enemyBrain.GetComponent<Health>() : null;
            enemyHitReaction =
                enemyBrain != null
                    ? enemyBrain.GetComponent<HitReactionPresenter>()
                    : null;
            enemyController = enemyBrain != null ? enemyBrain.GetComponent<CharacterController>() : null;
            weaponSlots = motor != null
                ? motor.GetComponentInChildren<TwoSlotWeaponPresenter>(true)
                : null;
            bowWeapon = motor != null
                ? motor.GetComponentInChildren<BowWeapon>(true)
                : null;
            cameraAimTarget =
                FindFirstObjectByType<CameraAimTarget>();
            aimPresenter = motor != null
                ? motor.GetComponentInChildren<UpperBodyAimPresenter>(true)
                : null;
            stancePresenter = motor != null
                ? motor.GetComponentInChildren<
                    AimStanceLocomotionPresenter>(true)
                : null;
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
            float maximumBlockStrafeFacingError = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "block-strafe",
                FrameCount = 60,
                Screenshot = true,
                OnStart = () =>
                    maximumBlockStrafeFacingError = 0f,
                Intent = _ => Intent(
                    Vector2.right,
                    sprint: true,
                    blockHeld: true),
                BeforeFrame = _ =>
                    maximumBlockStrafeFacingError = Mathf.Max(
                        maximumBlockStrafeFacingError,
                        CameraFacingError()),
                OnEnd = () =>
                {
                    RequireAimFacing(
                        "sword block strafe",
                        maximumBlockStrafeFacingError);
                    RequireAimWalkSpeed(
                        "sword block strafe");
                    RequireSwordGuardWalk(
                        "sword block strafe",
                        1f);
                    recorder.MarkLastFrame(
                        "sword-block-standard-walk-held",
                        true);
                }
            });
            float maximumBlockBackpedalFacingError = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "block-backpedal",
                FrameCount = 60,
                Screenshot = true,
                OnStart = () =>
                    maximumBlockBackpedalFacingError = 0f,
                Intent = _ => Intent(
                    Vector2.down,
                    blockHeld: true),
                BeforeFrame = _ =>
                    maximumBlockBackpedalFacingError = Mathf.Max(
                        maximumBlockBackpedalFacingError,
                        CameraFacingError()),
                OnEnd = () =>
                {
                    RequireAimFacing(
                        "sword block backpedal",
                        maximumBlockBackpedalFacingError);
                    RequireSwordGuardWalk(
                        "sword block backpedal",
                        1f);
                }
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
                        TwoSlotWeaponPresenter.SecondarySlot &&
                    weaponSlots.BowIsEquipped,
                OnEnd = () =>
                    recorder.MarkLastFrame("sword-sheathed-on-back", true)
            });
            Vector3 previousRestingBowElbow = Vector3.zero;
            bool hasRestingBowElbow = false;
            float minimumRestingBowElbowSide =
                float.PositiveInfinity;
            float maximumRestingBowElbowSide =
                float.NegativeInfinity;
            float maximumRestingBowElbowStep = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-slot",
                FrameCount = 30,
                Screenshot = true,
                Intent = _ => default,
                OnStart = () =>
                {
                    previousRestingBowElbow = Vector3.zero;
                    hasRestingBowElbow = false;
                    minimumRestingBowElbowSide =
                        float.PositiveInfinity;
                    maximumRestingBowElbowSide =
                        float.NegativeInfinity;
                    maximumRestingBowElbowStep = 0f;
                },
                BeforeFrame = _ =>
                {
                    Vector3 localElbow =
                        motor.transform.InverseTransformPoint(
                            weaponSlots.PresentedRightElbowPosition);
                    minimumRestingBowElbowSide = Mathf.Min(
                        minimumRestingBowElbowSide,
                        localElbow.x);
                    maximumRestingBowElbowSide = Mathf.Max(
                        maximumRestingBowElbowSide,
                        localElbow.x);
                    if (hasRestingBowElbow)
                    {
                        maximumRestingBowElbowStep = Mathf.Max(
                            maximumRestingBowElbowStep,
                            Vector3.Distance(
                                localElbow,
                                previousRestingBowElbow));
                    }

                    previousRestingBowElbow = localElbow;
                    hasRestingBowElbow = true;
                },
                OnEnd = () =>
                {
                    if (!hasRestingBowElbow ||
                        minimumRestingBowElbowSide < 0.10f ||
                        maximumRestingBowElbowSide > 0.55f ||
                        maximumRestingBowElbowStep > 0.035f)
                    {
                        throw new InvalidOperationException(
                            "The undrawn bow elbow did not remain quietly " +
                            "beside the right torso: " +
                            $"side={minimumRestingBowElbowSide:0.000}-" +
                            $"{maximumRestingBowElbowSide:0.000}, " +
                            $"step={maximumRestingBowElbowStep:0.000} m/frame.");
                    }

                    RequireRigidBowGrip("resting bow");
                    recorder.MarkLastFrame(
                        "slot-two-bow-equipped",
                        true);
                }
            });
            int arrowsBeforeGrace = 0;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-grace-cancel",
                FrameCount = 18,
                Screenshot = true,
                OnStart = () =>
                    arrowsBeforeGrace = bowWeapon.FiredArrowCount,
                Intent = frame => Intent(
                    Vector2.zero,
                    attackHeld: frame < 6),
                OnEnd = () =>
                {
                    if (bowWeapon.FiredArrowCount != arrowsBeforeGrace)
                    {
                        throw new InvalidOperationException(
                            "A bow tap inside the grace period fired an arrow.");
                    }

                    recorder.MarkLastFrame("bow-grace-cancelled", true);
                }
            });
            EnqueueFixed(
                "combat",
                "bow-partial-draw",
                32,
                Intent(Vector2.zero, attackHeld: true),
                screenshot: true,
                onEnd: () =>
                {
                    if (aimPresenter.BowDrawTorsoYaw < 10f ||
                        aimPresenter.BowDrawTorsoYaw > 55f)
                    {
                        throw new InvalidOperationException(
                            "The partial bow draw did not progressively turn " +
                            "the torso into the archer stance: " +
                            $"yaw={aimPresenter.BowDrawTorsoYaw:0.0} degrees.");
                    }

                    RequireBowDrawHandOrientation(
                        "partial bow draw",
                        60f);
                    RequireRigidBowGrip("partial bow draw");
                    recorder.MarkLastFrame(
                        "bow-partially-drawn",
                        true);
                });
            int arrowsBeforePartialRelease = 0;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-partial-release",
                FrameCount = 45,
                Screenshot = true,
                OnStart = () =>
                    arrowsBeforePartialRelease =
                        bowWeapon.FiredArrowCount,
                Intent = _ => default,
                CompleteBeforeFrame = frame =>
                    frame > 2 &&
                    bowWeapon.FiredArrowCount >
                        arrowsBeforePartialRelease,
                OnEnd = () =>
                {
                    if (bowWeapon.LastShotSpeed >
                        bowWeapon.MaximumArrowSpeed * 0.35f)
                    {
                        throw new InvalidOperationException(
                            "The partial bow shot retained too much velocity: " +
                            $"speed={bowWeapon.LastShotSpeed:0.00}, " +
                            $"full={bowWeapon.MaximumArrowSpeed:0.00} m/s.");
                    }

                    recorder.MarkLastFrame(
                        "bow-partial-arrow-fired",
                        true);
                }
            });
            EnqueueFixed("combat", "bow-reload", 30, default);
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-full-draw",
                FrameCount = 120,
                Screenshot = true,
                Intent = _ =>
                    Intent(Vector2.zero, attackHeld: true),
                CompleteBeforeFrame = frame =>
                    frame > 2 &&
                    bowWeapon.DrawNormalized >= 0.99f,
                OnEnd = () =>
                {
                    if (bowWeapon.DrawNormalized < 0.99f)
                    {
                        throw new InvalidOperationException(
                            "The full bow draw did not reach the configured " +
                            "draw duration before the diagnostic timeout: " +
                            $"draw={bowWeapon.DrawNormalized:0.000}.");
                    }

                    if (aimPresenter.BowDrawTorsoYaw < 70f)
                    {
                        throw new InvalidOperationException(
                            "The full bow draw did not finish in the " +
                            "side-facing archer stance: " +
                            $"yaw={aimPresenter.BowDrawTorsoYaw:0.0} degrees.");
                    }

                    RequireBowDrawHandOrientation(
                        "full bow draw",
                        20f);
                    RequireRigidBowGrip("full bow draw");
                    recorder.MarkLastFrame(
                        "bow-fully-drawn",
                        true);
                }
            });
            Vector3 inspectionFacingStart = Vector3.forward;
            Vector3 inspectionAimStart = Vector3.forward;
            Vector3 inspectionCameraStart = Vector3.forward;
            Vector3 inspectionArrowStart = Vector3.forward;
            float maximumInspectionFacingDrift = 0f;
            float maximumInspectionAimDrift = 0f;
            float maximumInspectionArrowDrift = 0f;
            float maximumInspectionCameraOrbit = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "model-inspection-orbit",
                FrameCount = 45,
                Screenshot = true,
                OnStart = () =>
                {
                    inspectionFacingStart =
                        motor.transform.forward;
                    inspectionAimStart =
                        cameraAimTarget.AimDirection;
                    Camera activeCamera = Camera.main;
                    inspectionCameraStart =
                        activeCamera != null
                            ? activeCamera.transform.forward
                            : inspectionAimStart;
                    inspectionArrowStart =
                        bowWeapon.PresentedArrowDirection;
                    maximumInspectionFacingDrift = 0f;
                    maximumInspectionAimDrift = 0f;
                    maximumInspectionArrowDrift = 0f;
                    maximumInspectionCameraOrbit = 0f;
                    cameraAimTarget.SetInspectionDiagnosticOverride(
                        true,
                        new Vector2(1.4f, 0.35f));
                },
                Intent = _ => Intent(
                    Vector2.zero,
                    attackHeld: true),
                BeforeFrame = _ =>
                {
                    maximumInspectionFacingDrift = Mathf.Max(
                        maximumInspectionFacingDrift,
                        Vector3.Angle(
                            inspectionFacingStart,
                            motor.transform.forward));
                    maximumInspectionAimDrift = Mathf.Max(
                        maximumInspectionAimDrift,
                        Vector3.Angle(
                            inspectionAimStart,
                            cameraAimTarget.AimDirection));
                    maximumInspectionArrowDrift = Mathf.Max(
                        maximumInspectionArrowDrift,
                        Vector3.Angle(
                            inspectionArrowStart,
                            bowWeapon.PresentedArrowDirection));
                    Camera activeCamera = Camera.main;
                    if (activeCamera != null)
                    {
                        maximumInspectionCameraOrbit = Mathf.Max(
                            maximumInspectionCameraOrbit,
                            Vector3.Angle(
                                inspectionCameraStart,
                                activeCamera.transform.forward));
                    }
                },
                OnEnd = () =>
                {
                    bool orbitActive =
                        cameraAimTarget.InspectionOrbitActive;
                    cameraAimTarget
                        .ClearInspectionDiagnosticOverride();
                    if (!orbitActive ||
                        maximumInspectionCameraOrbit < 30f ||
                        maximumInspectionFacingDrift > 1f ||
                        maximumInspectionAimDrift > 0.1f ||
                        maximumInspectionArrowDrift > 2f)
                    {
                        throw new InvalidOperationException(
                            "The middle-mouse model inspection orbit did " +
                            "not preserve the model direction: " +
                            $"active={orbitActive}, " +
                            $"cameraOrbit={maximumInspectionCameraOrbit:0.00}, " +
                            $"facingDrift={maximumInspectionFacingDrift:0.00}, " +
                            $"aimDrift={maximumInspectionAimDrift:0.00}, " +
                            $"arrowDrift={maximumInspectionArrowDrift:0.00} degrees.");
                    }

                    recorder.MarkLastFrame(
                        "model-inspection-orbit-stable",
                        true);
                }
            });
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "model-inspection-restore",
                FrameCount = 30,
                Intent = _ => Intent(
                    Vector2.zero,
                    attackHeld: true),
                OnEnd = () =>
                {
                    Camera activeCamera = Camera.main;
                    float cameraRestoreError =
                        activeCamera != null
                            ? Vector3.Angle(
                                inspectionAimStart,
                                activeCamera.transform.forward)
                            : 180f;
                    float facingRestoreError =
                        Vector3.Angle(
                            Vector3.ProjectOnPlane(
                                inspectionAimStart,
                                Vector3.up),
                            motor.transform.forward);
                    if (cameraAimTarget.InspectionOrbitActive ||
                        cameraRestoreError > 2f ||
                        facingRestoreError > 6f)
                    {
                        throw new InvalidOperationException(
                            "The camera did not settle back onto the " +
                            "pre-inspection aim after releasing middle mouse: " +
                            $"active={cameraAimTarget.InspectionOrbitActive}, " +
                            $"cameraError={cameraRestoreError:0.00}, " +
                            $"facingError={facingRestoreError:0.00} degrees.");
                    }

                    recorder.MarkLastFrame(
                        "model-inspection-view-restored",
                        false);
                }
            });
            float maximumBowSweepFacingError = 0f;
            float minimumBowSweepElbowSide = float.PositiveInfinity;
            float minimumBowSweepHeadClearance = float.PositiveInfinity;
            Transform bowSweepHead = null;
            Transform bowSweepShoulder = null;
            Transform bowSweepElbow = null;
            Transform bowSweepHand = null;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-aim-yaw-sweep",
                FrameCount = 120,
                Screenshot = true,
                OnStart = () =>
                {
                    maximumBowSweepFacingError = 0f;
                    minimumBowSweepElbowSide =
                        float.PositiveInfinity;
                    minimumBowSweepHeadClearance =
                        float.PositiveInfinity;
                    Animator animator =
                        weaponSlots.GetComponent<Animator>();
                    bowSweepHead = animator.GetBoneTransform(
                        HumanBodyBones.Head);
                    bowSweepShoulder = animator.GetBoneTransform(
                        HumanBodyBones.RightUpperArm);
                    bowSweepElbow = animator.GetBoneTransform(
                        HumanBodyBones.RightLowerArm);
                    bowSweepHand = animator.GetBoneTransform(
                        HumanBodyBones.RightHand);
                },
                Intent = frame => Intent(
                    Vector2.zero,
                    attackHeld: true,
                    look: new Vector2(
                        frame < 60 ? 1.5f : -1.5f,
                        0f)),
                BeforeFrame = _ =>
                {
                    maximumBowSweepFacingError = Mathf.Max(
                        maximumBowSweepFacingError,
                        CameraFacingError());
                    minimumBowSweepElbowSide = Mathf.Min(
                        minimumBowSweepElbowSide,
                        Vector3.Dot(
                            bowSweepElbow.position -
                                bowSweepHead.position,
                            motor.transform.right));
                    minimumBowSweepHeadClearance = Mathf.Min(
                        minimumBowSweepHeadClearance,
                        Mathf.Min(
                            DistanceToSegment(
                                bowSweepHead.position,
                                bowSweepShoulder.position,
                                bowSweepElbow.position),
                            DistanceToSegment(
                                bowSweepHead.position,
                                bowSweepElbow.position,
                                bowSweepHand.position)));
                },
                OnEnd = () =>
                {
                    RequireAimFacing(
                        "bow yaw sweep",
                        maximumBowSweepFacingError);
                    if (minimumBowSweepElbowSide < 0.10f ||
                        minimumBowSweepHeadClearance < 0.09f)
                    {
                        throw new InvalidOperationException(
                            "The bow elbow crossed its stable outside bend " +
                            $"plane: side={minimumBowSweepElbowSide:0.000}, " +
                            $"headClearance={minimumBowSweepHeadClearance:0.000}.");
                    }

                    recorder.MarkLastFrame(
                        "bow-yaw-sweep-stable",
                        true);
                }
            });
            float minimumBowAimHeight = float.PositiveInfinity;
            float maximumBowAimHeight = float.NegativeInfinity;
            float maximumBowPitchAlignmentError = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-aim-pitch-sweep",
                FrameCount = 120,
                Screenshot = true,
                OnStart = () =>
                {
                    minimumBowAimHeight = float.PositiveInfinity;
                    maximumBowAimHeight = float.NegativeInfinity;
                    maximumBowPitchAlignmentError = 0f;
                },
                Intent = frame => Intent(
                    Vector2.zero,
                    attackHeld: true,
                    look: new Vector2(
                        0f,
                        frame < 30
                            ? 1.2f
                            : frame < 90
                                ? -1.2f
                                : 1.2f)),
                BeforeFrame = _ =>
                {
                    Vector3 aimDirection =
                        bowWeapon.CurrentAimDirection;
                    minimumBowAimHeight = Mathf.Min(
                        minimumBowAimHeight,
                        aimDirection.y);
                    maximumBowAimHeight = Mathf.Max(
                        maximumBowAimHeight,
                        aimDirection.y);
                    maximumBowPitchAlignmentError = Mathf.Max(
                        maximumBowPitchAlignmentError,
                        Vector3.Angle(
                            bowWeapon.PresentedArrowDirection,
                            aimDirection));
                },
                OnEnd = () =>
                {
                    if (minimumBowAimHeight > -0.05f ||
                        maximumBowAimHeight < 0.30f)
                    {
                        throw new InvalidOperationException(
                            "The bow pitch sweep did not cover both downward " +
                            $"and upward aim: minY={minimumBowAimHeight:0.000}, " +
                            $"maxY={maximumBowAimHeight:0.000}.");
                    }

                    if (maximumBowPitchAlignmentError > 2f)
                    {
                        throw new InvalidOperationException(
                            "The presented bow diverged from the camera aim " +
                            $"during pitch: maxError={maximumBowPitchAlignmentError:0.00} degrees.");
                    }

                    recorder.MarkLastFrame(
                        "bow-pitch-and-reticle-aligned",
                        true);
                }
            });
            float maximumBowStrafeFacingError = 0f;
            float maximumBowStrafeJitter = 0f;
            float maximumBowStrafePositionStep = 0f;
            float maximumBowStrafeLeftHandStep = 0f;
            float maximumBowStrafeRightHandStep = 0f;
            float maximumBowStrafeElbowStep = 0f;
            Vector3 previousBowStrafeDirection = Vector3.zero;
            Vector3 previousBowStrafePosition = Vector3.zero;
            Vector3 previousBowStrafeLeftHand = Vector3.zero;
            Vector3 previousBowStrafeRightHand = Vector3.zero;
            Vector3 previousBowStrafeElbow = Vector3.zero;
            Transform bowStrafeLeftHand = null;
            Transform bowStrafeRightHand = null;
            Transform bowStrafeElbow = null;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-aim-strafe",
                FrameCount = 60,
                Screenshot = true,
                OnStart = () =>
                {
                    maximumBowStrafeFacingError = 0f;
                    maximumBowStrafeJitter = 0f;
                    maximumBowStrafePositionStep = 0f;
                    maximumBowStrafeLeftHandStep = 0f;
                    maximumBowStrafeRightHandStep = 0f;
                    maximumBowStrafeElbowStep = 0f;
                    Animator animator =
                        weaponSlots.GetComponent<Animator>();
                    bowStrafeLeftHand = animator.GetBoneTransform(
                        HumanBodyBones.LeftHand);
                    bowStrafeRightHand = animator.GetBoneTransform(
                        HumanBodyBones.RightHand);
                    bowStrafeElbow = animator.GetBoneTransform(
                        HumanBodyBones.RightLowerArm);
                    previousBowStrafeDirection =
                        bowWeapon.PresentedArrowDirection;
                    previousBowStrafePosition =
                        motor.transform.InverseTransformPoint(
                            bowWeapon.PresentedBowPosition);
                    previousBowStrafeLeftHand =
                        motor.transform.InverseTransformPoint(
                            bowStrafeLeftHand.position);
                    previousBowStrafeRightHand =
                        motor.transform.InverseTransformPoint(
                            bowStrafeRightHand.position);
                    previousBowStrafeElbow =
                        motor.transform.InverseTransformPoint(
                            bowStrafeElbow.position);
                },
                Intent = _ => Intent(
                    Vector2.left,
                    sprint: true,
                    attackHeld: true),
                BeforeFrame = frame =>
                {
                    maximumBowStrafeFacingError = Mathf.Max(
                        maximumBowStrafeFacingError,
                        CameraFacingError());
                    Vector3 direction =
                        bowWeapon.PresentedArrowDirection;
                    Vector3 bowPosition =
                        motor.transform.InverseTransformPoint(
                            bowWeapon.PresentedBowPosition);
                    Vector3 leftHandPosition =
                        motor.transform.InverseTransformPoint(
                            bowStrafeLeftHand.position);
                    Vector3 rightHandPosition =
                        motor.transform.InverseTransformPoint(
                            bowStrafeRightHand.position);
                    Vector3 elbowPosition =
                        motor.transform.InverseTransformPoint(
                            bowStrafeElbow.position);
                    if (frame >= 10)
                    {
                        maximumBowStrafeJitter = Mathf.Max(
                            maximumBowStrafeJitter,
                            Vector3.Angle(
                                previousBowStrafeDirection,
                                direction));
                        maximumBowStrafePositionStep = Mathf.Max(
                            maximumBowStrafePositionStep,
                            Vector3.Distance(
                                previousBowStrafePosition,
                                bowPosition));
                        maximumBowStrafeLeftHandStep = Mathf.Max(
                            maximumBowStrafeLeftHandStep,
                            Vector3.Distance(
                                previousBowStrafeLeftHand,
                                leftHandPosition));
                        maximumBowStrafeRightHandStep = Mathf.Max(
                            maximumBowStrafeRightHandStep,
                            Vector3.Distance(
                                previousBowStrafeRightHand,
                                rightHandPosition));
                        maximumBowStrafeElbowStep = Mathf.Max(
                            maximumBowStrafeElbowStep,
                            Vector3.Distance(
                                previousBowStrafeElbow,
                                elbowPosition));
                    }
                    previousBowStrafeDirection = direction;
                    previousBowStrafePosition = bowPosition;
                    previousBowStrafeLeftHand = leftHandPosition;
                    previousBowStrafeRightHand = rightHandPosition;
                    previousBowStrafeElbow = elbowPosition;
                },
                OnEnd = () =>
                {
                    RequireAimFacing(
                        "bow strafe",
                        maximumBowStrafeFacingError);
                    RequireAimWalkSpeed("bow strafe");
                    if (maximumBowStrafeJitter > 1f)
                    {
                        throw new InvalidOperationException(
                            "The bow aim visibly stepped while strafing: " +
                            $"maxFrameDelta={maximumBowStrafeJitter:0.00} degrees.");
                    }

                    if (maximumBowStrafePositionStep > 0.015f ||
                        maximumBowStrafeLeftHandStep > 0.035f ||
                        maximumBowStrafeRightHandStep > 0.035f ||
                        maximumBowStrafeElbowStep > 0.045f)
                    {
                        throw new InvalidOperationException(
                            "The aimed bow rig stepped while strafing: " +
                            $"bow={maximumBowStrafePositionStep:0.000}, " +
                            $"leftHand={maximumBowStrafeLeftHandStep:0.000}, " +
                            $"rightHand={maximumBowStrafeRightHandStep:0.000}, " +
                            $"elbow={maximumBowStrafeElbowStep:0.000} m/frame.");
                    }

                    RequireBowWalk(
                        "bow reverse strafe",
                        65f,
                        85f,
                        -1f);
                    RequireRigidBowGrip("moving bow aim");
                    recorder.MarkLastFrame(
                        "bow-aim-archer-stance-held",
                        true);
                }
            });
            float maximumBowBackpedalFacingError = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-aim-backpedal",
                FrameCount = 60,
                Screenshot = true,
                OnStart = () =>
                    maximumBowBackpedalFacingError = 0f,
                Intent = _ => Intent(
                    Vector2.down,
                    attackHeld: true),
                BeforeFrame = _ =>
                    maximumBowBackpedalFacingError = Mathf.Max(
                        maximumBowBackpedalFacingError,
                        CameraFacingError()),
                OnEnd = () =>
                {
                    RequireAimFacing(
                        "bow backpedal",
                        maximumBowBackpedalFacingError);
                    RequireBowWalk(
                        "bow backpedal",
                        35f,
                        55f,
                        -1f);
                }
            });
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-aim-forward-walk",
                FrameCount = 60,
                Screenshot = true,
                Intent = _ => Intent(
                    Vector2.up,
                    attackHeld: true),
                OnEnd = () =>
                {
                    RequireBowWalk(
                        "bow forward walk",
                        35f,
                        55f,
                        1f);
                    recorder.MarkLastFrame(
                        "bow-forward-authored-walk-held",
                        true);
                }
            });
            int arrowsBeforeFullRelease = 0;
            int stuckArrowsBeforeFullRelease = 0;
            int swordHitSoundsBeforeFullRelease = 0;
            float enemyHealthBeforeFullRelease = 0f;
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-full-release",
                FrameCount = 45,
                Screenshot = true,
                OnStart = () =>
                {
                    arrowsBeforeFullRelease =
                        bowWeapon.FiredArrowCount;
                    stuckArrowsBeforeFullRelease =
                        CountStuckArrows();
                    swordHitSoundsBeforeFullRelease =
                        enemyHitReaction != null
                            ? enemyHitReaction.HitSoundPlayCount
                            : 0;
                    enemyHealthBeforeFullRelease =
                        enemyHealth.Current;
                },
                Intent = _ => default,
                CompleteBeforeFrame = frame =>
                    frame > 2 &&
                    bowWeapon.FiredArrowCount >
                        arrowsBeforeFullRelease,
                OnEnd = () =>
                {
                    float zeroGravityError =
                        Vector3.ProjectOnPlane(
                            bowWeapon.LastZeroGravityImpactPoint -
                                bowWeapon.LastCrosshairPoint,
                            bowWeapon.LastAimDirection).magnitude;
                    if (zeroGravityError > 0.015f)
                    {
                        throw new InvalidOperationException(
                            "The predicted zero-gravity arrow impact did not " +
                            $"match the crosshair: error={zeroGravityError:0.000} m.");
                    }
                    if (bowWeapon.LastShotSpeed <
                        bowWeapon.MaximumArrowSpeed * 0.98f)
                    {
                        throw new InvalidOperationException(
                            "The full bow shot lost its accepted velocity: " +
                            $"speed={bowWeapon.LastShotSpeed:0.00}, " +
                            $"expected={bowWeapon.MaximumArrowSpeed:0.00} m/s.");
                    }

                    recorder.MarkLastFrame(
                        "bow-full-arrow-fired-crosshair-solved",
                        true);
                }
            });
            EnqueueStep(new DiagnosticStep
            {
                Scenario = "combat",
                Phase = "bow-flight-impact",
                FrameCount = 180,
                Screenshot = true,
                Intent = _ => default,
                CompleteBeforeFrame = frame =>
                    frame > 2 &&
                    CountStuckArrows() >
                        stuckArrowsBeforeFullRelease &&
                    enemyHealth.Current <
                        enemyHealthBeforeFullRelease,
                OnEnd = () =>
                {
                    BowArrowProjectile arrow =
                        bowWeapon.LastFiredProjectile;
                    if (arrow == null || !arrow.IsStuck)
                    {
                        throw new InvalidOperationException(
                            "The full-draw arrow did not remain available for " +
                            "crosshair accuracy validation.");
                    }

                    float stuckAngleError = Vector3.Angle(
                        arrow.ImpactDirection,
                        arrow.transform.forward);
                    float stuckScaleError = Vector3.Distance(
                        arrow.LaunchWorldScale,
                        arrow.transform.lossyScale);
                    Vector3 rootToSurface =
                        arrow.HitPoint -
                        arrow.transform.position;
                    float surfaceDepth = Vector3.Dot(
                        rootToSurface,
                        arrow.transform.forward);
                    float surfaceLateralError =
                        Vector3.ProjectOnPlane(
                            rootToSurface,
                            arrow.transform.forward).magnitude;
                    if (stuckAngleError > 0.1f ||
                        stuckScaleError > 0.0001f ||
                        Mathf.Abs(
                            surfaceDepth -
                            arrow.SurfaceIntersectionDistance) > 0.005f ||
                        surfaceLateralError > 0.005f)
                    {
                        throw new InvalidOperationException(
                            "The stuck arrow did not preserve its incoming " +
                            "transform and embedded arrowhead placement: " +
                            $"angle={stuckAngleError:0.000} degrees, " +
                            $"scale={stuckScaleError:0.00000}, " +
                            $"depth={surfaceDepth:0.000}, " +
                            $"expected={arrow.SurfaceIntersectionDistance:0.000}, " +
                            $"lateral={surfaceLateralError:0.000} m.");
                    }

                    Vector3 toImpact =
                        arrow.HitPoint - bowWeapon.LastAimOrigin;
                    float alongAim = Vector3.Dot(
                        toImpact,
                        bowWeapon.LastAimDirection);
                    Vector3 expectedPoint =
                        bowWeapon.LastAimOrigin +
                        bowWeapon.LastAimDirection * alongAim;
                    Vector3 miss = arrow.HitPoint - expectedPoint;
                    float lateralMiss = Mathf.Abs(Vector3.Dot(
                        miss,
                        bowWeapon.LastAimRight));
                    float verticalMiss = Vector3.Dot(
                        miss,
                        Vector3.up);
                    if (lateralMiss > 0.08f ||
                        verticalMiss < -0.32f)
                    {
                        throw new InvalidOperationException(
                            "The full-draw arrow diverged too far from the " +
                            $"crosshair ray: lateral={lateralMiss:0.000}, " +
                            $"vertical={verticalMiss:0.000} m.");
                    }
                    if (enemyHitReaction != null &&
                        enemyHitReaction.HitSoundPlayCount !=
                            swordHitSoundsBeforeFullRelease)
                    {
                        throw new InvalidOperationException(
                            "Bow damage incorrectly triggered the sword-hit audio.");
                    }

                    recorder.MarkLastFrame(
                        "bow-arrow-stuck-and-accurate",
                        true);
                }
            });
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
            bool blockHeld = false,
            bool attackHeld = false,
            Vector2 look = default)
        {
            return new PlayerIntent(
                move,
                look,
                sprint,
                jumpPressed,
                jumpHeld,
                crouch,
                attackPressed,
                blockHeld,
                attackHeld);
        }

        private static int CountStuckArrows()
        {
            BowArrowProjectile[] arrows =
                FindObjectsByType<BowArrowProjectile>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            int count = 0;
            for (int index = 0; index < arrows.Length; index++)
            {
                if (arrows[index].IsStuck)
                {
                    count++;
                }
            }

            return count;
        }

        private float CameraFacingError()
        {
            Camera camera = Camera.main;
            if (camera == null || motor == null)
            {
                return 180f;
            }

            Vector3 cameraForward = Vector3.ProjectOnPlane(
                camera.transform.forward,
                Vector3.up);
            return cameraForward.sqrMagnitude > 0.001f
                ? Vector3.Angle(
                    motor.transform.forward,
                    cameraForward)
                : 180f;
        }

        private static void RequireAimFacing(
            string label,
            float maximumError)
        {
            if (maximumError > 3f)
            {
                throw new InvalidOperationException(
                    $"{label} exceeded the aim-facing lock: " +
                    $"{maximumError:0.00} degrees.");
            }
        }

        private void RequireBowDrawHandOrientation(
            string label,
            float maximumPalmError)
        {
            Vector3 palmDirection =
                weaponSlots != null
                    ? weaponSlots.PresentedDrawPalmDirection
                    : Vector3.zero;
            Vector3 fingerDirection =
                weaponSlots != null
                    ? weaponSlots.PresentedDrawFingerDirection
                    : Vector3.zero;
            float palmError =
                motor != null &&
                palmDirection.sqrMagnitude > 0.5f
                    ? Vector3.Angle(
                        palmDirection,
                        -motor.transform.right)
                    : 180f;
            float fingerError =
                motor != null &&
                fingerDirection.sqrMagnitude > 0.5f
                    ? Vector3.Angle(
                        fingerDirection,
                        -motor.transform.up)
                    : 180f;
            float wristDeviation =
                weaponSlots != null
                    ? weaponSlots.PresentedDrawWristDeviation
                    : 180f;
            if (palmError > maximumPalmError ||
                wristDeviation > 1f)
            {
                throw new InvalidOperationException(
                    $"{label} did not retain the neutral palm-left " +
                    "drawing-hand frame: " +
                    $"palmError={palmError:0.00}, " +
                    $"fingerError={fingerError:0.00}, " +
                    $"wrist={wristDeviation:0.00} degrees.");
            }
        }

        private void RequireRigidBowGrip(string label)
        {
            float wristDeviation =
                weaponSlots != null
                    ? weaponSlots.PresentedBowWristDeviation
                    : 180f;
            float gripPositionDeviation =
                weaponSlots != null
                    ? weaponSlots.PresentedBowGripPositionDeviation
                    : 99f;
            float gripRotationDeviation =
                weaponSlots != null
                    ? weaponSlots.PresentedBowGripRotationDeviation
                    : 180f;
            if (wristDeviation > 1f ||
                gripPositionDeviation > 0.0001f ||
                gripRotationDeviation > 0.1f)
            {
                throw new InvalidOperationException(
                    $"{label} did not retain the rigid neutral-wrist " +
                    "bow grip: " +
                    $"wrist={wristDeviation:0.00} degrees, " +
                    $"position={gripPositionDeviation:0.00000} m, " +
                    $"rotation={gripRotationDeviation:0.00} degrees.");
            }
        }

        private void RequireSwordGuardWalk(
            string label,
            float expectedPlayback)
        {
            if (stancePresenter == null ||
                Mathf.Abs(
                    stancePresenter.CurrentStanceYaw) > 5f ||
                stancePresenter.GaitPlaybackDirection *
                    expectedPlayback < 0.8f)
            {
                throw new InvalidOperationException(
                    $"{label} did not keep aim-forward authored walking: " +
                    $"yaw={(stancePresenter != null ? stancePresenter.CurrentStanceYaw : -1f):0.0}, " +
                    $"playback={(stancePresenter != null ? stancePresenter.GaitPlaybackDirection : 0f):0.00}.");
            }
        }

        private void RequireBowWalk(
            string label,
            float minimumYaw,
            float maximumYaw,
            float expectedPlayback)
        {
            if (stancePresenter == null ||
                stancePresenter.BowStanceWeight < 0.85f ||
                !stancePresenter.UsesAuthoredWalk ||
                stancePresenter.CurrentStanceYaw <
                    minimumYaw ||
                stancePresenter.CurrentStanceYaw >
                    maximumYaw ||
                stancePresenter.GaitPlaybackDirection *
                    expectedPlayback < 0.8f)
            {
                throw new InvalidOperationException(
                    $"{label} did not use the directional bow walk: " +
                    $"weight={(stancePresenter != null ? stancePresenter.BowStanceWeight : -1f):0.000}, " +
                    $"yaw={(stancePresenter != null ? stancePresenter.CurrentStanceYaw : -1f):0.0}, " +
                    $"playback={(stancePresenter != null ? stancePresenter.GaitPlaybackDirection : 0f):0.00}.");
            }
        }

        private void RequireAimWalkSpeed(string label)
        {
            if (motor.TargetHorizontalSpeed >
                    motor.WalkSpeed + 0.01f ||
                motor.HorizontalSpeed >
                    motor.WalkSpeed + 0.05f)
            {
                throw new InvalidOperationException(
                    $"{label} accepted sprint speed while aim-locked: " +
                    $"target={motor.TargetHorizontalSpeed:0.00}, " +
                    $"actual={motor.HorizontalSpeed:0.00}, " +
                    $"walk={motor.WalkSpeed:0.00} m/s.");
            }
        }

        private static float DistanceToSegment(
            Vector3 point,
            Vector3 start,
            Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return Vector3.Distance(point, start);
            }

            float progress = Mathf.Clamp01(
                Vector3.Dot(
                    point - start,
                    segment) /
                lengthSquared);
            return Vector3.Distance(
                point,
                start + segment * progress);
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
