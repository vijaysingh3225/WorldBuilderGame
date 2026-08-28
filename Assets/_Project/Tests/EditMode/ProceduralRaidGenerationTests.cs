using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests
{
    // Advanced graph and feature-enabled integration coverage lives in the
    // focused AdvancedLandformGenerationTests fixture.
    public sealed class ProceduralRaidGenerationTests
    {
        [Test]
        public void VisibleForestFloorUsesOnlyRestrainedGreenOutsideCamps()
        {
            Vector4 forest =
                ProceduralRaidGenerator.VisibleForestGroundWeightsForValidation(
                    new Vector4(0.42f, 0f, 0.18f, 0.25f),
                    0.15f,
                    0f);
            Assert.That(forest.x, Is.EqualTo(0f));
            Assert.That(forest.y, Is.EqualTo(0f));
            Assert.That(forest.z, Is.EqualTo(1f));
            Assert.That(
                forest.w,
                Is.EqualTo(0f),
                "The greenest groundcover tier must never receive visible terrain weight.");

            Vector4 camp =
                ProceduralRaidGenerator.VisibleForestGroundWeightsForValidation(
                    new Vector4(0.42f, 0f, 0.18f, 0.25f),
                    0.15f,
                    1f);
            Assert.That(
                camp.x,
                Is.GreaterThan(0.5f),
                "Camp clearings must retain their authored exposed-loam override.");
            Assert.That(
                camp.w,
                Is.EqualTo(0f),
                "Camp transitions must not reintroduce the retired greenest texture.");
        }

        [Test]
        public void RaidGuardCompositionIsOneQuarterArchers()
        {
            Assert.That(
                ProceduralRaidGenerator.ArcherGuardShare,
                Is.EqualTo(0.25f).Within(0.0001f),
                "Raid patrol and camp guard selection should produce 25% archers and 75% swordsmen.");
        }

        [Test]
        public void RaidGeneratesJumpableDeadfallsAndGuardedWatchtowers()
        {
            const int ProductionSeed = 20260730;
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);

            generator.GenerateWithSeed(ProductionSeed);

            Assert.That(
                generator.GeneratedFallenTreeCrossingCount,
                Is.InRange(4, 6));
            Assert.That(
                generator.GeneratedWatchtowerCount,
                Is.EqualTo(ProceduralRaidGenerator.WatchtowerCount));
            Assert.That(generator.GeneratedWideWatchtowerCount, Is.EqualTo(1));
            Assert.That(
                generator.GeneratedTowerGuardCount,
                Is.EqualTo(ProceduralRaidGenerator.WatchtowerCount));

            Transform generated = generator.transform.Find(
                $"Generated Raid {ProductionSeed}");
            Assert.That(generated, Is.Not.Null);
            Transform crossings = generated.Find(
                "River Fallen Tree Crossings");
            Transform towers = generated.Find("Forest Watchtowers");
            Assert.That(crossings, Is.Not.Null);
            Assert.That(towers, Is.Not.Null);
            Assert.That(
                crossings.GetComponentsInChildren<MeshCollider>(true),
                Has.Length.GreaterThanOrEqualTo(
                    generator.GeneratedFallenTreeCrossingCount));
            Assert.That(
                towers.GetComponentsInChildren<RaidLootContainer>(true),
                Has.Length.EqualTo(ProceduralRaidGenerator.WatchtowerCount));
            Assert.That(
                towers.GetComponentsInChildren<Transform>(true).Count(
                    item => item.name == "Rear Access Ladder"),
                Is.EqualTo(ProceduralRaidGenerator.SkinnyWatchtowerCount));
            LadderClimbPoint[] ladderClimbs =
                towers.GetComponentsInChildren<LadderClimbPoint>(true);
            Assert.That(
                ladderClimbs,
                Has.Length.EqualTo(
                    ProceduralRaidGenerator.SkinnyWatchtowerCount));
            foreach (LadderClimbPoint ladderClimb in ladderClimbs)
            {
                Assert.That(ladderClimb.ClimbHeight, Is.GreaterThan(4f));
                Assert.That(
                    ladderClimb.TopPosition.y,
                    Is.GreaterThan(ladderClimb.BottomPosition.y));
                Assert.That(
                    ladderClimb.ClimbFacing.sqrMagnitude,
                    Is.EqualTo(1f).Within(0.001f));
            }
            Transform wideTower = towers.Find("Wide Watchtower");
            Assert.That(wideTower, Is.Not.Null);
            BoxCollider[] stairRamps = wideTower
                .GetComponentsInChildren<BoxCollider>(true)
                .Where(collider => collider.name.StartsWith(
                    "Wide Tower Stair Ramp"))
                .ToArray();
            Assert.That(
                stairRamps.Length,
                Is.EqualTo(3),
                "Every switchback stair flight needs one continuous invisible walking surface.");
            foreach (BoxCollider stairRamp in stairRamps)
            {
                Assert.That(stairRamp.isTrigger, Is.False);
                Assert.That(
                    stairRamp.size.y,
                    Is.GreaterThanOrEqualTo(0.29f),
                    $"{stairRamp.name} must sit above the authored step noses instead of intersecting them.");
                Assert.That(
                    stairRamp.bounds.size.magnitude,
                    Is.GreaterThan(1.5f),
                    $"{stairRamp.name} must overlap both flight transitions.");
            }

            Physics.SyncTransforms();
            foreach (RaidLootContainer chest in
                     towers.GetComponentsInChildren<RaidLootContainer>(true))
            {
                Bounds chestBounds = VisibleRendererBounds(chest.transform);
                Transform chestTower = chest.transform.parent;
                Assert.That(chestTower, Is.Not.Null);
                Vector3 horizontalOffset =
                    chestBounds.center - chestTower.position;
                horizontalOffset.y = 0f;
                Vector3 chestLongAxis =
                    chest.transform.TransformDirection(Vector3.right);
                chestLongAxis.y = 0f;
                chestLongAxis.Normalize();
                Vector3 wallNormal = Vector3.Cross(
                    Vector3.up,
                    chestLongAxis).normalized;
                Assert.That(
                    Mathf.Abs(Vector3.Dot(horizontalOffset, wallNormal)),
                    Is.GreaterThan(0.42f),
                    $"{chest.name} should be moved out from the deck center against a wall.");
                RaycastHit[] hits = Physics.RaycastAll(
                    new Vector3(
                        chestBounds.center.x,
                        chestBounds.min.y + 0.35f,
                        chestBounds.center.z),
                    Vector3.down,
                    4.5f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                float deckHeight = float.NegativeInfinity;
                for (int hitIndex = 0;
                     hitIndex < hits.Length;
                     hitIndex++)
                {
                    Collider collider = hits[hitIndex].collider;
                    if (collider == null ||
                        collider.transform.IsChildOf(chest.transform) ||
                        hits[hitIndex].normal.y < 0.60f)
                    {
                        continue;
                    }
                    deckHeight = Mathf.Max(
                        deckHeight,
                        hits[hitIndex].point.y);
                }
                Assert.That(
                    deckHeight,
                    Is.GreaterThan(float.NegativeInfinity),
                    $"{chest.name} has no tower deck below it.");
                Assert.That(
                    chestBounds.min.y - deckHeight,
                    Is.InRange(-0.015f, 0.025f),
                    $"{chest.name} must rest directly on its tower deck.");
            }

            FieldInfo patrolRouteField = typeof(EnemyBrain).GetField(
                "patrolRoute",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo passiveSightField = typeof(EnemyBrain).GetField(
                "passiveSightRange",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lookoutMovementModeField = typeof(EnemyBrain).GetField(
                "lookoutMovementMode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo patrolLookDirectionsField = typeof(EnemyBrain).GetField(
                "patrolRouteLookDirections",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(patrolRouteField, Is.Not.Null);
            Assert.That(passiveSightField, Is.Not.Null);
            Assert.That(lookoutMovementModeField, Is.Not.Null);
            Assert.That(patrolLookDirectionsField, Is.Not.Null);
            EnemyBrain[] towerLookouts = Object
                .FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(candidate =>
                    (float)passiveSightField.GetValue(candidate) ==
                        ProceduralRaidGenerator.TowerGuardSightRange)
                .ToArray();
            EnemyBrain wideLookout = towerLookouts
                .FirstOrDefault(candidate =>
                    patrolRouteField.GetValue(candidate) is Vector3[] route &&
                    route.Length == 2);
            Assert.That(wideLookout, Is.Not.Null);
            Vector3[] lookoutRoute =
                (Vector3[])patrolRouteField.GetValue(wideLookout);
            Assert.That(
                Vector3.Distance(lookoutRoute[0], lookoutRoute[1]),
                Is.GreaterThanOrEqualTo(2.5f));
            Assert.That(
                (float)passiveSightField.GetValue(wideLookout),
                Is.EqualTo(ProceduralRaidGenerator.TowerGuardSightRange));
            Assert.That(
                lookoutMovementModeField.GetValue(wideLookout).ToString(),
                Is.EqualTo("PlatformPatrol"));
            Vector3[] wideLookDirections =
                (Vector3[])patrolLookDirectionsField.GetValue(wideLookout);
            Assert.That(wideLookDirections, Has.Length.EqualTo(2));
            Assert.That(
                Vector3.Distance(
                    wideLookout.transform.position,
                    lookoutRoute[0]),
                Is.LessThan(0.05f),
                "The wide lookout must spawn on the first supported deck waypoint.");
            for (int waypointIndex = 0;
                 waypointIndex < lookoutRoute.Length;
                 waypointIndex++)
            {
                Vector3 waypoint = lookoutRoute[waypointIndex];
                RaycastHit[] deckHits = Physics.RaycastAll(
                    waypoint - Vector3.up * 0.15f,
                    Vector3.down,
                    1.5f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                RaycastHit deckHit = deckHits.FirstOrDefault(hit =>
                    hit.collider != null &&
                    hit.collider.transform.IsChildOf(wideTower) &&
                    hit.normal.y >= 0.60f);
                Assert.That(
                    deckHit.collider,
                    Is.Not.Null,
                    $"Wide lookout waypoint {waypointIndex} needs tower deck support.");
                Assert.That(
                    waypoint.y - deckHit.point.y,
                    Is.EqualTo(1f).Within(0.03f));
            }

            EnemyBrain[] skinnyLookouts = towerLookouts
                .Where(candidate =>
                    patrolRouteField.GetValue(candidate) is Vector3[] route &&
                    route.Length == 1)
                .ToArray();
            Assert.That(
                skinnyLookouts,
                Has.Length.EqualTo(
                    ProceduralRaidGenerator.SkinnyWatchtowerCount));
            Assert.That(
                skinnyLookouts.All(candidate =>
                    lookoutMovementModeField.GetValue(candidate).ToString() ==
                        "Stationary"),
                Is.True,
                "Skinny lookouts must scan from a movement-locked post instead of entering the patrol locomotion path.");

            MeshFilter loweredRailingFilter = wideTower
                .GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(filter =>
                    filter.sharedMesh != null &&
                    filter.sharedMesh.name ==
                        "Wide Watchtower Lowered Railings");
            Assert.That(loweredRailingFilter, Is.Not.Null);
            Mesh loweredRailingMesh = loweredRailingFilter.sharedMesh;
            Bounds loweredBounds = loweredRailingMesh.bounds;
            float railingBase = loweredBounds.min.y +
                loweredBounds.size.y * (3.545f / 7.674f);
            float oldRailingTop = loweredBounds.min.y +
                loweredBounds.size.y * (4.824f / 7.674f);
            float loweredRailingTop = loweredRailingMesh.vertices
                .Where(vertex =>
                    vertex.y > railingBase + 0.0005f &&
                    vertex.y < oldRailingTop - 0.0005f)
                .Max(vertex => vertex.y);
            float railingWorldHeight = Vector3.Distance(
                loweredRailingFilter.transform.TransformPoint(
                    new Vector3(0f, railingBase, 0f)),
                loweredRailingFilter.transform.TransformPoint(
                    new Vector3(0f, loweredRailingTop, 0f)));
            Assert.That(
                railingWorldHeight,
                Is.LessThanOrEqualTo(
                    ProceduralRaidGenerator.WideWatchtowerRailingHeight +
                    0.03f));
        }

        [Test]
        public void LegacyRaidSceneReceivesForestSaturationDefault()
        {
            Assert.That(
                ProceduralRaidGenerator.ResolveForestGroundSaturation(0f),
                Is.EqualTo(
                    ProceduralRaidGenerator.DefaultForestGroundSaturation));
            Assert.That(
                ProceduralRaidGenerator.ResolveForestGroundSaturation(0.62f),
                Is.EqualTo(
                    ProceduralRaidGenerator.DefaultForestGroundSaturation),
                "Saved review scenes using the previous muddy tuning should migrate to the brighter restrained value.");
            Assert.That(
                ProceduralRaidGenerator.ResolveForestGroundSaturation(0.78f),
                Is.EqualTo(
                    ProceduralRaidGenerator.DefaultForestGroundSaturation),
                "Playable scenes using the prior restrained tuning should receive the greener revision.");
            Assert.That(
                ProceduralRaidGenerator.ResolveForestGroundSaturation(0.48f),
                Is.EqualTo(0.48f).Within(0.0001f));
        }

        [Test]
        public void DeepOceanShaderUsesTextureIndependentSubtleWaves()
        {
            Shader shader = Shader.Find("WorldBuilder/Deep Ocean");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);

            Material material = null;
            try
            {
                material = new Material(shader);
                Assert.That(material.HasProperty("_BaseMap"), Is.False);
                Assert.That(material.HasProperty("_DeepColor"), Is.True);
                Assert.That(material.HasProperty("_WaveSpeed"), Is.True);
                Assert.That(
                    material.GetFloat("_WaveSpeed"),
                    Is.LessThan(0.08f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void LowHorizonFogShaderCompilesInsteadOfRenderingErrorPurple()
        {
            Shader shader = Shader.Find(
                "WorldBuilder/Low Horizon Fog");
            Assert.That(shader, Is.Not.Null);
            Assert.That(
                shader.isSupported,
                Is.True,
                "Unsupported fog shaders render as Unity's purple error material.");

            Material material = null;
            try
            {
                material = new Material(shader);
                Assert.That(
                    material.renderQueue,
                    Is.GreaterThanOrEqualTo(
                        (int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(material.HasProperty("_BaseColor"), Is.True);
                Assert.That(material.HasProperty("_DepthFadeDistance"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void StackedCampPropsTouchAlongSupportSurface()
        {
            GameObject support =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject upper =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject collisionHelper =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                support.transform.SetPositionAndRotation(
                    new Vector3(3f, 1f, -2f),
                    Quaternion.Euler(9f, 28f, -7f));
                support.transform.localScale =
                    new Vector3(1.5f, 0.8f, 1.2f);
                upper.transform.SetPositionAndRotation(
                    new Vector3(3f, 5f, -2f),
                    Quaternion.Euler(-4f, 71f, 5f));
                upper.transform.localScale =
                    new Vector3(1.35f, 0.7f, 1.1f);

                collisionHelper.name = "UCX_Oversized_Crate_Helper";
                collisionHelper.transform.SetParent(
                    support.transform,
                    false);
                collisionHelper.transform.localPosition =
                    Vector3.up * 4f;
                collisionHelper.transform.localScale =
                    Vector3.one * 3f;
                collisionHelper.GetComponent<Renderer>().enabled = false;

                MethodInfo place = typeof(ProceduralRaidGenerator)
                    .GetMethod(
                        "PlacePropOnTop",
                        BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(place, Is.Not.Null);
                place.Invoke(null, new object[] { upper, support });

                Vector3 stackingAxis = support.transform.up;
                float supportTop = Vector3.Dot(
                    support.transform.TransformPoint(
                        new Vector3(0f, 0.5f, 0f)),
                    stackingAxis);
                float upperBottom = Vector3.Dot(
                    upper.transform.TransformPoint(
                        new Vector3(0f, -0.5f, 0f)),
                    stackingAxis);

                Assert.That(
                    Vector3.Angle(upper.transform.up, stackingAxis),
                    Is.LessThan(0.001f));
                Assert.That(
                    upperBottom - supportTop,
                    Is.EqualTo(-0.003f).Within(0.0002f),
                    "Visible faces should touch without using the disabled collision helper's bounds.");
            }
            finally
            {
                Object.DestroyImmediate(collisionHelper);
                Object.DestroyImmediate(upper);
                Object.DestroyImmediate(support);
            }
        }

        [Test]
        public void PcPipelineUsesGpuDrivenRaidRendering()
        {
            Object pipelineAsset =
                AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/Settings/PC_RPAsset.asset");

            Assert.That(pipelineAsset, Is.Not.Null);
            var pipeline = new SerializedObject(pipelineAsset);
            Assert.That(
                pipeline.FindProperty(
                    "m_GPUResidentDrawerMode").intValue,
                Is.EqualTo(1));
            Assert.That(
                pipeline.
                    FindProperty(
                        "m_GPUResidentDrawerEnableOcclusionCullingInCameras")
                    .boolValue,
                Is.True);
            Assert.That(
                pipeline.FindProperty(
                    "m_RequireOpaqueTexture").boolValue,
                Is.False,
                "The Raid uses no scene-color sampling, so a full opaque-frame copy is unnecessary.");
        }

        [Test]
        public void EnvironmentCullerKeepsAllVisualsButOnlyNearbyPhysicsActive()
        {
            GameObject managerObject =
                new GameObject("Culling Test Manager");
            GameObject anchorObject =
                new GameObject("Culling Test Anchor");
            GameObject rootObject =
                new GameObject("Culling Test Root");
            GameObject near =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject far =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                near.transform.SetParent(rootObject.transform, false);
                far.transform.SetParent(rootObject.transform, false);
                far.transform.position = Vector3.right * 120f;
                RaidEnvironmentCuller culler =
                    managerObject.AddComponent<
                        RaidEnvironmentCuller>();
                culler.Configure(
                    anchorObject.transform,
                    rootObject.transform);
                culler.RefreshImmediately();

                Assert.That(
                    near.GetComponent<Renderer>().enabled,
                    Is.True);
                Assert.That(
                    near.GetComponent<Collider>().enabled,
                    Is.True);
                Assert.That(
                    far.GetComponent<Renderer>().enabled,
                    Is.True);
                Assert.That(
                    far.GetComponent<Collider>().enabled,
                    Is.False);

                anchorObject.transform.position =
                    Vector3.right * 120f;
                culler.RefreshImmediately();
                Assert.That(
                    near.GetComponent<Renderer>().enabled,
                    Is.True);
                Assert.That(
                    far.GetComponent<Renderer>().enabled,
                    Is.True);
                Assert.That(
                    far.GetComponent<Collider>().enabled,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(anchorObject);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void EnvironmentAssetGalleryDisplaysEveryPackVariant()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/" +
                "EnvironmentAssetGallery.unity",
                OpenSceneMode.Single);
            GameObject ground =
                GameObject.Find("Raid Green Ground");
            Assert.That(ground, Is.Not.Null);
            Assert.That(
                ground.GetComponent<MeshRenderer>()
                    .sharedMaterial.mainTexture.name,
                Is.EqualTo(
                    "ForestUndergrass"));
            AssertGalleryRow("01 - All Trees", 11);
            AssertGalleryRow(
                "02 - Bushes Flowers and Plants",
                10);
            AssertGalleryRow("03 - All Rocks", 9);
            AssertGalleryRow("04 - All Grass", 5);
            GameObject studies = GameObject.Find(
                GroundFloraStudyAssetBuilder.GalleryRootName);
            Assert.That(studies, Is.Not.Null);
            Assert.That(
                studies.transform.childCount,
                Is.EqualTo(GroundFloraStudyAssetBuilder.StudyCount));
            foreach (MeshFilter study in
                     studies.GetComponentsInChildren<MeshFilter>())
            {
                Assert.That(study.sharedMesh, Is.Not.Null);
                Assert.That(
                    study.sharedMesh.vertexCount,
                    Is.GreaterThan(20),
                    $"{study.name} should contain authored flora geometry.");
                Assert.That(
                    study.GetComponent<Collider>(),
                    Is.Null,
                    "Gallery flora studies should remain non-blocking.");
            }
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(
                camera.transform.position.y,
                Is.GreaterThan(10f));
        }

        [Test]
        public void GrassMeshesRemainReadableForRuntimeBatching()
        {
            for (int grassIndex = 1;
                 grassIndex <= 5;
                 grassIndex++)
            {
                GameObject grass =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Art/Environment/" +
                        "StylizedForest/Models/" +
                        "Stylized_forest_fbx/" +
                        $"SM_sf_grass_{grassIndex:00}.FBX");
                Assert.That(grass, Is.Not.Null);
                MeshFilter[] filters =
                    grass.GetComponentsInChildren<MeshFilter>(
                        true);
                Assert.That(filters, Is.Not.Empty);
                foreach (MeshFilter filter in filters)
                {
                    if (filter.name.StartsWith("UCX_"))
                    {
                        continue;
                    }
                    Assert.That(
                        filter.sharedMesh.isReadable,
                        Is.True,
                        $"{grass.name} must allow runtime mesh batching.");
                }
            }
        }

        [Test]
        public void UndergrowthMeshesRemainReadableForRuntimeBatching()
        {
            string[] names =
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
            foreach (string name in names)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Art/Environment/" +
                        "StylizedForest/Models/" +
                        $"Stylized_forest_fbx/{name}.FBX");
                Assert.That(prefab, Is.Not.Null);
                foreach (MeshFilter filter in
                    prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.name.StartsWith("UCX_"))
                    {
                        continue;
                    }
                    Assert.That(
                        filter.sharedMesh.isReadable,
                        Is.True,
                        $"{name} must allow runtime understory batching.");
                }
            }
        }

        [Test]
        public void ImportedTreesSeparateVisualAndCollisionHelperMeshes()
        {
            string[] treeNames =
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
            for (int index = 0;
                 index < treeNames.Length;
                 index++)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Art/Environment/" +
                        "StylizedForest/Models/" +
                        "Stylized_forest_fbx/" +
                        treeNames[index] +
                        ".FBX");
                Assert.That(prefab, Is.Not.Null);
                Renderer[] renderers =
                    prefab.GetComponentsInChildren<
                        Renderer>(true);
                var materialNames =
                    new System.Collections.Generic.List<
                        string>();
                int collisionHelperCount = 0;
                for (int rendererIndex = 0;
                     rendererIndex <
                        renderers.Length;
                     rendererIndex++)
                {
                    if (renderers[rendererIndex].name
                        .StartsWith(
                            "UCX_",
                            System.StringComparison
                                .OrdinalIgnoreCase))
                    {
                        collisionHelperCount++;
                        continue;
                    }
                    Material[] materials =
                        renderers[rendererIndex]
                            .sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex <
                            materials.Length;
                         materialIndex++)
                    {
                        materialNames.Add(
                            (materials[materialIndex] != null
                                ? materials[materialIndex]
                                    .name
                                : "<null>"));
                    }
                }
                Assert.That(
                    collisionHelperCount,
                    Is.EqualTo(1),
                    $"{treeNames[index]} should retain one recognizable UCX collision helper.");
                Assert.That(
                    materialNames.Exists(
                        name =>
                            name.Contains("barck")),
                    Is.True);
                Assert.That(
                    materialNames.Exists(
                        name =>
                            name.Contains("foliage") ||
                            name.Contains("pine")),
                    Is.True);
            }
        }

        [Test]
        public void SameSeedProducesTheSameRoadAndRiver()
        {
            ProceduralRaidGenerator.RaidLayout first =
                ProceduralRaidGenerator.CreateLayout(
                    17381,
                    144f);
            ProceduralRaidGenerator.RaidLayout second =
                ProceduralRaidGenerator.CreateLayout(
                    17381,
                    144f);

            Assert.That(
                second.HasRoadFork,
                Is.EqualTo(first.HasRoadFork));
            Assert.That(
                second.RiverCrossesRoad,
                Is.EqualTo(first.RiverCrossesRoad));
            Assert.That(
                second.MainRoad,
                Is.EqualTo(first.MainRoad));
            Assert.That(
                second.ForkRoad,
                Is.EqualTo(first.ForkRoad));
            Assert.That(
                second.BranchRoadA,
                Is.EqualTo(first.BranchRoadA));
            Assert.That(
                second.BranchRoadB,
                Is.EqualTo(first.BranchRoadB));
            Assert.That(
                second.BranchRoadC,
                Is.EqualTo(first.BranchRoadC));
            Assert.That(
                second.BranchRoads,
                Is.EqualTo(first.BranchRoads));
            Assert.That(
                second.River,
                Is.EqualTo(first.River));
            Assert.That(
                second.PlayerStart,
                Is.EqualTo(first.PlayerStart));
            Assert.That(
                second.Extraction,
                Is.EqualTo(first.Extraction));
            Assert.That(
                second.CoastRadii,
                Is.EqualTo(first.CoastRadii));
        }

        [Test]
        public void TreeDensityBiomesPreservePlainMediumAndDenseBands()
        {
            Assert.That(
                ProceduralRaidGenerator
                    .CalculateTreeDensityMultiplier(-0.30f),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                ProceduralRaidGenerator
                    .CalculateTreeDensityMultiplier(0f),
                Is.EqualTo(
                    ProceduralRaidGenerator
                        .MediumWoodlandTreeDensity)
                    .Within(0.0001f));
            Assert.That(
                ProceduralRaidGenerator
                    .CalculateTreeDensityMultiplier(0.32f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                ProceduralRaidGenerator
                    .CalculateTreeDensityMultiplier(-0.17f),
                Is.InRange(
                    0.01f,
                    ProceduralRaidGenerator
                        .MediumWoodlandTreeDensity - 0.01f),
                "The plain edge should feather naturally rather than forming a hard tree wall.");
            Assert.That(
                ProceduralRaidGenerator
                    .CalculateTreeDensityMultiplier(0.19f),
                Is.InRange(
                    ProceduralRaidGenerator
                        .MediumWoodlandTreeDensity + 0.01f,
                    0.99f),
                "The medium woodland should feather into the unchanged dense forest.");
        }

        [Test]
        public void ProductionIslandCoastHasExactlyExpandedPlayableArea()
        {
            const float BaselineRadius = 144f;
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);

            float[] baselineCoast =
                ProceduralRaidGenerator.CreateIslandCoastRadii(
                    generator.Seed,
                    BaselineRadius);
            float[] productionCoast =
                ProceduralRaidGenerator.CreateIslandCoastRadii(
                    generator.Seed,
                    generator.MapRadius);
            float areaRatio = SampledCoastArea(productionCoast) /
                SampledCoastArea(baselineCoast);

            Assert.That(
                generator.MapRadius,
                Is.EqualTo(
                        BaselineRadius * Mathf.Sqrt(
                            ProceduralRaidGenerator
                                .ExpandedIslandAreaMultiplier))
                    .Within(0.001f),
                "The production radius must represent an area expansion, not a 2.5x radius expansion.");
            Assert.That(
                areaRatio,
                Is.EqualTo(
                        ProceduralRaidGenerator
                            .ExpandedIslandAreaMultiplier)
                    .Within(0.0001f),
                "The sampled production coastline must enclose exactly 2.5x the baseline playable area.");
        }

        [Test]
        public void PolylineSpatialQueryRemainsValidAfterRebuild()
        {
            System.Type queryType = typeof(ProceduralRaidGenerator)
                .GetNestedType(
                    "PolylineQuery",
                    BindingFlags.NonPublic);
            Assert.That(queryType, Is.Not.Null);
            object query = System.Activator.CreateInstance(queryType);
            MethodInfo add = queryType.GetMethod("Add");
            MethodInfo clear = queryType.GetMethod("Clear");
            MethodInfo tryClosest = queryType.GetMethod(
                "TryClosestPointWithin");
            Assert.That(add, Is.Not.Null);
            Assert.That(clear, Is.Not.Null);
            Assert.That(tryClosest, Is.Not.Null);

            var line = new List<Vector3>
            {
                new Vector3(-8f, 0f, 0f),
                new Vector3(8f, 0f, 0f)
            };
            add.Invoke(query, new object[] { line });
            object[] firstArguments =
            {
                new Vector2(0f, 0.5f),
                2f,
                Vector2.zero,
                0f
            };
            Assert.That(
                (bool)tryClosest.Invoke(query, firstArguments),
                Is.True);

            clear.Invoke(query, null);
            add.Invoke(query, new object[] { line });
            object[] rebuiltArguments =
            {
                new Vector2(0f, 0.5f),
                2f,
                Vector2.zero,
                0f
            };
            Assert.That(
                (bool)tryClosest.Invoke(query, rebuiltArguments),
                Is.True,
                "Rebuilding a spatial spline query must not reuse stale segment-visit stamps.");
            Assert.That(
                (float)rebuiltArguments[3],
                Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void RemoteRiverMouthStagesDryBankApproachBeforeBridgeCommitment()
        {
            const int ProductionSeed = 20260730;
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);
            generator.GenerateWithSeed(ProductionSeed);

            Vector3[] river = generator.CurrentLayout.River;
            int remoteIndex = 1;
            float farthestFromTrail = float.NegativeInfinity;
            for (int index = 1; index < river.Length - 1; index++)
            {
                float trailDistance = DistanceToTrailNetwork(
                    river[index],
                    generator.CurrentLayout);
                if (trailDistance <= farthestFromTrail)
                {
                    continue;
                }

                farthestFromTrail = trailDistance;
                remoteIndex = index;
            }

            Vector3 tangent = Vector3.ProjectOnPlane(
                river[remoteIndex + 1] - river[remoteIndex - 1],
                Vector3.up).normalized;
            Vector3 across = Vector3.Cross(
                Vector3.up,
                tangent).normalized;
            Vector3 from = river[remoteIndex] + across * 10f;
            Vector3 destination = river[remoteIndex] - across * 10f;
            Assert.That(
                farthestFromTrail,
                Is.GreaterThan(200f),
                "The regression probe must remain the remote meandering " +
                "river mouth rather than an ordinary bridge-adjacent bank.");
            Assert.That(
                generator.TryResolveEnemyBridgeRoute(
                    from,
                    destination,
                    out _,
                    out _),
                Is.False,
                "The strict bridge resolver must not pretend the concave " +
                "bank has one dry straight approach chord.");
            Assert.That(
                generator.TryResolveEnemyBridgeBankApproach(
                    from,
                    destination,
                    out ProceduralRaidGenerator
                        .EnemyBridgeApproachStep approach),
                Is.True,
                "A remote guard should receive a staged dry-bank approach " +
                "to a fitted bridge.");
            Assert.That(approach.CanCommitBridge, Is.False);
            Assert.That(
                generator.IsEnemyBridgeBankApproachDestinationCompatible(
                    approach,
                    destination),
                Is.True,
                "The staged route must use local river-bank topology rather " +
                "than the bridge center's half-plane.");
            Assert.That(
                generator.IsEnemyBridgeBankApproachDestinationCompatible(
                    approach,
                    from),
                Is.False,
                "Returning the strategic destination to the guard's entry " +
                "bank must cancel the unentered staged route.");

            Vector3 current = from;
            int legCount = 0;
            while (!approach.CanCommitBridge)
            {
                Assert.That(
                    legCount,
                    Is.LessThan(64),
                    "The staged bank route must converge instead of cycling.");
                Assert.That(
                    generator.IsEnemyBridgeApproachSegmentSafe(
                        current,
                        approach.Waypoint,
                        0.25f),
                    Is.True,
                    "Every stored bank-approach leg must stay dry, inside " +
                    "the coast, and away from un-authored cliff crossings.");
                Assert.That(
                    generator.IsEnemyNavigationPositionSafe(
                        approach.Waypoint,
                        0.25f),
                    Is.True,
                    "A bank waypoint must leave guard-radius clearance from " +
                    "the river exclusion.");
                Vector2 waypointPlanar = new Vector2(
                    approach.Waypoint.x,
                    approach.Waypoint.z);
                float coastRadius = generator.CurrentLayout
                    .CoastRadiusAtAngle(
                        Mathf.Atan2(
                            waypointPlanar.y,
                            waypointPlanar.x));
                Assert.That(
                    coastRadius - waypointPlanar.magnitude,
                    Is.GreaterThanOrEqualTo(0.85f),
                    "A bank waypoint must preserve its full coast inset.");

                Vector3 committedEntry = approach.BridgeEntry;
                Vector3 committedExit = approach.BridgeExit;
                int previousRemaining =
                    approach.RemainingRiverSamples;
                current = approach.Waypoint;
                Assert.That(
                    generator.TryAdvanceEnemyBridgeBankApproach(
                        current,
                        approach,
                        out ProceduralRaidGenerator
                            .EnemyBridgeApproachStep next),
                    Is.True,
                    "Reaching a stored bank waypoint must advance the same " +
                    "committed bridge plan.");
                Assert.That(
                    next.RemainingRiverSamples,
                    Is.LessThan(previousRemaining),
                    "River-sample progress must be strictly monotonic toward " +
                    "the committed bridge.");
                Assert.That(
                    Vector3.Distance(
                        next.BridgeEntry,
                        committedEntry),
                    Is.LessThan(0.05f));
                Assert.That(
                    Vector3.Distance(
                        next.BridgeExit,
                        committedExit),
                    Is.LessThan(0.05f));
                approach = next;
                legCount++;
            }

            Assert.That(
                legCount,
                Is.GreaterThan(1),
                "The production mouth needs a genuinely staged bank route, " +
                "not a relabeled one-leg bridge approach.");
            Assert.That(
                generator.TryResolveEnemyBridgeRoute(
                    current,
                    destination,
                    out Vector3 resolvedEntry,
                    out Vector3 resolvedExit),
                Is.True,
                "After the dry-bank stages, the existing strict bridge route " +
                "must take over.");
            Assert.That(
                Vector3.Distance(
                    resolvedEntry,
                    approach.BridgeEntry),
                Is.LessThan(0.05f));
            Assert.That(
                Vector3.Distance(
                    resolvedExit,
                    approach.BridgeExit),
                Is.LessThan(0.05f));
        }

        [Test]
        public void SeededIslandCoastsHaveSmoothBroadBaysAndHeadlands()
        {
            const float EqualAreaRadius = 144f;
            float expectedArea = Mathf.PI *
                EqualAreaRadius * EqualAreaRadius;
            float[] firstShape = null;
            for (int seed = 1; seed <= 24; seed++)
            {
                float[] coast =
                    ProceduralRaidGenerator.CreateIslandCoastRadii(
                        seed,
                        EqualAreaRadius);
                Assert.That(coast, Has.Length.EqualTo(256));
                float squaredTotal = 0f;
                float minimum = float.PositiveInfinity;
                float maximum = 0f;
                float maximumNeighborStep = 0f;
                for (int index = 0; index < coast.Length; index++)
                {
                    squaredTotal += coast[index] * coast[index];
                    minimum = Mathf.Min(minimum, coast[index]);
                    maximum = Mathf.Max(maximum, coast[index]);
                    maximumNeighborStep = Mathf.Max(
                        maximumNeighborStep,
                        Mathf.Abs(
                            coast[index] -
                            coast[(index + 1) % coast.Length]));
                }
                float sampledArea = Mathf.PI *
                    squaredTotal / coast.Length;
                float minimumBroadRadius = float.PositiveInfinity;
                float maximumBroadRadius = 0f;
                const int BroadHalfWindow = 8;
                for (int index = 0; index < coast.Length; index++)
                {
                    float broadTotal = 0f;
                    for (int offset = -BroadHalfWindow;
                         offset <= BroadHalfWindow;
                         offset++)
                    {
                        broadTotal += coast[
                            (index + offset + coast.Length) %
                            coast.Length];
                    }
                    float broadRadius = broadTotal /
                        (BroadHalfWindow * 2 + 1);
                    minimumBroadRadius = Mathf.Min(
                        minimumBroadRadius,
                        broadRadius);
                    maximumBroadRadius = Mathf.Max(
                        maximumBroadRadius,
                        broadRadius);
                }
                Assert.That(
                    sampledArea,
                    Is.EqualTo(expectedArea).Within(expectedArea * 0.001f));
                Assert.That(
                    maximum - minimum,
                    Is.GreaterThan(EqualAreaRadius * 0.20f));
                Assert.That(
                    maximumBroadRadius - minimumBroadRadius,
                    Is.GreaterThan(EqualAreaRadius * 0.22f),
                    $"Seed {seed} lacks a bay/headland variation that persists across a broad shoreline arc.");
                Assert.That(
                    maximumBroadRadius,
                    Is.GreaterThan(EqualAreaRadius * 1.08f),
                    $"Seed {seed} needs at least one broad headland outside the equal-area radius.");
                Assert.That(
                    minimumBroadRadius,
                    Is.LessThan(EqualAreaRadius * 0.92f),
                    $"Seed {seed} needs at least one broad bay inside the equal-area radius.");
                Assert.That(
                    maximumNeighborStep,
                    Is.LessThan(EqualAreaRadius * 0.035f),
                    $"Seed {seed} contains an abrupt artificial shoreline corner.");
                if (firstShape == null)
                {
                    firstShape = coast;
                }
                else if (seed == 2)
                {
                    Assert.That(coast, Is.Not.EqualTo(firstShape));
                }
            }
        }

        [Test]
        public void FireflyPocketsAreRareAndSeedDeterministic()
        {
            int selectedSeedCount = 0;
            for (int seed = 0; seed < 1000; seed++)
            {
                bool first =
                    ProceduralRaidGenerator.ShouldGenerateFireflies(seed);
                bool second =
                    ProceduralRaidGenerator.ShouldGenerateFireflies(seed);
                Assert.That(second, Is.EqualTo(first));
                if (first)
                {
                    selectedSeedCount++;
                }
            }

            Assert.That(
                selectedSeedCount,
                Is.InRange(100, 180),
                "Fireflies should be an uncommon map event, not a standard layer on every Raid.");
            Assert.That(
                ProceduralRaidGenerator.FireflyMapChance,
                Is.LessThan(0.2f));
        }

        [Test]
        public void SelectedSeedBuildsOneCompactFireflyPocket()
        {
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);
            Assert.That(
                ProceduralRaidGenerator.ShouldGenerateFireflies(0),
                Is.True);

            generator.GenerateWithSeed(0);

            Assert.That(
                generator.GeneratedFireflyZoneCount,
                Is.EqualTo(1));
            Assert.That(generator.FireflyZoneCenters, Has.Count.EqualTo(1));
            Transform pocket = generator.transform.Find(
                "Generated Raid 0/Rare Firefly Pocket");
            Assert.That(pocket, Is.Not.Null);
            ParticleSystem particles =
                pocket.GetComponent<ParticleSystem>();
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.main.maxParticles, Is.LessThanOrEqualTo(22));
            Assert.That(particles.shape.scale.x, Is.LessThanOrEqualTo(10.5f));
            Assert.That(pocket.GetComponent<Light>(), Is.Null);
            Assert.That(pocket.GetComponent<Collider>(), Is.Null);
            Vector2 center = generator.FireflyZoneCenters[0];
            Assert.That(
                generator.DistanceToNearestTrail(
                    new Vector3(center.x, 0f, center.y)),
                Is.GreaterThanOrEqualTo(7.5f));
        }

        [Test]
        public void MainRoadTouchesIslandCoastButSpawnUsesSafeOuterAnnulus()
        {
            const float Radius = 144f;
            ProceduralRaidGenerator.RaidLayout layout =
                ProceduralRaidGenerator.CreateLayout(
                    90210,
                    Radius);

            Assert.That(
                layout.MainRoad,
                Has.Length.GreaterThanOrEqualTo(12));
            Assert.That(
                CoastRatio(layout, layout.MainRoad[0]),
                Is.InRange(0.975f, 1f));
            Assert.That(
                CoastRatio(
                    layout,
                    layout.MainRoad[
                        layout.MainRoad.Length - 1]),
                Is.InRange(0.975f, 1f));
            Assert.That(
                CoastRatio(layout, layout.PlayerStart),
                Is.InRange(0.70f, 0.88f),
                "The player should spawn in a broad outer donut with enough terrain behind them to hide the disc edge.");
            Assert.That(
                CoastRatio(layout, layout.Extraction),
                Is.InRange(0.70f, 0.93f));
        }

        [Test]
        public void SpawnAndExtractionUseWholeOuterRingStayDryAndRemainOpposed()
        {
            const float Radius = 144f;
            var occupiedSectors = new bool[8];
            bool foundForestSpawn = false;
            bool foundForestExtraction = false;
            for (int seed = 1; seed <= 160; seed++)
            {
                ProceduralRaidGenerator.RaidLayout layout =
                    ProceduralRaidGenerator.CreateLayout(
                        seed,
                        Radius);
                float spawnAngle = Mathf.Atan2(
                    layout.PlayerStart.z,
                    layout.PlayerStart.x);
                int sector = Mathf.FloorToInt(
                    Mathf.Repeat(
                        spawnAngle,
                        Mathf.PI * 2f) /
                    (Mathf.PI * 0.25f));
                occupiedSectors[sector] = true;
                foundForestSpawn |=
                    DistanceToTrailNetwork(
                        layout.PlayerStart,
                        layout) > 8f;
                foundForestExtraction |=
                    DistanceToTrailNetwork(
                        layout.Extraction,
                        layout) > 8f;

                Assert.That(
                    DistanceToPolyline(
                        layout.PlayerStart,
                        layout.River),
                    Is.GreaterThanOrEqualTo(8f),
                    $"Seed {seed} placed the player in the river clearance.");
                Assert.That(
                    DistanceToPolyline(
                        layout.Extraction,
                        layout.River),
                    Is.GreaterThanOrEqualTo(8f),
                    $"Seed {seed} placed extraction in the river clearance.");
                Assert.That(
                    Vector3.Dot(
                        layout.PlayerStart.normalized,
                        layout.Extraction.normalized),
                    Is.LessThanOrEqualTo(-Mathf.Cos(0.70f) + 0.0001f),
                    $"Seed {seed} should put extraction toward the opposite side of the arena.");
            }

            Assert.That(
                occupiedSectors,
                Is.All.True,
                "Player spawns should cover all 360 degrees of the outer ring across seeds.");
            Assert.That(
                foundForestSpawn,
                Is.True,
                "The player must be able to spawn away from every trail.");
            Assert.That(
                foundForestExtraction,
                Is.True,
                "Extraction must be able to appear away from every trail.");
        }

        [Test]
        public void SeedsProduceTwoCompleteTrunksAndFourToSixPurposefulBranches()
        {
            bool foundFourBranches = false;
            bool foundSixBranches = false;
            for (int seed = 1; seed <= 40; seed++)
            {
                ProceduralRaidGenerator.RaidLayout layout =
                    ProceduralRaidGenerator.CreateLayout(
                        seed,
                        144f);
                Assert.That(
                    layout.ForkRoad,
                    Is.Not.Empty,
                    $"Seed {seed} needs a second coast-to-coast trunk.");
                Assert.That(layout.BranchRoads, Is.Not.Null);
                Assert.That(layout.BranchRoads.Length, Is.InRange(4, 6),
                    $"Seed {seed} should use four to six purposeful branches.");
                foundFourBranches |= layout.BranchRoads.Length == 4;
                foundSixBranches |= layout.BranchRoads.Length == 6;
                foreach (Vector3[] branch in layout.BranchRoads)
                {
                    Assert.That(
                        CoastRatio(
                            layout,
                            branch[branch.Length - 1]),
                        Is.GreaterThan(0.965f));
                }
                Assert.That(layout.RiverCrossesRoad, Is.True);
            }

            Assert.That(foundFourBranches, Is.True);
            Assert.That(foundSixBranches, Is.True);
        }

        [Test]
        public void PrimaryRiverCrossesTheExpandedMapWithVisibleMeanders()
        {
            ProceduralRaidGenerator.RaidLayout layout =
                ProceduralRaidGenerator.CreateLayout(
                    41721,
                    144f);
            float pathLength = 0f;
            for (int index = 1;
                 index < layout.River.Length;
                 index++)
            {
                pathLength += Vector3.Distance(
                    layout.River[index - 1],
                    layout.River[index]);
            }
            float directDistance = Vector3.Distance(
                layout.River[0],
                layout.River[layout.River.Length - 1]);

            Assert.That(layout.River, Has.Length.GreaterThanOrEqualTo(25));
            Assert.That(
                CoastRatio(layout, layout.River[0]),
                Is.GreaterThan(1f));
            Assert.That(
                CoastRatio(
                    layout,
                    layout.River[layout.River.Length - 1]),
                Is.GreaterThan(1f));
            Assert.That(
                pathLength,
                Is.GreaterThan(directDistance * 1.025f),
                "The primary river should visibly wind rather than read as a straight ribbon.");
        }

        [Test]
        public void TrailForksAndRiverCrossingsRemainIntentionalAcrossSeeds()
        {
            const float MinimumForkClearance = 11.5f;
            const float MinimumCrossingSeparation = 5f;
            for (int iteration = 1; iteration <= 242; iteration++)
            {
                int seed = iteration <= 240
                    ? iteration
                    : iteration == 241
                        ? 1242752318
                        : 586556700;
                ProceduralRaidGenerator.RaidLayout layout =
                    ProceduralRaidGenerator.CreateLayout(seed, 144f);
                var roads = new List<Vector3[]>
                {
                    layout.MainRoad,
                    layout.ForkRoad
                };
                roads.AddRange(layout.BranchRoads);
                Vector3[][] branches = layout.BranchRoads;
                var routeDestinations = new List<Vector3>();
                foreach (Vector3[] road in roads)
                {
                    if (road.Length == 0)
                    {
                        continue;
                    }
                    if (ReferenceEquals(road, layout.MainRoad) ||
                        ReferenceEquals(road, layout.ForkRoad))
                    {
                        routeDestinations.Add(road[0]);
                    }
                    routeDestinations.Add(road[road.Length - 1]);
                }
                for (int first = 0;
                     first < routeDestinations.Count;
                     first++)
                {
                    for (int second = first + 1;
                         second < routeDestinations.Count;
                         second++)
                    {
                        Assert.That(
                            Vector3.Distance(
                                routeDestinations[first],
                                routeDestinations[second]),
                            Is.GreaterThanOrEqualTo(10f),
                            $"Seed {seed} sends separate trails to effectively the same boundary destination.");
                        Assert.That(
                            BoundaryAngleBetween(
                                routeDestinations[first],
                                routeDestinations[second]),
                            Is.GreaterThanOrEqualTo(0.08f),
                            $"Seed {seed} sends separate trails into the same directional sector.");
                    }
                }

                if (layout.ForkRoad.Length > 0)
                {
                    var primaryJunctions =
                        new List<CrossingSample>();
                    FindCrossings(
                        layout.ForkRoad,
                        layout.MainRoad,
                        primaryJunctions);
                    Assert.That(
                        primaryJunctions,
                        Is.Not.Empty,
                        $"Seed {seed} generated a disconnected second primary trail instead of a navigable crossroads.");
                }

                foreach (Vector3[] branch in branches)
                {
                    if (branch.Length == 0)
                    {
                        continue;
                    }
                    Assert.That(
                        DistanceToPolyline(branch[0], layout.River),
                        Is.GreaterThanOrEqualTo(MinimumForkClearance),
                        $"Seed {seed} starts a fork beside the river instead of at a meaningful dry-land junction.");
                    float networkDistance = Mathf.Min(
                        DistanceToPolyline(
                            branch[0],
                            layout.MainRoad),
                        layout.ForkRoad.Length > 0
                            ? DistanceToPolyline(
                                branch[0],
                                layout.ForkRoad)
                            : float.PositiveInfinity);
                    Assert.That(
                        networkDistance,
                        Is.LessThanOrEqualTo(0.05f),
                        $"Seed {seed} generated a branch that is not connected to a primary route.");
                }

                var crossings = new List<CrossingSample>();
                foreach (Vector3[] road in roads)
                {
                    FindCrossings(
                        road,
                        layout.River,
                        crossings);
                }

                foreach (CrossingSample crossing in crossings)
                {
                    Assert.That(
                        Mathf.Abs(Vector3.Dot(
                            crossing.RoadDirection,
                            crossing.RiverDirection)),
                        Is.LessThanOrEqualTo(0.12f),
                        $"Seed {seed} contains a river crossing that is not perpendicular enough for a clean bridge approach.");
                }

                for (int first = 0; first < crossings.Count; first++)
                {
                    for (int second = first + 1;
                         second < crossings.Count;
                         second++)
                    {
                        Assert.That(
                            Vector3.Distance(
                                crossings[first].Point,
                                crossings[second].Point),
                            Is.GreaterThanOrEqualTo(
                                MinimumCrossingSeparation),
                            $"Seed {seed} creates neighboring bridges that do not represent distinct crossings.");
                    }
                }
            }
        }

        [Test]
        public void ProductionBridgesStayOutsideProtectedCoastBand()
        {
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);

            generator.Generate();

            Assert.That(
                generator.GeneratedBridgeCount,
                Is.GreaterThanOrEqualTo(1),
                "The production raid must retain at least one valid bridge.");
            Transform generated = generator.transform.Find(
                $"Generated Raid {generator.Seed}");
            Assert.That(generated, Is.Not.Null);
            Transform[] bridges = generated
                .Cast<Transform>()
                .Where(child => child.name.StartsWith("Road Bridge"))
                .ToArray();
            Assert.That(
                bridges,
                Has.Length.EqualTo(generator.GeneratedBridgeCount));
            foreach (Transform bridge in bridges)
            {
                Vector2 point = new Vector2(
                    bridge.position.x,
                    bridge.position.z);
                float coastRadius = generator.CurrentLayout
                    .CoastRadiusAtAngle(Mathf.Atan2(point.y, point.x));
                Assert.That(
                    coastRadius - point.magnitude,
                    Is.GreaterThanOrEqualTo(
                        ProceduralRaidGenerator
                            .MinimumBridgeCoastClearance),
                    $"{bridge.name} spawned inside the protected coast band.");
            }
        }

        [Test]
        public void RaidSceneGeneratesTraversableSurfacesForestAndPatrols()
        {
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);

            generator.Generate();

            Transform terrain =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Terrain Island");
            Transform road =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Main Dirt Road");
            Transform river =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/River");
            Transform shorelineBoundary =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/" +
                    "Island Shoreline Boundary");
            Transform ocean =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Endless Ocean");
            Transform coastalRockFaces =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/" +
                    "Coastal Rock Faces");
            Assert.That(terrain, Is.Not.Null);
            Assert.That(ocean, Is.Not.Null);
            Assert.That(
                coastalRockFaces,
                Is.Null,
                "Terrain elevation must not create a separate coastal " +
                "rock-face mesh.");
            Assert.That(
                ocean.GetComponent<MeshRenderer>(),
                Is.Not.Null);
            Material oceanMaterial =
                ocean.GetComponent<MeshRenderer>().sharedMaterial;
            Mesh oceanMesh =
                ocean.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(
                oceanMesh.vertexCount,
                Is.EqualTo(512),
                "The sea must be an exterior coast ring rather than a quad beneath the island.");
            float nearestOceanVertex = float.PositiveInfinity;
            foreach (Vector3 vertex in oceanMesh.vertices)
            {
                nearestOceanVertex = Mathf.Min(
                    nearestOceanVertex,
                    new Vector2(vertex.x, vertex.z).magnitude);
            }
            Assert.That(
                nearestOceanVertex,
                Is.GreaterThan(generator.MapRadius * 0.625f),
                "Ocean geometry must not exist beneath inland valleys or river channels.");
            Assert.That(
                oceanMaterial.name,
                Does.Contain("Deep Ocean"));
            Assert.That(
                oceanMaterial.shader.name,
                Is.EqualTo("WorldBuilder/Deep Ocean"),
                "The ocean must not reuse the river's directional-flow texture shader.");
            Assert.That(oceanMaterial.shader.isSupported, Is.True);
            Assert.That(
                Vector4.Distance(
                    oceanMaterial.GetColor("_DeepColor"),
                    new Color(
                        0.012f,
                        0.055f,
                        0.14f,
                        1f)),
                Is.LessThan(0.001f));
            Assert.That(
                Vector4.Distance(
                    oceanMaterial.GetColor("_CurrentColor"),
                    new Color(
                        0.025f,
                        0.14f,
                        0.28f,
                        1f)),
                Is.LessThan(0.001f));
            Assert.That(
                oceanMaterial.GetFloat("_WaveSpeed"),
                Is.EqualTo(0.035f).Within(0.001f),
                "Ocean motion should remain subtle rather than reading like a river current.");
            Mesh coastlineCheckedRiverMesh =
                river.GetComponent<MeshFilter>().sharedMesh;
            foreach (Vector3 vertex in coastlineCheckedRiverMesh.vertices)
            {
                float vertexRadius = new Vector2(
                    vertex.x,
                    vertex.z).magnitude;
                float coastRadius =
                    generator.CurrentLayout.CoastRadiusAtAngle(
                        Mathf.Atan2(vertex.z, vertex.x));
                Assert.That(
                    vertexRadius,
                    Is.LessThanOrEqualTo(coastRadius + 0.001f),
                    "The directional river surface must stop at the island coastline.");
            }
            Assert.That(shorelineBoundary, Is.Not.Null);
            Assert.That(
                shorelineBoundary.GetComponentsInChildren<MeshRenderer>(),
                Is.Empty,
                "The ocean blocker should be invisible; the old fog wall must not return.");
            Assert.That(
                shorelineBoundary.GetComponentsInChildren<BoxCollider>(),
                Has.Length.EqualTo(256),
                "The natural shoreline needs a continuous physical boundary.");
            Vector3[] generatedRiver =
                generator.CurrentLayout.River;
            float farthestRoutableFromTrail = float.NegativeInfinity;
            Vector3 bridgeWaypoint = Vector3.zero;
            Vector3 committedEntry = Vector3.zero;
            Vector3 committedExit = Vector3.zero;
            bool foundRoutableCrossing = false;
            for (int index = 1;
                 index < generatedRiver.Length - 1;
                 index++)
            {
                Vector3 riverTangent = Vector3.ProjectOnPlane(
                    generatedRiver[index + 1] -
                    generatedRiver[index - 1],
                    Vector3.up).normalized;
                Vector3 acrossRiver = Vector3.Cross(
                    Vector3.up,
                    riverTangent).normalized;
                Vector3 candidateFrom = generatedRiver[index] +
                    acrossRiver * 10f;
                Vector3 candidateDestination = generatedRiver[index] -
                    acrossRiver * 10f;
                if (!generator.TryResolveEnemyBridgeRoute(
                        candidateFrom,
                        candidateDestination,
                        out Vector3 candidateEntry,
                        out Vector3 candidateExit) ||
                    !generator.TryResolveEnemyRiverWaypoint(
                        candidateFrom,
                        candidateDestination,
                        out Vector3 candidateWaypoint))
                {
                    continue;
                }

                float trailDistance = DistanceToTrailNetwork(
                    generatedRiver[index],
                    generator.CurrentLayout);
                if (trailDistance <= farthestRoutableFromTrail)
                {
                    continue;
                }

                farthestRoutableFromTrail = trailDistance;
                bridgeWaypoint = candidateWaypoint;
                committedEntry = candidateEntry;
                committedExit = candidateExit;
                foundRoutableCrossing = true;
            }
            Assert.That(
                foundRoutableCrossing,
                Is.True,
                "At least one cross-river bank pair must redirect toward a " +
                "fitted bridge without asking the guard to cross open water " +
                "on its approach.");
            Assert.That(
                generator.IsInsideEnemyRiverExclusion(
                    bridgeWaypoint,
                    0.25f),
                Is.False,
                "The selected bridge approach waypoint must remain on dry ground.");
            Assert.That(
                Vector3.Distance(bridgeWaypoint, committedEntry),
                Is.LessThan(0.05f),
                "The ordinary steering waypoint must begin at the committed route entry.");
            Assert.That(
                Vector3.Distance(committedEntry, committedExit),
                Is.GreaterThan(8f),
                "A committed bridge route must span the river and both bank approaches.");
            Vector3 committedExitClearance =
                EnemyBrain.ResolveCommittedBridgeExitClearancePoint(
                    committedEntry,
                    committedExit);
            Assert.That(
                generator.IsEnemyNavigationPositionSafe(
                    committedExitClearance,
                    0.25f),
                Is.True,
                "A guard must finish its committed crossing on navigable " +
                "dry bank beyond the bridge endpoint.");
            Vector3 bridgeExitWaypoint = committedExit;
            Vector3 insideBridgeApproach = Vector3.MoveTowards(
                bridgeWaypoint,
                bridgeExitWaypoint,
                0.50f);
            insideBridgeApproach.y = bridgeWaypoint.y;
            Assert.That(
                generator.IsEnemyNavigationPositionSafe(
                    insideBridgeApproach,
                    0.25f),
                Is.True,
                "The dry portion of a bridge lane must not teleport a pursuing guard back from the bridge foot.");
            Assert.That(
                terrain.GetComponent<MeshCollider>(),
                Is.Not.Null);
            Assert.That(
                road,
                Is.Null,
                "The road must be integrated into the terrain mesh, not laid over it.");
            MeshFilter terrainFilter =
                terrain.GetComponent<MeshFilter>();
            MeshRenderer terrainRenderer =
                terrain.GetComponent<MeshRenderer>();
            Assert.That(
                terrainFilter.sharedMesh.subMeshCount,
                Is.EqualTo(2));
            Mesh terrainMesh =
                terrainFilter.sharedMesh;
            var habitatWeights = new List<Vector4>();
            terrainMesh.GetUVs(1, habitatWeights);
            Assert.That(
                habitatWeights,
                Has.Count.EqualTo(terrainMesh.vertexCount),
                "Every terrain vertex should carry the four primary habitat weights.");
            int restrainedForestVertices = 0;
            foreach (Vector4 weights in habitatWeights)
            {
                float stony = 1f - weights.x - weights.y -
                    weights.z - weights.w;
                Assert.That(stony, Is.GreaterThanOrEqualTo(-0.002f));
                Assert.That(
                    weights.x + weights.y + weights.z +
                    weights.w + stony,
                    Is.EqualTo(1f).Within(0.002f));
                Assert.That(
                    weights.w,
                    Is.LessThanOrEqualTo(0.001f),
                    "The greenest groundcover tier must have zero weight over the complete generated terrain.");
                if (weights.x + weights.y <= 0.001f &&
                    stony <= 0.001f &&
                    weights.z >= 0.999f)
                {
                    restrainedForestVertices++;
                }
            }
            Assert.That(
                restrainedForestVertices,
                Is.GreaterThan(terrainMesh.vertexCount * 0.90f),
                "The restrained green surface should be the sole ordinary forest ground; only camp clearings may blend toward loam.");
            Material terrainMaterial =
                terrainRenderer.sharedMaterials[0];
            Assert.That(
                terrainMaterial.shader.name,
                Is.EqualTo(
                    "WorldBuilder/Terrain Road Blend Lit"));
            Assert.That(
                terrainMaterial.GetTexture("_MossyLoamMap").name,
                Is.EqualTo("ForestMossyLoam_BaseColor_2048"));
            Assert.That(
                terrainMaterial.GetTexture("_CanopyDuffMap").name,
                Is.EqualTo("ForestCanopyDuff_BaseColor_2048"));
            Assert.That(
                terrainMaterial.GetTexture("_MossCarpetMap").name,
                Is.EqualTo("ForestMossCarpet_BaseColor_2048"),
                "The forest base should use the grass-dominant green source and restrain it through material saturation.");
            Assert.That(
                terrainMaterial.GetColor("_MossTint"),
                Is.EqualTo(Color.white),
                "The restored under-grass texture should not receive an additional green-biased tint.");
            Assert.That(
                terrainMaterial.GetTexture("_GroundcoverMap").name,
                Is.EqualTo("ForestMossCarpet_BaseColor_2048"),
                "The unused tier should share the same restrained source so it cannot introduce a different saturated surface.");
            Assert.That(
                terrainMaterial.GetColor("_GroundcoverTint"),
                Is.EqualTo(Color.white));
            Assert.That(
                terrainMaterial.GetFloat("_ForestGroundSaturation"),
                Is.EqualTo(
                    ProceduralRaidGenerator.DefaultForestGroundSaturation)
                    .Within(0.001f),
                "The preferred green coverage must be visibly desaturated rather than displayed at its original intensity.");
            Assert.That(
                terrainMaterial.GetTexture("_StonyLichenMap").name,
                Is.EqualTo("ForestStonyLichenSoil_BaseColor_2048"));
            var grassVertices =
                new System.Collections.Generic.HashSet<int>(
                    terrainMesh.GetTriangles(0));
            var dirtVertices =
                new System.Collections.Generic.HashSet<int>(
                    terrainMesh.GetTriangles(1));
            grassVertices.IntersectWith(
                dirtVertices);
            Vector3[] terrainVertices =
                terrainMesh.vertices;
            int gridWidth = 1;
            while (gridWidth < terrainVertices.Length &&
                Mathf.Approximately(
                    terrainVertices[gridWidth].z,
                    terrainVertices[0].z))
            {
                gridWidth++;
            }
            int originalGridVertexCount =
                gridWidth * gridWidth;
            float controllerGradeLimit =
                AssertRaisedLandformTraversal(generator);
            AssertBroadNavigableTerrainRegions(
                terrainVertices,
                gridWidth,
                generator,
                generator.CurrentLayout.River,
                controllerGradeLimit);
            int interpolatedBoundaryCount = 0;
            foreach (int vertexIndex in grassVertices)
            {
                if (vertexIndex >= originalGridVertexCount)
                {
                    interpolatedBoundaryCount++;
                }
            }
            Assert.That(
                grassVertices.Count,
                Is.GreaterThan(40),
                "The integrated dirt trail needs a continuous shared boundary.");
            Assert.That(
                interpolatedBoundaryCount,
                Is.GreaterThan(
                    grassVertices.Count * 0.75f),
                "The dirt edge must cut through terrain cells instead of following square grid steps.");
            Assert.That(
                terrainRenderer.sharedMaterials,
                Has.Length.EqualTo(2));
            Assert.That(
                terrainRenderer.sharedMaterials[0]
                    .mainTexture.name,
                Is.EqualTo(
                    "ForestMossyLoam_BaseColor_2048"));
            Assert.That(
                terrainRenderer.sharedMaterials[0]
                    .shader.name,
                Is.EqualTo(
                    "WorldBuilder/Terrain Road Blend Lit"));
            Assert.That(
                terrainRenderer.sharedMaterials[1],
                Is.SameAs(
                    terrainRenderer.sharedMaterials[0]),
                "Both terrain submeshes must share one blend shader so the old material seam cannot return.");
            Assert.That(
                terrainRenderer.sharedMaterials[0]
                    .GetTexture("_RoadMap").name,
                Is.EqualTo(
                    "RaidPath"));
            Color[] terrainColors =
                terrainMesh.colors;
            Assert.That(
                terrainColors,
                Has.Length.EqualTo(
                    terrainMesh.vertexCount));
            float minimumTerrainGreen = 1f;
            float maximumTerrainGreen = 0f;
            float minimumRoadBlend = 1f;
            float maximumRoadBlend = 0f;
            int transitionVertexCount = 0;
            foreach (Color color in terrainColors)
            {
                minimumTerrainGreen =
                    Mathf.Min(
                        minimumTerrainGreen,
                        color.g);
                maximumTerrainGreen =
                    Mathf.Max(
                        maximumTerrainGreen,
                        color.g);
                minimumRoadBlend =
                    Mathf.Min(
                        minimumRoadBlend,
                        color.a);
                maximumRoadBlend =
                    Mathf.Max(
                        maximumRoadBlend,
                        color.a);
                if (color.a > 0.08f &&
                    color.a < 0.92f)
                {
                    transitionVertexCount++;
                }
            }
            Assert.That(
                maximumTerrainGreen -
                    minimumTerrainGreen,
                Is.GreaterThan(0.08f),
                "The meadow terrain needs visible healthy-to-dry tint variation.");
            Assert.That(
                minimumRoadBlend,
                Is.LessThan(0.02f));
            Assert.That(
                maximumRoadBlend,
                Is.GreaterThan(0.98f));
            Assert.That(
                transitionVertexCount,
                Is.GreaterThan(80),
                "The trail edge needs a broad field of partially blended vertices rather than a binary cutoff.");
            Assert.That(river, Is.Not.Null);
            Assert.That(
                river.GetComponent<MeshRenderer>(),
                Is.Not.Null);
            Material riverMaterial =
                river.GetComponent<MeshRenderer>()
                    .sharedMaterial;
            Mesh riverMesh =
                river.GetComponent<MeshFilter>()
                    .sharedMesh;
            Assert.That(
                riverMesh.vertexCount,
                Is.GreaterThanOrEqualTo(600),
                "The water needs enough subdivisions for moving surface relief.");
            Assert.That(
                riverMesh.tangents,
                Has.Length.EqualTo(
                    riverMesh.vertexCount),
                "Animated water normals require a complete tangent basis.");
            Vector2[] riverFlowData =
                riverMesh.uv2;
            Assert.That(
                riverFlowData,
                Has.Length.EqualTo(
                    riverMesh.vertexCount),
                "The optimized river mesh should carry a precomputed spline flow field.");
            float maximumCurvature = 0f;
            float minimumCurrentSpeed =
                float.PositiveInfinity;
            float maximumCurrentSpeed = 0f;
            foreach (Vector2 flow in riverFlowData)
            {
                maximumCurvature =
                    Mathf.Max(
                        maximumCurvature,
                        Mathf.Abs(flow.x));
                minimumCurrentSpeed =
                    Mathf.Min(
                        minimumCurrentSpeed,
                        flow.y);
                maximumCurrentSpeed =
                    Mathf.Max(
                        maximumCurrentSpeed,
                        flow.y);
            }
            Assert.That(
                maximumCurvature,
                Is.GreaterThan(0.01f),
                "River bends should influence merging currents and whitewater.");
            Assert.That(
                maximumCurrentSpeed -
                    minimumCurrentSpeed,
                Is.LessThan(0.001f),
                "Every section should use one steady downstream rate without sludge-like speed changes.");
            Assert.That(
                Vector3.Distance(
                    riverMesh.vertices[0],
                    riverMesh.vertices[8]),
                Is.GreaterThan(8f),
                "The water surface should overlap beneath both banks so no riverbed gap is exposed.");
            Assert.That(riverMaterial, Is.Not.Null);
            Assert.That(
                riverMaterial.shader.name,
                Is.EqualTo(
                    "WorldBuilder/Stylized River Flow"));
            Assert.That(
                riverMaterial.shader.isSupported,
                Is.True);
            Assert.That(
                riverMaterial.mainTexture,
                Is.Not.Null);
            Assert.That(
                riverMaterial.mainTexture.name,
                Is.EqualTo("StylizedRiverFlow"));
            Assert.That(
                riverMaterial.GetFloat("_Opacity"),
                Is.EqualTo(0.99f).Within(0.001f));
            Assert.That(
                riverMaterial.GetFloat(
                    "_StreamSeparation"),
                Is.GreaterThanOrEqualTo(0.19f));
            Assert.That(
                riverMaterial.GetFloat(
                    "_BankEddyStrength"),
                Is.GreaterThanOrEqualTo(0.05f));
            Assert.That(
                riverMaterial.GetFloat("_WaveHeight"),
                Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(
                riverMaterial.GetFloat("_FoamStrength"),
                Is.GreaterThanOrEqualTo(0.95f),
                "The river should retain strong, readable whitewater accents.");
            Assert.That(
                riverMaterial.GetFloat("_FlowSpeed"),
                Is.EqualTo(0.22f).Within(0.001f),
                "The primary current should read as forceful rather than gently drifting.");
            Assert.That(
                riverMaterial.GetFloat(
                    "_NormalStrength"),
                Is.InRange(8f, 12f),
                "Water normals should retain depth without sharp lava-like popping.");
            Color foamColor =
                riverMaterial.GetColor("_FoamColor");
            Assert.That(
                foamColor.r,
                Is.GreaterThanOrEqualTo(0.94f),
                "Rushing whitewater should remain visibly bright against the blue current.");
            Assert.That(
                Mathf.Abs(
                    riverMaterial.GetFloat(
                        "_FlowDirection")),
                Is.EqualTo(1f).Within(0.001f),
                "Every generated river should choose one deterministic flow direction.");
            Assert.That(
                riverMaterial.renderQueue,
                Is.EqualTo(
                    (int)UnityEngine.Rendering
                        .RenderQueue.Transparent));
            Assert.That(
                generator.GeneratedTreeCount,
                Is.GreaterThanOrEqualTo(
                    generator.GeneratedTreeTarget * 0.95f),
                "Forest patches must redistribute the configured tree budget rather than deleting it.");
            Assert.That(
                generator.GeneratedTreeCount,
                Is.EqualTo(generator.GeneratedTreeTarget)
                    .Within(
                        Mathf.Max(
                            24,
                            Mathf.CeilToInt(
                                generator.GeneratedTreeTarget *
                                0.12f))),
                "The forest should fill its configured budget despite leaving intentional sparse and open patches.");
            Assert.That(
                generator.GeneratedTreeDensityCoverage,
                Is.InRange(0.60f, 0.65f),
                "Dense forest should cover roughly five eighths of the island, with the remainder reserved for sparse woodland and open pockets.");
            Assert.That(
                generator.GeneratedMediumWoodlandTreeCount,
                Is.GreaterThan(
                    generator.GeneratedTreeCount * 0.10f),
                "The medium-density woodland must contain a meaningful population rather than only a thin border.");
            Assert.That(
                generator.GeneratedDenseForestTreeCount,
                Is.GreaterThan(
                    generator.GeneratedTreeCount * 0.60f),
                "Dense forest must remain the dominant visual state across the map.");
            Transform forest =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Dense Stylized Forest");
            Assert.That(forest, Is.Not.Null);
            Vector2 upland = generator.UplandDirection.normalized;
            Vector2 lateral = new Vector2(-upland.y, upland.x);
            float positiveDensity = 0f;
            float negativeDensity = 0f;
            int halfSamples = 0;
            for (int alongIndex = 1; alongIndex <= 7; alongIndex++)
            {
                float along = alongIndex / 8f * generator.MapRadius * 0.72f;
                for (int lateralIndex = -5; lateralIndex <= 5; lateralIndex++)
                {
                    float across = lateralIndex / 5f *
                        generator.MapRadius * 0.48f;
                    positiveDensity += generator.TreeDensityMultiplierAt(
                        upland * along + lateral * across);
                    negativeDensity += generator.TreeDensityMultiplierAt(
                        -upland * along + lateral * across);
                    halfSamples++;
                }
            }
            Assert.That(
                Mathf.Abs(positiveDensity - negativeDensity) /
                    Mathf.Max(1, halfSamples),
                Is.LessThan(0.18f),
                "Neither the upland nor lowland half may become the preferred forest side.");
            for (int treeIndex = 0;
                 treeIndex < forest.childCount;
                 treeIndex++)
            {
                Vector3 treePosition =
                    forest.GetChild(treeIndex).position;
                Assert.That(
                    generator.TreeDensityBiomeAt(
                        new Vector2(
                            treePosition.x,
                            treePosition.z)),
                    Is.Not.EqualTo(
                        ProceduralRaidGenerator.TreeDensityBiome
                            .OpenPlain),
                    $"{forest.GetChild(treeIndex).name} should not intrude into the open plain.");
            }
            Assert.That(
                generator.TreeVariantCount,
                Is.EqualTo(11));
            Assert.That(
                generator.GrassVariantCount,
                Is.EqualTo(5));
            Assert.That(
                generator.UndergrowthVariantCount,
                Is.EqualTo(10));
            Assert.That(
                generator.GroundFloraStudyVariantCount,
                Is.EqualTo(GroundFloraStudyAssetBuilder.StudyCount));
            Assert.That(
                generator.RockVariantCount,
                Is.EqualTo(9));
            Assert.That(
                forest.childCount,
                Is.EqualTo(generator.GeneratedTreeCount));
            Transform camps = generator.transform.Find(
                $"Generated Raid {generator.Seed}/Forest Camps");
            Assert.That(camps, Is.Not.Null);
            Assert.That(
                generator.GeneratedCampCount,
                Is.InRange(
                    ProceduralRaidGenerator.MinimumCampCount,
                    ProceduralRaidGenerator.MaximumCampCount));
            Assert.That(
                camps.childCount,
                Is.EqualTo(generator.GeneratedCampCount));
            Assert.That(
                generator.GeneratedCampGuardCount,
                Is.InRange(
                    generator.GeneratedCampCount,
                    ProceduralRaidGenerator.MaximumCampGuardPoolSize));
            Assert.That(
                generator.GeneratedCampTentCount,
                Is.InRange(
                    generator.GeneratedCampCount * 2,
                    generator.GeneratedCampCount * 4),
                "Level One camps use two or three tents and Level Two camps use three or four.");
            Assert.That(
                generator.GeneratedCampBowGuardCount +
                    generator.GeneratedCampSwordGuardCount,
                Is.EqualTo(generator.GeneratedCampGuardCount));
            Assert.That(
                ProceduralRaidGenerator.LevelOneWoodenBoxChance,
                Is.EqualTo(0.5f).Within(0.0001f));
            int observedWoodenBoxCount = 0;
            for (int campIndex = 0;
                 campIndex < camps.childCount;
                 campIndex++)
            {
                Transform camp = camps.GetChild(campIndex);
                int chestCount = 0;
                int tentCount = 0;
                int benchCount = 0;
                int barrelCount = 0;
                int innerRackCount = 0;
                int outerDefenseCount = 0;
                int rackSwordCount = 0;
                int potCount = 0;
                Transform firewood = null;
                Transform cookingSpit = null;
                var chests = new List<Transform>();
                var tents = new List<Transform>();
                var benches = new List<Transform>();
                var woodenBoxes = new List<Transform>();
                for (int childIndex = 0;
                     childIndex < camp.childCount;
                     childIndex++)
                {
                    Transform child = camp.GetChild(childIndex);
                    if (child.name.StartsWith("Camp Chest"))
                    {
                        chestCount++;
                        chests.Add(child);
                    }
                    if (child.name.StartsWith("Guard Tent "))
                    {
                        tentCount++;
                        tents.Add(child);
                    }
                    if (child.name.StartsWith("Campfire Bench "))
                    {
                        benchCount++;
                        benches.Add(child);
                    }
                    if (child.name.StartsWith("Supply Barrel "))
                    {
                        barrelCount++;
                    }
                    if (child.name.StartsWith("Wooden Box "))
                    {
                        woodenBoxes.Add(child);
                    }
                    if (child.name.StartsWith("Inner Weapon Rack "))
                    {
                        innerRackCount++;
                    }
                    if (child.name.StartsWith("Outer Log Defense "))
                    {
                        outerDefenseCount++;
                    }
                    if (child.name.StartsWith("Rack Sword "))
                    {
                        rackSwordCount++;
                    }
                    if (child.name.StartsWith("Cooking Pot"))
                    {
                        potCount++;
                    }
                    if (child.name == "Firewood Pile")
                    {
                        firewood = child;
                    }
                    if (child.name == "Cooking Spit")
                    {
                        cookingSpit = child;
                    }
                }
                int campLevel = generator.CampLevel(campIndex);
                bool levelTwo = campLevel == 2;
                Assert.That(
                    camp.name,
                    Does.EndWith($"Level {campLevel}"));
                Assert.That(chestCount, Is.EqualTo(levelTwo ? 2 : 1));
                Assert.That(
                    tentCount,
                    Is.EqualTo(generator.CampTentCount(campIndex)));
                Assert.That(
                    tentCount,
                    levelTwo ? Is.InRange(3, 4) : Is.InRange(2, 3));
                Assert.That(benchCount, Is.EqualTo(levelTwo ? 2 : 0));
                Assert.That(barrelCount, Is.EqualTo(levelTwo ? 2 : 0));
                Assert.That(innerRackCount, Is.EqualTo(levelTwo ? 2 : 0));
                Assert.That(outerDefenseCount, Is.EqualTo(levelTwo ? 3 : 0));
                Assert.That(rackSwordCount, Is.EqualTo(levelTwo ? 2 : 0));
                Assert.That(potCount, Is.EqualTo(levelTwo ? 2 : 1));
                Assert.That(
                    woodenBoxes.Count,
                    levelTwo ? Is.InRange(2, 4) : Is.InRange(0, 1));
                Assert.That(
                    woodenBoxes.Count,
                    Is.EqualTo(
                        generator.CampWoodenBoxCount(campIndex)));
                observedWoodenBoxCount += woodenBoxes.Count;
                Assert.That(firewood, Is.Not.Null);
                Assert.That(cookingSpit, Is.Not.Null);
                Vector2 campCenter = generator.CampCenters[campIndex];
                Assert.That(
                    Vector2.Distance(
                        ToXZ(cookingSpit.position),
                        campCenter),
                    Is.LessThanOrEqualTo(1.8f),
                    "The cooking spit must remain beside or centered over the fire.");
                if (!levelTwo)
                {
                    Assert.That(
                        Vector2.Distance(ToXZ(firewood.position), campCenter),
                        Is.GreaterThanOrEqualTo(
                            generator.CampClearingRadius(campIndex) - 2.25f),
                        "Level One firewood belongs near the clearing's outer edge, not beside the fire.");
                    if (woodenBoxes.Count == 1)
                    {
                        float boxRadius = Vector2.Distance(
                            ToXZ(woodenBoxes[0].position),
                            campCenter);
                        Assert.That(
                            boxRadius,
                            Is.InRange(
                                generator.CampClearingRadius(campIndex) *
                                    0.64f,
                                generator.CampClearingRadius(campIndex) *
                                    0.84f),
                            "A Level One wooden box belongs in the clearing's middle outer ring.");
                    }
                }
                else
                {
                    float lowestBox = woodenBoxes.Min(
                        box => box.position.y);
                    float highestBox = woodenBoxes.Max(
                        box => box.position.y);
                    Assert.That(
                        highestBox - lowestBox,
                        Is.GreaterThan(0.5f),
                        "Every Level Two box group should include a visibly stacked box.");
                    int chestsInBoxCluster = chests.Count(chest =>
                        woodenBoxes.Any(box =>
                            Vector2.Distance(
                                ToXZ(chest.position),
                                ToXZ(box.position)) <= 1.8f));
                    Assert.That(
                        chestsInBoxCluster,
                        Is.EqualTo(1),
                        "Exactly one Level Two chest belongs on or beside the wooden-box cluster.");

                    for (int swordIndex = 1;
                         swordIndex <= 2;
                         swordIndex++)
                    {
                        Transform sword = camp.Find(
                            $"Rack Sword {swordIndex}");
                        Transform rack = camp.Find(
                            $"Inner Weapon Rack {swordIndex}");
                        Assert.That(sword, Is.Not.Null);
                        Assert.That(rack, Is.Not.Null);
                        Transform blade = sword.Find("Pointed Blade");
                        Mesh bladeMesh = blade
                            .GetComponent<MeshFilter>()
                            .sharedMesh;
                        Vector3 bladeTip = blade.TransformPoint(
                            new Vector3(
                                0f,
                                bladeMesh.bounds.max.y,
                                0f));
                        Assert.That(
                            bladeTip.y,
                            Is.EqualTo(
                                generator.SampleTerrainHeight(
                                    bladeTip.x,
                                    bladeTip.z) + 0.025f)
                                .Within(0.035f),
                            "A displayed camp sword should rest its blade point on the ground.");
                        Assert.That(
                            Vector2.Distance(
                                ToXZ(sword.position),
                                ToXZ(rack.position)),
                            Is.LessThan(0.8f),
                            "A displayed camp sword's hilt should lean against its weapon rack.");
                    }
                }
                foreach (Transform bench in benches)
                {
                    Renderer[] benchRenderers =
                        bench.GetComponentsInChildren<Renderer>(true);
                    Bounds benchBounds = benchRenderers[0].bounds;
                    for (int rendererIndex = 1;
                         rendererIndex < benchRenderers.Length;
                         rendererIndex++)
                    {
                        benchBounds.Encapsulate(
                            benchRenderers[rendererIndex].bounds);
                    }
                    Assert.That(
                        Mathf.Max(
                            benchBounds.size.x,
                            benchBounds.size.y,
                            benchBounds.size.z),
                        Is.EqualTo(1.7625f).Within(0.025f),
                        "Level Two benches should be 75 percent of their former 2.35-meter size.");
                    Vector3 towardFire = Vector3.ProjectOnPlane(
                        new Vector3(
                            campCenter.x,
                            bench.position.y,
                            campCenter.y) - bench.position,
                        Vector3.up).normalized;
                    Vector3 benchLengthAxis =
                        Vector3.ProjectOnPlane(
                            bench.right,
                            Vector3.up).normalized;
                    Assert.That(
                        Mathf.Abs(Vector3.Dot(
                            benchLengthAxis,
                            towardFire)),
                        Is.LessThan(0.12f),
                        "The long seat axis must run across the fire so the bench's broad sitting side, not its short end, faces the flames.");
                }
                Assert.That(
                    generator.CampClearingRadius(campIndex),
                    levelTwo
                        ? Is.GreaterThanOrEqualTo(17f)
                        : Is.LessThan(13f));
                foreach (Transform tent in tents)
                {
                    foreach (Transform chest in chests)
                    {
                        Assert.That(
                            Vector2.Distance(
                                ToXZ(chest.position),
                                ToXZ(tent.position)),
                            Is.GreaterThanOrEqualTo(3.44f),
                            "Camp chests should line up near the tent sector without entering a tent.");
                    }
                    Vector3 openingDirection =
                        Vector3.ProjectOnPlane(-tent.up, Vector3.up)
                            .normalized;
                    Vector3 towardFire =
                        Vector3.ProjectOnPlane(
                            new Vector3(
                                generator.CampCenters[campIndex].x,
                                tent.position.y,
                                generator.CampCenters[campIndex].y) -
                            tent.position,
                            Vector3.up).normalized;
                    Assert.That(
                        Vector3.Dot(openingDirection, towardFire),
                        Is.GreaterThan(0.92f),
                        "Each tent opening should face the central campfire.");
                }
                for (int firstTent = 0;
                     firstTent < tents.Count;
                     firstTent++)
                {
                    for (int secondTent = firstTent + 1;
                         secondTent < tents.Count;
                         secondTent++)
                    {
                        Assert.That(
                            Vector2.Distance(
                                ToXZ(tents[firstTent].position),
                                ToXZ(tents[secondTent].position)),
                            Is.GreaterThanOrEqualTo(4.8f),
                            "Camp tents should never overlap each other.");
                    }
                }
                ParticleSystem campfire =
                    camp.GetComponentInChildren<ParticleSystem>(true);
                Assert.That(campfire, Is.Not.Null,
                    "The central campfire should have animated flames.");
                Assert.That(
                    campfire.main.startSize.constantMax,
                    Is.InRange(0.20f, 0.23f),
                    "Campfire particles should remain small enough to read as flames, not pixels.");
                Light fireLight = campfire.GetComponent<Light>();
                Assert.That(fireLight, Is.Not.Null);
                Assert.That(fireLight.range, Is.LessThanOrEqualTo(4.5f));
            }
            Assert.That(
                observedWoodenBoxCount,
                Is.EqualTo(generator.GeneratedCampWoodenBoxCount));
            Vector3[] campTerrainVertices = terrainMesh.vertices;
            for (int campIndex = 0;
                 campIndex < generator.CampCenters.Count;
                 campIndex++)
            {
                Vector2 center = generator.CampCenters[campIndex];
                int nearestIndex = 0;
                float nearestDistanceSquared = float.MaxValue;
                for (int vertexIndex = 0;
                     vertexIndex < campTerrainVertices.Length;
                     vertexIndex++)
                {
                    Vector2 vertex = ToXZ(
                        campTerrainVertices[vertexIndex]);
                    float distanceSquared =
                        (vertex - center).sqrMagnitude;
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = distanceSquared;
                        nearestIndex = vertexIndex;
                    }
                }
                Assert.That(
                    habitatWeights[nearestIndex].x,
                    Is.GreaterThan(0.48f),
                    "Each camp should naturally prefer the lighter dead-grass loam without becoming a hard painted circle.");
                Assert.That(
                    habitatWeights[nearestIndex].y,
                    Is.LessThanOrEqualTo(0.001f));
            }
            foreach (Transform tree in forest)
            {
                for (int campIndex = 0;
                     campIndex < generator.CampCenters.Count;
                     campIndex++)
                {
                    Assert.That(
                        Vector2.Distance(
                            ToXZ(tree.position),
                            generator.CampCenters[campIndex]),
                        Is.GreaterThanOrEqualTo(
                            generator.CampClearingRadius(campIndex) -
                            0.01f),
                        $"{tree.name} entered a reserved camp clearing.");
                }
                Assert.That(
                    tree.GetComponentsInChildren<
                        CapsuleCollider>(true),
                    Is.Empty,
                    $"{tree.name} must not use a broad trunk capsule.");
                Assert.That(
                    tree.GetComponentsInChildren<
                        BoxCollider>(true),
                    Is.Empty,
                    $"{tree.name} must not use renderer-bounds boxes.");
                MeshCollider[] treeColliders =
                    tree.GetComponentsInChildren<
                        MeshCollider>(true);
                Assert.That(
                    treeColliders,
                    Is.Not.Empty,
                    $"{tree.name} needs exact wood collision.");
                foreach (MeshCollider collider in treeColliders)
                {
                    Assert.That(collider.sharedMesh, Is.Not.Null);
                    Assert.That(
                        collider.sharedMesh.name,
                        Does.Contain("Exact Wood Collision"),
                        "Only the visible bark trunk and branch triangles should stop arrows.");
                }
            }
            Transform grass =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Batched Meadow Grass");
            Transform undergrowth =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Shrubs Flowers and Ground Cover");
            Transform groundFlora =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Habitat Ground Flora Studies");
            Transform boulders =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Boulders");
            Transform trailStones =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Trail and Edge Stones");
            Assert.That(grass, Is.Not.Null);
            Assert.That(undergrowth, Is.Not.Null);
            Assert.That(groundFlora, Is.Not.Null);
            Assert.That(boulders, Is.Not.Null);
            Assert.That(trailStones, Is.Not.Null);
            Assert.That(
                grass.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Grass must never block arrows, actors, or AI sight.");
            Assert.That(
                undergrowth.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Shrubs, flowers, and ground cover must never block arrows or AI sight.");
            Assert.That(
                groundFlora.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Habitat ground flora must never block arrows, actors, or AI sight.");
            Assert.That(
                trailStones.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Small decorative trail stones should remain non-blocking.");
            foreach (Transform boulder in boulders)
            {
                Assert.That(
                    boulder.GetComponentsInChildren<BoxCollider>(true),
                    Is.Empty,
                    $"{boulder.name} must not retain an invisible bounds box.");
                MeshCollider[] rockColliders =
                    boulder.GetComponentsInChildren<MeshCollider>(true);
                Assert.That(
                    rockColliders,
                    Is.Not.Empty,
                    $"{boulder.name} needs visible-mesh collision.");
                foreach (MeshCollider collider in rockColliders)
                {
                    MeshFilter filter =
                        collider.GetComponent<MeshFilter>();
                    Assert.That(filter, Is.Not.Null);
                    Assert.That(
                        collider.sharedMesh,
                        Is.SameAs(filter.sharedMesh),
                        "Boulder collision must use the exact rendered mesh, not a proxy volume.");
                }
            }
            Assert.That(
                ProceduralRaidGenerator.GrassCoverageMultiplier,
                Is.EqualTo(2f));
            Assert.That(
                generator.GeneratedGrassCount,
                Is.GreaterThanOrEqualTo(240000));
            Assert.That(
                generator.GeneratedTrailTransitionGrassCount,
                Is.GreaterThan(80),
                "Sparse dirt-tinted grass should encroach onto the path margins.");
            int leafyGrassCount =
                generator.GeneratedGrassVariantCount(0);
            int spikyGrassCount =
                generator.GeneratedGrassCount -
                leafyGrassCount;
            Assert.That(
                spikyGrassCount,
                Is.GreaterThan(
                    generator.GeneratedGrassCount *
                    0.92f),
                "The four spiky grass meshes should form nearly all of the meadow layer.");
            Assert.That(
                leafyGrassCount,
                Is.GreaterThan(25),
                "The broad leafy grass should still appear as an occasional detail.");
            for (int variantIndex = 1;
                 variantIndex <
                    generator.GrassVariantCount;
                 variantIndex++)
            {
                Assert.That(
                    generator.GeneratedGrassVariantCount(
                        variantIndex),
                    Is.GreaterThan(3500),
                    $"Spiky grass variant {variantIndex + 1} should contribute substantial regional coverage.");
            }
            Assert.That(
                generator.GeneratedUndergrowthCount,
                Is.GreaterThanOrEqualTo(2650));
            TestContext.WriteLine(
                $"Ground flora: {generator.GeneratedGroundFloraStudyCount:N0} " +
                $"instances across {generator.GeneratedGroundFloraColonyCount:N0} " +
                $"general colonies, {generator.GeneratedGroundFloraTreePocketCount:N0} " +
                $"tree-pocket plants, and {generator.GeneratedGroundFloraBoulderPocketCount:N0} " +
                "boulder-pocket plants.");
            Assert.That(
                generator.GeneratedGroundFloraStudyCount,
                Is.GreaterThanOrEqualTo(4400),
                "The new gallery flora should form a substantial arena-wide ecology layer.");
            Assert.That(
                generator.GeneratedGroundFloraColonyCount,
                Is.GreaterThanOrEqualTo(80),
                "Most study flora should occur in readable same-species colonies.");
            Assert.That(
                generator.GeneratedGroundFloraTreePocketCount,
                Is.GreaterThanOrEqualTo(250),
                "Tree bases should gather compatible woodland flora.");
            Assert.That(
                generator.GeneratedGroundFloraBoulderPocketCount,
                Is.GreaterThanOrEqualTo(150),
                "Sheltered boulder edges should gather compatible flora.");
            Assert.That(
                generator.GeneratedBushGroupCount,
                Is.GreaterThanOrEqualTo(25),
                "Bushes should form repeated same-species shrub colonies.");
            Assert.That(
                generator.GeneratedBushClusterMemberCount /
                    (float)generator.GeneratedBushGroupCount,
                Is.GreaterThanOrEqualTo(6.5f),
                "Shrub colonies should be visibly larger than isolated prop groups.");
            Assert.That(
                generator.GeneratedFlowerPatchCount,
                Is.GreaterThanOrEqualTo(25),
                "Flowers should appear in natural colonies instead of isolated single props.");
            Assert.That(
                generator.GeneratedFlowerClusterMemberCount /
                    (float)generator.GeneratedFlowerPatchCount,
                Is.GreaterThanOrEqualTo(12f),
                "Flower colonies should read as broad patches with many matching plants.");
            Assert.That(
                generator.GeneratedGroundCoverPatchCount,
                Is.GreaterThanOrEqualTo(25),
                "Clover and low plants should form exaggerated ground-cover patches.");
            Assert.That(
                generator.GeneratedBoulderGrassCount,
                Is.GreaterThanOrEqualTo(180),
                "Large boulders should gather pockets of taller spiky grass around their sheltered sides.");
            Assert.That(
                generator.GeneratedTreeBaseGrassCount,
                Is.GreaterThanOrEqualTo(
                    Mathf.CeilToInt(
                        700f *
                        generator.GeneratedTreeDensityCoverage)),
                "Tall grass should emerge from the bases of many trees.");
            Assert.That(
                generator.GeneratedPlantEdgeGrassCount,
                Is.GreaterThanOrEqualTo(150),
                "Tall grass should blend into the edges of flower, clover, and shrub patches.");
            Assert.That(
                generator.GeneratedTreeBaseFoliageCount,
                Is.GreaterThanOrEqualTo(
                    Mathf.CeilToInt(
                        450f *
                        generator.GeneratedTreeDensityCoverage)),
                "Tree trunks should gather visible shrubs and ground cover at their bases.");
            Assert.That(
                generator.GeneratedBoulderBaseFoliageCount,
                Is.GreaterThanOrEqualTo(400),
                "Rock habitats should gather dense same-species shrubs, flowers, and ground cover.");
            Assert.That(
                generator.GeneratedBoulderCount,
                Is.GreaterThanOrEqualTo(175));
            Assert.That(
                generator.GeneratedTrailStoneCount,
                Is.GreaterThanOrEqualTo(150));
            int minimumLocalizedChunkCount =
                Mathf.FloorToInt(
                    120f *
                    ProceduralRaidGenerator
                        .ExpandedIslandAreaMultiplier);
            int maximumLocalizedChunkCount =
                Mathf.CeilToInt(
                    190f *
                    ProceduralRaidGenerator
                        .ExpandedIslandAreaMultiplier);
            Assert.That(
                grass.childCount,
                Is.InRange(
                    Mathf.FloorToInt(
                        130f *
                        ProceduralRaidGenerator
                            .ExpandedIslandAreaMultiplier),
                    maximumLocalizedChunkCount),
                "Meadow grass should retain the original world-space chunk density across the expanded island.");
            Assert.That(
                grass.GetComponentInChildren<MeshFilter>(),
                Is.Not.Null);
            Renderer[] grassRenderers =
                grass.GetComponentsInChildren<Renderer>();
            Assert.That(
                grassRenderers,
                Is.Not.Empty);
            Bounds grassBounds =
                grassRenderers[0].bounds;
            int grassVertexCount = 0;
            float minimumGrassHeight = float.PositiveInfinity;
            float maximumGrassHeight = float.NegativeInfinity;
            float minimumGrassGreen = 1f;
            float maximumGrassGreen = 0f;
            int sampledGrassVertices = 0;
            MeshCollider grassTerrainCollider =
                terrain.GetComponent<MeshCollider>();
            for (int grassIndex = 0;
                 grassIndex < grassRenderers.Length;
                 grassIndex++)
            {
                Assert.That(
                    grassRenderers[grassIndex].bounds.size.x,
                    Is.LessThanOrEqualTo(22f),
                    "A grass renderer must stay local enough for frustum and distance culling.");
                Assert.That(
                    grassRenderers[grassIndex].bounds.size.z,
                    Is.LessThanOrEqualTo(22f),
                    "A grass renderer must stay local enough for frustum and distance culling.");
                grassBounds.Encapsulate(
                    grassRenderers[grassIndex].bounds);
                MeshFilter filter =
                    grassRenderers[grassIndex]
                        .GetComponent<MeshFilter>();
                if (filter != null &&
                    filter.sharedMesh != null)
                {
                    grassVertexCount +=
                        filter.sharedMesh.vertexCount;
                    Assert.That(
                        filter.sharedMesh.colors,
                        Has.Length.EqualTo(
                            filter.sharedMesh.vertexCount));
                    foreach (Color color in
                        filter.sharedMesh.colors)
                    {
                        minimumGrassGreen =
                            Mathf.Min(
                                minimumGrassGreen,
                                color.g);
                        maximumGrassGreen =
                            Mathf.Max(
                                maximumGrassGreen,
                                color.g);
                    }
                    Vector3[] vertices =
                        filter.sharedMesh.vertices;
                    for (int vertexIndex = 0;
                         vertexIndex < vertices.Length;
                         vertexIndex += 97)
                    {
                        Vector3 vertex =
                            filter.transform.TransformPoint(
                                vertices[vertexIndex]);
                        if (!grassTerrainCollider.Raycast(
                                new Ray(
                                    vertex + Vector3.up * 10f,
                                    Vector3.down),
                                out RaycastHit hit,
                                20f))
                        {
                            continue;
                        }
                        float height = vertex.y - hit.point.y;
                        minimumGrassHeight =
                            Mathf.Min(
                                minimumGrassHeight,
                                height);
                        maximumGrassHeight =
                            Mathf.Max(
                                maximumGrassHeight,
                                height);
                        sampledGrassVertices++;
                    }
                }
            }
            TestContext.WriteLine(
                $"Grass bounds={grassBounds}; " +
                $"sampled={sampledGrassVertices}; " +
                $"terrain-relative height=" +
                $"{minimumGrassHeight:0.000}.." +
                $"{maximumGrassHeight:0.000}");
            Assert.That(
                maximumGrassHeight,
                Is.GreaterThan(0.3f),
                "Visible grass blades must extend above the terrain.");
            Assert.That(
                maximumGrassGreen -
                    minimumGrassGreen,
                Is.GreaterThan(0.08f),
                "Grass must share the meadow's healthy-to-dry tint variation.");
            Assert.That(
                grassBounds.size.x,
                Is.InRange(395f, 522f),
                $"Grass batches should span the seeded island, actual bounds were {grassBounds}.");
            Assert.That(
                grassBounds.size.z,
                Is.InRange(395f, 522f),
                $"Grass batches should span the seeded island, actual bounds were {grassBounds}.");
            Assert.That(
                grassVertexCount,
                Is.GreaterThan(150000),
                "The combined meadow must contain the authored grass geometry.");
            AssertUnityFoliageTexture(
                "Assets/_Project/Art/Prototype/Materials/StylizedForestGrassDetails.mat",
                "T_grass_BaseColor");
            Material matteGround =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/Materials/RaidGround.mat");
            Material matteRoad =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/Materials/RaidDirtRoad.mat");
            Assert.That(
                matteGround.HasProperty("_Smoothness"),
                Is.False,
                "The diffuse-only ground shader should expose no glossy response.");
            Assert.That(
                matteRoad.GetFloat("_Smoothness"),
                Is.LessThanOrEqualTo(0.01f));
            Assert.That(
                undergrowth.childCount,
                Is.InRange(
                    minimumLocalizedChunkCount,
                    maximumLocalizedChunkCount),
                "Thousands of foliage placements should retain localized render chunks across the expanded island.");
            Assert.That(
                undergrowth.GetComponentsInChildren<Renderer>().Length,
                Is.EqualTo(undergrowth.childCount));
            Assert.That(
                generator.GeneratedRendererCount,
                Is.LessThan(
                    Mathf.CeilToInt(
                        4500f *
                        ProceduralRaidGenerator
                            .ExpandedIslandAreaMultiplier)),
                "The generated Raid should remain within its renderer-object budget.");
            Assert.That(
                generator.GeneratedColliderCount,
                Is.LessThan(
                    Mathf.CeilToInt(
                        2200f *
                        ProceduralRaidGenerator
                            .ExpandedIslandAreaMultiplier)),
                "Only terrain, solid wood, rocks, water crossings, and gameplay surfaces should retain colliders.");
            RaidEnvironmentCuller environmentCuller =
                generator.GetComponent<RaidEnvironmentCuller>();
            Assert.That(environmentCuller, Is.Not.Null);
            Assert.That(
                environmentCuller.EntryCount,
                Is.EqualTo(
                    forest.childCount +
                    grass.childCount +
                    undergrowth.childCount +
                    boulders.childCount +
                    trailStones.childCount),
                "Every localized forest, grass, foliage, boulder, and trail-stone child should have exactly one culling entry regardless of island scale.");
            Assert.That(
                environmentCuller.EntryCount,
                Is.LessThan(
                    Mathf.CeilToInt(
                        2400f *
                        ProceduralRaidGenerator
                            .ExpandedIslandAreaMultiplier)),
                "The expanded environment should retain the original per-area culling-entry budget.");
            Assert.That(
                environmentCuller.RendererDistanceCullingEnabled,
                Is.False,
                "Distant trees must remain rendered so the arena never exposes an empty pop-in boundary.");
            Assert.That(
                Camera.main.farClipPlane,
                Is.GreaterThanOrEqualTo(generator.MapRadius * 2.25f));
            Assert.That(
                boulders.childCount,
                Is.EqualTo(generator.GeneratedBoulderCount));
            Assert.That(
                trailStones.childCount,
                Is.EqualTo(
                    generator.GeneratedTrailStoneCount));
            Assert.That(
                boulders.GetComponentInChildren<MeshCollider>(),
                Is.Not.Null,
                "Large boulders should block movement with exact mesh collision.");
            Assert.That(
                trailStones.GetComponentInChildren<Collider>(),
                Is.Null,
                "Small trail stones should not obstruct movement.");
            Renderer firstTreeRenderer =
                forest.GetComponentInChildren<Renderer>(true);
            Assert.That(firstTreeRenderer, Is.Not.Null);
            Assert.That(firstTreeRenderer.enabled, Is.True);
            Assert.That(firstTreeRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(
                firstTreeRenderer.sharedMaterial.shader,
                Is.Not.Null);
            Assert.That(
                firstTreeRenderer.sharedMaterial.shader.isSupported,
                Is.True);
            var generatorSerialized =
                new SerializedObject(generator);
            float treeScaleMultiplier =
                generatorSerialized.FindProperty(
                    "treeScaleMultiplier").floatValue;
            Assert.That(
                firstTreeRenderer.bounds.size.y,
                Is.InRange(
                    14f * treeScaleMultiplier,
                    21.5f * treeScaleMultiplier));
            Assert.That(
                firstTreeRenderer.bounds.size.y,
                Is.GreaterThan(
                    firstTreeRenderer.bounds.size.x * 0.85f));
            Assert.That(
                firstTreeRenderer.bounds.size.y,
                Is.GreaterThan(
                    firstTreeRenderer.bounds.size.z * 0.85f));
            var generatedVariants =
                new System.Collections.Generic.HashSet<string>();
            var generatedMaterials =
                new System.Collections.Generic.HashSet<string>();
            for (int index = 0;
                 index < forest.childCount;
                 index++)
            {
                Transform treeInstance =
                    forest.GetChild(index);
                string instanceName =
                    treeInstance.name;
                int suffixStart =
                    instanceName.LastIndexOf(' ');
                generatedVariants.Add(
                    suffixStart > 0
                        ? instanceName.Substring(
                            0,
                            suffixStart)
                        : instanceName);
                Renderer[] treeRenderers =
                    treeInstance.GetComponentsInChildren<
                        Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex <
                        treeRenderers.Length;
                     rendererIndex++)
                {
                    if (treeRenderers[rendererIndex]
                        .name.StartsWith(
                            "UCX_",
                            System.StringComparison
                                .OrdinalIgnoreCase))
                    {
                        Assert.That(
                            treeRenderers[rendererIndex]
                                .enabled,
                            Is.False,
                            "Imported UCX collision hulls must never render.");
                        continue;
                    }
                    Material[] materials =
                        treeRenderers[rendererIndex]
                            .sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex <
                            materials.Length;
                         materialIndex++)
                    {
                        Material material =
                            materials[materialIndex];
                        Assert.That(
                            material,
                            Is.Not.Null);
                        Assert.That(
                            material.mainTexture,
                            Is.Not.Null);
                        Assert.That(
                            material.enableInstancing,
                            Is.True);
                        generatedMaterials.Add(
                            material.name);
                    }
                }
            }
            Assert.That(
                generatedVariants,
                Has.Count.EqualTo(11),
                "Every supplied tree variant should appear in the generated forest.");
            Assert.That(
                generatedMaterials,
                Does.Contain(
                    "StylizedForestBark"));
            Assert.That(
                generatedMaterials,
                Does.Contain(
                    "StylizedForestBirchBark"));
            Assert.That(
                generatedMaterials,
                Does.Contain(
                    "StylizedForestLeaves"));
            Assert.That(
                generatedMaterials,
                Does.Contain(
                    "StylizedForestPineLeaves"));
            AssertUnityFoliageTexture(
                "Assets/_Project/Art/Prototype/Materials/StylizedForestLeaves.mat",
                "T_leaves_BaseColor_Unity");
            AssertUnityFoliageTexture(
                "Assets/_Project/Art/Prototype/Materials/StylizedForestPineLeaves.mat",
                "T_pine_leaves_BaseColor_Unity");
            Material pineMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/" +
                    "StylizedForestPineLeaves.mat");
            Color pineTint =
                pineMaterial.GetColor("_BaseColor");
            Assert.That(
                pineTint.b,
                Is.GreaterThan(pineTint.r));
            Assert.That(
                pineMaterial.shader.name,
                Is.EqualTo(
                    "WorldBuilder/Foliage Wind Lit"));
            Assert.That(
                pineMaterial.GetFloat("_WindStrength"),
                Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(
                pineMaterial.GetFloat("_RustleStrength"),
                Is.GreaterThanOrEqualTo(0.034f));
            Assert.That(
                RenderSettings.fog,
                Is.True,
                "The Raid should retain a lighter version of the original distance atmosphere.");
            GameObject groundFog = GameObject.Find("Layered Ground Fog");
            Assert.That(
                groundFog,
                Is.Not.Null,
                "The Raid should add low, terrain-hugging fog banks.");
            MeshFilter groundFogFilter =
                groundFog.GetComponent<MeshFilter>();
            MeshRenderer groundFogRenderer =
                groundFog.GetComponent<MeshRenderer>();
            int expectedGroundFogResolution =
                ProceduralRaidGenerator
                    .CalculateGroundFogGridResolution(
                        generator.MapRadius);
            Assert.That(
                expectedGroundFogResolution,
                Is.EqualTo(
                    Mathf.RoundToInt(
                        114f *
                        Mathf.Sqrt(
                            ProceduralRaidGenerator
                                .ExpandedIslandAreaMultiplier))),
                "The expanded island should keep the original ground-fog vertex spacing.");
            Assert.That(
                groundFogFilter.sharedMesh.vertexCount,
                Is.EqualTo(
                    (expectedGroundFogResolution + 1) *
                    (expectedGroundFogResolution + 1)),
                "Ground fog should preserve its terrain-following world-space sampling density as the island expands.");
            Assert.That(
                groundFogFilter.sharedMesh.subMeshCount,
                Is.EqualTo(1),
                "Ground fog must be one continuous surface, not visibly stacked planes.");
            Assert.That(
                groundFogRenderer.sharedMaterial.shader.name,
                Is.EqualTo("WorldBuilder/Low Horizon Fog"));
            Assert.That(
                Vector4.Distance(
                    groundFogRenderer.sharedMaterial.GetColor(
                        "_BaseColor"),
                    new Color(
                        0.22f,
                        0.36f,
                        0.34f,
                        1f)),
                Is.LessThan(0.001f),
                "The upgraded fog must preserve the established Raid color.");
            Assert.That(
                groundFogRenderer.sharedMaterial.GetFloat("_PatchCoverage"),
                Is.EqualTo(0.38f).Within(0.001f),
                "Hanging fog should occupy isolated banks rather than blanket the island.");
            Assert.That(
                groundFogRenderer.sharedMaterial.GetFloat("_PatchScale"),
                Is.EqualTo(0.025f).Within(0.001f));
            Assert.That(
                RenderSettings.skybox,
                Is.SameAs(generator.SkyboxMaterial));
            Assert.That(
                RenderSettings.skybox.shader.name,
                Is.EqualTo("Skybox/Panoramic"));
            Assert.That(
                Camera.main.clearFlags,
                Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(
                RenderSettings.fogMode,
                Is.EqualTo(FogMode.Linear));
            Assert.That(
                Vector4.Distance(
                    RenderSettings.fogColor,
                    new Color(
                        0.22f,
                        0.36f,
                        0.34f,
                        1f)),
                Is.LessThan(0.001f));
            Assert.That(
                RenderSettings.fogStartDistance,
                Is.EqualTo(28f));
            Assert.That(
                RenderSettings.fogEndDistance,
                Is.EqualTo(110f));
            Assert.That(
                RenderSettings.ambientIntensity,
                Is.EqualTo(1.02f));
            Assert.That(
                RenderSettings.ambientGroundColor.r,
                Is.EqualTo(0.11f).Within(0.001f));
            Light raidSun =
                GameObject.Find("Sun")
                    .GetComponent<Light>();
            Assert.That(raidSun, Is.Not.Null);
            Assert.That(
                raidSun.shadowStrength,
                Is.EqualTo(0.60f).Within(0.001f),
                "Tree shadows should retain shape without crushing the forest floor to black.");
            Assert.That(
                Vector3.Dot(
                    raidSun.transform.forward,
                    Vector3.down),
                Is.GreaterThan(0.85f),
                "The raid sun should sit high in the sky like mid-afternoon daylight.");
            MeshCollider terrainCollider =
                terrain.GetComponent<MeshCollider>();
            int groundedTreeChecks =
                Mathf.Min(
                    24,
                    forest.childCount);
            for (int index = 0;
                 index < groundedTreeChecks;
                 index++)
            {
                Transform tree = forest.GetChild(index);
                Bounds treeBounds =
                    VisibleRendererBounds(tree);
                float lowestTerrain =
                    MinimumTerrainHeight(
                        terrainCollider,
                        tree.position,
                        0.62f * treeScaleMultiplier,
                        0.62f * treeScaleMultiplier,
                        16);
                Assert.That(
                    treeBounds.size.y,
                    Is.InRange(
                        14.4f * treeScaleMultiplier - 0.1f,
                        21f * treeScaleMultiplier + 0.1f),
                    $"{tree.name} should use the requested 1.75x tree scale.");
                Assert.That(
                    treeBounds.min.y,
                    Is.EqualTo(lowestTerrain)
                        .Within(0.07f),
                    $"{tree.name} should be lowered only far enough to hide its trunk underside across the slope.");
            }

            int groundedBoulderChecks =
                Mathf.Min(
                    16,
                    boulders.childCount);
            for (int index = 0;
                 index < groundedBoulderChecks;
                 index++)
            {
                Transform boulder =
                    boulders.GetChild(index);
                Bounds boulderBounds =
                    VisibleRendererBounds(boulder);
                float lowestTerrain =
                    MinimumTerrainHeight(
                        terrainCollider,
                        boulder.position,
                        boulderBounds.extents.x * 0.72f,
                        boulderBounds.extents.z * 0.72f,
                        12);
                float embedDepth =
                    lowestTerrain -
                    boulderBounds.min.y;
                Assert.That(
                    embedDepth,
                    Is.InRange(-0.04f, 0.19f),
                    $"{boulder.name} should make stable contact without floating or being deeply buried.");
            }

            if (generator.CurrentLayout.RiverCrossesRoad)
            {
                Assert.That(
                    generator.GeneratedBridgeCount,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(
                    generator.BridgePrefab,
                    Is.Not.Null,
                    "River crossings should use the imported bridge asset.");
                Transform bridge =
                    generator.transform.Find(
                        $"Generated Raid {generator.Seed}/Road Bridge");
                Assert.That(
                    bridge,
                    Is.Not.Null);
                Vector2 bridgePlanar = new Vector2(
                    bridge.position.x,
                    bridge.position.z);
                float bridgeCoastRadius = generator.CurrentLayout
                    .CoastRadiusAtAngle(
                        Mathf.Atan2(
                            bridgePlanar.y,
                            bridgePlanar.x));
                Assert.That(
                    bridgeCoastRadius - bridgePlanar.magnitude,
                    Is.GreaterThanOrEqualTo(
                        ProceduralRaidGenerator
                            .MinimumBridgeCoastClearance),
                    "Bridges must remain outside the protected coast band.");
                Assert.That(
                    bridge.GetComponentsInChildren<MeshFilter>(true).Length,
                    Is.GreaterThan(0));
                Assert.That(
                    bridge.GetComponentsInChildren<MeshCollider>(true).Length,
                    Is.GreaterThan(0),
                    "The bridge needs walkable collision for players and AI.");
                Physics.SyncTransforms();
                RaycastHit[] bridgeHits = Physics.RaycastAll(
                    bridge.position + Vector3.up * 20f,
                    Vector3.down,
                    40f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                bool hasCenterSupport = false;
                float bridgeDeckHeight = float.NegativeInfinity;
                for (int hitIndex = 0;
                     hitIndex < bridgeHits.Length;
                     hitIndex++)
                {
                    if (bridgeHits[hitIndex].collider != null &&
                        bridgeHits[hitIndex].collider.transform
                            .IsChildOf(bridge))
                    {
                        hasCenterSupport = true;
                        bridgeDeckHeight = Mathf.Max(
                            bridgeDeckHeight,
                            bridgeHits[hitIndex].point.y);
                    }
                }
                Assert.That(
                    hasCenterSupport,
                    Is.True,
                    "The center of the trail must have bridge support rather than open water.");
                Vector3 supportedBridgePoint = new Vector3(
                    bridge.position.x,
                    bridgeDeckHeight + 0.05f,
                    bridge.position.z);
                Assert.That(
                    generator.IsEnemyNavigationPositionSafe(
                        supportedBridgePoint,
                        0.30f),
                    Is.True,
                    "The narrow center of a bridge must remain a valid AI route.");
                FieldInfo bridgeRoutesField =
                    typeof(ProceduralRaidGenerator).GetField(
                        "bridgeNavigationRoutes",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(bridgeRoutesField, Is.Not.Null);
                var bridgeRoutes = bridgeRoutesField.GetValue(generator)
                    as System.Collections.IList;
                Assert.That(bridgeRoutes, Is.Not.Null.And.Not.Empty);
                object firstRoute = bridgeRoutes[0];
                System.Type routeType = firstRoute.GetType();
                Vector2 routeCenter = (Vector2)routeType.GetField(
                    "Center").GetValue(firstRoute);
                Vector2 routeDirection = (Vector2)routeType.GetField(
                    "AcrossDirection").GetValue(firstRoute);
                float routeHalfWidth = (float)routeType.GetField(
                    "HalfWidth").GetValue(firstRoute);
                float routeDeckHeight = (float)routeType.GetField(
                    "ReferenceDeckHeight").GetValue(firstRoute);
                Vector2 routeLateral = new Vector2(
                    -routeDirection.y,
                    routeDirection.x).normalized;
                Vector2 supportedEdge = routeCenter +
                    routeLateral * (routeHalfWidth - 0.10f);
                Vector3 supportedEdgePoint = new Vector3(
                    supportedEdge.x,
                    routeDeckHeight + 0.05f,
                    supportedEdge.y);
                Assert.That(
                    generator.IsEnemyNavigationPositionSafe(
                        supportedEdgePoint,
                        0.30f),
                    Is.True,
                    "Real deck support near the bridge rail must override the river exclusion even outside the capsule-shrunken center strip.");
                Vector2 outsideBridgeLane = routeCenter +
                    routeLateral * (routeHalfWidth + 0.35f);
                Assert.That(
                    generator.IsEnemyNavigationPositionSafe(
                        new Vector3(
                            outsideBridgeLane.x,
                            routeDeckHeight + 0.05f,
                            outsideBridgeLane.y),
                        0.30f),
                    Is.False,
                    "Footprint support must not widen the authored bridge lane into open river.");
                Vector3 mountingBridgePoint = new Vector3(
                    routeCenter.x,
                    routeDeckHeight - 0.80f,
                    routeCenter.y);
                Assert.That(
                    generator.IsEnemyNavigationPositionSafe(
                        mountingBridgePoint,
                        0.30f),
                    Is.True,
                    "A guard mounting the deck must retain the authored bridge route even during the one-frame support-ray gap at the bank handoff.");
                Assert.That(
                    generator.IsEnemyNavigationPositionSafe(
                        supportedBridgePoint + Vector3.down * 2f,
                        0.30f),
                    Is.False,
                    "An enemy falling below the bridge deck must be treated as entering the river.");
                MeshFilter rootMesh =
                    bridge.GetComponent<MeshFilter>();
                if (rootMesh != null && rootMesh.sharedMesh != null)
                {
                    Assert.That(
                        rootMesh.sharedMesh.name,
                        Is.Not.EqualTo("Cube"),
                        "The old primitive cube bridge should be replaced.");
                }

                int generatedBridgeObjects = 0;
                Transform generatedRoot = bridge.parent;
                for (int childIndex = 0;
                     childIndex < generatedRoot.childCount;
                     childIndex++)
                {
                    if (generatedRoot.GetChild(childIndex).name
                        .StartsWith("Road Bridge"))
                    {
                        generatedBridgeObjects++;
                    }
                }
                Assert.That(
                    generatedBridgeObjects,
                    Is.EqualTo(generator.GeneratedBridgeCount),
                    "Every unique trail/river crossing should receive a bridge.");
            }

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            ExtractionZone extraction =
                Object.FindFirstObjectByType<ExtractionZone>();
            EnemyBrain[] allEnemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(player, Is.Not.Null);
            Material playerMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/Player.mat");
            Assert.That(playerMaterial, Is.Not.Null);
            Assert.That(
                playerMaterial.GetTexture("_BaseMap"),
                Is.Null);
            Assert.That(
                playerMaterial.GetTexture("_BumpMap"),
                Is.Null);
            Assert.That(
                playerMaterial.GetTexture("_OcclusionMap"),
                Is.Null);
            Assert.That(
                Vector4.Distance(
                    playerMaterial.GetColor("_BaseColor"),
                    new Color(0.36f, 0.36f, 0.36f, 1f)),
                Is.LessThan(0.001f));
            bool playerUsesMaterial = false;
            foreach (Renderer renderer in
                     player.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in
                         renderer.sharedMaterials)
                {
                    if (material == playerMaterial)
                    {
                        playerUsesMaterial = true;
                        break;
                    }
                }
            }
            Assert.That(
                playerUsesMaterial,
                Is.True,
                "The raid player should use the brighter player material.");
            Assert.That(extraction, Is.Not.Null);
            int trailRaiderCount = 0;
            int campGuardPoolCount = 0;
            var trailEnemies = new List<EnemyBrain>();
            foreach (EnemyBrain enemy in allEnemies)
            {
                if (enemy.name.StartsWith("Raider "))
                {
                    trailRaiderCount++;
                    trailEnemies.Add(enemy);
                }
                else if (enemy.name.StartsWith("Camp Guard Pool "))
                {
                    campGuardPoolCount++;
                }
            }
            Assert.That(trailRaiderCount, Is.EqualTo(8));
            Assert.That(
                campGuardPoolCount,
                Is.EqualTo(
                    ProceduralRaidGenerator.MaximumCampGuardPoolSize));
            EnemyBrain[] enemies = trailEnemies.ToArray();
            FieldInfo patrolRouteField = typeof(EnemyBrain).GetField(
                "patrolRoute",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(patrolRouteField, Is.Not.Null);
            var patrolRoutes = new List<Vector3[]>();
            foreach (EnemyBrain enemy in enemies)
            {
                Assert.That(
                    enemy.gameObject.activeSelf,
                    Is.True,
                    $"{enemy.name} should receive a stable trail patrol instead of remaining at its scene fallback position.");
                Vector3[] patrolRoute =
                    patrolRouteField.GetValue(enemy) as Vector3[];
                Assert.That(
                    patrolRoute,
                    Has.Length.EqualTo(3));
                patrolRoutes.Add(patrolRoute);
                for (int index = 1; index < patrolRoute.Length; index++)
                {
                    Assert.That(
                        Vector3.Distance(
                            patrolRoute[index - 1],
                            patrolRoute[index]),
                            Is.InRange(3.5f, 36f),
                        $"{enemy.name} needs meaningful, evenly spaced patrol legs rather than rapid short reversals.");
                }
                for (int index = 1; index < patrolRoute.Length - 1; index++)
                {
                    Vector3 before = Vector3.ProjectOnPlane(
                        patrolRoute[index] - patrolRoute[index - 1],
                        Vector3.up).normalized;
                    Vector3 after = Vector3.ProjectOnPlane(
                        patrolRoute[index + 1] - patrolRoute[index],
                        Vector3.up).normalized;
                    Assert.That(
                        Vector3.Dot(before, after),
                        Is.GreaterThan(0.85f),
                        $"{enemy.name} patrol route contains a hairpin that would look like broken back-and-forth movement.");
                }
            }
            for (int first = 0; first < patrolRoutes.Count; first++)
            {
                for (int second = first + 1;
                     second < patrolRoutes.Count;
                     second++)
                {
                    float routeDistance = float.PositiveInfinity;
                    foreach (Vector3 firstPoint in patrolRoutes[first])
                    {
                        foreach (Vector3 secondPoint in patrolRoutes[second])
                        {
                            routeDistance = Mathf.Min(
                                routeDistance,
                                Vector3.Distance(
                                    firstPoint,
                                    secondPoint));
                        }
                    }
                    Assert.That(
                        routeDistance < 3f || routeDistance >= 23.5f,
                        Is.True,
                        "Only paired guards may share a patrol corridor; separate groups need the original route separation.");
                }
            }
            RaidObelisk[] obelisks =
                Object.FindObjectsByType<RaidObelisk>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            System.Array.Sort(
                obelisks,
                (left, right) =>
                    left.QuadrantIndex.CompareTo(
                        right.QuadrantIndex));
            Assert.That(obelisks, Has.Length.EqualTo(4));
            float obeliskRadius =
                XzMagnitude(obelisks[0].transform.position);
            for (int index = 0; index < obelisks.Length; index++)
            {
                Vector3 position = obelisks[index].transform.position;
                Assert.That(
                    XzMagnitude(position),
                    Is.EqualTo(obeliskRadius).Within(0.01f),
                    "All four monuments should share one arena ring.");
                Assert.That(
                    DistanceToPolyline(
                        position,
                        generator.CurrentLayout.River),
                    Is.GreaterThanOrEqualTo(8f),
                    "An obelisk must never be placed in or against the river.");
                Assert.That(
                    grassTerrainCollider.Raycast(
                        new Ray(
                            position + Vector3.up * 20f,
                            Vector3.down),
                        out RaycastHit groundHit,
                        40f),
                    Is.True);
                Assert.That(
                    position.y,
                    Is.EqualTo(groundHit.point.y).Within(0.02f),
                    "The obelisk root must sit directly on terrain while its lower stone remains buried.");

                Vector3 next =
                    obelisks[(index + 1) % obelisks.Length]
                        .transform.position;
                Assert.That(
                    Vector3.Angle(
                        Vector3.ProjectOnPlane(position, Vector3.up),
                        Vector3.ProjectOnPlane(next, Vector3.up)),
                    Is.EqualTo(90f).Within(0.05f),
                    "Neighboring quadrant obelisks should be equidistant around the arena.");
            }
            Assert.That(obelisks[0].transform.position.x, Is.Positive);
            Assert.That(obelisks[0].transform.position.z, Is.Positive);
            Assert.That(obelisks[1].transform.position.x, Is.Negative);
            Assert.That(obelisks[1].transform.position.z, Is.Positive);
            Assert.That(obelisks[2].transform.position.x, Is.Negative);
            Assert.That(obelisks[2].transform.position.z, Is.Negative);
            Assert.That(obelisks[3].transform.position.x, Is.Positive);
            Assert.That(obelisks[3].transform.position.z, Is.Negative);
            foreach (Transform tree in forest)
            {
                foreach (RaidObelisk obelisk in obelisks)
                {
                    Assert.That(
                        Vector2.Distance(
                            ToXZ(tree.position),
                            ToXZ(obelisk.transform.position)),
                        Is.GreaterThanOrEqualTo(
                            ProceduralRaidGenerator
                                .ObeliskTreeClearance - 0.01f),
                        $"{tree.name} entered the six-meter tree-free obelisk clearing.");
                }
            }
            foreach (EnemyBrain enemy in allEnemies)
            {
                EnemyDamageProfile damageProfile =
                    enemy.GetComponent<EnemyDamageProfile>();
                Assert.That(damageProfile, Is.Not.Null);
                Assert.That(
                    damageProfile.Variant,
                    Is.EqualTo(EnemyCombatVariant.RaidEnemy));
                Assert.That(
                    enemy.GetComponent<Health>().Minimum,
                    Is.Zero,
                    "Raid enemies must never retain the legacy immortal dummy floor.");
                if (enemy.gameObject.activeInHierarchy)
                {
                    Assert.That(
                        generator.IsInsideEnemyRiverExclusion(
                            enemy.transform.position,
                            0.25f),
                        Is.False,
                        $"{enemy.name} spawned inside the river exclusion.");
                }
            }
            EnemyBrain safetyEnemy = enemies[0];
            CharacterController safetyController =
                safetyEnemy.GetComponent<CharacterController>();
            Vector3 lastDryPosition = safetyEnemy.transform.position;
            MethodInfo navigationSafetyUpdate =
                typeof(EnemyBrain).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(navigationSafetyUpdate, Is.Not.Null);
            navigationSafetyUpdate.Invoke(safetyEnemy, null);
            Vector3 forcedRiverPosition =
                generator.CurrentLayout.River[
                    generator.CurrentLayout.River.Length / 2];
            forcedRiverPosition.y = lastDryPosition.y - 3f;
            bool safetyControllerWasEnabled =
                safetyController.enabled;
            safetyController.enabled = false;
            safetyEnemy.transform.position = forcedRiverPosition;
            safetyController.enabled = safetyControllerWasEnabled;

            navigationSafetyUpdate.Invoke(safetyEnemy, null);

            Assert.That(
                safetyEnemy.transform.position,
                Is.EqualTo(lastDryPosition),
                "A living enemy displaced into the river must immediately return to its last dry navigation position.");
            Assert.That(
                CoastRatio(
                    generator.CurrentLayout,
                    player.transform.position),
                Is.InRange(0.70f, 0.88f));
            Assert.That(
                CoastRatio(
                    generator.CurrentLayout,
                    extraction.transform.position),
                Is.InRange(0.70f, 0.93f));
            foreach (Transform boulder in boulders)
            {
                Assert.That(
                    Vector2.Distance(
                        ToXZ(boulder.position),
                        ToXZ(player.transform.position)),
                    Is.GreaterThanOrEqualTo(7.5f),
                    $"{boulder.name} must not occupy the player's reserved solid-ground spawn area.");
                Assert.That(
                    Vector2.Distance(
                        ToXZ(boulder.position),
                        ToXZ(extraction.transform.position)),
                    Is.GreaterThanOrEqualTo(7.5f),
                    $"{boulder.name} must not occupy extraction's reserved solid-ground area.");
            }
            foreach (Transform stone in trailStones)
            {
                Assert.That(
                    Vector2.Distance(
                        ToXZ(stone.position),
                        ToXZ(player.transform.position)),
                    Is.GreaterThanOrEqualTo(7.5f),
                    $"{stone.name} must not be generated beneath the player.");
                Assert.That(
                    Vector2.Distance(
                        ToXZ(stone.position),
                        ToXZ(extraction.transform.position)),
                    Is.GreaterThanOrEqualTo(7.5f),
                    $"{stone.name} must not be generated inside extraction.");
            }
            Assert.That(
                generator.GeneratedGuardGroupCount,
                Is.InRange(4, 7));
            Assert.That(
                generator.GeneratedGuardPairCount,
                Is.GreaterThanOrEqualTo(1));
            for (int first = 0; first < enemies.Length; first++)
            {
                int nearbyPartners = 0;
                for (int second = 0; second < enemies.Length; second++)
                {
                    if (first == second)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(
                        enemies[first].transform.position,
                        enemies[second].transform.position);
                    if (distance < 3f)
                    {
                        nearbyPartners++;
                    }
                    else
                    {
                        Assert.That(
                            distance,
                            Is.GreaterThanOrEqualTo(
                                ProceduralRaidGenerator.
                                    MinimumGuardPatrolSeparation - 2f),
                            "Separate guard groups must not spawn as a larger cluster.");
                    }
                }
                Assert.That(
                    nearbyPartners,
                    Is.LessThanOrEqualTo(1),
                    "A guard group may contain at most two archers.");
            }
            Assert.That(
                enemies,
                Has.All.Matches<EnemyBrain>(
                    enemy =>
                        generator.DistanceToNearestTrail(
                            enemy.transform.position) < 1.1f),
                "Every guard should spawn directly on one of the generated trails.");
            float nearestGuardDistance = float.PositiveInfinity;
            foreach (EnemyBrain enemy in enemies)
            {
                nearestGuardDistance = Mathf.Min(
                    nearestGuardDistance,
                    Vector3.Distance(
                        enemy.transform.position,
                        player.transform.position));
            }
            Assert.That(
                nearestGuardDistance,
                Is.GreaterThanOrEqualTo(30f),
                "No trail guard should spawn inside the player's protected entry area.");
        }

        private static void AssertUnityFoliageTexture(
            string materialPath,
            string expectedTextureName)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.mainTexture, Is.Not.Null);
            Assert.That(
                material.mainTexture.name,
                Is.EqualTo(expectedTextureName));
            string texturePath =
                AssetDatabase.GetAssetPath(
                    material.mainTexture);
            TextureImporter importer =
                AssetImporter.GetAtPath(
                    texturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(
                importer.DoesSourceTextureHaveAlpha(),
                Is.True);
            Assert.That(
                importer.alphaSource,
                Is.EqualTo(
                    TextureImporterAlphaSource.FromInput));
            Assert.That(
                importer.mipMapsPreserveCoverage,
                Is.True);
        }

        private static Bounds VisibleRendererBounds(
            Transform root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            bool foundRenderer = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (!foundRenderer)
                {
                    bounds = renderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Assert.That(
                foundRenderer,
                Is.True,
                $"{root.name} should contain visible renderers.");
            return bounds;
        }

        private static float XzMagnitude(Vector3 point)
        {
            return new Vector2(point.x, point.z).magnitude;
        }

        private static float CoastRatio(
            ProceduralRaidGenerator.RaidLayout layout,
            Vector3 point)
        {
            float angle = Mathf.Atan2(point.z, point.x);
            return XzMagnitude(point) /
                Mathf.Max(
                    0.001f,
                    layout.CoastRadiusAtAngle(angle));
        }

        private readonly struct CrossingSample
        {
            public CrossingSample(
                Vector3 point,
                Vector3 roadDirection,
                Vector3 riverDirection)
            {
                Point = point;
                RoadDirection = roadDirection;
                RiverDirection = riverDirection;
            }

            public Vector3 Point { get; }
            public Vector3 RoadDirection { get; }
            public Vector3 RiverDirection { get; }
        }

        private static void FindCrossings(
            Vector3[] road,
            Vector3[] river,
            List<CrossingSample> results)
        {
            if (road == null || road.Length < 2)
            {
                return;
            }
            for (int roadIndex = 0; roadIndex < road.Length - 1; roadIndex++)
            {
                Vector2 roadStart = ToXZ(road[roadIndex]);
                Vector2 roadDelta =
                    ToXZ(road[roadIndex + 1]) - roadStart;
                for (int riverIndex = 0;
                     riverIndex < river.Length - 1;
                     riverIndex++)
                {
                    Vector2 riverStart = ToXZ(river[riverIndex]);
                    Vector2 riverDelta =
                        ToXZ(river[riverIndex + 1]) - riverStart;
                    float denominator = Cross2D(roadDelta, riverDelta);
                    if (Mathf.Abs(denominator) <= 0.00001f)
                    {
                        continue;
                    }
                    Vector2 separation = riverStart - roadStart;
                    float roadT = Cross2D(separation, riverDelta) / denominator;
                    float riverT = Cross2D(separation, roadDelta) / denominator;
                    if (roadT < 0f || roadT > 1f ||
                        riverT < 0f || riverT > 1f)
                    {
                        continue;
                    }
                    Vector2 point = roadStart + roadDelta * roadT;
                    results.Add(
                        new CrossingSample(
                            new Vector3(point.x, 0f, point.y),
                            new Vector3(
                                roadDelta.x,
                                0f,
                                roadDelta.y).normalized,
                            new Vector3(
                                riverDelta.x,
                                0f,
                                riverDelta.y).normalized));
                }
            }
        }

        private static float DistanceToPolyline(
            Vector3 point,
            Vector3[] line)
        {
            float result = float.PositiveInfinity;
            Vector2 point2 = ToXZ(point);
            for (int index = 0; index < line.Length - 1; index++)
            {
                Vector2 start = ToXZ(line[index]);
                Vector2 segment = ToXZ(line[index + 1]) - start;
                float progress = segment.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point2 - start, segment) /
                        segment.sqrMagnitude)
                    : 0f;
                result = Mathf.Min(
                    result,
                    Vector2.Distance(
                        point2,
                        start + segment * progress));
            }
            return result;
        }

        private static float DistanceToTrailNetwork(
            Vector3 point,
            ProceduralRaidGenerator.RaidLayout layout)
        {
            float result = float.PositiveInfinity;
            var trails = new List<Vector3[]>
            {
                layout.MainRoad,
                layout.ForkRoad
            };
            trails.AddRange(layout.BranchRoads);
            foreach (Vector3[] trail in trails)
            {
                if (trail == null || trail.Length < 2)
                {
                    continue;
                }
                result = Mathf.Min(
                    result,
                    DistanceToPolyline(point, trail));
            }
            return result;
        }

        private static void AssertPurposefulBranch(
            int seed,
            Vector3[] branch,
            Vector3[] sourceRoad,
            Vector3[][] existingRoads)
        {
            if (branch.Length == 0)
            {
                return;
            }

            Vector3 sourceDirection = ClosestPolylineDirection(
                branch[0],
                sourceRoad,
                out _);
            Vector3 departure = Vector3.ProjectOnPlane(
                branch[Mathf.Min(3, branch.Length - 1)] - branch[0],
                Vector3.up).normalized;
            float departureAngle = Mathf.Acos(Mathf.Clamp(
                Mathf.Abs(Vector3.Dot(
                    departure,
                    sourceDirection)),
                -1f,
                1f));
            Assert.That(
                departureAngle,
                Is.GreaterThanOrEqualTo(0.20f),
                $"Seed {seed} creates a branch that merely continues parallel to its parent trail.");

            foreach (Vector3[] existing in existingRoads)
            {
                if (existing == null || existing.Length < 2)
                {
                    continue;
                }
                int consecutiveParallelSamples = 0;
                for (int index = 2;
                     index < branch.Length - 1;
                     index++)
                {
                    Vector3 branchDirection = Vector3.ProjectOnPlane(
                        branch[index + 1] - branch[index - 1],
                        Vector3.up).normalized;
                    Vector3 existingDirection = ClosestPolylineDirection(
                        branch[index],
                        existing,
                        out float distance);
                    bool followsSameCorridor =
                        distance < 14.9f &&
                        Mathf.Abs(Vector3.Dot(
                            branchDirection,
                            existingDirection)) > 0.88f;
                    consecutiveParallelSamples = followsSameCorridor
                        ? consecutiveParallelSamples + 1
                        : 0;
                    Assert.That(
                        consecutiveParallelSamples,
                        Is.LessThan(5),
                        $"Seed {seed} creates redundant parallel trails in the same corridor.");
                }
            }
        }

        private static Vector3 ClosestPolylineDirection(
            Vector3 point,
            Vector3[] line,
            out float closestDistance)
        {
            closestDistance = float.PositiveInfinity;
            Vector3 closestDirection = Vector3.forward;
            Vector2 point2 = ToXZ(point);
            for (int index = 0; index < line.Length - 1; index++)
            {
                Vector2 start = ToXZ(line[index]);
                Vector2 segment = ToXZ(line[index + 1]) - start;
                float progress = segment.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point2 - start, segment) /
                        segment.sqrMagnitude)
                    : 0f;
                float distance = Vector2.Distance(
                    point2,
                    start + segment * progress);
                if (distance >= closestDistance)
                {
                    continue;
                }
                closestDistance = distance;
                closestDirection = new Vector3(
                    segment.x,
                    0f,
                    segment.y).normalized;
            }
            return closestDirection;
        }

        private static float BoundaryAngleBetween(
            Vector3 first,
            Vector3 second)
        {
            float firstAngle = Mathf.Atan2(first.x, first.z);
            float secondAngle = Mathf.Atan2(second.x, second.z);
            return Mathf.Abs(Mathf.DeltaAngle(
                firstAngle * Mathf.Rad2Deg,
                secondAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
        }

        private static Vector2 ToXZ(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }

        private static float Cross2D(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private static float SampledCoastArea(float[] coastRadii)
        {
            double squaredTotal = 0d;
            for (int index = 0; index < coastRadii.Length; index++)
            {
                squaredTotal += coastRadii[index] *
                    coastRadii[index];
            }
            return Mathf.PI * (float)(squaredTotal /
                coastRadii.Length);
        }

        private static float AssertRaisedLandformTraversal(
            ProceduralRaidGenerator generator)
        {
            IReadOnlyList<Vector2> passCenters =
                generator.EscarpmentPassCenters;
            Assert.That(
                passCenters,
                Is.Not.Null.And.Not.Empty,
                "The production trail network must author at least one navigable escarpment pass.");

            Vector2 uplandDirection =
                generator.UplandDirection.normalized;
            Assert.That(
                uplandDirection.sqrMagnitude,
                Is.GreaterThan(0.99f));
            float escarpmentProjection = 0f;
            for (int index = 0; index < passCenters.Count; index++)
            {
                escarpmentProjection += Vector2.Dot(
                    passCenters[index],
                    uplandDirection);
            }
            escarpmentProjection /= passCenters.Count;

            const float StableSideDistance = 5f;
            ProceduralRaidGenerator.RaidLayout layout =
                generator.CurrentLayout;
            Vector3[][] routes =
            {
                layout.MainRoad,
                layout.ForkRoad,
                layout.BranchRoadA,
                layout.BranchRoadB,
                layout.BranchRoadC
            };
            bool routeSpansStableSides = false;
            foreach (Vector3[] route in routes)
            {
                if (route == null || route.Length == 0)
                {
                    continue;
                }
                float minimumSide = float.PositiveInfinity;
                float maximumSide = float.NegativeInfinity;
                foreach (Vector3 point in route)
                {
                    float side = Vector2.Dot(
                        ToXZ(point),
                        uplandDirection) -
                        escarpmentProjection;
                    minimumSide = Mathf.Min(minimumSide, side);
                    maximumSide = Mathf.Max(maximumSide, side);
                }
                if (minimumSide <= -StableSideDistance &&
                    maximumSide >= StableSideDistance)
                {
                    routeSpansStableSides = true;
                    break;
                }
            }
            Assert.That(
                routeSpansStableSides,
                Is.True,
                "At least one authored trail must reach stable terrain on both sides of the escarpment.");

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(player, Is.Not.Null);
            CharacterController controller =
                player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);
            float controllerGradeLimit = Mathf.Tan(
                controller.slopeLimit * Mathf.Deg2Rad);

            const float RouteProbeDistance = 36f;
            Vector3 passEntry = Vector3.zero;
            Vector3 passExit = Vector3.zero;
            Vector3 passProbeFrom = Vector3.zero;
            Vector3 passProbeDestination = Vector3.zero;
            bool resolvedPass = false;
            for (int index = 0;
                 index < passCenters.Count;
                 index++)
            {
                Vector2 passCenter = passCenters[index];
                Vector2 lowProbe = passCenter -
                    uplandDirection * RouteProbeDistance;
                Vector2 highProbe = passCenter +
                    uplandDirection * RouteProbeDistance;
                Vector3 lowPoint = new Vector3(
                    lowProbe.x,
                    generator.SampleTerrainHeight(
                        lowProbe.x,
                        lowProbe.y) + 1f,
                    lowProbe.y);
                Vector3 highPoint = new Vector3(
                    highProbe.x,
                    generator.SampleTerrainHeight(
                        highProbe.x,
                        highProbe.y) + 1f,
                    highProbe.y);
                if (generator.TryResolveEnemyEscarpmentRoute(
                        lowPoint,
                        highPoint,
                        out passEntry,
                        out passExit))
                {
                    resolvedPass = true;
                    passProbeFrom = lowPoint;
                    passProbeDestination = highPoint;
                    break;
                }
            }
            Assert.That(
                resolvedPass,
                Is.True,
                "Enemy navigation must resolve a committed route through a production escarpment pass.");
            if (generator.AdvancedLandformsEnabled)
            {
                float advancedStepLength = Vector2.Distance(
                    ToXZ(passEntry),
                    ToXZ(passExit));
                Assert.That(
                    advancedStepLength,
                    Is.GreaterThan(8f),
                    "An advanced-region handoff must commit to a meaningful traversal segment.");
                Assert.That(
                    Vector2.Distance(
                        ToXZ(passExit),
                        ToXZ(passProbeDestination)),
                    Is.LessThan(
                        Vector2.Distance(
                            ToXZ(passEntry),
                            ToXZ(passProbeDestination))),
                    "The first advanced-region handoff must progress toward the requested destination.");
                return controllerGradeLimit;
            }
            float entrySide = Vector2.Dot(
                ToXZ(passEntry),
                uplandDirection) -
                escarpmentProjection;
            float exitSide = Vector2.Dot(
                ToXZ(passExit),
                uplandDirection) -
                escarpmentProjection;
            Assert.That(
                entrySide,
                Is.LessThan(-StableSideDistance));
            Assert.That(
                exitSide,
                Is.GreaterThan(StableSideDistance));
            Assert.That(
                Vector2.Distance(
                    ToXZ(passEntry),
                    ToXZ(passExit)),
                Is.GreaterThan(40f),
                "The committed pass route must span both stable sides instead of ending on the cliff transition.");

            Assert.That(
                generator.TryResolveEnemyBridgeRoute(
                    passProbeFrom,
                    passProbeDestination,
                    out Vector3 bridgeEntry,
                    out Vector3 bridgeExit),
                Is.True,
                "The production pass shares its approach with an authored " +
                "bridge and must compose the two safe corridors.");
            Vector3 bridgeClearance =
                EnemyBrain.ResolveCommittedBridgeExitClearancePoint(
                    bridgeEntry,
                    bridgeExit);
            Vector2 passRoute = ToXZ(passExit) - ToXZ(passEntry);
            float passRouteLength = passRoute.magnitude;
            float postBridgeProgress = Vector2.Dot(
                ToXZ(bridgeClearance) - ToXZ(passEntry),
                passRoute / passRouteLength);
            Assert.That(
                generator.IsInsideEnemyEscarpmentPassLane(
                    bridgeClearance,
                    0.25f),
                Is.True,
                "Bridge exit clearance must land inside the same authored " +
                "escarpment corridor.");
            Assert.That(
                postBridgeProgress,
                Is.GreaterThan(0f).And.LessThan(passRouteLength),
                "The bridge exit must make positive pass progress without " +
                "already clearing its far endpoint.");
            Assert.That(
                generator.TryResolveEnemyEscarpmentRoute(
                    bridgeClearance,
                    passProbeDestination,
                    out Vector3 handoffEntry,
                    out Vector3 handoffExit),
                Is.True,
                "After bridge clearance, enemy navigation must hand off to " +
                "the overlapping escarpment pass without walking backward " +
                "through the river.");
            Assert.That(
                Vector2.Distance(
                    ToXZ(handoffEntry),
                    ToXZ(passEntry)),
                Is.LessThan(0.05f));
            Assert.That(
                Vector2.Distance(
                    ToXZ(handoffExit),
                    ToXZ(passExit)),
                Is.LessThan(0.05f));

            const int PassGradeSamples = 112;
            float maximumPassGrade = 0f;
            Vector2 previousPoint = ToXZ(passEntry);
            float previousHeight = generator.SampleTerrainHeight(
                previousPoint.x,
                previousPoint.y);
            bool previousPointUsesBridge =
                generator.IsInsideEnemyBridgeLane(
                    new Vector3(
                        previousPoint.x,
                        previousHeight,
                        previousPoint.y),
                    0.25f);
            for (int sample = 0;
                 sample <= PassGradeSamples;
                 sample++)
            {
                float progress = sample / (float)PassGradeSamples;
                Vector2 point = Vector2.Lerp(
                    ToXZ(passEntry),
                    ToXZ(passExit),
                    progress);
                float height = generator.SampleTerrainHeight(
                    point.x,
                    point.y);
                bool pointUsesBridge =
                    generator.IsInsideEnemyBridgeLane(
                        new Vector3(point.x, height, point.y),
                        0.25f);
                Assert.That(
                    generator.IsInsideEnemyEscarpmentPassLane(
                        new Vector3(point.x, height, point.y),
                        0.05f),
                    Is.True,
                    "Every sampled point on the committed pass centerline must remain inside its authored navigation lane.");
                if (sample > 0 &&
                    !previousPointUsesBridge &&
                    !pointUsesBridge)
                {
                    float spacing = Vector2.Distance(
                        previousPoint,
                        point);
                    maximumPassGrade = Mathf.Max(
                        maximumPassGrade,
                        Mathf.Abs(height - previousHeight) /
                        spacing);
                }
                previousPoint = point;
                previousHeight = height;
                previousPointUsesBridge = pointUsesBridge;
            }
            Assert.That(
                maximumPassGrade,
                Is.LessThan(controllerGradeLimit),
                $"The steepest sampled pass grade ({maximumPassGrade:F3}) must stay below the active controller limit ({controllerGradeLimit:F3}).");

            float uplandAngle = Mathf.Atan2(
                uplandDirection.y,
                uplandDirection.x);
            float uplandCoastRadius =
                layout.CoastRadiusAtAngle(uplandAngle);
            float lowlandCoastRadius =
                layout.CoastRadiusAtAngle(
                    uplandAngle + Mathf.PI);
            Vector2 uplandShore = uplandDirection *
                (uplandCoastRadius - 0.5f);
            Vector2 lowlandShore = -uplandDirection *
                (lowlandCoastRadius - 0.5f);
            float uplandCliffInfluence =
                generator.SampleCliffCoastInfluence(
                    uplandShore.x,
                    uplandShore.y);
            float lowlandCliffInfluence =
                generator.SampleCliffCoastInfluence(
                    lowlandShore.x,
                    lowlandShore.y);
            Assert.That(
                uplandCliffInfluence,
                Is.GreaterThan(0.85f),
                "The upland-facing shore should strongly select the cliff-coast profile.");
            Assert.That(
                lowlandCliffInfluence,
                Is.LessThan(0.15f),
                "The opposite shore should retain the low beach profile.");
            Assert.That(
                uplandCliffInfluence - lowlandCliffInfluence,
                Is.GreaterThan(0.75f));

            foreach (Vector3 endpoint in new[]
                     {
                         layout.PlayerStart,
                         layout.Extraction
                     })
            {
                Vector3 endpointNormal =
                    generator.SampleTerrainNormal(
                        endpoint.x,
                        endpoint.z);
                Assert.That(
                    endpointNormal.y,
                    Is.GreaterThanOrEqualTo(0.88f),
                    "Player entry and extraction must be reselected away from impassable raised-land slopes.");
                Assert.That(
                    generator.SampleCliffTerrainInfluence(
                        endpoint.x,
                        endpoint.z),
                    Is.LessThanOrEqualTo(0.12f),
                    "Player entry and extraction cannot land on an escarpment face.");
                Assert.That(
                    generator.IsInsideEnemyRiverExclusion(
                        endpoint,
                        0f),
                    Is.False,
                    "Player entry and extraction must remain on dry terrain.");
            }

            return controllerGradeLimit;
        }

        private static void AssertBroadNavigableTerrainRegions(
            Vector3[] vertices,
            int gridWidth,
            ProceduralRaidGenerator generator,
            Vector3[] river,
            float controllerGradeLimit)
        {
            if (generator.AdvancedLandformsEnabled)
            {
                AssertDistributedAdvancedTerrain(
                    vertices,
                    gridWidth,
                    generator,
                    river,
                    controllerGradeLimit);
                return;
            }

            float mapRadius = generator.MapRadius;
            Vector2 uplandDirection =
                generator.UplandDirection.normalized;
            Vector2 escarpmentTangent = new Vector2(
                -uplandDirection.y,
                uplandDirection.x);
            IReadOnlyList<Vector2> passCenters =
                generator.EscarpmentPassCenters;
            float escarpmentProjection = 0f;
            for (int index = 0; index < passCenters.Count; index++)
            {
                escarpmentProjection += Vector2.Dot(
                    passCenters[index],
                    uplandDirection);
            }
            escarpmentProjection /= passCenters.Count;

            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            var lowlandHeights = new List<float>();
            var uplandHeights = new List<float>();
            var allGrades = new List<float>();
            var stableTerrainGrades = new List<float>();
            const int SteepBandBinCount = 12;
            var steepBandBins = new bool[SteepBandBinCount];
            int controllerWalkableCount = 0;
            int steepSampleCount = 0;
            int intentionalSteepSampleCount = 0;
            float sampleSpacing = Mathf.Abs(
                vertices[1].x - vertices[0].x);
            for (int z = 1; z < gridWidth - 1; z++)
            {
                for (int x = 1; x < gridWidth - 1; x++)
                {
                    int index = z * gridWidth + x;
                    Vector3 point = vertices[index];
                    Vector2 point2 = ToXZ(point);
                    float coastRadius = generator.CurrentLayout
                        .CoastRadiusAtAngle(
                            Mathf.Atan2(point.z, point.x));
                    if (point2.magnitude > coastRadius * 0.72f ||
                        DistanceToPolyline(point, river) < 8f)
                    {
                        continue;
                    }

                    minimum = Mathf.Min(minimum, point.y);
                    maximum = Mathf.Max(maximum, point.y);
                    float signedEscarpmentDistance = Vector2.Dot(
                        point2,
                        uplandDirection) -
                        escarpmentProjection;
                    const float StableRegionInset = 32f;
                    if (signedEscarpmentDistance <=
                        -StableRegionInset)
                    {
                        lowlandHeights.Add(point.y);
                    }
                    else if (signedEscarpmentDistance >=
                        StableRegionInset)
                    {
                        uplandHeights.Add(point.y);
                    }

                    float xGrade =
                        (vertices[index + 1].y -
                         vertices[index - 1].y) /
                        (sampleSpacing * 2f);
                    float zGrade =
                        (vertices[index + gridWidth].y -
                         vertices[index - gridWidth].y) /
                        (sampleSpacing * 2f);
                    float grade = Mathf.Sqrt(
                        xGrade * xGrade +
                        zGrade * zGrade);
                    allGrades.Add(grade);

                    bool nearPassShoulder = false;
                    for (int passIndex = 0;
                         passIndex < passCenters.Count;
                         passIndex++)
                    {
                        if (Vector2.Distance(
                                point2,
                                passCenters[passIndex]) <= 32f)
                        {
                            nearPassShoulder = true;
                            break;
                        }
                    }
                    bool insideIntentionalSteepBand =
                        Mathf.Abs(signedEscarpmentDistance) <= 12f ||
                        nearPassShoulder;
                    if (!insideIntentionalSteepBand)
                    {
                        stableTerrainGrades.Add(grade);
                    }
                    if (grade <= controllerGradeLimit)
                    {
                        controllerWalkableCount++;
                        continue;
                    }

                    steepSampleCount++;
                    if (insideIntentionalSteepBand)
                    {
                        intentionalSteepSampleCount++;
                    }
                    float tangentDistance = Vector2.Dot(
                        point2,
                        escarpmentTangent);
                    float halfCoverage = mapRadius * 0.60f;
                    if (Mathf.Abs(signedEscarpmentDistance) <= 12f &&
                        Mathf.Abs(tangentDistance) < halfCoverage)
                    {
                        int bin = Mathf.Clamp(
                            Mathf.FloorToInt(
                                Mathf.InverseLerp(
                                    -halfCoverage,
                                    halfCoverage,
                                    tangentDistance) *
                                SteepBandBinCount),
                            0,
                            SteepBandBinCount - 1);
                        steepBandBins[bin] = true;
                    }
                }
            }

            Assert.That(allGrades.Count, Is.GreaterThan(1000));
            Assert.That(lowlandHeights.Count, Is.GreaterThan(500));
            Assert.That(uplandHeights.Count, Is.GreaterThan(500));
            Assert.That(stableTerrainGrades.Count, Is.GreaterThan(1000));
            lowlandHeights.Sort();
            uplandHeights.Sort();
            stableTerrainGrades.Sort();

            float lowlandMedian = PercentileOfSorted(
                lowlandHeights,
                0.50f);
            float uplandMedian = PercentileOfSorted(
                uplandHeights,
                0.50f);
            float stableGrade95 = PercentileOfSorted(
                stableTerrainGrades,
                0.95f);
            float walkableShare = controllerWalkableCount /
                (float)allGrades.Count;
            float steepShare = steepSampleCount /
                (float)allGrades.Count;
            float intentionalSteepShare =
                intentionalSteepSampleCount /
                (float)Mathf.Max(1, steepSampleCount);
            int coveredSteepBandBins = steepBandBins.Count(
                covered => covered);

            Assert.That(
                maximum - minimum,
                Is.GreaterThan(
                    ProceduralRaidGenerator.RaisedUplandHeight),
                "The dry interior should contain a real raised land slice, not uniformly rolling terrain.");
            Assert.That(
                uplandMedian - lowlandMedian,
                Is.GreaterThan(
                    ProceduralRaidGenerator.RaisedUplandHeight *
                    0.65f),
                "Stable upland and lowland regions need clearly separated median elevations.");
            Assert.That(
                PercentileOfSorted(uplandHeights, 0.25f) -
                    PercentileOfSorted(lowlandHeights, 0.75f),
                Is.GreaterThan(1.5f),
                "The middle half of the upland should remain visibly above the middle half of the lowland.");
            Assert.That(
                stableGrade95,
                Is.LessThan(controllerGradeLimit * 0.70f),
                "Stable terrain away from the escarpment should remain comfortably below the controller slope limit.");
            Assert.That(
                walkableShare,
                Is.GreaterThan(0.90f),
                "Most dry interior terrain must remain controller-walkable even with the new cliff band.");
            Assert.That(
                steepSampleCount,
                Is.GreaterThan(50),
                "The raised slice needs a meaningful steep face rather than only a height offset.");
            Assert.That(
                steepShare,
                Is.LessThan(0.08f),
                "Above-limit terrain should remain a limited escarpment band, not dominate the island interior.");
            Assert.That(
                intentionalSteepShare,
                Is.GreaterThan(0.80f),
                "Above-limit samples should concentrate on the escarpment and authored pass shoulders instead of appearing as random traversal hazards.");
            Assert.That(
                coveredSteepBandBins,
                Is.GreaterThanOrEqualTo(SteepBandBinCount / 2),
                "The cliff face should read as one broad landform band rather than a few isolated steep spikes.");
        }

        private static void AssertDistributedAdvancedTerrain(
            Vector3[] vertices,
            int gridWidth,
            ProceduralRaidGenerator generator,
            Vector3[] river,
            float controllerGradeLimit)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            int sampled = 0;
            int walkable = 0;
            float sampleSpacing = Mathf.Abs(
                vertices[1].x - vertices[0].x);
            for (int z = 1; z < gridWidth - 1; z++)
            {
                for (int x = 1; x < gridWidth - 1; x++)
                {
                    int index = z * gridWidth + x;
                    Vector3 point = vertices[index];
                    Vector2 point2 = ToXZ(point);
                    float coastRadius = generator.CurrentLayout
                        .CoastRadiusAtAngle(
                            Mathf.Atan2(point.z, point.x));
                    if (point2.magnitude > coastRadius * 0.72f ||
                        DistanceToPolyline(point, river) < 8f)
                    {
                        continue;
                    }

                    minimum = Mathf.Min(minimum, point.y);
                    maximum = Mathf.Max(maximum, point.y);
                    float xGrade =
                        (vertices[index + 1].y -
                         vertices[index - 1].y) /
                        (sampleSpacing * 2f);
                    float zGrade =
                        (vertices[index + gridWidth].y -
                         vertices[index - gridWidth].y) /
                        (sampleSpacing * 2f);
                    float grade = Mathf.Sqrt(
                        xGrade * xGrade + zGrade * zGrade);
                    sampled++;
                    if (grade <= controllerGradeLimit)
                    {
                        walkable++;
                    }
                }
            }

            Assert.That(sampled, Is.GreaterThan(1000));
            Assert.That(
                maximum - minimum,
                Is.GreaterThan(
                    ProceduralRaidGenerator.RaisedUplandHeight),
                "Distributed tiers must retain substantial vertical range.");
            Assert.That(
                walkable / (float)sampled,
                Is.GreaterThan(0.86f),
                "Most terrain between distributed tiers must remain controller-walkable.");

            Vector2 direction = generator.UplandDirection.normalized;
            foreach (ProceduralRaidGenerator.LandformTier tier in new[]
                     {
                         ProceduralRaidGenerator.LandformTier.MidShelf,
                         ProceduralRaidGenerator.LandformTier.Highland
                     })
            {
                bool positive = false;
                bool negative = false;
                foreach (ProceduralRaidGenerator.LandformRegion region in
                         generator.AdvancedLandformRegions)
                {
                    if (region.Tier != tier)
                    {
                        continue;
                    }
                    float projection = Vector2.Dot(
                        region.Center,
                        direction);
                    positive |= projection > 0f;
                    negative |= projection < 0f;
                }
                Assert.That(
                    positive && negative,
                    Is.True,
                    $"{tier} terrain must occur on both map halves.");
            }
        }

        private static float PercentileOfSorted(
            List<float> values,
            float percentile)
        {
            float scaledIndex = Mathf.Clamp01(percentile) *
                (values.Count - 1);
            int lower = Mathf.FloorToInt(scaledIndex);
            int upper = Mathf.Min(lower + 1, values.Count - 1);
            return Mathf.Lerp(
                values[lower],
                values[upper],
                scaledIndex - lower);
        }

        private static float MinimumTerrainHeight(
            MeshCollider terrain,
            Vector3 center,
            float radiusX,
            float radiusZ,
            int perimeterSamples)
        {
            float minimumHeight = float.PositiveInfinity;
            for (int ring = 0;
                 ring <= 2;
                 ring++)
            {
                int sampleCount =
                    ring == 0
                        ? 1
                        : perimeterSamples;
                float radiusScale = ring * 0.5f;
                for (int sample = 0;
                     sample < sampleCount;
                     sample++)
                {
                    float angle =
                        sample *
                        Mathf.PI * 2f /
                        sampleCount;
                    Vector3 origin =
                        new Vector3(
                            center.x +
                            Mathf.Cos(angle) *
                            radiusX *
                            radiusScale,
                            center.y + 30f,
                            center.z +
                            Mathf.Sin(angle) *
                            radiusZ *
                            radiusScale);
                    bool foundGround =
                        terrain.Raycast(
                            new Ray(
                                origin,
                                Vector3.down),
                            out RaycastHit hit,
                            60f);
                    Assert.That(
                        foundGround,
                        Is.True,
                        "The sampled scenery footprint should remain over generated terrain.");
                    minimumHeight =
                        Mathf.Min(
                            minimumHeight,
                            hit.point.y);
                }
            }

            return minimumHeight;
        }

        private static void AssertGalleryRow(
            string rowName,
            int expectedCount)
        {
            GameObject row = GameObject.Find(rowName);
            Assert.That(row, Is.Not.Null);
            Assert.That(
                row.transform.childCount,
                Is.EqualTo(expectedCount));
            Renderer[] renderers =
                row.GetComponentsInChildren<Renderer>(
                    true);
            Assert.That(renderers, Is.Not.Empty);
            foreach (Renderer renderer in renderers)
            {
                if (renderer.name.StartsWith("UCX_"))
                {
                    Assert.That(
                        renderer.enabled,
                        Is.False,
                        $"{renderer.name} should stay hidden in the gallery.");
                    continue;
                }
                Assert.That(
                    renderer.sharedMaterial,
                    Is.Not.Null);
                Assert.That(
                    renderer.sharedMaterial.mainTexture,
                    Is.Not.Null,
                    $"{renderer.name} needs its in-game texture.");
            }
        }
    }
}
