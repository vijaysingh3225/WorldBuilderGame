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
    public sealed class ProceduralRaidGenerationTests
    {
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
                    "T_Landscape_grass_BaseColor"));
            AssertGalleryRow("01 - All Trees", 11);
            AssertGalleryRow(
                "02 - Bushes Flowers and Plants",
                10);
            AssertGalleryRow("03 - All Rocks", 9);
            AssertGalleryRow("04 - All Grass", 5);
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
                    72f);
            ProceduralRaidGenerator.RaidLayout second =
                ProceduralRaidGenerator.CreateLayout(
                    17381,
                    72f);

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
                second.River,
                Is.EqualTo(first.River));
        }

        [Test]
        public void MainRoadConnectsOppositeSidesOfTheDisc()
        {
            ProceduralRaidGenerator.RaidLayout layout =
                ProceduralRaidGenerator.CreateLayout(
                    90210,
                    72f);

            Assert.That(
                layout.MainRoad,
                Has.Length.GreaterThanOrEqualTo(12));
            Assert.That(
                layout.PlayerStart.z,
                Is.LessThan(-60f));
            Assert.That(
                layout.Extraction.z,
                Is.GreaterThan(60f));
            Assert.That(
                new Vector2(
                    layout.PlayerStart.x,
                    layout.PlayerStart.z).magnitude,
                Is.LessThanOrEqualTo(72f));
            Assert.That(
                new Vector2(
                    layout.Extraction.x,
                    layout.Extraction.z).magnitude,
                Is.LessThanOrEqualTo(72f));
        }

        [Test]
        public void SeedsProduceBothForkedAndSingleRoadVariants()
        {
            bool foundFork = false;
            bool foundSingle = false;
            bool foundCrossing = false;
            bool foundAlongside = false;
            for (int seed = 1; seed <= 40; seed++)
            {
                ProceduralRaidGenerator.RaidLayout layout =
                    ProceduralRaidGenerator.CreateLayout(
                        seed,
                        72f);
                foundFork |= layout.HasRoadFork;
                foundSingle |= !layout.HasRoadFork;
                foundCrossing |= layout.RiverCrossesRoad;
                foundAlongside |= !layout.RiverCrossesRoad;
            }

            Assert.That(foundFork, Is.True);
            Assert.That(foundSingle, Is.True);
            Assert.That(foundCrossing, Is.True);
            Assert.That(foundAlongside, Is.True);
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
                    $"Generated Raid {generator.Seed}/Terrain Disc");
            Transform road =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Main Dirt Road");
            Transform river =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/River");
            Assert.That(terrain, Is.Not.Null);
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
                    "T_Landscape_grass_BaseColor"));
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
                    "T_Landscape_dirt_BaseColor"));
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
                Is.GreaterThanOrEqualTo(280));
            Transform forest =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Dense Stylized Forest");
            Assert.That(forest, Is.Not.Null);
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
                generator.RockVariantCount,
                Is.EqualTo(9));
            Assert.That(
                forest.childCount,
                Is.EqualTo(generator.GeneratedTreeCount));
            foreach (Transform tree in forest)
            {
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
            Transform boulders =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Boulders");
            Transform trailStones =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/Trail and Edge Stones");
            Assert.That(grass, Is.Not.Null);
            Assert.That(undergrowth, Is.Not.Null);
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
                generator.GeneratedGrassCount,
                Is.GreaterThanOrEqualTo(31000));
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
                Is.GreaterThanOrEqualTo(400));
            Assert.That(
                generator.GeneratedBushGroupCount,
                Is.GreaterThanOrEqualTo(8),
                "Bushes should form repeated same-species shrub colonies.");
            Assert.That(
                generator.GeneratedBushClusterMemberCount /
                    (float)generator.GeneratedBushGroupCount,
                Is.GreaterThanOrEqualTo(4.8f),
                "Shrub colonies should be visibly larger than isolated prop groups.");
            Assert.That(
                generator.GeneratedFlowerPatchCount,
                Is.GreaterThanOrEqualTo(8),
                "Flowers should appear in natural colonies instead of isolated single props.");
            Assert.That(
                generator.GeneratedFlowerClusterMemberCount /
                    (float)generator.GeneratedFlowerPatchCount,
                Is.GreaterThanOrEqualTo(8f),
                "Flower colonies should read as broad patches with many matching plants.");
            Assert.That(
                generator.GeneratedGroundCoverPatchCount,
                Is.GreaterThanOrEqualTo(8),
                "Clover and low plants should form exaggerated ground-cover patches.");
            Assert.That(
                generator.GeneratedBoulderGrassCount,
                Is.GreaterThanOrEqualTo(180),
                "Large boulders should gather pockets of taller spiky grass around their sheltered sides.");
            Assert.That(
                generator.GeneratedTreeBaseGrassCount,
                Is.GreaterThanOrEqualTo(700),
                "Tall grass should emerge from the bases of many trees.");
            Assert.That(
                generator.GeneratedPlantEdgeGrassCount,
                Is.GreaterThanOrEqualTo(150),
                "Tall grass should blend into the edges of flower, clover, and shrub patches.");
            Assert.That(
                generator.GeneratedTreeBaseFoliageCount,
                Is.GreaterThanOrEqualTo(100),
                "Tree trunks should gather visible shrubs and ground cover at their bases.");
            Assert.That(
                generator.GeneratedBoulderCount,
                Is.GreaterThanOrEqualTo(44));
            Assert.That(
                generator.GeneratedTrailStoneCount,
                Is.GreaterThanOrEqualTo(38));
            Assert.That(
                grass.childCount,
                Is.InRange(90, 125),
                "Meadow grass should be combined into a small number of render batches.");
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
                Is.InRange(125f, 150f),
                $"Grass batches should span the raid disc, actual bounds were {grassBounds}.");
            Assert.That(
                grassBounds.size.z,
                Is.InRange(125f, 150f),
                $"Grass batches should span the raid disc, actual bounds were {grassBounds}.");
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
                Is.EqualTo(
                    generator.GeneratedUndergrowthCount));
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
            Assert.That(
                firstTreeRenderer.bounds.size.y,
                Is.InRange(14f, 21.5f));
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
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(
                RenderSettings.fogMode,
                Is.EqualTo(FogMode.Linear));
            Assert.That(
                RenderSettings.fogColor,
                Is.EqualTo(
                    new Color(
                        0.46f,
                        0.52f,
                        0.58f,
                        1f)));
            Assert.That(
                RenderSettings.fogStartDistance,
                Is.EqualTo(28f));
            Assert.That(
                RenderSettings.fogEndDistance,
                Is.EqualTo(105f));
            Assert.That(
                RenderSettings.ambientIntensity,
                Is.EqualTo(1.05f));
            Assert.That(
                RenderSettings.ambientGroundColor.r,
                Is.EqualTo(0.16f).Within(0.001f));
            Light raidSun =
                GameObject.Find("Sun")
                    .GetComponent<Light>();
            Assert.That(raidSun, Is.Not.Null);
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
                        0.62f,
                        0.62f,
                        16);
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
                    generator.BridgePrefab,
                    Is.Not.Null,
                    "River crossings should use the imported bridge asset.");
                Transform bridge =
                    generator.transform.Find(
                        $"Generated Raid {generator.Seed}/Road Bridge");
                Assert.That(
                    bridge,
                    Is.Not.Null);
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
                for (int hitIndex = 0;
                     hitIndex < bridgeHits.Length;
                     hitIndex++)
                {
                    if (bridgeHits[hitIndex].collider != null &&
                        bridgeHits[hitIndex].collider.transform
                            .IsChildOf(bridge))
                    {
                        hasCenterSupport = true;
                        break;
                    }
                }
                Assert.That(
                    hasCenterSupport,
                    Is.True,
                    "The center of the trail must have bridge support rather than open water.");
                MeshFilter rootMesh =
                    bridge.GetComponent<MeshFilter>();
                if (rootMesh != null && rootMesh.sharedMesh != null)
                {
                    Assert.That(
                        rootMesh.sharedMesh.name,
                        Is.Not.EqualTo("Cube"),
                        "The old primitive cube bridge should be replaced.");
                }
            }

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            ExtractionZone extraction =
                Object.FindFirstObjectByType<ExtractionZone>();
            EnemyBrain[] enemies =
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
                playerMaterial.color.r,
                Is.EqualTo(0.36f).Within(0.001f),
                "The player should remain a clearly visible mid-light grey.");
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
            Assert.That(enemies, Has.Length.EqualTo(3));
            foreach (EnemyBrain enemy in enemies)
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
            }
            Assert.That(
                player.transform.position.z,
                Is.LessThan(-55f));
            Assert.That(
                extraction.transform.position.z,
                Is.GreaterThan(55f));
            Assert.That(
                enemies,
                Has.All.Matches<EnemyBrain>(
                    enemy =>
                        enemy.transform.position.z >
                            player.transform.position.z &&
                        enemy.transform.position.z <
                            extraction.transform.position.z));
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
