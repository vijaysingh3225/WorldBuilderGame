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
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Editor
{
    public static class GameplayLoopSceneBuilder
    {
        // Keeps scene generation deterministic across modular rebuilds.
        public const string InfrastructureMarkerName =
            "Gameplay Loop Infrastructure - V1";

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
            Material wood = CombatLabSceneBuilder.GetStandardMaterial(
                "HomeStorage",
                new Color(0.28f, 0.17f, 0.09f));
            Material gate = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidGate",
                new Color(0.24f, 0.48f, 0.35f),
                0.25f,
                0.05f);

            GameObject environment = new GameObject("Environment");
            CombatLabSceneBuilder.CreateStandardBlock(
                "Base Floor",
                new Vector3(0f, -0.25f, 0f),
                new Vector3(30f, 0.5f, 26f),
                floor,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "North Wall",
                new Vector3(0f, 2f, 12.75f),
                new Vector3(30f, 4.5f, 0.5f),
                wall,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "West Wall",
                new Vector3(-14.75f, 2f, 0f),
                new Vector3(0.5f, 4.5f, 26f),
                wall,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "East Wall",
                new Vector3(14.75f, 2f, 0f),
                new Vector3(0.5f, 4.5f, 26f),
                wall,
                environment.transform);

            for (int index = 0; index < 4; index++)
            {
                CombatLabSceneBuilder.CreateStandardBlock(
                    $"Storage Crate {index + 1}",
                    new Vector3(-10.5f + index * 2.1f, 0.65f, 8.5f),
                    new Vector3(1.6f, 1.3f, 1.6f),
                    wood,
                    environment.transform);
            }

            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Gate Left",
                new Vector3(-3.5f, 2.5f, 11.9f),
                new Vector3(1.25f, 5f, 1f),
                gate,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Gate Right",
                new Vector3(3.5f, 2.5f, 11.9f),
                new Vector3(1.25f, 5f, 1f),
                gate,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Gate Header",
                new Vector3(0f, 4.5f, 11.9f),
                new Vector3(5.8f, 1f, 1f),
                gate,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardMarker(
                "Raid Launch Marker",
                new Vector3(0f, 0.03f, 9.4f),
                new Vector3(3.2f, 0.04f, 2.2f),
                gate,
                environment.transform);

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
            SaveScene(scene, GameplaySceneRegistry.HomeBaseScenePath);
        }

        private static void BuildRaidPrototype()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CombatLabSceneBuilder.CreateStandardLighting();
            CreateSceneBootstrap(
                GameLaunchMode.RaidSandbox,
                initializeOnAwake: true);

            Material ground = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidGround",
                new Color(0.19f, 0.23f, 0.17f));
            Material stone = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidStone",
                new Color(0.27f, 0.29f, 0.26f));
            Material bark = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidBark",
                new Color(0.22f, 0.13f, 0.065f));
            Material leaves = CombatLabSceneBuilder.GetStandardMaterial(
                "RaidLeaves",
                new Color(0.16f, 0.32f, 0.16f));
            Material extraction = CombatLabSceneBuilder.GetStandardMaterial(
                "Extraction",
                new Color(0.18f, 0.72f, 0.54f),
                0.35f,
                0.08f);

            GameObject environment = new GameObject("Environment");
            CombatLabSceneBuilder.CreateStandardBlock(
                "Raid Ground",
                new Vector3(0f, -0.3f, 10f),
                new Vector3(72f, 0.6f, 86f),
                ground,
                environment.transform);
            CreateHill(
                "West Hill",
                new Vector3(-27f, -0.9f, 8f),
                new Vector3(14f, 3.5f, 18f),
                stone,
                environment.transform);
            CreateHill(
                "East Hill",
                new Vector3(28f, -1.2f, 25f),
                new Vector3(16f, 4f, 19f),
                stone,
                environment.transform);

            Vector3[] treePositions =
            {
                new Vector3(-18f, 0f, -15f),
                new Vector3(-23f, 0f, 2f),
                new Vector3(20f, 0f, -4f),
                new Vector3(24f, 0f, 12f),
                new Vector3(-19f, 0f, 25f),
                new Vector3(18f, 0f, 34f)
            };
            for (int index = 0; index < treePositions.Length; index++)
            {
                CreateTree(
                    $"Tree {index + 1}",
                    treePositions[index],
                    bark,
                    leaves,
                    environment.transform);
            }

            GameObject player =
                CombatLabSceneBuilder.CreateStandardPlayer(
                    new Vector3(0f, 1f, -20f),
                    out Health _,
                    out PlayerInputSource input);
            CombatLabSceneBuilder.CreateStandardCamera(
                player.transform,
                input);

            Vector3[] enemyPositions =
            {
                new Vector3(0f, 1f, 1f),
                new Vector3(-8f, 1f, 15f),
                new Vector3(8f, 1f, 27f)
            };
            for (int index = 0; index < enemyPositions.Length; index++)
            {
                GameObject enemy =
                    CombatLabSceneBuilder.CreateStandardEnemy(
                        enemyPositions[index],
                        out Health _);
                enemy.name = $"Raider {index + 1}";
                EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                if (brain != null)
                {
                    brain.enabled = false;
                }
            }

            GameObject systems =
                new GameObject(InfrastructureMarkerName);
            systems.AddComponent<RaidPrototypeController>();
            AttachWeaponGrid(systems, player, input);

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
            CreateExtractionZone(
                new Vector3(0f, 0.05f, 42f),
                extraction);

            SaveScene(scene, GameplaySceneRegistry.RaidPrototypeScenePath);
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
            Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;

            GameObject trunk =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, 1.5f, 0.35f);
            trunk.GetComponent<Renderer>().sharedMaterial = bark;

            GameObject crown =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Canopy";
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0f, 4.1f, 0f);
            crown.transform.localScale = new Vector3(2.4f, 2.8f, 2.4f);
            crown.GetComponent<Renderer>().sharedMaterial = leaves;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
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

        private static void CreateExtractionZone(
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
            zone.AddComponent<ExtractionZone>();
            BoxCollider trigger = zone.GetComponent<BoxCollider>();
            trigger.size = new Vector3(1f, 40f, 1f);
            trigger.center = new Vector3(0f, 20f, 0f);
            Rigidbody body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
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
