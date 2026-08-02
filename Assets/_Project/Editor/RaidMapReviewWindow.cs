using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Editor
{
    public sealed class RaidMapReviewWindow : EditorWindow
    {
        public const string ReviewScenePath =
            "Assets/_Project/Scenes/RaidMapReview.unity";
        private const string SeedPreference =
            "WorldBuilder.RaidMapReview.Seed";
        private const string FogPreference =
            "WorldBuilder.RaidMapReview.PreviewFog";

        private int seed = 20260730;
        private bool previewRaidFog;
        private ProceduralRaidGenerator generator;
        private Vector2 scroll;

        [MenuItem("WorldBuilder/Review/Raid Map Generator")]
        public static void Open()
        {
            RaidMapReviewWindow window = GetWindow<RaidMapReviewWindow>();
            window.titleContent = new GUIContent("Raid Map Review");
            window.minSize = new Vector2(340f, 360f);
            window.Show();
            window.OpenReviewScene();
        }

        [MenuItem("WorldBuilder/Build/Raid Map Review")]
        public static void BuildReviewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            RebuildReviewScene(openWhenFinished: true);
        }

        public static void BuildReviewSceneFromCommandLine()
        {
            RebuildReviewScene(openWhenFinished: false);
            AssetDatabase.SaveAssets();
        }

        private void OnEnable()
        {
            seed = EditorPrefs.GetInt(SeedPreference, 20260730);
            previewRaidFog = EditorPrefs.GetBool(
                FogPreference,
                false);
            ResolveGenerator();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Raid Map Review",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates the production Raid map directly in Edit Mode. " +
                "Gameplay actors stay hidden, so you can inspect terrain, " +
                "routes, rivers, bridges, and generation failures without " +
                "entering Play Mode.",
                MessageType.Info);

            seed = EditorGUILayout.IntField("Seed", seed);
            EditorPrefs.SetInt(SeedPreference, seed);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous"))
            {
                seed--;
                Generate();
            }
            if (GUILayout.Button("Generate", GUILayout.Height(28f)))
            {
                Generate();
            }
            if (GUILayout.Button("Next"))
            {
                seed++;
                Generate();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Random Seed"))
            {
                seed = new System.Random(
                    Environment.TickCount).Next(1, int.MaxValue);
                Generate();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Scene View",
                EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Top Down"))
            {
                FrameMap(topDown: true);
            }
            if (GUILayout.Button("Oblique"))
            {
                FrameMap(topDown: false);
            }
            EditorGUILayout.EndHorizontal();
            bool nextFog = EditorGUILayout.ToggleLeft(
                "Preview Raid Fog",
                previewRaidFog);
            if (nextFog != previewRaidFog)
            {
                previewRaidFog = nextFog;
                EditorPrefs.SetBool(
                    FogPreference,
                    previewRaidFog);
                ApplySceneViewFog();
            }

            EditorGUILayout.Space(8f);
            DrawSummary();

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Open Review Scene"))
            {
                OpenReviewScene();
            }
            if (GUILayout.Button("Rebuild Review Scene From Raid"))
            {
                BuildReviewScene();
                ResolveGenerator();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField(
                "Generated Map",
                EditorStyles.boldLabel);
            if (generator == null || !generator.IsGenerated)
            {
                EditorGUILayout.HelpBox(
                    "Choose a seed and press Generate.",
                    MessageType.None);
                return;
            }

            ProceduralRaidGenerator.RaidLayout layout =
                generator.CurrentLayout;
            EditorGUILayout.LabelField("Seed", generator.Seed.ToString());
            EditorGUILayout.LabelField(
                "Primary Routes",
                layout != null && layout.ForkRoad.Length > 0
                    ? "2 (crossroads)"
                    : "1");
            int branches = layout == null
                ? 0
                : 1 +
                    (layout.BranchRoadB.Length > 0 ? 1 : 0) +
                    (layout.BranchRoadC.Length > 0 ? 1 : 0);
            EditorGUILayout.LabelField("Branches", branches.ToString());
            EditorGUILayout.LabelField(
                "Bridges",
                generator.GeneratedBridgeCount.ToString());
            EditorGUILayout.LabelField(
                "Guard Groups",
                generator.GeneratedGuardGroupCount.ToString());
            EditorGUILayout.LabelField(
                "Active Renderers",
                generator.GeneratedRendererCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Active Colliders",
                generator.GeneratedColliderCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Map Radius",
                $"{generator.MapRadius:0} m");
        }

        private void Generate()
        {
            if (!EnsureReviewSceneOpen())
            {
                return;
            }
            ResolveGenerator();
            if (generator == null)
            {
                EditorUtility.DisplayDialog(
                    "Raid Map Review",
                    "The review scene does not contain a ProceduralRaidGenerator. Rebuild it from the Raid scene.",
                    "OK");
                return;
            }

            EditorPrefs.SetInt(SeedPreference, seed);
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Raid Map Review",
                    $"Generating seed {seed}...",
                    0.35f);
                generator.GenerateWithSeed(seed);
                HideGameplayActors();
                EditorSceneManager.SaveScene(
                    generator.gameObject.scene,
                    ReviewScenePath);
                FrameMap(topDown: true);
                Repaint();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void OpenReviewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            if (!EnsureReviewSceneAsset())
            {
                return;
            }
            EditorSceneManager.OpenScene(
                ReviewScenePath,
                OpenSceneMode.Single);
            ResolveGenerator();
            HideGameplayActors();
            FrameMap(topDown: true);
        }

        private bool EnsureReviewSceneOpen()
        {
            if (!EnsureReviewSceneAsset())
            {
                return false;
            }
            Scene active = SceneManager.GetActiveScene();
            if (active.path != ReviewScenePath)
            {
                EditorSceneManager.OpenScene(
                    ReviewScenePath,
                    OpenSceneMode.Single);
            }
            return true;
        }

        private static bool EnsureReviewSceneAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    ReviewScenePath) != null)
            {
                return true;
            }
            return RebuildReviewScene(openWhenFinished: false);
        }

        private static bool RebuildReviewScene(bool openWhenFinished)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GameplaySceneRegistry.RaidPrototypeScenePath) == null)
            {
                Debug.LogError(
                    "Build the Raid Prototype scene before building its map-review scene.");
                return false;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.path == ReviewScenePath)
            {
                EditorSceneManager.OpenScene(
                    GameplaySceneRegistry.RaidPrototypeScenePath,
                    OpenSceneMode.Single);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    ReviewScenePath) != null)
            {
                AssetDatabase.DeleteAsset(ReviewScenePath);
            }
            if (!AssetDatabase.CopyAsset(
                    GameplaySceneRegistry.RaidPrototypeScenePath,
                    ReviewScenePath))
            {
                Debug.LogError("Could not create the Raid map-review scene.");
                return false;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene review = EditorSceneManager.OpenScene(
                ReviewScenePath,
                OpenSceneMode.Single);
            PrepareReviewScene(review);
            EditorSceneManager.SaveScene(review, ReviewScenePath);
            if (!openWhenFinished)
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
            Debug.Log(
                $"Raid map-review scene built at {ReviewScenePath}.");
            return true;
        }

        private static void PrepareReviewScene(Scene scene)
        {
            ProceduralRaidGenerator reviewGenerator =
                UnityEngine.Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            if (reviewGenerator != null)
            {
                reviewGenerator.enabled = false;
            }
            HideGameplayActors();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void HideGameplayActors()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.SetActive(false);
            }
            EnemyBrain[] enemies =
                UnityEngine.Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < enemies.Length; index++)
            {
                enemies[index].gameObject.SetActive(false);
            }
            Camera[] cameras =
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                cameras[index].gameObject.SetActive(false);
            }
            RaidPickup[] pickups =
                UnityEngine.Object.FindObjectsByType<RaidPickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < pickups.Length; index++)
            {
                pickups[index].gameObject.SetActive(false);
            }
            ExtractionZone[] extractionZones =
                UnityEngine.Object.FindObjectsByType<ExtractionZone>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < extractionZones.Length; index++)
            {
                extractionZones[index].gameObject.SetActive(false);
            }
        }

        private void ResolveGenerator()
        {
            generator = UnityEngine.Object.FindFirstObjectByType<
                ProceduralRaidGenerator>(
                FindObjectsInactive.Include);
        }

        private void FrameMap(bool topDown)
        {
            ResolveGenerator();
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = GetWindow<SceneView>();
            }
            float radius = generator != null
                ? generator.MapRadius
                : 144f;
            sceneView.cameraMode = SceneView.GetBuiltinCameraMode(
                DrawCameraMode.Textured);
            sceneView.sceneLighting = true;
            sceneView.sceneViewState.showFog = previewRaidFog;
            sceneView.sceneViewState.showSkybox = true;
            sceneView.in2DMode = false;
            sceneView.orthographic = topDown;
            sceneView.pivot = Vector3.zero;
            sceneView.rotation = topDown
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.Euler(58f, 0f, 35f);
            sceneView.size = topDown
                ? radius * 1.08f
                : radius * 0.88f;
            Selection.activeObject = null;
            sceneView.Show();
            sceneView.Focus();
            sceneView.Repaint();
        }

        private void ApplySceneViewFog()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }
            sceneView.sceneViewState.showFog = previewRaidFog;
            sceneView.Repaint();
        }
    }
}
