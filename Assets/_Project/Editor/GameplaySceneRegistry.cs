using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace WorldBuilder.Editor
{
    public static class GameplaySceneRegistry
    {
        public const string BootstrapScenePath =
            "Assets/_Project/Scenes/Bootstrap.unity";
        public const string HomeBaseScenePath =
            "Assets/_Project/Scenes/HomeBase.unity";
        public const string RaidPrototypeScenePath =
            "Assets/_Project/Scenes/RaidPrototype.unity";
        public const string CombatLabScenePath =
            "Assets/_Project/Scenes/CombatLab.unity";
        public const string ShortSwordGeneratorLabScenePath =
            ShortSwordGeneratorLabSceneBuilder.ScenePath;

        private static readonly string[] OrderedKnownScenePaths =
        {
            BootstrapScenePath,
            HomeBaseScenePath,
            RaidPrototypeScenePath,
            CombatLabScenePath,
            ShortSwordGeneratorLabScenePath
        };

        public static void ApplyExistingScenesToBuildSettings()
        {
            EditorBuildSettingsScene[] currentScenes =
                EditorBuildSettings.scenes;
            HashSet<string> knownPaths =
                new HashSet<string>(OrderedKnownScenePaths);
            List<EditorBuildSettingsScene> registeredScenes =
                new List<EditorBuildSettingsScene>();

            for (int index = 0;
                 index < OrderedKnownScenePaths.Length;
                 index++)
            {
                string path = OrderedKnownScenePaths[index];
                if (File.Exists(path))
                {
                    registeredScenes.Add(
                        new EditorBuildSettingsScene(path, true));
                }
            }

            for (int index = 0; index < currentScenes.Length; index++)
            {
                EditorBuildSettingsScene scene = currentScenes[index];
                if (!knownPaths.Contains(scene.path) &&
                    File.Exists(scene.path))
                {
                    registeredScenes.Add(scene);
                }
            }

            EditorBuildSettings.scenes = registeredScenes.ToArray();
        }
    }
}
