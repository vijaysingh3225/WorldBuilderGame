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
            "Sword Generator Lab - V8 Silhouette Inset Blades";
        public const string ColumnBladeTextureFolder =
            "Assets/_Project/Art/Weapons/ColumnBlade/Textures";
        public const string ColumnBladeStoneTexturePath =
            ColumnBladeTextureFolder + "/ColumnBladeStone.png";
        public const string ColumnBladeWoodTexturePath =
            ColumnBladeTextureFolder + "/ColumnBladeWood.png";
        public const string ColumnBladeObsidianTexturePath =
            ColumnBladeTextureFolder + "/ColumnBladeObsidian.png";
        public const string ColumnBladeMaterialFolder =
            "Assets/_Project/Art/Prototype/Materials";
        public const string ColumnBladeStoneMaterialPath =
            ColumnBladeMaterialFolder + "/ColumnBladeStone.mat";
        public const string ColumnBladeWoodMaterialPath =
            ColumnBladeMaterialFolder + "/ColumnBladeWood.mat";
        public const string ColumnBladeObsidianMaterialPath =
            ColumnBladeMaterialFolder + "/ColumnBladeObsidian.mat";
        public static readonly int[] ColumnBladeCaptureSeeds =
            { 2405, 2413 };
        // Marker path supports repeatable evaluation capture from this editor.
        public const string ColumnBladeCaptureRequestPath =
            "Temp/WorldBuilder.CaptureColumnBladeMatrix";
        public const string ColumnBladeInsetCaptureRequestPath =
            "Temp/WorldBuilder.CaptureColumnBladeInsetMatrix";

        [InitializeOnLoadMethod]
        private static void UpgradeGeneratedLabWhenNeeded()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += TryUpgradeGeneratedLab;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += TryUpgradeGeneratedLab;
            }
        }

        private static void TryUpgradeGeneratedLab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !System.IO.File.Exists(ScenePath))
            {
                return;
            }

            string sceneSource = System.IO.File.ReadAllText(ScenePath);
            if (sceneSource.Contains("Procedural Column Blade") &&
                sceneSource.Contains(InfrastructureMarkerName))
            {
                return;
            }

            Build();
        }

        [MenuItem("WorldBuilder/Build/Short Sword Generator Lab")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.19f, 0.20f, 0.205f);

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
            Material backdropUpperMaterial =
                CombatLabSceneBuilder.GetStandardMaterial(
                    "SwordGeneratorBackdropUpper",
                    new Color(0.022f, 0.029f, 0.034f),
                    0.03f);
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
            Material columnStoneMaterial = CreateColumnBladeMaterial(
                "ColumnBladeStone",
                ColumnBladeStoneTexturePath,
                0.05f,
                0f);
            Material columnWoodMaterial = CreateColumnBladeMaterial(
                "ColumnBladeWood",
                ColumnBladeWoodTexturePath,
                0.10f,
                0f);
            Material columnObsidianMaterial = CreateColumnBladeMaterial(
                "ColumnBladeObsidian",
                ColumnBladeObsidianTexturePath,
                0.42f,
                0.06f);
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
                "Studio Lower Backdrop",
                new Vector3(0f, 1.45f, 3.2f),
                new Vector3(16f, 3.2f, 0.35f),
                backdropMaterial,
                environment.transform);
            CombatLabSceneBuilder.CreateStandardBlock(
                "Studio Upper Backdrop",
                new Vector3(0f, 4.65f, 3.22f),
                new Vector3(16f, 3.25f, 0.35f),
                backdropUpperMaterial,
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

            GameObject columnBladeRoot =
                new GameObject("Procedural Column Blade");
            columnBladeRoot.transform.position = swordRoot.transform.position;
            columnBladeRoot.transform.localScale = swordRoot.transform.localScale;
            ProceduralColumnBladeGenerator columnBladeGenerator =
                columnBladeRoot.AddComponent<ProceduralColumnBladeGenerator>();
            columnBladeGenerator.ConfigureMaterials(
                columnStoneMaterial,
                columnWoodMaterial,
                columnObsidianMaterial,
                guardMaterial,
                handleMaterial);
            columnBladeRoot.SetActive(false);

            GameObject systems = new GameObject(InfrastructureMarkerName);
            ShortSwordGeneratorLabController controller =
                systems.AddComponent<ShortSwordGeneratorLabController>();
            controller.Configure(
                generator,
                swordRoot.transform,
                columnBladeGenerator,
                columnBladeRoot.transform);

            CreateCamera();
            CreateDisplayLights();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            GameplaySceneRegistry.ApplyExistingScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"WorldBuilder Short Sword Generator Lab generated at {ScenePath}");
        }

        private static Material CreateColumnBladeMaterial(
            string name,
            string texturePath,
            float smoothness,
            float metallic)
        {
            ConfigureColumnBladeTextureImporter(texturePath);
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Material material =
                CombatLabSceneBuilder.GetStandardMaterial(
                    name,
                    Color.white,
                    smoothness,
                    metallic);
            if (texture == null)
            {
                Debug.LogWarning(
                    $"Column Blade texture is missing at {texturePath}.");
                return material;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureColumnBladeTextureImporter(
            string texturePath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool needsImport =
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Trilinear ||
                importer.anisoLevel != 4 ||
                importer.sRGBTexture == false ||
                importer.mipmapEnabled == false ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression !=
                    TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize != 2048;
            if (!needsImport)
            {
                return;
            }

            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
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
            ProceduralColumnBladeGenerator columnBladeGenerator =
                Object.FindFirstObjectByType<ProceduralColumnBladeGenerator>(
                    FindObjectsInactive.Include);
            ShortSwordGeneratorLabController controller =
                Object.FindFirstObjectByType<ShortSwordGeneratorLabController>();
            Camera camera = Camera.main;
            if (generator == null ||
                columnBladeGenerator == null ||
                controller == null ||
                camera == null)
            {
                throw new System.InvalidOperationException(
                    "The Sword Generator Lab is missing a generator, its controller, or its camera.");
            }
            int seed = 1201;
            string seedText = System.Environment.GetEnvironmentVariable(
                "SHORT_SWORD_LAB_SEED");
            if (!string.IsNullOrWhiteSpace(seedText) &&
                int.TryParse(seedText, out int requestedSeed))
            {
                seed = requestedSeed;
            }
            string familyText = System.Environment.GetEnvironmentVariable(
                "SWORD_LAB_FAMILY");
            bool captureColumnBlade = string.Equals(
                familyText,
                "column",
                System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    familyText,
                    "column-blade",
                    System.StringComparison.OrdinalIgnoreCase);
            if (captureColumnBlade)
            {
                string materialText =
                    System.Environment.GetEnvironmentVariable(
                        "COLUMN_BLADE_MATERIAL");
                if (System.Enum.TryParse(
                        materialText,
                        true,
                        out ColumnBladeMaterial requestedMaterial))
                {
                    columnBladeGenerator.SetBladeMaterial(
                        requestedMaterial,
                        regenerateCurrent: false);
                }
                controller.SelectFamily(SwordGeneratorFamily.ColumnBlade);
                columnBladeGenerator.Generate(seed);
            }
            else
            {
                generator.Generate(seed);
            }
            string crackText = System.Environment.GetEnvironmentVariable(
                "SHORT_SWORD_LAB_CRACK");
            if (!captureColumnBlade &&
                (crackText == "1" ||
                string.Equals(
                    crackText,
                    "true",
                    System.StringComparison.OrdinalIgnoreCase)))
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

        public static string[] GetColumnBladeCaptureFileNames()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (ColumnBladeMaterial material in
                     System.Enum.GetValues(typeof(ColumnBladeMaterial)))
            {
                foreach (int seed in ColumnBladeCaptureSeeds)
                {
                    names.Add($"{material}-{seed}-front-three-quarter.png");
                    names.Add($"{material}-{seed}-side-readability.png");
                }
            }
            return names.ToArray();
        }

        [MenuItem("WorldBuilder/Capture/Column Blade Evaluation Matrix")]
        public static void CaptureColumnBladeEvaluationMatrix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException(
                    "Exit Play mode before capturing the Column Blade matrix.");
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ProceduralColumnBladeGenerator generator =
                Object.FindFirstObjectByType<ProceduralColumnBladeGenerator>(
                    FindObjectsInactive.Include);
            ShortSwordGeneratorLabController controller =
                Object.FindFirstObjectByType<ShortSwordGeneratorLabController>();
            Camera camera = Camera.main;
            if (generator == null || controller == null || camera == null)
            {
                throw new System.InvalidOperationException(
                    "The Column Blade capture setup is incomplete.");
            }

            string outputFolder = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "../Artifacts/ColumnBladeMatrix"));
            System.IO.Directory.CreateDirectory(outputFolder);
            controller.SelectFamily(SwordGeneratorFamily.ColumnBlade);
            foreach (ColumnBladeMaterial material in
                     System.Enum.GetValues(typeof(ColumnBladeMaterial)))
            {
                generator.SetBladeMaterial(material, regenerateCurrent: false);
                foreach (int seed in ColumnBladeCaptureSeeds)
                {
                    generator.Generate(seed);
                    generator.transform.rotation =
                        Quaternion.Euler(0f, -18f, 0f);
                    CaptureCameraToPath(
                        camera,
                        System.IO.Path.Combine(
                            outputFolder,
                            $"{material}-{seed}-front-three-quarter.png"),
                        960,
                        960);
                    generator.transform.rotation =
                        Quaternion.Euler(0f, 78f, 0f);
                    CaptureCameraToPath(
                        camera,
                        System.IO.Path.Combine(
                            outputFolder,
                            $"{material}-{seed}-side-readability.png"),
                        960,
                        960);
                }
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log(
                $"Column Blade evaluation matrix captured to {outputFolder}");
        }

        [MenuItem("WorldBuilder/Capture/Column Blade Inset Matrix")]
        public static void CaptureColumnBladeInsetMatrix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException(
                    "Exit Play mode before capturing the inset matrix.");
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ProceduralColumnBladeGenerator generator =
                Object.FindFirstObjectByType<ProceduralColumnBladeGenerator>(
                    FindObjectsInactive.Include);
            ShortSwordGeneratorLabController controller =
                Object.FindFirstObjectByType<ShortSwordGeneratorLabController>();
            Camera camera = Camera.main;
            if (generator == null || controller == null || camera == null)
            {
                throw new System.InvalidOperationException(
                    "The Column Blade inset capture setup is incomplete.");
            }

            string outputFolder = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "../Artifacts/ColumnBladeInsetMatrix"));
            System.IO.Directory.CreateDirectory(outputFolder);
            controller.SelectFamily(SwordGeneratorFamily.ColumnBlade);
            Vector3 originalCameraPosition = camera.transform.position;
            Quaternion originalCameraRotation = camera.transform.rotation;
            float originalFieldOfView = camera.fieldOfView;
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.SilhouetteInset);
            foreach (ColumnBladeShapeCategory shape in
                     System.Enum.GetValues(typeof(ColumnBladeShapeCategory)))
            {
                generator.ToggleShapeCategoryLock(shape);
                generator.ToggleEdgeStyleLock(
                    shape == ColumnBladeShapeCategory.SquareBlock
                        ? ColumnBladeEdgeStyle.Plain
                        : ColumnBladeEdgeStyle.TwinSideEdges);
                foreach (ColumnBladeSilhouetteWallProfile wallProfile in
                         System.Enum.GetValues(
                             typeof(ColumnBladeSilhouetteWallProfile)))
                {
                    generator.ToggleSilhouetteWallProfileLock(wallProfile);
                    foreach (ColumnBladeMaterial material in
                             System.Enum.GetValues(
                                 typeof(ColumnBladeMaterial)))
                    {
                        generator.SetBladeMaterial(
                            material,
                            regenerateCurrent: false);
                        generator.Generate(
                            7619 + (int)shape * 37 +
                            (int)wallProfile * 101);
                        camera.transform.SetPositionAndRotation(
                            originalCameraPosition,
                            originalCameraRotation);
                        camera.fieldOfView = originalFieldOfView;
                        generator.transform.rotation =
                            Quaternion.Euler(0f, -12f, 0f);
                        CaptureCameraToPath(
                            camera,
                            System.IO.Path.Combine(
                                outputFolder,
                                $"{shape}-{wallProfile}-{material}-front.png"),
                            960,
                            960);
                        generator.transform.rotation = Quaternion.identity;
                        FrameColumnBladeTopCloseUp(camera, generator);
                        CaptureCameraToPath(
                            camera,
                            System.IO.Path.Combine(
                                outputFolder,
                                $"{shape}-{wallProfile}-{material}-top.png"),
                            960,
                            960);
                    }
                }
            }
            camera.transform.SetPositionAndRotation(
                originalCameraPosition,
                originalCameraRotation);
            camera.fieldOfView = originalFieldOfView;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log(
                $"Column Blade inset matrix captured to {outputFolder}");
        }

        private static void FrameColumnBladeTopCloseUp(
            Camera camera,
            ProceduralColumnBladeGenerator generator)
        {
            Renderer blade = null;
            foreach (GameObject part in generator.GeneratedParts)
            {
                if (part != null &&
                    part.name == ProceduralColumnBladeGenerator.BladePartName)
                {
                    blade = part.GetComponent<Renderer>();
                    break;
                }
            }
            if (blade == null)
            {
                throw new System.InvalidOperationException(
                    "The generated blade renderer is unavailable.");
            }

            Bounds bounds = blade.bounds;
            Vector3 target = new Vector3(
                bounds.center.x,
                bounds.max.y - Mathf.Min(0.035f, bounds.size.y * 0.04f),
                bounds.center.z);
            float distance = Mathf.Max(
                0.22f,
                Mathf.Max(bounds.size.x, bounds.size.z) * 3.2f);
            camera.transform.position = target + new Vector3(
                distance * 0.72f,
                distance * 0.88f,
                -distance);
            camera.transform.LookAt(target);
            camera.fieldOfView = 31f;
        }

        [MenuItem("WorldBuilder/Capture/Column Blade Ring Guard Matrix")]
        public static void CaptureColumnBladeRingGuardMatrix()
        {
            CaptureColumnBladeRingGuardMatrixInternal(
                forceSilhouetteInset: false,
                "ColumnBladeRingGuardMatrix");
        }

        [MenuItem("WorldBuilder/Capture/Column Blade Ring Cohesion Matrix")]
        public static void CaptureColumnBladeRingCohesionMatrix()
        {
            CaptureColumnBladeRingGuardMatrixInternal(
                forceSilhouetteInset: true,
                "ColumnBladeRingCohesionMatrix");
        }

        private static void CaptureColumnBladeRingGuardMatrixInternal(
            bool forceSilhouetteInset,
            string outputFolderName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException(
                    "Exit Play mode before capturing the ring guard matrix.");
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ProceduralColumnBladeGenerator generator =
                Object.FindFirstObjectByType<ProceduralColumnBladeGenerator>(
                    FindObjectsInactive.Include);
            ShortSwordGeneratorLabController controller =
                Object.FindFirstObjectByType<ShortSwordGeneratorLabController>();
            Camera camera = Camera.main;
            if (generator == null || controller == null || camera == null)
            {
                throw new System.InvalidOperationException(
                    "The Column Blade ring capture setup is incomplete.");
            }

            string outputFolder = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    Application.dataPath,
                    $"../Artifacts/{outputFolderName}"));
            System.IO.Directory.CreateDirectory(outputFolder);
            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            float originalFieldOfView = camera.fieldOfView;
            controller.SelectFamily(SwordGeneratorFamily.ColumnBlade);
            generator.ToggleEngravingStyleLock(
                forceSilhouetteInset
                    ? ColumnBladeEngravingStyle.SilhouetteInset
                    : ColumnBladeEngravingStyle.None);

            foreach (ColumnBladeShapeCategory shape in new[]
                     {
                         ColumnBladeShapeCategory.FlatThin,
                         ColumnBladeShapeCategory.WideFlat
                     })
            {
                generator.ToggleShapeCategoryLock(shape);
                if (!generator.IsGuardProfileLocked(
                        ColumnBladeGuardProfile.Ring))
                {
                    generator.ToggleGuardProfileLock(
                        ColumnBladeGuardProfile.Ring);
                }
                for (int variant = 0; variant < 3; variant++)
                {
                    int seed = FindRingGuardPaletteSeed(
                        variant,
                        9001 + (int)shape * 997);
                    foreach (ColumnBladeMaterial material in
                             System.Enum.GetValues(
                                 typeof(ColumnBladeMaterial)))
                    {
                        generator.SetBladeMaterial(
                            material,
                            regenerateCurrent: false);
                        generator.Generate(seed);
                        camera.transform.SetPositionAndRotation(
                            originalPosition,
                            originalRotation);
                        camera.fieldOfView = originalFieldOfView;
                        generator.transform.rotation =
                            Quaternion.Euler(0f, -12f, 0f);
                        string prefix =
                            $"{shape}-{material}-palette-{variant + 1}";
                        CaptureCameraToPath(
                            camera,
                            System.IO.Path.Combine(
                                outputFolder,
                                $"{prefix}-full.png"),
                            960,
                            960);
                        generator.transform.rotation = Quaternion.identity;
                        FrameColumnBladeRingCloseUp(camera, generator);
                        CaptureCameraToPath(
                            camera,
                            System.IO.Path.Combine(
                                outputFolder,
                                $"{prefix}-guard.png"),
                            960,
                            960);
                    }
                }
            }
            camera.transform.SetPositionAndRotation(
                originalPosition,
                originalRotation);
            camera.fieldOfView = originalFieldOfView;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log(
                $"Column Blade ring matrix captured to {outputFolder}");
        }

        private static int FindRingGuardPaletteSeed(
            int variant,
            int firstSeed)
        {
            for (int seed = firstSeed; seed < firstSeed + 4096; seed++)
            {
                if (ProceduralColumnBladeGenerator
                        .ResolveRingGuardColorVariant(seed) == variant)
                {
                    return seed;
                }
            }
            throw new System.InvalidOperationException(
                $"No seed resolved ring guard palette {variant}.");
        }

        private static void FrameColumnBladeRingCloseUp(
            Camera camera,
            ProceduralColumnBladeGenerator generator)
        {
            Renderer guard = null;
            foreach (GameObject part in generator.GeneratedParts)
            {
                if (part != null &&
                    part.name == ProceduralColumnBladeGenerator.GuardPartName)
                {
                    guard = part.GetComponent<Renderer>();
                    break;
                }
            }
            if (guard == null)
            {
                throw new System.InvalidOperationException(
                    "The generated ring guard renderer is unavailable.");
            }
            Bounds bounds = guard.bounds;
            float distance = Mathf.Max(
                0.24f,
                Mathf.Max(bounds.size.x, bounds.size.y) * 2.6f);
            camera.transform.position = bounds.center + new Vector3(
                distance * 0.22f,
                distance * 0.08f,
                -distance);
            camera.transform.LookAt(bounds.center);
            camera.fieldOfView = 30f;
        }

        [InitializeOnLoadMethod]
        private static void CaptureColumnBladeMatrixIfRequested()
        {
            if (System.IO.File.Exists(
                    ColumnBladeInsetCaptureRequestPath))
            {
                System.IO.File.Delete(ColumnBladeInsetCaptureRequestPath);
                EditorApplication.update -=
                    CaptureColumnBladeInsetMatrixWhenReady;
                EditorApplication.update +=
                    CaptureColumnBladeInsetMatrixWhenReady;
                return;
            }
            if (!System.IO.File.Exists(ColumnBladeCaptureRequestPath))
            {
                return;
            }
            System.IO.File.Delete(ColumnBladeCaptureRequestPath);
            EditorApplication.delayCall +=
                CaptureColumnBladeEvaluationMatrix;
        }

        private static void CaptureColumnBladeInsetMatrixWhenReady()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            EditorApplication.update -=
                CaptureColumnBladeInsetMatrixWhenReady;
            CaptureColumnBladeInsetMatrix();
        }

        private static void CaptureCameraToPath(
            Camera camera,
            string outputPath,
            int width,
            int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                width,
                height,
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
                    width,
                    height,
                    TextureFormat.RGB24,
                    false);
                image.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0);
                image.Apply(false, false);
                System.IO.File.WriteAllBytes(
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
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.023f, 0.027f);
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
            key.color = new Color(1f, 0.98f, 0.94f);
            key.intensity = 5.2f;
            key.range = 12f;
            key.spotAngle = 64f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.68f;
            keyObject.transform.position = new Vector3(-0.4f, 4.5f, -2.4f);
            keyObject.transform.LookAt(new Vector3(1.75f, 1.7f, 0.3f));

            GameObject rimObject = new GameObject("Sword Rim Light");
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.color = new Color(0.50f, 0.65f, 0.72f);
            rim.intensity = 3.0f;
            rim.range = 10f;
            rim.spotAngle = 58f;
            rim.shadows = LightShadows.Soft;
            rim.shadowStrength = 0.42f;
            rimObject.transform.position = new Vector3(3.8f, 3.4f, 2.4f);
            rimObject.transform.LookAt(new Vector3(1.75f, 1.6f, 0.3f));

            GameObject fillObject = new GameObject("Sword Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Spot;
            fill.color = new Color(0.72f, 0.78f, 0.80f);
            fill.intensity = 2.0f;
            fill.range = 9f;
            fill.spotAngle = 70f;
            fill.shadows = LightShadows.None;
            fillObject.transform.position = new Vector3(4.4f, 2.5f, -2.2f);
            fillObject.transform.LookAt(new Vector3(1.75f, 1.65f, 0.3f));
        }
    }
}
