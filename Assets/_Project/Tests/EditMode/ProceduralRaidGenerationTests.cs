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
    public sealed class ProceduralRaidGenerationTests
    {
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
                second.River,
                Is.EqualTo(first.River));
            Assert.That(
                second.PlayerStart,
                Is.EqualTo(first.PlayerStart));
            Assert.That(
                second.Extraction,
                Is.EqualTo(first.Extraction));
        }

        [Test]
        public void ExpandedMainRoadTouchesBoundaryButSpawnUsesSafeOuterAnnulus()
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
                XzMagnitude(layout.MainRoad[0]),
                Is.InRange(Radius - 2.1f, Radius));
            Assert.That(
                XzMagnitude(
                    layout.MainRoad[
                        layout.MainRoad.Length - 1]),
                Is.InRange(Radius - 2.1f, Radius));
            Assert.That(
                XzMagnitude(layout.PlayerStart) / Radius,
                Is.InRange(0.70f, 0.88f),
                "The player should spawn in a broad outer donut with enough terrain behind them to hide the disc edge.");
            Assert.That(
                XzMagnitude(layout.Extraction) / Radius,
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
        public void SeedsProduceBroadCoverageWithAtMostTwoEdgeBranches()
        {
            bool foundOnePrimary = false;
            bool foundTwoPrimaries = false;
            bool foundOneBranch = false;
            bool foundTwoBranches = false;
            for (int seed = 1; seed <= 40; seed++)
            {
                ProceduralRaidGenerator.RaidLayout layout =
                    ProceduralRaidGenerator.CreateLayout(
                        seed,
                        144f);
                foundOnePrimary |= layout.ForkRoad.Length == 0;
                foundTwoPrimaries |= layout.ForkRoad.Length > 0;
                int branchCount =
                    (layout.BranchRoadA.Length > 0 ? 1 : 0) +
                    (layout.BranchRoadB.Length > 0 ? 1 : 0) +
                    (layout.BranchRoadC.Length > 0 ? 1 : 0);
                foundOneBranch |= branchCount == 1;
                foundTwoBranches |= branchCount >= 2;

                Assert.That(branchCount, Is.InRange(1, 2),
                    $"Seed {seed} should use one or two purposeful branches.");
                Assert.That(layout.BranchRoadC, Is.Empty,
                    "The road grammar should cap secondary branches at two.");
                Vector3[][] branches =
                {
                    layout.BranchRoadA,
                    layout.BranchRoadB
                };
                foreach (Vector3[] branch in branches)
                {
                    if (branch.Length == 0)
                    {
                        continue;
                    }
                    Assert.That(
                        XzMagnitude(branch[branch.Length - 1]),
                        Is.GreaterThan(140f));
                }
                Assert.That(layout.RiverCrossesRoad, Is.True);
            }

            Assert.That(foundOnePrimary, Is.True);
            Assert.That(foundTwoPrimaries, Is.True);
            Assert.That(foundOneBranch, Is.True);
            Assert.That(foundTwoBranches, Is.True);
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
            Assert.That(XzMagnitude(layout.River[0]), Is.GreaterThan(140f));
            Assert.That(
                XzMagnitude(layout.River[layout.River.Length - 1]),
                Is.GreaterThan(140f));
            Assert.That(
                pathLength,
                Is.GreaterThan(directDistance * 1.025f),
                "The primary river should visibly wind rather than read as a straight ribbon.");
        }

        [Test]
        public void TrailForksAndRiverCrossingsRemainIntentionalAcrossSeeds()
        {
            const float MinimumForkClearance = 17.5f;
            const float MinimumCrossingSeparation = 25f;
            for (int iteration = 1; iteration <= 242; iteration++)
            {
                int seed = iteration <= 240
                    ? iteration
                    : iteration == 241
                        ? 1242752318
                        : 586556700;
                ProceduralRaidGenerator.RaidLayout layout =
                    ProceduralRaidGenerator.CreateLayout(seed, 144f);
                Vector3[][] roads =
                {
                    layout.MainRoad,
                    layout.ForkRoad,
                    layout.BranchRoadA,
                    layout.BranchRoadB,
                    layout.BranchRoadC
                };
                Vector3[][] branches =
                {
                    layout.BranchRoadA,
                    layout.BranchRoadB,
                    layout.BranchRoadC
                };
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
                            Is.GreaterThanOrEqualTo(51.5f),
                            $"Seed {seed} sends separate trails to effectively the same boundary destination.");
                        Assert.That(
                            BoundaryAngleBetween(
                                routeDestinations[first],
                                routeDestinations[second]),
                            Is.GreaterThanOrEqualTo(0.5f),
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

                AssertPurposefulBranch(
                    seed,
                    layout.BranchRoadA,
                    layout.MainRoad,
                    new[]
                    {
                        layout.MainRoad,
                        layout.ForkRoad
                    });
                AssertPurposefulBranch(
                    seed,
                    layout.BranchRoadB,
                    layout.ForkRoad.Length > 0
                        ? layout.ForkRoad
                        : layout.MainRoad,
                    new[]
                    {
                        layout.MainRoad,
                        layout.ForkRoad,
                        layout.BranchRoadA
                    });

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
                        Is.LessThanOrEqualTo(0.025f),
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
            Transform horizonBoundary =
                generator.transform.Find(
                    $"Generated Raid {generator.Seed}/" +
                    "Arena Horizon Fog and Boundary");
            Assert.That(terrain, Is.Not.Null);
            Assert.That(horizonBoundary, Is.Not.Null);
            MeshRenderer horizonFog =
                horizonBoundary.GetComponentInChildren<MeshRenderer>();
            Assert.That(horizonFog, Is.Not.Null);
            Assert.That(
                horizonFog.sharedMaterial.shader.name,
                Is.EqualTo("WorldBuilder/Low Horizon Fog"));
            Assert.That(
                horizonBoundary.GetComponentsInChildren<BoxCollider>(),
                Has.Length.EqualTo(48),
                "The faded horizon needs a complete physical boundary ring.");
            Vector3[] generatedRiver =
                generator.CurrentLayout.River;
            int dryCrossingIndex = 1;
            float farthestFromTrail = float.NegativeInfinity;
            for (int index = 1;
                 index < generatedRiver.Length - 1;
                 index++)
            {
                float trailDistance = DistanceToTrailNetwork(
                    generatedRiver[index],
                    generator.CurrentLayout);
                if (trailDistance > farthestFromTrail)
                {
                    farthestFromTrail = trailDistance;
                    dryCrossingIndex = index;
                }
            }
            Vector3 riverTangent = Vector3.ProjectOnPlane(
                generatedRiver[dryCrossingIndex + 1] -
                generatedRiver[dryCrossingIndex - 1],
                Vector3.up).normalized;
            Vector3 acrossRiver = Vector3.Cross(
                Vector3.up,
                riverTangent).normalized;
            Assert.That(
                generator.TryResolveEnemyRiverWaypoint(
                    generatedRiver[dryCrossingIndex] +
                        acrossRiver * 10f,
                    generatedRiver[dryCrossingIndex] -
                        acrossRiver * 10f,
                    out Vector3 bridgeWaypoint),
                Is.True,
                "Enemy pursuit across open water should redirect toward a fitted bridge.");
            Assert.That(
                generator.IsInsideEnemyRiverExclusion(
                    bridgeWaypoint,
                    0.25f),
                Is.False,
                "The selected bridge approach waypoint must remain on dry ground.");
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
            int mixedHabitatVertices = 0;
            foreach (Vector4 weights in habitatWeights)
            {
                Assert.That(
                    weights.y,
                    Is.LessThanOrEqualTo(0.001f),
                    "The removed dark canopy-duff layer must not contribute anywhere on the generated terrain.");
                float stony = 1f - weights.x - weights.y -
                    weights.z - weights.w;
                Assert.That(stony, Is.GreaterThanOrEqualTo(-0.002f));
                Assert.That(
                    weights.x + weights.y + weights.z +
                    weights.w + stony,
                    Is.EqualTo(1f).Within(0.002f));
                int visibleWeights =
                    (weights.x > 0.04f ? 1 : 0) +
                    (weights.y > 0.04f ? 1 : 0) +
                    (weights.z > 0.04f ? 1 : 0) +
                    (weights.w > 0.04f ? 1 : 0) +
                    (stony > 0.04f ? 1 : 0);
                if (visibleWeights >= 2 && visibleWeights <= 3)
                {
                    mixedHabitatVertices++;
                }
            }
            Assert.That(
                mixedHabitatVertices,
                Is.GreaterThan(terrainMesh.vertexCount * 0.55f),
                "Most forest vertices should feather between one dominant habitat and one or two supporting habitats.");
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
                Is.EqualTo("ForestMossCarpet_BaseColor_2048"));
            Assert.That(
                terrainMaterial.GetTexture("_GroundcoverMap").name,
                Is.EqualTo("ForestCreepingGroundcover_BaseColor_2048"));
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
            AssertBroadNavigableTerrainRegions(
                terrainVertices,
                gridWidth,
                generator.MapRadius,
                generator.CurrentLayout.River);
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
                Is.GreaterThanOrEqualTo(1120));
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
            Assert.That(generator.GeneratedCampCount, Is.InRange(1, 3));
            Assert.That(
                camps.childCount,
                Is.EqualTo(generator.GeneratedCampCount));
            Assert.That(
                generator.GeneratedCampGuardCount,
                Is.InRange(generator.GeneratedCampCount, 9));
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
                Is.GreaterThanOrEqualTo(700),
                "Tall grass should emerge from the bases of many trees.");
            Assert.That(
                generator.GeneratedPlantEdgeGrassCount,
                Is.GreaterThanOrEqualTo(150),
                "Tall grass should blend into the edges of flower, clover, and shrub patches.");
            Assert.That(
                generator.GeneratedTreeBaseFoliageCount,
                Is.GreaterThanOrEqualTo(450),
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
            Assert.That(
                grass.childCount,
                Is.InRange(130, 190),
                "Meadow grass should be combined into localized render chunks.");
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
                Is.InRange(270f, 290f),
                $"Grass batches should span the raid disc, actual bounds were {grassBounds}.");
            Assert.That(
                grassBounds.size.z,
                Is.InRange(270f, 290f),
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
                Is.InRange(120, 190),
                "Thousands of foliage placements should collapse into localized render chunks.");
            Assert.That(
                undergrowth.GetComponentsInChildren<Renderer>().Length,
                Is.EqualTo(undergrowth.childCount));
            Assert.That(
                generator.GeneratedRendererCount,
                Is.LessThan(4500),
                "The generated Raid should remain within its renderer-object budget.");
            Assert.That(
                generator.GeneratedColliderCount,
                Is.LessThan(2200),
                "Only terrain, solid wood, rocks, water crossings, and gameplay surfaces should retain colliders.");
            RaidEnvironmentCuller environmentCuller =
                generator.GetComponent<RaidEnvironmentCuller>();
            Assert.That(environmentCuller, Is.Not.Null);
            Assert.That(
                environmentCuller.EntryCount,
                Is.InRange(1900, 2400));
            Assert.That(
                environmentCuller.RendererDistanceCullingEnabled,
                Is.False,
                "Distant trees must remain rendered so the arena never exposes an empty pop-in boundary.");
            Assert.That(
                Camera.main.farClipPlane,
                Is.GreaterThanOrEqualTo(324f));
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
            Assert.That(RenderSettings.fog, Is.True);
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
                RenderSettings.fogColor,
                Is.EqualTo(
                    new Color(
                        0.22f,
                        0.36f,
                        0.34f,
                        1f)));
            Assert.That(
                RenderSettings.fogStartDistance,
                Is.EqualTo(21f));
            Assert.That(
                RenderSettings.fogEndDistance,
                Is.EqualTo(90f));
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
            Assert.That(campGuardPoolCount, Is.EqualTo(9));
            EnemyBrain[] enemies = trailEnemies.ToArray();
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
                XzMagnitude(player.transform.position) /
                    generator.MapRadius,
                Is.InRange(0.70f, 0.88f));
            Assert.That(
                XzMagnitude(extraction.transform.position) /
                    generator.MapRadius,
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
            Vector3[][] trails =
            {
                layout.MainRoad,
                layout.ForkRoad,
                layout.BranchRoadA,
                layout.BranchRoadB,
                layout.BranchRoadC
            };
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
                Is.GreaterThanOrEqualTo(0.56f),
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
                        Is.LessThan(3),
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

        private static void AssertBroadNavigableTerrainRegions(
            Vector3[] vertices,
            int gridWidth,
            float mapRadius,
            Vector3[] river)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            var quadrantTotals = new float[4];
            var quadrantCounts = new int[4];
            var grades = new List<float>();
            float sampleSpacing = Mathf.Abs(
                vertices[1].x - vertices[0].x);
            for (int z = 1; z < gridWidth - 1; z++)
            {
                for (int x = 1; x < gridWidth - 1; x++)
                {
                    int index = z * gridWidth + x;
                    Vector3 point = vertices[index];
                    if (new Vector2(point.x, point.z).magnitude >
                            mapRadius * 0.72f ||
                        DistanceToPolyline(point, river) < 8f)
                    {
                        continue;
                    }

                    minimum = Mathf.Min(minimum, point.y);
                    maximum = Mathf.Max(maximum, point.y);
                    int quadrant =
                        (point.x >= 0f ? 1 : 0) +
                        (point.z >= 0f ? 2 : 0);
                    quadrantTotals[quadrant] += point.y;
                    quadrantCounts[quadrant]++;
                    grades.Add(Mathf.Abs(
                        vertices[index + 1].y - point.y) /
                        sampleSpacing);
                    grades.Add(Mathf.Abs(
                        vertices[index + gridWidth].y - point.y) /
                        sampleSpacing);
                }
            }

            float lowestRegion = float.PositiveInfinity;
            float highestRegion = float.NegativeInfinity;
            for (int index = 0; index < quadrantTotals.Length; index++)
            {
                Assert.That(quadrantCounts[index], Is.GreaterThan(100));
                float average = quadrantTotals[index] / quadrantCounts[index];
                lowestRegion = Mathf.Min(lowestRegion, average);
                highestRegion = Mathf.Max(highestRegion, average);
            }
            grades.Sort();
            float ninetyFifthPercentile = grades[
                Mathf.Clamp(
                    Mathf.FloorToInt(grades.Count * 0.95f),
                    0,
                    grades.Count - 1)];

            Assert.That(
                maximum - minimum,
                Is.GreaterThan(10f),
                "The playable interior should include meaningful high and low terrain, not one uniformly rolling surface.");
            Assert.That(
                highestRegion - lowestRegion,
                Is.GreaterThan(1.4f),
                "Arena-scale sectors should sit at visibly different average elevations.");
            Assert.That(
                ninetyFifthPercentile,
                Is.LessThan(0.48f),
                "At least 95% of dry interior terrain should remain comfortably traversable by the player controller.");
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
