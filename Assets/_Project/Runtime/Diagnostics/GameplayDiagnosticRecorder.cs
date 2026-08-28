using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Diagnostics
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class GameplayDiagnosticRecorder : MonoBehaviour
    {
        private const Key ManualCaptureKey = Key.F9;
        private const Key ManualMarkerKey = Key.F10;
        private const float MissingGroundGap = 99f;

        private readonly List<GameplayDiagnosticFrame> frames = new List<GameplayDiagnosticFrame>(4096);
        private readonly List<GameplayDiagnosticEvent> events = new List<GameplayDiagnosticEvent>(256);
        private readonly List<GameplayDiagnosticMarker> markers = new List<GameplayDiagnosticMarker>(64);
        private readonly List<PendingMarker> pendingMarkers = new List<PendingMarker>(8);
        private readonly Dictionary<string, PhaseStartSnapshot> phaseStartSnapshots =
            new Dictionary<string, PhaseStartSnapshot>(64);

        [SerializeField] private bool allowManualHotkeys = true;
        [SerializeField] private bool captureMarkerScreenshots = true;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private Animator animator;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Health enemyHealth;
        [SerializeField] private EnemyBrain enemyBrain;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private ShortSwordBlockPresenter blockPresenter;

        private Transform player;
        private Transform enemy;
        private Transform hips;
        private Transform spine;
        private Transform chest;
        private Transform head;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform leftKnee;
        private Transform rightKnee;
        private Transform leftToe;
        private Transform rightToe;
        private Transform leftElbow;
        private Transform rightElbow;
        private Transform leftHand;
        private Transform rightHand;
        private Vector3 previousLeftFootWorld;
        private Vector3 previousRightFootWorld;
        private Vector3 previousLeftHandLocal;
        private Quaternion previousHeadRotation;
        private bool hasPreviousPose;
        private bool recording;
        private bool ownsManualCapture;
        private float captureStartedAt;
        private float previousWallCaptureTime;
        private float deterministicDeltaTime;
        private bool deterministicClock;
        private int lastCapturedUnityFrame = -1;
        private float lastCapturedTime;
        private bool captureCompleted;
        private string captureAbortReason;
        private string sourceRevision = "unavailable";
        private readonly List<Vector3> leftHeelProbeSamples = new List<Vector3>(24);
        private readonly List<Vector3> rightHeelProbeSamples = new List<Vector3>(24);
        private readonly List<Vector3> leftToeProbeSamples = new List<Vector3>(24);
        private readonly List<Vector3> rightToeProbeSamples = new List<Vector3>(24);
        private readonly List<float> standingPelvisGapSamples = new List<float>(24);
        private readonly List<float> standingLeftAnkleGapSamples = new List<float>(24);
        private readonly List<float> standingRightAnkleGapSamples = new List<float>(24);
        private Vector3 leftHeelProbeLocal;
        private Vector3 rightHeelProbeLocal;
        private Vector3 leftToeProbeLocal;
        private Vector3 rightToeProbeLocal;
        private float standingPelvisGap;
        private float standingLeftAnkleGap;
        private float standingRightAnkleGap;
        private bool soleCalibrationValid;
        private AnimatorCullingMode previousAnimatorCullingMode;
        private bool animatorCullingOverridden;
        private float observedPlayerHealth;
        private float observedEnemyHealth;
        private long nextEventSequence;
        private int manualMarkerIndex;
        private string runId;
        private string runKind;
        private string outputDirectory;
        private string currentScenario = "freeplay";
        private string currentPhase = "unlabeled";

        public static event Action<GameplayDiagnosticCompletion> CaptureCompleted;

        public static string ArtifactRoot => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Artifacts",
            "CombatLabDiagnostics");

        public bool IsRecording => recording;
        public string CurrentScenario => currentScenario;
        public string CurrentPhase => currentPhase;
        public string OutputDirectory => outputDirectory;
        public int SampleCount => frames.Count;

        private readonly struct PendingMarker
        {
            public PendingMarker(string scenario, string phase, string name, bool screenshot)
            {
                Scenario = scenario;
                Phase = phase;
                Name = name;
                Screenshot = screenshot;
            }

            public string Scenario { get; }
            public string Phase { get; }
            public string Name { get; }
            public bool Screenshot { get; }
        }

        private readonly struct ContactSlipResult
        {
            public ContactSlipResult(float slip, int samples, float rate)
            {
                Slip = slip;
                Samples = samples;
                Rate = rate;
            }

            public float Slip { get; }
            public int Samples { get; }
            public float Rate { get; }
        }

        private readonly struct PhaseStartSnapshot
        {
            public PhaseStartSnapshot(float playerHealth, float enemyHealth, Vector3 enemyPosition)
            {
                PlayerHealth = playerHealth;
                EnemyHealth = enemyHealth;
                EnemyPosition = enemyPosition;
            }

            public float PlayerHealth { get; }
            public float EnemyHealth { get; }
            public Vector3 EnemyPosition { get; }
        }

        public void Configure(
            ThirdPersonMotor movementMotor,
            PlayerInputSource intentSource,
            Animator targetAnimator,
            Health owningHealth,
            Health targetHealth,
            EnemyBrain targetBrain,
            Camera targetCamera)
        {
            motor = movementMotor;
            input = intentSource;
            animator = targetAnimator;
            playerHealth = owningHealth;
            enemyHealth = targetHealth;
            enemyBrain = targetBrain;
            gameplayCamera = targetCamera;
            blockPresenter = targetAnimator != null
                ? targetAnimator.GetComponent<ShortSwordBlockPresenter>()
                : null;
            ResolveSceneReferences();
        }

        public void SetSourceRevision(string revision)
        {
            sourceRevision = string.IsNullOrWhiteSpace(revision) ? "unavailable" : revision.Trim();
        }

        private void Awake()
        {
            ResolveSceneReferences();
        }

        private void Update()
        {
            if (!allowManualHotkeys || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[ManualCaptureKey].wasPressedThisFrame)
            {
                if (recording && ownsManualCapture)
                {
                    CompleteCapture();
                }
                else if (!recording)
                {
                    ownsManualCapture = true;
                    BeginCapture("manual-freeplay");
                }
            }

            if (recording && Keyboard.current[ManualMarkerKey].wasPressedThisFrame)
            {
                manualMarkerIndex++;
                Mark($"manual-{manualMarkerIndex:00}", true);
            }
        }

        private void LateUpdate()
        {
            if (recording)
            {
                CaptureFrame();
                FlushPendingMarkers();
            }
        }

        private void OnGUI()
        {
            if (!recording)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = new Color(0.9f, 0.18f, 0.12f, 0.95f);
            GUI.Box(new Rect(Screen.width - 318f, 16f, 302f, 58f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(Screen.width - 305f, 23f, 280f, 44f),
                $"● DIAGNOSTIC RECORDING  {frames.Count} frames\n{currentScenario} / {currentPhase}   [F9 stop, F10 mark]");
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            RestoreAnimatorCullingMode();
        }

        private void OnDisable()
        {
            if (!recording || !ownsManualCapture)
            {
                return;
            }

            try
            {
                CompleteCapture(
                    completed: false,
                    abortReason: "Play mode or the recorder stopped before F9 ended the manual capture.",
                    captureCurrentFrame: false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void BeginCapture(string captureKind, string explicitOutputDirectory = null)
        {
            if (recording)
            {
                throw new InvalidOperationException("A gameplay diagnostic capture is already running.");
            }

            ResolveSceneReferences();
            if (animator != null)
            {
                previousAnimatorCullingMode = animator.cullingMode;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animatorCullingOverridden = true;
            }

            frames.Clear();
            events.Clear();
            markers.Clear();
            pendingMarkers.Clear();
            phaseStartSnapshots.Clear();
            leftHeelProbeSamples.Clear();
            rightHeelProbeSamples.Clear();
            leftToeProbeSamples.Clear();
            rightToeProbeSamples.Clear();
            standingPelvisGapSamples.Clear();
            standingLeftAnkleGapSamples.Clear();
            standingRightAnkleGapSamples.Clear();
            soleCalibrationValid = false;
            nextEventSequence = 1;
            manualMarkerIndex = 0;
            hasPreviousPose = false;
            lastCapturedUnityFrame = -1;
            lastCapturedTime = 0f;
            captureCompleted = false;
            captureAbortReason = string.Empty;
            runKind = string.IsNullOrWhiteSpace(captureKind) ? "unspecified" : captureKind;
            runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                "-" + Sanitize(runKind);
            outputDirectory = string.IsNullOrWhiteSpace(explicitOutputDirectory)
                ? Path.Combine(ArtifactRoot, "runs", runId)
                : Path.GetFullPath(explicitOutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(Path.Combine(outputDirectory, "screenshots"));
            deterministicClock = string.Equals(runKind, "deterministic-full-suite", StringComparison.Ordinal);
            deterministicDeltaTime = deterministicClock
                ? Mathf.Max(0.0001f, Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.fixedDeltaTime)
                : 0f;
            captureStartedAt = Time.realtimeSinceStartup;
            previousWallCaptureTime = captureStartedAt;
            observedPlayerHealth = playerHealth != null ? playerHealth.Current : 0f;
            observedEnemyHealth = enemyHealth != null ? enemyHealth.Current : 0f;
            currentScenario = runKind == "manual-freeplay" ? "freeplay" : "suite";
            currentPhase = "capture-start";
            GameplayEventLog.Clear();
            Subscribe();
            recording = true;
            Mark("capture-start", true);
        }

        public void BeginPhase(string scenario, string phase, bool screenshot = false)
        {
            currentScenario = string.IsNullOrWhiteSpace(scenario) ? "unnamed" : scenario;
            currentPhase = string.IsNullOrWhiteSpace(phase) ? "unnamed" : phase;
            phaseStartSnapshots[PhaseKey(currentScenario, currentPhase)] = new PhaseStartSnapshot(
                playerHealth != null ? playerHealth.Current : 0f,
                enemyHealth != null ? enemyHealth.Current : 0f,
                enemy != null ? enemy.position : Vector3.zero);
            Mark("phase-start", screenshot);
        }

        public void ResetContinuity()
        {
            hasPreviousPose = false;
            previousLeftFootWorld = Vector3.zero;
            previousRightFootWorld = Vector3.zero;
            previousLeftHandLocal = Vector3.zero;
            previousHeadRotation = Quaternion.identity;
        }

        public void Mark(string markerName, bool screenshot = false)
        {
            if (!recording)
            {
                return;
            }

            PendingMarker marker = new PendingMarker(currentScenario, currentPhase, markerName, screenshot);
            if (lastCapturedUnityFrame == Time.frameCount)
            {
                CommitMarker(marker, lastCapturedTime);
                return;
            }

            pendingMarkers.Add(marker);
        }

        public void MarkLastFrame(string markerName, bool screenshot = false)
        {
            if (!recording)
            {
                return;
            }

            PendingMarker marker = new PendingMarker(currentScenario, currentPhase, markerName, screenshot);
            CommitMarker(marker, frames.Count > 0 ? lastCapturedTime : RelativeTime);
        }

        private void CommitMarker(in PendingMarker marker, float markerTime)
        {
            string screenshotPath = string.Empty;
            if (marker.Screenshot && captureMarkerScreenshots)
            {
                string fileName = $"{markers.Count:000}-{Sanitize(marker.Scenario)}-{Sanitize(marker.Phase)}-" +
                    $"{Sanitize(marker.Name)}.bmp";
                string candidate = Path.Combine("screenshots", fileName).Replace('\\', '/');
                if (TryCaptureScreenshot(Path.Combine(outputDirectory, candidate)))
                {
                    screenshotPath = candidate;
                }
            }

            markers.Add(new GameplayDiagnosticMarker
            {
                sample = Mathf.Max(0, frames.Count - 1),
                unityFrame = frames.Count > 0 ? lastCapturedUnityFrame : Time.frameCount,
                time = markerTime,
                scenario = marker.Scenario,
                phase = marker.Phase,
                name = marker.Name,
                screenshot = screenshotPath
            });
        }

        private void FlushPendingMarkers()
        {
            if (pendingMarkers.Count == 0)
            {
                return;
            }

            float markerTime = lastCapturedUnityFrame == Time.frameCount ? lastCapturedTime : RelativeTime;
            foreach (PendingMarker marker in pendingMarkers)
            {
                CommitMarker(marker, markerTime);
            }

            pendingMarkers.Clear();
        }

        public GameplayDiagnosticCompletion CompleteCapture(
            bool completed = true,
            string abortReason = "",
            bool captureCurrentFrame = true)
        {
            if (!recording)
            {
                return default;
            }

            captureCompleted = completed;
            captureAbortReason = abortReason ?? string.Empty;
            if (captureCurrentFrame)
            {
                Mark("capture-end", true);
                if (lastCapturedUnityFrame != Time.frameCount)
                {
                    CaptureFrame();
                }

                FlushPendingMarkers();
            }
            else
            {
                CommitMarker(
                    new PendingMarker(currentScenario, currentPhase, "capture-end", true),
                    frames.Count > 0 ? lastCapturedTime : RelativeTime);
            }
            recording = false;
            ownsManualCapture = false;
            Unsubscribe();

            GameplayDiagnosticReport report;
            try
            {
                report = BuildReport();
                WriteArtifacts(report);
            }
            finally
            {
                RestoreAnimatorCullingMode();
            }

            GameplayDiagnosticCompletion completion = new GameplayDiagnosticCompletion(outputDirectory, report);
            CaptureCompleted?.Invoke(completion);
            return completion;
        }

        private void RestoreAnimatorCullingMode()
        {
            if (animatorCullingOverridden && animator != null)
            {
                animator.cullingMode = previousAnimatorCullingMode;
            }

            animatorCullingOverridden = false;
        }

        private float RelativeTime => deterministicClock
            ? frames.Count * deterministicDeltaTime
            : Mathf.Max(0f, Time.realtimeSinceStartup - captureStartedAt);

        private void ResolveSceneReferences()
        {
            if (motor == null)
            {
                GameObject taggedPlayer =
                    GameObject.FindGameObjectWithTag("Player");
                motor = taggedPlayer != null
                    ? taggedPlayer.GetComponent<ThirdPersonMotor>()
                    : null;
                motor ??= FindFirstObjectByType<ThirdPersonMotor>();
            }

            player = motor != null ? motor.transform : null;
            if (input == null && player != null)
            {
                input = player.GetComponent<PlayerInputSource>();
            }

            if (animator == null && player != null)
            {
                animator = player.GetComponentInChildren<Animator>(true);
            }

            if (blockPresenter == null && animator != null)
            {
                blockPresenter = animator.GetComponent<ShortSwordBlockPresenter>();
            }

            if (playerHealth == null && player != null)
            {
                playerHealth = player.GetComponent<Health>();
            }

            if (enemyBrain == null)
            {
                enemyBrain = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).FirstOrDefault();
            }

            enemy = enemyBrain != null ? enemyBrain.transform : null;
            if (enemyHealth == null && enemy != null)
            {
                enemyHealth = enemy.GetComponent<Health>();
            }

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            CacheBones();
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                animator.GetBoneTransform(HumanBodyBones.Chest);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder) ??
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder) ??
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void Subscribe()
        {
            GameplayEventLog.Published -= OnGameplayEvent;
            GameplayEventLog.Published += OnGameplayEvent;
            DamageService.Resolved -= OnDamageResolved;
            DamageService.Resolved += OnDamageResolved;
        }

        private void Unsubscribe()
        {
            GameplayEventLog.Published -= OnGameplayEvent;
            DamageService.Resolved -= OnDamageResolved;
        }

        private void OnGameplayEvent(GameplayEventRecord record)
        {
            AddEvent(new GameplayDiagnosticEvent
            {
                unityFrame = record.Frame,
                time = RelativeTime,
                kind = "gameplay-" + record.Category,
                source = record.SourceId,
                detail = record.Detail
            });
        }

        private void OnDamageResolved(GameObject target, DamageRequest request)
        {
            Health resolvedHealth = target != null ? target.GetComponentInParent<Health>() : null;
            string targetRole = RoleOf(resolvedHealth);
            float before = targetRole == "player" ? observedPlayerHealth :
                targetRole == "dummy" ? observedEnemyHealth : resolvedHealth != null ? resolvedHealth.Current : 0f;
            float after = resolvedHealth != null ? resolvedHealth.Current : before;
            float effective = Mathf.Max(0f, before - after);
            if (targetRole == "player")
            {
                observedPlayerHealth = after;
            }
            else if (targetRole == "dummy")
            {
                observedEnemyHealth = after;
            }

            AddEvent(new GameplayDiagnosticEvent
            {
                unityFrame = Time.frameCount,
                time = RelativeTime,
                kind = "damage-resolved",
                source = RoleOf(request.Instigator),
                target = targetRole,
                detail = request.SourceId,
                requestedDamage = request.Amount,
                effectiveDamage = effective,
                overkillDamage = Mathf.Max(0f, request.Amount - effective),
                position = request.HitPoint,
                direction = request.Direction
            });
        }

        private void AddEvent(GameplayDiagnosticEvent diagnosticEvent)
        {
            diagnosticEvent.sequence = nextEventSequence++;
            diagnosticEvent.sample = frames.Count;
            diagnosticEvent.scenario = currentScenario;
            diagnosticEvent.phase = currentPhase;
            events.Add(diagnosticEvent);
        }

        private void CaptureFrame()
        {
            ResolveSceneReferences();
            float wallNow = Time.realtimeSinceStartup;
            float wallTime = Mathf.Max(0f, wallNow - captureStartedAt);
            float wallDeltaTime = frames.Count > 0 ? Mathf.Max(0f, wallNow - previousWallCaptureTime) : 0f;
            float sampleTime = deterministicClock ? frames.Count * deterministicDeltaTime : wallTime;
            float sampleDeltaTime = deterministicClock ? deterministicDeltaTime : wallDeltaTime;
            PlayerIntent intent = input != null ? input.CurrentIntent : default;
            Vector3 playerPosition = player != null ? player.position : Vector3.zero;
            Vector3 leftWorld = leftFoot != null ? leftFoot.position : Vector3.zero;
            Vector3 rightWorld = rightFoot != null ? rightFoot.position : Vector3.zero;
            Vector3 leftLocal = LocalPoint(leftFoot);
            Vector3 rightLocal = LocalPoint(rightFoot);
            bool leftIsRear = leftLocal.z <= rightLocal.z;
            Transform rearFoot = leftIsRear ? leftFoot : rightFoot;
            Transform rearToe = leftIsRear ? leftToe : rightToe;
            Transform frontFoot = leftIsRear ? rightFoot : leftFoot;
            Transform frontToe = leftIsRear ? rightToe : leftToe;
            Transform rearKnee = leftIsRear ? leftKnee : rightKnee;
            Transform rearHip = leftIsRear ? leftUpperLeg : rightUpperLeg;
            TryUpdateSoleCalibration();
            float leftHeelProbeGap = soleCalibrationValid
                ? ProbeGroundGap(leftFoot, leftHeelProbeLocal)
                : MissingGroundGap;
            float rightHeelProbeGap = soleCalibrationValid
                ? ProbeGroundGap(rightFoot, rightHeelProbeLocal)
                : MissingGroundGap;
            float leftToeProbeGap = soleCalibrationValid && leftToe != null && leftToeProbeSamples.Count >= 10
                ? ProbeGroundGap(leftToe, leftToeProbeLocal)
                : MissingGroundGap;
            float rightToeProbeGap = soleCalibrationValid && rightToe != null && rightToeProbeSamples.Count >= 10
                ? ProbeGroundGap(rightToe, rightToeProbeLocal)
                : MissingGroundGap;
            float leftKneeSurfaceGap = EstimatedKneeSurfaceGap(leftKnee, leftFoot, standingLeftAnkleGap);
            float rightKneeSurfaceGap = EstimatedKneeSurfaceGap(rightKnee, rightFoot, standingRightAnkleGap);
            float frontHeelProbeGap = leftIsRear ? rightHeelProbeGap : leftHeelProbeGap;
            float frontToeProbeGap = leftIsRear ? rightToeProbeGap : leftToeProbeGap;
            Vector3 rearHeelWorld = soleCalibrationValid && rearFoot != null
                ? rearFoot.TransformPoint(leftIsRear ? leftHeelProbeLocal : rightHeelProbeLocal)
                : rearFoot != null ? rearFoot.position : Vector3.zero;
            float rearThighLength = rearHip != null && rearKnee != null
                ? Vector3.Distance(rearHip.position, rearKnee.position)
                : 0f;
            Vector3 rearHipHeel = rearHip != null ? rearHip.position - rearHeelWorld : Vector3.zero;
            float spinePitch = SpineWorldPitch();
            float leftTravel = hasPreviousPose ? Vector3.Distance(previousLeftFootWorld, leftWorld) : 0f;
            float rightTravel = hasPreviousPose ? Vector3.Distance(previousRightFootWorld, rightWorld) : 0f;
            Vector3 leftHandLocal = LocalPoint(leftHand);
            float leftHandLocalTravel = hasPreviousPose
                ? Vector3.Distance(previousLeftHandLocal, leftHandLocal)
                : 0f;
            float headAngularSpeed = hasPreviousPose && head != null && sampleDeltaTime > 0f
                ? Quaternion.Angle(previousHeadRotation, head.rotation) / sampleDeltaTime
                : 0f;

            AnimatorStateInfo state = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
            AnimatorClipInfo[] clipInfo = animator != null ? animator.GetCurrentAnimatorClipInfo(0) : Array.Empty<AnimatorClipInfo>();
            AnimatorClipInfo dominant = clipInfo.OrderByDescending(info => info.weight).FirstOrDefault();
            bool inTransition = animator != null && animator.IsInTransition(0);
            AnimatorStateInfo nextState = inTransition ? animator.GetNextAnimatorStateInfo(0) : default;
            AnimatorClipInfo[] nextClipInfo = inTransition
                ? animator.GetNextAnimatorClipInfo(0)
                : Array.Empty<AnimatorClipInfo>();
            AnimatorClipInfo dominantNext = nextClipInfo.OrderByDescending(info => info.weight).FirstOrDefault();
            Vector3 desired = motor != null ? motor.DesiredWorldDirection : Vector3.zero;
            Vector3 velocity = motor != null ? motor.HorizontalVelocity : Vector3.zero;
            Vector3 cameraPosition = gameplayCamera != null ? gameplayCamera.transform.position : Vector3.zero;
            Vector3 cameraEuler = gameplayCamera != null ? gameplayCamera.transform.eulerAngles : Vector3.zero;

            GameplayDiagnosticFrame frame = new GameplayDiagnosticFrame
            {
                sample = frames.Count,
                unityFrame = Time.frameCount,
                time = sampleTime,
                gameTime = Time.time,
                deltaTime = sampleDeltaTime,
                wallTime = wallTime,
                wallDeltaTime = wallDeltaTime,
                scenario = currentScenario,
                phase = currentPhase,
                intentMoveX = intent.Move.x,
                intentMoveY = intent.Move.y,
                intentSprint = intent.SprintHeld,
                intentJumpPressed = intent.JumpPressed,
                intentJumpHeld = intent.JumpHeld,
                intentCrouch = intent.CrouchHeld,
                intentAttack = intent.AttackPressed,
                intentBlock = intent.BlockHeld,
                blockWeight = blockPresenter != null ? blockPresenter.BlockWeight : 0f,
                leftHandHiltContactGap =
                    blockPresenter != null
                        ? blockPresenter.LeftHandHiltContactGap
                        : MissingGroundGap,
                leftGripAxisAlignmentAngle =
                    blockPresenter != null
                        ? blockPresenter.LeftGripAxisAlignmentAngle
                        : 180f,
                bladeHeadClearance =
                    blockPresenter != null
                        ? blockPresenter.BladeHeadClearance
                        : MissingGroundGap,
                bladeHeadSilhouetteClearance =
                    blockPresenter != null
                        ? blockPresenter.BladeHeadSilhouetteClearance
                        : MissingGroundGap,
                playerPosition = playerPosition,
                playerYaw = player != null ? player.eulerAngles.y : 0f,
                horizontalVelocity = velocity,
                horizontalSpeed = motor != null ? motor.HorizontalSpeed : 0f,
                targetSpeed = motor != null ? motor.TargetHorizontalSpeed : 0f,
                verticalVelocity = motor != null ? motor.VerticalVelocity : 0f,
                grounded = motor != null && motor.IsGrounded,
                groundControl = motor != null && motor.HasGroundControl,
                crouched = motor != null && motor.IsCrouched,
                crouchAmount = motor != null ? motor.CrouchAmount : 0f,
                controllerHeight = motor != null ? motor.CharacterHeight : 0f,
                reversalBraking = motor != null && motor.IsBrakingForReversal,
                velocityFacingError = FacingError(player, velocity),
                desiredFacingError = FacingError(player, desired),
                animatorStateHash = state.fullPathHash,
                animatorNormalizedTime = state.normalizedTime,
                dominantClip = dominant.clip != null ? dominant.clip.name : "none",
                dominantClipWeight = dominant.weight,
                animatorInTransition = inTransition,
                animatorNextStateHash = nextState.fullPathHash,
                animatorNextNormalizedTime = nextState.normalizedTime,
                dominantNextClip = dominantNext.clip != null ? dominantNext.clip.name : "none",
                dominantNextClipWeight = dominantNext.weight,
                poseFacingError = PoseFacingError(leftUpperLeg, rightUpperLeg),
                shoulderFacingError = PoseFacingError(leftShoulder, rightShoulder),
                headChestAngle = head != null && chest != null ? Quaternion.Angle(chest.rotation, head.rotation) : 0f,
                headAngularSpeed = headAngularSpeed,
                leftFootLocal = leftLocal,
                rightFootLocal = rightLocal,
                leftFootWorld = leftWorld,
                rightFootWorld = rightWorld,
                leftFootGroundGap = GroundGap(leftFoot),
                rightFootGroundGap = GroundGap(rightFoot),
                leftKneeGroundGap = GroundGap(leftKnee),
                rightKneeGroundGap = GroundGap(rightKnee),
                leftToeGroundGap = GroundGap(leftToe),
                rightToeGroundGap = GroundGap(rightToe),
                leftFootFrameTravel = leftTravel,
                rightFootFrameTravel = rightTravel,
                footWidth = leftFoot != null && rightFoot != null
                    ? rightLocal.x - leftLocal.x
                    : 0f,
                leftLegIsRear = leftIsRear,
                rearKneeGroundGap = GroundGap(rearKnee),
                frontFootGroundGap = ContactPointGroundGap(frontFoot, frontToe),
                rearFootGroundGap = ContactPointGroundGap(rearFoot, rearToe),
                pelvisRearFootDistance = hips != null && rearFoot != null
                    ? Vector3.Distance(hips.position, rearFoot.position)
                    : MissingGroundGap,
                pelvisGroundGap = GroundGap(hips),
                spineUprightAngle = hips != null && chest != null
                    ? Vector3.Angle(chest.position - hips.position, Vector3.up)
                    : 0f,
                soleCalibrationValid = soleCalibrationValid,
                leftHeelProbeGroundGap = leftHeelProbeGap,
                rightHeelProbeGroundGap = rightHeelProbeGap,
                leftToeProbeGroundGap = leftToeProbeGap,
                rightToeProbeGroundGap = rightToeProbeGap,
                leftKneeEstimatedSurfaceGap = leftKneeSurfaceGap,
                rightKneeEstimatedSurfaceGap = rightKneeSurfaceGap,
                leftKneeFlexion = KneeFlexion(leftUpperLeg, leftKnee, leftFoot),
                rightKneeFlexion = KneeFlexion(rightUpperLeg, rightKnee, rightFoot),
                frontFootPlantError = MaximumValidAbsolute(frontHeelProbeGap, frontToeProbeGap),
                pelvisHeightRatio = standingPelvisGap > 0.0001f
                    ? GroundGap(hips) / standingPelvisGap
                    : 0f,
                spineWorldPitch = spinePitch,
                rearHipHeelDistanceRatio = rearThighLength > 0.0001f
                    ? rearHipHeel.magnitude / rearThighLength
                    : 0f,
                rearHipHeelForwardRatio = rearThighLength > 0.0001f && player != null
                    ? Mathf.Abs(Vector3.Dot(rearHipHeel, player.forward)) / rearThighLength
                    : 0f,
                splitStance = leftIsRear ? rightLocal.z - leftLocal.z : leftLocal.z - rightLocal.z,
                leftElbowLocalX = LocalPoint(leftElbow).x,
                rightElbowLocalX = LocalPoint(rightElbow).x,
                handSpread = leftHand != null && rightHand != null
                    ? Vector3.Distance(leftHand.position, rightHand.position)
                    : 0f,
                leftHandLocal = leftHandLocal,
                rightHandLocal = LocalPoint(rightHand),
                leftHandLocalFrameTravel = leftHandLocalTravel,
                cameraPosition = cameraPosition,
                cameraYaw = cameraEuler.y,
                cameraPitch = NormalizeAngle(cameraEuler.x),
                cameraDistance = gameplayCamera != null && player != null
                    ? Vector3.Distance(gameplayCamera.transform.position, player.position)
                    : 0f,
                playerHealth = playerHealth != null ? playerHealth.Current : 0f,
                enemyHealth = enemyHealth != null ? enemyHealth.Current : 0f,
                enemyPosition = enemy != null ? enemy.position : Vector3.zero,
                enemyDistance = player != null && enemy != null
                    ? Vector3.Distance(player.position, enemy.position)
                    : 0f,
                enemyFacingAngle = player != null && enemy != null
                    ? FacingError(enemy, player.position - enemy.position)
                    : 0f,
                enemyState = enemyBrain != null ? enemyBrain.CurrentState.ToString() : "none"
            };
            frames.Add(frame);
            observedPlayerHealth = frame.playerHealth;
            observedEnemyHealth = frame.enemyHealth;
            previousLeftFootWorld = leftWorld;
            previousRightFootWorld = rightWorld;
            previousLeftHandLocal = leftHandLocal;
            previousHeadRotation = head != null ? head.rotation : Quaternion.identity;
            previousWallCaptureTime = wallNow;
            hasPreviousPose = true;
            lastCapturedUnityFrame = Time.frameCount;
            lastCapturedTime = sampleTime;
        }

        private GameplayDiagnosticReport BuildReport()
        {
            List<GameplayDiagnosticPhaseSummary> phaseSummaries = frames
                .Where(frame => frame.scenario != "setup")
                .GroupBy(frame => new { frame.scenario, frame.phase })
                .Select(group => SummarizePhase(group.Key.scenario, group.Key.phase, group.ToList()))
                .ToList();
            GameplayDiagnosticCapabilities capabilities = new GameplayDiagnosticCapabilities
            {
                input = input != null,
                motor = motor != null,
                humanoidAnimator = animator != null && animator.isHuman,
                humanoidPoseBones = hips != null && spine != null && chest != null && head != null &&
                    leftUpperLeg != null && rightUpperLeg != null && leftKnee != null && rightKnee != null &&
                    leftFoot != null && rightFoot != null,
                camera = gameplayCamera != null,
                meleeWeapon = false,
                playerHealth = playerHealth != null,
                enemyHealth = enemyHealth != null,
                enemyBrain = enemyBrain != null,
                screenshots = markers.Any(marker => !string.IsNullOrEmpty(marker.screenshot) &&
                    File.Exists(Path.Combine(outputDirectory, marker.screenshot)))
            };
            List<GameplayDiagnosticCheck> checks = BuildChecks(phaseSummaries, capabilities);
            int failures = checks.Count(check => check.status == "fail");
            int warnings = checks.Count(check => check.status == "warn");
            string checkpoint = FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Select(candidate => candidate.name)
                .FirstOrDefault(name => name.StartsWith("Prototype Systems -", StringComparison.Ordinal)) ?? "unknown";

            return new GameplayDiagnosticReport
            {
                schemaVersion = GameplayDiagnosticSchema.Version,
                runId = runId,
                runKind = runKind,
                sourceRevision = sourceRevision,
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                scene = SceneManager.GetActiveScene().path,
                checkpoint = checkpoint,
                width = Screen.width,
                height = Screen.height,
                fixedDeltaTime = Time.fixedDeltaTime,
                captureDeltaTime = Time.captureDeltaTime,
                sampleCount = frames.Count,
                eventCount = events.Count,
                markerCount = markers.Count,
                duration = frames.Count > 0 ? frames[frames.Count - 1].time : 0f,
                completed = captureCompleted,
                abortReason = captureAbortReason,
                passed = captureCompleted && failures == 0,
                failureCount = failures,
                warningCount = warnings,
                capabilities = capabilities,
                configuration = BuildConfiguration(),
                phases = phaseSummaries.ToArray(),
                checks = checks.ToArray()
            };
        }

        private GameplayDiagnosticConfiguration BuildConfiguration()
        {
            ThirdPersonCamera cameraRig = gameplayCamera != null
                ? gameplayCamera.GetComponent<ThirdPersonCamera>()
                : null;
            return new GameplayDiagnosticConfiguration
            {
                walkSpeed = motor != null ? motor.WalkSpeed : 0f,
                sprintSpeed = motor != null ? motor.SprintSpeed : 0f,
                crouchSpeed = motor != null ? motor.CrouchSpeed : 0f,
                acceleration = motor != null ? motor.AccelerationRate : 0f,
                airAcceleration = motor != null ? motor.AirAccelerationRate : 0f,
                turnSpeed = motor != null ? motor.TurnSpeed : 0f,
                jumpHeight = motor != null ? motor.JumpHeight : 0f,
                gravity = motor != null ? motor.Gravity : 0f,
                reversalBrakeDot = motor != null ? motor.ReversalBrakeDot : 0f,
                reversalRestartAngle = motor != null ? motor.ReversalRestartAngle : 0f,
                crouchTransitionSpeed = motor != null ? motor.CrouchTransitionSpeed : 0f,
                animatorController = animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "none",
                animatorPlaybackSpeed = animator != null ? animator.speed : 0f,
                weaponAttackId = "none",
                cameraDistance = cameraRig != null ? cameraRig.DesiredDistance : 0f,
                cameraShoulderOffset = cameraRig != null ? cameraRig.ShoulderOffset : 0f,
                cameraPositionSmoothTime = cameraRig != null ? cameraRig.PositionSmoothTime : 0f
            };
        }

        private GameplayDiagnosticPhaseSummary SummarizePhase(
            string scenario,
            string phase,
            List<GameplayDiagnosticFrame> samples)
        {
            GameplayDiagnosticFrame first = samples[0];
            GameplayDiagnosticFrame last = samples[samples.Count - 1];
            bool hasPhaseStart = phaseStartSnapshots.TryGetValue(
                PhaseKey(scenario, phase), out PhaseStartSnapshot phaseStart);
            float distance = 0f;
            float maximumAcceleration = 0f;
            float maximumJerk = 0f;
            float previousAcceleration = 0f;
            bool hasAcceleration = false;
            for (int index = 1; index < samples.Count; index++)
            {
                GameplayDiagnosticFrame previous = samples[index - 1];
                GameplayDiagnosticFrame current = samples[index];
                distance += Vector3.Distance(previous.playerPosition, current.playerPosition);
                float delta = Mathf.Max(0.0001f, current.deltaTime);
                float acceleration = (current.horizontalSpeed - previous.horizontalSpeed) / delta;
                maximumAcceleration = Mathf.Max(maximumAcceleration, Mathf.Abs(acceleration));
                if (hasAcceleration)
                {
                    maximumJerk = Mathf.Max(maximumJerk, Mathf.Abs(acceleration - previousAcceleration) / delta);
                }

                previousAcceleration = acceleration;
                hasAcceleration = true;
            }

            float leftMinimumGap = samples.Min(sample => sample.leftFootGroundGap);
            float rightMinimumGap = samples.Min(sample => sample.rightFootGroundGap);
            ContactSlipResult leftContact = MeasureContactSlip(samples, true);
            ContactSlipResult rightContact = MeasureContactSlip(samples, false);
            int steadyStart = Mathf.Clamp(Mathf.FloorToInt(samples.Count * 0.7f), 0, samples.Count - 1);
            List<GameplayDiagnosticFrame> steadySamples = samples.Skip(steadyStart).ToList();
            float steadySpeed = steadySamples.Average(sample => sample.horizontalSpeed);
            float meanTarget = samples.Average(sample => sample.targetSpeed);
            float timeToNinety = -1f;
            if (meanTarget > 0.1f)
            {
                GameplayDiagnosticFrame reached = samples.FirstOrDefault(
                    sample => sample.horizontalSpeed >= meanTarget * 0.9f);
                if (reached != null)
                {
                    timeToNinety = reached.time - first.time;
                }
            }

            List<GameplayDiagnosticEvent> phaseEvents = events
                .Where(item => item.scenario == scenario && item.phase == phase)
                .ToList();
            List<GameplayDiagnosticFrame> settledCrouchCandidates = samples
                .Where(sample => sample.crouched && sample.crouchAmount >= 0.95f &&
                    sample.horizontalSpeed <= 0.1f && sample.groundControl && sample.soleCalibrationValid)
                .ToList();
            List<GameplayDiagnosticFrame> settledCrouch = settledCrouchCandidates
                .Skip(Mathf.Max(0, settledCrouchCandidates.Count - 30))
                .ToList();
            bool settledRearLeft = settledCrouch.Count > 0 &&
                settledCrouch.Count(sample => sample.leftLegIsRear) >= settledCrouch.Count * 0.5f;
            IEnumerable<float> settledRearKneeSurface = settledCrouch.Select(sample =>
                settledRearLeft ? sample.leftKneeEstimatedSurfaceGap : sample.rightKneeEstimatedSurfaceGap);
            float enemyTravel = 0f;
            if (hasPhaseStart)
            {
                enemyTravel += Vector3.Distance(phaseStart.EnemyPosition, first.enemyPosition);
            }

            for (int index = 1; index < samples.Count; index++)
            {
                enemyTravel += Vector3.Distance(samples[index - 1].enemyPosition, samples[index].enemyPosition);
            }

            return new GameplayDiagnosticPhaseSummary
            {
                scenario = scenario,
                phase = phase,
                samples = samples.Count,
                duration = last.time - first.time,
                distance = distance,
                meanSpeed = samples.Average(sample => sample.horizontalSpeed),
                maximumSpeed = samples.Max(sample => sample.horizontalSpeed),
                meanTargetSpeed = meanTarget,
                steadySpeed = steadySpeed,
                timeToNinetyPercentSpeed = timeToNinety,
                maximumAcceleration = maximumAcceleration,
                maximumJerk = maximumJerk,
                maximumVelocityFacingError = samples.Max(sample => Mathf.Abs(sample.velocityFacingError)),
                maximumDesiredFacingError = samples.Max(sample => Mathf.Abs(sample.desiredFacingError)),
                maximumPoseFacingError = samples.Max(sample => Mathf.Abs(sample.poseFacingError)),
                maximumShoulderFacingError = samples.Max(sample => Mathf.Abs(sample.shoulderFacingError)),
                maximumHeadChestAngle = samples.Max(sample => sample.headChestAngle),
                maximumHeadAngularSpeed = samples.Max(sample => sample.headAngularSpeed),
                maximumSpineUprightAngle = samples.Max(sample => sample.spineUprightAngle),
                steadySpineUprightAngle = steadySamples.Average(sample => sample.spineUprightAngle),
                minimumFootWidth = samples.Min(sample => sample.footWidth),
                crossoverFrames = samples.Count(sample => sample.footWidth < 0f),
                maximumFootFrameTravel = samples.Max(sample =>
                    Mathf.Max(sample.leftFootFrameTravel, sample.rightFootFrameTravel)),
                leftFootMinimumGroundGap = leftMinimumGap,
                rightFootMinimumGroundGap = rightMinimumGap,
                leftFootMaximumGroundGap = samples.Max(sample => sample.leftFootGroundGap),
                rightFootMaximumGroundGap = samples.Max(sample => sample.rightFootGroundGap),
                leftContactSlip = leftContact.Slip,
                rightContactSlip = rightContact.Slip,
                leftContactSamples = leftContact.Samples,
                rightContactSamples = rightContact.Samples,
                leftContactSlipRate = leftContact.Rate,
                rightContactSlipRate = rightContact.Rate,
                minimumRearKneeGroundGap = samples.Min(sample => sample.rearKneeGroundGap),
                steadyRearKneeGroundGap = steadySamples.Average(sample => sample.rearKneeGroundGap),
                steadyFrontFootGroundGap = steadySamples.Average(sample => sample.frontFootGroundGap),
                steadyRearFootGroundGap = steadySamples.Average(sample => sample.rearFootGroundGap),
                steadyPelvisRearFootDistance = steadySamples.Average(sample => sample.pelvisRearFootDistance),
                settledCrouchSamples = settledCrouch.Count,
                settledRearSide = settledCrouch.Count == 0 ? "unknown" : settledRearLeft ? "left" : "right",
                settledRearKneeSurfaceGapMedian = Median(settledRearKneeSurface),
                settledRearKneeSurfaceGapP90 = Percentile(settledRearKneeSurface, 0.9f),
                settledRearKneeFlexionMedian = Median(settledCrouch.Select(sample =>
                    settledRearLeft ? sample.leftKneeFlexion : sample.rightKneeFlexion)),
                settledFrontFootPlantErrorMedian = Median(settledCrouch.Select(sample => sample.frontFootPlantError)),
                settledFrontFootPlantErrorP90 = Percentile(
                    settledCrouch.Select(sample => sample.frontFootPlantError), 0.9f),
                settledSpinePitchMedian = Median(settledCrouch.Select(sample => sample.spineWorldPitch)),
                settledSpinePitchP90 = Percentile(
                    settledCrouch.Select(sample => Mathf.Abs(sample.spineWorldPitch)), 0.9f),
                settledPelvisHeightRatioMedian = Median(settledCrouch.Select(sample => sample.pelvisHeightRatio)),
                settledRearHipHeelDistanceRatioMedian = Median(
                    settledCrouch.Select(sample => sample.rearHipHeelDistanceRatio)),
                settledRearHipHeelForwardRatioMedian = Median(
                    settledCrouch.Select(sample => sample.rearHipHeelForwardRatio)),
                settledSplitStanceMedian = Median(settledCrouch.Select(sample => sample.splitStance)),
                leftElbowLateralRange = Range(samples.Select(sample => sample.leftElbowLocalX)),
                rightElbowLateralRange = Range(samples.Select(sample => sample.rightElbowLocalX)),
                minimumCameraDistance = samples.Min(sample => sample.cameraDistance),
                maximumCameraDistance = samples.Max(sample => sample.cameraDistance),
                cameraDistanceRange = Range(samples.Select(sample => sample.cameraDistance)),
                groundedRatio = samples.Count(sample => sample.grounded) / (float)samples.Count,
                airborneRatio = samples.Count(sample => !sample.grounded) / (float)samples.Count,
                crouchedRatio = samples.Count(sample => sample.crouched) / (float)samples.Count,
                reversalBrakingRatio = samples.Count(sample => sample.reversalBraking) / (float)samples.Count,
                verticalRange = samples.Max(sample => sample.playerPosition.y) -
                    samples.Min(sample => sample.playerPosition.y),
                endingSpeed = last.horizontalSpeed,
                endingVelocityFacingError = Mathf.Abs(last.velocityFacingError),
                endingGrounded = last.grounded,
                endingCrouched = last.crouched,
                enemyTravel = enemyTravel,
                playerHealthStart = hasPhaseStart ? phaseStart.PlayerHealth : first.playerHealth,
                playerHealthEnd = last.playerHealth,
                enemyHealthStart = hasPhaseStart ? phaseStart.EnemyHealth : first.enemyHealth,
                enemyHealthEnd = last.enemyHealth,
                attackStarts = phaseEvents.Count(item => item.kind == "attack-started"),
                attackRejections = phaseEvents.Count(item => item.kind == "attack-rejected"),
                resolvedAttacks = phaseEvents.Count(item => item.kind == "attack-resolved"),
                damagingAttacks = phaseEvents.Count(item => item.kind == "attack-resolved" && item.damagedTargetCount > 0),
                damageEvents = phaseEvents.Count(item => item.kind == "damage-resolved"),
                deathEvents = phaseEvents.Count(item => item.kind == "gameplay-death"),
                requestedDamage = phaseEvents.Where(item => item.kind == "damage-resolved")
                    .Sum(item => item.requestedDamage),
                effectiveDamage = phaseEvents.Where(item => item.kind == "damage-resolved")
                    .Sum(item => item.effectiveDamage),
                overkillDamage = phaseEvents.Where(item => item.kind == "damage-resolved")
                    .Sum(item => item.overkillDamage)
            };
        }

        private static ContactSlipResult MeasureContactSlip(
            IReadOnlyList<GameplayDiagnosticFrame> samples,
            bool left)
        {
            float[] validGaps = samples
                .Select(sample => left ? sample.leftFootGroundGap : sample.rightFootGroundGap)
                .Where(gap => gap < MissingGroundGap)
                .ToArray();
            if (validGaps.Length < 3)
            {
                return default;
            }

            float contactThreshold = Percentile(validGaps, 0.2f) + 0.003f;
            float slip = 0f;
            float contactTime = 0f;
            int contactSamples = 0;
            for (int index = 1; index < samples.Count; index++)
            {
                GameplayDiagnosticFrame previous = samples[index - 1];
                GameplayDiagnosticFrame current = samples[index];
                float previousGap = left ? previous.leftFootGroundGap : previous.rightFootGroundGap;
                float currentGap = left ? current.leftFootGroundGap : current.rightFootGroundGap;
                if (previousGap > contactThreshold || currentGap > contactThreshold)
                {
                    continue;
                }

                Vector3 previousWorld = left ? previous.leftFootWorld : previous.rightFootWorld;
                Vector3 currentWorld = left ? current.leftFootWorld : current.rightFootWorld;
                previousWorld.y = 0f;
                currentWorld.y = 0f;
                slip += Vector3.Distance(previousWorld, currentWorld);
                contactTime += Mathf.Max(0f, current.deltaTime);
                contactSamples++;
            }

            return new ContactSlipResult(
                slip,
                contactSamples,
                contactTime > 0.0001f ? slip / contactTime : 0f);
        }

        private List<GameplayDiagnosticCheck> BuildChecks(
            List<GameplayDiagnosticPhaseSummary> phases,
            GameplayDiagnosticCapabilities capabilities)
        {
            List<GameplayDiagnosticCheck> checks = new List<GameplayDiagnosticCheck>();
            AddSystemCheck(checks, "capture-completed", "failure", "suite", "complete", "completed",
                captureCompleted ? 1f : 0f, captureCompleted, "1",
                captureCompleted ? "Capture reached its requested terminal state." : captureAbortReason);
            AddCapabilityCheck(checks, "input-present", capabilities.input, "PlayerInputSource");
            AddCapabilityCheck(checks, "motor-present", capabilities.motor, "ThirdPersonMotor");
            AddCapabilityCheck(checks, "humanoid-present", capabilities.humanoidAnimator, "Humanoid Animator");
            AddCapabilityCheck(checks, "pose-bones-present", capabilities.humanoidPoseBones,
                "required Humanoid pose bones");
            AddCapabilityCheck(checks, "camera-present", capabilities.camera, "gameplay Camera");
            AddCapabilityCheck(checks, "player-health-present", capabilities.playerHealth, "player Health");
            AddCapabilityCheck(checks, "dummy-present", capabilities.enemyHealth, "training dummy Health");
            AddCapabilityCheck(checks, "dummy-brain-present", capabilities.enemyBrain, "training dummy EnemyBrain");

            bool deterministicSuite = string.Equals(runKind, "deterministic-full-suite", StringComparison.Ordinal);
            if (deterministicSuite)
            {
                AddCapabilityCheck(checks, "screenshots-written", capabilities.screenshots,
                    "at least one verified screenshot");
                foreach (string key in CombatLabDiagnosticScenarioRunner.RequiredPhaseKeys)
                {
                    string[] parts = key.Split('/');
                    int phaseStarts = markers.Count(marker => marker.scenario == parts[0] &&
                        marker.phase == parts[1] && marker.name == "phase-start");
                    bool exists = phases.Any(phase => phase.scenario == parts[0] && phase.phase == parts[1]);
                    AddSystemCheck(checks, "required-phase-" + parts[0] + '-' + parts[1], "failure",
                        parts[0], parts[1], "phaseStarts", phaseStarts, exists && phaseStarts == 1,
                        "exactly 1 phase start", "A partial or duplicated suite must never report success.");
                }

                float expectedDelta = 1f / 60f;
                float maximumDeltaError = frames.Count == 0
                    ? expectedDelta
                    : frames.Max(frame => Mathf.Abs(frame.deltaTime - expectedDelta));
                AddSystemCheck(checks, "deterministic-sample-clock", "failure", "suite", "complete",
                    "maximumDeltaError", maximumDeltaError, maximumDeltaError <= 0.00001f,
                    "<= 0.00001 s", "Screenshot stalls must not change simulation telemetry time.");

                GameplayDiagnosticFrame firstWalk = frames.FirstOrDefault(
                    frame => frame.scenario == "movement" && frame.phase == "walk-forward");
                AddSystemCheck(checks, "walk-intent-state-aligned", "failure", "movement", "walk-forward",
                    "firstTargetSpeed", firstWalk != null ? firstWalk.targetSpeed : -1f,
                    firstWalk != null && firstWalk.intentMoveY > 0.9f && firstWalk.targetSpeed > 1f,
                    "walk intent and target on same frame", "Input telemetry must describe the state that consumed it.");
                GameplayDiagnosticFrame firstStop = frames.FirstOrDefault(
                    frame => frame.scenario == "movement" && frame.phase == "walk-stop");
                AddSystemCheck(checks, "stop-intent-state-aligned", "failure", "movement", "walk-stop",
                    "firstTargetSpeed", firstStop != null ? firstStop.targetSpeed : -1f,
                    firstStop != null && Mathf.Abs(firstStop.intentMoveY) < 0.01f && firstStop.targetSpeed < 0.01f,
                    "zero intent and zero target on same frame", "Phase boundaries must not be one frame out of sync.");

                List<GameplayDiagnosticFrame> rapidBlockFrames = frames.FindAll(
                    frame => frame.scenario == "combat" &&
                    frame.phase == "block-toggle-stress");
                float maximumRapidBlockHandTravel = rapidBlockFrames.Count == 0
                    ? 99f
                    : rapidBlockFrames.Max(frame => frame.leftHandLocalFrameTravel);
                AddSystemCheck(checks, "block-toggle-hand-bounded", "failure",
                    "combat", "block-toggle-stress", "maximumLeftHandFrameTravel",
                    maximumRapidBlockHandTravel,
                    rapidBlockFrames.Count > 0 && maximumRapidBlockHandTravel <= 0.14f,
                    "<= 0.14 m/frame",
                    "Rapid block reversals must blend directly between the authored guard and rest without a snap.");

                GameplayDiagnosticFrame recoveredBlockRest = frames.LastOrDefault(
                    frame => frame.scenario == "combat" &&
                    frame.phase == "block-entry-rest");
                AddSystemCheck(checks, "block-toggle-rest-recovered", "failure",
                    "combat", "block-entry-rest", "handSpread",
                    recoveredBlockRest != null ? recoveredBlockRest.handSpread : -1f,
                    recoveredBlockRest != null &&
                    recoveredBlockRest.blockWeight <= 0.01f &&
                    recoveredBlockRest.handSpread >= 0.6f,
                    "weight <= 0.01 and hands >= 0.60 m apart",
                    "Rapid toggling must not accumulate entry offsets or leave the left hand away from rest.");

                List<GameplayDiagnosticFrame> blockEntryFrames = frames.FindAll(
                    frame => frame.scenario == "combat" &&
                    frame.phase == "block-hold" &&
                    frame.blockWeight <= 0.42f);
                float maximumEarlyEntryTravel = blockEntryFrames.Count == 0
                    ? 99f
                    : blockEntryFrames.Max(frame => frame.leftHandLocalFrameTravel);
                AddSystemCheck(checks, "block-entry-direct", "failure",
                    "combat", "block-hold", "maximumEarlyLeftHandFrameTravel",
                    maximumEarlyEntryTravel,
                    blockEntryFrames.Count >= 3 && maximumEarlyEntryTravel <= 0.14f,
                    "<= 0.14 m/frame",
                    "The left hand must blend directly into the fixed guard without a waypoint or catch-up excursion.");

                List<GameplayDiagnosticFrame> blockJumpFrames = frames.FindAll(
                    frame => frame.scenario == "combat" &&
                    frame.phase == "block-jump");
                float maximumBlockJumpPairTravel = 99f;
                if (blockJumpFrames.Count > 0)
                {
                    maximumBlockJumpPairTravel = 0f;
                    Vector3 previousHandPair =
                        blockJumpFrames[0].leftHandLocal -
                        blockJumpFrames[0].rightHandLocal;
                    for (int index = 1; index < blockJumpFrames.Count; index++)
                    {
                        Vector3 handPair =
                            blockJumpFrames[index].leftHandLocal -
                            blockJumpFrames[index].rightHandLocal;
                        maximumBlockJumpPairTravel = Mathf.Max(
                            maximumBlockJumpPairTravel,
                            Vector3.Distance(previousHandPair, handPair));
                        previousHandPair = handPair;
                    }
                }
                float maximumBlockJumpHiltGap = blockJumpFrames.Count == 0
                    ? 99f
                    : blockJumpFrames.Max(
                        frame => frame.leftHandHiltContactGap);
                AddSystemCheck(checks, "block-jump-hand-locked", "failure",
                    "combat", "block-jump", "maximumHandPairFrameTravel",
                    maximumBlockJumpPairTravel,
                    blockJumpFrames.Count > 0 &&
                    maximumBlockJumpPairTravel <= 0.01f,
                    "<= 0.01 m/frame",
                    "A held guard must keep both hands locked together through takeoff, apex, and landing.");
                AddSystemCheck(checks, "block-jump-grip-held", "failure",
                    "combat", "block-jump", "maximumLeftHandHiltGap",
                    maximumBlockJumpHiltGap,
                    blockJumpFrames.Count > 0 &&
                    maximumBlockJumpHiltGap <= 0.09f,
                    "<= 0.09 m",
                    "Jump and landing motion must not pull the guarded left hand away from the hilt.");

                GameplayDiagnosticFrame heldBlock = frames.LastOrDefault(
                    frame => frame.scenario == "combat" && frame.phase == "block-hold");
                AddSystemCheck(checks, "block-held-two-handed", "failure", "combat", "block-hold",
                    "leftHandHiltContactGap",
                    heldBlock != null ? heldBlock.leftHandHiltContactGap : -1f,
                    heldBlock != null &&
                    heldBlock.intentBlock &&
                    heldBlock.blockWeight >= 0.99f &&
                    heldBlock.leftHandHiltContactGap <= 0.09f,
                    "held, weight >= 0.99, left knuckles <= 0.09 m from hilt",
                    "The held block must reach the actual two-handed hilt pose.");
                GameplayDiagnosticFrame releasedBlock = frames.LastOrDefault(
                    frame => frame.scenario == "combat" && frame.phase == "block-release");
                AddSystemCheck(checks, "block-release-restores-carry", "failure", "combat", "block-release",
                    "blockWeight", releasedBlock != null ? releasedBlock.blockWeight : -1f,
                    releasedBlock != null &&
                    !releasedBlock.intentBlock &&
                    releasedBlock.blockWeight <= 0.01f &&
                    releasedBlock.handSpread >= 0.6f,
                    "released, weight <= 0.01, hands >= 0.60 m apart",
                    "Releasing right click must restore the one-handed carry.");
            }

            bool eventOrderValid = events.Select((item, index) => item.sequence == index + 1).All(value => value);
            AddSystemCheck(checks, "event-sequence-contiguous", "failure", "suite", "complete", "sequence",
                eventOrderValid ? 1f : 0f, eventOrderValid, "contiguous from 1",
                "Correlated gameplay events must have an unambiguous order.");
            float minimumPlayerHealth = frames.Count > 0 ? frames.Min(frame => frame.playerHealth) : 0f;
            AddSystemCheck(checks, "player-remains-undamaged", "failure", "suite", "complete", "minimumHealth",
                minimumPlayerHealth, minimumPlayerHealth >= 99.99f, ">= 99.99",
                "The training dummy must remain passive throughout the deterministic suite.");

            GameplayDiagnosticPhaseSummary idle = phases.FirstOrDefault(
                phase => phase.scenario == "movement" && phase.phase == "idle");
            float idleNearestFootGap = idle != null
                ? Mathf.Min(idle.leftFootMinimumGroundGap, idle.rightFootMinimumGroundGap)
                : 0f;

            foreach (GameplayDiagnosticPhaseSummary phase in phases)
            {
                if (phase.meanTargetSpeed > 0.5f && phase.groundedRatio > 0.5f)
                {
                    AddCheck(checks, "facing-" + phase.phase, "warning", phase,
                        "maximumVelocityFacingError", phase.maximumVelocityFacingError,
                        phase.maximumVelocityFacingError <= 12f, "<= 12 degrees",
                        "Large values expose visible travel/body misalignment.");
                    AddCheck(checks, "crossover-" + phase.phase, "warning", phase,
                        "crossoverFrames", phase.crossoverFrames, phase.crossoverFrames == 0,
                        "0 frames", "Negative foot width indicates a crossover gait.");
                    AddCheck(checks, "contact-slip-" + phase.phase, "warning", phase,
                        "maximumContactSlipRate", Mathf.Max(phase.leftContactSlipRate, phase.rightContactSlipRate),
                        Mathf.Max(phase.leftContactSlipRate, phase.rightContactSlipRate) <= 0.4f,
                        "<= 0.40 m/s", "Uses only the lowest 20% of foot-height samples and horizontal travel.");
                }

                if (phase.scenario == "movement")
                {
                    AddCheck(checks, "foot-snap-" + phase.phase, "warning", phase,
                        "maximumFootFrameTravel", phase.maximumFootFrameTravel,
                        phase.maximumFootFrameTravel <= 0.45f, "<= 0.45 m/frame",
                        "Large one-frame bone travel exposes animation or transition snapping.");
                    AddCheck(checks, "head-snap-" + phase.phase, "warning", phase,
                        "maximumHeadAngularSpeed", phase.maximumHeadAngularSpeed,
                        phase.maximumHeadAngularSpeed <= 720f, "<= 720 degrees/s",
                        "Large deterministic angular speeds expose head or transition pops.");
                    AddCheck(checks, "elbow-lateral-" + phase.phase, "warning", phase,
                        "maximumElbowLateralRange",
                        Mathf.Max(phase.leftElbowLateralRange, phase.rightElbowLateralRange),
                        Mathf.Max(phase.leftElbowLateralRange, phase.rightElbowLateralRange) <= 0.18f,
                        "<= 0.18 m", "Large lateral ranges expose elbows flaring during locomotion.");
                }

                if (idle != null && phase.groundedRatio > 0.8f &&
                    phase.phase != "idle-jump" && phase.phase != "running-jump")
                {
                    float nearestGap = Mathf.Min(
                        phase.leftFootMinimumGroundGap, phase.rightFootMinimumGroundGap);
                    AddCheck(checks, "foot-penetration-" + phase.phase, "warning", phase,
                        "nearestFootMinimumGapDelta", nearestGap - idleNearestFootGap,
                        nearestGap >= idleNearestFootGap - 0.08f, ">= idle - 0.08 m",
                        "Ankle-pivot gaps are compared with standing idle rather than incorrectly treated as sole height.");
                    AddCheck(checks, "grounded-hover-" + phase.phase, "warning", phase,
                        "nearestFootMinimumGapDelta", nearestGap - idleNearestFootGap,
                        nearestGap <= idleNearestFootGap + 0.06f, "<= idle + 0.06 m",
                        "At least one foot should approach its calibrated standing contact height.");
                }

                AddCheck(checks, "camera-range-" + phase.phase, "warning", phase,
                    "cameraDistanceRange", phase.cameraDistanceRange, phase.cameraDistanceRange <= 1f,
                    "<= 1.00 m", "Unexpected camera-distance excursions expose collision or reset discontinuities.");

                if (phase.phase == "walk-forward")
                {
                    AddCheck(checks, "walk-speed", "warning", phase, "steadySpeed", phase.steadySpeed,
                        Mathf.Abs(phase.steadySpeed - ThirdPersonMotor.DefaultWalkSpeed) <= 0.25f,
                        $"{ThirdPersonMotor.DefaultWalkSpeed:0.00} +/- 0.25 m/s",
                        "Confirms animation review is performed at the intended motor speed.");
                }
                else if (phase.phase == "sprint-forward")
                {
                    AddCheck(checks, "sprint-speed", "warning", phase, "steadySpeed", phase.steadySpeed,
                        Mathf.Abs(phase.steadySpeed - ThirdPersonMotor.DefaultSprintSpeed) <= 0.35f,
                        $"{ThirdPersonMotor.DefaultSprintSpeed:0.00} +/- 0.35 m/s",
                        "Confirms the sprint reaches its intended steady state.");
                }
                else if (phase.phase == "sprint-reversal")
                {
                    AddCheck(checks, "reversal-brake", "failure", phase, "reversalBrakingRatio",
                        phase.reversalBrakingRatio, phase.reversalBrakingRatio > 0.03f, "> 0.03",
                        "A 180-degree reversal must enter the motor's braking state.");
                    AddCheck(checks, "reversal-recovered", "failure", phase, "endingFacingError",
                        phase.endingVelocityFacingError,
                        phase.endingSpeed >= ThirdPersonMotor.DefaultSprintSpeed * 0.85f &&
                        phase.endingVelocityFacingError <= 12f,
                        ">= 85% sprint speed and <= 12 degrees",
                        "The reversal must recover instead of merely entering its brake state.");
                }
                else if (phase.phase == "crouch-idle" || phase.phase == "crouch-move")
                {
                    AddCheck(checks, "crouch-state-" + phase.phase, "failure", phase, "crouchedRatio",
                        phase.crouchedRatio, phase.crouchedRatio >= 0.75f, ">= 0.75",
                        "The diagnostic intent must reach the real crouch state.");
                    if (phase.phase == "crouch-idle")
                    {
                        AddCrouchPostureChecks(checks, phase);
                    }
                }
                else if (phase.phase == "crouch-exit")
                {
                    AddCheck(checks, "crouch-exit-standing", "failure", phase, "endingCrouched",
                        phase.endingCrouched ? 1f : 0f, !phase.endingCrouched, "0",
                        "Crouch release must return to the standing controller state.");
                }
                else if (phase.phase == "idle-jump" || phase.phase == "running-jump")
                {
                    AddCheck(checks, "jump-height-" + phase.phase, "failure", phase, "verticalRange",
                        phase.verticalRange, phase.verticalRange >= 0.55f, ">= 0.55 m",
                        "Confirms a real physics jump occurred instead of an animation-only transition.");
                    AddCheck(checks, "jump-landed-" + phase.phase, "failure", phase, "endingGrounded",
                        phase.endingGrounded ? 1f : 0f,
                        phase.airborneRatio > 0.05f && phase.endingGrounded, "airborne then grounded",
                        "A jump is incomplete unless it leaves the ground and lands in the same phase.");
                }
            }

            return checks;
        }

        private static void AddCrouchPostureChecks(
            List<GameplayDiagnosticCheck> checks,
            GameplayDiagnosticPhaseSummary phase)
        {
            AddCheck(checks, "crouch-settled-samples", "failure", phase, "settledCrouchSamples",
                phase.settledCrouchSamples, phase.settledCrouchSamples >= 10, ">= 10",
                "Posture checks require calibrated, grounded, fully crouched idle samples.");
            if (phase.settledCrouchSamples < 10)
            {
                return;
            }

            AddCheck(checks, "crouch-rear-knee-hover", "warning", phase, "rearKneeSurfaceGapP90",
                phase.settledRearKneeSurfaceGapP90, phase.settledRearKneeSurfaceGapP90 <= 0.04f,
                "<= 0.04 m", "Estimated knee surface should rest near the ground.");
            AddCheck(checks, "crouch-rear-knee-penetration", "warning", phase, "rearKneeSurfaceGapMedian",
                phase.settledRearKneeSurfaceGapMedian, phase.settledRearKneeSurfaceGapMedian >= -0.025f,
                ">= -0.025 m", "The knee-radius estimate should not penetrate the floor.");
            AddCheck(checks, "crouch-front-foot-planted", "warning", phase, "frontFootPlantErrorP90",
                phase.settledFrontFootPlantErrorP90, phase.settledFrontFootPlantErrorP90 <= 0.035f,
                "<= 0.035 m", "Calibrated heel and toe probes expose front-foot hover or penetration.");
            AddCheck(checks, "crouch-spine-upright", "warning", phase, "absoluteSpinePitchP90",
                phase.settledSpinePitchP90, phase.settledSpinePitchP90 <= 12f, "<= 12 degrees",
                "Uses chest-to-spine positions so importer-specific bone axes cannot fake an upright torso.");
            AddCheck(checks, "crouch-resting-pelvis-height", "warning", phase, "pelvisHeightRatio",
                phase.settledPelvisHeightRatioMedian,
                phase.settledPelvisHeightRatioMedian >= 0.32f &&
                phase.settledPelvisHeightRatioMedian <= 0.68f,
                "0.32 to 0.68 of standing height", "A tactical resting crouch should sit low without clipping.");
            AddCheck(checks, "crouch-sitting-on-heel", "warning", phase, "rearHipHeelDistanceRatio",
                phase.settledRearHipHeelDistanceRatioMedian,
                phase.settledRearHipHeelDistanceRatioMedian <= 1.2f, "<= 1.20 thigh lengths",
                "Normalized rear hip-to-calibrated-heel distance measures whether the pelvis rests back.");
            AddCheck(checks, "crouch-heel-sagittal-offset", "warning", phase, "rearHipHeelForwardRatio",
                phase.settledRearHipHeelForwardRatioMedian,
                phase.settledRearHipHeelForwardRatioMedian <= 0.55f, "<= 0.55 thigh lengths",
                "Large values mean the pelvis is not actually seated over the rear heel.");
            AddCheck(checks, "crouch-rear-knee-flexion", "warning", phase, "rearKneeFlexion",
                phase.settledRearKneeFlexionMedian, phase.settledRearKneeFlexionMedian >= 120f,
                ">= 120 degrees", "The rear leg should be folded rather than hovering in a shallow squat.");
            AddCheck(checks, "crouch-split-stance", "warning", phase, "splitStance",
                phase.settledSplitStanceMedian, phase.settledSplitStanceMedian >= 0.18f,
                ">= 0.18 m", "The planted front foot and kneeling rear leg must remain visibly separated.");
        }

        private static void AddCapabilityCheck(
            List<GameplayDiagnosticCheck> checks,
            string id,
            bool present,
            string capability)
        {
            checks.Add(new GameplayDiagnosticCheck
            {
                id = id,
                severity = "failure",
                status = present ? "pass" : "fail",
                scenario = "setup",
                phase = "capture-start",
                metric = "capability",
                observed = present ? 1f : 0f,
                expectation = capability + " present",
                detail = present ? capability + " was observed." : capability + " is missing from the scene."
            });
        }

        private static void AddSystemCheck(
            List<GameplayDiagnosticCheck> checks,
            string id,
            string severity,
            string scenario,
            string phase,
            string metric,
            float observed,
            bool passed,
            string expectation,
            string detail)
        {
            checks.Add(new GameplayDiagnosticCheck
            {
                id = id,
                severity = severity,
                status = passed ? "pass" : severity == "failure" ? "fail" : "warn",
                scenario = scenario,
                phase = phase,
                metric = metric,
                observed = observed,
                expectation = expectation,
                detail = detail
            });
        }

        private static void AddCheck(
            List<GameplayDiagnosticCheck> checks,
            string id,
            string severity,
            GameplayDiagnosticPhaseSummary phase,
            string metric,
            float observed,
            bool passed,
            string expectation,
            string detail)
        {
            checks.Add(new GameplayDiagnosticCheck
            {
                id = id,
                severity = severity,
                status = passed ? "pass" : severity == "failure" ? "fail" : "warn",
                scenario = phase.scenario,
                phase = phase.phase,
                metric = metric,
                observed = observed,
                expectation = expectation,
                detail = detail
            });
        }

        private void WriteArtifacts(GameplayDiagnosticReport report)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "report.json"), JsonUtility.ToJson(report, true));
            WriteFramesCsv(Path.Combine(outputDirectory, "frames.csv"));
            WriteEventsCsv(Path.Combine(outputDirectory, "events.csv"));
            WriteEventsJsonLines(Path.Combine(outputDirectory, "events.jsonl"));
            WriteMarkersCsv(Path.Combine(outputDirectory, "markers.csv"));
            WritePhaseSummaryCsv(Path.Combine(outputDirectory, "phase_summary.csv"), report.phases);
            File.WriteAllText(Path.Combine(outputDirectory, "timeline.svg"), BuildTimelineSvg());
            File.WriteAllText(Path.Combine(outputDirectory, "ai_report.md"), BuildAiReport(report));

            Directory.CreateDirectory(ArtifactRoot);
            LatestPointer pointer = new LatestPointer
            {
                schemaVersion = GameplayDiagnosticSchema.Version,
                runId = report.runId,
                generatedUtc = report.generatedUtc,
                passed = report.passed,
                relativeDirectory = Path.GetRelativePath(ArtifactRoot, outputDirectory).Replace('\\', '/')
            };
            File.WriteAllText(Path.Combine(ArtifactRoot, "latest.json"), JsonUtility.ToJson(pointer, true));
            File.WriteAllText(Path.Combine(ArtifactRoot, "AI_LATEST.md"), BuildAiReport(report));
        }

        private void WriteFramesCsv(string path)
        {
            using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("sample,unity_frame,time,game_time,delta_time,wall_time,wall_delta_time,scenario,phase,intent_x,intent_y,sprint,jump_pressed,jump_held,crouch,attack,block,block_weight,left_hand_hilt_gap,left_grip_axis_angle,blade_head_clearance,blade_head_silhouette_clearance,player_x,player_y,player_z,player_yaw,velocity_x,velocity_y,velocity_z,speed,target_speed,vertical_velocity,grounded,ground_control,crouched,crouch_amount,controller_height,reversal_braking,velocity_facing_error,desired_facing_error,animator_state_hash,animator_normalized_time,dominant_clip,dominant_weight,animator_in_transition,next_state_hash,next_normalized_time,next_clip,next_clip_weight,pose_facing_error,shoulder_facing_error,head_chest_angle,head_angular_speed,left_foot_x,left_foot_y,left_foot_z,right_foot_x,right_foot_y,right_foot_z,left_ground_gap,right_ground_gap,left_knee_gap,right_knee_gap,left_toe_gap,right_toe_gap,left_foot_travel,right_foot_travel,foot_width,left_is_rear,rear_knee_gap,front_foot_gap,rear_foot_gap,pelvis_rear_foot_distance,pelvis_gap,spine_upright_angle,sole_calibrated,left_heel_probe_gap,right_heel_probe_gap,left_toe_probe_gap,right_toe_probe_gap,left_knee_surface_gap,right_knee_surface_gap,left_knee_flexion,right_knee_flexion,front_plant_error,pelvis_height_ratio,spine_pitch,rear_hip_heel_ratio,rear_hip_heel_forward_ratio,split_stance,left_elbow_x,right_elbow_x,hand_spread,left_hand_x,left_hand_y,left_hand_z,right_hand_x,right_hand_y,right_hand_z,left_hand_local_frame_travel,sword_attack_active,sword_direction_x,sword_direction_y,sword_direction_z,sword_plane_normal_x,sword_plane_normal_y,sword_plane_normal_z,sword_plane_error,sword_forearm_angle,camera_x,camera_y,camera_z,camera_yaw,camera_pitch,camera_distance,player_health,enemy_health,enemy_x,enemy_y,enemy_z,enemy_distance,enemy_facing_angle,enemy_state,cooldown,attack_center_x,attack_center_y,attack_center_z,weapon_attack_in_progress,blade_base_x,blade_base_y,blade_base_z,blade_tip_x,blade_tip_y,blade_tip_z");
            foreach (GameplayDiagnosticFrame frame in frames)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    frame.sample.ToString(CultureInfo.InvariantCulture), frame.unityFrame.ToString(CultureInfo.InvariantCulture),
                    F(frame.time), F(frame.gameTime), F(frame.deltaTime), F(frame.wallTime), F(frame.wallDeltaTime),
                    Csv(frame.scenario), Csv(frame.phase),
                    F(frame.intentMoveX), F(frame.intentMoveY), B(frame.intentSprint), B(frame.intentJumpPressed),
                    B(frame.intentJumpHeld), B(frame.intentCrouch), B(frame.intentAttack),
                    B(frame.intentBlock), F(frame.blockWeight),
                    F(frame.leftHandHiltContactGap),
                    F(frame.leftGripAxisAlignmentAngle), F(frame.bladeHeadClearance),
                    F(frame.bladeHeadSilhouetteClearance),
                    F(frame.playerPosition.x), F(frame.playerPosition.y), F(frame.playerPosition.z), F(frame.playerYaw),
                    F(frame.horizontalVelocity.x), F(frame.horizontalVelocity.y), F(frame.horizontalVelocity.z),
                    F(frame.horizontalSpeed), F(frame.targetSpeed), F(frame.verticalVelocity), B(frame.grounded),
                    B(frame.groundControl), B(frame.crouched), F(frame.crouchAmount), F(frame.controllerHeight),
                    B(frame.reversalBraking), F(frame.velocityFacingError), F(frame.desiredFacingError),
                    frame.animatorStateHash.ToString(CultureInfo.InvariantCulture), F(frame.animatorNormalizedTime),
                    Csv(frame.dominantClip), F(frame.dominantClipWeight),
                    B(frame.animatorInTransition), frame.animatorNextStateHash.ToString(CultureInfo.InvariantCulture),
                    F(frame.animatorNextNormalizedTime), Csv(frame.dominantNextClip), F(frame.dominantNextClipWeight),
                    F(frame.poseFacingError),
                    F(frame.shoulderFacingError), F(frame.headChestAngle), F(frame.headAngularSpeed),
                    F(frame.leftFootLocal.x), F(frame.leftFootLocal.y), F(frame.leftFootLocal.z),
                    F(frame.rightFootLocal.x), F(frame.rightFootLocal.y), F(frame.rightFootLocal.z),
                    F(frame.leftFootGroundGap), F(frame.rightFootGroundGap), F(frame.leftKneeGroundGap),
                    F(frame.rightKneeGroundGap), F(frame.leftToeGroundGap), F(frame.rightToeGroundGap),
                    F(frame.leftFootFrameTravel), F(frame.rightFootFrameTravel), F(frame.footWidth),
                    B(frame.leftLegIsRear), F(frame.rearKneeGroundGap), F(frame.frontFootGroundGap),
                    F(frame.rearFootGroundGap), F(frame.pelvisRearFootDistance), F(frame.pelvisGroundGap),
                    F(frame.spineUprightAngle), B(frame.soleCalibrationValid), F(frame.leftHeelProbeGroundGap),
                    F(frame.rightHeelProbeGroundGap), F(frame.leftToeProbeGroundGap),
                    F(frame.rightToeProbeGroundGap), F(frame.leftKneeEstimatedSurfaceGap),
                    F(frame.rightKneeEstimatedSurfaceGap), F(frame.leftKneeFlexion), F(frame.rightKneeFlexion),
                    F(frame.frontFootPlantError), F(frame.pelvisHeightRatio), F(frame.spineWorldPitch),
                    F(frame.rearHipHeelDistanceRatio), F(frame.rearHipHeelForwardRatio), F(frame.splitStance),
                    F(frame.leftElbowLocalX),
                    F(frame.rightElbowLocalX), F(frame.handSpread),
                    F(frame.leftHandLocal.x), F(frame.leftHandLocal.y), F(frame.leftHandLocal.z),
                    F(frame.rightHandLocal.x), F(frame.rightHandLocal.y), F(frame.rightHandLocal.z),
                    F(frame.leftHandLocalFrameTravel),
                    B(frame.swordAttackActive),
                    F(frame.swordDirection.x), F(frame.swordDirection.y), F(frame.swordDirection.z),
                    F(frame.swordBladePlaneNormal.x), F(frame.swordBladePlaneNormal.y),
                    F(frame.swordBladePlaneNormal.z), F(frame.swordBladePlaneError),
                    F(frame.swordForearmAngle),
                    F(frame.cameraPosition.x),
                    F(frame.cameraPosition.y), F(frame.cameraPosition.z), F(frame.cameraYaw), F(frame.cameraPitch),
                    F(frame.cameraDistance), F(frame.playerHealth), F(frame.enemyHealth), F(frame.enemyPosition.x),
                    F(frame.enemyPosition.y), F(frame.enemyPosition.z), F(frame.enemyDistance),
                    F(frame.enemyFacingAngle), Csv(frame.enemyState), F(frame.weaponCooldownRemaining),
                    F(frame.attackCenter.x), F(frame.attackCenter.y), F(frame.attackCenter.z),
                    B(frame.weaponAttackInProgress),
                    F(frame.bladeBase.x), F(frame.bladeBase.y), F(frame.bladeBase.z),
                    F(frame.bladeTip.x), F(frame.bladeTip.y), F(frame.bladeTip.z)
                }));
            }
        }

        private void WriteEventsCsv(string path)
        {
            using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("sequence,sample,unity_frame,time,scenario,phase,kind,source,target,detail,requested_damage,effective_damage,overkill_damage,position_x,position_y,position_z,direction_x,direction_y,direction_z,colliders,unique_targets,damaged_targets");
            foreach (GameplayDiagnosticEvent item in events)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    item.sequence.ToString(CultureInfo.InvariantCulture), item.sample.ToString(CultureInfo.InvariantCulture),
                    item.unityFrame.ToString(CultureInfo.InvariantCulture),
                    F(item.time), Csv(item.scenario), Csv(item.phase), Csv(item.kind), Csv(item.source), Csv(item.target),
                    Csv(item.detail), F(item.requestedDamage), F(item.effectiveDamage), F(item.overkillDamage),
                    F(item.position.x), F(item.position.y), F(item.position.z), F(item.direction.x), F(item.direction.y),
                    F(item.direction.z), item.colliderCount.ToString(CultureInfo.InvariantCulture),
                    item.uniqueTargetCount.ToString(CultureInfo.InvariantCulture),
                    item.damagedTargetCount.ToString(CultureInfo.InvariantCulture)
                }));
            }
        }

        private void WriteEventsJsonLines(string path)
        {
            using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
            foreach (GameplayDiagnosticEvent item in events)
            {
                writer.WriteLine(JsonUtility.ToJson(item, false));
            }
        }

        private void WriteMarkersCsv(string path)
        {
            using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("sample,unity_frame,time,scenario,phase,name,screenshot");
            foreach (GameplayDiagnosticMarker marker in markers)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    marker.sample.ToString(CultureInfo.InvariantCulture),
                    marker.unityFrame.ToString(CultureInfo.InvariantCulture), F(marker.time), Csv(marker.scenario),
                    Csv(marker.phase), Csv(marker.name), Csv(marker.screenshot)
                }));
            }
        }

        private static void WritePhaseSummaryCsv(
            string path,
            IEnumerable<GameplayDiagnosticPhaseSummary> phases)
        {
            using StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("scenario,phase,samples,duration,distance,mean_speed,steady_speed,max_speed,target_speed,time_to_90,max_acceleration,max_jerk,velocity_facing_error,pose_facing_error,shoulder_facing_error,head_chest_angle,head_angular_speed,max_spine_upright,steady_spine_upright,min_foot_width,crossover_frames,max_foot_travel,left_min_gap,right_min_gap,left_max_gap,right_max_gap,left_contact_slip,right_contact_slip,left_contact_samples,right_contact_samples,left_contact_rate,right_contact_rate,min_rear_knee_gap,steady_rear_knee_gap,steady_front_foot_gap,steady_rear_foot_gap,steady_pelvis_rear_foot,settled_crouch_samples,settled_rear_side,rear_knee_surface_median,rear_knee_surface_p90,rear_knee_flexion_median,front_plant_error_median,front_plant_error_p90,spine_pitch_median,spine_pitch_p90,pelvis_height_ratio,rear_hip_heel_ratio,rear_hip_heel_forward_ratio,split_stance,left_elbow_range,right_elbow_range,min_camera_distance,max_camera_distance,camera_distance_range,grounded_ratio,airborne_ratio,crouched_ratio,reversal_ratio,vertical_range,ending_speed,ending_facing_error,ending_grounded,ending_crouched,enemy_travel,player_health_start,player_health_end,enemy_health_start,enemy_health_end,attack_starts,attack_rejections,resolved_attacks,damaging_attacks,damage_events,death_events,requested_damage,effective_damage,overkill_damage");
            foreach (GameplayDiagnosticPhaseSummary phase in phases)
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    Csv(phase.scenario), Csv(phase.phase), phase.samples.ToString(CultureInfo.InvariantCulture),
                    F(phase.duration), F(phase.distance), F(phase.meanSpeed), F(phase.steadySpeed),
                    F(phase.maximumSpeed), F(phase.meanTargetSpeed), F(phase.timeToNinetyPercentSpeed),
                    F(phase.maximumAcceleration), F(phase.maximumJerk), F(phase.maximumVelocityFacingError),
                    F(phase.maximumPoseFacingError), F(phase.maximumShoulderFacingError),
                    F(phase.maximumHeadChestAngle), F(phase.maximumHeadAngularSpeed),
                    F(phase.maximumSpineUprightAngle), F(phase.steadySpineUprightAngle), F(phase.minimumFootWidth),
                    phase.crossoverFrames.ToString(CultureInfo.InvariantCulture), F(phase.maximumFootFrameTravel),
                    F(phase.leftFootMinimumGroundGap), F(phase.rightFootMinimumGroundGap),
                    F(phase.leftFootMaximumGroundGap), F(phase.rightFootMaximumGroundGap),
                    F(phase.leftContactSlip), F(phase.rightContactSlip),
                    phase.leftContactSamples.ToString(CultureInfo.InvariantCulture),
                    phase.rightContactSamples.ToString(CultureInfo.InvariantCulture),
                    F(phase.leftContactSlipRate), F(phase.rightContactSlipRate),
                    F(phase.minimumRearKneeGroundGap), F(phase.steadyRearKneeGroundGap),
                    F(phase.steadyFrontFootGroundGap), F(phase.steadyRearFootGroundGap),
                    F(phase.steadyPelvisRearFootDistance),
                    phase.settledCrouchSamples.ToString(CultureInfo.InvariantCulture), Csv(phase.settledRearSide),
                    F(phase.settledRearKneeSurfaceGapMedian), F(phase.settledRearKneeSurfaceGapP90),
                    F(phase.settledRearKneeFlexionMedian), F(phase.settledFrontFootPlantErrorMedian),
                    F(phase.settledFrontFootPlantErrorP90), F(phase.settledSpinePitchMedian),
                    F(phase.settledSpinePitchP90), F(phase.settledPelvisHeightRatioMedian),
                    F(phase.settledRearHipHeelDistanceRatioMedian),
                    F(phase.settledRearHipHeelForwardRatioMedian), F(phase.settledSplitStanceMedian),
                    F(phase.leftElbowLateralRange), F(phase.rightElbowLateralRange),
                    F(phase.minimumCameraDistance), F(phase.maximumCameraDistance), F(phase.cameraDistanceRange),
                    F(phase.groundedRatio), F(phase.airborneRatio), F(phase.crouchedRatio),
                    F(phase.reversalBrakingRatio), F(phase.verticalRange), F(phase.endingSpeed),
                    F(phase.endingVelocityFacingError), B(phase.endingGrounded), B(phase.endingCrouched),
                    F(phase.enemyTravel), F(phase.playerHealthStart),
                    F(phase.playerHealthEnd), F(phase.enemyHealthStart), F(phase.enemyHealthEnd),
                    phase.attackStarts.ToString(CultureInfo.InvariantCulture),
                    phase.attackRejections.ToString(CultureInfo.InvariantCulture),
                    phase.resolvedAttacks.ToString(CultureInfo.InvariantCulture),
                    phase.damagingAttacks.ToString(CultureInfo.InvariantCulture),
                    phase.damageEvents.ToString(CultureInfo.InvariantCulture),
                    phase.deathEvents.ToString(CultureInfo.InvariantCulture), F(phase.requestedDamage),
                    F(phase.effectiveDamage), F(phase.overkillDamage)
                }));
            }
        }

        private string BuildTimelineSvg()
        {
            const int width = 1600;
            const int height = 900;
            const float left = 92f;
            const float right = 30f;
            const float top = 54f;
            const float panelHeight = 170f;
            const float panelGap = 34f;
            float plotWidth = width - left - right;
            float duration = frames.Count > 0 ? Mathf.Max(0.001f, frames[frames.Count - 1].time) : 1f;
            StringBuilder svg = new StringBuilder(65536);
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(width)
                .Append("\" height=\"").Append(height).Append("\" viewBox=\"0 0 ").Append(width).Append(' ')
                .Append(height).AppendLine("\">");
            svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#0d1319\"/>");
            svg.AppendLine("<style>text{font-family:Segoe UI,Arial,sans-serif;fill:#dce6ee;font-size:14px}.small{font-size:10px;fill:#93a8b8}.grid{stroke:#263746;stroke-width:1}.phase{stroke:#50697a;stroke-width:1;stroke-dasharray:3 5}</style>");
            svg.Append("<text x=\"24\" y=\"30\" font-size=\"20\">Combat Lab synchronized diagnostic timeline — ")
                .Append(Xml(runId)).AppendLine("</text>");

            string[] titles = { "Horizontal speed / target (m/s)", "Vertical velocity (m/s)",
                "Foot-to-ground gap (m)", "Enemy health / weapon cooldown" };
            for (int panel = 0; panel < 4; panel++)
            {
                float y = top + panel * (panelHeight + panelGap);
                svg.Append("<rect x=\"").Append(F(left)).Append("\" y=\"").Append(F(y)).Append("\" width=\"")
                    .Append(F(plotWidth)).Append("\" height=\"").Append(F(panelHeight))
                    .AppendLine("\" fill=\"#111b23\" stroke=\"#304454\"/>");
                svg.Append("<text x=\"20\" y=\"").Append(F(y + 18f)).Append("\">")
                    .Append(Xml(titles[panel])).AppendLine("</text>");
                for (int line = 1; line < 4; line++)
                {
                    float gridY = y + panelHeight * line / 4f;
                    svg.Append("<line class=\"grid\" x1=\"").Append(F(left)).Append("\" y1=\"")
                        .Append(F(gridY)).Append("\" x2=\"").Append(F(left + plotWidth)).Append("\" y2=\"")
                        .Append(F(gridY)).AppendLine("\"/>");
                }
            }

            string previousPhase = string.Empty;
            foreach (GameplayDiagnosticFrame frame in frames)
            {
                string phaseKey = frame.scenario + "/" + frame.phase;
                if (phaseKey == previousPhase)
                {
                    continue;
                }

                previousPhase = phaseKey;
                float x = left + frame.time / duration * plotWidth;
                svg.Append("<line class=\"phase\" x1=\"").Append(F(x)).Append("\" y1=\"").Append(F(top))
                    .Append("\" x2=\"").Append(F(x)).Append("\" y2=\"").Append(F(height - 35f)).AppendLine("\"/>");
                svg.Append("<text class=\"small\" transform=\"translate(").Append(F(x + 3f)).Append(' ')
                    .Append(F(height - 18f)).Append(") rotate(-55)\">").Append(Xml(frame.phase)).AppendLine("</text>");
            }

            float speedMax = Mathf.Max(5f, frames.Count > 0 ? frames.Max(frame =>
                Mathf.Max(frame.horizontalSpeed, frame.targetSpeed)) : 5f);
            AppendPolyline(svg, frames, left, top, plotWidth, panelHeight, duration, 0f, speedMax,
                frame => frame.horizontalSpeed, "#63d98b", 2.5f);
            AppendPolyline(svg, frames, left, top, plotWidth, panelHeight, duration, 0f, speedMax,
                frame => frame.targetSpeed, "#42bde8", 1.5f);

            float verticalMax = Mathf.Max(8f, frames.Count > 0 ? frames.Max(frame => Mathf.Abs(frame.verticalVelocity)) : 8f);
            float verticalTop = top + panelHeight + panelGap;
            AppendPolyline(svg, frames, left, verticalTop, plotWidth, panelHeight, duration, -verticalMax, verticalMax,
                frame => frame.verticalVelocity, "#f2a65a", 2f);

            float gapTop = top + 2f * (panelHeight + panelGap);
            float gapMin = frames.Count > 0 ? Mathf.Min(-0.15f, frames.Min(frame =>
                Mathf.Min(frame.leftFootGroundGap, frame.rightFootGroundGap))) : -0.15f;
            float gapMax = frames.Count > 0 ? Mathf.Min(2f, Mathf.Max(0.5f, frames.Max(frame =>
                Mathf.Max(frame.leftFootGroundGap, frame.rightFootGroundGap)))) : 1f;
            AppendPolyline(svg, frames, left, gapTop, plotWidth, panelHeight, duration, gapMin, gapMax,
                frame => Mathf.Min(frame.leftFootGroundGap, 2f), "#58a6ff", 1.8f);
            AppendPolyline(svg, frames, left, gapTop, plotWidth, panelHeight, duration, gapMin, gapMax,
                frame => Mathf.Min(frame.rightFootGroundGap, 2f), "#ff8c42", 1.8f);

            float combatTop = top + 3f * (panelHeight + panelGap);
            float healthMax = Mathf.Max(100f, frames.Count > 0 ? frames.Max(frame => frame.enemyHealth) : 100f);
            AppendPolyline(svg, frames, left, combatTop, plotWidth, panelHeight, duration, 0f, healthMax,
                frame => frame.enemyHealth, "#ff5f6d", 2.5f);

            svg.AppendLine("<text x=\"1120\" y=\"28\" class=\"small\">speed #63d98b · target #42bde8 · vertical #f2a65a · L foot #58a6ff · R foot #ff8c42 · health #ff5f6d · cooldown #c792ea</text>");
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        private static void AppendPolyline(
            StringBuilder svg,
            IReadOnlyList<GameplayDiagnosticFrame> samples,
            float left,
            float top,
            float width,
            float height,
            float duration,
            float minimum,
            float maximum,
            Func<GameplayDiagnosticFrame, float> selector,
            string color,
            float strokeWidth)
        {
            if (samples.Count == 0)
            {
                return;
            }

            float range = Mathf.Max(0.0001f, maximum - minimum);
            svg.Append("<polyline fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"")
                .Append(F(strokeWidth)).Append("\" points=\"");
            foreach (GameplayDiagnosticFrame sample in samples)
            {
                float x = left + sample.time / duration * width;
                float normalized = Mathf.Clamp01((selector(sample) - minimum) / range);
                float y = top + height - normalized * height;
                svg.Append(F(x)).Append(',').Append(F(y)).Append(' ');
            }

            svg.AppendLine("\"/>");
        }

        private static string BuildAiReport(GameplayDiagnosticReport report)
        {
            StringBuilder output = new StringBuilder(8192);
            output.AppendLine("# Combat Lab diagnostic handoff").AppendLine();
            output.Append("Run: `").Append(report.runId).AppendLine("`");
            output.Append("Result: **").Append(report.passed ? "PASS" : "FAIL").Append("** — ")
                .Append(report.failureCount).Append(" failures, ").Append(report.warningCount).AppendLine(" warnings");
            output.Append("Completion: **").Append(report.completed ? "terminal" : "aborted").Append("**");
            if (!report.completed && !string.IsNullOrWhiteSpace(report.abortReason))
            {
                output.Append(" — ").Append(report.abortReason);
            }

            output.AppendLine();
            output.Append("Schema/source: v").Append(report.schemaVersion).Append(" / `")
                .Append(report.sourceRevision).AppendLine("`");
            output.Append("Scene/checkpoint: `").Append(report.scene).Append("` / `")
                .Append(report.checkpoint).AppendLine("`");
            output.Append("Samples/events/duration: ").Append(report.sampleCount).Append(" / ")
                .Append(report.eventCount).Append(" / ").Append(report.duration.ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine(" s").AppendLine();

            if (report.configuration != null)
            {
                output.AppendLine("## Captured configuration").AppendLine();
                output.Append("Motor walk/sprint/crouch: ")
                    .Append(report.configuration.walkSpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(report.configuration.sprintSpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(report.configuration.crouchSpeed.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" m/s; acceleration/turn: ")
                    .Append(report.configuration.acceleration.ToString("0.0", CultureInfo.InvariantCulture)).Append(" m/s² / ")
                    .Append(report.configuration.turnSpeed.ToString("0", CultureInfo.InvariantCulture)).AppendLine(" deg/s.");
                output.Append("Animator/controller: `").Append(report.configuration.animatorController)
                    .AppendLine("`; playable attack: none.").AppendLine();
            }

            GameplayDiagnosticCheck[] noteworthy = report.checks
                .Where(check => check.status != "pass")
                .OrderBy(check => check.status == "fail" ? 0 : 1)
                .ToArray();
            output.AppendLine("## Findings").AppendLine();
            if (noteworthy.Length == 0)
            {
                output.AppendLine("No automated failures or warnings.").AppendLine();
            }
            else
            {
                foreach (GameplayDiagnosticCheck check in noteworthy)
                {
                    output.Append("- **").Append(check.status.ToUpperInvariant()).Append("** `")
                        .Append(check.id).Append("`: ").Append(check.metric).Append(" = ")
                        .Append(check.observed.ToString("0.###", CultureInfo.InvariantCulture)).Append("; expected ")
                        .Append(check.expectation).Append(". ").AppendLine(check.detail);
                }

                output.AppendLine();
            }

            output.AppendLine("## Phase metrics").AppendLine();
            output.AppendLine("| Scenario / phase | Speed steady/max | Facing pose/velocity | Foot gap min L/R | Foot step max | Contact slip rate L/R | Damage | HP enemy |")
                .AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (GameplayDiagnosticPhaseSummary phase in report.phases)
            {
                output.Append("| ").Append(phase.scenario).Append(" / ").Append(phase.phase).Append(" | ")
                    .Append(phase.steadySpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(phase.maximumSpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(phase.maximumPoseFacingError.ToString("0.0", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(phase.maximumVelocityFacingError.ToString("0.0", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(phase.leftFootMinimumGroundGap.ToString("0.000", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(phase.rightFootMinimumGroundGap.ToString("0.000", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(phase.maximumFootFrameTravel.ToString("0.000", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(phase.leftContactSlipRate.ToString("0.000", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(phase.rightContactSlipRate.ToString("0.000", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(phase.effectiveDamage.ToString("0.#", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(phase.enemyHealthStart.ToString("0.#", CultureInfo.InvariantCulture)).Append("→")
                    .Append(phase.enemyHealthEnd.ToString("0.#", CultureInfo.InvariantCulture)).AppendLine(" |");
            }

            GameplayDiagnosticPhaseSummary[] crouchPhases = report.phases
                .Where(phase => phase.settledCrouchSamples > 0)
                .ToArray();
            if (crouchPhases.Length > 0)
            {
                output.AppendLine().AppendLine("## Settled crouch posture").AppendLine();
                output.AppendLine("| Phase | Samples/rear | Knee surface median/p90 | Front plant error median/p90 | Spine pitch median/p90 | Pelvis height | Hip-to-heel / forward | Split |")
                    .AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
                foreach (GameplayDiagnosticPhaseSummary phase in crouchPhases)
                {
                    output.Append("| ").Append(phase.phase).Append(" | ")
                        .Append(phase.settledCrouchSamples).Append(" / ").Append(phase.settledRearSide).Append(" | ")
                        .Append(phase.settledRearKneeSurfaceGapMedian.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" / ").Append(phase.settledRearKneeSurfaceGapP90.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(phase.settledFrontFootPlantErrorMedian.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" / ").Append(phase.settledFrontFootPlantErrorP90.ToString("0.000", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(phase.settledSpinePitchMedian.ToString("0.0", CultureInfo.InvariantCulture))
                        .Append(" / ").Append(phase.settledSpinePitchP90.ToString("0.0", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(phase.settledPelvisHeightRatioMedian.ToString("0.00", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(phase.settledRearHipHeelDistanceRatioMedian.ToString("0.00", CultureInfo.InvariantCulture))
                        .Append(" / ").Append(phase.settledRearHipHeelForwardRatioMedian.ToString("0.00", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(phase.settledSplitStanceMedian.ToString("0.00", CultureInfo.InvariantCulture))
                        .AppendLine(" |");
                }
            }

            output.AppendLine().AppendLine("## Files").AppendLine();
            output.AppendLine("- `report.json`: compact machine-readable summary and checks.");
            output.AppendLine("- `frames.csv`: synchronized input, motor, animation, pose, camera, and combat state.");
            output.AppendLine("- `phase_summary.csv`: one compact metric row per named behavior.");
            output.AppendLine("- `events.csv` / `events.jsonl`: attack acceptance/rejection, overlap resolution, damage, death, and gameplay events.");
            output.AppendLine("- `markers.csv` and `screenshots/`: event-aligned visual checkpoints.");
            output.AppendLine("- `timeline.svg`: synchronized speed, jump, ground-gap, health, cooldown, and phase visualization.");
            return output.ToString();
        }

        private bool TryCaptureScreenshot(string path)
        {
            if (gameplayCamera == null)
            {
                return false;
            }

            const int width = 960;
            const int height = 540;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = gameplayCamera.targetTexture;
            try
            {
                gameplayCamera.targetTexture = renderTexture;
                gameplayCamera.Render();
                RenderTexture.active = renderTexture;
                Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    image.Apply(false, false);
                    WriteBmp(path, image);
                    return File.Exists(path) && new FileInfo(path).Length > 54;
                }
                finally
                {
                    Destroy(image);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Diagnostic screenshot failed: {exception.Message}");
                return false;
            }
            finally
            {
                gameplayCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private float ContactPointGroundGap(Transform foot, Transform toe)
        {
            return Mathf.Min(GroundGap(foot), GroundGap(toe));
        }

        private void TryUpdateSoleCalibration()
        {
            if (motor == null || !motor.HasGroundControl || motor.IsCrouched || motor.HorizontalSpeed > 0.1f)
            {
                return;
            }

            AddProbeSample(leftFoot, leftHeelProbeSamples);
            AddProbeSample(rightFoot, rightHeelProbeSamples);
            AddProbeSample(leftToe, leftToeProbeSamples);
            AddProbeSample(rightToe, rightToeProbeSamples);
            AddScalarSample(standingPelvisGapSamples, GroundGap(hips));
            AddScalarSample(standingLeftAnkleGapSamples, GroundGap(leftFoot));
            AddScalarSample(standingRightAnkleGapSamples, GroundGap(rightFoot));

            if (leftHeelProbeSamples.Count < 10 || rightHeelProbeSamples.Count < 10 ||
                standingPelvisGapSamples.Count < 10)
            {
                return;
            }

            leftHeelProbeLocal = MedianVector(leftHeelProbeSamples);
            rightHeelProbeLocal = MedianVector(rightHeelProbeSamples);
            if (leftToeProbeSamples.Count >= 10)
            {
                leftToeProbeLocal = MedianVector(leftToeProbeSamples);
            }

            if (rightToeProbeSamples.Count >= 10)
            {
                rightToeProbeLocal = MedianVector(rightToeProbeSamples);
            }

            standingPelvisGap = Median(standingPelvisGapSamples);
            standingLeftAnkleGap = Median(standingLeftAnkleGapSamples);
            standingRightAnkleGap = Median(standingRightAnkleGapSamples);
            soleCalibrationValid = true;
        }

        private void AddProbeSample(Transform bone, List<Vector3> samples)
        {
            if (bone == null || samples.Count >= 24 || !TryGroundHit(bone.position, out RaycastHit hit))
            {
                return;
            }

            samples.Add(bone.InverseTransformPoint(hit.point));
        }

        private static void AddScalarSample(List<float> samples, float value)
        {
            if (samples.Count < 24 && value < MissingGroundGap)
            {
                samples.Add(value);
            }
        }

        private float GroundGap(Transform foot)
        {
            if (foot == null || !TryGroundHit(foot.position, out RaycastHit hit))
            {
                return MissingGroundGap;
            }

            return foot.position.y - hit.point.y;
        }

        private float ProbeGroundGap(Transform bone, Vector3 localProbe)
        {
            if (bone == null)
            {
                return MissingGroundGap;
            }

            Vector3 probe = bone.TransformPoint(localProbe);
            return TryGroundHit(probe, out RaycastHit hit)
                ? Vector3.Dot(probe - hit.point, hit.normal)
                : MissingGroundGap;
        }

        private bool TryGroundHit(Vector3 point, out RaycastHit closest)
        {
            closest = default;
            float closestDistance = float.PositiveInfinity;
            RaycastHit[] hits = Physics.RaycastAll(
                point + Vector3.up * 0.35f,
                Vector3.down,
                2f,
                ~0,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit candidate in hits)
            {
                Transform hitTransform = candidate.collider != null ? candidate.collider.transform : null;
                if (player != null && hitTransform != null &&
                    (hitTransform == player || hitTransform.IsChildOf(player)))
                {
                    continue;
                }

                if (candidate.distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = candidate.distance;
                }
            }

            return closestDistance < float.PositiveInfinity;
        }

        private float EstimatedKneeSurfaceGap(Transform knee, Transform ankle, float standingAnkleGap)
        {
            float pivotGap = GroundGap(knee);
            if (pivotGap >= MissingGroundGap || knee == null || ankle == null)
            {
                return MissingGroundGap;
            }

            float scale = animator != null ? Mathf.Max(0.01f, animator.humanScale) : 1f;
            float shinLength = Vector3.Distance(knee.position, ankle.position);
            float ankleReference = standingAnkleGap > 0f ? standingAnkleGap : 0.11f * scale;
            float kneeRadius = Mathf.Clamp(
                Mathf.Max(0.16f * shinLength, 0.65f * ankleReference),
                0.055f * scale,
                0.09f * scale);
            return pivotGap - kneeRadius;
        }

        private static float KneeFlexion(Transform hip, Transform knee, Transform ankle)
        {
            if (hip == null || knee == null || ankle == null)
            {
                return 0f;
            }

            return 180f - Vector3.Angle(hip.position - knee.position, ankle.position - knee.position);
        }

        private float SpineWorldPitch()
        {
            if (spine == null || chest == null || player == null)
            {
                return 0f;
            }

            Vector3 axis = chest.position - spine.position;
            return Mathf.Atan2(Vector3.Dot(axis, player.forward), Vector3.Dot(axis, Vector3.up)) * Mathf.Rad2Deg;
        }

        private static float MaximumValidAbsolute(float first, float second)
        {
            bool firstValid = first < MissingGroundGap;
            bool secondValid = second < MissingGroundGap;
            if (!firstValid && !secondValid)
            {
                return MissingGroundGap;
            }

            if (!firstValid)
            {
                return Mathf.Abs(second);
            }

            if (!secondValid)
            {
                return Mathf.Abs(first);
            }

            return Mathf.Max(Mathf.Abs(first), Mathf.Abs(second));
        }

        private static void WriteBmp(string path, Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            int rowBytes = image.width * 3;
            int rowPadding = (4 - rowBytes % 4) % 4;
            int pixelBytes = (rowBytes + rowPadding) * image.height;
            using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write));
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(54 + pixelBytes);
            writer.Write(0);
            writer.Write(54);
            writer.Write(40);
            writer.Write(image.width);
            writer.Write(image.height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixelBytes);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            for (int y = 0; y < image.height; y++)
            {
                int rowStart = y * image.width;
                for (int x = 0; x < image.width; x++)
                {
                    Color32 pixel = pixels[rowStart + x];
                    writer.Write(pixel.b);
                    writer.Write(pixel.g);
                    writer.Write(pixel.r);
                }

                for (int padding = 0; padding < rowPadding; padding++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        private Vector3 LocalPoint(Transform target)
        {
            return player != null && target != null ? player.InverseTransformPoint(target.position) : Vector3.zero;
        }

        private float PoseFacingError(Transform left, Transform right)
        {
            if (player == null || left == null || right == null)
            {
                return 0f;
            }

            Vector3 rightAxis = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up).normalized;
            Vector3 poseForward = Vector3.Cross(rightAxis, Vector3.up).normalized;
            if (Vector3.Dot(poseForward, player.forward) < 0f)
            {
                poseForward = -poseForward;
            }

            return Vector3.SignedAngle(player.forward, poseForward, Vector3.up);
        }

        private static float FacingError(Transform owner, Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return owner == null || flat.sqrMagnitude <= 0.0001f
                ? 0f
                : Vector3.SignedAngle(owner.forward, flat.normalized, Vector3.up);
        }

        private string RoleOf(GameObject target)
        {
            return target != null ? RoleOf(target.GetComponentInParent<Health>()) : "system";
        }

        private string RoleOf(Health health)
        {
            if (health == playerHealth)
            {
                return "player";
            }

            if (health == enemyHealth)
            {
                return "dummy";
            }

            return health != null ? health.name : "system";
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.DeltaAngle(0f, angle);
        }

        private static float Range(IEnumerable<float> values)
        {
            float[] samples = values.ToArray();
            return samples.Length == 0 ? 0f : samples.Max() - samples.Min();
        }

        private static float Median(IEnumerable<float> values)
        {
            return Percentile(values, 0.5f);
        }

        private static float Percentile(IEnumerable<float> values, float percentile)
        {
            float[] samples = values.OrderBy(value => value).ToArray();
            if (samples.Length == 0)
            {
                return 0f;
            }

            float position = Mathf.Clamp01(percentile) * (samples.Length - 1);
            int lower = Mathf.FloorToInt(position);
            int upper = Mathf.CeilToInt(position);
            return Mathf.Lerp(samples[lower], samples[upper], position - lower);
        }

        private static Vector3 MedianVector(IEnumerable<Vector3> values)
        {
            Vector3[] samples = values.ToArray();
            return samples.Length == 0
                ? Vector3.zero
                : new Vector3(
                    Median(samples.Select(value => value.x)),
                    Median(samples.Select(value => value.y)),
                    Median(samples.Select(value => value.z)));
        }

        private static string F(float value)
        {
            return value.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        private static string B(bool value)
        {
            return value ? "1" : "0";
        }

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return '"' + safe.Replace("\"", "\"\"") + '"';
        }

        private static string Xml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static string PhaseKey(string scenario, string phase)
        {
            return (scenario ?? string.Empty) + "\n" + (phase ?? string.Empty);
        }

        private static string Sanitize(string value)
        {
            StringBuilder output = new StringBuilder(value.Length);
            foreach (char character in value.ToLowerInvariant())
            {
                output.Append(char.IsLetterOrDigit(character) ? character : '-');
            }

            return output.ToString().Trim('-');
        }

        [Serializable]
        private sealed class LatestPointer
        {
            public int schemaVersion;
            public string runId;
            public string generatedUtc;
            public bool passed;
            public string relativeDirectory;
        }
    }
}
