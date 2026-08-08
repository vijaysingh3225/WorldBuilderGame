using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Editor
{
    public static class ShortSwordGeneratorLabSceneBuilder
    {
        public const string ScenePath =
            "Assets/_Project/Scenes/ShortSwordGeneratorLab.unity";
        public const string InfrastructureMarkerName =
            "Short Sword Generator Lab - V1";

        [MenuItem("WorldBuilder/Build/Short Sword Generator Lab")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CombatLabSceneBuilder.CreateStandardLighting();
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.23f, 0.25f, 0.24f);

            Material floorMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "SwordGeneratorFloor",
                    new Color(0.075f, 0.09f, 0.095f),
                    0.08f);
            Material backdropMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "SwordGeneratorBackdrop",
                    new Color(0.045f, 0.055f, 0.058f),
                    0.04f);
            Material pedestalMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "SwordGeneratorPedestal",
                    new Color(0.15f, 0.16f, 0.15f),
                    0.18f);
            Material bladeMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "ProceduralSwordBlade",
                    new Color(0.62f, 0.66f, 0.67f),
                    0.62f,
                    0.22f);
            Material guardMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "ProceduralSwordGuard",
                    new Color(0.28f, 0.27f, 0.24f),
                    0.40f,
                    0.12f);
            Material handleMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "ProceduralSwordHandle",
                    new Color(0.18f, 0.105f, 0.065f),
                    0.16f);
            Material hiltMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "ProceduralSwordHilt",
                    new Color(0.24f, 0.23f, 0.20f),
                    0.34f,
                    0.08f);

            GameObject environment = new GameObject("Environment");
            CombatLabSceneBuilder.CreateStandardBlock(
                "Studio Floor",
                new Vector3(0f, -0.18f, 0f),
                new Vector3(16f, 0.35f, 11f),
                floorMaterial,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Studio Backdrop",
                new Vector3(0f, 3.1f, 3.2f),
                new Vector3(16f, 6.5f, 0.35f),
                backdropMaterial,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Sword Pedestal",
                new Vector3(1.75f, 0.34f, 0.3f),
                new Vector3(1.15f, 0.68f, 1.15f),
                pedestalMaterial,
                environment.transform);

            GameObject swordRoot = new GameObject("Procedural Short Sword");
            swordRoot.transform.position = new Vector3(1.75f, 1.28f, 0.3f);
            swordRoot.transform.localScale = Vector3.one * 1.55f;
            ProceduralShortSwordGenerator generator =
                swordRoot.AddComponent<ProceduralShortSwordGenerator>();
            generator.ConfigureMaterials(
                bladeMaterial,
                guardMaterial,
                handleMaterial,
                hiltMaterial);

            GameObject systems = new GameObject(InfrastructureMarkerName);
            ShortSwordGeneratorLabController controller =
                systems.AddComponent<ShortSwordGeneratorLabController>();
            controller.Configure(generator, swordRoot.transform);

            CreateCamera();
            CreateDisplayLights();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            GameplaySceneRegistry.ApplyExistingScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"WorldBuilder Short Sword Generator Lab generated at {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        public static void CapturePreviewFromCommandLine()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Build();
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ProceduralShortSwordGenerator generator =
                Object.FindFirstObjectByType<ProceduralShortSwordGenerator>();
            Camera camera = Camera.main;
            if (generator == null || camera == null)
            {
                throw new System.InvalidOperationException(
                    "The Short Sword Generator Lab is missing its generator or camera.");
            }
            int seed = 1201;
            string seedText = System.Environment.GetEnvironmentVariable(
                "SHORT_SWORD_LAB_SEED");
            if (!string.IsNullOrWhiteSpace(seedText) &&
                int.TryParse(seedText, out int requestedSeed))
            {
                seed = requestedSeed;
            }
            generator.Generate(seed);
            string crackText = System.Environment.GetEnvironmentVariable(
                "SHORT_SWORD_LAB_CRACK");
            if (crackText == "1" ||
                string.Equals(
                    crackText,
                    "true",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                generator.CrackBlade();
            }

            string outputPath = System.Environment.GetEnvironmentVariable(
                "SHORT_SWORD_LAB_CAPTURE");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(
                        Application.dataPath,
                        "../Artifacts/ShortSwordGeneratorLab.png"));
            }
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(outputPath));

            RenderTexture target = RenderTexture.GetTemporary(
                1280,
                720,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(
                    1280,
                    720,
                    TextureFormat.RGB24,
                    false);
                image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                image.Apply(false, false);
                System.IO.File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
            Debug.Log($"Short Sword Generator Lab captured: {outputPath}");
        }

        [MenuItem("WorldBuilder/Open/Short Sword Generator Lab")]
        public static void Open()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Build();
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("WorldBuilder/Play/Short Sword Generator Lab")]
        public static void Play()
        {
            Open();
            EditorApplication.isPlaying = true;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.031f, 0.033f);
            camera.fieldOfView = 37f;
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(1.75f, 1.65f, -4.15f);
            cameraObject.transform.LookAt(new Vector3(1.75f, 1.78f, 0.3f));
        }

        private static void CreateDisplayLights()
        {
            GameObject keyObject = new GameObject("Sword Key Light");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.91f, 0.74f);
            key.intensity = 7.5f;
            key.range = 10f;
            key.spotAngle = 48f;
            keyObject.transform.position = new Vector3(-0.4f, 4.5f, -2.4f);
            keyObject.transform.LookAt(new Vector3(1.75f, 1.7f, 0.3f));

            GameObject rimObject = new GameObject("Sword Rim Light");
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.color = new Color(0.44f, 0.63f, 0.72f);
            rim.intensity = 5.5f;
            rim.range = 9f;
            rim.spotAngle = 52f;
            rimObject.transform.position = new Vector3(3.8f, 3.4f, 2.4f);
            rimObject.transform.LookAt(new Vector3(1.75f, 1.6f, 0.3f));
        }
    }
}
