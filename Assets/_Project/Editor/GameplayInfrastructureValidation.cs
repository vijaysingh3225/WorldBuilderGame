using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace WorldBuilder.Editor
{
    [InitializeOnLoad]
    public static class GameplayInfrastructureValidation
    {
        public const string RequestPath =
            "Temp/WorldBuilder.RunInfrastructureTests";
        public const string ResultPath =
            "Temp/WorldBuilder.InfrastructureTestResults.txt";
        public const string FullRequestPath =
            "Temp/WorldBuilder.RunFullEditModeTests";
        public const string FullResultPath =
            "Temp/WorldBuilder.FullEditModeTestResults.txt";

        private static TestRunnerApi runner;
        private static ResultCallbacks callbacks;

        static GameplayInfrastructureValidation()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        [MenuItem("WorldBuilder/Validate Gameplay Infrastructure")]
        public static void Run()
        {
            RunScope(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    categoryNames = new[] { "GameplayInfrastructure" }
                },
                ResultPath,
                "Gameplay infrastructure");
        }

        [MenuItem("WorldBuilder/Validate Full EditMode Suite")]
        public static void RunFullSuite()
        {
            RunScope(
                new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames =
                        new[] { "WorldBuilder.Tests.EditMode" }
                },
                FullResultPath,
                "Full EditMode suite");
        }

        private static void RunScope(
            Filter filter,
            string resultPath,
            string label)
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () =>
                    RunScope(filter, resultPath, label);
                return;
            }

            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new ResultCallbacks(resultPath, label);
            runner.RegisterCallbacks(callbacks);
            runner.Execute(new ExecutionSettings(filter));
        }

        [InitializeOnLoadMethod]
        private static void RunIfRequested()
        {
            if (File.Exists(FullRequestPath))
            {
                File.Delete(FullRequestPath);
                EditorApplication.delayCall += RunFullSuite;
                return;
            }
            if (!File.Exists(RequestPath))
            {
                return;
            }

            File.Delete(RequestPath);
            EditorApplication.delayCall += Run;
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly List<string> failures =
                new List<string>();
            private readonly string resultPath;
            private readonly string label;

            public ResultCallbacks(string resultPath, string label)
            {
                this.resultPath = resultPath;
                this.label = label;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log(
                    $"WorldBuilder {label} validation started: " +
                    $"{testsToRun.TestCaseCount} tests selected.");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string summary =
                    $"status={result.TestStatus}\n" +
                    $"passed={result.PassCount}\n" +
                    $"failed={result.FailCount}\n" +
                    $"skipped={result.SkipCount}\n" +
                    $"duration={result.Duration:0.000}\n";
                if (failures.Count > 0)
                {
                    summary +=
                        "\n" +
                        string.Join("\n\n", failures);
                }

                Directory.CreateDirectory(
                    Path.GetDirectoryName(resultPath));
                File.WriteAllText(resultPath, summary);
                Debug.Log(
                    $"WorldBuilder {label} validation " +
                    $"finished: {result.TestStatus}, " +
                    $"{result.PassCount} passed, " +
                    $"{result.FailCount} failed.");
                runner = null;
                callbacks = null;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren ||
                    result.TestStatus != TestStatus.Failed)
                {
                    return;
                }

                failures.Add(
                    $"{result.FullName}\n{result.Message}\n" +
                    $"{result.StackTrace}");
            }
        }
    }
}
