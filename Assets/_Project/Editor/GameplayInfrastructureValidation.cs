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

        private static TestRunnerApi runner;
        private static ResultCallbacks callbacks;

        static GameplayInfrastructureValidation()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        [MenuItem("WorldBuilder/Validate Gameplay Infrastructure")]
        public static void Run()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new ResultCallbacks();
            runner.RegisterCallbacks(callbacks);
            runner.Execute(
                new ExecutionSettings(
                    new Filter
                    {
                        testMode = TestMode.EditMode,
                        assemblyNames =
                            new[] { "WorldBuilder.Tests.EditMode" }
                    }));
        }

        [InitializeOnLoadMethod]
        private static void RunIfRequested()
        {
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

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log(
                    "WorldBuilder EditMode infrastructure validation started.");
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
                    Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, summary);
                Debug.Log(
                    "WorldBuilder EditMode infrastructure validation " +
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
