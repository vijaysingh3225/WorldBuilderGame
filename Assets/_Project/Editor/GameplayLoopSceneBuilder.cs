using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Editor
{
    public static class GameplayLoopSceneBuilder
    {
        // Keeps scene generation deterministic across modular rebuilds.
        public const string InfrastructureMarkerName =
            "Gameplay Loop Infrastructure - V1";
        private const string EnvironmentMeshFolder =
            "Assets/_Project/Art/Prototype/Environment";
        private const string EnvironmentAssetGalleryScenePath =
            "Assets/_Project/Scenes/EnvironmentAssetGallery.unity";
        private const string RaidTreeTrunkMeshPath =
            EnvironmentMeshFolder + "/RaidTreeTrunk.asset";
        private const string RaidTreeCanopyMeshPath =
            EnvironmentMeshFolder + "/RaidTreeCanopy.asset";
        private const string StylizedForestModelFolder =
            "Assets/_Project/Art/Environment/StylizedForest/Models/Stylized_forest_fbx";
        private const string StylizedForestTextureFolder =
            "Assets/_Project/Art/Environment/StylizedForest/Textures";
        private static readonly string[] StylizedForestTreeNames =
        {
            "SM_sf_birch_01",
            "SM_sf_birch_02",
            "SM_sf_birch_03",
            "SM_sf_tree_01",
            "SM_sf_tree_02",
            "SM_sf_tree_03",
            "SM_sf_tree_04",
            "SM_sf_pine_01",
            "SM_sf_pine_02",
            "SM_sf_pine_03",
            "SM_sf_pine_04"
        };
        private static readonly string[] StylizedForestGrassNames =
        {
            "SM_sf_grass_01",
            "SM_sf_grass_02",
            "SM_sf_grass_03",
            "SM_sf_grass_04",
            "SM_sf_grass_05"
        };
        private static readonly string[] StylizedForestUndergrowthNames =
        {
            "SM_sf_bush_01",
            "SM_sf_bush_02",
            "SM_sf_clover_01",
            "SM_sf_clover_02",
            "SM_sf_flower_01",
            "SM_sf_flower_02",
            "SM_sf_flower_03",
            "SM_sf_plnats_01",
            "SM_sf_plnats_02",
            "SM_sf_plnats_03"
        };
        private static readonly string[] StylizedForestRockNames =
        {
            "SM_rock_01",
            "SM_rock_02",
            "SM_rock_03",
            "SM_rock_04",
            "SM_rock_05",
            "SM_rock_06",
            "SM_rock_07",
            "SM_rock_08",
            "SM_rock_09"
        };
        private const string ChestPrefabPath =
            "Assets/_Project/Art/Environment/Chest/Chest.fbx";
        private const string ChestDiffusePath =
            "Assets/_Project/Art/Environment/Chest/Chest_Diffuse.png";
        private const string ChestNormalPath =
            "Assets/_Project/Art/Environment/Chest/Chest_Normal_OpenGL.png";
        private const string BridgeModelPath =
            "Assets/_Project/Art/Environment/StylizedBridge/source/Bridge_low.fbx";
        private const string BridgeTextureFolder =
            "Assets/_Project/Art/Environment/StylizedBridge/textures";

        [MenuItem("WorldBuilder/Build Gameplay Loop")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildAllWithoutPrompt();
        }

        public static void BuildAllFromCommandLine()
        {
            BuildAllWithoutPrompt();
        }

        [MenuItem("WorldBuilder/Build/Bootstrap")]
        public static void BuildBootstrapOnly()
        {
            BuildSingleScene(
                "Bootstrap",
                GameplaySceneRegistry.BootstrapScenePath,
                BuildBootstrap);
        }

        [MenuItem("WorldBuilder/Build/Home Base")]
        public static void BuildHomeBaseOnly()
        {
            BuildSingleScene(
                "Home Base",
                GameplaySceneRegistry.HomeBaseScenePath,
                BuildHomeBase);
        }

        [MenuItem("WorldBuilder/Build/Raid Prototype")]
        public static void BuildRaidPrototypeOnly()
        {
            BuildSingleScene(
                "Raid Prototype",
                GameplaySceneRegistry.RaidPrototypeScenePath,
                BuildRaidPrototype);
        }

        public static void BuildRaidPrototypeFromCommandLine()
        {
            BuildRaidPrototype();
        }

        [MenuItem("WorldBuilder/Build/Environment Asset Gallery")]
        public static void BuildEnvironmentAssetGalleryOnly()
        {
            BuildSingleScene(
                "Environment Asset Gallery",
                EnvironmentAssetGalleryScenePath,
                BuildEnvironmentAssetGallery);
        }

        public static void BuildEnvironmentAssetGalleryFromCommandLine()
        {
            BuildEnvironmentAssetGallery();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("WorldBuilder/Open/Environment Asset Gallery")]
        public static void OpenEnvironmentAssetGallery()
        {
            EditorSceneManager.OpenScene(
                EnvironmentAssetGalleryScenePath,
                OpenSceneMode.Single);
        }

        [MenuItem("WorldBuilder/Capture/Environment Asset Gallery Preview")]
        public static void CaptureEnvironmentAssetGalleryPreview()
        {
            EditorSceneManager.OpenScene(
                EnvironmentAssetGalleryScenePath,
                OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException(
                    "Environment asset gallery camera is missing.");
            }
            string outputPath =
                System.Environment.GetEnvironmentVariable(
                    "ENVIRONMENT_GALLERY_CAPTURE");
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "../Artifacts/" +
                        "EnvironmentAssetGallery.png"));
            }
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath));
            RenderTexture target =
                RenderTexture.GetTemporary(
                    1600,
                    900,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
            RenderTexture previous =
                RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image =
                    new Texture2D(
                        1600,
                        900,
                        TextureFormat.RGB24,
                        false);
                image.ReadPixels(
                    new Rect(
                        0f,
                        0f,
                        1600f,
                        900f),
                    0,
                    0);
                image.Apply(false, false);
                File.WriteAllBytes(
                    outputPath,
                    image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
            Debug.Log(
                $"Environment asset gallery captured: {outputPath}");
        }

        public static void CaptureEnvironmentAssetGalleryFromCommandLine()
        {
            CaptureEnvironmentAssetGalleryPreview();
        }

        public static void BuildHomeBaseFromCommandLine()
        {
            BuildHomeBase();
        }

        [MenuItem("WorldBuilder/Build/Combat Lab")]
        public static void BuildCombatLabOnly()
        {
            BuildSingleScene(
                "Combat Lab",
                GameplaySceneRegistry.CombatLabScenePath,
                CombatLabSceneBuilder.Build);
        }

        [MenuItem("WorldBuilder/Play/Full Loop")]
        public static void PlayFullLoop()
        {
            OpenAndPlay(GameplaySceneRegistry.BootstrapScenePath);
        }

        [MenuItem("WorldBuilder/Play/Home Base Sandbox")]
        public static void PlayHomeSandbox()
        {
            OpenAndPlay(GameplaySceneRegistry.HomeBaseScenePath);
        }

        [MenuItem("WorldBuilder/Play/Raid Sandbox")]
        public static void PlayRaidSandbox()
        {
            OpenAndPlay(GameplaySceneRegistry.RaidPrototypeScenePath);
        }

        [MenuItem("WorldBuilder/Play/Combat Lab")]
        public static void PlayCombatLab()
        {
            OpenAndPlay(GameplaySceneRegistry.CombatLabScenePath);
        }

        internal static GameplayLoopBootstrap CreateSceneBootstrap(
            GameLaunchMode directMode,
            bool initializeOnAwake)
        {
            GameObject host = new GameObject("[Gameplay Loop]");
            GameplayLoopBootstrap bootstrap =
                host.AddComponent<GameplayLoopBootstrap>();
            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("initializeOnAwake").boolValue =
                initializeOnAwake;
            serialized.FindProperty("directSceneLaunchMode").enumValueIndex =
                (int)directMode;
            serialized.FindProperty("directScenePresetId").stringValue =
                directMode.ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return bootstrap;
        }

        internal static WeaponGridRuntime AttachWeaponGrid(
            GameObject systems,
            GameObject player,
            PlayerInputSource playerInput)
        {
            WeaponGridRuntime runtime =
                systems.AddComponent<WeaponGridRuntime>();
            runtime.InitializeSandboxDefaults();

            WeaponGridSandboxToolkit toolkit =
                systems.AddComponent<WeaponGridSandboxToolkit>();
            toolkit.SetRuntime(runtime);
            toolkit.SetInputSource(playerInput);

            WeaponGridProfileBinding binding =
                systems.AddComponent<WeaponGridProfileBinding>();
            binding.Configure(runtime);

            WeaponGridCombatBridge bridge =
                systems.AddComponent<WeaponGridCombatBridge>();
            bridge.Configure(runtime, player);
            return runtime;
        }

        internal static SceneNavigationMenu AttachSceneNavigation(
            GameObject systems,
            PlayerInputSource playerInput)
        {
            SceneNavigationMenu menu =
                systems.AddComponent<SceneNavigationMenu>();
            menu.Configure(playerInput);
            return menu;
        }

        private static void BuildAllWithoutPrompt()
        {
            EnsureSceneFolder();
            CombatLabSceneBuilder.Build();
            BuildBootstrap();
            BuildHomeBase();
            BuildRaidPrototype();
            GameplaySceneRegistry.ApplyExistingScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.BootstrapScenePath,
                OpenSceneMode.Single);
            Debug.Log(
                "WorldBuilder gameplay loop generated: Bootstrap, HomeBase, " +
                "RaidPrototype, and CombatLab.");
        }

        private static void BuildBootstrap()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CombatLabSceneBuilder.CreateStandardLighting();
            CreateSceneBootstrap(
                GameLaunchMode.CombatLab,
                initializeOnAwake: false);

            Material floor = CombatLabSceneBuilder.GetStandardMaterial(
                "LoopMenuFloor",
                new Color(0.08f, 0.095f, 0.105f));
            Material accent = CombatLabSceneBuilder.GetStandardMaterial(
                "LoopMenuAccent",
                new Color(0.34f, 0.49f, 0.38f));
            GameObject environment = new GameObject("Environment");
            CombatLabSceneBuilder.CreateStandardBlock(
                "Menu Floor",
                new Vector3(0f, -0.35f, 0f),
                new Vector3(22f, 0.7f, 18f),
                floor,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardMarker(
                "Loop Marker",
                new Vector3(0f, 0.03f, 2.5f),
                new Vector3(3.4f, 0.04f, 3.4f),
                accent,
                environment.transform);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.052f);
            camera.fieldOfView = 52f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position =
                new Vector3(0f, 5.2f, -10.5f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.1f, 1.5f));

            GameObject systems =
                new GameObject(InfrastructureMarkerName);
            systems.AddComponent<BootstrapMenuController>();
            SaveScene(scene, GameplaySceneRegistry.BootstrapScenePath);
        }

        private static void BuildHomeBase()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CombatLabSceneBuilder.CreateStandardLighting();
            CreateSceneBootstrap(
                GameLaunchMode.HomeSandbox,
                initializeOnAwake: true);

            Material floor = CombatLabSceneBuilder.GetStandardMaterial(
                "HomeFloor",
                new Color(0.17f, 0.19f, 0.18f));
            Material wall = CombatLabSceneBuilder.GetStandardMaterial(
                "HomeWall",
                new Color(0.24f, 0.26f, 0.24f));
            Material chestMaterial =
                GetOrCreateChestMaterial();
            Material gate = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidGate",
                new Color(0.24f, 0.48f, 0.35f),
                0.25f,
                0.05f);

            GameObject environment = new GameObject("Environment");
            GameObject gridObject =
                new GameObject("Home Placement Grid");
            gridObject.transform.SetParent(
                environment.transform,
                false);
            HomePlacementGrid homeGrid =
                gridObject.AddComponent<HomePlacementGrid>();
            homeGrid.Configure(2.5f);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Base Floor",
                new Vector3(0f, -0.25f, 0f),
                new Vector3(30f, 0.5f, 25f),
                floor,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "North Wall",
                new Vector3(0f, 2f, 12.25f),
                new Vector3(30f, 4.5f, 0.5f),
                wall,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "West Wall",
                new Vector3(-14.75f, 2f, 0f),
                new Vector3(0.5f, 4.5f, 25f),
                wall,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "East Wall",
                new Vector3(14.75f, 2f, 0f),
                new Vector3(0.5f, 4.5f, 25f),
                wall,
                environment.transform);

            GameObject[] storageChests = new GameObject[4];
            for (int index = 0; index < 4; index++)
            {
                storageChests[index] =
                    CreateHomeStorageChest(
                        index,
                        homeGrid,
                        chestMaterial,
                        environment.transform);
            }

            GameObject raidGateAssembly =
                new GameObject("Raid Gate Assembly");
            raidGateAssembly.transform.SetParent(
                environment.transform,
                false);
            HomeGridOccupant raidGateOccupant =
                raidGateAssembly.AddComponent<HomeGridOccupant>();
            raidGateOccupant.Configure(
                homeGrid,
                new Vector2Int(-1, 4),
                new Vector2Int(3, 1));
            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Gate Left",
                new Vector3(-3.5f, 2.5f, 11.9f),
                new Vector3(1.25f, 5f, 1f),
                gate,
                raidGateAssembly.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Gate Right",
                new Vector3(3.5f, 2.5f, 11.9f),
                new Vector3(1.25f, 5f, 1f),
                gate,
                raidGateAssembly.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Gate Header",
                new Vector3(0f, 4.5f, 11.9f),
                new Vector3(5.8f, 1f, 1f),
                gate,
                raidGateAssembly.transform);
            CombatLabSceneBuilder.CreateStandardMarker(
                "Raid Launch Marker",
                new Vector3(0f, 0.03f, 9.4f),
                new Vector3(3.2f, 0.04f, 2.2f),
                gate,
                raidGateAssembly.transform);

            GameObject player =
                CombatLabSceneBuilder.CreateStandardPlayer(
                    new Vector3(0f, 1f, -6.5f),
                    out Health _,
                    out PlayerInputSource input);
            CombatLabSceneBuilder.CreateStandardCamera(
                player.transform,
                input);

            GameObject systems =
                new GameObject(InfrastructureMarkerName);
            HomeBaseController homeBase =
                systems.AddComponent<HomeBaseController>();
            homeBase.Configure(input);
            AttachWeaponGrid(systems, player, input);
            WeaponGridSandboxToolkit toolkit =
                systems.GetComponent<WeaponGridSandboxToolkit>();
            toolkit.SetToggleWithTab(false);
            HomeInventoryController inventory =
                systems.AddComponent<HomeInventoryController>();
            inventory.Configure(homeBase, input, toolkit);
            AttachSceneNavigation(systems, input);

            for (int index = 0;
                 index < storageChests.Length;
                 index++)
            {
                GameObject chestInteraction =
                    new GameObject(
                        $"Chest Interaction {index + 1}");
                chestInteraction.transform.SetParent(
                    storageChests[index].transform,
                    false);
                chestInteraction.transform.localPosition =
                    new Vector3(0f, 0.35f, -0.65f);
                BoxCollider chestTrigger =
                    chestInteraction.AddComponent<BoxCollider>();
                chestTrigger.size =
                    new Vector3(2.4f, 2.4f, 2.8f);
                HomeStorageChest chest =
                    chestInteraction.AddComponent<HomeStorageChest>();
                chest.Configure(
                    inventory,
                    $"home-chest-{index + 1}",
                    $"Chest {index + 1}");
            }

            GameObject raidDoorInteraction =
                new GameObject("Raid Door Interaction");
            raidDoorInteraction.transform.SetParent(
                raidGateAssembly.transform);
            raidDoorInteraction.transform.position =
                new Vector3(0f, 1.6f, 10.3f);
            BoxCollider raidDoorTrigger =
                raidDoorInteraction.AddComponent<BoxCollider>();
            raidDoorTrigger.size = new Vector3(5f, 3.2f, 2.6f);
            HomeRaidDoor raidDoor =
                raidDoorInteraction.AddComponent<HomeRaidDoor>();
            raidDoor.Configure(homeBase);
            SaveScene(scene, GameplaySceneRegistry.HomeBaseScenePath);
        }

        private static GameObject CreateHomeStorageChest(
            int zeroBasedIndex,
            HomePlacementGrid grid,
            Material material,
            Transform parent)
        {
            GameObject chest =
                new GameObject(
                    $"Interactive Storage Chest {zeroBasedIndex + 1}");
            chest.transform.SetParent(parent, false);
            HomeGridOccupant occupant =
                chest.AddComponent<HomeGridOccupant>();
            occupant.Configure(
                grid,
                new Vector2Int(-4 + zeroBasedIndex, 3),
                Vector2Int.one,
                0f,
                2);

            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    ChestPrefabPath);
            GameObject model =
                source != null
                    ? PrefabUtility.InstantiatePrefab(
                        source,
                        chest.transform) as GameObject
                    : null;
            if (model == null)
            {
                model =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);
                model.transform.SetParent(
                    chest.transform,
                    false);
            }
            model.name = "Chest Model";

            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                renderers[index].sharedMaterial = material;
            }

            if (TryGetRendererBounds(
                    renderers,
                    out Bounds initialBounds))
            {
                if (initialBounds.size.z >
                    initialBounds.size.x)
                {
                    model.transform.rotation =
                        Quaternion.AngleAxis(
                            90f,
                            Vector3.up) *
                        model.transform.rotation;
                    TryGetRendererBounds(
                        renderers,
                        out initialBounds);
                }

                float scale =
                    Mathf.Min(
                        2.15f /
                            Mathf.Max(
                                0.001f,
                                initialBounds.size.x),
                        Mathf.Min(
                            1.25f /
                                Mathf.Max(
                                    0.001f,
                                    initialBounds.size.y),
                            1.65f /
                                Mathf.Max(
                                    0.001f,
                                    initialBounds.size.z)));
                model.transform.localScale *= scale;
                TryGetRendererBounds(
                    renderers,
                    out Bounds scaledBounds);
                model.transform.position +=
                    Vector3.up *
                    (chest.transform.position.y -
                     scaledBounds.min.y);
            }

            var solidCollider =
                chest.AddComponent<BoxCollider>();
            if (TryGetRendererBounds(
                    renderers,
                    out Bounds finalBounds))
            {
                solidCollider.center =
                    chest.transform.InverseTransformPoint(
                        finalBounds.center);
                solidCollider.size =
                    finalBounds.size +
                    new Vector3(0.04f, 0.02f, 0.04f);
            }
            else
            {
                solidCollider.center =
                    new Vector3(0f, 0.6f, 0f);
                solidCollider.size =
                    new Vector3(2f, 1.2f, 1.5f);
            }

            return chest;
        }

        private static void BuildRaidPrototype()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CombatLabSceneBuilder.CreateStandardLighting();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor =
                new Color(
                    0.46f,
                    0.52f,
                    0.58f,
                    1f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 105f;
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor =
                new Color(0.40f, 0.47f, 0.54f, 1f);
            RenderSettings.ambientEquatorColor =
                new Color(0.25f, 0.30f, 0.34f, 1f);
            RenderSettings.ambientGroundColor =
                new Color(0.16f, 0.18f, 0.19f, 1f);
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.reflectionIntensity = 0.42f;
            GameObject raidSun = GameObject.Find("Sun");
            Light raidSunLight =
                raidSun != null
                    ? raidSun.GetComponent<Light>()
                    : null;
            if (raidSunLight != null)
            {
                raidSunLight.color =
                    new Color(0.94f, 0.86f, 0.72f, 1f);
                raidSunLight.intensity = 1.35f;
                raidSunLight.shadowStrength = 0.82f;
                raidSun.transform.rotation =
                    Quaternion.Euler(62f, -42f, 0f);
            }
            CreateSceneBootstrap(
                GameLaunchMode.RaidSandbox,
                initializeOnAwake: true);

            Material ground =
                GetOrCreateStylizedForestMaterial(
                    "RaidGround",
                    "Stylized_forest_tga/" +
                    "T_Landscape_grass_BaseColor.TGA",
                    false,
                    new Color(0.72f, 0.64f, 0.50f, 1f));
            Material road =
                GetOrCreateStylizedForestMaterial(
                    "RaidDirtRoad",
                    "Stylized_forest_tga/" +
                    "T_Landscape_dirt_BaseColor.TGA",
                    false,
                    new Color(0.72f, 0.61f, 0.49f, 1f));
            Material water = GetOrCreateRiverMaterial();
            Material bridge = GetOrCreateBridgeMaterial();
            GameObject bridgePrefab = LoadBridgePrefab();
            Material treeBark =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestBark",
                    "Stylized_forest_tga/T_bark_BaseColor.TGA",
                    false,
                    new Color(0.58f, 0.56f, 0.52f, 1f));
            Material birchBark =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestBirchBark",
                    "Stylized_forest_tga/T_bark_birch_BaseColor.TGA",
                    false,
                    new Color(0.72f, 0.72f, 0.68f, 1f));
            Material treeLeaves =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestLeaves",
                    "T_leaves_BaseColor_Unity.TGA",
                    true,
                    new Color(0.57f, 0.63f, 0.58f, 1f));
            Material pineLeaves =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestPineLeaves",
                    "T_pine_leaves_BaseColor_Unity.TGA",
                    true,
                    new Color(
                        0.50f,
                        0.59f,
                        0.63f,
                        1f));
            ApplyFoliageWindShader(
                treeLeaves,
                0.28f,
                0.042f,
                0.82f,
                4.8f);
            ApplyFoliageWindShader(
                pineLeaves,
                0.22f,
                0.034f,
                0.78f,
                4.5f);
            Material grassDetails =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestGrassDetails",
                    "Stylized_forest_tga/" +
                    "T_grass_BaseColor.TGA",
                    true,
                    new Color(0.72f, 0.75f, 0.62f, 1f));
            ApplyVertexTintShader(
                ground);
            ApplyVertexTintShader(
                grassDetails);
            ApplyMatteSurface(ground);
            ApplyMatteSurface(road);
            ApplyMatteSurface(grassDetails);
            Material plantDetails =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestPlantDetails",
                    "Stylized_forest_tga/" +
                    "T_plants_BaseColor.TGA",
                    true,
                    new Color(0.64f, 0.66f, 0.55f, 1f));
            Material rocks =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestRocks",
                    "Stylized_forest_tga/" +
                    "T_rocks_BaseColor.TGA",
                    false,
                    new Color(0.62f, 0.64f, 0.64f, 1f));
            Material extraction = CombatLabSceneBuilder.GetStandardMaterial(
                "Extraction",
                new Color(0.18f, 0.72f, 0.54f),
                0.35f,
                0.08f);

            new GameObject(
                "Environment - Generated At Runtime");

            GameObject player =
                CombatLabSceneBuilder.CreateStandardPlayer(
                    new Vector3(0f, 1f, -65f),
                    out Health _,
                    out PlayerInputSource input);
            CombatLabSceneBuilder.CreateStandardCamera(
                player.transform,
                input);

            Vector3[] enemyPositions =
            {
                new Vector3(0f, 1f, -25f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1f, 25f)
            };
            EnemyBrain[] raidEnemies =
                new EnemyBrain[enemyPositions.Length];
            for (int index = 0; index < enemyPositions.Length; index++)
            {
                GameObject enemy =
                    CombatLabSceneBuilder.CreateRaidEnemy(
                        enemyPositions[index],
                        out Health _);
                enemy.name = $"Raider {index + 1}";
                EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                raidEnemies[index] = brain;
                if (brain != null)
                {
                    brain.ConfigureAsTrainingDummy(
                        requireManualActivation: false);
                    brain.enabled = false;
                }
            }

            GameObject systems =
                new GameObject(InfrastructureMarkerName);
            RaidPrototypeController raidController =
                systems.AddComponent<RaidPrototypeController>();
            BowAimCrosshairPresenter crosshair =
                systems.AddComponent<BowAimCrosshairPresenter>();
            crosshair.Configure(
                player.GetComponentInChildren<BowWeapon>(true));
            AttachWeaponGrid(systems, player, input);
            AttachSceneNavigation(systems, input);

            CreatePickup(
                "Keen Shard Pickup",
                "keen-shard",
                new Vector3(-4f, 0.75f, 7f),
                new Color(0.93f, 0.38f, 0.19f));
            CreatePickup(
                "Iron Bond Pickup",
                "iron-bond",
                new Vector3(6f, 0.75f, 20f),
                new Color(0.42f, 0.62f, 0.76f));
            CreatePickup(
                "Wind Step Pickup",
                "wind-step",
                new Vector3(-3f, 0.75f, 31f),
                new Color(0.31f, 0.82f, 0.62f));
            ExtractionZone extractionZone =
                CreateExtractionZone(
                new Vector3(0f, 0.05f, 65f),
                extraction);
            ProceduralRaidGenerator generator =
                systems.AddComponent<ProceduralRaidGenerator>();
            generator.Configure(
                player.transform,
                raidEnemies,
                extractionZone,
                LoadStylizedForestTreePrefabs(),
                LoadStylizedForestPrefabs(
                    StylizedForestGrassNames),
                LoadStylizedForestPrefabs(
                    StylizedForestUndergrowthNames),
                LoadStylizedForestPrefabs(
                    StylizedForestRockNames),
                bridgePrefab,
                ground,
                road,
                water,
                bridge,
                treeBark,
                birchBark,
                treeLeaves,
                pineLeaves,
                grassDetails,
                plantDetails,
                rocks);
            extractionZone.Configure(
                raidController,
                "Far Trail Extraction");

            SaveScene(scene, GameplaySceneRegistry.RaidPrototypeScenePath);
        }

        private static void BuildEnvironmentAssetGallery()
        {
            EnsureSceneFolder();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CombatLabSceneBuilder.CreateStandardLighting();
            RenderSettings.fog = false;

            Material ground =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/RaidGround.mat");
            Material bark =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestBark.mat");
            Material birch =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestBirchBark.mat");
            Material leaves =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestLeaves.mat");
            Material pineLeaves =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestPineLeaves.mat");
            Material plants =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestPlantDetails.mat");
            Material rocks =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestRocks.mat");
            Material grass =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/StylizedForestGrassDetails.mat");
            grass =
                GetOrCreateGalleryGrassMaterial(
                    grass);

            CreateGalleryGround(ground);
            Transform treesRoot =
                new GameObject("01 - All Trees").transform;
            Transform plantsRoot =
                new GameObject(
                    "02 - Bushes Flowers and Plants").transform;
            Transform rocksRoot =
                new GameObject("03 - All Rocks").transform;
            Transform grassRoot =
                new GameObject("04 - All Grass").transform;

            GameObject[] treePrefabs =
                LoadStylizedForestTreePrefabs();
            for (int index = 0;
                 index < treePrefabs.Length;
                 index++)
            {
                float x =
                    (index -
                     (treePrefabs.Length - 1) * 0.5f) *
                    5.25f;
                float targetHeight =
                    treePrefabs[index].name.Contains("pine")
                        ? 10.5f
                        : 8.5f;
                CreateGalleryInstance(
                    scene,
                    treePrefabs[index],
                    treesRoot,
                    new Vector3(x, 0f, 10f),
                    targetHeight,
                    null,
                    bark,
                    birch,
                    leaves,
                    pineLeaves);
            }

            GameObject[] plantPrefabs =
                LoadStylizedForestPrefabs(
                    StylizedForestUndergrowthNames);
            for (int index = 0;
                 index < plantPrefabs.Length;
                 index++)
            {
                float x =
                    (index -
                     (plantPrefabs.Length - 1) * 0.5f) *
                    5.1f;
                string lower =
                    plantPrefabs[index].name.ToLowerInvariant();
                float targetHeight =
                    lower.Contains("bush")
                        ? 2.25f
                        : lower.Contains("flower")
                            ? 1.45f
                            : lower.Contains("clover")
                                ? 0.90f
                                : 1.55f;
                CreateGalleryInstance(
                    scene,
                    plantPrefabs[index],
                    plantsRoot,
                    new Vector3(x, 0f, 2.5f),
                    targetHeight,
                    plants,
                    null,
                    null,
                    null,
                    null);
            }

            GameObject[] rockPrefabs =
                LoadStylizedForestPrefabs(
                    StylizedForestRockNames);
            for (int index = 0;
                 index < rockPrefabs.Length;
                 index++)
            {
                float x =
                    (index -
                     (rockPrefabs.Length - 1) * 0.5f) *
                    5.6f;
                CreateGalleryInstance(
                    scene,
                    rockPrefabs[index],
                    rocksRoot,
                    new Vector3(x, 0f, -4.5f),
                    1.8f,
                    rocks,
                    null,
                    null,
                    null,
                    null);
            }

            GameObject[] grassPrefabs =
                LoadStylizedForestPrefabs(
                    StylizedForestGrassNames);
            for (int index = 0;
                 index < grassPrefabs.Length;
                 index++)
            {
                float x =
                    (index -
                     (grassPrefabs.Length - 1) * 0.5f) *
                    6.2f;
                CreateGalleryInstance(
                    scene,
                    grassPrefabs[index],
                    grassRoot,
                    new Vector3(x, 0f, -11.5f),
                    0.90f + index * 0.14f,
                    grass,
                    null,
                    null,
                    null,
                    null);
            }

            GameObject cameraObject =
                new GameObject("Gallery Camera");
            cameraObject.tag = "MainCamera";
            Camera camera =
                cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position =
                new Vector3(0f, 15f, -40f);
            camera.transform.LookAt(
                new Vector3(0f, 3.2f, 0f));
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.clearFlags =
                CameraClearFlags.Skybox;

            SaveScene(
                scene,
                EnvironmentAssetGalleryScenePath);
        }

        private static void CreateGalleryGround(
            Material material)
        {
            var mesh = new Mesh
            {
                name = "Environment Gallery Ground"
            };
            mesh.vertices = new[]
            {
                new Vector3(-34f, 0f, -20f),
                new Vector3(-34f, 0f, 18f),
                new Vector3(34f, 0f, 18f),
                new Vector3(34f, 0f, -20f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 5f),
                new Vector2(10f, 5f),
                new Vector2(10f, 0f)
            };
            mesh.colors = new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            };
            mesh.triangles =
                new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject ground =
                new GameObject("Raid Green Ground");
            ground.AddComponent<MeshFilter>()
                .sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>()
                .sharedMaterial = material;
            ground.AddComponent<MeshCollider>()
                .sharedMesh = mesh;
        }

        private static GameObject CreateGalleryInstance(
            Scene scene,
            GameObject prefab,
            Transform parent,
            Vector3 groundPosition,
            float targetHeight,
            Material fixedMaterial,
            Material bark,
            Material birch,
            Material leaves,
            Material pineLeaves)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab,
                    scene) as GameObject;
            instance.name = prefab.name;
            instance.transform.SetParent(parent, true);
            instance.transform.position =
                Vector3.zero;
            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(
                    true);
            var visible =
                new System.Collections.Generic.List<Renderer>();
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];
                if (renderer.name.StartsWith(
                        "UCX_",
                        System.StringComparison
                            .OrdinalIgnoreCase))
                {
                    renderer.enabled = false;
                    continue;
                }
                visible.Add(renderer);
                Material[] materials =
                    renderer.sharedMaterials;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    materials[materialIndex] =
                        fixedMaterial != null
                            ? fixedMaterial
                            : ResolveGalleryTreeMaterial(
                                materials[materialIndex],
                                prefab.name,
                                bark,
                                birch,
                                leaves,
                                pineLeaves);
                }
                renderer.sharedMaterials = materials;
            }
            if (!TryGetRendererBounds(
                    visible.ToArray(),
                    out Bounds initialBounds))
            {
                return instance;
            }
            float scale =
                targetHeight /
                Mathf.Max(
                    0.001f,
                    initialBounds.size.y);
            instance.transform.localScale *= scale;
            TryGetRendererBounds(
                visible.ToArray(),
                out Bounds finalBounds);
            instance.transform.position =
                groundPosition +
                Vector3.up *
                    (groundPosition.y -
                     finalBounds.min.y);
            return instance;
        }

        private static Material ResolveGalleryTreeMaterial(
            Material source,
            string prefabName,
            Material bark,
            Material birch,
            Material leaves,
            Material pineLeaves)
        {
            string materialName =
                source != null
                    ? source.name.ToLowerInvariant()
                    : string.Empty;
            string treeName =
                prefabName.ToLowerInvariant();
            if (materialName.Contains("birch"))
            {
                return birch;
            }
            if (materialName.Contains("pine") ||
                treeName.Contains("_pine_") &&
                !materialName.Contains("barck") &&
                !materialName.Contains("bark"))
            {
                return pineLeaves;
            }
            if (materialName.Contains("foliage") ||
                materialName.Contains("leaves"))
            {
                return leaves;
            }
            return treeName.Contains("birch")
                ? birch
                : bark;
        }

        private static Material GetOrCreateGalleryGrassMaterial(
            Material source)
        {
            const string path =
                "Assets/_Project/Art/Prototype/Materials/" +
                "StylizedForestGrassGallery.mat";
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path);
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "StylizedForestGrassGallery"
                };
                AssetDatabase.CreateAsset(
                    material,
                    path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            Texture texture =
                source != null
                    ? source.mainTexture
                    : null;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture(
                    "_BaseMap",
                    texture);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    new Color(
                        1.18f,
                        1.34f,
                        1.08f,
                        1f));
            }
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat(
                    "_AlphaClip",
                    1f);
            }
            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat(
                    "_Cutoff",
                    0.35f);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    0f);
            }
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue =
                (int)UnityEngine.Rendering
                    .RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject[] LoadStylizedForestTreePrefabs()
        {
            return LoadStylizedForestPrefabs(
                StylizedForestTreeNames);
        }

        private static GameObject[] LoadStylizedForestPrefabs(
            string[] prefabNames)
        {
            GameObject[] prefabs =
                new GameObject[prefabNames.Length];
            for (int index = 0;
                 index < prefabs.Length;
                 index++)
            {
                prefabs[index] =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{StylizedForestModelFolder}/" +
                        $"{prefabNames[index]}.FBX");
            }
            return prefabs;
        }

        private static Material GetOrCreateRiverMaterial()
        {
            const string materialPath =
                "Assets/_Project/Art/Prototype/Materials/" +
                "RaidWater.mat";
            const string texturePath =
                "Assets/_Project/Art/Prototype/Textures/" +
                "StylizedRiverFlow.png";
            Shader shader =
                Shader.Find(
                    "WorldBuilder/Stylized River Flow");
            if (shader == null)
            {
                shader = Shader.Find(
                    "Universal Render Pipeline/Lit");
            }
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "RaidWater"
                };
                AssetDatabase.CreateAsset(
                    material,
                    materialPath);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    texturePath) as TextureImporter;
            if (importer != null &&
                (importer.wrapMode !=
                    TextureWrapMode.Repeat ||
                 !importer.mipmapEnabled ||
                 importer.anisoLevel != 4))
            {
                importer.wrapMode =
                    TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            Texture2D flowTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    texturePath);
            material.SetTexture(
                "_BaseMap",
                flowTexture);
            material.SetTextureScale(
                "_BaseMap",
                new Vector2(1.1f, 0.72f));
            material.SetColor(
                "_DeepColor",
                new Color(
                    0.035f,
                    0.09f,
                    0.145f,
                    1f));
            material.SetColor(
                "_CurrentColor",
                new Color(
                    0.18f,
                    0.32f,
                    0.42f,
                    1f));
            material.SetColor(
                "_FoamColor",
                new Color(
                    0.95f,
                    0.96f,
                    0.93f,
                    1f));
            material.SetFloat("_Opacity", 0.99f);
            material.SetFloat("_FlowSpeed", 0.22f);
            material.SetFloat(
                "_SecondarySpeed",
                0.22f);
            material.SetFloat("_FoamStrength", 1f);
            material.SetFloat("_WaveHeight", 0.22f);
            material.SetFloat("_NormalStrength", 10f);
            material.SetFloat("_StreamSeparation", 0.20f);
            material.SetFloat("_BankEddyStrength", 0.055f);
            material.renderQueue =
                (int)UnityEngine.Rendering
                    .RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateBridgeMaterial()
        {
            const string materialPath =
                "Assets/_Project/Art/Prototype/Materials/RaidBridge.mat";
            const string baseColorPath = BridgeTextureFolder +
                "/Bridge_low_Bridge_BaseColor.png";
            const string normalPath = BridgeTextureFolder +
                "/Bridge_low_Bridge_Normal.png";
            TextureImporter normalImporter =
                AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null &&
                normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit");
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "RaidBridge"
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            Texture2D baseColor =
                AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
            Texture2D normal =
                AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", baseColor);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    new Color(0.76f, 0.72f, 0.66f, 1f));
            }
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.78f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.03f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.16f);
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static GameObject LoadBridgePrefab()
        {
            ModelImporter importer =
                AssetImporter.GetAtPath(BridgeModelPath) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                BridgeModelPath);
        }

        private static Material GetOrCreateStylizedForestMaterial(
            string materialName,
            string textureName,
            bool alphaClipped,
            Color tint)
        {
            Material material =
                CombatLabSceneBuilder.GetStandardMaterial(
                    materialName,
                    Color.white,
                    0.08f);
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");
            if (shader != null)
            {
                material.shader = shader;
            }

            string texturePath =
                $"{StylizedForestTextureFolder}/" +
                textureName;
            TextureImporter textureImporter =
                AssetImporter.GetAtPath(
                    texturePath) as TextureImporter;
            if (alphaClipped &&
                textureImporter != null &&
                (textureImporter.alphaSource !=
                    TextureImporterAlphaSource.FromInput ||
                 !textureImporter.alphaIsTransparency ||
                 !textureImporter.mipmapEnabled ||
                 !textureImporter
                    .mipMapsPreserveCoverage ||
                 !Mathf.Approximately(
                    textureImporter
                        .alphaTestReferenceValue,
                    0.35f)))
            {
                textureImporter.alphaSource =
                    TextureImporterAlphaSource.FromInput;
                textureImporter.alphaIsTransparency =
                    true;
                textureImporter.mipmapEnabled = true;
                textureImporter.mipMapsPreserveCoverage =
                    true;
                textureImporter.alphaTestReferenceValue =
                    0.35f;
                textureImporter.SaveAndReimport();
            }
            Texture2D baseColor =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    texturePath);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture(
                    "_BaseMap",
                    baseColor);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    tint);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor(
                    "_Color",
                    tint);
            }
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat(
                    "_AlphaClip",
                    alphaClipped ? 1f : 0f);
            }
            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat(
                    "_Cutoff",
                    0.35f);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    alphaClipped ? 0f : 2f);
            }
            if (alphaClipped)
            {
                material.EnableKeyword(
                    "_ALPHATEST_ON");
                material.renderQueue =
                    (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                material.doubleSidedGI = true;
            }
            else
            {
                material.DisableKeyword(
                    "_ALPHATEST_ON");
                material.renderQueue = -1;
                material.doubleSidedGI = false;
            }
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyVertexTintShader(
            Material material)
        {
            Shader shader =
                Shader.Find(
                    "WorldBuilder/Vertex Tint Lit");
            if (material != null && shader != null)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }

        private static void ApplyFoliageWindShader(
            Material material,
            float swayStrength,
            float rustleStrength,
            float windSpeed,
            float rustleSpeed)
        {
            Shader shader =
                Shader.Find(
                    "WorldBuilder/Foliage Wind Lit");
            if (material == null || shader == null)
            {
                return;
            }

            material.shader = shader;
            material.SetFloat(
                "_WindStrength",
                swayStrength);
            material.SetFloat(
                "_WindSpeed",
                windSpeed);
            material.SetFloat(
                "_RustleStrength",
                rustleStrength);
            material.SetFloat(
                "_RustleSpeed",
                rustleSpeed);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.doubleSidedGI = true;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static void ApplyMatteSurface(
            Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.01f);
            }
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
            }
            if (material.HasProperty("_EnvironmentReflections"))
            {
                material.SetFloat("_EnvironmentReflections", 0f);
            }
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
        }

        private static Material GetOrCreateChestMaterial()
        {
            TextureImporter normalImporter =
                AssetImporter.GetAtPath(
                    ChestNormalPath) as TextureImporter;
            if (normalImporter != null &&
                (normalImporter.textureType !=
                    TextureImporterType.NormalMap ||
                 !normalImporter.flipGreenChannel))
            {
                normalImporter.textureType =
                    TextureImporterType.NormalMap;
                normalImporter.flipGreenChannel = true;
                normalImporter.SaveAndReimport();
            }

            Material material =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "HomeStorage",
                    Color.white,
                    0.26f,
                    0f);
            Texture2D diffuse =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    ChestDiffusePath);
            Texture2D normal =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    ChestNormalPath);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture(
                    "_BaseMap",
                    diffuse);
            }
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture(
                    "_BumpMap",
                    normal);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool TryGetRendererBounds(
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
            return found;
        }

        private static void CreateHill(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            GameObject hill =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hill.name = name;
            hill.transform.SetParent(parent);
            hill.transform.position = position;
            hill.transform.localScale = scale;
            hill.GetComponent<Renderer>().sharedMaterial = material;
            SphereCollider sphereCollider =
                hill.GetComponent<SphereCollider>();
            Object.DestroyImmediate(sphereCollider);
            MeshFilter meshFilter = hill.GetComponent<MeshFilter>();
            MeshCollider meshCollider = hill.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(
                hill,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic);
        }

        private static void CreateTree(
            string name,
            Vector3 position,
            Material bark,
            Material leaves,
            Transform parent,
            int variant)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(
                0f,
                (variant * 47f + 13f) % 360f,
                0f);

            float trunkRadius =
                1.15f + variant % 4 * 0.13f;
            float trunkHeight =
                7.4f + variant % 5 * 0.55f;
            float canopyRadius =
                3.15f + variant % 3 * 0.28f;
            float canopyHeight =
                3.35f + (variant + 1) % 4 * 0.24f;
            Mesh trunkMesh = GetOrCreateRaidTreeTrunkMesh();
            Mesh canopyMesh = GetOrCreateRaidTreeCanopyMesh();

            GameObject trunk = new GameObject("Trunk");
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localScale = new Vector3(
                trunkRadius,
                trunkHeight,
                trunkRadius);
            MeshFilter trunkFilter = trunk.AddComponent<MeshFilter>();
            trunkFilter.sharedMesh = trunkMesh;
            MeshRenderer trunkRenderer =
                trunk.AddComponent<MeshRenderer>();
            trunkRenderer.sharedMaterial = bark;
            MeshCollider trunkCollider =
                trunk.AddComponent<MeshCollider>();
            trunkCollider.sharedMesh = trunkMesh;
            GameObjectUtility.SetStaticEditorFlags(
                trunk,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            CreateTreeCanopy(
                "Lower Canopy",
                root.transform,
                canopyMesh,
                leaves,
                new Vector3(0f, trunkHeight - 0.1f, 0f),
                new Vector3(
                    canopyRadius,
                    canopyHeight,
                    canopyRadius));
            CreateTreeCanopy(
                "Upper Canopy",
                root.transform,
                canopyMesh,
                leaves,
                new Vector3(
                    Mathf.Lerp(-0.55f, 0.55f, variant % 3 * 0.5f),
                    trunkHeight + canopyHeight * 0.72f,
                    Mathf.Lerp(0.45f, -0.45f, variant % 2)),
                new Vector3(
                    canopyRadius * 0.72f,
                    canopyHeight * 0.78f,
                    canopyRadius * 0.72f));
        }

        private static void CreateTreeCanopy(
            string name,
            Transform parent,
            Mesh mesh,
            Material material,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject canopy = new GameObject(name);
            canopy.transform.SetParent(parent, false);
            canopy.transform.localPosition = localPosition;
            canopy.transform.localScale = localScale;
            MeshFilter filter = canopy.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer =
                canopy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(
                canopy,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic);
        }

        private static Mesh GetOrCreateRaidTreeTrunkMesh()
        {
            return GetOrUpdateEnvironmentMesh(
                RaidTreeTrunkMeshPath,
                BuildRaidTreeTrunkMesh);
        }

        private static Mesh GetOrCreateRaidTreeCanopyMesh()
        {
            return GetOrUpdateEnvironmentMesh(
                RaidTreeCanopyMeshPath,
                BuildRaidTreeCanopyMesh);
        }

        private static Mesh GetOrUpdateEnvironmentMesh(
            string assetPath,
            System.Func<Mesh> factory)
        {
            EnsureEnvironmentMeshFolder();
            Mesh generated = factory();
            Mesh existing =
                AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh BuildRaidTreeTrunkMesh()
        {
            const int sides = 9;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int side = 0; side < sides; side++)
            {
                float angleA = side * Mathf.PI * 2f / sides;
                float angleB = (side + 1) * Mathf.PI * 2f / sides;
                Vector3 bottomA =
                    new Vector3(Mathf.Cos(angleA), 0f, Mathf.Sin(angleA));
                Vector3 bottomB =
                    new Vector3(Mathf.Cos(angleB), 0f, Mathf.Sin(angleB));
                Vector3 topA =
                    new Vector3(
                        Mathf.Cos(angleA) * 0.72f,
                        1f,
                        Mathf.Sin(angleA) * 0.72f);
                Vector3 topB =
                    new Vector3(
                        Mathf.Cos(angleB) * 0.72f,
                        1f,
                        Mathf.Sin(angleB) * 0.72f);
                AddQuad(
                    vertices,
                    triangles,
                    bottomA,
                    topA,
                    topB,
                    bottomB);
                AddTriangle(
                    vertices,
                    triangles,
                    Vector3.zero,
                    bottomA,
                    bottomB);
                AddTriangle(
                    vertices,
                    triangles,
                    Vector3.up,
                    topB,
                    topA);
            }

            return CreateFacetedMesh(
                "Raid Tree Trunk",
                vertices,
                triangles);
        }

        private static Mesh BuildRaidTreeCanopyMesh()
        {
            const int sides = 8;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            Vector3 top = new Vector3(0f, 1.3f, 0f);
            Vector3 bottom = new Vector3(0f, -1.15f, 0f);
            for (int side = 0; side < sides; side++)
            {
                float angleA = side * Mathf.PI * 2f / sides;
                float angleB = (side + 1) * Mathf.PI * 2f / sides;
                Vector3 upperA = new Vector3(
                    Mathf.Cos(angleA) * 0.76f,
                    0.52f,
                    Mathf.Sin(angleA) * 0.76f);
                Vector3 upperB = new Vector3(
                    Mathf.Cos(angleB) * 0.76f,
                    0.52f,
                    Mathf.Sin(angleB) * 0.76f);
                Vector3 lowerA = new Vector3(
                    Mathf.Cos(angleA),
                    -0.18f,
                    Mathf.Sin(angleA));
                Vector3 lowerB = new Vector3(
                    Mathf.Cos(angleB),
                    -0.18f,
                    Mathf.Sin(angleB));
                AddTriangle(
                    vertices,
                    triangles,
                    top,
                    upperB,
                    upperA);
                AddQuad(
                    vertices,
                    triangles,
                    upperA,
                    upperB,
                    lowerB,
                    lowerA);
                AddTriangle(
                    vertices,
                    triangles,
                    bottom,
                    lowerA,
                    lowerB);
            }

            return CreateFacetedMesh(
                "Raid Tree Canopy",
                vertices,
                triangles);
        }

        private static Mesh CreateFacetedMesh(
            string name,
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int> triangles)
        {
            Mesh mesh = new Mesh
            {
                name = name
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            AddTriangle(vertices, triangles, a, b, c);
            AddTriangle(vertices, triangles, a, c, d);
        }

        private static void AddTriangle(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        private static void EnsureEnvironmentMeshFolder()
        {
            const string artFolder = "Assets/_Project/Art";
            const string prototypeFolder =
                "Assets/_Project/Art/Prototype";
            if (!AssetDatabase.IsValidFolder(artFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project",
                    "Art");
            }

            if (!AssetDatabase.IsValidFolder(prototypeFolder))
            {
                AssetDatabase.CreateFolder(
                    artFolder,
                    "Prototype");
            }

            if (!AssetDatabase.IsValidFolder(EnvironmentMeshFolder))
            {
                AssetDatabase.CreateFolder(
                    prototypeFolder,
                    "Environment");
            }
        }

        private static void CreatePickup(
            string name,
            string definitionId,
            Vector3 position,
            Color color)
        {
            GameObject pickup =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = name;
            pickup.transform.position = position;
            pickup.transform.localScale = Vector3.one * 0.55f;
            pickup.transform.rotation = Quaternion.Euler(32f, 45f, 18f);
            pickup.GetComponent<Renderer>().sharedMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    name.Replace(" ", string.Empty),
                    color,
                    0.45f,
                    0.1f);
            Collider collider = pickup.GetComponent<Collider>();
            collider.isTrigger = true;
            Rigidbody body = pickup.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            RaidPickup raidPickup = pickup.AddComponent<RaidPickup>();
            raidPickup.Configure(definitionId, name.Replace(" Pickup", ""));
        }

        private static ExtractionZone CreateExtractionZone(
            Vector3 position,
            Material material)
        {
            GameObject zone =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zone.name = "Extraction Zone";
            zone.transform.position = position;
            zone.transform.localScale = new Vector3(3.8f, 0.05f, 3.8f);
            zone.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = zone.GetComponent<Collider>();
            Object.DestroyImmediate(collider);
            ExtractionZone extractionZone =
                zone.AddComponent<ExtractionZone>();
            BoxCollider trigger = zone.GetComponent<BoxCollider>();
            trigger.size = new Vector3(1f, 40f, 1f);
            trigger.center = new Vector3(0f, 20f, 0f);
            Rigidbody body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return extractionZone;
        }

        private static void SaveScene(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void EnsureSceneFolder()
        {
            const string projectFolder = "Assets/_Project";
            const string scenesFolder = "Assets/_Project/Scenes";
            if (!AssetDatabase.IsValidFolder(projectFolder))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }

            if (!AssetDatabase.IsValidFolder(scenesFolder))
            {
                AssetDatabase.CreateFolder(projectFolder, "Scenes");
            }
        }

        private static void BuildSingleScene(
            string displayName,
            string scenePath,
            System.Action buildAction)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    $"Stop Play Mode before rebuilding {displayName}.");
                return;
            }

            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureSceneFolder();
            buildAction();
            GameplaySceneRegistry.ApplyExistingScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            Debug.Log(
                $"WorldBuilder {displayName} rebuilt at {scenePath}");
        }

        private static void OpenAndPlay(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                BuildAllWithoutPrompt();
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
    }

}
