using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Editor
{
    public static class CombatLabSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CombatLab.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Prototype/Materials";

        [MenuItem("WorldBuilder/Build Combat Lab")]
        public static void Build()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMaterial = GetOrCreateMaterial("Floor", new Color(0.16f, 0.19f, 0.20f));
            Material wallMaterial = GetOrCreateMaterial("Stone", new Color(0.25f, 0.28f, 0.27f));
            Material accentMaterial = GetOrCreateMaterial("MossAccent", new Color(0.30f, 0.40f, 0.27f));
            Material playerMaterial = GetOrCreateMaterial("Player", new Color(0.18f, 0.34f, 0.46f));
            Material enemyMaterial = GetOrCreateMaterial("Enemy", new Color(0.50f, 0.20f, 0.12f));
            Material weaponMaterial = GetOrCreateMaterial("Weapon", new Color(0.68f, 0.62f, 0.46f), 0.7f, 0.15f);

            CreateLighting();
            GameObject environment = new GameObject("Environment");
            CreateArena(environment.transform, floorMaterial, wallMaterial, accentMaterial);

            GameObject player = CreatePlayer(new Vector3(0f, 1f, -5.5f), playerMaterial, weaponMaterial, out Health playerHealth, out PlayerInputSource playerInput);
            GameObject enemy = CreateEnemy(new Vector3(0f, 1f, 5f), enemyMaterial, player.transform, out Health enemyHealth);
            CreateCamera(player.transform, playerInput);

            GameObject systems = new GameObject("Prototype Systems");
            CombatLabHud hud = systems.AddComponent<CombatLabHud>();
            hud.Configure(playerHealth, enemyHealth);

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"WorldBuilder Combat Lab generated at {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "WorldBuilder";
            PlayerSettings.productName = "WorldBuilder Game";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.92f, 0.78f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.33f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.18f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.08f);
        }

        private static void CreateArena(Transform parent, Material floor, Material stone, Material accent)
        {
            CreateBlock("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 21f), floor, parent);
            CreateBlock("North Wall", new Vector3(0f, 1.5f, 10.25f), new Vector3(24f, 3.5f, 0.5f), stone, parent);
            CreateBlock("South Wall", new Vector3(0f, 1.5f, -10.25f), new Vector3(24f, 3.5f, 0.5f), stone, parent);
            CreateBlock("East Wall", new Vector3(11.75f, 1.5f, 0f), new Vector3(0.5f, 3.5f, 21f), stone, parent);
            CreateBlock("West Wall", new Vector3(-11.75f, 1.5f, 0f), new Vector3(0.5f, 3.5f, 21f), stone, parent);

            CreateBlock("West Cover", new Vector3(-4.4f, 0.7f, -0.8f), new Vector3(3.2f, 1.4f, 1.1f), stone, parent);
            CreateBlock("East Cover", new Vector3(4.4f, 0.7f, 1.2f), new Vector3(3.2f, 1.4f, 1.1f), stone, parent);
            CreateBlock("North Pillar", new Vector3(-5.7f, 1.3f, 5.8f), new Vector3(1.4f, 2.6f, 1.4f), stone, parent);
            CreateBlock("South Pillar", new Vector3(5.7f, 1.3f, -5.8f), new Vector3(1.4f, 2.6f, 1.4f), stone, parent);

            CreateMarker("Player Start Marker", new Vector3(0f, 0.03f, -5.5f), new Vector3(2.4f, 0.05f, 2.4f), accent, parent);
            CreateMarker("Enemy Start Marker", new Vector3(0f, 0.03f, 5f), new Vector3(2.4f, 0.05f, 2.4f), accent, parent);
        }

        private static GameObject CreatePlayer(Vector3 position, Material bodyMaterial, Material weaponMaterial, out Health health, out PlayerInputSource input)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.layer = 2;
            player.transform.position = position;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.05f;

            StableId stableId = player.AddComponent<StableId>();
            stableId.EnsureAssigned();
            health = player.AddComponent<Health>();
            health.Configure(100f);
            input = player.AddComponent<PlayerInputSource>();
            player.AddComponent<ThirdPersonMotor>();
            MeleeWeapon weapon = player.AddComponent<MeleeWeapon>();

            GameObject sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sword.name = "Sword Visual";
            sword.layer = 2;
            sword.transform.SetParent(player.transform, false);
            sword.transform.localPosition = new Vector3(0.62f, 0.45f, 0.7f);
            sword.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            sword.transform.localScale = new Vector3(0.12f, 0.12f, 1.35f);
            Object.DestroyImmediate(sword.GetComponent<BoxCollider>());
            sword.GetComponent<Renderer>().sharedMaterial = weaponMaterial;

            WeaponSwingPresenter swing = player.AddComponent<WeaponSwingPresenter>();
            swing.Configure(weapon, sword.transform);
            return player;
        }

        private static GameObject CreateEnemy(Vector3 position, Material bodyMaterial, Transform target, out Health health)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Raider Prototype";
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Object.DestroyImmediate(enemy.GetComponent<CapsuleCollider>());
            Renderer renderer = enemy.GetComponent<Renderer>();
            renderer.sharedMaterial = bodyMaterial;

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.05f;

            StableId stableId = enemy.AddComponent<StableId>();
            stableId.EnsureAssigned();
            health = enemy.AddComponent<Health>();
            health.Configure(88f);
            EnemyBrain brain = enemy.AddComponent<EnemyBrain>();
            brain.Configure(target);
            EnemyTelegraphPresenter presenter = enemy.AddComponent<EnemyTelegraphPresenter>();
            presenter.Configure(renderer);
            return enemy;
        }

        private static void CreateCamera(Transform target, PlayerInputSource input)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = target.position + new Vector3(0.65f, 2.2f, -4.8f);
            cameraObject.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            ThirdPersonCamera rig = cameraObject.AddComponent<ThirdPersonCamera>();
            rig.Configure(target, input);
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(block, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            return block;
        }

        private static GameObject CreateMarker(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            marker.transform.localScale = scale;
            marker.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(marker.GetComponent<CapsuleCollider>());
            return marker;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness = 0.15f, float metallic = 0f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { name = name, color = color };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureProjectFolders()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project", "Art");
            EnsureFolder("Assets/_Project/Art", "Prototype");
            EnsureFolder("Assets/_Project/Art/Prototype", "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }

    [InitializeOnLoad]
    internal static class CombatLabFirstImport
    {
        private const string SessionKey = "WorldBuilder.CombatLabFirstImportAttempted";

        static CombatLabFirstImport()
        {
            EditorApplication.delayCall += TryBuildInitialScene;
        }

        private static void TryBuildInitialScene()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            if (!File.Exists(CombatLabSceneBuilder.ScenePath))
            {
                CombatLabSceneBuilder.Build();
            }
        }
    }
}
