using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests
{
    public sealed class ProceduralRaidGenerationTests
    {
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
                terrainRenderer.sharedMaterials[1]
                    .mainTexture.name,
                Is.EqualTo(
                    "T_Landscape_dirt_BaseColor"));
            Assert.That(river, Is.Not.Null);
            Assert.That(
                river.GetComponent<MeshRenderer>(),
                Is.Not.Null);
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
                generator.GeneratedGrassCount,
                Is.GreaterThanOrEqualTo(17000));
            Assert.That(
                generator.GeneratedUndergrowthCount,
                Is.GreaterThanOrEqualTo(125));
            Assert.That(
                generator.GeneratedBoulderCount,
                Is.GreaterThanOrEqualTo(44));
            Assert.That(
                generator.GeneratedTrailStoneCount,
                Is.GreaterThanOrEqualTo(38));
            Assert.That(
                grass.childCount,
                Is.InRange(50, 65),
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
                }
            }
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
                Is.GreaterThan(100000),
                "The combined meadow must contain the authored grass geometry.");
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
                boulders.GetComponentInChildren<BoxCollider>(),
                Is.Not.Null,
                "Large boulders should block movement.");
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
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(
                RenderSettings.fogMode,
                Is.EqualTo(FogMode.Linear));
            Assert.That(
                RenderSettings.fogColor,
                Is.EqualTo(
                    new Color(
                        0.72f,
                        0.74f,
                        0.75f,
                        1f)));
            Assert.That(
                RenderSettings.fogStartDistance,
                Is.EqualTo(14f));
            Assert.That(
                RenderSettings.fogEndDistance,
                Is.EqualTo(62f));
            MeshCollider terrainCollider =
                terrain.GetComponent<MeshCollider>();
            Transform firstTree = forest.GetChild(0);
            bool foundGround =
                terrainCollider.Raycast(
                    new Ray(
                        firstTree.position +
                        Vector3.up * 30f,
                        Vector3.down),
                    out RaycastHit groundHit,
                    60f);
            Assert.That(foundGround, Is.True);
            Assert.That(
                firstTreeRenderer.bounds.min.y,
                Is.EqualTo(groundHit.point.y).Within(0.08f));

            if (generator.CurrentLayout.RiverCrossesRoad)
            {
                Assert.That(
                    generator.transform.Find(
                        $"Generated Raid {generator.Seed}/Road Bridge"),
                    Is.Not.Null);
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
            Assert.That(extraction, Is.Not.Null);
            Assert.That(enemies, Has.Length.EqualTo(3));
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
    }
}
