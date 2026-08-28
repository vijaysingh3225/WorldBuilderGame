using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace WorldBuilder.Editor
{
    [InitializeOnLoad]
    public static class ColumnBladeValidation
    {
        // Marker requests keep focused feature verification reproducible after
        // generator, engraving-path, and inset-panel changes reload scripts;
        // the update queue also survives temporarily entering Play mode.
        public const string RequestPath =
            "Temp/WorldBuilder.RunColumnBladeTests";
        public const string ResultPath =
            "Temp/WorldBuilder.ColumnBladeTestResults.txt";
        public const string CombatLabResultPath =
            "Temp/WorldBuilder.CombatLabColumnBladeTestResults.txt";
        public const string CombatLabRequestPath =
            "Temp/WorldBuilder.RunCombatLabColumnBladeTests";

        private static TestRunnerApi runner;
        private static bool runQueued;
        private static Filter pendingFilter;
        private static string pendingResultPath;
        private static string pendingLabel;

        static ColumnBladeValidation()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        [MenuItem("WorldBuilder/Validate Column Blade Generator %#b")]
        public static void Run()
        {
            RunScope(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    categoryNames = new[] { "ColumnBlade" }
                },
                ResultPath,
                "Column Blade");
        }

        [MenuItem("WorldBuilder/Validate Column Blade Inset %#i")]
        public static void RunInset()
        {
            RunScope(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    categoryNames = new[] { "ColumnBladeInset" }
                },
                "Temp/WorldBuilder.ColumnBladeInsetTestResults.txt",
                "Column Blade Inset");
        }

        [MenuItem("WorldBuilder/Validate Combat Lab Column Blades")]
        public static void RunCombatLabIntegration()
        {
            RunScope(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    categoryNames = new[] { "CombatLabColumnBlade" }
                },
                CombatLabResultPath,
                "Combat Lab Column Blade");
        }

        private static void RunScope(
            Filter filter,
            string resultPath,
            string label)
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!runQueued)
                {
                    runQueued = true;
                    pendingFilter = filter;
                    pendingResultPath = resultPath;
                    pendingLabel = label;
                    EditorApplication.update += RunWhenReady;
                }
                return;
            }
            runQueued = false;
            if (runner != null)
            {
                return;
            }
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Callbacks(resultPath, label));
            runner.Execute(new ExecutionSettings(filter));
        }

        private static void RunWhenReady()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            EditorApplication.update -= RunWhenReady;
            runQueued = false;
            Filter filter = pendingFilter;
            string resultPath = pendingResultPath;
            string label = pendingLabel;
            pendingFilter = null;
            pendingResultPath = null;
            pendingLabel = null;
            RunScope(filter, resultPath, label);
        }

        private static void RunIfRequested()
        {
            if (File.Exists(CombatLabRequestPath))
            {
                File.Delete(CombatLabRequestPath);
                QueueScope(
                    new Filter
                    {
                        testMode = TestMode.EditMode,
                        categoryNames = new[] { "CombatLabColumnBlade" }
                    },
                    CombatLabResultPath,
                    "Combat Lab Column Blade");
                return;
            }
            if (!File.Exists(RequestPath))
            {
                return;
            }
            File.Delete(RequestPath);
            QueueScope(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    categoryNames = new[] { "ColumnBlade" }
                },
                ResultPath,
                "Column Blade");
        }

        private static void QueueScope(
            Filter filter,
            string resultPath,
            string label)
        {
            runQueued = true;
            pendingFilter = filter;
            pendingResultPath = resultPath;
            pendingLabel = label;
            EditorApplication.update -= RunWhenReady;
            EditorApplication.update += RunWhenReady;
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string resultPath;
            private readonly string label;

            public Callbacks(string resultPath, string label)
            {
                this.resultPath = resultPath;
                this.label = label;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log(
                    $"{label} validation started: " +
                    $"{testsToRun.TestCaseCount} tests selected.");
            }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.FailCount > 0)
                {
                    Debug.LogError(
                        $"{label} test failed: {result.FullName}\n" +
                        $"{result.Message}\n{result.StackTrace}");
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                File.WriteAllText(
                    resultPath,
                    $"status={result.TestStatus}\n" +
                    $"passed={result.PassCount}\n" +
                    $"failed={result.FailCount}\n" +
                    $"skipped={result.SkipCount}\n" +
                    $"duration={result.Duration:0.000}\n");
                Debug.Log(
                    $"{label} validation finished: {result.PassCount} passed, " +
                    $"{result.FailCount} failed.");
                runner = null;
            }
        }
    }
}
