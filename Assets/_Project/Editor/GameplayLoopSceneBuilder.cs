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
                    0.72f,
                    0.74f,
                    0.75f,
                    1f);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 62f;
            CreateSceneBootstrap(
                GameLaunchMode.RaidSandbox,
                initializeOnAwake: true);

            Material ground =
                GetOrCreateStylizedForestMaterial(
                    "RaidGround",
                    "Stylized_forest_tga/" +
                    "T_Landscape_grass_BaseColor.TGA",
                    false,
                    Color.white);
            Material road =
                GetOrCreateStylizedForestMaterial(
                    "RaidDirtRoad",
                    "Stylized_forest_tga/" +
                    "T_Landscape_dirt_BaseColor.TGA",
                    false,
                    Color.white);
            Material water = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidWater",
                new Color(0.07f, 0.32f, 0.42f),
                0.82f,
                0.08f);
            Material bridge = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidBridge",
                new Color(0.31f, 0.19f, 0.095f),
                0.12f);
            Material treeBark =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestBark",
                    "Stylized_forest_tga/T_bark_BaseColor.TGA",
                    false,
                    Color.white);
            Material birchBark =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestBirchBark",
                    "Stylized_forest_tga/T_bark_birch_BaseColor.TGA",
                    false,
                    Color.white);
            Material treeLeaves =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestLeaves",
                    "T_leaves_BaseColor_Unity.TGA",
                    true,
                    Color.white);
            Material pineLeaves =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestPineLeaves",
                    "T_pine_leaves_BaseColor_Unity.TGA",
                    true,
                    new Color(
                        0.48f,
                        0.82f,
                        1.35f,
                        1f));
            Material grassDetails =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestGrassDetails",
                    "Stylized_forest_tga/" +
                    "T_grass_BaseColor.TGA",
                    true,
                    Color.white);
            Material plantDetails =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestPlantDetails",
                    "Stylized_forest_tga/" +
                    "T_plants_BaseColor.TGA",
                    true,
                    Color.white);
            Material rocks =
                GetOrCreateStylizedForestMaterial(
                    "StylizedForestRocks",
                    "Stylized_forest_tga/" +
                    "T_rocks_BaseColor.TGA",
                    false,
                    Color.white);
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
                    CombatLabSceneBuilder.CreateStandardEnemy(
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
