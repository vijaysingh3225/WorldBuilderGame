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
                new Vector3(-27f, 0f, -19f),
                new Vector3(-17f, 0f, -16f),
                new Vector3(-7f, 0f, -12f),
                new Vector3(7f, 0f, -11f),
                new Vector3(18f, 0f, -15f),
                new Vector3(29f, 0f, -10f),
                new Vector3(-25f, 0f, -5f),
                new Vector3(-12f, 0f, -3f),
                new Vector3(9f, 0f, -2f),
                new Vector3(22f, 0f, 2f),
                new Vector3(-29f, 0f, 10f),
                new Vector3(-16f, 0f, 8f),
                new Vector3(14f, 0f, 10f),
                new Vector3(28f, 0f, 15f),
                new Vector3(-24f, 0f, 20f),
                new Vector3(-11f, 0f, 21f),
                new Vector3(18f, 0f, 22f),
                new Vector3(-29f, 0f, 34f),
                new Vector3(-16f, 0f, 33f),
                new Vector3(11f, 0f, 34f),
                new Vector3(25f, 0f, 31f),
                new Vector3(-21f, 0f, 45f),
                new Vector3(-9f, 0f, 42f),
                new Vector3(12f, 0f, 45f),
                new Vector3(27f, 0f, 43f)
            };
            for (int index = 0; index < treePositions.Length; index++)
            {
                CreateTree(
                    $"Cover Tree {index + 1:00}",
                    treePositions[index],
                    bark,
                    leaves,
                    environment.transform,
                    index);
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
            BowAimCrosshairPresenter crosshair =
                systems.AddComponent<BowAimCrosshairPresenter>();
            crosshair.Configure(
                player.GetComponentInChildren<BowWeapon>(true));
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
