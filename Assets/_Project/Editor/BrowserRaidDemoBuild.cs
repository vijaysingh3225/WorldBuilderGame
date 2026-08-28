using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WorldBuilder.Editor
{
    public static class BrowserRaidDemoBuild
    {
        public const string DefaultOutputPath =
            "Artifacts/RaidBrowserDemo";

        private static readonly string[] DemoScenes =
        {
            GameplaySceneRegistry.BootstrapScenePath,
            GameplaySceneRegistry.RaidPrototypeScenePath
        };

        [MenuItem("WorldBuilder/Build/Browser Raid Demo")]
        public static void BuildFromMenu()
        {
            Build(DefaultOutputPath);
        }

        [MenuItem("WorldBuilder/Build/Browser Raid Demo and Run")]
        public static void BuildAndRunFromMenu()
        {
            Build(DefaultOutputPath, BuildOptions.AutoRunPlayer);
        }

        [MenuItem("WorldBuilder/Play/Browser Raid Demo")]
        public static void PlayInEditor()
        {
            PlayerPrefs.SetInt(
                WorldBuilder.Gameplay.Loop.Scenes.BrowserRaidDemoController
                    .EditorPreviewPreference,
                1);
            GameplayLoopSceneBuilder.BuildBootstrapOnly();
            EditorApplication.isPlaying = true;
        }

        public static void BuildFromCommandLine()
        {
            Build(ResolveCommandLineOutputPath());
        }

        public static BuildReport Build(string outputPath)
        {
            return Build(outputPath, BuildOptions.None);
        }

        private static BuildReport Build(
            string outputPath,
            BuildOptions buildOptions)
        {
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL))
            {
                throw new BuildFailedException(
                    "Unity Web Build Support is not installed for this " +
                    "Editor version. Add it in Unity Hub, then run the " +
                    "browser demo build again.");
            }

            string normalizedOutput = string.IsNullOrWhiteSpace(outputPath)
                ? DefaultOutputPath
                : outputPath.Trim();
            string absoluteOutput = Path.GetFullPath(normalizedOutput);
            Directory.CreateDirectory(absoluteOutput);

            GameplayLoopSceneBuilder.BuildBootstrapFromCommandLine();

            PlayerSettings.productName = "World Builder Raid Prototype";
            PlayerSettings.WebGL.compressionFormat =
                WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.SetManagedStrippingLevel(
                NamedBuildTarget.WebGL,
                ManagedStrippingLevel.High);

            var options = new BuildPlayerOptions
            {
                scenes = DemoScenes,
                locationPathName = absoluteOutput,
                target = BuildTarget.WebGL,
                options = buildOptions,
                extraScriptingDefines = new[]
                {
                    "WORLD_BUILDER_RAID_DEMO"
                }
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Browser raid demo build ended with {summary.result}. " +
                    $"Errors: {summary.totalErrors}; " +
                    $"warnings: {summary.totalWarnings}.");
            }

            Debug.Log(
                $"Browser raid demo built at '{absoluteOutput}' " +
                $"({summary.totalSize / (1024d * 1024d):0.0} MB).");
            return report;
        }

        private static string ResolveCommandLineOutputPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(
                        arguments[index],
                        "-browserDemoOutput",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return DefaultOutputPath;
        }
    }
}
