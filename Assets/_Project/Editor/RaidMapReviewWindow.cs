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
        private const string ShowTreesPreference =
            "WorldBuilder.RaidMapReview.ShowTrees";
        private const string ShowLandformRegionsPreference =
            "WorldBuilder.RaidMapReview.ShowLandformRegions";
        private const string ShowLandformRoutesPreference =
            "WorldBuilder.RaidMapReview.ShowLandformRoutes";
        private const string ShowScenicAnchorsPreference =
            "WorldBuilder.RaidMapReview.ShowScenicAnchors";
        private const string QualityPreference =
            "WorldBuilder.RaidMapReview.GenerationQuality";

        private int seed = 20260730;
        private bool previewRaidFog;
        private bool showTrees = true;
        private bool showLandformRegions = true;
        private bool showLandformRoutes = true;
        private bool showScenicAnchors = true;
        private ProceduralRaidGenerator.GenerationQuality generationQuality =
            ProceduralRaidGenerator.GenerationQuality.FastPreview;
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

        public static void PrepareSavedReviewFromCommandLine()
        {
            Scene review = EditorSceneManager.OpenScene(
                ReviewScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator reviewGenerator =
                UnityEngine.Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            if (reviewGenerator != null)
            {
                for (int childIndex =
                         reviewGenerator.transform.childCount - 1;
                     childIndex >= 0;
                     childIndex--)
                {
                    Transform child =
                        reviewGenerator.transform.GetChild(childIndex);
                    if (child.name.StartsWith(
                            "Generated Raid ",
                            StringComparison.Ordinal))
                    {
                        UnityEngine.Object.DestroyImmediate(
                            child.gameObject);
                    }
                }
            }
            HideGameplayActors();
            EditorSceneManager.SaveScene(review, ReviewScenePath);
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        private void OnEnable()
        {
            seed = EditorPrefs.GetInt(SeedPreference, 20260730);
            previewRaidFog = EditorPrefs.GetBool(
                FogPreference,
                false);
            showTrees = EditorPrefs.GetBool(
                ShowTreesPreference,
                true);
            showLandformRegions = EditorPrefs.GetBool(
                ShowLandformRegionsPreference,
                true);
            showLandformRoutes = EditorPrefs.GetBool(
                ShowLandformRoutesPreference,
                true);
            showScenicAnchors = EditorPrefs.GetBool(
                ShowScenicAnchorsPreference,
                true);
            generationQuality =
                (ProceduralRaidGenerator.GenerationQuality)
                EditorPrefs.GetInt(
                    QualityPreference,
                    (int)ProceduralRaidGenerator.GenerationQuality
                        .FastPreview);
            ResolveGenerator();
            EditorApplication.delayCall += ApplyTreeVisibility;
            SceneView.duringSceneGui += DrawAdvancedLandformOverlay;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= ApplyTreeVisibility;
            SceneView.duringSceneGui -= DrawAdvancedLandformOverlay;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Raid Map Review",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates a transient Raid preview directly in Edit Mode. " +
                "The generated hierarchy and meshes are never saved into " +
                "the review scene. Production quality retains every authored " +
                "ecology budget; Fast Preview is intended for layout work.",
                MessageType.Info);

            seed = EditorGUILayout.IntField("Seed", seed);
            EditorPrefs.SetInt(SeedPreference, seed);
            ProceduralRaidGenerator.GenerationQuality nextQuality =
                (ProceduralRaidGenerator.GenerationQuality)
                EditorGUILayout.EnumPopup(
                    "Generation Quality",
                    generationQuality);
            if (nextQuality != generationQuality)
            {
                generationQuality = nextQuality;
                EditorPrefs.SetInt(
                    QualityPreference,
                    (int)generationQuality);
            }

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
            bool nextShowTrees = EditorGUILayout.ToggleLeft(
                "Show Trees",
                showTrees);
            if (nextShowTrees != showTrees)
            {
                showTrees = nextShowTrees;
                EditorPrefs.SetBool(
                    ShowTreesPreference,
                    showTrees);
                ApplyTreeVisibility();
            }
            ResolveGenerator();
            if (generator != null)
            {
                bool nextAdvanced = EditorGUILayout.ToggleLeft(
                    "Use Advanced Three-Tier Landforms",
                    generator.AdvancedLandformsEnabled);
                if (nextAdvanced != generator.AdvancedLandformsEnabled)
                {
                    Undo.RecordObject(
                        generator,
                        "Toggle Advanced Raid Landforms");
                    generator.SetAdvancedLandformsEnabled(nextAdvanced);
                    EditorUtility.SetDirty(generator);
                    Generate();
                }
                ProceduralRaidGenerator.ForestFloorDebugMode nextDebug =
                    (ProceduralRaidGenerator.ForestFloorDebugMode)
                    EditorGUILayout.EnumPopup(
                        "Forest Floor Debug",
                        generator.HabitatDebugMode);
                if (nextDebug != generator.HabitatDebugMode)
                {
                    Undo.RecordObject(
                        generator,
                        "Change Forest Floor Debug Mode");
                    generator.SetForestFloorDebugMode(nextDebug);
                    EditorUtility.SetDirty(generator);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "Landform Graph Overlay",
                    EditorStyles.miniBoldLabel);
                DrawOverlayToggle(
                    "Region Boundaries",
                    ref showLandformRegions,
                    ShowLandformRegionsPreference);
                DrawOverlayToggle(
                    "Traversal Routes",
                    ref showLandformRoutes,
                    ShowLandformRoutesPreference);
                DrawOverlayToggle(
                    "Scenic Anchors",
                    ref showScenicAnchors,
                    ShowScenicAnchorsPreference);
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
                ApplyTreeVisibility();
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
                "Quality",
                ObjectNames.NicifyVariableName(
                    generator.CurrentGenerationQuality.ToString()));
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
                "Ground Flora Studies",
                generator.GeneratedGroundFloraStudyCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Flora Colonies",
                generator.GeneratedGroundFloraColonyCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Tree-base Flora",
                generator.GeneratedGroundFloraTreePocketCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Rock-shelter Flora",
                generator.GeneratedGroundFloraBoulderPocketCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Active Renderers",
                generator.GeneratedRendererCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Active Colliders",
                generator.GeneratedColliderCount.ToString("N0"));
            EditorGUILayout.LabelField(
                "Map Radius",
                $"{generator.MapRadius:0} m");
            EditorGUILayout.LabelField(
                "Generation Time",
                $"{generator.LastGenerationMilliseconds / 1000d:0.00} s");
            if (generator.GenerationStageMilliseconds.TryGetValue(
                    "ground-scenery",
                    out double sceneryMilliseconds))
            {
                EditorGUILayout.LabelField(
                    "  Scenery",
                    $"{sceneryMilliseconds:0} ms");
            }
            if (generator.GenerationStageMilliseconds.TryGetValue(
                    "terrain",
                    out double terrainMilliseconds))
            {
                EditorGUILayout.LabelField(
                    "  Terrain",
                    $"{terrainMilliseconds:0} ms");
            }
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Dominant Forest Habitats",
                EditorStyles.boldLabel);
            foreach (ProceduralRaidGenerator.ForestHabitat habitat in
                     Enum.GetValues(
                         typeof(ProceduralRaidGenerator.ForestHabitat)))
            {
                EditorGUILayout.LabelField(
                    ObjectNames.NicifyVariableName(habitat.ToString()),
                    $"{generator.DominantHabitatPercentage(habitat):0.0}%");
            }
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
                generator.SetGenerationQuality(generationQuality);
                generator.GenerateWithSeed(seed);
                HideGameplayActors();
                ApplyTreeVisibility();
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
            ApplyTreeVisibility();
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
            RaidObelisk[] obelisks =
                UnityEngine.Object.FindObjectsByType<RaidObelisk>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < obelisks.Length; index++)
            {
                obelisks[index].gameObject.SetActive(false);
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

        public static Transform FindGeneratedForestRoot(
            ProceduralRaidGenerator reviewGenerator)
        {
            if (reviewGenerator == null)
            {
                return null;
            }

            for (int childIndex = 0;
                 childIndex < reviewGenerator.transform.childCount;
                 childIndex++)
            {
                Transform generatedRaid =
                    reviewGenerator.transform.GetChild(childIndex);
                if (!generatedRaid.name.StartsWith(
                        "Generated Raid ",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Transform forest = generatedRaid.Find(
                    "Dense Stylized Forest");
                if (forest != null)
                {
                    return forest;
                }
            }

            return null;
        }

        public static void SetTreeVisibilityForReview(
            ProceduralRaidGenerator reviewGenerator,
            bool visible)
        {
            Transform forest = FindGeneratedForestRoot(
                reviewGenerator);
            if (forest == null)
            {
                return;
            }

            if (visible)
            {
                SceneVisibilityManager.instance.Show(
                    forest.gameObject,
                    true);
            }
            else
            {
                SceneVisibilityManager.instance.Hide(
                    forest.gameObject,
                    true);
            }
            SceneView.RepaintAll();
        }

        private void ApplyTreeVisibility()
        {
            ResolveGenerator();
            SetTreeVisibilityForReview(generator, showTrees);
        }

        private static void DrawOverlayToggle(
            string label,
            ref bool value,
            string preference)
        {
            bool next = EditorGUILayout.ToggleLeft(label, value);
            if (next == value)
            {
                return;
            }
            value = next;
            EditorPrefs.SetBool(preference, value);
            SceneView.RepaintAll();
        }

        private void DrawAdvancedLandformOverlay(SceneView sceneView)
        {
            ResolveGenerator();
            if (generator == null ||
                !generator.AdvancedLandformsEnabled ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (showLandformRegions)
            {
                foreach (ProceduralRaidGenerator.LandformRegion region in
                         generator.AdvancedLandformRegions)
                {
                    Handles.color = region.Tier ==
                            ProceduralRaidGenerator.LandformTier.Highland
                        ? new Color(1f, 0.42f, 0.16f, 0.95f)
                        : region.Tier ==
                            ProceduralRaidGenerator.LandformTier.MidShelf
                            ? new Color(1f, 0.82f, 0.18f, 0.9f)
                            : new Color(0.2f, 0.8f, 0.42f, 0.85f);
                    const int Samples = 64;
                    var points = new Vector3[Samples + 1];
                    float radians = region.RotationDegrees * Mathf.Deg2Rad;
                    float cosine = Mathf.Cos(radians);
                    float sine = Mathf.Sin(radians);
                    for (int index = 0; index <= Samples; index++)
                    {
                        float angle = index * Mathf.PI * 2f / Samples;
                        Vector2 local = new Vector2(
                            Mathf.Cos(angle) * region.Radii.x,
                            Mathf.Sin(angle) * region.Radii.y);
                        Vector2 rotated = new Vector2(
                            local.x * cosine - local.y * sine,
                            local.x * sine + local.y * cosine);
                        Vector2 xz = region.Center + rotated;
                        points[index] = new Vector3(
                            xz.x,
                            generator.SampleTerrainHeight(xz.x, xz.y) + 0.6f,
                            xz.y);
                    }
                    Handles.DrawAAPolyLine(3f, points);
                    Vector3 labelPosition = new Vector3(
                        region.Center.x,
                        generator.SampleTerrainHeight(
                            region.Center.x,
                            region.Center.y) + 3f,
                        region.Center.y);
                    Handles.Label(
                        labelPosition,
                        $"{region.Name}  T{(int)region.Tier}  " +
                        $"{region.TargetHeight:0.#}m");
                }
            }

            if (showLandformRoutes)
            {
                Handles.color = new Color(0.15f, 0.85f, 1f, 0.95f);
                foreach (ProceduralRaidGenerator.LandformConnection connection in
                         generator.AdvancedLandformConnections)
                {
                    var points = new Vector3[connection.Waypoints.Length];
                    for (int index = 0; index < points.Length; index++)
                    {
                        Vector2 xz = connection.Waypoints[index];
                        points[index] = new Vector3(
                            xz.x,
                            generator.SampleTerrainHeight(xz.x, xz.y) + 0.85f,
                            xz.y);
                    }
                    Handles.DrawAAPolyLine(5f, points);
                    Handles.Label(
                        points[points.Length / 2] + Vector3.up * 1.2f,
                        $"{connection.TraversalType}  " +
                        $"grade {connection.MaxGrade:0.00}");
                }
            }

            if (showScenicAnchors)
            {
                Handles.color = new Color(0.9f, 0.32f, 1f, 1f);
                foreach (ProceduralRaidGenerator.ScenicAnchor anchor in
                         generator.AdvancedScenicAnchors)
                {
                    Vector3 position =
                        generator.AdvancedScenicAnchorWorldPosition(anchor);
                    Handles.DrawWireDisc(
                        position,
                        Vector3.up,
                        anchor.ClearanceRadius);
                    Vector3 direction = new Vector3(
                        anchor.LookDirection.x,
                        0f,
                        anchor.LookDirection.y);
                    Handles.ArrowHandleCap(
                        0,
                        position + Vector3.up * 1.5f,
                        Quaternion.LookRotation(direction, Vector3.up),
                        8f,
                        EventType.Repaint);
                    Handles.Label(position + Vector3.up * 2.5f, anchor.Name);
                }
            }
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
