using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldBuilder.Gameplay.Diagnostics;

namespace WorldBuilder.Editor
{
    public sealed class CombatLabDiagnosticsWindow : EditorWindow
    {
        private const string BaselinePath = "Assets/_Project/Diagnostics/AcceptedCombatLabBaseline.json";
        private Vector2 scroll;
        private string latestText;
        private string reviewNotes;
        private ReviewVerdict reviewVerdict;
        private string loadedRunId;
        private string promotionStatus;

        [MenuItem("WorldBuilder/Diagnostics/Combat Lab Diagnostics")]
        public static void Open()
        {
            GetWindow<CombatLabDiagnosticsWindow>("Combat Lab Diagnostics");
        }

        private void OnEnable()
        {
            RefreshLatest();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("AI-facing movement + combat diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs the actual Combat Lab scene at a frame-locked 60 Hz through production input, motor, " +
                "Animator, physics, weapon, damage, health, and event paths. F9 records free play; F10 adds a " +
                "screenshot marker. Timestamped runs are preserved under Artifacts/CombatLabDiagnostics.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || CombatLabDiagnosticsOrchestrator.IsRunning))
            {
                if (GUILayout.Button("Run Deterministic Full Suite", GUILayout.Height(38f)))
                {
                    CombatLabDiagnosticsOrchestrator.RunInteractive();
                }

                if (GUILayout.Button("Capture Isolated Animator Cycles", GUILayout.Height(26f)))
                {
                    LocomotionDiagnosticsRunner.Capture();
                    RefreshLatest();
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                {
                    RefreshLatest();
                }

                if (GUILayout.Button("Reveal Latest Run"))
                {
                    string directory = CombatLabDiagnosticArtifacts.ResolveLatestRunDirectory();
                    if (!string.IsNullOrEmpty(directory))
                    {
                        EditorUtility.RevealInFinder(directory);
                    }
                }

                if (GUILayout.Button("Open AI Report"))
                {
                    string report = CombatLabDiagnosticArtifacts.ResolveLatestFile("ai_report.md");
                    if (!string.IsNullOrEmpty(report))
                    {
                        EditorUtility.OpenWithDefaultApp(report);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Creator review", EditorStyles.boldLabel);
            reviewVerdict = (ReviewVerdict)EditorGUILayout.EnumPopup("Verdict", reviewVerdict);
            EditorGUILayout.LabelField("Describe what feels right or wrong in your own language:");
            reviewNotes = EditorGUILayout.TextArea(reviewNotes ?? string.Empty, GUILayout.MinHeight(64f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Review With Latest Run"))
                {
                    CombatLabDiagnosticArtifacts.SaveReview(reviewVerdict.ToString(), reviewNotes);
                    RefreshLatest();
                }

                bool canPromote = CombatLabDiagnosticArtifacts.CanPromoteLatestBaseline(out promotionStatus);
                using (new EditorGUI.DisabledScope(!canPromote))
                {
                    if (GUILayout.Button("Promote Accepted Baseline"))
                    {
                        CombatLabDiagnosticArtifacts.PromoteLatestBaseline(reviewNotes, BaselinePath);
                        RefreshLatest();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(promotionStatus))
            {
                EditorGUILayout.HelpBox(promotionStatus, MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Latest AI handoff", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(latestText ?? "No full-scene diagnostic run exists yet.", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RefreshLatest()
        {
            CombatLabDiagnosticArtifacts.WriteBaselineComparison(BaselinePath);
            GameplayDiagnosticReport report = CombatLabDiagnosticArtifacts.LoadLatestReport();
            string currentRunId = report != null ? report.runId : string.Empty;
            if (!string.Equals(loadedRunId, currentRunId, StringComparison.Ordinal))
            {
                loadedRunId = currentRunId;
                reviewVerdict = ReviewVerdict.Unreviewed;
                reviewNotes = string.Empty;
            }

            if (CombatLabDiagnosticArtifacts.TryLoadLatestReview(
                out string savedVerdict,
                out string savedNotes,
                out string reviewedRunId) && reviewedRunId == currentRunId)
            {
                Enum.TryParse(savedVerdict, out reviewVerdict);
                reviewNotes = savedNotes;
            }

            string path = CombatLabDiagnosticArtifacts.ResolveLatestFile("ai_report.md");
            latestText = !string.IsNullOrEmpty(path) && File.Exists(path)
                ? File.ReadAllText(path)
                : "No full-scene diagnostic run exists yet.";
            string comparison = CombatLabDiagnosticArtifacts.ResolveLatestFile("comparison.md");
            if (!string.IsNullOrEmpty(comparison) && File.Exists(comparison))
            {
                latestText += "\n\n" + File.ReadAllText(comparison);
            }

            Repaint();
        }

        private enum ReviewVerdict
        {
            Unreviewed,
            Accepted,
            Mixed,
            Rejected
        }
    }

    [InitializeOnLoad]
    public static class CombatLabDiagnosticsOrchestrator
    {
        private const string RunningKey = "WorldBuilder.CombatDiagnostics.Running";
        private const string BatchKey = "WorldBuilder.CombatDiagnostics.Batch";
        private const string FailureKey = "WorldBuilder.CombatDiagnostics.Failed";
        private const string OutputKey = "WorldBuilder.CombatDiagnostics.Output";
        private const string CompletionSeenKey = "WorldBuilder.CombatDiagnostics.CompletionSeen";
        private const string SourceRevisionKey = "WorldBuilder.CombatDiagnostics.SourceRevision";
        private static double startedAt;

        static CombatLabDiagnosticsOrchestrator()
        {
            EditorApplication.playModeStateChanged -= StampFreePlaySourceRevision;
            EditorApplication.playModeStateChanged += StampFreePlaySourceRevision;
            if (SessionState.GetBool(RunningKey, false))
            {
                startedAt = EditorApplication.timeSinceStartup;
                Subscribe();
            }
        }

        public static bool IsRunning => SessionState.GetBool(RunningKey, false);

        private static void StampFreePlaySourceRevision(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || IsRunning)
            {
                return;
            }

            GameplayDiagnosticRecorder recorder =
                UnityEngine.Object.FindFirstObjectByType<GameplayDiagnosticRecorder>();
            recorder?.SetSourceRevision(CaptureSourceRevision());
        }

        public static void RunInteractive()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Start(false);
        }

        public static void RunBatch()
        {
            Start(true);
        }

        private static void Start(bool batch)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A Combat Lab diagnostic run is already active.");
            }

            if (!File.Exists(CombatLabSceneBuilder.ScenePath))
            {
                CombatLabSceneBuilder.Build();
            }

            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetBool(FailureKey, false);
            SessionState.SetBool(CompletionSeenKey, false);
            SessionState.SetString(SourceRevisionKey, CaptureSourceRevision());
            SessionState.EraseString(OutputKey);
            startedAt = EditorApplication.timeSinceStartup;
            Subscribe();
            EditorSceneManager.OpenScene(CombatLabSceneBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            CombatLabDiagnosticScenarioRunner.SuiteCompleted -= OnSuiteCompleted;
            CombatLabDiagnosticScenarioRunner.SuiteCompleted += OnSuiteCompleted;
        }

        private static void Unsubscribe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            CombatLabDiagnosticScenarioRunner.SuiteCompleted -= OnSuiteCompleted;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && IsRunning)
            {
                startedAt = EditorApplication.timeSinceStartup;
                try
                {
                    GameplayDiagnosticRecorder recorder =
                        UnityEngine.Object.FindFirstObjectByType<GameplayDiagnosticRecorder>();
                    if (recorder == null)
                    {
                        GameObject diagnostics = new GameObject("Runtime Diagnostic Harness");
                        recorder = diagnostics.AddComponent<GameplayDiagnosticRecorder>();
                    }

                    recorder.SetSourceRevision(SessionState.GetString(SourceRevisionKey, "unavailable"));
                    CombatLabDiagnosticScenarioRunner runner =
                        recorder.gameObject.AddComponent<CombatLabDiagnosticScenarioRunner>();
                    runner.Configure(recorder);
                    runner.StartSuite();
                }
                catch (Exception exception)
                {
                    SessionState.SetBool(FailureKey, true);
                    Debug.LogException(exception);
                    EditorApplication.isPlaying = false;
                }
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && IsRunning &&
                !SessionState.GetBool(CompletionSeenKey, false))
            {
                CombatLabDiagnosticScenarioRunner runner =
                    UnityEngine.Object.FindFirstObjectByType<CombatLabDiagnosticScenarioRunner>();
                runner?.AbortImmediately("Play mode exited before the suite completed.");
            }
            else if (change == PlayModeStateChange.EnteredEditMode && IsRunning)
            {
                FinishEditorRun();
            }
        }

        private static void OnSuiteCompleted(GameplayDiagnosticCompletion completion)
        {
            SessionState.SetBool(CompletionSeenKey, true);
            SessionState.SetString(OutputKey, completion.OutputDirectory ?? string.Empty);
            if (completion.Report == null || !completion.Report.completed || !completion.Report.passed)
            {
                SessionState.SetBool(FailureKey, true);
            }

            EditorApplication.isPlaying = false;
        }

        private static void Tick()
        {
            if (!IsRunning)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - startedAt > 180d)
            {
                SessionState.SetBool(FailureKey, true);
                Debug.LogError("COMBAT_LAB_DIAGNOSTICS_TIMEOUT: exceeded 180 seconds.");
                CombatLabDiagnosticScenarioRunner runner =
                    UnityEngine.Object.FindFirstObjectByType<CombatLabDiagnosticScenarioRunner>();
                if (runner != null)
                {
                    runner.AbortImmediately("The deterministic suite exceeded its 180-second timeout.");
                }
                else
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!IsRunning || !EditorApplication.isPlaying ||
                (!string.IsNullOrEmpty(stackTrace) && stackTrace.Contains("UnityEditor.Search")))
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                SessionState.SetBool(FailureKey, true);
                CombatLabDiagnosticScenarioRunner runner =
                    UnityEngine.Object.FindFirstObjectByType<CombatLabDiagnosticScenarioRunner>();
                runner?.RequestAbort($"Unity logged {type}: {condition}");
            }
        }

        private static void FinishEditorRun()
        {
            bool failed = SessionState.GetBool(FailureKey, false);
            bool batch = SessionState.GetBool(BatchKey, false);
            string output = SessionState.GetString(OutputKey, string.Empty);
            bool completionSeen = SessionState.GetBool(CompletionSeenKey, false);
            failed |= !completionSeen || string.IsNullOrWhiteSpace(output) ||
                !File.Exists(Path.Combine(output ?? string.Empty, "report.json"));
            SessionState.SetBool(RunningKey, false);
            SessionState.SetBool(BatchKey, false);
            SessionState.SetBool(CompletionSeenKey, false);
            Unsubscribe();
            try
            {
                if (!string.IsNullOrWhiteSpace(output))
                {
                    CombatLabDiagnosticArtifacts.WriteBaselineComparison(
                        "Assets/_Project/Diagnostics/AcceptedCombatLabBaseline.json");
                }

                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }

            Debug.Log($"COMBAT_LAB_DIAGNOSTICS_{(failed ? "FAILED" : "PASSED")}:{output}");
            if (batch)
            {
                EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        private static string CaptureSourceRevision()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(Path.Combine(projectRoot, ".git")))
                {
                    return "unavailable";
                }

                string revision = RunGit(projectRoot, "rev-parse --short=12 HEAD");
                string status = RunGit(projectRoot, "status --porcelain");
                return string.IsNullOrWhiteSpace(revision)
                    ? "unavailable"
                    : revision.Trim() + (string.IsNullOrWhiteSpace(status) ? string.Empty : "+dirty");
            }
            catch
            {
                return "unavailable";
            }
        }

        private static string RunGit(string workingDirectory, string arguments)
        {
            using System.Diagnostics.Process process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return process.ExitCode == 0 ? output : string.Empty;
        }
    }

    public static class CombatLabDiagnosticArtifacts
    {
        public static string ResolveLatestRunDirectory()
        {
            string pointerPath = Path.Combine(GameplayDiagnosticRecorder.ArtifactRoot, "latest.json");
            if (!File.Exists(pointerPath))
            {
                return string.Empty;
            }

            LatestPointer pointer = JsonUtility.FromJson<LatestPointer>(File.ReadAllText(pointerPath));
            return pointer == null || string.IsNullOrWhiteSpace(pointer.relativeDirectory)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(GameplayDiagnosticRecorder.ArtifactRoot, pointer.relativeDirectory));
        }

        public static string ResolveLatestFile(string fileName)
        {
            string directory = ResolveLatestRunDirectory();
            return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, fileName);
        }

        public static void SaveReview(string verdict, string notes)
        {
            string directory = ResolveLatestRunDirectory();
            GameplayDiagnosticReport report = LoadLatestReport();
            if (string.IsNullOrEmpty(directory) || report == null)
            {
                return;
            }

            Review review = new Review
            {
                schemaVersion = GameplayDiagnosticSchema.Version,
                runId = report.runId,
                savedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                verdict = verdict,
                notes = notes ?? string.Empty
            };
            File.WriteAllText(Path.Combine(directory, "creator_review.json"), JsonUtility.ToJson(review, true));
        }

        public static void PromoteLatestBaseline(string notes, string baselineAssetPath)
        {
            GameplayDiagnosticReport report = LoadLatestReport();
            if (report == null)
            {
                throw new InvalidOperationException("No diagnostic report is available to promote.");
            }

            if (!CanPromoteLatestBaseline(out string reason))
            {
                throw new InvalidOperationException(reason);
            }

            Review review = LoadReview(ResolveLatestRunDirectory());

            string absolute = Path.GetFullPath(baselineAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            AcceptedBaseline baseline = new AcceptedBaseline
            {
                schemaVersion = GameplayDiagnosticSchema.Version,
                acceptedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceRunId = report.runId,
                creatorNotes = review?.notes ?? notes ?? string.Empty,
                report = report
            };
            File.WriteAllText(absolute, JsonUtility.ToJson(baseline, true));
            AssetDatabase.Refresh();
            WriteBaselineComparison(baselineAssetPath);
        }

        public static void WriteBaselineComparison(string baselineAssetPath)
        {
            GameplayDiagnosticReport latest = LoadLatestReport();
            string latestDirectory = ResolveLatestRunDirectory();
            if (latest == null || string.IsNullOrEmpty(latestDirectory))
            {
                return;
            }

            string baselineAbsolute = Path.GetFullPath(baselineAssetPath);
            if (!File.Exists(baselineAbsolute))
            {
                File.WriteAllText(
                    Path.Combine(latestDirectory, "comparison.md"),
                    "# Accepted-baseline comparison\n\nNo creator-accepted full-scene baseline has been promoted yet. " +
                    "The current run is preserved but is not treated as taste canon.\n");
                return;
            }

            AcceptedBaseline baseline = JsonUtility.FromJson<AcceptedBaseline>(File.ReadAllText(baselineAbsolute));
            if (baseline?.report == null)
            {
                return;
            }

            List<MetricDelta> deltas = BuildDeltas(baseline.report, latest);
            Comparison comparison = new Comparison
            {
                schemaVersion = GameplayDiagnosticSchema.Version,
                baselineRunId = baseline.sourceRunId,
                currentRunId = latest.runId,
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                deltas = deltas.ToArray()
            };
            File.WriteAllText(Path.Combine(latestDirectory, "comparison.json"), JsonUtility.ToJson(comparison, true));
            File.WriteAllText(Path.Combine(latestDirectory, "comparison.md"), BuildComparisonMarkdown(baseline, latest, deltas));
        }

        public static GameplayDiagnosticReport LoadLatestReport()
        {
            string path = ResolveLatestFile("report.json");
            return !string.IsNullOrEmpty(path) && File.Exists(path)
                ? JsonUtility.FromJson<GameplayDiagnosticReport>(File.ReadAllText(path))
                : null;
        }

        public static bool TryLoadLatestReview(out string verdict, out string notes, out string runId)
        {
            Review review = LoadReview(ResolveLatestRunDirectory());
            verdict = review?.verdict ?? string.Empty;
            notes = review?.notes ?? string.Empty;
            runId = review?.runId ?? string.Empty;
            return review != null;
        }

        public static bool CanPromoteLatestBaseline(out string reason)
        {
            GameplayDiagnosticReport report = LoadLatestReport();
            if (report == null)
            {
                reason = "Run the deterministic full suite before promoting a baseline.";
                return false;
            }

            if (!string.Equals(report.runKind, "deterministic-full-suite", StringComparison.Ordinal) ||
                !report.completed || !report.passed || report.schemaVersion != GameplayDiagnosticSchema.Version)
            {
                reason = "Only a completed, functionally passing full-suite run using the current schema can be promoted.";
                return false;
            }

            Review review = LoadReview(ResolveLatestRunDirectory());
            if (review == null || review.schemaVersion != GameplayDiagnosticSchema.Version ||
                review.runId != report.runId || review.verdict != "Accepted")
            {
                reason = "Save an Accepted creator review for this exact run before promoting it.";
                return false;
            }

            reason = "This run has a persisted Accepted review and is eligible for baseline promotion.";
            return true;
        }

        private static Review LoadReview(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            string path = Path.Combine(directory, "creator_review.json");
            return File.Exists(path) ? JsonUtility.FromJson<Review>(File.ReadAllText(path)) : null;
        }

        private static List<MetricDelta> BuildDeltas(
            GameplayDiagnosticReport baseline,
            GameplayDiagnosticReport current)
        {
            List<MetricDelta> deltas = new List<MetricDelta>();
            GameplayDiagnosticPhaseSummary[] beforePhases =
                baseline.phases ?? Array.Empty<GameplayDiagnosticPhaseSummary>();
            GameplayDiagnosticPhaseSummary[] currentPhases =
                current.phases ?? Array.Empty<GameplayDiagnosticPhaseSummary>();
            foreach (GameplayDiagnosticPhaseSummary before in beforePhases)
            {
                if (!currentPhases.Any(now => now.scenario == before.scenario && now.phase == before.phase))
                {
                    AddPresenceDelta(deltas, before.scenario, before.phase, 1f, 0f, "missing-current");
                }
            }

            foreach (GameplayDiagnosticPhaseSummary now in currentPhases)
            {
                GameplayDiagnosticPhaseSummary before = beforePhases
                    .FirstOrDefault(candidate => candidate.scenario == now.scenario && candidate.phase == now.phase);
                if (before == null)
                {
                    AddPresenceDelta(deltas, now.scenario, now.phase, 0f, 1f, "new-current");
                    continue;
                }

                AddDelta(deltas, now, "steadySpeed", before.steadySpeed, now.steadySpeed);
                AddDelta(deltas, now, "maximumVelocityFacingError", before.maximumVelocityFacingError,
                    now.maximumVelocityFacingError);
                AddDelta(deltas, now, "maximumPoseFacingError", before.maximumPoseFacingError,
                    now.maximumPoseFacingError);
                AddDelta(deltas, now, "maximumFootFrameTravel", before.maximumFootFrameTravel,
                    now.maximumFootFrameTravel);
                AddDelta(deltas, now, "leftFootMinimumGroundGap", before.leftFootMinimumGroundGap,
                    now.leftFootMinimumGroundGap);
                AddDelta(deltas, now, "rightFootMinimumGroundGap", before.rightFootMinimumGroundGap,
                    now.rightFootMinimumGroundGap);
                AddDelta(deltas, now, "leftContactSlipRate", before.leftContactSlipRate, now.leftContactSlipRate);
                AddDelta(deltas, now, "rightContactSlipRate", before.rightContactSlipRate, now.rightContactSlipRate);
                AddDelta(deltas, now, "leftElbowLateralRange", before.leftElbowLateralRange,
                    now.leftElbowLateralRange);
                AddDelta(deltas, now, "rightElbowLateralRange", before.rightElbowLateralRange,
                    now.rightElbowLateralRange);
                AddDelta(deltas, now, "maximumHeadChestAngle", before.maximumHeadChestAngle,
                    now.maximumHeadChestAngle);
                AddDelta(deltas, now, "maximumHeadAngularSpeed", before.maximumHeadAngularSpeed,
                    now.maximumHeadAngularSpeed);
                AddDelta(deltas, now, "cameraDistanceRange", before.cameraDistanceRange, now.cameraDistanceRange);
                AddDelta(deltas, now, "settledRearKneeSurfaceGapMedian",
                    before.settledRearKneeSurfaceGapMedian, now.settledRearKneeSurfaceGapMedian);
                AddDelta(deltas, now, "settledFrontFootPlantErrorP90",
                    before.settledFrontFootPlantErrorP90, now.settledFrontFootPlantErrorP90);
                AddDelta(deltas, now, "settledSpinePitchP90",
                    before.settledSpinePitchP90, now.settledSpinePitchP90);
                AddDelta(deltas, now, "settledPelvisHeightRatioMedian",
                    before.settledPelvisHeightRatioMedian, now.settledPelvisHeightRatioMedian);
                AddDelta(deltas, now, "settledRearHipHeelDistanceRatioMedian",
                    before.settledRearHipHeelDistanceRatioMedian, now.settledRearHipHeelDistanceRatioMedian);
                AddDelta(deltas, now, "verticalRange", before.verticalRange, now.verticalRange);
                AddDelta(deltas, now, "effectiveDamage", before.effectiveDamage, now.effectiveDamage);
                AddDelta(deltas, now, "overkillDamage", before.overkillDamage, now.overkillDamage);
            }

            return deltas;
        }

        private static void AddDelta(
            List<MetricDelta> deltas,
            GameplayDiagnosticPhaseSummary phase,
            string metric,
            float baseline,
            float current)
        {
            deltas.Add(new MetricDelta
            {
                scenario = phase.scenario,
                phase = phase.phase,
                metric = metric,
                baseline = baseline,
                current = current,
                absoluteDelta = current - baseline,
                relativeDelta = (current - baseline) /
                    Mathf.Max(Mathf.Abs(baseline), NormalizationFloor(metric)),
                status = "matched"
            });
        }

        private static void AddPresenceDelta(
            List<MetricDelta> deltas,
            string scenario,
            string phase,
            float baseline,
            float current,
            string status)
        {
            deltas.Add(new MetricDelta
            {
                scenario = scenario,
                phase = phase,
                metric = "phasePresence",
                baseline = baseline,
                current = current,
                absoluteDelta = current - baseline,
                relativeDelta = current - baseline,
                status = status
            });
        }

        private static float NormalizationFloor(string metric)
        {
            if (metric.Contains("Angle", StringComparison.Ordinal) ||
                metric.Contains("Facing", StringComparison.Ordinal) ||
                metric.Contains("Pitch", StringComparison.Ordinal) ||
                metric.Contains("Angular", StringComparison.Ordinal))
            {
                return 1f;
            }

            if (metric.Contains("Speed", StringComparison.Ordinal) ||
                metric.Contains("Rate", StringComparison.Ordinal))
            {
                return 0.1f;
            }

            return 0.01f;
        }

        private static string BuildComparisonMarkdown(
            AcceptedBaseline baseline,
            GameplayDiagnosticReport current,
            List<MetricDelta> deltas)
        {
            StringBuilder output = new StringBuilder(4096);
            output.AppendLine("# Accepted-baseline comparison").AppendLine();
            output.Append("Accepted run: `").Append(baseline.sourceRunId).AppendLine("`");
            output.Append("Current run: `").Append(current.runId).AppendLine("`");
            if (!string.IsNullOrWhiteSpace(baseline.creatorNotes))
            {
                output.Append("Creator acceptance note: ").AppendLine(baseline.creatorNotes);
            }

            output.AppendLine().AppendLine("Largest normalized changes:").AppendLine();
            foreach (MetricDelta delta in deltas.OrderByDescending(item => Mathf.Abs(item.relativeDelta)).Take(20))
            {
                output.Append("- `").Append(delta.scenario).Append('/').Append(delta.phase).Append("` ")
                    .Append(delta.metric).Append(": ")
                    .Append(delta.baseline.ToString("0.###", CultureInfo.InvariantCulture)).Append(" → ")
                    .Append(delta.current.ToString("0.###", CultureInfo.InvariantCulture)).Append(" (Δ ")
                    .Append(delta.absoluteDelta.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture))
                    .Append(")");
                if (!string.Equals(delta.status, "matched", StringComparison.Ordinal))
                {
                    output.Append(" **").Append(delta.status).Append("**");
                }

                output.AppendLine();
            }

            return output.ToString();
        }

        [Serializable]
        private sealed class LatestPointer
        {
            public string relativeDirectory;
        }

        [Serializable]
        private sealed class Review
        {
            public int schemaVersion;
            public string runId;
            public string savedUtc;
            public string verdict;
            public string notes;
        }

        [Serializable]
        private sealed class AcceptedBaseline
        {
            public int schemaVersion;
            public string acceptedUtc;
            public string sourceRunId;
            public string creatorNotes;
            public GameplayDiagnosticReport report;
        }

        [Serializable]
        private sealed class Comparison
        {
            public int schemaVersion;
            public string baselineRunId;
            public string currentRunId;
            public string generatedUtc;
            public MetricDelta[] deltas;
        }

        [Serializable]
        private sealed class MetricDelta
        {
            public string scenario;
            public string phase;
            public string metric;
            public float baseline;
            public float current;
            public float absoluteDelta;
            public float relativeDelta;
            public string status;
        }
    }
}
