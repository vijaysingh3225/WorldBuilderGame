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
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Editor
{
    public static class CombatLabSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CombatLab.unity";
        public const string CheckpointMarkerName =
            "Prototype Systems - V67 Seamless Player And Dummy";
        private const string MaterialFolder = "Assets/_Project/Art/Prototype/Materials";
        private const string ShortSwordBladePath =
            "Assets/_Project/Art/Prototype/Weapons/PrototypeShortSwordBlade.asset";
        private const string SwordSwingAudioPath =
            "Assets/_Project/Audio/SFX/Sword Swing.mp3";
        private const string SwordHitAudioPath =
            "Assets/_Project/Audio/SFX/Sword Hit.mp3";

        [MenuItem("WorldBuilder/Build Combat Lab")]
        public static void Build()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMaterial = GetOrCreateMaterial("Floor", new Color(0.16f, 0.19f, 0.20f));
            Material wallMaterial = GetOrCreateMaterial("Stone", new Color(0.25f, 0.28f, 0.27f));
            Material accentMaterial = GetOrCreateMaterial("MossAccent", new Color(0.30f, 0.40f, 0.27f));
            Material playerMaterial = GetOrCreateMaterial(
                "Player",
                new Color(0.22f, 0.22f, 0.22f),
                0.05f,
                0f,
                true);
            Material playerSecondaryMaterial = playerMaterial;
            Material enemyMaterial = GetOrCreateMaterial(
                "TrainingDummyRed",
                new Color(0.42f, 0.035f, 0.03f),
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
            GameObject environment = new GameObject("Environment");
            CreateArena(environment.transform, floorMaterial, wallMaterial, accentMaterial);

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
                out Health enemyHealth);
            CreateCamera(player.transform, playerInput);

            GameObject systems = new GameObject(CheckpointMarkerName);
            CombatLabHud hud = systems.AddComponent<CombatLabHud>();
            hud.Configure(playerHealth, enemyHealth);
            systems.AddComponent<GameplayDiagnosticRecorder>();

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

            CreateBlock("Crouch Test Roof", new Vector3(7.6f, 1.95f, 5.8f), new Vector3(4f, 0.5f, 3f), stone, parent);
            CreateBlock("Crouch Test Left Support", new Vector3(5.85f, 0.85f, 5.8f), new Vector3(0.5f, 1.7f, 3f), stone, parent);
            CreateBlock("Crouch Test Right Support", new Vector3(9.35f, 0.85f, 5.8f), new Vector3(0.5f, 1.7f, 3f), stone, parent);
            CreateMarker("Crouch Test Marker", new Vector3(7.6f, 0.03f, 5.8f), new Vector3(3f, 0.05f, 2.2f), accent, parent);

            CreateMarker("Player Start Marker", new Vector3(0f, 0.03f, -5.5f), new Vector3(2.4f, 0.05f, 2.4f), accent, parent);
            CreateMarker("Enemy Start Marker", new Vector3(0f, 0.03f, 5f), new Vector3(2.4f, 0.05f, 2.4f), accent, parent);
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
            ThirdPersonMotor motor = player.AddComponent<ThirdPersonMotor>();
            player.AddComponent<MeleeWeapon>();
            CreateHumanoidVisual(
                player,
                motor,
                bodyMaterial,
                secondaryMaterial,
                bladeMaterial,
                guardMaterial,
                gripMaterial);
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
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                    HumanoidAnimatorPresenter presenter = player.AddComponent<HumanoidAnimatorPresenter>();
                    presenter.Configure(motor, animator);
                    Transform swordRoot = CreateShortSword(
                        animator,
                        player.transform,
                        bladeMaterial,
                        guardMaterial,
                        gripMaterial);
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
                    UpperBodyAimPresenter aimPresenter =
                        animator.gameObject.AddComponent<UpperBodyAimPresenter>();
                    aimPresenter.Configure(animator, player.transform);
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
            renderer.updateWhenOffscreen = false;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.reflectionProbeUsage =
                UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return true;
        }

        private static Transform CreateShortSword(
            Animator animator,
            Transform player,
            Material bladeMaterial,
            Material guardMaterial,
            Material gripMaterial)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform indexKnuckle =
                animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform middleKnuckle =
                animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform littleKnuckle =
                animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if (hand == null || lowerArm == null)
            {
                Debug.LogWarning("The prototype short sword could not find the humanoid right-hand socket.");
                return null;
            }

            Vector3 forearmDirection = (hand.position - lowerArm.position).normalized;
            if (forearmDirection.sqrMagnitude < 0.9f)
            {
                forearmDirection = -player.up;
            }

            Vector3 swordDirection =
                indexKnuckle != null && littleKnuckle != null
                    ? (indexKnuckle.position - littleKnuckle.position).normalized
                    : player.forward;
            Vector3 swordRight =
                Vector3.ProjectOnPlane(forearmDirection, swordDirection).normalized;
            if (swordRight.sqrMagnitude < 0.9f)
            {
                swordRight =
                    Vector3.ProjectOnPlane(player.right, swordDirection).normalized;
            }

            Vector3 swordForward = Vector3.Cross(swordRight, swordDirection).normalized;
            Vector3 knuckleCenter = middleKnuckle != null
                ? middleKnuckle.position
                : indexKnuckle != null && littleKnuckle != null
                    ? (indexKnuckle.position + littleKnuckle.position) * 0.5f
                    : hand.position + swordDirection * 0.13f;
            Vector3 palmCenter = Vector3.Lerp(hand.position, knuckleCenter, 0.68f);
            GameObject swordRoot = new GameObject("Equipped Short Sword");
            swordRoot.layer = 2;
            swordRoot.transform.position = palmCenter - swordDirection * 0.09f;
            swordRoot.transform.rotation = Quaternion.LookRotation(swordForward, swordDirection);
            swordRoot.transform.SetParent(hand, true);

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
            out Health health)
        {
            GameObject enemy = new GameObject("Raider Prototype");
            enemy.name = "Raider Prototype";
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Transform visual = CreateTrainingDummyVisual(
                enemy.transform,
                bodyMaterial,
                secondaryMaterial);

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.05f;

            StableId stableId = enemy.AddComponent<StableId>();
            stableId.EnsureAssigned();
            health = enemy.AddComponent<Health>();
            health.ConfigureWithFloor(88f, 1f);
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
