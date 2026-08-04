using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Diagnostics;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Editor
{
    public static class CombatLabSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CombatLab.unity";
        public const string CheckpointMarkerName =
            "Prototype Systems - V74 Weapon Grid Toolkit";
        private const string MaterialFolder = "Assets/_Project/Art/Prototype/Materials";
        private const string ShortSwordBladePath =
            "Assets/_Project/Art/Prototype/Weapons/PrototypeShortSwordBlade.asset";
        private const string BowLimbBeamPath =
            "Assets/_Project/Art/Prototype/Weapons/PrototypeBowLimbBeam.asset";
        private const string SwordSwingAudioPath =
            "Assets/_Project/Audio/SFX/Sword Swing.mp3";
        private const string SwordHitAudioPath =
            "Assets/_Project/Audio/SFX/Sword Hit.mp3";
        private const string BowPullbackAudioPath =
            "Assets/_Project/Audio/SFX/Bow Pullback.wav";
        private const string ArrowImpactAudioPath =
            "Assets/_Project/Audio/SFX/Arrow Impact.wav";
        private const string ArrowHitFeedbackAudioPath =
            "Assets/_Project/Audio/SFX/ArrowHit.mp3";
        private const string HeadshotFeedbackAudioPath =
            "Assets/_Project/Audio/SFX/HeadShot.mp3";
        private const string ArrowFlybyAudioPath =
            "Assets/_Project/Audio/SFX/Arrow Flyby.mp3";
        private static readonly Vector3 ShortSwordGuardLocalPosition =
            new Vector3(0.035220847f, -0.066798866f, -0.038464874f);
        private static readonly Quaternion ShortSwordGuardLocalRotation =
            new Quaternion(
                -0.28831902f,
                0.8950361f,
                -0.17096046f,
                0.29420236f);
        private static readonly Quaternion ShortSwordGuardLeftHandLocalRotation =
            new Quaternion(
                -0.2711382f,
                0.16369334f,
                0.27808982f,
                -0.9068378f);
        private static readonly Vector3 ShortSwordCarryLocalPosition =
            new Vector3(-0.00072210626f, -0.07712167f, -0.068963856f);
        private static readonly Quaternion ShortSwordCarryLocalRotation =
            new Quaternion(
                -0.0575469f,
                0.7047954f,
                -0.06148468f,
                0.70439446f);
        private static readonly Vector3 ShortSwordCarryLocalScale =
            new Vector3(0.9090908f, 0.9090911f, 0.90909094f);

        [MenuItem("WorldBuilder/Build Combat Lab %#g")]
        public static void Build()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMaterial = GetOrCreateMaterial("Floor", new Color(0.16f, 0.19f, 0.20f));
            Material wallMaterial = GetOrCreateMaterial("Stone", new Color(0.25f, 0.28f, 0.27f));
            Material accentMaterial = GetOrCreateMaterial("MossAccent", new Color(0.30f, 0.40f, 0.27f));
            Material rangeMaterial = GetOrCreateMaterial(
                "RangeZone",
                new Color(0.19f, 0.30f, 0.34f),
                0.08f);
            Material closeQuartersMaterial = GetOrCreateMaterial(
                "CloseQuartersZone",
                new Color(0.39f, 0.29f, 0.17f),
                0.08f);
            Material traversalMaterial = GetOrCreateMaterial(
                "TraversalZone",
                new Color(0.22f, 0.34f, 0.23f),
                0.08f);
            Material measurementMaterial = GetOrCreateMaterial(
                "RangeMeasurement",
                new Color(0.68f, 0.64f, 0.45f),
                0.05f,
                0f,
                true);
            Material playerMaterial = GetOrCreateMaterial(
                "CombatLabPlayer",
                new Color(0.22f, 0.22f, 0.22f),
                0.05f,
                0f,
                true);
            Material playerSecondaryMaterial = playerMaterial;
            Material enemyMaterial = GetOrCreateMaterial(
                "TrainingDummyRed",
                new Color(0.16f, 0.17f, 0.18f),
                0.05f,
                0f,
                true);
            Material enemySecondaryMaterial = enemyMaterial;
            Material bladeMaterial = GetOrCreateMaterial(
                "ShortSwordBlade",
                new Color(0.56f, 0.62f, 0.67f),
                0.72f,
                0.82f);
            Material guardMaterial = GetOrCreateMaterial(
                "ShortSwordGuard",
                new Color(0.15f, 0.17f, 0.18f),
                0.4f,
                0.75f);
            Material gripMaterial = GetOrCreateMaterial(
                "ShortSwordGrip",
                new Color(0.21f, 0.105f, 0.045f),
                0.22f);

            CreateLighting();
            GameplayLoopSceneBuilder.CreateSceneBootstrap(
                GameLaunchMode.CombatLab,
                initializeOnAwake: true);
            GameObject environment = new GameObject("Environment");
            CreateArena(
                environment.transform,
                floorMaterial,
                wallMaterial,
                accentMaterial,
                rangeMaterial,
                closeQuartersMaterial,
                traversalMaterial,
                measurementMaterial);

            GameObject player = CreatePlayer(
                new Vector3(0f, 1f, -5.5f),
                playerMaterial,
                playerSecondaryMaterial,
                bladeMaterial,
                guardMaterial,
                gripMaterial,
                out Health playerHealth,
                out PlayerInputSource playerInput);
            GameObject enemy = CreateEnemy(
                new Vector3(0f, 1f, 5f),
                enemyMaterial,
                enemySecondaryMaterial,
                bladeMaterial,
                guardMaterial,
                gripMaterial,
                EnemyCombatVariant.CombatLabDummy,
                out Health enemyHealth);
            CreateRangedTargets(
                enemyMaterial,
                enemySecondaryMaterial,
                bladeMaterial,
                guardMaterial,
                gripMaterial);
            CreateCamera(player.transform, playerInput);

            GameObject systems = new GameObject(CheckpointMarkerName);
            CombatLabHud hud = systems.AddComponent<CombatLabHud>();
            hud.Configure(playerHealth, enemyHealth);
            systems.AddComponent<GameplayDiagnosticRecorder>();
            GameplayLoopSceneBuilder.AttachWeaponGrid(
                systems,
                player,
                playerInput);
            GameplayLoopSceneBuilder.AttachSceneNavigation(
                systems,
                playerInput);

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            GameplaySceneRegistry.ApplyExistingScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"WorldBuilder Combat Lab generated at {ScenePath}");
        }

        [MenuItem("WorldBuilder/Activate Dummy AI %#t")]
        public static void ActivateDummyAi()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            EnemyBrain brain =
                Object.FindFirstObjectByType<EnemyBrain>();
            brain?.ActivateForDiagnostics();
        }

        [MenuItem("WorldBuilder/Test Dummy Melee %#m")]
        public static void TestDummyMelee()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            EnemyBrain brain =
                Object.FindFirstObjectByType<EnemyBrain>();
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            if (brain == null || player == null)
            {
                return;
            }

            CharacterController controller =
                brain.GetComponent<CharacterController>();
            bool controllerWasEnabled =
                controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                player.transform.forward,
                Vector3.up).normalized;
            brain.transform.position =
                player.transform.position +
                forward * 1.05f;
            brain.transform.rotation =
                Quaternion.LookRotation(-forward, Vector3.up);
            if (controller != null)
            {
                controller.enabled = controllerWasEnabled;
            }

            brain.Configure(player.transform);
        }

        [MenuItem("WorldBuilder/Test Dummy Walk %#w")]
        public static void TestDummyWalk()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            EnemyBrain brain =
                Object.FindFirstObjectByType<EnemyBrain>();
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            if (brain == null || player == null)
            {
                return;
            }

            CharacterController controller =
                brain.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                player.transform.forward,
                Vector3.up).normalized;
            brain.transform.position =
                player.transform.position +
                forward * 4f;
            brain.transform.rotation =
                Quaternion.LookRotation(-forward, Vector3.up);
            brain.Configure(player.transform);
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        internal static GameObject CreateStandardPlayer(
            Vector3 position,
            out Health health,
            out PlayerInputSource input)
        {
            Material body = GetOrCreateMaterial(
                "Player",
                new Color(0.36f, 0.36f, 0.36f),
                0.05f,
                0f,
                true);
            return CreatePlayer(
                position,
                body,
                body,
                GetOrCreateMaterial(
                    "ShortSwordBlade",
                    new Color(0.56f, 0.62f, 0.67f),
                    0.72f,
                    0.82f),
                GetOrCreateMaterial(
                    "ShortSwordGuard",
                    new Color(0.15f, 0.17f, 0.18f),
                    0.4f,
                    0.75f),
                GetOrCreateMaterial(
                    "ShortSwordGrip",
                    new Color(0.21f, 0.105f, 0.045f),
                    0.22f),
                out health,
                out input);
        }

        internal static GameObject CreateCombatLabDummy(
            Vector3 position,
            out Health health)
        {
            return CreateConfiguredEnemy(
                position,
                EnemyCombatVariant.CombatLabDummy,
                out health);
        }

        internal static GameObject CreateRaidEnemy(
            Vector3 position,
            out Health health)
        {
            return CreateConfiguredEnemy(
                position,
                EnemyCombatVariant.RaidEnemy,
                out health);
        }

        private static GameObject CreateConfiguredEnemy(
            Vector3 position,
            EnemyCombatVariant variant,
            out Health health)
        {
            Material body = GetOrCreateMaterial(
                "TrainingDummyRed",
                new Color(0.16f, 0.17f, 0.18f),
                0.05f,
                0f,
                true);
            return CreateEnemy(
                position,
                body,
                body,
                GetOrCreateMaterial(
                    "ShortSwordBlade",
                    new Color(0.56f, 0.62f, 0.67f),
                    0.72f,
                    0.82f),
                GetOrCreateMaterial(
                    "ShortSwordGuard",
                    new Color(0.15f, 0.17f, 0.18f),
                    0.4f,
                    0.75f),
                GetOrCreateMaterial(
                    "ShortSwordGrip",
                    new Color(0.21f, 0.105f, 0.045f),
                    0.22f),
                variant,
                out health);
        }

        internal static void CreateStandardCamera(
            Transform target,
            PlayerInputSource input)
        {
            CreateCamera(target, input);
        }

        internal static void CreateStandardLighting()
        {
            CreateLighting();
        }

        internal static GameObject CreateStandardBlock(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent = null)
        {
            return CreateBlock(
                name,
                position,
                scale,
                material,
                parent);
        }

        internal static GameObject CreateStandardMarker(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent = null)
        {
            return CreateMarker(
                name,
                position,
                scale,
                material,
                parent);
        }

        internal static Material GetStandardMaterial(
            string name,
            Color color,
            float smoothness = 0.2f,
            float metallic = 0f)
        {
            return GetOrCreateMaterial(
                name,
                color,
                smoothness,
                metallic);
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
            light.shadowStrength = 0.85f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.33f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.18f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.08f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 1f;
        }

        private static void CreateArena(
            Transform parent,
            Material floor,
            Material stone,
            Material accent,
            Material range,
            Material closeQuarters,
            Material traversal,
            Material measurement)
        {
            CreateBlock(
                "Lab Floor",
                new Vector3(0f, -0.25f, 35f),
                new Vector3(104f, 0.5f, 120f),
                floor,
                parent);
            CreateBlock("North Wall", new Vector3(0f, 2f, 94.75f), new Vector3(104f, 4.5f, 0.5f), stone, parent);
            CreateBlock("South Wall", new Vector3(0f, 2f, -24.75f), new Vector3(104f, 4.5f, 0.5f), stone, parent);
            CreateBlock("East Wall", new Vector3(51.75f, 2f, 35f), new Vector3(0.5f, 4.5f, 120f), stone, parent);
            CreateBlock("West Wall", new Vector3(-51.75f, 2f, 35f), new Vector3(0.5f, 4.5f, 120f), stone, parent);

            Transform duelZone =
                CreateZoneRoot("01 - Central Duel Yard", parent);
            CreateSurfaceMarker(
                "Duel Yard Boundary",
                new Vector3(0f, 0.012f, 0f),
                new Vector3(25f, 0.024f, 25f),
                accent,
                duelZone);

            CreateBlock("West Cover", new Vector3(-4.4f, 0.7f, -0.8f), new Vector3(3.2f, 1.4f, 1.1f), stone, duelZone);
            CreateBlock("East Cover", new Vector3(4.4f, 0.7f, 1.2f), new Vector3(3.2f, 1.4f, 1.1f), stone, duelZone);
            CreateBlock("North Pillar", new Vector3(-5.7f, 1.3f, 5.8f), new Vector3(1.4f, 2.6f, 1.4f), stone, duelZone);
            CreateBlock("South Pillar", new Vector3(5.7f, 1.3f, -5.8f), new Vector3(1.4f, 2.6f, 1.4f), stone, duelZone);

            CreateBlock("Crouch Test Roof", new Vector3(7.6f, 1.95f, 5.8f), new Vector3(4f, 0.5f, 3f), stone, duelZone);
            CreateBlock("Crouch Test Left Support", new Vector3(5.85f, 0.85f, 5.8f), new Vector3(0.5f, 1.7f, 3f), stone, duelZone);
            CreateBlock("Crouch Test Right Support", new Vector3(9.35f, 0.85f, 5.8f), new Vector3(0.5f, 1.7f, 3f), stone, duelZone);
            CreateMarker("Crouch Test Marker", new Vector3(7.6f, 0.03f, 5.8f), new Vector3(3f, 0.05f, 2.2f), accent, duelZone);

            CreateMarker("Player Start Marker", new Vector3(0f, 0.03f, -5.5f), new Vector3(2.4f, 0.05f, 2.4f), accent, duelZone);
            CreateMarker("Enemy Start Marker", new Vector3(0f, 0.03f, 5f), new Vector3(2.4f, 0.05f, 2.4f), accent, duelZone);

            CreateShootingRange(
                parent,
                stone,
                range,
                measurement);
            CreateCloseQuartersCourse(
                parent,
                stone,
                closeQuarters);
            CreateTraversalCourse(
                parent,
                stone,
                traversal,
                measurement);
        }

        private static void CreateShootingRange(
            Transform parent,
            Material stone,
            Material range,
            Material measurement)
        {
            Transform zone =
                CreateZoneRoot("02 - Shooting Range", parent);
            CreateSurfaceMarker(
                "Shooting Range Floor",
                new Vector3(0f, 0.014f, 54f),
                new Vector3(48f, 0.028f, 74f),
                range,
                zone);
            CreateSurfaceMarker(
                "Shooting Range Firing Line",
                new Vector3(0f, 0.032f, 18f),
                new Vector3(48f, 0.035f, 0.35f),
                measurement,
                zone);
            float[] distances = { 15f, 30f, 45f, 60f };
            for (int index = 0; index < distances.Length; index++)
            {
                float distance = distances[index];
                CreateSurfaceMarker(
                    $"{distance:0}m Distance Stripe",
                    new Vector3(
                        0f,
                        0.03f,
                        18f + distance),
                    new Vector3(48f, 0.03f, 0.16f),
                    measurement,
                    zone);
                CreateRangeLabel(
                    $"{distance:0} METERS",
                    new Vector3(
                        -23f,
                        0.08f,
                        18f + distance - 0.8f),
                    zone);
            }

            CreateBlock(
                "Arrow Backstop",
                new Vector3(0f, 3f, 91f),
                new Vector3(50f, 6f, 1f),
                stone,
                zone);
            for (int lane = -2; lane <= 2; lane++)
            {
                CreateSurfaceMarker(
                    $"Lane {lane + 3} Guide",
                    new Vector3(
                        lane * 10f,
                        0.025f,
                        54f),
                    new Vector3(0.12f, 0.025f, 72f),
                    measurement,
                    zone);
            }
        }

        private static void CreateCloseQuartersCourse(
            Transform parent,
            Material stone,
            Material zoneMaterial)
        {
            Transform zone =
                CreateZoneRoot("03 - Close Quarters Course", parent);
            CreateSurfaceMarker(
                "Close Quarters Floor",
                new Vector3(-37f, 0.016f, 4f),
                new Vector3(23f, 0.03f, 34f),
                zoneMaterial,
                zone);
            CreateBlock("CQ Entry Left", new Vector3(-47f, 1.25f, -11f), new Vector3(0.6f, 2.5f, 8f), stone, zone);
            CreateBlock("CQ Entry Right", new Vector3(-27f, 1.25f, -11f), new Vector3(0.6f, 2.5f, 8f), stone, zone);
            CreateBlock("CQ Cover A", new Vector3(-42f, 0.65f, -1f), new Vector3(6f, 1.3f, 1f), stone, zone);
            CreateBlock("CQ Cover B", new Vector3(-32f, 1.1f, 4f), new Vector3(1f, 2.2f, 7f), stone, zone);
            CreateBlock("CQ Cover C", new Vector3(-43f, 1.1f, 9f), new Vector3(8f, 2.2f, 1f), stone, zone);
            CreateBlock("CQ Corner Pillar", new Vector3(-35f, 1.6f, 14f), new Vector3(2f, 3.2f, 2f), stone, zone);
        }

        private static void CreateTraversalCourse(
            Transform parent,
            Material stone,
            Material zoneMaterial,
            Material measurement)
        {
            Transform zone =
                CreateZoneRoot("04 - Traversal And Elevation", parent);
            CreateSurfaceMarker(
                "Traversal Floor",
                new Vector3(37f, 0.016f, 8f),
                new Vector3(24f, 0.03f, 40f),
                zoneMaterial,
                zone);

            for (int step = 0; step < 4; step++)
            {
                float height = (step + 1) * 0.35f;
                CreateBlock(
                    $"Elevation Step {step + 1}",
                    new Vector3(
                        29f + step * 1.5f,
                        height * 0.5f,
                        -5f),
                    new Vector3(1.5f, height, 5f),
                    stone,
                    zone);
            }
            CreateBlock(
                "Elevation Platform",
                new Vector3(38f, 1.5f, -5f),
                new Vector3(10f, 3f, 8f),
                stone,
                zone);
            CreateRotatedBlock(
                "Walkable Ramp",
                new Vector3(44f, 0.75f, -5f),
                new Vector3(8f, 0.45f, 7f),
                new Vector3(0f, 0f, -10.5f),
                stone,
                zone);

            for (int hurdle = 0; hurdle < 4; hurdle++)
            {
                CreateBlock(
                    $"Jump Hurdle {hurdle + 1}",
                    new Vector3(
                        31f,
                        0.35f + hurdle * 0.08f,
                        4f + hurdle * 4.2f),
                    new Vector3(
                        8f,
                        0.7f + hurdle * 0.16f,
                        0.45f),
                    stone,
                    zone);
            }
            CreateBlock("Traversal Crouch Roof", new Vector3(43f, 1.72f, 11f), new Vector3(8f, 0.45f, 8f), stone, zone);
            CreateBlock("Traversal Crouch Left", new Vector3(39.25f, 0.75f, 11f), new Vector3(0.5f, 1.5f, 8f), stone, zone);
            CreateBlock("Traversal Crouch Right", new Vector3(46.75f, 0.75f, 11f), new Vector3(0.5f, 1.5f, 8f), stone, zone);
            CreateSurfaceMarker(
                "Traversal Direction Line",
                new Vector3(31f, 0.035f, 8f),
                new Vector3(0.25f, 0.04f, 34f),
                measurement,
                zone);
        }

        private static void CreateRangedTargets(
            Material bodyMaterial,
            Material secondaryMaterial,
            Material bladeMaterial,
            Material guardMaterial,
            Material gripMaterial)
        {
            Transform targetRoot =
                new GameObject("Ranged Training Targets")
                    .transform;
            Vector3[] positions =
            {
                new Vector3(-18f, 1f, 33f),
                new Vector3(-6f, 1f, 48f),
                new Vector3(6f, 1f, 63f),
                new Vector3(18f, 1f, 78f),
                new Vector3(38f, 4f, -5f)
            };
            string[] names =
            {
                "Range Target - 15m",
                "Range Target - 30m",
                "Range Target - 45m",
                "Range Target - 60m",
                "Elevated Target - 3m Platform"
            };
            for (int index = 0; index < positions.Length; index++)
            {
                GameObject target = CreateEnemy(
                    positions[index],
                    bodyMaterial,
                    secondaryMaterial,
                    bladeMaterial,
                    guardMaterial,
                    gripMaterial,
                    EnemyCombatVariant.CombatLabDummy,
                    out _);
                target.name = names[index];
                target.transform.SetParent(
                    targetRoot,
                    true);
            }
        }

        private static Transform CreateZoneRoot(
            string name,
            Transform parent)
        {
            Transform root =
                new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static GameObject CreatePlayer(
            Vector3 position,
            Material bodyMaterial,
            Material secondaryMaterial,
            Material bladeMaterial,
            Material guardMaterial,
            Material gripMaterial,
            out Health health,
            out PlayerInputSource input)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.layer = 2;
            player.transform.position = position;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<Renderer>().enabled = false;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.36f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.04f;
            controller.stepOffset = 0.22f;

            StableId stableId = player.AddComponent<StableId>();
            stableId.EnsureAssigned();
            health = player.AddComponent<Health>();
            health.Configure(100f);
            input = player.AddComponent<PlayerInputSource>();
            player.AddComponent<CharacterAimSource>();
            ThirdPersonMotor motor = player.AddComponent<ThirdPersonMotor>();
            motor.ConfigureWalkSpeed(
                ThirdPersonMotor.DefaultPlayerWalkSpeed);
            player.AddComponent<MeleeWeapon>();
            CreateHumanoidVisual(
                player,
                motor,
                bodyMaterial,
                secondaryMaterial,
                bladeMaterial,
                guardMaterial,
                gripMaterial);
            Animator playerAnimator =
                player.GetComponentInChildren<Animator>(true);
            HitReactionPresenter hitReaction =
                player.AddComponent<HitReactionPresenter>();
            hitReaction.Configure(
                health,
                playerAnimator != null
                    ? playerAnimator.transform
                    : player.transform,
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    SwordHitAudioPath),
                0.138f);
            return player;
        }

        private static void CreateHumanoidVisual(
            GameObject player,
            ThirdPersonMotor motor,
            Material bodyMaterial,
            Material secondaryMaterial,
            Material bladeMaterial,
            Material guardMaterial,
            Material gripMaterial)
        {
            if (HumanoidAnimationSetup.EnsureGeneratedAssets())
            {
                GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAnimationSetup.ModelPath);
                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);
                if (modelPrefab != null && controller != null)
                {
                    GameObject visual = PrefabUtility.InstantiatePrefab(modelPrefab, player.transform) as GameObject;
                    visual.name = "Humanoid Visual - Authored Locomotion V1";
                    visual.transform.localPosition = Vector3.down;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one * 1.1f;
                    SetLayerRecursively(visual.transform, 2);

                    Transform previewFloor = FindDescendant(visual.transform, "Cube");
                    if (previewFloor != null)
                    {
                        Object.DestroyImmediate(previewFloor.gameObject);
                    }

                    SkinnedMeshRenderer mannequin = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .FirstOrDefault(renderer => renderer.name == "Mannequin");
                    if (mannequin != null)
                    {
                        Material[] materials = mannequin.sharedMaterials;
                        for (int index = 0; index < materials.Length; index++)
                        {
                            materials[index] = index == 0 ? bodyMaterial : secondaryMaterial;
                        }

                        mannequin.sharedMaterials = materials;
                    }

                    Animator animator = visual.GetComponentInChildren<Animator>(true);
                    if (animator == null)
                    {
                        animator = visual.AddComponent<Animator>();
                    }

                    if (mannequin != null &&
                        TryAttachMannequinRenderer(
                            visual,
                            bodyMaterial,
                            secondaryMaterial,
                            HumanoidAnimationSetup.LowPolyMannequinPath,
                            "MannequinLowPoly_Renderer",
                            HumanoidAnimationSetup.LowPolyRuntimeMeshPath,
                            "MannequinLowPoly_Renderer",
                            "MannequinLowPoly_Runtime",
                            out SkinnedMeshRenderer lowPolyFallback))
                    {
                        mannequin.enabled = false;
                        if (TryAttachMannequinRenderer(
                            visual,
                            bodyMaterial,
                            secondaryMaterial,
                            HumanoidAnimationSetup.SeamlessLowPolyMannequinPath,
                            "MannequinSeamlessLowPoly_Renderer",
                            HumanoidAnimationSetup.SeamlessLowPolyRuntimeMeshPath,
                            "MannequinSeamlessLowPoly_Renderer",
                            "MannequinSeamlessLowPoly_Runtime",
                            out _))
                        {
                            lowPolyFallback.enabled = false;
                        }
                    }

                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.updateMode = AnimatorUpdateMode.Normal;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    HumanoidAnimatorPresenter presenter = player.AddComponent<HumanoidAnimatorPresenter>();
                    presenter.Configure(motor, animator);
                    Transform swordRoot = CreateShortSword(
                        animator,
                        bladeMaterial,
                        guardMaterial,
                        gripMaterial);
                    Transform swordBackSocket =
                        CreateShortSwordBackSocket(
                            animator,
                            player.transform);
                    Transform bowBackSocket =
                        CreateBowBackSocket(
                            animator,
                            player.transform);
                    Transform bowRoot = CreateLowPolyBow(
                        bowBackSocket,
                        gripMaterial,
                        guardMaterial,
                        bladeMaterial,
                        out Transform arrowRoot);
                    ShortSwordAttackPresenter attackPresenter =
                        animator.gameObject.AddComponent<ShortSwordAttackPresenter>();
                    attackPresenter.Configure(
                        animator,
                        player.transform,
                        motor,
                        player.GetComponent<MeleeWeapon>(),
                        swordRoot,
                        AssetDatabase.LoadAssetAtPath<AudioClip>(SwordSwingAudioPath));
                    ShortSwordBlockPresenter blockPresenter =
                        animator.gameObject.AddComponent<ShortSwordBlockPresenter>();
                    blockPresenter.Configure(
                        animator,
                        player.GetComponent<PlayerInputSource>(),
                        attackPresenter,
                        player.transform,
                        swordRoot);
                    blockPresenter.ConfigureAuthoredGuardSwordTransform(
                        ShortSwordGuardLocalPosition,
                        ShortSwordGuardLocalRotation,
                        ShortSwordGuardLeftHandLocalRotation);
                    CombatGuard combatGuard =
                        player.GetComponent<CombatGuard>() ??
                        player.AddComponent<CombatGuard>();
                    combatGuard.Configure(blockPresenter);
                    BowWeapon bowWeapon =
                        animator.gameObject.AddComponent<BowWeapon>();
                    bowWeapon.Configure(
                        player.GetComponent<PlayerInputSource>(),
                        player.transform,
                        bowRoot,
                        arrowRoot,
                        AssetDatabase.LoadAssetAtPath<AudioClip>(
                            BowPullbackAudioPath),
                        AssetDatabase.LoadAssetAtPath<AudioClip>(
                            ArrowImpactAudioPath),
                        AssetDatabase.LoadAssetAtPath<AudioClip>(
                            ArrowHitFeedbackAudioPath),
                        AssetDatabase.LoadAssetAtPath<AudioClip>(
                            HeadshotFeedbackAudioPath),
                        AssetDatabase.LoadAssetAtPath<AudioClip>(
                            ArrowFlybyAudioPath));
                    TwoSlotWeaponPresenter loadoutPresenter =
                        animator.gameObject.AddComponent<TwoSlotWeaponPresenter>();
                    loadoutPresenter.Configure(
                        animator,
                        player.GetComponent<PlayerInputSource>(),
                        player.transform,
                        swordRoot,
                        swordBackSocket,
                        bowRoot,
                        bowBackSocket,
                        arrowRoot,
                        bowWeapon,
                        attackPresenter,
                        blockPresenter);
                    UpperBodyAimPresenter aimPresenter =
                        animator.gameObject.AddComponent<UpperBodyAimPresenter>();
                    aimPresenter.Configure(animator, player.transform);
                    HumanoidRagdoll ragdoll =
                        player.GetComponent<HumanoidRagdoll>() ??
                        player.AddComponent<HumanoidRagdoll>();
                    ragdoll.Configure(animator);
                    AimStanceLocomotionPresenter stancePresenter =
                        animator.gameObject.GetComponent<
                            AimStanceLocomotionPresenter>();
                    stancePresenter.Configure(
                        animator,
                        motor,
                        aimPresenter);
                    LocomotionDebugOverlay diagnostics = player.GetComponent<LocomotionDebugOverlay>();
                    if (diagnostics == null)
                    {
                        diagnostics = player.AddComponent<LocomotionDebugOverlay>();
                    }

                    diagnostics.Configure(motor, animator);
                    return;
                }
            }

            Debug.LogWarning("Falling back to the procedural humanoid because authored animation assets are unavailable.");
            CreateProceduralHumanoidVisual(player, motor, bodyMaterial, secondaryMaterial);
        }

        private static bool TryAttachMannequinRenderer(
            GameObject visual,
            Material bodyMaterial,
            Material secondaryMaterial,
            string sourcePath,
            string sourceRendererName,
            string runtimeMeshPath,
            string rendererName,
            string meshName,
            out SkinnedMeshRenderer renderer)
        {
            renderer = null;
            GameObject sourcePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            SkinnedMeshRenderer sourceRenderer = sourcePrefab == null
                ? null
                : sourcePrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(
                        candidate =>
                            candidate.name == sourceRendererName);
            if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
            {
                return false;
            }

            Transform[] playableTransforms =
                visual.GetComponentsInChildren<Transform>(true);
            Transform FindPlayableBone(string boneName)
            {
                return playableTransforms.FirstOrDefault(
                    candidate => candidate.name == boneName);
            }

            Transform[] bones = new Transform[sourceRenderer.bones.Length];
            for (int index = 0; index < sourceRenderer.bones.Length; index++)
            {
                bones[index] = FindPlayableBone(sourceRenderer.bones[index].name);
                if (bones[index] == null)
                {
                    Debug.LogError(
                        $"Playable rig is missing low-poly mesh bone " +
                        $"{sourceRenderer.bones[index].name}.");
                    return false;
                }
            }

            Transform rootBone = sourceRenderer.rootBone == null
                ? null
                : FindPlayableBone(sourceRenderer.rootBone.name);
            if (sourceRenderer.rootBone != null && rootBone == null)
            {
                return false;
            }

            GameObject rendererObject =
                new GameObject(rendererName);
            rendererObject.transform.SetParent(visual.transform, false);
            rendererObject.transform.localPosition =
                sourceRenderer.transform.localPosition;
            rendererObject.transform.localRotation =
                sourceRenderer.transform.localRotation;
            rendererObject.transform.localScale =
                sourceRenderer.transform.localScale;
            SetLayerRecursively(rendererObject.transform, 2);

            renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = bones;
            renderer.rootBone = rootBone;

            Mesh generatedMesh = Object.Instantiate(sourceRenderer.sharedMesh);
            generatedMesh.name = meshName;
            Matrix4x4 rendererLocalToWorld =
                rendererObject.transform.localToWorldMatrix;
            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int index = 0; index < bones.Length; index++)
            {
                bindPoses[index] =
                    bones[index].worldToLocalMatrix * rendererLocalToWorld;
            }

            generatedMesh.bindposes = bindPoses;
            generatedMesh.RecalculateBounds();

            Mesh runtimeMesh =
                AssetDatabase.LoadAssetAtPath<Mesh>(runtimeMeshPath);
            if (runtimeMesh == null)
            {
                AssetDatabase.CreateAsset(generatedMesh, runtimeMeshPath);
                runtimeMesh = generatedMesh;
            }
            else
            {
                EditorUtility.CopySerialized(generatedMesh, runtimeMesh);
                Object.DestroyImmediate(generatedMesh);
                EditorUtility.SetDirty(runtimeMesh);
            }

            renderer.sharedMesh = runtimeMesh;
            renderer.sharedMaterials = runtimeMesh.subMeshCount > 1
                ? new[] { bodyMaterial, secondaryMaterial }
                : new[] { bodyMaterial };
            renderer.localBounds = sourceRenderer.localBounds;
            renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = true;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.reflectionProbeUsage =
                UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return true;
        }

        private static Transform CreateShortSword(
            Animator animator,
            Material bladeMaterial,
            Material guardMaterial,
            Material gripMaterial)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
            {
                Debug.LogWarning("The prototype short sword could not find the humanoid right-hand socket.");
                return null;
            }

            GameObject swordRoot = new GameObject("Equipped Short Sword");
            swordRoot.layer = 2;
            swordRoot.transform.SetParent(hand, false);
            swordRoot.transform.localPosition = ShortSwordCarryLocalPosition;
            swordRoot.transform.localRotation = ShortSwordCarryLocalRotation;
            swordRoot.transform.localScale = ShortSwordCarryLocalScale;

            GameObject grip = CreateVisualPart(
                "Leather Grip",
                PrimitiveType.Cylinder,
                swordRoot.transform,
                new Vector3(0f, 0.09f, 0f),
                new Vector3(0.032f, 0.09f, 0.032f),
                gripMaterial);
            grip.transform.localRotation = Quaternion.identity;

            GameObject pommel = CreateVisualPart(
                "Pommel",
                PrimitiveType.Sphere,
                swordRoot.transform,
                new Vector3(0f, -0.015f, 0f),
                new Vector3(0.075f, 0.055f, 0.055f),
                guardMaterial);
            pommel.transform.localRotation = Quaternion.identity;

            GameObject guard = CreateVisualPart(
                "Crossguard",
                PrimitiveType.Cube,
                swordRoot.transform,
                new Vector3(0f, 0.195f, 0f),
                new Vector3(0.30f, 0.035f, 0.052f),
                guardMaterial);
            guard.transform.localRotation = Quaternion.identity;

            GameObject blade = new GameObject("Pointed Blade");
            blade.layer = 2;
            blade.transform.SetParent(swordRoot.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0.215f, 0f);
            MeshFilter filter = blade.AddComponent<MeshFilter>();
            filter.sharedMesh = GetOrCreateShortSwordBlade();
            MeshRenderer renderer = blade.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = bladeMaterial;
            return swordRoot.transform;
        }

        private static Transform CreateShortSwordBackSocket(
            Animator animator,
            Transform player)
        {
            Transform upperChest =
                animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                animator.GetBoneTransform(HumanBodyBones.Chest);
            if (upperChest == null)
            {
                Debug.LogWarning(
                    "The prototype short sword could not find an upper-chest back socket.");
                return null;
            }

            Vector3 bladeDirection =
                (-player.up - player.right * 0.28f).normalized;
            GameObject socket = new GameObject("Short Sword Back Socket");
            socket.layer = 2;
            socket.transform.position =
                upperChest.position +
                player.up * 0.26f +
                player.right * 0.10f -
                player.forward * 0.16f;
            socket.transform.rotation =
                Quaternion.LookRotation(-player.forward, bladeDirection);
            socket.transform.SetParent(upperChest, true);
            return socket.transform;
        }

        private static Transform CreateBowBackSocket(
            Animator animator,
            Transform player)
        {
            Transform upperChest =
                animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                animator.GetBoneTransform(HumanBodyBones.Chest);
            if (upperChest == null)
            {
                Debug.LogWarning(
                    "The prototype bow could not find an upper-chest back socket.");
                return null;
            }

            Vector3 bowUp =
                (player.up + player.right * 0.24f).normalized;
            GameObject socket = new GameObject("Bow Back Socket");
            socket.layer = 2;
            socket.transform.position =
                upperChest.position +
                player.up * 0.06f -
                player.right * 0.14f -
                player.forward * 0.20f;
            socket.transform.rotation =
                Quaternion.LookRotation(-player.forward, bowUp);
            socket.transform.SetParent(upperChest, true);
            return socket.transform;
        }

        private static Transform CreateLowPolyBow(
            Transform backSocket,
            Material woodMaterial,
            Material stringMaterial,
            Material arrowTipMaterial,
            out Transform arrowRoot)
        {
            Material bowWoodMaterial = GetOrCreateMaterial(
                "BowWood",
                new Color(0.34f, 0.14f, 0.035f),
                0.20f,
                0.04f);
            Material bowTrimMaterial = GetOrCreateMaterial(
                "BowMetalTrim",
                new Color(0.38f, 0.25f, 0.09f),
                0.48f,
                0.62f);
            Material fletchingMaterial = GetOrCreateMaterial(
                "ArrowFletching",
                new Color(0.52f, 0.12f, 0.035f),
                0.12f);
            GameObject bow = new GameObject("Low Poly Bow");
            bow.layer = 2;
            bow.transform.SetParent(backSocket, false);
            bow.transform.localPosition = Vector3.zero;
            bow.transform.localRotation = Quaternion.identity;
            bow.transform.localScale = Vector3.one;

            Vector3 upperInner = new Vector3(0f, 0.13f, 0f);
            Vector3 upperPeak = new Vector3(0f, 0.27f, 0.12f);
            Vector3 upperOuter = new Vector3(0f, 0.40f, -0.03f);
            Vector3 upperTip =
                new Vector3(
                    0f,
                    0.52f,
                    -TwoSlotWeaponPresenter.BowBraceHeight);
            Vector3 lowerInner = new Vector3(0f, -0.13f, 0f);
            Vector3 lowerPeak = new Vector3(0f, -0.27f, 0.12f);
            Vector3 lowerOuter = new Vector3(0f, -0.40f, -0.03f);
            Vector3 lowerTip =
                new Vector3(
                    0f,
                    -0.52f,
                    -TwoSlotWeaponPresenter.BowBraceHeight);
            CreatePolygonBeamBetween(
                "Upper Bow Inner Limb",
                bow.transform,
                upperInner,
                upperPeak,
                0.052f,
                0.070f,
                bowWoodMaterial);
            CreatePolygonBeamBetween(
                "Upper Bow Middle Limb",
                bow.transform,
                upperPeak,
                upperOuter,
                0.048f,
                0.066f,
                bowWoodMaterial);
            CreatePolygonBeamBetween(
                "Upper Bow Tip",
                bow.transform,
                upperOuter,
                upperTip,
                0.042f,
                0.058f,
                bowWoodMaterial);
            CreatePolygonBeamBetween(
                "Lower Bow Inner Limb",
                bow.transform,
                lowerInner,
                lowerPeak,
                0.052f,
                0.070f,
                bowWoodMaterial);
            CreatePolygonBeamBetween(
                "Lower Bow Middle Limb",
                bow.transform,
                lowerPeak,
                lowerOuter,
                0.048f,
                0.066f,
                bowWoodMaterial);
            CreatePolygonBeamBetween(
                "Lower Bow Tip",
                bow.transform,
                lowerOuter,
                lowerTip,
                0.042f,
                0.058f,
                bowWoodMaterial);
            CreatePolygonBeamBetween(
                "Lower Gray Bow Grip",
                bow.transform,
                lowerInner,
                Vector3.zero,
                0.036f,
                0.045f,
                stringMaterial);
            CreatePolygonBeamBetween(
                "Upper Gray Bow Grip",
                bow.transform,
                Vector3.zero,
                upperInner,
                0.036f,
                0.045f,
                stringMaterial);
            CreatePolygonBeamBetween(
                "Upper Grip Collar",
                bow.transform,
                new Vector3(0f, 0.130f, 0f),
                new Vector3(0f, 0.160f, 0f),
                0.052f,
                0.058f,
                bowTrimMaterial);
            CreatePolygonBeamBetween(
                "Lower Grip Collar",
                bow.transform,
                new Vector3(0f, -0.160f, 0f),
                new Vector3(0f, -0.130f, 0f),
                0.052f,
                0.058f,
                bowTrimMaterial);
            for (int wrapIndex = -2; wrapIndex <= 2; wrapIndex++)
            {
                if (Mathf.Abs(wrapIndex) < 2)
                {
                    continue;
                }

                float wrapCenter = wrapIndex * 0.045f;
                CreatePolygonBeamBetween(
                    "Leather Wrap " + (wrapIndex + 3),
                    bow.transform,
                    new Vector3(0f, wrapCenter - 0.0045f, 0f),
                    new Vector3(0f, wrapCenter + 0.0045f, 0f),
                    0.044f,
                    0.050f,
                    bowTrimMaterial);
            }

            Vector3 stringNock =
                new Vector3(
                    0f,
                    0f,
                    -TwoSlotWeaponPresenter.BowBraceHeight);
            CreateCylinderBetween(
                "Upper Bow String",
                bow.transform,
                upperTip,
                stringNock,
                0.004f,
                stringMaterial);
            CreateCylinderBetween(
                "Lower Bow String",
                bow.transform,
                lowerTip,
                stringNock,
                0.004f,
                stringMaterial);

            GameObject arrow = new GameObject("Nocked Arrow");
            arrow.layer = 2;
            arrow.transform.SetParent(bow.transform, false);
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
            arrowRoot = arrow.transform;
            CreateCylinderBetween(
                "Arrow Shaft",
                arrow.transform,
                stringNock,
                new Vector3(0f, 0f, 0.60f),
                0.008f,
                bowWoodMaterial);
            GameObject arrowTip = CreateVisualPart(
                "Arrow Tip",
                PrimitiveType.Cube,
                arrow.transform,
                new Vector3(0f, 0f, 0.62f),
                new Vector3(0.025f, 0.025f, 0.055f),
                arrowTipMaterial);
            arrowTip.transform.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
            GameObject fletchingHorizontal = CreateVisualPart(
                "Arrow Fletching Horizontal",
                PrimitiveType.Cube,
                arrow.transform,
                stringNock + Vector3.forward * 0.055f,
                new Vector3(0.07f, 0.012f, 0.11f),
                fletchingMaterial);
            fletchingHorizontal.transform.localRotation =
                Quaternion.identity;
            GameObject fletchingVertical = CreateVisualPart(
                "Arrow Fletching Vertical",
                PrimitiveType.Cube,
                arrow.transform,
                stringNock + Vector3.forward * 0.055f,
                new Vector3(0.012f, 0.07f, 0.11f),
                fletchingMaterial);
            fletchingVertical.transform.localRotation =
                Quaternion.identity;
            arrow.SetActive(false);
            return bow.transform;
        }

        private static GameObject CreateCylinderBetween(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject cylinder = CreateVisualPart(
                name,
                PrimitiveType.Cylinder,
                parent,
                Vector3.Lerp(start, end, 0.5f),
                new Vector3(
                    radius,
                    direction.magnitude * 0.5f,
                    radius),
                material);
            cylinder.transform.localRotation =
                Quaternion.FromToRotation(Vector3.up, direction.normalized);
            return cylinder;
        }

        private static GameObject CreateBoxBeamBetween(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float width,
            float depth,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject beam = CreateVisualPart(
                name,
                PrimitiveType.Cube,
                parent,
                Vector3.Lerp(start, end, 0.5f),
                new Vector3(
                    width,
                    direction.magnitude,
                    depth),
                material);
            beam.transform.localRotation =
                Quaternion.FromToRotation(Vector3.up, direction.normalized);
            return beam;
        }

        private static GameObject CreatePolygonBeamBetween(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float width,
            float depth,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject beam = new GameObject(name);
            beam.layer = 2;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition =
                Vector3.Lerp(start, end, 0.5f);
            beam.transform.localRotation =
                Quaternion.FromToRotation(Vector3.up, direction.normalized);
            beam.transform.localScale =
                new Vector3(width, direction.magnitude, depth);
            MeshFilter filter = beam.AddComponent<MeshFilter>();
            filter.sharedMesh = GetOrCreateBowLimbBeamMesh();
            MeshRenderer renderer = beam.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return beam;
        }

        private static Mesh GetOrCreateBowLimbBeamMesh()
        {
            Mesh existing =
                AssetDatabase.LoadAssetAtPath<Mesh>(BowLimbBeamPath);
            if (existing != null)
            {
                return existing;
            }

            const int sides = 8;
            float[] ringHeights = { -0.5f, 0f, 0.5f };
            float[] ringScales = { 0.82f, 1f, 0.82f };
            Vector2[] profile =
            {
                new Vector2(-0.35f, -0.50f),
                new Vector2(0.35f, -0.50f),
                new Vector2(0.50f, -0.35f),
                new Vector2(0.50f, 0.35f),
                new Vector2(0.35f, 0.50f),
                new Vector2(-0.35f, 0.50f),
                new Vector2(-0.50f, 0.35f),
                new Vector2(-0.50f, -0.35f)
            };
            Vector3[] vertices =
                new Vector3[ringHeights.Length * sides];
            for (int ring = 0; ring < ringHeights.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    Vector2 point = profile[side] * ringScales[ring];
                    vertices[ring * sides + side] =
                        new Vector3(point.x, ringHeights[ring], point.y);
                }
            }

            int[] triangles = new int[132];
            int triangleIndex = 0;
            for (int ring = 0; ring < ringHeights.Length - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int lower = ring * sides + side;
                    int lowerNext = ring * sides + next;
                    int upper = (ring + 1) * sides + side;
                    int upperNext = (ring + 1) * sides + next;
                    triangles[triangleIndex++] = lower;
                    triangles[triangleIndex++] = upper;
                    triangles[triangleIndex++] = lowerNext;
                    triangles[triangleIndex++] = lowerNext;
                    triangles[triangleIndex++] = upper;
                    triangles[triangleIndex++] = upperNext;
                }
            }

            for (int side = 1; side < sides - 1; side++)
            {
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = side + 1;
                triangles[triangleIndex++] = side;
                int top = (ringHeights.Length - 1) * sides;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = top + side;
                triangles[triangleIndex++] = top + side + 1;
            }

            Mesh mesh = new Mesh
            {
                name = "PrototypeBowLimbBeam",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, BowLimbBeamPath);
            return mesh;
        }

        private static Mesh GetOrCreateShortSwordBlade()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(ShortSwordBladePath);
            if (existing != null)
            {
                return existing;
            }

            const float halfWidth = 0.055f;
            const float halfThickness = 0.012f;
            const float shoulderHeight = 0.64f;
            const float tipHeight = 0.78f;
            Vector3[] vertices =
            {
                new Vector3(-halfWidth, 0f, -halfThickness),
                new Vector3(halfWidth, 0f, -halfThickness),
                new Vector3(-halfWidth, shoulderHeight, -halfThickness),
                new Vector3(halfWidth, shoulderHeight, -halfThickness),
                new Vector3(0f, tipHeight, -halfThickness),
                new Vector3(-halfWidth, 0f, halfThickness),
                new Vector3(halfWidth, 0f, halfThickness),
                new Vector3(-halfWidth, shoulderHeight, halfThickness),
                new Vector3(halfWidth, shoulderHeight, halfThickness),
                new Vector3(0f, tipHeight, halfThickness)
            };
            int[] triangles =
            {
                0, 2, 1, 1, 2, 3, 2, 4, 3,
                5, 6, 7, 6, 8, 7, 7, 8, 9,
                0, 1, 5, 1, 6, 5,
                0, 5, 2, 5, 7, 2,
                1, 3, 6, 3, 8, 6,
                2, 7, 4, 7, 9, 4,
                3, 4, 8, 4, 9, 8
            };

            Mesh mesh = new Mesh
            {
                name = "Prototype Short Sword Blade",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, ShortSwordBladePath);
            return mesh;
        }

        private static void CreateProceduralHumanoidVisual(
            GameObject player,
            ThirdPersonMotor motor,
            Material bodyMaterial,
            Material secondaryMaterial)
        {
            Transform visualRoot = CreatePivot("Humanoid Visual - Procedural Fallback", player.transform, Vector3.zero);
            Transform pelvis = CreatePivot("Pelvis", visualRoot, Vector3.zero);
            Transform body = CreatePivot("Body Weight", pelvis, Vector3.zero);
            CreateVisualPart("Pelvis Shape", PrimitiveType.Cube, body, Vector3.zero, new Vector3(0.42f, 0.20f, 0.29f), secondaryMaterial);

            Transform chest = CreatePivot("Chest", body, new Vector3(0f, 0.1f, 0f));
            CreateVisualPart("Torso", PrimitiveType.Cube, chest, new Vector3(0f, 0.29f, 0f), new Vector3(0.56f, 0.58f, 0.31f), bodyMaterial);
            CreateVisualPart("Head", PrimitiveType.Sphere, chest, new Vector3(0f, 0.73f, 0f), new Vector3(0.35f, 0.42f, 0.35f), bodyMaterial);

            Transform leftThigh = CreatePivot("Left Thigh", pelvis, new Vector3(-0.16f, -0.1f, 0f));
            CreateVisualPart("Left Upper Leg", PrimitiveType.Capsule, leftThigh, new Vector3(0f, -0.25f, 0f), new Vector3(0.16f, 0.27f, 0.16f), secondaryMaterial);
            Transform leftKnee = CreatePivot("Left Knee", leftThigh, new Vector3(0f, -0.48f, 0f));
            CreateVisualPart("Left Lower Leg", PrimitiveType.Capsule, leftKnee, new Vector3(0f, -0.23f, 0f), new Vector3(0.13f, 0.25f, 0.13f), bodyMaterial);
            Transform leftFoot = CreatePivot("Left Foot Pivot", leftKnee, new Vector3(0f, -0.46f, 0f));
            CreateVisualPart("Left Foot", PrimitiveType.Cube, leftFoot, new Vector3(0f, 0.055f, 0.08f), new Vector3(0.18f, 0.11f, 0.33f), secondaryMaterial);

            Transform rightThigh = CreatePivot("Right Thigh", pelvis, new Vector3(0.16f, -0.1f, 0f));
            CreateVisualPart("Right Upper Leg", PrimitiveType.Capsule, rightThigh, new Vector3(0f, -0.25f, 0f), new Vector3(0.16f, 0.27f, 0.16f), secondaryMaterial);
            Transform rightKnee = CreatePivot("Right Knee", rightThigh, new Vector3(0f, -0.48f, 0f));
            CreateVisualPart("Right Lower Leg", PrimitiveType.Capsule, rightKnee, new Vector3(0f, -0.23f, 0f), new Vector3(0.13f, 0.25f, 0.13f), bodyMaterial);
            Transform rightFoot = CreatePivot("Right Foot Pivot", rightKnee, new Vector3(0f, -0.46f, 0f));
            CreateVisualPart("Right Foot", PrimitiveType.Cube, rightFoot, new Vector3(0f, 0.055f, 0.08f), new Vector3(0.18f, 0.11f, 0.33f), secondaryMaterial);

            Transform leftShoulder = CreatePivot("Left Shoulder", chest, new Vector3(-0.36f, 0.48f, 0f));
            CreateVisualPart("Left Upper Arm", PrimitiveType.Capsule, leftShoulder, new Vector3(0f, -0.19f, 0f), new Vector3(0.12f, 0.22f, 0.12f), bodyMaterial);
            Transform leftElbow = CreatePivot("Left Elbow", leftShoulder, new Vector3(0f, -0.38f, 0f));
            CreateVisualPart("Left Forearm", PrimitiveType.Capsule, leftElbow, new Vector3(0f, -0.18f, 0f), new Vector3(0.10f, 0.20f, 0.10f), bodyMaterial);
            CreateVisualPart("Left Hand", PrimitiveType.Sphere, leftElbow, new Vector3(0f, -0.39f, 0f), new Vector3(0.14f, 0.17f, 0.13f), bodyMaterial);

            Transform rightShoulder = CreatePivot("Right Shoulder", chest, new Vector3(0.36f, 0.48f, 0f));
            CreateVisualPart("Right Upper Arm", PrimitiveType.Capsule, rightShoulder, new Vector3(0f, -0.19f, 0f), new Vector3(0.12f, 0.22f, 0.12f), bodyMaterial);
            Transform rightElbow = CreatePivot("Right Elbow", rightShoulder, new Vector3(0f, -0.38f, 0f));
            CreateVisualPart("Right Forearm", PrimitiveType.Capsule, rightElbow, new Vector3(0f, -0.18f, 0f), new Vector3(0.10f, 0.20f, 0.10f), bodyMaterial);
            CreateVisualPart("Right Hand", PrimitiveType.Sphere, rightElbow, new Vector3(0f, -0.39f, 0f), new Vector3(0.14f, 0.17f, 0.13f), bodyMaterial);

            ProceduralHumanoidPresenter presenter = player.AddComponent<ProceduralHumanoidPresenter>();
            presenter.Configure(
                motor,
                pelvis,
                body,
                chest,
                leftThigh,
                rightThigh,
                leftKnee,
                rightKnee,
                leftFoot,
                rightFoot,
                leftShoulder,
                rightShoulder,
                leftElbow,
                rightElbow);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            return descendants.FirstOrDefault(descendant => descendant.name == name);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                descendants[index].gameObject.layer = layer;
            }
        }

        private static GameObject CreateEnemy(
            Vector3 position,
            Material bodyMaterial,
            Material secondaryMaterial,
            Material bladeMaterial,
            Material guardMaterial,
            Material gripMaterial,
            EnemyCombatVariant variant,
            out Health health)
        {
            GameObject enemy = new GameObject("Raider Prototype");
            enemy.name = "Raider Prototype";
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.24f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.05f;

            StableId stableId = enemy.AddComponent<StableId>();
            stableId.EnsureAssigned();
            health = enemy.AddComponent<Health>();
            EnemyDamageProfile damageProfile =
                enemy.AddComponent<EnemyDamageProfile>();
            damageProfile.Configure(variant);
            enemy.AddComponent<PlayerInputSource>();
            enemy.AddComponent<CharacterAimSource>();
            ThirdPersonMotor motor =
                enemy.AddComponent<ThirdPersonMotor>();
            enemy.AddComponent<MeleeWeapon>();
            CreateHumanoidVisual(
                enemy,
                motor,
                bodyMaterial,
                secondaryMaterial,
                bladeMaterial,
                guardMaterial,
                gripMaterial);
            Transform visual =
                enemy.GetComponentInChildren<Animator>(true).transform;
            EnemyBrain brain = enemy.AddComponent<EnemyBrain>();
            brain.ConfigureAsTrainingDummy();
            HitReactionPresenter hitReaction = enemy.AddComponent<HitReactionPresenter>();
            hitReaction.Configure(
                health,
                visual,
                AssetDatabase.LoadAssetAtPath<AudioClip>(SwordHitAudioPath),
                0.138f);
            return enemy;
        }

        private static Transform CreateTrainingDummyVisual(
            Transform parent,
            Material bodyMaterial,
            Material secondaryMaterial)
        {
            if (HumanoidAnimationSetup.EnsureGeneratedAssets())
            {
                GameObject modelPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAnimationSetup.ModelPath);
                RuntimeAnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        HumanoidAnimationSetup.ControllerPath);
                if (modelPrefab != null && controller != null)
                {
                    GameObject visual =
                        PrefabUtility.InstantiatePrefab(modelPrefab, parent) as GameObject;
                    visual.name = "Training Dummy Humanoid Visual";
                    visual.transform.localPosition = Vector3.down;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one * 1.1f;

                    Transform previewFloor = FindDescendant(visual.transform, "Cube");
                    if (previewFloor != null)
                    {
                        Object.DestroyImmediate(previewFloor.gameObject);
                    }

                    SkinnedMeshRenderer[] renderers =
                        visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    for (int rendererIndex = 0;
                         rendererIndex < renderers.Length;
                         rendererIndex++)
                    {
                        Material[] materials = renderers[rendererIndex].sharedMaterials;
                        for (int materialIndex = 0;
                             materialIndex < materials.Length;
                             materialIndex++)
                        {
                            materials[materialIndex] =
                                materialIndex == 0 ? bodyMaterial : secondaryMaterial;
                        }

                        renderers[rendererIndex].sharedMaterials = materials;
                    }

                    Animator animator = visual.GetComponentInChildren<Animator>(true);
                    if (animator == null)
                    {
                        animator = visual.AddComponent<Animator>();
                    }

                    SkinnedMeshRenderer mannequin =
                        visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .FirstOrDefault(renderer => renderer.name == "Mannequin");
                    if (mannequin != null &&
                        TryAttachMannequinRenderer(
                            visual,
                            bodyMaterial,
                            secondaryMaterial,
                            HumanoidAnimationSetup.SeamlessLowPolyMannequinPath,
                            "MannequinSeamlessLowPoly_Renderer",
                            HumanoidAnimationSetup.SeamlessLowPolyRuntimeMeshPath,
                            "MannequinSeamlessLowPoly_Renderer",
                            "MannequinSeamlessLowPoly_Runtime",
                            out _))
                    {
                        mannequin.enabled = false;
                    }

                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.updateMode = AnimatorUpdateMode.Normal;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    StationaryHumanoidPresenter stationaryPresenter =
                        animator.gameObject.AddComponent<StationaryHumanoidPresenter>();
                    stationaryPresenter.Configure(animator);
                    return visual.transform;
                }
            }

            Debug.LogWarning(
                "The humanoid training dummy could not load, so the capsule fallback was used.");
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "Training Dummy Visual - Fallback";
            fallback.transform.SetParent(parent, false);
            Object.DestroyImmediate(fallback.GetComponent<CapsuleCollider>());
            fallback.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            return fallback.transform;
        }

        private static void CreateCamera(Transform target, PlayerInputSource input)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            GameObject aimTargetObject = new GameObject("Camera Aim Target");
            CameraAimTarget aimTarget = aimTargetObject.AddComponent<CameraAimTarget>();
            aimTarget.Configure(target, input);

            GameObject cinematicCameraObject = new GameObject("Movement Camera");
            CinemachineCamera cinematicCamera = cinematicCameraObject.AddComponent<CinemachineCamera>();
            cinematicCamera.Follow = aimTargetObject.transform;
            cinematicCamera.Lens.FieldOfView = 62f;
            cinematicCamera.Lens.NearClipPlane = 0.08f;
            cinematicCamera.Lens.FarClipPlane = 250f;

            CinemachineThirdPersonFollow follow = cinematicCameraObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.Damping = new Vector3(0.08f, 0.09f, 0.12f);
            follow.ShoulderOffset = new Vector3(0.62f, 0f, 0f);
            follow.VerticalArmLength = 0.08f;
            follow.CameraSide = 1f;
            follow.CameraDistance = 4.7f;
            CinemachineThirdPersonFollow.ObstacleSettings obstacles = follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.CollisionFilter = ~(1 << 2);
            obstacles.IgnoreTag = string.Empty;
            obstacles.CameraRadius = 0.2f;
            obstacles.DampingIntoCollision = 0.05f;
            obstacles.DampingFromCollision = 0.28f;
            follow.AvoidObstacles = obstacles;
        }

        private static Transform CreatePivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject pivot = new GameObject(name);
            pivot.layer = 2;
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPosition;
            return pivot.transform;
        }

        private static GameObject CreateVisualPart(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.layer = 2;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return part;
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

        private static GameObject CreateRotatedBlock(
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 eulerAngles,
            Material material,
            Transform parent)
        {
            GameObject block = CreateBlock(
                name,
                position,
                scale,
                material,
                parent);
            block.transform.rotation =
                Quaternion.Euler(eulerAngles);
            return block;
        }

        private static GameObject CreateSurfaceMarker(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            GameObject marker =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            marker.transform.localScale = scale;
            marker.GetComponent<Renderer>()
                .sharedMaterial = material;
            Object.DestroyImmediate(
                marker.GetComponent<BoxCollider>());
            GameObjectUtility.SetStaticEditorFlags(
                marker,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic);
            return marker;
        }

        private static void CreateRangeLabel(
            string text,
            Vector3 position,
            Transform parent)
        {
            GameObject label =
                new GameObject($"Range Label - {text}");
            label.transform.SetParent(parent);
            label.transform.position = position;
            label.transform.rotation =
                Quaternion.Euler(0f, 90f, 0f);
            TextMesh textMesh =
                label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.12f;
            textMesh.color =
                new Color(0.88f, 0.84f, 0.62f);
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

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            float smoothness = 0.15f,
            float metallic = 0f,
            bool matte = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_EnvironmentReflections"))
            {
                material.SetFloat("_EnvironmentReflections", matte ? 0f : 1f);
            }

            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", matte ? 0f : 1f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureProjectFolders()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project", "Art");
            EnsureFolder("Assets/_Project/Art", "Prototype");
            EnsureFolder("Assets/_Project/Art/Prototype", "Materials");
            EnsureFolder("Assets/_Project/Art/Prototype", "Weapons");
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
        static CombatLabFirstImport()
        {
            EditorApplication.delayCall += TryBuildInitialScene;
        }

        private static void TryBuildInitialScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(CombatLabSceneBuilder.ScenePath))
            {
                CombatLabSceneBuilder.Build();
                return;
            }

            string sceneContents = File.ReadAllText(CombatLabSceneBuilder.ScenePath);
            if (!sceneContents.Contains(CombatLabSceneBuilder.CheckpointMarkerName))
            {
                CombatLabSceneBuilder.Build();
            }
        }
    }
}
