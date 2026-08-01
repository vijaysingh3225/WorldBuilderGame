using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    public sealed class ProceduralRaidGenerator : MonoBehaviour
    {
        [Serializable]
        public sealed class RaidLayout
        {
            public int Seed;
            public bool HasRoadFork;
            public bool RiverCrossesRoad;
            public Vector3[] MainRoad;
            public Vector3[] ForkRoad;
            public Vector3[] BranchRoadA;
            public Vector3[] BranchRoadB;
            public Vector3[] BranchRoadC;
            public Vector3[] River;
            public float PlayerSpawnRoadT;
            public float ExtractionRoadT;

            public Vector3 PlayerStart =>
                MainRoad != null && MainRoad.Length > 0
                    ? PointOnPolyline(
                        MainRoad,
                        PlayerSpawnRoadT)
                    : Vector3.zero;

            public Vector3 Extraction =>
                MainRoad != null && MainRoad.Length > 0
                    ? PointOnPolyline(
                        MainRoad,
                        ExtractionRoadT)
                    : Vector3.zero;
        }

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private EnemyBrain[] enemies;
        [SerializeField] private ExtractionZone extractionZone;
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject[] grassPrefabs;
        [SerializeField] private GameObject[] undergrowthPrefabs;
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField] private GameObject bridgePrefab;
        [Header("Materials")]
        [SerializeField] private Material forestGroundMaterial;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material bridgeMaterial;
        [SerializeField] private Material treeBarkMaterial;
        [SerializeField] private Material birchBarkMaterial;
        [SerializeField] private Material treeLeavesMaterial;
        [SerializeField] private Material pineLeavesMaterial;
        [SerializeField] private Material grassDetailMaterial;
        [SerializeField] private Material plantDetailMaterial;
        [SerializeField] private Material rockMaterial;
        [Header("Generation")]
        [SerializeField, Min(30f)] private float mapRadius = 144f;
        [SerializeField, Range(24, 384)] private int terrainResolution = 256;
        [SerializeField, Range(80, 1600)] private int treeCount = 640;
        [SerializeField, Range(8000, 140000)] private int grassCount = 64000;
        [SerializeField, Range(40, 1800)] private int undergrowthCount = 760;
        [SerializeField, Range(10, 260)] private int boulderCount = 96;
        [SerializeField, Range(10, 240)] private int trailStoneCount = 80;
        [SerializeField, Min(1f)] private float roadHalfWidth = 1.8f;
        [SerializeField, Min(1f)] private float riverHalfWidth = 3.1f;
        [SerializeField, Min(0.5f)] private float treeClearance = 5.8f;
        [SerializeField] private int fallbackSeed = 20260730;

        private const float RoadIndentation = 0.18f;
        private const float RoadShoulderWidth = 2.2f;
        private const float GrassRoadInteriorLimit = -1.35f;
        private const float GrassRiverClearance = 0.78f;
        private const float RiverWaterBankOverlap = 1.15f;
        private const float BridgeCrossSectionScale = 0.46f;
        private const float BridgeExtraWidthScale = 1.65f;
        private const float BridgeDeckLift = 0.35f;
        private const int GrassPlacementsPerBatch = 768;

        private sealed class GrassMeshSource
        {
            public string Name;
            public Quaternion ImportedRotation;
            public Bounds LocalBounds;
            public GrassMeshPart[] Parts;
        }

        private struct GrassMeshPart
        {
            public Mesh Mesh;
            public Matrix4x4 LocalMatrix;
        }

        private struct BoulderPlacement
        {
            public Vector2 Position;
            public float Radius;
        }

        private struct TrailRiverCrossing
        {
            public Vector3 Point;
            public Vector3 RoadDirection;
        }

        private readonly List<Vector3> mainRoadSamples =
            new List<Vector3>();
        private readonly List<Vector3> forkRoadSamples =
            new List<Vector3>();
        private readonly List<Vector3> branchRoadASamples =
            new List<Vector3>();
        private readonly List<Vector3> branchRoadBSamples =
            new List<Vector3>();
        private readonly List<Vector3> branchRoadCSamples =
            new List<Vector3>();
        private readonly List<Vector3> riverSamples =
            new List<Vector3>();
        private readonly List<Vector2> generatedTreePositions =
            new List<Vector2>();
        private readonly List<BoulderPlacement>
            generatedBoulderPlacements =
                new List<BoulderPlacement>();
        private readonly List<Vector2>
            generatedFoliageAnchors =
                new List<Vector2>();
        private readonly Dictionary<Mesh, Mesh>
            treeCollisionMeshCache =
                new Dictionary<Mesh, Mesh>();

        private RaidLayout layout;
        private Transform generatedRoot;
        private int generatedTreeCount;
        private int generatedGrassCount;
        private int[] generatedGrassVariantCounts =
            Array.Empty<int>();
        private int generatedUndergrowthCount;
        private int generatedBoulderCount;
        private int generatedTrailStoneCount;
        private int generatedBushGroupCount;
        private int generatedFlowerPatchCount;
        private int generatedBoulderGrassCount;
        private int generatedTreeBaseGrassCount;
        private int generatedPlantEdgeGrassCount;
        private int generatedTreeBaseFoliageCount;
        private int generatedBushClusterMemberCount;
        private int generatedFlowerClusterMemberCount;
        private int generatedGroundCoverPatchCount;
        private int generatedTrailTransitionGrassCount;
        private int generatedGuardGroupCount;
        private int generatedGuardPairCount;
        private int generatedBridgeCount;
        private Vector2 noiseOffsetA;
        private Vector2 noiseOffsetB;

        public bool IsGenerated => generatedRoot != null;
        public int Seed => layout != null
            ? layout.Seed
            : fallbackSeed;
        public int GeneratedTreeCount => generatedTreeCount;
        public int TreeVariantCount =>
            treePrefabs != null
                ? treePrefabs.Length
                : 0;
        public int GrassVariantCount =>
            grassPrefabs != null
                ? grassPrefabs.Length
                : 0;
        public int UndergrowthVariantCount =>
            undergrowthPrefabs != null
                ? undergrowthPrefabs.Length
                : 0;
        public int RockVariantCount =>
            rockPrefabs != null
                ? rockPrefabs.Length
                : 0;
        public int GeneratedGrassCount =>
            generatedGrassCount;
        public int GeneratedGrassVariantCount(
            int variantIndex)
        {
            return variantIndex >= 0 &&
                variantIndex <
                    generatedGrassVariantCounts.Length
                    ? generatedGrassVariantCounts[
                        variantIndex]
                    : 0;
        }
        public int GeneratedUndergrowthCount =>
            generatedUndergrowthCount;
        public int GeneratedBoulderCount =>
            generatedBoulderCount;
        public int GeneratedTrailStoneCount =>
            generatedTrailStoneCount;
        public int GeneratedBushGroupCount =>
            generatedBushGroupCount;
        public int GeneratedFlowerPatchCount =>
            generatedFlowerPatchCount;
        public int GeneratedBoulderGrassCount =>
            generatedBoulderGrassCount;
        public int GeneratedTreeBaseGrassCount =>
            generatedTreeBaseGrassCount;
        public int GeneratedPlantEdgeGrassCount =>
            generatedPlantEdgeGrassCount;
        public int GeneratedTreeBaseFoliageCount =>
            generatedTreeBaseFoliageCount;
        public int GeneratedBushClusterMemberCount =>
            generatedBushClusterMemberCount;
        public int GeneratedFlowerClusterMemberCount =>
            generatedFlowerClusterMemberCount;
        public int GeneratedGroundCoverPatchCount =>
            generatedGroundCoverPatchCount;
        public int GeneratedTrailTransitionGrassCount =>
            generatedTrailTransitionGrassCount;
        public int GeneratedGuardGroupCount =>
            generatedGuardGroupCount;
        public int GeneratedGuardPairCount =>
            generatedGuardPairCount;
        public int GeneratedBridgeCount =>
            generatedBridgeCount;
        public RaidLayout CurrentLayout => layout;
        public GameObject BridgePrefab => bridgePrefab;
        public float MapRadius => mapRadius;

        public float DistanceToNearestTrail(
            Vector3 worldPoint)
        {
            return DistanceToRoad(
                new Vector2(
                    worldPoint.x,
                    worldPoint.z));
        }

        public void Configure(
            Transform playerRoot,
            EnemyBrain[] raidEnemies,
            ExtractionZone extraction,
            GameObject[] forestTreePrefabs,
            GameObject[] forestGrassPrefabs,
            GameObject[] forestUndergrowthPrefabs,
            GameObject[] forestRockPrefabs,
            GameObject riverBridgePrefab,
            Material forestGround,
            Material dirtRoad,
            Material water,
            Material bridge,
            Material bark,
            Material birchBark,
            Material leaves,
            Material pineLeaves,
            Material grassDetails,
            Material plantDetails,
            Material rocks)
        {
            player = playerRoot;
            enemies = raidEnemies;
            extractionZone = extraction;
            treePrefabs = forestTreePrefabs;
            grassPrefabs = forestGrassPrefabs;
            undergrowthPrefabs =
                forestUndergrowthPrefabs;
            rockPrefabs = forestRockPrefabs;
            bridgePrefab = riverBridgePrefab;
            forestGroundMaterial = forestGround;
            roadMaterial = dirtRoad;
            waterMaterial = water;
            bridgeMaterial = bridge;
            treeBarkMaterial = bark;
            birchBarkMaterial = birchBark;
            treeLeavesMaterial = leaves;
            pineLeavesMaterial = pineLeaves;
            grassDetailMaterial = grassDetails;
            plantDetailMaterial = plantDetails;
            rockMaterial = rocks;
        }

        private void Start()
        {
            Generate();
        }

        private void OnEnable()
        {
            if (Application.isPlaying &&
                FindExistingGeneratedRoot() != null)
            {
                Generate();
            }
        }

        [ContextMenu("Generate Raid")]
        public void Generate()
        {
            ConfigureRaidAtmosphere();
            int seed = ResolveSeed();
            layout = CreateLayout(seed, mapRadius);
            var random = new System.Random(seed);
            noiseOffsetA = new Vector2(
                random.Next(-10000, 10001),
                random.Next(-10000, 10001));
            noiseOffsetB = new Vector2(
                random.Next(-10000, 10001),
                random.Next(-10000, 10001));

            mainRoadSamples.Clear();
            forkRoadSamples.Clear();
            branchRoadASamples.Clear();
            branchRoadBSamples.Clear();
            branchRoadCSamples.Clear();
            riverSamples.Clear();
            SampleSpline(
                layout.MainRoad,
                2,
                mainRoadSamples);
            SampleSpline(
                layout.ForkRoad,
                2,
                forkRoadSamples);
            SampleSpline(
                layout.BranchRoadA,
                2,
                branchRoadASamples);
            SampleSpline(
                layout.BranchRoadB,
                2,
                branchRoadBSamples);
            SampleSpline(
                layout.BranchRoadC,
                2,
                branchRoadCSamples);
            SampleSpline(
                layout.River,
                3,
                riverSamples);

            if (generatedRoot == null)
            {
                generatedRoot =
                    FindExistingGeneratedRoot();
            }
            if (generatedRoot != null)
            {
                Destroy(generatedRoot.gameObject);
            }

            generatedRoot =
                new GameObject(
                    $"Generated Raid {seed}").transform;
            generatedRoot.SetParent(transform, false);

            Material forestRuntime =
                CreateTexturedMaterial(
                    forestGroundMaterial,
                    seed ^ 0x13579,
                    new Color(0.18f, 0.22f, 0.145f),
                    new Color(0.285f, 0.265f, 0.17f),
                    7f,
                    true);
            Material roadRuntime =
                CreateTexturedMaterial(
                    roadMaterial,
                    seed ^ 0x24680,
                    new Color(0.30f, 0.20f, 0.105f),
                    new Color(0.47f, 0.34f, 0.19f),
                    5f,
                    true);
            Material waterRuntime =
                CreateRiverMaterial(
                    waterMaterial,
                    seed ^ 0x55aa55);

            CreateTerrain(
                forestRuntime,
                roadRuntime);

            CreateRibbon(
                "River",
                riverSamples,
                riverHalfWidth +
                    RiverWaterBankOverlap,
                waterRuntime,
                false);
            CreateBridges();

            CreateForest(random);
            CreateGroundScenery(random);
            PlaceActorsAndObjectives(random);
            GameplayEventLog.Publish(
                "raid-generated",
                gameObject,
                $"seed={seed}; trees={generatedTreeCount}; " +
                $"grass={generatedGrassCount}; " +
                $"undergrowth={generatedUndergrowthCount}; " +
                $"boulders={generatedBoulderCount}; " +
                $"trailStones={generatedTrailStoneCount}; " +
                $"guardGroups={generatedGuardGroupCount}; " +
                $"guardPairs={generatedGuardPairCount}; " +
                $"fork={layout.HasRoadFork}; " +
                $"crossing={layout.RiverCrossesRoad}");
        }

        private Transform FindExistingGeneratedRoot()
        {
            for (int childIndex = 0;
                 childIndex < transform.childCount;
                 childIndex++)
            {
                Transform child =
                    transform.GetChild(childIndex);
                if (child.name.StartsWith(
                        "Generated Raid ",
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }
            return null;
        }

        public static RaidLayout CreateLayout(
            int seed,
            float radius)
        {
            var random = new System.Random(seed);
            float mainAngle =
                Mathf.Lerp(
                    0f,
                    Mathf.PI * 2f,
                    (float)random.NextDouble());
            Vector3[] road = CreateBoundaryRoad(
                random,
                radius,
                mainAngle,
                25,
                0f);

            bool hasSecondPrimary =
                random.NextDouble() < 0.68;
            Vector3[] fork = hasSecondPrimary
                ? CreateBoundaryRoad(
                    random,
                    radius,
                    mainAngle +
                        Mathf.Lerp(
                            0.95f,
                            1.78f,
                            (float)random.NextDouble()),
                    23,
                    Mathf.Lerp(
                        -radius * 0.12f,
                        radius * 0.12f,
                        (float)random.NextDouble()))
                : Array.Empty<Vector3>();

            Vector3[] branchA = CreateBranchRoad(
                random,
                radius,
                road,
                Mathf.Lerp(
                    0.26f,
                    0.43f,
                    (float)random.NextDouble()),
                random.NextDouble() < 0.5 ? -1f : 1f);
            Vector3[] branchB =
                random.NextDouble() < 0.78
                    ? CreateBranchRoad(
                        random,
                        radius,
                        hasSecondPrimary ? fork : road,
                        Mathf.Lerp(
                            0.52f,
                            0.72f,
                            (float)random.NextDouble()),
                        random.NextDouble() < 0.5 ? -1f : 1f)
                    : Array.Empty<Vector3>();
            Vector3[] branchC =
                random.NextDouble() < 0.38
                    ? CreateBranchRoad(
                        random,
                        radius,
                        road,
                        Mathf.Lerp(
                            0.58f,
                            0.78f,
                            (float)random.NextDouble()),
                        random.NextDouble() < 0.5 ? -1f : 1f)
                    : Array.Empty<Vector3>();

            Vector3[] river = CreateBoundaryRiver(
                random,
                radius,
                mainAngle +
                    Mathf.PI * 0.5f +
                    Mathf.Lerp(
                        -0.22f,
                        0.22f,
                        (float)random.NextDouble()));

            float playerSpawnT =
                Mathf.Lerp(
                    0.085f,
                    0.135f,
                    (float)random.NextDouble());
            float extractionT =
                Mathf.Lerp(
                    0.875f,
                    0.925f,
                    (float)random.NextDouble());

            return new RaidLayout
            {
                Seed = seed,
                HasRoadFork = true,
                RiverCrossesRoad = true,
                MainRoad = road,
                ForkRoad = fork,
                BranchRoadA = branchA,
                BranchRoadB = branchB,
                BranchRoadC = branchC,
                River = river,
                PlayerSpawnRoadT = playerSpawnT,
                ExtractionRoadT = extractionT
            };
        }

        private static Vector3[] CreateBoundaryRoad(
            System.Random random,
            float radius,
            float angle,
            int pointCount,
            float centerOffset)
        {
            var points = new Vector3[pointCount];
            Vector3 forward = new Vector3(
                Mathf.Sin(angle),
                0f,
                Mathf.Cos(angle));
            Vector3 right = new Vector3(
                forward.z,
                0f,
                -forward.x);
            float edge = Mathf.Max(1f, radius - 2f);
            float phaseA =
                Mathf.Lerp(
                    0f,
                    Mathf.PI * 2f,
                    (float)random.NextDouble());
            float phaseB =
                Mathf.Lerp(
                    0f,
                    Mathf.PI * 2f,
                    (float)random.NextDouble());
            for (int index = 0; index < pointCount; index++)
            {
                float t = index / (pointCount - 1f);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float lateral =
                    centerOffset * envelope +
                    Mathf.Sin(
                        t * Mathf.PI * 2.2f + phaseA) *
                    radius * 0.055f * envelope +
                    Mathf.Sin(
                        t * Mathf.PI * 5.1f + phaseB) *
                    radius * 0.018f * envelope;
                points[index] =
                    forward * Mathf.Lerp(-edge, edge, t) +
                    right * lateral;
            }
            return points;
        }

        private static Vector3[] CreateBranchRoad(
            System.Random random,
            float radius,
            Vector3[] sourceRoad,
            float sourceT,
            float side)
        {
            const int PointCount = 13;
            var points = new Vector3[PointCount];
            Vector3 start = PointOnPolyline(
                sourceRoad,
                sourceT);
            float startAngle = Mathf.Atan2(
                start.x,
                start.z);
            float exitAngle =
                startAngle +
                side *
                Mathf.Lerp(
                    0.72f,
                    1.35f,
                    (float)random.NextDouble());
            Vector3 end = new Vector3(
                Mathf.Sin(exitAngle) * (radius - 2f),
                0f,
                Mathf.Cos(exitAngle) * (radius - 2f));
            Vector3 midpointDirection =
                (start + end).sqrMagnitude > 0.001f
                    ? (start + end).normalized
                    : new Vector3(
                        Mathf.Sin(exitAngle),
                        0f,
                        Mathf.Cos(exitAngle));
            Vector3 control =
                midpointDirection * radius * 0.58f +
                Vector3.Cross(
                    Vector3.up,
                    midpointDirection) *
                side *
                radius * 0.08f;
            for (int index = 0; index < PointCount; index++)
            {
                float t = index / (PointCount - 1f);
                float inverse = 1f - t;
                points[index] =
                    inverse * inverse * start +
                    2f * inverse * t * control +
                    t * t * end;
            }
            return points;
        }

        private static Vector3[] CreateBoundaryRiver(
            System.Random random,
            float radius,
            float angle)
        {
            const int PointCount = 31;
            var points = new Vector3[PointCount];
            Vector3 forward = new Vector3(
                Mathf.Sin(angle),
                0f,
                Mathf.Cos(angle));
            Vector3 right = new Vector3(
                forward.z,
                0f,
                -forward.x);
            float centerOffset = Mathf.Lerp(
                -radius * 0.09f,
                radius * 0.09f,
                (float)random.NextDouble());
            float edge = Mathf.Sqrt(
                Mathf.Max(
                    1f,
                    (radius - 1.5f) * (radius - 1.5f) -
                    centerOffset * centerOffset));
            float phase = Mathf.Lerp(
                0f,
                Mathf.PI * 2f,
                (float)random.NextDouble());
            for (int index = 0; index < PointCount; index++)
            {
                float t = index / (PointCount - 1f);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float meander =
                    Mathf.Sin(
                        t * Mathf.PI * 3.25f + phase) *
                    radius * 0.085f * envelope +
                    Mathf.Sin(
                        t * Mathf.PI * 7.1f + phase * 0.47f) *
                    radius * 0.026f * envelope;
                points[index] =
                    forward * Mathf.Lerp(-edge, edge, t) +
                    right * (centerOffset + meander);
            }
            return points;
        }

        private static Vector3 PointOnPolyline(
            Vector3[] points,
            float t)
        {
            if (points == null || points.Length == 0)
            {
                return Vector3.zero;
            }
            float scaled =
                Mathf.Clamp01(t) * (points.Length - 1);
            int first = Mathf.FloorToInt(scaled);
            int second = Mathf.Min(points.Length - 1, first + 1);
            return Vector3.Lerp(
                points[first],
                points[second],
                scaled - first);
        }

        private int ResolveSeed()
        {
            GameplayLoopBootstrap bootstrap =
                GameplaySceneRuntime.ResolveBootstrap();
            GameSession session =
                bootstrap != null
                    ? bootstrap.Session
                    : null;
            if (session == null)
            {
                return fallbackSeed;
            }

            if (!session.HasActiveRaid)
            {
                try
                {
                    session.BeginRaid();
                }
                catch (InvalidOperationException)
                {
                    return fallbackSeed;
                }
            }

            return session.ActiveRaid != null &&
                session.ActiveRaid.LaunchRequest != null
                    ? session.ActiveRaid.LaunchRequest.Seed
                    : fallbackSeed;
        }

        private void CreateTerrain(
            Material groundMaterial,
            Material dirtRoadMaterial)
        {
            int width = terrainResolution + 1;
            var vertices =
                new Vector3[width * width];
            var uv =
                new Vector2[vertices.Length];
            var roadField =
                new float[vertices.Length];
            float diameter = mapRadius * 2f;
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float worldX =
                        -mapRadius +
                        diameter * x / terrainResolution;
                    float worldZ =
                        -mapRadius +
                        diameter * z / terrainResolution;
                    int index = z * width + x;
                    vertices[index] =
                        new Vector3(
                            worldX,
                            TerrainHeight(worldX, worldZ),
                            worldZ);
                    uv[index] =
                        new Vector2(
                            worldX / 12f,
                            worldZ / 12f);
                    roadField[index] =
                        SignedDistanceToRoad(
                            new Vector2(
                                worldX,
                                worldZ));
                }
            }

            var meshVertices =
                new List<Vector3>(vertices);
            var meshUv =
                new List<Vector2>(uv);
            var groundTriangles =
                new List<int>(
                    terrainResolution *
                    terrainResolution *
                    6);
            var roadTriangles =
                new List<int>(
                    terrainResolution *
                    terrainResolution);
            var boundaryVertices =
                new Dictionary<long, int>();
            for (int z = 0;
                 z < terrainResolution;
                 z++)
            {
                for (int x = 0;
                     x < terrainResolution;
                     x++)
                {
                    float centerX =
                        -mapRadius +
                        diameter *
                        (x + 0.5f) /
                        terrainResolution;
                    float centerZ =
                        -mapRadius +
                        diameter *
                        (z + 0.5f) /
                        terrainResolution;
                    if (centerX * centerX +
                        centerZ * centerZ >
                        mapRadius * mapRadius)
                    {
                        continue;
                    }

                    int a = z * width + x;
                    int b = a + 1;
                    int c = a + width;
                    int d = c + 1;
                    AppendTerrainTriangle(
                        a,
                        c,
                        d,
                        roadField,
                        meshVertices,
                        meshUv,
                        boundaryVertices,
                        groundTriangles,
                        roadTriangles);
                    AppendTerrainTriangle(
                        a,
                        d,
                        b,
                        roadField,
                        meshVertices,
                        meshUv,
                        boundaryVertices,
                        groundTriangles,
                        roadTriangles);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Procedural Raid Disc"
            };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(meshVertices);
            mesh.SetUVs(0, meshUv);
            var terrainColors =
                new List<Color>(
                    meshVertices.Count);
            for (int vertexIndex = 0;
                 vertexIndex < meshVertices.Count;
                 vertexIndex++)
            {
                Vector3 vertex =
                    meshVertices[vertexIndex];
                terrainColors.Add(
                    TerrainBlendTintAt(
                        vertex.x,
                        vertex.z));
            }
            mesh.SetColors(terrainColors);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(
                groundTriangles,
                0);
            mesh.SetTriangles(
                roadTriangles,
                1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject terrain =
                new GameObject("Terrain Disc");
            terrain.transform.SetParent(
                generatedRoot,
                false);
            terrain.AddComponent<MeshFilter>()
                .sharedMesh = mesh;
            Material terrainBlend =
                CreateTerrainBlendMaterial(
                    groundMaterial,
                    dirtRoadMaterial);
            terrain.AddComponent<MeshRenderer>()
                .sharedMaterials =
                new[]
                {
                    terrainBlend,
                    terrainBlend
                };
            terrain.AddComponent<MeshCollider>()
                .sharedMesh = mesh;
        }

        private static void AppendTerrainTriangle(
            int a,
            int b,
            int c,
            float[] roadField,
            List<Vector3> vertices,
            List<Vector2> uv,
            Dictionary<long, int> boundaryVertices,
            List<int> groundTriangles,
            List<int> roadTriangles)
        {
            bool aIsRoad = roadField[a] <= 0f;
            bool bIsRoad = roadField[b] <= 0f;
            bool cIsRoad = roadField[c] <= 0f;
            if (aIsRoad && bIsRoad && cIsRoad)
            {
                AddTriangle(
                    roadTriangles,
                    a,
                    b,
                    c);
                return;
            }

            if (!aIsRoad && !bIsRoad && !cIsRoad)
            {
                AddTriangle(
                    groundTriangles,
                    a,
                    b,
                    c);
                return;
            }

            int[] source = { a, b, c };
            AppendClippedTriangle(
                source,
                true,
                roadField,
                vertices,
                uv,
                boundaryVertices,
                roadTriangles);
            AppendClippedTriangle(
                source,
                false,
                roadField,
                vertices,
                uv,
                boundaryVertices,
                groundTriangles);
        }

        private static void AppendClippedTriangle(
            int[] source,
            bool keepRoad,
            float[] roadField,
            List<Vector3> vertices,
            List<Vector2> uv,
            Dictionary<long, int> boundaryVertices,
            List<int> triangles)
        {
            var polygon = new List<int>(4);
            int previous = source[source.Length - 1];
            bool previousInside =
                IsInsideRoadContour(
                    roadField[previous],
                    keepRoad);
            for (int index = 0;
                 index < source.Length;
                 index++)
            {
                int current = source[index];
                bool currentInside =
                    IsInsideRoadContour(
                        roadField[current],
                        keepRoad);
                if (currentInside != previousInside)
                {
                    polygon.Add(
                        GetBoundaryVertex(
                            previous,
                            current,
                            roadField,
                            vertices,
                            uv,
                            boundaryVertices));
                }
                if (currentInside)
                {
                    polygon.Add(current);
                }
                previous = current;
                previousInside = currentInside;
            }

            for (int index = 1;
                 index < polygon.Count - 1;
                 index++)
            {
                AddTriangle(
                    triangles,
                    polygon[0],
                    polygon[index],
                    polygon[index + 1]);
            }
        }

        private static bool IsInsideRoadContour(
            float field,
            bool keepRoad)
        {
            return keepRoad
                ? field <= 0f
                : field >= 0f;
        }

        private static int GetBoundaryVertex(
            int a,
            int b,
            float[] roadField,
            List<Vector3> vertices,
            List<Vector2> uv,
            Dictionary<long, int> boundaryVertices)
        {
            int minimum = Mathf.Min(a, b);
            int maximum = Mathf.Max(a, b);
            long key =
                ((long)minimum << 32) |
                (uint)maximum;
            if (boundaryVertices.TryGetValue(
                    key,
                    out int existing))
            {
                return existing;
            }

            float aField = roadField[a];
            float bField = roadField[b];
            float denominator =
                aField - bField;
            float t =
                Mathf.Abs(denominator) > 0.000001f
                    ? Mathf.Clamp01(
                        aField / denominator)
                    : 0.5f;
            int created = vertices.Count;
            vertices.Add(
                Vector3.Lerp(
                    vertices[a],
                    vertices[b],
                    t));
            uv.Add(
                Vector2.Lerp(
                    uv[a],
                    uv[b],
                    t));
            boundaryVertices.Add(
                key,
                created);
            return created;
        }

        private static void AddTriangle(
            List<int> triangles,
            int a,
            int b,
            int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private void CreateRibbon(
            string name,
            List<Vector3> points,
            float halfWidth,
            Material material,
            bool road)
        {
            if (points.Count < 2)
            {
                return;
            }

            int widthSegments = road ? 1 : 8;
            int verticesAcross = widthSegments + 1;
            var vertices =
                new Vector3[
                    points.Count * verticesAcross];
            var uv =
                new Vector2[vertices.Length];
            var flowData =
                new Vector2[vertices.Length];
            var triangles =
                new int[
                    (points.Count - 1) *
                    widthSegments * 6];
            float distanceAlong = 0f;
            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                if (index > 0)
                {
                    distanceAlong += Vector3.Distance(
                        points[index - 1],
                        points[index]);
                }
                Vector3 previous =
                    points[Mathf.Max(0, index - 1)];
                Vector3 next =
                    points[
                        Mathf.Min(
                            points.Count - 1,
                            index + 1)];
                Vector3 forward =
                    Vector3.ProjectOnPlane(
                        next - previous,
                        Vector3.up).normalized;
                Vector3 right =
                    Vector3.Cross(
                        Vector3.up,
                        forward);
                Vector3 center = points[index];
                Vector3 incoming =
                    index > 0
                        ? Vector3.ProjectOnPlane(
                            center - points[index - 1],
                            Vector3.up).normalized
                        : forward;
                Vector3 outgoing =
                    index < points.Count - 1
                        ? Vector3.ProjectOnPlane(
                            points[index + 1] - center,
                            Vector3.up).normalized
                        : forward;
                float curvature =
                    road
                        ? 0f
                        : Mathf.Clamp(
                            Vector3.SignedAngle(
                                incoming,
                                outgoing,
                                Vector3.up) /
                            18f,
                            -1f,
                            1f);
                float curveSpeed =
                    1f;
                float y =
                    road
                        ? RoadSurfaceHeight(
                            center.x,
                            center.z) +
                            0.10f
                        : WaterHeight(
                            center.x,
                            center.z);
                center.y = y;
                float localHalfWidth = halfWidth;
                if (!road)
                {
                    float widthNoise =
                        Mathf.PerlinNoise(
                            noiseOffsetA.x * 0.007f +
                            center.x * 0.045f,
                            noiseOffsetA.y * 0.007f +
                            center.z * 0.045f);
                    localHalfWidth *=
                        Mathf.Lerp(
                            0.94f,
                            1.08f,
                            widthNoise);
                }
                for (int crossIndex = 0;
                     crossIndex < verticesAcross;
                     crossIndex++)
                {
                    float crossT =
                        crossIndex /
                        (float)widthSegments;
                    int vertex =
                        index * verticesAcross +
                        crossIndex;
                    vertices[vertex] =
                        center +
                        right *
                        Mathf.Lerp(
                            -localHalfWidth,
                            localHalfWidth,
                            crossT);
                    uv[vertex] =
                        new Vector2(
                            crossT,
                            distanceAlong / 5f);
                    flowData[vertex] =
                        new Vector2(
                            curvature,
                            curveSpeed);
                }
            }

            for (int index = 0;
                 index < points.Count - 1;
                 index++)
            {
                for (int crossIndex = 0;
                     crossIndex < widthSegments;
                     crossIndex++)
                {
                    int triangle =
                        (index * widthSegments +
                         crossIndex) * 6;
                    int vertex =
                        index * verticesAcross +
                        crossIndex;
                    int nextRow =
                        vertex + verticesAcross;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = nextRow;
                    triangles[triangle + 2] = nextRow + 1;
                    triangles[triangle + 3] = vertex;
                    triangles[triangle + 4] = nextRow + 1;
                    triangles[triangle + 5] = vertex + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                name = name
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.uv2 = flowData;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject ribbon =
                new GameObject(name);
            ribbon.transform.SetParent(
                generatedRoot,
                false);
            ribbon.AddComponent<MeshFilter>()
                .sharedMesh = mesh;
            ribbon.AddComponent<MeshRenderer>()
                .sharedMaterial = material;
            if (road)
            {
                ribbon.AddComponent<MeshCollider>()
                    .sharedMesh = mesh;
            }
        }

        private void CreateBridges()
        {
            generatedBridgeCount = 0;
            var bridgePoints = new List<Vector3>();
            var crossings = new List<TrailRiverCrossing>();
            foreach (List<Vector3> road in AllRoads())
            {
                crossings.Clear();
                FindPolylineIntersections(
                    road,
                    riverSamples,
                    crossings);
                for (int crossingIndex = 0;
                     crossingIndex < crossings.Count;
                     crossingIndex++)
                {
                    TrailRiverCrossing crossing =
                        crossings[crossingIndex];
                    bool duplicate = false;
                    for (int index = 0;
                         index < bridgePoints.Count;
                         index++)
                    {
                        if (Vector3.Distance(
                                bridgePoints[index],
                                crossing.Point) <
                            riverHalfWidth * 2.5f)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (duplicate)
                    {
                        continue;
                    }

                    bridgePoints.Add(crossing.Point);
                    CreateBridgeAt(
                        crossing.Point,
                        crossing.RoadDirection,
                        bridgePoints.Count);
                }
            }

            if (bridgePoints.Count == 0 &&
                TryFindClosestPair(
                         mainRoadSamples,
                         riverSamples,
                         out int roadIndex,
                         out int riverIndex))
            {
                Vector3 point = (mainRoadSamples[roadIndex] +
                    riverSamples[riverIndex]) * 0.5f;
                Vector3 previous =
                    mainRoadSamples[Mathf.Max(0, roadIndex - 1)];
                Vector3 next =
                    mainRoadSamples[Mathf.Min(
                        mainRoadSamples.Count - 1,
                        roadIndex + 1)];
                Vector3 direction = Vector3.ProjectOnPlane(
                    next - previous,
                    Vector3.up).normalized;
                CreateBridgeAt(point, direction, 1);
            }
        }

        private void CreateBridgeAt(
            Vector3 point,
            Vector3 direction,
            int bridgeNumber)
        {
            generatedBridgeCount++;
            GameObject bridge = bridgePrefab != null
                ? Instantiate(bridgePrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            bridge.name = bridgeNumber == 1
                ? "Road Bridge"
                : $"Road Bridge {bridgeNumber}";
            bridge.transform.SetParent(
                generatedRoot,
                false);
            if (bridgePrefab == null)
            {
                ConfigureFallbackBridge(
                    bridge,
                    point,
                    direction);
                return;
            }

            ConfigureImportedBridge(
                bridge,
                point,
                direction);
        }

        private void ConfigureFallbackBridge(
            GameObject bridge,
            Vector3 point,
            Vector3 direction)
        {
            float deckHeight = BridgeDeckHeight(point.x, point.z);
            bridge.transform.position =
                new Vector3(point.x, deckHeight - 0.17f, point.z);
            bridge.transform.rotation = Quaternion.LookRotation(
                direction,
                Vector3.up);
            bridge.transform.localScale = new Vector3(
                roadHalfWidth * 2f + 1.1f,
                0.34f,
                riverHalfWidth * 2.8f + 4f);
            Renderer renderer = bridge.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = bridgeMaterial;
            }
        }

        private void ConfigureImportedBridge(
            GameObject bridge,
            Vector3 point,
            Vector3 direction)
        {
            bridge.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            bridge.transform.localScale = Vector3.one;
            Renderer[] renderers =
                bridge.GetComponentsInChildren<Renderer>(true);
            if (!TryGetRendererBounds(renderers, out Bounds sourceBounds))
            {
                ConfigureFallbackBridge(bridge, point, direction);
                return;
            }

            bool lengthUsesX =
                sourceBounds.size.x >= sourceBounds.size.z;
            float sourceLength = Mathf.Max(
                0.01f,
                lengthUsesX
                    ? sourceBounds.size.x
                    : sourceBounds.size.z);
            float targetLength =
                (riverHalfWidth + 2.2f) * 2f;
            float lengthScale = targetLength / sourceLength;
            float heightScale =
                lengthScale * BridgeCrossSectionScale;
            float widthScale =
                heightScale * BridgeExtraWidthScale;
            bridge.transform.localScale = lengthUsesX
                ? new Vector3(lengthScale, widthScale, heightScale)
                : new Vector3(heightScale, widthScale, lengthScale);

            ResolveBridgeBankFit(
                point,
                direction,
                targetLength,
                out Vector3 fittedDirection,
                out float deckHeight);

            Quaternion pathAxisCorrection = lengthUsesX
                ? Quaternion.Euler(0f, -90f, 0f)
                : Quaternion.identity;
            Quaternion deckUprightCorrection = lengthUsesX
                ? Quaternion.Euler(-90f, 0f, 0f)
                : Quaternion.Euler(0f, 0f, 90f);
            bridge.transform.rotation =
                Quaternion.LookRotation(fittedDirection, Vector3.up) *
                pathAxisCorrection *
                deckUprightCorrection;

            bridge.transform.position = new Vector3(
                point.x,
                0f,
                point.z);

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null && bridgeMaterial != null)
                {
                    Material[] materials =
                        renderers[index].sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex < materials.Length;
                         materialIndex++)
                    {
                        materials[materialIndex] = bridgeMaterial;
                    }
                    renderers[index].sharedMaterials = materials;
                }
            }
            ConfigureBridgeColliders(bridge);
            SetStaticRecursively(bridge.transform);
            Physics.SyncTransforms();
            if (TryFindBridgeDeckHeight(
                    bridge,
                    point,
                    out float currentDeckHeight))
            {
                bridge.transform.position += Vector3.up *
                    (deckHeight - currentDeckHeight);
            }
            else if (TryGetRendererBounds(
                         renderers,
                         out Bounds placedBounds))
            {
                float estimatedDeckHeight =
                    placedBounds.min.y +
                    placedBounds.size.y * 0.18f;
                bridge.transform.position += Vector3.up *
                    (deckHeight - estimatedDeckHeight);
            }
        }

        private static bool TryFindBridgeDeckHeight(
            GameObject bridge,
            Vector3 center,
            out float deckHeight)
        {
            deckHeight = 0f;
            RaycastHit[] hits = Physics.RaycastAll(
                new Vector3(center.x, 50f, center.z),
                Vector3.down,
                100f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float highest = float.NegativeInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider != null &&
                    collider.transform.IsChildOf(bridge.transform))
                {
                    highest = Mathf.Max(highest, hits[index].point.y);
                }
            }
            if (float.IsNegativeInfinity(highest))
            {
                return false;
            }
            deckHeight = highest;
            return true;
        }

        private static void ConfigureBridgeColliders(GameObject bridge)
        {
            Collider[] importedColliders =
                bridge.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < importedColliders.Length; index++)
            {
                Collider collider = importedColliders[index];
                if (collider != null)
                {
                    collider.enabled = false;
                    Destroy(collider);
                }
            }

            MeshFilter[] filters =
                bridge.GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }
                MeshCollider collider =
                    filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
        }

        private static void SetStaticRecursively(Transform root)
        {
            root.gameObject.isStatic = true;
            for (int index = 0; index < root.childCount; index++)
            {
                SetStaticRecursively(root.GetChild(index));
            }
        }

        private float BridgeDeckHeight(float x, float z)
        {
            return RawLandHeight(x, z) - RoadIndentation + 0.02f;
        }

        private void ResolveBridgeBankFit(
            Vector3 center,
            Vector3 horizontalDirection,
            float span,
            out Vector3 fittedDirection,
            out float centerDeckHeight)
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(
                horizontalDirection,
                Vector3.up).normalized;
            float halfSpan = span * 0.5f;
            Vector3 nearBank = center - flatDirection * halfSpan;
            Vector3 farBank = center + flatDirection * halfSpan;
            float nearHeight = BridgeDeckHeight(
                nearBank.x,
                nearBank.z);
            float farHeight = BridgeDeckHeight(
                farBank.x,
                farBank.z);
            centerDeckHeight =
                (nearHeight + farHeight) * 0.5f +
                BridgeDeckLift;
            fittedDirection = new Vector3(
                flatDirection.x * span,
                farHeight - nearHeight,
                flatDirection.z * span).normalized;
        }

        private void CreateForest(System.Random random)
        {
            generatedTreeCount = 0;
            generatedTreePositions.Clear();
            var validTreePrefabs =
                new List<GameObject>();
            if (treePrefabs != null)
            {
                for (int index = 0;
                     index < treePrefabs.Length;
                     index++)
                {
                    if (treePrefabs[index] != null)
                    {
                        validTreePrefabs.Add(
                            treePrefabs[index]);
                    }
                }
            }
            if (validTreePrefabs.Count == 0)
            {
                return;
            }

            Transform forest =
                new GameObject("Dense Stylized Forest")
                    .transform;
            forest.SetParent(generatedRoot, false);
            int attempts = treeCount * 12;
            float minimumSpacing = 2.15f;
            for (int attempt = 0;
                 attempt < attempts &&
                 generatedTreeCount < treeCount;
                 attempt++)
            {
                float angle =
                    (float)random.NextDouble() *
                    Mathf.PI *
                    2f;
                float radius =
                    Mathf.Sqrt(
                        (float)random.NextDouble()) *
                    (mapRadius - 3f);
                Vector2 point =
                    new Vector2(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius);
                if (DistanceToRoad(point) <
                        treeClearance ||
                    DistanceToPolyline(
                        point,
                        riverSamples) <
                        riverHalfWidth + 2f ||
                    Vector2.Distance(
                        point,
                        ToXZ(layout.PlayerStart)) <
                        7.5f ||
                    Vector2.Distance(
                        point,
                        ToXZ(layout.Extraction)) <
                        7.5f ||
                    HasNearbyTree(
                        generatedTreePositions,
                        point,
                        minimumSpacing))
                {
                    continue;
                }

                int variantIndex =
                    generatedTreeCount <
                        validTreePrefabs.Count
                        ? (generatedTreeCount +
                            Mathf.Abs(Seed)) %
                            validTreePrefabs.Count
                        : random.Next(
                            validTreePrefabs.Count);
                GameObject selectedPrefab =
                    validTreePrefabs[variantIndex];
                GameObject tree =
                    Instantiate(
                        selectedPrefab,
                        forest);
                RemoveAllColliders(tree);
                tree.name =
                    $"{selectedPrefab.name} " +
                    $"{generatedTreeCount + 1:000}";
                float targetHeight =
                    Mathf.Lerp(
                        14.4f,
                        21f,
                        (float)random.NextDouble());
                float terrainHeight =
                    TerrainHeight(
                        point.x,
                        point.y);
                tree.transform.position =
                    new Vector3(
                        point.x,
                        terrainHeight,
                        point.y);
                Quaternion importedRotation =
                    tree.transform.rotation;
                tree.transform.rotation =
                    Quaternion.AngleAxis(
                        (float)random.NextDouble() * 360f,
                        Vector3.up) *
                    importedRotation;
                Renderer[] importedRenderers =
                    tree.GetComponentsInChildren<
                        Renderer>(true);
                var visibleRenderers =
                    new List<Renderer>(
                        importedRenderers.Length);
                for (int index = 0;
                     index < importedRenderers.Length;
                     index++)
                {
                    Renderer renderer =
                        importedRenderers[index];
                    if (IsCollisionHelper(renderer))
                    {
                        renderer.enabled = false;
                        continue;
                    }

                    visibleRenderers.Add(renderer);
                    Material[] materials =
                        renderer.sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex <
                            materials.Length;
                         materialIndex++)
                    {
                        materials[materialIndex] =
                            ResolveTreeMaterial(
                                materials[materialIndex],
                                selectedPrefab.name);
                    }
                    renderer.sharedMaterials =
                        materials;
                    renderer.shadowCastingMode =
                        ShadowCastingMode.On;
                }
                Renderer[] renderers =
                    visibleRenderers.ToArray();

                if (TryGetRendererBounds(
                        renderers,
                        out Bounds importedBounds) &&
                    importedBounds.size.y > 0.001f)
                {
                    float normalizedScale =
                        targetHeight /
                        importedBounds.size.y;
                    tree.transform.localScale *=
                        normalizedScale;
                    TryGetRendererBounds(
                        renderers,
                        out Bounds scaledBounds);
                    float groundedBaseHeight =
                        MinimumTerrainHeightUnderFootprint(
                            point,
                            0.62f,
                            0.62f,
                            16) -
                        0.018f;
                    tree.transform.position +=
                        Vector3.up *
                        (groundedBaseHeight -
                         scaledBounds.min.y);
                }

                AddExactTreeWoodColliders(
                    tree,
                    renderers);
                generatedTreePositions.Add(point);
            generatedTreeCount++;
            }
        }

        private void CreateGroundScenery(
            System.Random random)
        {
            generatedGrassCount = 0;
            generatedGrassVariantCounts =
                grassPrefabs != null
                    ? new int[grassPrefabs.Length]
                    : Array.Empty<int>();
            generatedUndergrowthCount = 0;
            generatedBoulderCount = 0;
            generatedTrailStoneCount = 0;
            generatedBushGroupCount = 0;
            generatedFlowerPatchCount = 0;
            generatedBoulderGrassCount = 0;
            generatedTreeBaseGrassCount = 0;
            generatedPlantEdgeGrassCount = 0;
            generatedTreeBaseFoliageCount = 0;
            generatedBushClusterMemberCount = 0;
            generatedFlowerClusterMemberCount = 0;
            generatedGroundCoverPatchCount = 0;
            generatedTrailTransitionGrassCount = 0;
            generatedBoulderPlacements.Clear();
            generatedFoliageAnchors.Clear();

            List<GameObject> grasses =
                CollectValidPrefabs(grassPrefabs);
            List<GameObject> undergrowth =
                CollectValidPrefabs(
                    undergrowthPrefabs);
            List<GameObject> rocks =
                CollectValidPrefabs(rockPrefabs);

            if (rocks.Count > 0)
            {
                CreateBoulders(
                    random,
                    rocks);
            }
            if (undergrowth.Count > 0)
            {
                CreateUndergrowth(
                    random,
                    undergrowth);
            }
            if (grasses.Count > 0)
            {
                CreateGrassCoverage(
                    random,
                    grasses);
            }
            if (rocks.Count > 0)
            {
                CreateTrailStones(
                    random,
                    rocks);
            }
        }

        private void CreateGrassCoverage(
            System.Random random,
            List<GameObject> prefabs)
        {
            Transform root =
                new GameObject(
                    "Batched Meadow Grass").transform;
            root.SetParent(generatedRoot, false);
            List<GrassMeshSource> sources =
                BuildGrassMeshSources(prefabs);
            if (sources.Count == 0)
            {
                return;
            }

            var batch =
                new List<CombineInstance>(
                    GrassPlacementsPerBatch * 2);
            int placementsInBatch = 0;
            int batchIndex = 0;
            int batchStart = 1;
            int generatedBaseGrassCount = 0;
            float usableRadius = mapRadius - 2.5f;
            float cellSpacing =
                Mathf.Sqrt(
                    Mathf.PI * usableRadius *
                    usableRadius /
                    (grassCount * 1.30f));
            int cellsAcross =
                Mathf.CeilToInt(
                    usableRadius * 2f /
                    cellSpacing);
            float gridStart =
                -usableRadius + cellSpacing * 0.5f;
            for (int row = 0;
                 row < cellsAcross &&
                 generatedBaseGrassCount < grassCount;
                 row++)
            {
                float stagger =
                    (row & 1) == 0
                        ? 0f
                        : cellSpacing * 0.5f;
                for (int column = 0;
                     column < cellsAcross &&
                     generatedBaseGrassCount < grassCount;
                     column++)
                {
                    Vector2 jitter =
                        new Vector2(
                            ((float)random.NextDouble() -
                             0.5f) * cellSpacing * 0.42f,
                            ((float)random.NextDouble() -
                             0.5f) * cellSpacing * 0.42f);
                    Vector2 point =
                        new Vector2(
                            gridStart +
                            column * cellSpacing +
                            stagger,
                            gridStart +
                            row * cellSpacing) +
                        jitter;
                    if (point.sqrMagnitude >
                        usableRadius * usableRadius)
                    {
                        continue;
                    }
                    float signedRoadDistance =
                        SignedDistanceToRoad(point);
                    if (signedRoadDistance <
                        GrassRoadInteriorLimit ||
                        DistanceToPolyline(
                            point,
                            riverSamples) <
                        riverHalfWidth +
                        GrassRiverClearance)
                    {
                        continue;
                    }

                    float barePatch =
                        Mathf.PerlinNoise(
                            noiseOffsetB.x * 0.019f +
                            point.x * 0.095f +
                            31.7f,
                            noiseOffsetB.y * 0.019f +
                            point.y * 0.095f +
                            17.3f);
                    float lowerZone =
                        Mathf.InverseLerp(
                            3.5f,
                            -3.5f,
                            RawLandHeight(
                                point.x,
                                point.y));
                    float barePatchStrength =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(
                                0.67f,
                                0.79f,
                                barePatch));
                    float density =
                        Mathf.Clamp01(
                            Mathf.Lerp(
                                0.975f,
                                0.035f,
                                barePatchStrength) +
                            lowerZone * 0.015f);
                    float roadBlend =
                        RoadSurfaceBlendAt(
                            point.x,
                            point.y);
                    density *=
                        Mathf.Lerp(
                            1f,
                            0.11f,
                            Mathf.Pow(
                                roadBlend,
                                1.25f));
                    if ((float)random.NextDouble() >
                        density)
                    {
                        continue;
                    }

                    float heightPatch =
                        Mathf.PerlinNoise(
                            noiseOffsetB.x * 0.011f +
                            point.x * 0.034f,
                            noiseOffsetB.y * 0.011f +
                            point.y * 0.034f);
                    int spikyVariantCount =
                        Mathf.Max(0, sources.Count - 1);
                    float grassRegionA =
                        Mathf.PerlinNoise(
                            noiseOffsetA.x * 0.017f +
                            point.x * 0.041f,
                            noiseOffsetA.y * 0.017f +
                            point.y * 0.041f);
                    float grassRegionB =
                        Mathf.PerlinNoise(
                            noiseOffsetB.x * 0.013f +
                            point.x * 0.037f + 43.1f,
                            noiseOffsetB.y * 0.013f +
                            point.y * 0.037f + 11.9f);
                    int sourceIndex = 0;
                    if (spikyVariantCount > 0)
                    {
                        if (generatedBaseGrassCount <
                            spikyVariantCount)
                        {
                            sourceIndex =
                                1 + generatedBaseGrassCount;
                        }
                        else if (spikyVariantCount == 4)
                        {
                            sourceIndex =
                                1 +
                                (grassRegionA >= 0.5f ? 1 : 0) +
                                (grassRegionB >= 0.5f ? 2 : 0);
                        }
                        else
                        {
                            float selector =
                                Mathf.Repeat(
                                    grassRegionA * 0.62f +
                                    grassRegionB * 0.38f +
                                    (float)random.NextDouble() *
                                        0.16f,
                                    0.9999f);
                            sourceIndex =
                                1 +
                                Mathf.Min(
                                    spikyVariantCount - 1,
                                    Mathf.FloorToInt(
                                        selector *
                                        spikyVariantCount));
                        }
                    }

                    float shapedHeightPatch =
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            heightPatch);
                    float baseHeight =
                        Mathf.Lerp(
                            0.20f,
                            0.64f,
                            shapedHeightPatch) *
                        Mathf.Lerp(
                            0.68f,
                            1.34f,
                            Mathf.Pow(
                                (float)random.NextDouble(),
                                1.12f));
                    float baseFootprint =
                        cellSpacing *
                        Mathf.Lerp(
                            1.42f,
                            2.02f,
                            (float)random.NextDouble());
                    AppendGrassPlacement(
                        batch,
                        sources[sourceIndex],
                        point,
                        baseHeight,
                        baseFootprint,
                        random);
                    generatedGrassCount++;
                    generatedBaseGrassCount++;
                    if (signedRoadDistance < 0f)
                    {
                        generatedTrailTransitionGrassCount++;
                    }
                    generatedGrassVariantCounts[
                        sourceIndex]++;
                    placementsInBatch++;

                    float leafyPatch =
                        Mathf.PerlinNoise(
                            noiseOffsetA.x * 0.014f +
                            point.x * 0.072f,
                            noiseOffsetA.y * 0.014f +
                            point.y * 0.072f);
                    if (sources.Count > 1 &&
                        leafyPatch >= 0.72f &&
                        random.NextDouble() < 0.34)
                    {
                        Vector2 accentPoint =
                            point +
                            new Vector2(
                                ((float)random.NextDouble() -
                                 0.5f) * cellSpacing * 0.44f,
                                ((float)random.NextDouble() -
                                 0.5f) * cellSpacing * 0.44f);
                        float accentHeight =
                            Mathf.Lerp(
                                0.14f,
                                0.29f,
                                shapedHeightPatch) *
                            Mathf.Lerp(
                                0.90f,
                                1.08f,
                                (float)random.NextDouble());
                        AppendGrassPlacement(
                            batch,
                            sources[0],
                            accentPoint,
                            accentHeight,
                            cellSpacing *
                                Mathf.Lerp(
                                    0.78f,
                                    1.08f,
                                    (float)random.NextDouble()),
                            random);
                        generatedGrassCount++;
                        generatedGrassVariantCounts[0]++;
                        placementsInBatch++;
                    }

                    if (placementsInBatch >=
                        GrassPlacementsPerBatch)
                    {
                        CreateGrassBatch(
                            root,
                            batch,
                            batchIndex++,
                            batchStart,
                            generatedGrassCount);
                        batch.Clear();
                        placementsInBatch = 0;
                        batchStart =
                            generatedGrassCount + 1;
                    }
                }
            }

            int spikySourceCount =
                Mathf.Max(
                    0,
                    sources.Count - 1);
            for (int boulderIndex = 0;
                 boulderIndex <
                    generatedBoulderPlacements.Count &&
                 spikySourceCount > 0;
                 boulderIndex++)
            {
                BoulderPlacement boulder =
                    generatedBoulderPlacements[
                        boulderIndex];
                if (boulder.Radius < 0.82f)
                {
                    continue;
                }

                int pocketCount =
                    Mathf.RoundToInt(
                        Mathf.Lerp(
                            12f,
                            22f,
                            Mathf.InverseLerp(
                                0.82f,
                                1.95f,
                                boulder.Radius)));
                float shelterAngle =
                    (float)random.NextDouble() *
                    Mathf.PI * 2f;
                for (int pocketIndex = 0;
                     pocketIndex < pocketCount;
                     pocketIndex++)
                {
                    float sideAngle =
                        shelterAngle +
                        (pocketIndex & 1) *
                        Mathf.PI;
                    float angle =
                        sideAngle +
                        Mathf.Lerp(
                            -0.68f,
                            0.68f,
                            (float)random.NextDouble());
                    float distance =
                        Mathf.Lerp(
                            boulder.Radius * 0.70f,
                            boulder.Radius + 0.72f,
                            Mathf.Sqrt(
                                (float)random.NextDouble()));
                    Vector2 point =
                        boulder.Position +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        distance;
                    if (point.sqrMagnitude >
                            usableRadius * usableRadius ||
                        SignedDistanceToRoad(point) <
                            GrassRoadInteriorLimit ||
                        DistanceToPolyline(
                            point,
                            riverSamples) <
                            riverHalfWidth +
                            GrassRiverClearance)
                    {
                        continue;
                    }

                    int sourceIndex =
                        1 + random.Next(
                            spikySourceCount);
                    AppendGrassPlacement(
                        batch,
                        sources[sourceIndex],
                        point,
                        Mathf.Lerp(
                            0.48f,
                            0.88f,
                            (float)random.NextDouble()),
                        cellSpacing *
                            Mathf.Lerp(
                                1.05f,
                                1.62f,
                                (float)random.NextDouble()),
                        random);
                    generatedGrassCount++;
                    generatedBoulderGrassCount++;
                    generatedGrassVariantCounts[
                        sourceIndex]++;
                    placementsInBatch++;

                    if (placementsInBatch >=
                        GrassPlacementsPerBatch)
                    {
                        CreateGrassBatch(
                            root,
                            batch,
                            batchIndex++,
                            batchStart,
                            generatedGrassCount);
                        batch.Clear();
                        placementsInBatch = 0;
                        batchStart =
                            generatedGrassCount + 1;
                    }
                }
            }

            for (int treeIndex = 0;
                 treeIndex < generatedTreePositions.Count;
                 treeIndex += 3)
            {
                generatedTreeBaseGrassCount +=
                    AppendHabitatGrassPocket(
                        root,
                        batch,
                        sources,
                        generatedTreePositions[
                            treeIndex],
                        0.58f,
                        random.Next(7, 13),
                        0.46f,
                        0.86f,
                        cellSpacing,
                        usableRadius,
                        random,
                        ref placementsInBatch,
                        ref batchIndex,
                        ref batchStart);
            }

            for (int anchorIndex = 0;
                 anchorIndex <
                    generatedFoliageAnchors.Count;
                 anchorIndex++)
            {
                generatedPlantEdgeGrassCount +=
                    AppendHabitatGrassPocket(
                        root,
                        batch,
                        sources,
                        generatedFoliageAnchors[
                            anchorIndex],
                        0.34f,
                        random.Next(6, 11),
                        0.40f,
                        0.76f,
                        cellSpacing,
                        usableRadius,
                        random,
                        ref placementsInBatch,
                        ref batchIndex,
                        ref batchStart);
            }

            if (placementsInBatch > 0)
            {
                CreateGrassBatch(
                    root,
                    batch,
                    batchIndex,
                    batchStart,
                    generatedGrassCount);
            }
        }

        private int AppendHabitatGrassPocket(
            Transform root,
            List<CombineInstance> batch,
            List<GrassMeshSource> sources,
            Vector2 center,
            float anchorRadius,
            int placementCount,
            float minimumHeight,
            float maximumHeight,
            float cellSpacing,
            float usableRadius,
            System.Random random,
            ref int placementsInBatch,
            ref int batchIndex,
            ref int batchStart)
        {
            int spikySourceCount =
                Mathf.Max(
                    0,
                    sources.Count - 1);
            if (spikySourceCount == 0)
            {
                return 0;
            }

            int placed = 0;
            float shelterAngle =
                (float)random.NextDouble() *
                Mathf.PI * 2f;
            for (int placementIndex = 0;
                 placementIndex < placementCount;
                 placementIndex++)
            {
                float angle =
                    shelterAngle +
                    (placementIndex & 1) *
                    Mathf.PI +
                    Mathf.Lerp(
                        -0.92f,
                        0.92f,
                        (float)random.NextDouble());
                float distance =
                    Mathf.Lerp(
                        anchorRadius * 0.38f,
                        anchorRadius + 0.82f,
                        Mathf.Sqrt(
                            (float)random.NextDouble()));
                Vector2 point =
                    center +
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) *
                    distance;
                if (point.sqrMagnitude >
                        usableRadius * usableRadius ||
                    SignedDistanceToRoad(point) <
                        GrassRoadInteriorLimit ||
                    DistanceToPolyline(
                        point,
                        riverSamples) <
                        riverHalfWidth +
                        GrassRiverClearance)
                {
                    continue;
                }

                int sourceIndex =
                    1 + random.Next(
                        spikySourceCount);
                AppendGrassPlacement(
                    batch,
                    sources[sourceIndex],
                    point,
                    Mathf.Lerp(
                        minimumHeight,
                        maximumHeight,
                        (float)random.NextDouble()),
                    cellSpacing *
                        Mathf.Lerp(
                            1.05f,
                            1.70f,
                            (float)random.NextDouble()),
                    random);
                generatedGrassCount++;
                generatedGrassVariantCounts[
                    sourceIndex]++;
                placementsInBatch++;
                placed++;

                if (placementsInBatch >=
                    GrassPlacementsPerBatch)
                {
                    CreateGrassBatch(
                        root,
                        batch,
                        batchIndex++,
                        batchStart,
                        generatedGrassCount);
                    batch.Clear();
                    placementsInBatch = 0;
                    batchStart =
                        generatedGrassCount + 1;
                }
            }
            return placed;
        }

        private void AppendGrassPlacement(
            List<CombineInstance> batch,
            GrassMeshSource source,
            Vector2 point,
            float targetHeight,
            float desiredFootprint,
            System.Random random)
        {
            float uniformScale =
                targetHeight /
                Mathf.Max(
                    0.001f,
                    source.LocalBounds.size.y);
            float sourceFootprint =
                Mathf.Max(
                    0.001f,
                    Mathf.Max(
                        source.LocalBounds.size.x,
                        source.LocalBounds.size.z));
            float footprintScale =
                desiredFootprint /
                sourceFootprint;
            Quaternion rotation =
                Quaternion.AngleAxis(
                    (float)random.NextDouble() *
                    360f,
                    Vector3.up) *
                source.ImportedRotation;
            Vector3 position =
                new Vector3(
                    point.x,
                    TerrainHeight(
                        point.x,
                        point.y) -
                        source.LocalBounds.min.y *
                        uniformScale -
                        0.012f,
                    point.y);
            Matrix4x4 placement =
                Matrix4x4.TRS(
                    position,
                    rotation,
                    new Vector3(
                        footprintScale,
                        uniformScale,
                        footprintScale));
            for (int partIndex = 0;
                 partIndex < source.Parts.Length;
                 partIndex++)
            {
                batch.Add(
                    new CombineInstance
                    {
                        mesh =
                            source.Parts[partIndex]
                                .Mesh,
                        transform =
                            placement *
                            source.Parts[partIndex]
                                .LocalMatrix
                    });
            }
        }

        private static List<GrassMeshSource>
            BuildGrassMeshSources(
                List<GameObject> prefabs)
        {
            var sources =
                new List<GrassMeshSource>(
                    prefabs.Count);
            for (int prefabIndex = 0;
                 prefabIndex < prefabs.Count;
                 prefabIndex++)
            {
                GameObject prefab =
                    prefabs[prefabIndex];
                MeshFilter[] filters =
                    prefab.GetComponentsInChildren<
                        MeshFilter>(true);
                var parts =
                    new List<GrassMeshPart>(
                        filters.Length);
                Bounds bounds = default;
                bool foundBounds = false;
                Matrix4x4 rootInverse =
                    prefab.transform
                        .worldToLocalMatrix;
                for (int filterIndex = 0;
                     filterIndex < filters.Length;
                     filterIndex++)
                {
                    MeshFilter filter =
                        filters[filterIndex];
                    if (filter.sharedMesh == null ||
                        filter.name.StartsWith(
                            "UCX_",
                            StringComparison
                                .OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Matrix4x4 localMatrix =
                        rootInverse *
                        filter.transform
                            .localToWorldMatrix;
                    Bounds partBounds =
                        TransformBounds(
                            filter.sharedMesh.bounds,
                            localMatrix);
                    if (!foundBounds)
                    {
                        bounds = partBounds;
                        foundBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(
                            partBounds);
                    }
                    parts.Add(
                        new GrassMeshPart
                        {
                            Mesh = filter.sharedMesh,
                            LocalMatrix = localMatrix
                        });
                }

                if (!foundBounds ||
                    bounds.size.y < 0.0001f ||
                    parts.Count == 0)
                {
                    continue;
                }

                sources.Add(
                    new GrassMeshSource
                    {
                        Name = prefab.name,
                        ImportedRotation =
                            prefab.transform.rotation,
                        LocalBounds = bounds,
                        Parts = parts.ToArray()
                    });
            }
            return sources;
        }

        private void CreateGrassBatch(
            Transform parent,
            List<CombineInstance> instances,
            int batchIndex,
            int firstPlacement,
            int lastPlacement)
        {
            if (instances.Count == 0)
            {
                return;
            }

            Mesh mesh = new Mesh
            {
                name =
                    $"Meadow Grass Batch " +
                    $"{batchIndex + 1:00}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.CombineMeshes(
                instances.ToArray(),
                true,
                true,
                false);
            Vector3[] vertices = mesh.vertices;
            var colors =
                new Color[vertices.Length];
            for (int vertexIndex = 0;
                 vertexIndex < vertices.Length;
                 vertexIndex++)
            {
                colors[vertexIndex] =
                    GrassTintAt(
                        vertices[vertexIndex].x,
                        vertices[vertexIndex].z);
            }
            mesh.colors = colors;
            mesh.RecalculateBounds();

            GameObject batch =
                new GameObject(
                    $"Meadow Grass " +
                    $"{firstPlacement:0000}-" +
                    $"{lastPlacement:0000}");
            batch.transform.SetParent(
                parent,
                false);
            batch.AddComponent<MeshFilter>()
                .sharedMesh = mesh;
            MeshRenderer renderer =
                batch.AddComponent<MeshRenderer>();
            renderer.sharedMaterial =
                grassDetailMaterial;
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Bounds TransformBounds(
            Bounds bounds,
            Matrix4x4 matrix)
        {
            Vector3 center =
                matrix.MultiplyPoint3x4(
                    bounds.center);
            Vector3 extents =
                bounds.extents;
            Vector3 axisX =
                matrix.MultiplyVector(
                    new Vector3(
                        extents.x,
                        0f,
                        0f));
            Vector3 axisY =
                matrix.MultiplyVector(
                    new Vector3(
                        0f,
                        extents.y,
                        0f));
            Vector3 axisZ =
                matrix.MultiplyVector(
                    new Vector3(
                        0f,
                        0f,
                        extents.z));
            Vector3 transformedExtents =
                new Vector3(
                    Mathf.Abs(axisX.x) +
                        Mathf.Abs(axisY.x) +
                        Mathf.Abs(axisZ.x),
                    Mathf.Abs(axisX.y) +
                        Mathf.Abs(axisY.y) +
                        Mathf.Abs(axisZ.y),
                    Mathf.Abs(axisX.z) +
                        Mathf.Abs(axisY.z) +
                        Mathf.Abs(axisZ.z));
            return new Bounds(
                center,
                transformedExtents * 2f);
        }

        private void CreateUndergrowth(
            System.Random random,
            List<GameObject> prefabs)
        {
            Transform root =
                new GameObject(
                    "Shrubs Flowers and Ground Cover")
                    .transform;
            root.SetParent(generatedRoot, false);
            var bushes = new List<GameObject>();
            var flowers = new List<GameObject>();
            var groundCover = new List<GameObject>();
            foreach (GameObject prefab in prefabs)
            {
                string lowerName =
                    prefab.name.ToLowerInvariant();
                if (lowerName.Contains("bush"))
                {
                    bushes.Add(prefab);
                }
                else if (lowerName.Contains("flower"))
                {
                    flowers.Add(prefab);
                }
                else
                {
                    groundCover.Add(prefab);
                }
            }

            var clusterCenters =
                new List<Vector2>();
            int generalUndergrowthTarget =
                Mathf.RoundToInt(
                    undergrowthCount * 0.70f);
            int clusterAttempts =
                undergrowthCount * 6;
            int clusterIndex = 0;
            for (int attempt = 0;
                 attempt < clusterAttempts &&
                 generatedUndergrowthCount <
                    generalUndergrowthTarget;
                 attempt++)
            {
                int clusterType = clusterIndex % 3;
                List<GameObject> choices =
                    clusterType == 0 && bushes.Count > 0
                        ? bushes
                        : clusterType == 1 &&
                          flowers.Count > 0
                            ? flowers
                            : groundCover.Count > 0
                                ? groundCover
                                : prefabs;
                Vector2 center =
                    RandomDiscPoint(
                        random,
                        3.4f);
                if (SignedDistanceToRoad(center) <
                        1.15f ||
                    DistanceToPolyline(
                        center,
                        riverSamples) <
                        riverHalfWidth + 0.72f ||
                    HasNearbyTree(
                        clusterCenters,
                        center,
                        3.2f))
                {
                    continue;
                }

                GameObject prefab =
                    choices[
                        clusterIndex % choices.Count];
                bool bushCluster =
                    prefab.name.IndexOf(
                        "bush",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                bool flowerCluster =
                    prefab.name.IndexOf(
                        "flower",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                int desiredGroupSize =
                    bushCluster
                        ? random.Next(5, 9)
                        : flowerCluster
                            ? random.Next(10, 17)
                            : random.Next(8, 15);
                desiredGroupSize =
                    Mathf.Min(
                        desiredGroupSize,
                        generalUndergrowthTarget -
                        generatedUndergrowthCount);
                float clusterRadius =
                    bushCluster
                        ? 1.72f
                        : flowerCluster
                            ? 2.10f
                            : 1.68f;
                int placedInCluster = 0;
                for (int member = 0;
                     member < desiredGroupSize;
                     member++)
                {
                    float angle =
                        (float)random.NextDouble() *
                        Mathf.PI * 2f;
                    float distance =
                        member == 0
                            ? 0f
                            : Mathf.Sqrt(
                                (float)random.NextDouble()) *
                              clusterRadius;
                    Vector2 point =
                        center +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        distance;
                    if (SignedDistanceToRoad(point) <
                            0.82f ||
                        DistanceToPolyline(
                            point,
                            riverSamples) <
                            riverHalfWidth + 0.48f ||
                        IsInsideBoulderCore(point))
                    {
                        continue;
                    }

                    float targetHeight =
                        UndergrowthHeight(
                            prefab.name,
                            random) *
                        Mathf.Lerp(
                            0.82f,
                            1.18f,
                            (float)random.NextDouble());
                    GameObject detail =
                        CreateSceneryInstance(
                            prefab,
                            root,
                            point,
                            targetHeight,
                            true,
                            plantDetailMaterial,
                            random,
                            false,
                            true);
                    if (detail == null)
                    {
                        continue;
                    }

                    string groupLabel =
                        bushCluster
                            ? "Bush Group"
                            : flowerCluster
                                ? "Flower Patch"
                                : "Ground Cover Pocket";
                    detail.name =
                        $"{prefab.name} {groupLabel} " +
                        $"{clusterIndex + 1:00}-" +
                        $"{placedInCluster + 1:00}";
                    generatedUndergrowthCount++;
                    placedInCluster++;
                }

                if (placedInCluster > 0)
                {
                    clusterCenters.Add(center);
                    generatedFoliageAnchors.Add(
                        center);
                    if (bushCluster &&
                        placedInCluster >= 3)
                    {
                        generatedBushGroupCount++;
                        generatedBushClusterMemberCount +=
                            placedInCluster;
                    }
                    if (flowerCluster &&
                        placedInCluster >= 3)
                    {
                        generatedFlowerPatchCount++;
                        generatedFlowerClusterMemberCount +=
                            placedInCluster;
                    }
                    if (!bushCluster &&
                        !flowerCluster &&
                        placedInCluster >= 4)
                    {
                        generatedGroundCoverPatchCount++;
                    }
                    clusterIndex++;
                }
            }

            int treeStart =
                generatedTreePositions.Count > 0
                    ? random.Next(
                        generatedTreePositions.Count)
                    : 0;
            int treeAttempts =
                generatedTreePositions.Count * 2;
            for (int attempt = 0;
                 attempt < treeAttempts &&
                 generatedUndergrowthCount <
                    undergrowthCount;
                 attempt++)
            {
                Vector2 treeCenter =
                    generatedTreePositions[
                        (treeStart + attempt * 7) %
                        generatedTreePositions.Count];
                List<GameObject> choices =
                    bushes.Count > 0 &&
                    random.NextDouble() < 0.62
                        ? bushes
                        : groundCover.Count > 0
                            ? groundCover
                            : prefabs;
                GameObject prefab =
                    choices[random.Next(choices.Count)];
                int groupSize =
                    Mathf.Min(
                        random.Next(3, 6),
                        undergrowthCount -
                        generatedUndergrowthCount);
                int placedAtTree = 0;
                for (int member = 0;
                     member < groupSize;
                     member++)
                {
                    float angle =
                        (float)random.NextDouble() *
                        Mathf.PI * 2f;
                    float distance =
                        Mathf.Lerp(
                            0.38f,
                            1.28f,
                            Mathf.Sqrt(
                                (float)random.NextDouble()));
                    Vector2 point =
                        treeCenter +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        distance;
                    if (SignedDistanceToRoad(point) <
                            0.78f ||
                        DistanceToPolyline(
                            point,
                            riverSamples) <
                            riverHalfWidth + 0.45f ||
                        IsInsideBoulderCore(point))
                    {
                        continue;
                    }

                    float targetHeight =
                        UndergrowthHeight(
                            prefab.name,
                            random) *
                        Mathf.Lerp(
                            0.88f,
                            1.28f,
                            (float)random.NextDouble());
                    GameObject detail =
                        CreateSceneryInstance(
                            prefab,
                            root,
                            point,
                            targetHeight,
                            true,
                            plantDetailMaterial,
                            random,
                            false,
                            true);
                    if (detail == null)
                    {
                        continue;
                    }

                    detail.name =
                        $"{prefab.name} Tree Base " +
                        $"{attempt + 1:000}-" +
                        $"{placedAtTree + 1:00}";
                    generatedUndergrowthCount++;
                    generatedTreeBaseFoliageCount++;
                    placedAtTree++;
                }
            }
        }

        private bool IsInsideBoulderCore(
            Vector2 point)
        {
            for (int index = 0;
                 index <
                    generatedBoulderPlacements.Count;
                 index++)
            {
                BoulderPlacement boulder =
                    generatedBoulderPlacements[index];
                if (Vector2.Distance(
                        point,
                        boulder.Position) <
                    boulder.Radius * 0.72f)
                {
                    return true;
                }
            }
            return false;
        }

        private void CreateBoulders(
            System.Random random,
            List<GameObject> prefabs)
        {
            Transform root =
                new GameObject(
                    "Boulders").transform;
            root.SetParent(generatedRoot, false);
            var accepted =
                new List<Vector2>(boulderCount);
            int attempts = boulderCount * 16;
            for (int attempt = 0;
                 attempt < attempts &&
                 generatedBoulderCount <
                    boulderCount;
                 attempt++)
            {
                Vector2 point =
                    RandomDiscPoint(
                        random,
                        3.5f);
                if (SignedDistanceToRoad(point) <
                        1.15f ||
                    DistanceToPolyline(
                        point,
                        riverSamples) <
                        riverHalfWidth + 0.25f ||
                    HasNearbyTree(
                        accepted,
                        point,
                        4.1f) ||
                    HasNearbyTree(
                        generatedTreePositions,
                        point,
                        2.4f))
                {
                    continue;
                }

                GameObject prefab =
                    prefabs[
                        generatedBoulderCount <
                            prefabs.Count
                            ? generatedBoulderCount %
                                prefabs.Count
                            : random.Next(
                                prefabs.Count)];
                float targetSize =
                    Mathf.Lerp(
                        1.35f,
                        3.9f,
                        Mathf.Pow(
                            (float)random.NextDouble(),
                            1.35f));
                GameObject boulder =
                    CreateSceneryInstance(
                        prefab,
                        root,
                        point,
                        targetSize,
                        false,
                        rockMaterial,
                        random,
                        true,
                        true,
                        true);
                if (boulder == null)
                {
                    continue;
                }

                boulder.name =
                    $"{prefab.name} Boulder " +
                    $"{generatedBoulderCount + 1:00}";
                Renderer[] boulderRenderers =
                    boulder.GetComponentsInChildren<
                        Renderer>(true);
                if (TryGetRendererBounds(
                        boulderRenderers,
                        out Bounds boulderBounds))
                {
                    generatedBoulderPlacements.Add(
                        new BoulderPlacement
                        {
                            Position = point,
                            Radius = Mathf.Max(
                                boulderBounds.extents.x,
                                boulderBounds.extents.z)
                        });
                }
                accepted.Add(point);
                generatedBoulderCount++;
            }
        }

        private void CreateTrailStones(
            System.Random random,
            List<GameObject> prefabs)
        {
            Transform root =
                new GameObject(
                    "Trail and Edge Stones")
                    .transform;
            root.SetParent(generatedRoot, false);
            int attempts = trailStoneCount * 5;
            for (int attempt = 0;
                 attempt < attempts &&
                 generatedTrailStoneCount <
                    trailStoneCount;
                 attempt++)
            {
                float t =
                    Mathf.Lerp(
                        0.035f,
                        0.965f,
                        (float)random.NextDouble());
                Vector3 center =
                    RoadPointAt(t);
                Vector3 previous =
                    RoadPointAt(
                        Mathf.Max(0f, t - 0.008f));
                Vector3 next =
                    RoadPointAt(
                        Mathf.Min(1f, t + 0.008f));
                Vector3 tangent =
                    Vector3.ProjectOnPlane(
                        next - previous,
                        Vector3.up).normalized;
                Vector3 right =
                    Vector3.Cross(
                        Vector3.up,
                        tangent);
                float localHalfWidth =
                    RoadHalfWidthAt(
                        ToXZ(center));
                bool onTrail =
                    random.NextDouble() < 0.32;
                float lateral =
                    onTrail
                        ? Mathf.Lerp(
                            -localHalfWidth * 0.62f,
                            localHalfWidth * 0.62f,
                            (float)random.NextDouble())
                        : (random.NextDouble() < 0.5
                            ? -1f
                            : 1f) *
                            Mathf.Lerp(
                                localHalfWidth + 0.08f,
                                localHalfWidth + 0.75f,
                                (float)random.NextDouble());
                Vector2 point =
                    ToXZ(
                        center +
                        right * lateral);
                if (DistanceToPolyline(
                        point,
                        riverSamples) <
                    riverHalfWidth + 0.2f)
                {
                    continue;
                }

                GameObject prefab =
                    prefabs[
                        generatedTrailStoneCount %
                        prefabs.Count];
                float targetSize =
                    Mathf.Lerp(
                        0.16f,
                        0.58f,
                        (float)random.NextDouble());
                GameObject stone =
                    CreateSceneryInstance(
                        prefab,
                        root,
                        point,
                        targetSize,
                        false,
                        rockMaterial,
                        random,
                        false,
                        false,
                        true);
                if (stone == null)
                {
                    continue;
                }

                stone.name =
                    $"{prefab.name} Trail Stone " +
                    $"{generatedTrailStoneCount + 1:00}";
                generatedTrailStoneCount++;
            }
        }

        private GameObject CreateSceneryInstance(
            GameObject prefab,
            Transform parent,
            Vector2 point,
            float targetSize,
            bool normalizeByHeight,
            Material material,
            System.Random random,
            bool addCollider,
            bool castShadows,
            bool conformToSlope = false)
        {
            if (prefab == null)
            {
                return null;
            }

            float terrainHeight =
                TerrainHeight(
                    point.x,
                    point.y);
            GameObject instance =
                Instantiate(
                    prefab,
                    parent);
            RemoveAllColliders(instance);
            instance.transform.position =
                new Vector3(
                    point.x,
                    terrainHeight,
                    point.y);
            Quaternion importedRotation =
                instance.transform.rotation;
            float yaw =
                (float)random.NextDouble() *
                360f;
            if (conformToSlope)
            {
                Vector3 terrainNormal =
                    TerrainNormalAt(
                        point.x,
                        point.y);
                terrainNormal =
                    Vector3.RotateTowards(
                        Vector3.up,
                        terrainNormal,
                        Mathf.Deg2Rad * 22f,
                        0f).normalized;
                Quaternion slopeAlignment =
                    Quaternion.FromToRotation(
                        Vector3.up,
                        terrainNormal);
                instance.transform.rotation =
                    Quaternion.AngleAxis(
                        yaw,
                        terrainNormal) *
                    slopeAlignment *
                    importedRotation;
            }
            else
            {
                instance.transform.rotation =
                    Quaternion.AngleAxis(
                        yaw,
                        Vector3.up) *
                    importedRotation;
            }

            Renderer[] importedRenderers =
                instance.GetComponentsInChildren<
                    Renderer>(true);
            var visibleRenderers =
                new List<Renderer>(
                    importedRenderers.Length);
            for (int index = 0;
                 index < importedRenderers.Length;
                 index++)
            {
                Renderer renderer =
                    importedRenderers[index];
                if (IsCollisionHelper(renderer))
                {
                    renderer.enabled = false;
                    continue;
                }

                visibleRenderers.Add(renderer);
                if (material != null)
                {
                    Material[] materials =
                        renderer.sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex <
                            materials.Length;
                         materialIndex++)
                    {
                        materials[materialIndex] =
                            material;
                    }
                    renderer.sharedMaterials =
                        materials;
                }
                renderer.shadowCastingMode =
                    castShadows
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off;
            }

            Renderer[] renderers =
                visibleRenderers.ToArray();
            if (!TryGetRendererBounds(
                    renderers,
                    out Bounds importedBounds))
            {
                Destroy(instance);
                return null;
            }

            float sourceSize =
                normalizeByHeight
                    ? importedBounds.size.y
                    : Mathf.Max(
                        importedBounds.size.x,
                        importedBounds.size.y,
                        importedBounds.size.z);
            if (sourceSize < 0.0001f)
            {
                Destroy(instance);
                return null;
            }

            instance.transform.localScale *=
                targetSize / sourceSize;
            TryGetRendererBounds(
                renderers,
                out Bounds scaledBounds);
            float groundedBaseHeight =
                terrainHeight - 0.015f;
            if (conformToSlope)
            {
                float footprintRadiusX =
                    Mathf.Max(
                        0.06f,
                        scaledBounds.extents.x * 0.72f);
                float footprintRadiusZ =
                    Mathf.Max(
                        0.06f,
                        scaledBounds.extents.z * 0.72f);
                float settlingDepth =
                    Mathf.Min(
                        0.12f,
                        scaledBounds.size.y * 0.045f);
                groundedBaseHeight =
                    MinimumTerrainHeightUnderFootprint(
                        point,
                        footprintRadiusX,
                        footprintRadiusZ,
                        12) -
                    settlingDepth;
            }
            instance.transform.position +=
                Vector3.up *
                (groundedBaseHeight -
                 scaledBounds.min.y);

            if (addCollider)
            {
                AddExactVisibleMeshColliders(
                    renderers);
            }
            return instance;
        }

        private void AddExactTreeWoodColliders(
            GameObject tree,
            Renderer[] visibleRenderers)
        {
            int added = 0;
            for (int rendererIndex = 0;
                 rendererIndex < visibleRenderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    visibleRenderers[rendererIndex];
                MeshFilter filter =
                    renderer.GetComponent<MeshFilter>();
                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                bool[] woodSubmeshes =
                    ResolveWoodSubmeshes(
                        renderer,
                        filter.sharedMesh.subMeshCount);
                Mesh collisionMesh =
                    GetOrCreateTreeCollisionMesh(
                        filter.sharedMesh,
                        woodSubmeshes);
                if (collisionMesh == null)
                {
                    continue;
                }

                MeshCollider collider =
                    renderer.gameObject
                        .AddComponent<MeshCollider>();
                collider.sharedMesh = collisionMesh;
                collider.convex = false;
                added++;
            }

            if (added > 0)
            {
                return;
            }

            // A non-readable third-party mesh can still use its authored UCX
            // helper. This is a narrow solid hull, never the old broad capsule.
            MeshFilter[] filters =
                tree.GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (filter.sharedMesh == null ||
                    !filter.name.StartsWith(
                        "UCX_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MeshCollider collider =
                    filter.gameObject
                        .AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
        }

        private Mesh GetOrCreateTreeCollisionMesh(
            Mesh source,
            bool[] woodSubmeshes)
        {
            if (treeCollisionMeshCache.TryGetValue(
                    source,
                    out Mesh existing))
            {
                return existing;
            }
            if (!source.isReadable)
            {
                return null;
            }

            var sourceIndices = new List<int>();
            int submeshCount = Mathf.Min(
                source.subMeshCount,
                woodSubmeshes.Length);
            for (int submesh = 0;
                 submesh < submeshCount;
                 submesh++)
            {
                if (woodSubmeshes[submesh])
                {
                    sourceIndices.AddRange(
                        source.GetTriangles(submesh));
                }
            }
            if (sourceIndices.Count == 0)
            {
                return null;
            }

            Vector3[] sourceVertices = source.vertices;
            var remap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var triangles = new int[sourceIndices.Count];
            for (int index = 0;
                 index < sourceIndices.Count;
                 index++)
            {
                int sourceIndex = sourceIndices[index];
                if (!remap.TryGetValue(
                        sourceIndex,
                        out int collisionIndex))
                {
                    collisionIndex = vertices.Count;
                    remap.Add(
                        sourceIndex,
                        collisionIndex);
                    vertices.Add(
                        sourceVertices[sourceIndex]);
                }
                triangles[index] = collisionIndex;
            }

            Mesh collisionMesh = new Mesh
            {
                name =
                    $"{source.name} Exact Wood Collision",
                indexFormat = vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            collisionMesh.SetVertices(vertices);
            collisionMesh.SetTriangles(
                triangles,
                0);
            collisionMesh.RecalculateBounds();
            treeCollisionMeshCache.Add(
                source,
                collisionMesh);
            return collisionMesh;
        }

        private static bool[] ResolveWoodSubmeshes(
            Renderer renderer,
            int submeshCount)
        {
            var result = new bool[submeshCount];
            Material[] materials =
                renderer.sharedMaterials;
            int count = Mathf.Min(
                materials.Length,
                submeshCount);
            for (int index = 0; index < count; index++)
            {
                Material material = materials[index];
                string materialName =
                    material != null
                        ? material.name
                        : string.Empty;
                result[index] =
                    materialName.IndexOf(
                        "bark",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf(
                        "barck",
                        StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return result;
        }

        private static void AddExactVisibleMeshColliders(
            Renderer[] renderers)
        {
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                MeshFilter filter =
                    renderers[index]
                        .GetComponent<MeshFilter>();
                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                MeshCollider collider =
                    filter.gameObject
                        .AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
        }

        private static void RemoveAllColliders(
            GameObject root)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(true);
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider = colliders[index];
                collider.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }

        private static List<GameObject>
            CollectValidPrefabs(
                GameObject[] prefabs)
        {
            var valid = new List<GameObject>();
            if (prefabs == null)
            {
                return valid;
            }

            for (int index = 0;
                 index < prefabs.Length;
                 index++)
            {
                if (prefabs[index] != null)
                {
                    valid.Add(prefabs[index]);
                }
            }
            return valid;
        }

        private Vector2 RandomDiscPoint(
            System.Random random,
            float edgeMargin)
        {
            float angle =
                (float)random.NextDouble() *
                Mathf.PI *
                2f;
            float radius =
                Mathf.Sqrt(
                    (float)random.NextDouble()) *
                (mapRadius - edgeMargin);
            return new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius);
        }

        private static float UndergrowthHeight(
            string prefabName,
            System.Random random)
        {
            string lower =
                prefabName != null
                    ? prefabName.ToLowerInvariant()
                    : string.Empty;
            float t =
                (float)random.NextDouble();
            if (lower.Contains("bush"))
            {
                return Mathf.Lerp(
                    0.75f,
                    1.65f,
                    t);
            }
            if (lower.Contains("clover"))
            {
                return Mathf.Lerp(
                    0.14f,
                    0.32f,
                    t);
            }
            if (lower.Contains("flower"))
            {
                return Mathf.Lerp(
                    0.22f,
                    0.52f,
                    t);
            }
            return Mathf.Lerp(
                0.38f,
                0.88f,
                t);
        }

        private static void ConfigureRaidAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor =
                new Color(
                    0.46f,
                    0.52f,
                    0.58f,
                    1f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 105f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor =
                new Color(0.48f, 0.55f, 0.62f, 1f);
            RenderSettings.ambientEquatorColor =
                new Color(0.34f, 0.39f, 0.44f, 1f);
            RenderSettings.ambientGroundColor =
                new Color(0.24f, 0.27f, 0.29f, 1f);
            RenderSettings.ambientIntensity = 1.18f;
            RenderSettings.reflectionIntensity = 0.42f;

            Light[] lights =
                FindObjectsByType<Light>(
                    FindObjectsSortMode.None);
            foreach (Light light in lights)
            {
                if (light.type != LightType.Directional)
                {
                    continue;
                }
                light.color =
                    new Color(0.94f, 0.86f, 0.72f, 1f);
                light.intensity = 1.35f;
                light.shadowStrength = 0.68f;
                light.transform.rotation =
                    Quaternion.Euler(62f, -42f, 0f);
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags =
                    CameraClearFlags.SolidColor;
                camera.backgroundColor =
                    RenderSettings.fogColor;
                UniversalAdditionalCameraData cameraData =
                    camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
            }

            GameObject gradeObject =
                GameObject.Find("Raid Atmosphere Grade");
            if (gradeObject == null)
            {
                gradeObject =
                    new GameObject(
                        "Raid Atmosphere Grade");
            }
            Volume volume =
                gradeObject.GetComponent<Volume>();
            if (volume == null)
            {
                volume = gradeObject.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 20f;
            VolumeProfile profile = volume.profile;
            if (!profile.TryGet(
                    out ColorAdjustments color))
            {
                color = profile.Add<ColorAdjustments>();
            }
            color.active = true;
            color.postExposure.Override(0.68f);
            color.contrast.Override(4f);
            color.saturation.Override(-27f);
            color.colorFilter.Override(
                new Color(0.88f, 0.94f, 1f, 1f));

            if (!profile.TryGet(out Tonemapping tone))
            {
                tone = profile.Add<Tonemapping>();
            }
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>();
            }
            vignette.active = true;
            vignette.color.Override(
                new Color(0.015f, 0.020f, 0.022f, 1f));
            vignette.intensity.Override(0.025f);
            vignette.smoothness.Override(0.56f);

            if (!profile.TryGet(
                    out ShadowsMidtonesHighlights tonalShape))
            {
                tonalShape =
                    profile.Add<ShadowsMidtonesHighlights>();
            }
            tonalShape.active = true;
            tonalShape.shadows.Override(
                new Vector4(1.06f, 1.08f, 1.10f, 0f));
            tonalShape.midtones.Override(
                new Vector4(1f, 1f, 1f, 0f));
            tonalShape.highlights.Override(
                new Vector4(1.05f, 1.01f, 0.94f, 0f));

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>();
            }
            bloom.active = true;
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.08f);
            bloom.scatter.Override(0.55f);
            bloom.tint.Override(
                new Color(1f, 0.94f, 0.84f, 1f));
        }

        private static bool IsCollisionHelper(
            Renderer renderer)
        {
            return renderer != null &&
                renderer.name.StartsWith(
                    "UCX_",
                    StringComparison.OrdinalIgnoreCase);
        }

        private Material ResolveTreeMaterial(
            Material source,
            string prefabName)
        {
            string materialName =
                source != null
                    ? source.name.ToLowerInvariant()
                    : string.Empty;
            string treeName =
                prefabName != null
                    ? prefabName.ToLowerInvariant()
                    : string.Empty;
            if (materialName.Contains("birch"))
            {
                return birchBarkMaterial != null
                    ? birchBarkMaterial
                    : treeBarkMaterial;
            }
            if (materialName.Contains("pine") ||
                treeName.Contains("_pine_") &&
                !materialName.Contains("barck") &&
                !materialName.Contains("bark"))
            {
                return pineLeavesMaterial != null
                    ? pineLeavesMaterial
                    : treeLeavesMaterial;
            }
            if (materialName.Contains("foliage") ||
                materialName.Contains("leaves"))
            {
                return treeLeavesMaterial;
            }

            return treeBarkMaterial != null
                ? treeBarkMaterial
                : source;
        }

        private static bool TryGetRendererBounds(
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private void PlaceActorsAndObjectives(
            System.Random random)
        {
            Vector3 start =
                RoadPointAt(
                    layout != null
                        ? layout.PlayerSpawnRoadT
                        : 0.11f);
            Vector3 extraction =
                RoadPointAt(
                    layout != null
                        ? layout.ExtractionRoadT
                        : 0.90f);
            MoveActor(
                player,
                RoadSurfacePoint(start, 1f));
            if (player != null)
            {
                Vector3 forward =
                    RoadPointAt(
                        Mathf.Min(
                            1f,
                            (layout != null
                                ? layout.PlayerSpawnRoadT
                                : 0.11f) + 0.025f)) -
                    start;
                player.rotation =
                    Quaternion.LookRotation(
                        Vector3.ProjectOnPlane(
                            forward,
                            Vector3.up),
                        Vector3.up);
            }

            if (extractionZone != null)
            {
                extractionZone.transform.position =
                    RoadSurfacePoint(
                        extraction,
                        0.12f);
            }

            if (enemies != null)
            {
                generatedGuardGroupCount = 0;
                generatedGuardPairCount = 0;
                var roads = new List<List<Vector3>>();
                foreach (List<Vector3> road in AllRoads())
                {
                    roads.Add(road);
                }

                int enemyIndex = 0;
                while (enemyIndex < enemies.Length &&
                       roads.Count > 0)
                {
                    int remaining = enemies.Length - enemyIndex;
                    bool pair =
                        remaining >= 2 &&
                        ((generatedGuardPairCount == 0 &&
                          generatedGuardGroupCount >= 1) ||
                         random.NextDouble() < 0.34);
                    int groupSize = pair ? 2 : 1;
                    List<Vector3> patrolRoad = roads[0];
                    float t = 0.5f;
                    Vector3 groupCenter = Vector3.zero;
                    for (int attempt = 0; attempt < 12; attempt++)
                    {
                        int roadIndex =
                            (generatedGuardGroupCount +
                             random.Next(0, roads.Count)) %
                            roads.Count;
                        patrolRoad = roads[roadIndex];
                        t = Mathf.Lerp(
                            0.17f,
                            0.83f,
                            (float)random.NextDouble());
                        groupCenter = RoadPointAt(
                            patrolRoad,
                            t);
                        if (Vector3.Distance(
                                groupCenter,
                                start) >= 32f &&
                            Vector3.Distance(
                                groupCenter,
                                extraction) >= 24f)
                        {
                            break;
                        }
                    }
                    Vector3 tangent = RoadTangentAt(
                        patrolRoad,
                        t);
                    Vector3 side = Vector3.Cross(
                        Vector3.up,
                        tangent).normalized;
                    float routeSpan = Mathf.Clamp(
                        13f /
                        Mathf.Max(
                            1f,
                            PolylineLength(patrolRoad)),
                        0.035f,
                        0.12f);
                    Vector3[] route = BuildPatrolRoute(
                        patrolRoad,
                        Mathf.Max(0.04f, t - routeSpan),
                        Mathf.Min(0.96f, t + routeSpan),
                        7);

                    for (int member = 0;
                         member < groupSize &&
                         enemyIndex < enemies.Length;
                         member++, enemyIndex++)
                    {
                        EnemyBrain enemy = enemies[enemyIndex];
                        if (enemy == null)
                        {
                            continue;
                        }

                        EnemyDamageProfile damageProfile =
                            enemy.GetComponent<EnemyDamageProfile>();
                        if (damageProfile == null)
                        {
                            damageProfile = enemy.gameObject.AddComponent<
                                EnemyDamageProfile>();
                        }
                        damageProfile.Configure(
                            EnemyCombatVariant.RaidEnemy);

                        float lateral = groupSize == 2
                            ? member == 0 ? -0.72f : 0.72f
                            : 0f;
                        Vector3 spawn =
                            groupCenter + side * lateral;
                        MoveActor(
                            enemy.transform,
                            RoadSurfacePoint(spawn, 1f));
                        enemy.transform.rotation =
                            Quaternion.LookRotation(
                                tangent,
                                Vector3.up);
                        enemy.ConfigurePatrolRoute(
                            route,
                            route.Length / 2);
                    }

                    generatedGuardGroupCount++;
                    if (pair)
                    {
                        generatedGuardPairCount++;
                    }
                }
            }

            RaidPickup[] pickups =
                FindObjectsByType<RaidPickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0;
                 index < pickups.Length;
                 index++)
            {
                float t =
                    Mathf.Lerp(
                        0.22f,
                        0.82f,
                        (index + 1f) /
                        (pickups.Length + 1f));
                Vector3 roadPoint =
                    RoadPointAt(t);
                Vector3 tangent =
                    RoadPointAt(
                        Mathf.Min(1f, t + 0.02f)) -
                    roadPoint;
                Vector3 right =
                    Vector3.Cross(
                        Vector3.up,
                        tangent.normalized);
                float side =
                    index % 2 == 0
                        ? -1f
                        : 1f;
                Vector3 point =
                    roadPoint +
                    right *
                    side *
                    (roadHalfWidth + 1.7f);
                pickups[index].transform.position =
                    SurfacePoint(point, 0.75f);
            }
        }

        private Vector3[] BuildPatrolRoute(
            List<Vector3> road,
            float start,
            float end,
            int count)
        {
            var route = new Vector3[count];
            for (int index = 0;
                 index < count;
                 index++)
            {
                float t =
                    Mathf.Lerp(
                        start,
                        end,
                        index / (count - 1f));
                route[index] =
                    RoadSurfacePoint(
                        RoadPointAt(road, t),
                        1f);
            }
            return route;
        }

        private void MoveActor(
            Transform actor,
            Vector3 position)
        {
            if (actor == null)
            {
                return;
            }

            CharacterController controller =
                actor.GetComponent<CharacterController>();
            bool wasEnabled =
                controller != null &&
                controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }
            actor.position = position;
            if (controller != null)
            {
                controller.enabled = wasEnabled;
            }
        }

        private float TerrainHeight(
            float x,
            float z)
        {
            float height =
                RawLandHeight(x, z);
            float riverDistance =
                DistanceToPolyline(
                    new Vector2(x, z),
                    riverSamples);
            if (riverDistance <
                riverHalfWidth + 2.2f)
            {
                float riverBlend =
                    1f -
                    Mathf.InverseLerp(
                        riverHalfWidth * 0.55f,
                        riverHalfWidth + 2.2f,
                        riverDistance);
                height -=
                    riverBlend * riverBlend * 3.4f;
            }

            if (TryClosestRoadPoint(
                    new Vector2(x, z),
                    out Vector2 roadPoint,
                    out float roadDistance))
            {
                float localRoadHalfWidth =
                    RoadHalfWidthAt(roadPoint);
                float shoulder =
                    localRoadHalfWidth +
                    RoadShoulderWidth;
                if (roadDistance < shoulder)
                {
                    bool bridgeGap =
                        layout != null &&
                        layout.RiverCrossesRoad &&
                        riverDistance <
                        riverHalfWidth + 1.3f;
                    if (!bridgeGap)
                    {
                        float blend =
                            1f -
                            Mathf.InverseLerp(
                                localRoadHalfWidth,
                                shoulder,
                                roadDistance);
                        float roadHeight =
                            RawLandHeight(
                                roadPoint.x,
                                roadPoint.y) -
                            RoadIndentation;
                        height =
                            Mathf.Lerp(
                                height,
                                roadHeight,
                                Mathf.SmoothStep(
                                    0f,
                                    1f,
                                    blend));
                    }
                }
            }

            float edge =
                Mathf.Sqrt(x * x + z * z) /
                mapRadius;
            if (edge > 0.88f)
            {
                height -=
                    Mathf.InverseLerp(
                        0.88f,
                        1f,
                        edge) *
                    1.8f;
            }
            return height;
        }

        private Vector3 TerrainNormalAt(
            float x,
            float z)
        {
            const float SampleDistance = 0.45f;
            float left =
                TerrainHeight(
                    x - SampleDistance,
                    z);
            float right =
                TerrainHeight(
                    x + SampleDistance,
                    z);
            float back =
                TerrainHeight(
                    x,
                    z - SampleDistance);
            float forward =
                TerrainHeight(
                    x,
                    z + SampleDistance);
            return new Vector3(
                    left - right,
                    SampleDistance * 2f,
                    back - forward)
                .normalized;
        }

        private float MinimumTerrainHeightUnderFootprint(
            Vector2 center,
            float radiusX,
            float radiusZ,
            int perimeterSamples)
        {
            float minimumHeight =
                TerrainHeight(
                    center.x,
                    center.y);
            int sampleCount =
                Mathf.Max(
                    4,
                    perimeterSamples);
            for (int ring = 1;
                 ring <= 2;
                 ring++)
            {
                float radiusScale = ring * 0.5f;
                for (int sample = 0;
                     sample < sampleCount;
                     sample++)
                {
                    float angle =
                        sample *
                        Mathf.PI * 2f /
                        sampleCount;
                    float sampleX =
                        center.x +
                        Mathf.Cos(angle) *
                        radiusX *
                        radiusScale;
                    float sampleZ =
                        center.y +
                        Mathf.Sin(angle) *
                        radiusZ *
                        radiusScale;
                    minimumHeight =
                        Mathf.Min(
                            minimumHeight,
                            TerrainHeight(
                                sampleX,
                                sampleZ));
                }
            }

            return minimumHeight;
        }

        private float RawLandHeight(
            float x,
            float z)
        {
            float broad =
                Mathf.PerlinNoise(
                    noiseOffsetA.x + x * 0.018f,
                    noiseOffsetA.y + z * 0.018f);
            float medium =
                Mathf.PerlinNoise(
                    noiseOffsetB.x + x * 0.047f,
                    noiseOffsetB.y + z * 0.047f);
            float fine =
                Mathf.PerlinNoise(
                    noiseOffsetA.x * 0.37f + x * 0.11f,
                    noiseOffsetA.y * 0.37f + z * 0.11f);
            float valley =
                Mathf.Abs(
                    Mathf.Sin(
                        (x + z * 0.62f) *
                        0.038f +
                        noiseOffsetB.x * 0.001f));
            return
                (broad - 0.5f) * 8.5f +
                (medium - 0.5f) * 3.4f +
                (fine - 0.5f) * 0.8f -
                (1f - valley) * 1.4f;
        }

        private float RoadSurfaceHeight(
            float x,
            float z)
        {
            return TerrainHeight(x, z);
        }

        private float WaterHeight(
            float x,
            float z)
        {
            return
                RawLandHeight(x, z) -
                1.55f;
        }

        private Vector3 SurfacePoint(
            Vector3 point,
            float offset)
        {
            point.y =
                TerrainHeight(
                    point.x,
                    point.z) +
                offset;
            return point;
        }

        private Vector3 RoadSurfacePoint(
            Vector3 point,
            float offset)
        {
            point.y =
                RoadSurfaceHeight(
                    point.x,
                    point.z) +
                offset;
            return point;
        }

        private Vector3 RoadPointAt(float t)
        {
            return RoadPointAt(mainRoadSamples, t);
        }

        private static Vector3 RoadPointAt(
            List<Vector3> road,
            float t)
        {
            if (road == null || road.Count == 0)
            {
                return Vector3.zero;
            }

            float scaled =
                Mathf.Clamp01(t) *
                (road.Count - 1);
            int first =
                Mathf.FloorToInt(scaled);
            int second =
                Mathf.Min(
                    road.Count - 1,
                    first + 1);
            return Vector3.Lerp(
                road[first],
                road[second],
                scaled - first);
        }

        private static Vector3 RoadTangentAt(
            List<Vector3> road,
            float t)
        {
            Vector3 before = RoadPointAt(
                road,
                Mathf.Max(0f, t - 0.012f));
            Vector3 after = RoadPointAt(
                road,
                Mathf.Min(1f, t + 0.012f));
            Vector3 direction = Vector3.ProjectOnPlane(
                after - before,
                Vector3.up);
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
        }

        private static float PolylineLength(
            List<Vector3> road)
        {
            float length = 0f;
            if (road == null)
            {
                return length;
            }
            for (int index = 1; index < road.Count; index++)
            {
                length += Vector3.Distance(
                    road[index - 1],
                    road[index]);
            }
            return length;
        }

        private float DistanceToRoad(Vector2 point)
        {
            float distance = DistanceToPolyline(
                point,
                mainRoadSamples);
            distance = Mathf.Min(
                distance,
                DistanceToPolyline(point, forkRoadSamples));
            distance = Mathf.Min(
                distance,
                DistanceToPolyline(point, branchRoadASamples));
            distance = Mathf.Min(
                distance,
                DistanceToPolyline(point, branchRoadBSamples));
            return Mathf.Min(
                distance,
                DistanceToPolyline(point, branchRoadCSamples));
        }

        private float SignedDistanceToRoad(
            Vector2 point)
        {
            return TryClosestRoadPoint(
                    point,
                    out Vector2 closest,
                    out float distance)
                ? distance -
                    RoadHalfWidthAt(closest)
                : float.PositiveInfinity;
        }

        private float RoadHalfWidthAt(
            Vector2 roadPoint)
        {
            float broad =
                Mathf.PerlinNoise(
                    noiseOffsetA.x * 0.013f +
                    roadPoint.x * 0.055f,
                    noiseOffsetA.y * 0.013f +
                    roadPoint.y * 0.055f);
            float detail =
                Mathf.PerlinNoise(
                    noiseOffsetB.x * 0.021f +
                    roadPoint.x * 0.14f,
                    noiseOffsetB.y * 0.021f +
                    roadPoint.y * 0.14f);
            float variation =
                (broad - 0.5f) * 0.28f +
                (detail - 0.5f) * 0.10f;
            return roadHalfWidth *
                Mathf.Clamp(
                    1f + variation,
                    0.84f,
                    1.16f);
        }

        private bool TryClosestRoadPoint(
            Vector2 point,
            out Vector2 closest,
            out float distance)
        {
            bool found =
                TryClosestPoint(
                    point,
                    mainRoadSamples,
                    out closest,
                    out distance);
            TryUseCloserRoad(
                point,
                forkRoadSamples,
                ref found,
                ref closest,
                ref distance);
            TryUseCloserRoad(
                point,
                branchRoadASamples,
                ref found,
                ref closest,
                ref distance);
            TryUseCloserRoad(
                point,
                branchRoadBSamples,
                ref found,
                ref closest,
                ref distance);
            TryUseCloserRoad(
                point,
                branchRoadCSamples,
                ref found,
                ref closest,
                ref distance);
            return found;
        }

        private static void TryUseCloserRoad(
            Vector2 point,
            List<Vector3> road,
            ref bool found,
            ref Vector2 closest,
            ref float distance)
        {
            if (!TryClosestPoint(
                    point,
                    road,
                    out Vector2 roadClosest,
                    out float roadDistance) ||
                found && roadDistance >= distance)
            {
                return;
            }

            closest = roadClosest;
            distance = roadDistance;
            found = true;
        }

        private IEnumerable<List<Vector3>> AdditionalRoads()
        {
            if (forkRoadSamples.Count > 1)
            {
                yield return forkRoadSamples;
            }
            if (branchRoadASamples.Count > 1)
            {
                yield return branchRoadASamples;
            }
            if (branchRoadBSamples.Count > 1)
            {
                yield return branchRoadBSamples;
            }
            if (branchRoadCSamples.Count > 1)
            {
                yield return branchRoadCSamples;
            }
        }

        private IEnumerable<List<Vector3>> AllRoads()
        {
            if (mainRoadSamples.Count > 1)
            {
                yield return mainRoadSamples;
            }
            foreach (List<Vector3> road in AdditionalRoads())
            {
                yield return road;
            }
        }

        private static bool TryClosestPoint(
            Vector2 point,
            List<Vector3> line,
            out Vector2 closest,
            out float distance)
        {
            closest = Vector2.zero;
            distance = float.PositiveInfinity;
            if (line == null ||
                line.Count < 2)
            {
                return false;
            }

            for (int index = 0;
                 index < line.Count - 1;
                 index++)
            {
                Vector2 a = ToXZ(line[index]);
                Vector2 b = ToXZ(line[index + 1]);
                Vector2 segment = b - a;
                float denominator =
                    segment.sqrMagnitude;
                float t =
                    denominator > 0.0001f
                        ? Mathf.Clamp01(
                            Vector2.Dot(
                                point - a,
                                segment) /
                            denominator)
                        : 0f;
                Vector2 candidate =
                    a + segment * t;
                float candidateDistance =
                    Vector2.Distance(
                        point,
                        candidate);
                if (candidateDistance < distance)
                {
                    distance = candidateDistance;
                    closest = candidate;
                }
            }
            return true;
        }

        private static float DistanceToPolyline(
            Vector2 point,
            List<Vector3> line)
        {
            return TryClosestPoint(
                point,
                line,
                out _,
                out float distance)
                    ? distance
                    : float.PositiveInfinity;
        }

        private static bool TryFindClosestPair(
            List<Vector3> a,
            List<Vector3> b,
            out int indexA,
            out int indexB)
        {
            indexA = -1;
            indexB = -1;
            float best = float.PositiveInfinity;
            for (int first = 0;
                 first < a.Count;
                 first++)
            {
                for (int second = 0;
                     second < b.Count;
                     second++)
                {
                    float distance =
                        (ToXZ(a[first]) -
                         ToXZ(b[second]))
                        .sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        indexA = first;
                        indexB = second;
                    }
                }
            }
            return indexA >= 0;
        }

        private static void FindPolylineIntersections(
            List<Vector3> road,
            List<Vector3> river,
            List<TrailRiverCrossing> results)
        {
            for (int roadIndex = 0;
                 roadIndex < road.Count - 1;
                 roadIndex++)
            {
                Vector2 roadStart = ToXZ(road[roadIndex]);
                Vector2 roadDelta =
                    ToXZ(road[roadIndex + 1]) - roadStart;
                for (int riverIndex = 0;
                     riverIndex < river.Count - 1;
                     riverIndex++)
                {
                    Vector2 riverStart = ToXZ(river[riverIndex]);
                    Vector2 riverDelta =
                        ToXZ(river[riverIndex + 1]) - riverStart;
                    float denominator = Cross2D(
                        roadDelta,
                        riverDelta);
                    if (Mathf.Abs(denominator) <= 0.00001f)
                    {
                        continue;
                    }

                    Vector2 separation = riverStart - roadStart;
                    float roadT = Cross2D(
                        separation,
                        riverDelta) / denominator;
                    float riverT = Cross2D(
                        separation,
                        roadDelta) / denominator;
                    if (roadT < 0f || roadT > 1f ||
                        riverT < 0f || riverT > 1f)
                    {
                        continue;
                    }

                    Vector2 point = roadStart + roadDelta * roadT;
                    results.Add(new TrailRiverCrossing
                    {
                        Point = new Vector3(
                            point.x,
                            Mathf.Lerp(
                                road[roadIndex].y,
                                road[roadIndex + 1].y,
                                roadT),
                            point.y),
                        RoadDirection = new Vector3(
                            roadDelta.x,
                            0f,
                            roadDelta.y).normalized
                    });
                }
            }
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void SampleSpline(
            Vector3[] controlPoints,
            int samplesPerSegment,
            List<Vector3> output)
        {
            output.Clear();
            if (controlPoints == null ||
                controlPoints.Length == 0)
            {
                return;
            }
            if (controlPoints.Length == 1)
            {
                output.Add(controlPoints[0]);
                return;
            }

            for (int segment = 0;
                 segment < controlPoints.Length - 1;
                 segment++)
            {
                Vector3 p0 =
                    controlPoints[
                        Mathf.Max(0, segment - 1)];
                Vector3 p1 =
                    controlPoints[segment];
                Vector3 p2 =
                    controlPoints[segment + 1];
                Vector3 p3 =
                    controlPoints[
                        Mathf.Min(
                            controlPoints.Length - 1,
                            segment + 2)];
                for (int sample = 0;
                     sample < samplesPerSegment;
                     sample++)
                {
                    float t =
                        sample /
                        (float)samplesPerSegment;
                    output.Add(
                        CatmullRom(
                            p0,
                            p1,
                            p2,
                            p3,
                            t));
                }
            }
            output.Add(
                controlPoints[
                    controlPoints.Length - 1]);
        }

        private static Vector3 CatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f *
                ((2f * p1) +
                 (-p0 + p2) * t +
                 (2f * p0 -
                  5f * p1 +
                  4f * p2 -
                  p3) *
                 t2 +
                 (-p0 +
                  3f * p1 -
                  3f * p2 +
                  p3) *
                 t3);
        }

        private static bool HasNearbyTree(
            List<Vector2> points,
            Vector2 candidate,
            float spacing)
        {
            float spacingSquared =
                spacing * spacing;
            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                if ((points[index] - candidate)
                    .sqrMagnitude <
                    spacingSquared)
                {
                    return true;
                }
            }
            return false;
        }

        private static Material CreateTexturedMaterial(
            Material source,
            int seed,
            Color dark,
            Color light,
            float textureScale,
            bool preserveSourceTint)
        {
            Material material =
                source != null
                    ? new Material(source)
                    : new Material(
                        Shader.Find(
                            "Universal Render Pipeline/Lit"));
            material.name =
                source != null
                    ? $"{source.name} Runtime"
                    : "Procedural Raid Material";
            Texture texture =
                source != null &&
                source.mainTexture != null
                    ? source.mainTexture
                    : CreateNoiseTexture(
                        seed,
                        dark,
                        light);
            material.mainTexture = texture;
            material.mainTextureScale =
                Vector2.one * textureScale;
            Color materialTint =
                preserveSourceTint && source != null
                    ? source.color
                    : Color.white;
            material.color = materialTint;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture(
                    "_BaseMap",
                    texture);
                material.SetTextureScale(
                    "_BaseMap",
                    Vector2.one * textureScale);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    materialTint);
            }
            return material;
        }

        private static Material CreateTerrainBlendMaterial(
            Material ground,
            Material road)
        {
            Shader shader =
                Shader.Find(
                    "WorldBuilder/Terrain Road Blend Lit");
            if (shader == null)
            {
                return ground;
            }

            Material material = new Material(shader)
            {
                name = "Procedural Ground And Trail Blend"
            };
            Texture groundTexture =
                ground != null
                    ? ground.mainTexture
                    : Texture2D.whiteTexture;
            Texture roadTexture =
                road != null
                    ? road.mainTexture
                    : Texture2D.whiteTexture;
            material.SetTexture(
                "_GroundMap",
                groundTexture);
            material.SetTexture(
                "_RoadMap",
                roadTexture);
            material.SetTextureScale(
                "_GroundMap",
                ground != null
                    ? ground.mainTextureScale
                    : Vector2.one);
            material.SetTextureOffset(
                "_GroundMap",
                ground != null
                    ? ground.mainTextureOffset
                    : Vector2.zero);
            material.SetTextureScale(
                "_RoadMap",
                road != null
                    ? road.mainTextureScale
                    : Vector2.one);
            material.SetTextureOffset(
                "_RoadMap",
                road != null
                    ? road.mainTextureOffset
                    : Vector2.zero);
            material.SetColor(
                "_GroundColor",
                ground != null
                    ? ground.color
                    : Color.white);
            material.SetColor(
                "_RoadColor",
                road != null
                    ? road.color
                    : Color.white);
            return material;
        }

        private Material CreateRiverMaterial(
            Material source,
            int seed)
        {
            Material material =
                source != null
                    ? new Material(source)
                    : new Material(
                        Shader.Find(
                            "Universal Render Pipeline/Lit"));
            material.name =
                source != null
                    ? $"{source.name} Runtime"
                    : "Procedural River Material";

            float flowDirection = 1f;
            if (riverSamples.Count >= 2)
            {
                Vector3 start = riverSamples[0];
                Vector3 end =
                    riverSamples[
                        riverSamples.Count - 1];
                float startHeight =
                    TerrainHeight(
                        start.x,
                        start.z);
                float endHeight =
                    TerrainHeight(
                        end.x,
                        end.z);
                flowDirection =
                    Mathf.Abs(
                        startHeight - endHeight) >
                    0.025f
                        ? startHeight > endHeight
                            ? 1f
                            : -1f
                        : (seed & 1) == 0
                            ? 1f
                            : -1f;
            }

            if (material.HasProperty(
                    "_FlowDirection"))
            {
                material.SetFloat(
                    "_FlowDirection",
                    flowDirection);
            }
            if (material.HasProperty("_FlowPhase"))
            {
                var random =
                    new System.Random(seed);
                material.SetFloat(
                    "_FlowPhase",
                    (float)random.NextDouble());
            }
            return material;
        }

        private float MeadowDrynessAt(
            float worldX,
            float worldZ)
        {
            float broad =
                Mathf.PerlinNoise(
                    noiseOffsetA.x * 0.0019f +
                    worldX * 0.036f +
                    11.4f,
                    noiseOffsetA.y * 0.0019f +
                    worldZ * 0.036f +
                    27.8f);
            float detail =
                Mathf.PerlinNoise(
                    noiseOffsetB.x * 0.0031f +
                    worldX * 0.082f,
                    noiseOffsetB.y * 0.0031f +
                    worldZ * 0.082f);
            float dryness =
                Mathf.InverseLerp(
                    0.53f,
                    0.76f,
                    broad * 0.78f +
                    detail * 0.22f);
            return Mathf.SmoothStep(
                0f,
                1f,
                dryness);
        }

        private Color TerrainTintAt(
            float worldX,
            float worldZ)
        {
            float dryness =
                MeadowDrynessAt(
                    worldX,
                    worldZ);
            Color healthyEarth =
                new Color(
                    0.62f,
                    0.56f,
                    0.43f,
                    1f);
            Color wheatEarth =
                new Color(
                    0.69f,
                    0.58f,
                    0.39f,
                    1f);
            Color brown =
                new Color(
                    0.42f,
                    0.34f,
                    0.27f,
                    1f);
            return dryness < 0.72f
                ? Color.Lerp(
                    healthyEarth,
                    wheatEarth,
                    dryness / 0.72f)
                : Color.Lerp(
                    wheatEarth,
                    brown,
                    Mathf.InverseLerp(
                        0.72f,
                        1f,
                        dryness));
        }

        private Color TerrainBlendTintAt(
            float worldX,
            float worldZ)
        {
            float roadBlend =
                RoadSurfaceBlendAt(
                    worldX,
                    worldZ);
            Color meadowTint =
                TerrainTintAt(
                    worldX,
                    worldZ);
            Color blendedTint =
                Color.Lerp(
                    meadowTint,
                    Color.white,
                    roadBlend * 0.92f);
            blendedTint.a = roadBlend;
            return blendedTint;
        }

        private float RoadSurfaceBlendAt(
            float worldX,
            float worldZ)
        {
            float signedDistance =
                SignedDistanceToRoad(
                    new Vector2(
                        worldX,
                        worldZ));
            if (float.IsPositiveInfinity(
                    signedDistance))
            {
                return 0f;
            }

            float broadEdge =
                Mathf.PerlinNoise(
                    noiseOffsetA.x * 0.009f +
                    worldX * 0.19f +
                    6.7f,
                    noiseOffsetA.y * 0.009f +
                    worldZ * 0.19f +
                    19.2f);
            float brokenEdge =
                Mathf.PerlinNoise(
                    noiseOffsetB.x * 0.014f +
                    worldX * 0.52f +
                    31.8f,
                    noiseOffsetB.y * 0.014f +
                    worldZ * 0.52f +
                    12.4f);
            float irregularDistance =
                signedDistance +
                (broadEdge - 0.5f) * 0.82f +
                (brokenEdge - 0.5f) * 0.34f;
            float transition =
                Mathf.InverseLerp(
                    -0.78f,
                    1.38f,
                    irregularDistance);
            return 1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    transition);
        }

        private Color GrassTintAt(
            float worldX,
            float worldZ)
        {
            float dryness =
                MeadowDrynessAt(
                    worldX,
                    worldZ);
            Color mutedOlive =
                new Color(
                    0.69f,
                    0.72f,
                    0.57f,
                    1f);
            Color deadStraw =
                new Color(
                    0.76f,
                    0.68f,
                    0.46f,
                    1f);
            Color dryBrown =
                new Color(
                    0.48f,
                    0.42f,
                    0.31f,
                    1f);
            Color meadowTint = dryness < 0.72f
                ? Color.Lerp(
                    mutedOlive,
                    deadStraw,
                    dryness / 0.72f)
                : Color.Lerp(
                    deadStraw,
                    dryBrown,
                    Mathf.InverseLerp(
                        0.72f,
                        1f,
                        dryness));
            float trailVariation =
                Mathf.PerlinNoise(
                    noiseOffsetB.x * 0.012f +
                    worldX * 0.23f,
                    noiseOffsetB.y * 0.012f +
                    worldZ * 0.23f);
            Color trailGrassTint =
                Color.Lerp(
                    new Color(
                        0.68f,
                        0.50f,
                        0.31f,
                        1f),
                    new Color(
                        0.83f,
                        0.66f,
                        0.43f,
                        1f),
                    trailVariation);
            float roadBlend =
                RoadSurfaceBlendAt(
                    worldX,
                    worldZ);
            return Color.Lerp(
                meadowTint,
                trailGrassTint,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    roadBlend));
        }

        private static Texture2D CreateNoiseTexture(
            int seed,
            Color dark,
            Color light)
        {
            const int size = 64;
            var texture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGB24,
                    true)
                {
                    name = "Procedural Raid Surface",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
            var colors =
                new Color[size * size];
            var random =
                new System.Random(seed);
            float offsetX =
                random.Next(-10000, 10001);
            float offsetY =
                random.Next(-10000, 10001);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad =
                        Mathf.PerlinNoise(
                            offsetX + x * 0.08f,
                            offsetY + y * 0.08f);
                    float grain =
                        (float)random.NextDouble();
                    float blend =
                        Mathf.Clamp01(
                            broad * 0.78f +
                            grain * 0.22f);
                    colors[y * size + x] =
                        Color.Lerp(
                            dark,
                            light,
                            blend);
                }
            }
            texture.SetPixels(colors);
            texture.Apply(true, false);
            return texture;
        }

        private static Vector2 ToXZ(Vector3 point)
        {
            return new Vector2(
                point.x,
                point.z);
        }
    }
}
