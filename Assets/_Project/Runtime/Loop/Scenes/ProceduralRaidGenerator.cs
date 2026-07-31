using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Characters;
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
            public Vector3[] River;

            public Vector3 PlayerStart =>
                MainRoad != null && MainRoad.Length > 0
                    ? MainRoad[0]
                    : Vector3.zero;

            public Vector3 Extraction =>
                MainRoad != null && MainRoad.Length > 0
                    ? MainRoad[MainRoad.Length - 1]
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
        [SerializeField, Min(30f)] private float mapRadius = 72f;
        [SerializeField, Range(24, 128)] private int terrainResolution = 128;
        [SerializeField, Range(80, 700)] private int treeCount = 320;
        [SerializeField, Range(5000, 22000)] private int grassCount = 18000;
        [SerializeField, Range(40, 320)] private int undergrowthCount = 135;
        [SerializeField, Range(10, 120)] private int boulderCount = 48;
        [SerializeField, Range(10, 120)] private int trailStoneCount = 42;
        [SerializeField, Min(1f)] private float roadHalfWidth = 1.8f;
        [SerializeField, Min(1f)] private float riverHalfWidth = 3.1f;
        [SerializeField, Min(0.5f)] private float treeClearance = 5.8f;
        [SerializeField] private int fallbackSeed = 20260730;

        private const float RoadIndentation = 0.18f;
        private const float RoadShoulderWidth = 2.2f;
        private const int GrassPlacementsPerBatch = 320;

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

        private readonly List<Vector3> mainRoadSamples =
            new List<Vector3>();
        private readonly List<Vector3> forkRoadSamples =
            new List<Vector3>();
        private readonly List<Vector3> riverSamples =
            new List<Vector3>();
        private readonly List<Vector2> generatedTreePositions =
            new List<Vector2>();

        private RaidLayout layout;
        private Transform generatedRoot;
        private int generatedTreeCount;
        private int generatedGrassCount;
        private int generatedUndergrowthCount;
        private int generatedBoulderCount;
        private int generatedTrailStoneCount;
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
        public int GeneratedUndergrowthCount =>
            generatedUndergrowthCount;
        public int GeneratedBoulderCount =>
            generatedBoulderCount;
        public int GeneratedTrailStoneCount =>
            generatedTrailStoneCount;
        public RaidLayout CurrentLayout => layout;

        public void Configure(
            Transform playerRoot,
            EnemyBrain[] raidEnemies,
            ExtractionZone extraction,
            GameObject[] forestTreePrefabs,
            GameObject[] forestGrassPrefabs,
            GameObject[] forestUndergrowthPrefabs,
            GameObject[] forestRockPrefabs,
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

        [ContextMenu("Generate Raid")]
        public void Generate()
        {
            ConfigureRaidFog();
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
            riverSamples.Clear();
            SampleSpline(
                layout.MainRoad,
                5,
                mainRoadSamples);
            SampleSpline(
                layout.ForkRoad,
                5,
                forkRoadSamples);
            SampleSpline(
                layout.River,
                5,
                riverSamples);

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
                    7f);
            Material roadRuntime =
                CreateTexturedMaterial(
                    roadMaterial,
                    seed ^ 0x24680,
                    new Color(0.30f, 0.20f, 0.105f),
                    new Color(0.47f, 0.34f, 0.19f),
                    5f);
            Material waterRuntime =
                CreateTexturedMaterial(
                    waterMaterial,
                    seed ^ 0x55aa55,
                    new Color(0.055f, 0.23f, 0.31f),
                    new Color(0.12f, 0.43f, 0.52f),
                    8f);

            CreateTerrain(
                forestRuntime,
                roadRuntime);

            CreateRibbon(
                "River",
                riverSamples,
                riverHalfWidth,
                waterRuntime,
                false);
            if (layout.RiverCrossesRoad)
            {
                CreateBridge();
            }

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
                $"fork={layout.HasRoadFork}; " +
                $"crossing={layout.RiverCrossesRoad}");
        }

        public static RaidLayout CreateLayout(
            int seed,
            float radius)
        {
            var random = new System.Random(seed);
            const int roadPointCount = 17;
            var road = new Vector3[roadPointCount];
            float phase =
                Mathf.Lerp(
                    -1.1f,
                    1.1f,
                    (float)random.NextDouble());
            float secondaryPhase =
                Mathf.Lerp(
                    0f,
                    Mathf.PI * 2f,
                    (float)random.NextDouble());
            for (int index = 0;
                 index < roadPointCount;
                 index++)
            {
                float t =
                    index /
                    (roadPointCount - 1f);
                float z =
                    Mathf.Lerp(
                        -radius + 7f,
                        radius - 7f,
                        t);
                float x =
                    Mathf.Sin(t * Mathf.PI * 2.15f + phase) *
                    7.5f +
                    Mathf.Sin(
                        t * Mathf.PI * 4.1f +
                        secondaryPhase) *
                    2.4f;
                x += Mathf.Lerp(
                    -3.5f,
                    3.5f,
                    t);
                road[index] = new Vector3(x, 0f, z);
            }

            bool hasFork =
                random.NextDouble() < 0.52;
            Vector3[] fork = Array.Empty<Vector3>();
            if (hasFork)
            {
                const int forkPoints = 9;
                fork = new Vector3[forkPoints];
                int forkStart = 5;
                int forkEnd = 12;
                float side =
                    random.NextDouble() < 0.5
                        ? -1f
                        : 1f;
                float offset =
                    Mathf.Lerp(
                        9f,
                        14f,
                        (float)random.NextDouble());
                for (int index = 0;
                     index < forkPoints;
                     index++)
                {
                    float t =
                        index /
                        (forkPoints - 1f);
                    Vector3 basePoint =
                        Vector3.Lerp(
                            road[forkStart],
                            road[forkEnd],
                            t);
                    float bow =
                        Mathf.Sin(t * Mathf.PI) *
                        offset *
                        side;
                    fork[index] =
                        basePoint +
                        Vector3.right * bow;
                }
            }

            bool crosses =
                random.NextDouble() < 0.62;
            Vector3[] river;
            if (crosses)
            {
                const int riverPointCount = 15;
                river =
                    new Vector3[riverPointCount];
                float crossingZ =
                    Mathf.Lerp(
                        -radius * 0.22f,
                        radius * 0.30f,
                        (float)random.NextDouble());
                float riverPhase =
                    Mathf.Lerp(
                        0f,
                        Mathf.PI * 2f,
                        (float)random.NextDouble());
                for (int index = 0;
                     index < riverPointCount;
                     index++)
                {
                    float t =
                        index /
                        (riverPointCount - 1f);
                    float x =
                        Mathf.Lerp(
                            -radius + 2f,
                            radius - 2f,
                            t);
                    float z =
                        crossingZ +
                        Mathf.Sin(
                            t * Mathf.PI * 2f +
                            riverPhase) *
                        5.5f;
                    river[index] =
                        new Vector3(x, 0f, z);
                }
            }
            else
            {
                const int riverPointCount = 17;
                river =
                    new Vector3[riverPointCount];
                float side =
                    random.NextDouble() < 0.5
                        ? -1f
                        : 1f;
                float separation =
                    Mathf.Lerp(
                        13f,
                        18f,
                        (float)random.NextDouble());
                for (int index = 0;
                     index < riverPointCount;
                     index++)
                {
                    float t =
                        index /
                        (riverPointCount - 1f);
                    Vector3 basePoint =
                        road[index];
                    float meander =
                        Mathf.Sin(t * Mathf.PI * 3f) *
                        3.5f;
                    river[index] =
                        new Vector3(
                            basePoint.x +
                            side *
                            (separation + meander),
                            0f,
                            basePoint.z);
                }
            }

            return new RaidLayout
            {
                Seed = seed,
                HasRoadFork = hasFork,
                RiverCrossesRoad = crosses,
                MainRoad = road,
                ForkRoad = fork,
                River = river
            };
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
            terrain.AddComponent<MeshRenderer>()
                .sharedMaterials =
                new[]
                {
                    groundMaterial,
                    dirtRoadMaterial
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

            var vertices =
                new Vector3[points.Count * 2];
            var uv =
                new Vector2[vertices.Length];
            var triangles =
                new int[(points.Count - 1) * 6];
            float distanceAlong = 0f;
            for (int index = 0;
                 index < points.Count;
                 index++)
            {
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
                vertices[index * 2] =
                    center - right * halfWidth;
                vertices[index * 2 + 1] =
                    center + right * halfWidth;
                if (index > 0)
                {
                    distanceAlong += Vector3.Distance(
                        points[index - 1],
                        points[index]);
                }
                uv[index * 2] =
                    new Vector2(0f, distanceAlong / 5f);
                uv[index * 2 + 1] =
                    new Vector2(1f, distanceAlong / 5f);
            }

            for (int index = 0;
                 index < points.Count - 1;
                 index++)
            {
                int triangle = index * 6;
                int vertex = index * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 3;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }

            Mesh mesh = new Mesh
            {
                name = name
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
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

        private void CreateBridge()
        {
            if (!TryFindClosestPair(
                    mainRoadSamples,
                    riverSamples,
                    out int roadIndex,
                    out _))
            {
                return;
            }

            Vector3 point =
                mainRoadSamples[roadIndex];
            Vector3 previous =
                mainRoadSamples[
                    Mathf.Max(0, roadIndex - 1)];
            Vector3 next =
                mainRoadSamples[
                    Mathf.Min(
                        mainRoadSamples.Count - 1,
                        roadIndex + 1)];
            Vector3 direction =
                Vector3.ProjectOnPlane(
                    next - previous,
                    Vector3.up).normalized;
            GameObject bridge =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
            bridge.name = "Road Bridge";
            bridge.transform.SetParent(
                generatedRoot,
                false);
            bridge.transform.position =
                new Vector3(
                    point.x,
                    RoadSurfaceHeight(
                        point.x,
                        point.z) +
                    -0.07f,
                    point.z);
            bridge.transform.rotation =
                Quaternion.LookRotation(
                    direction,
                    Vector3.up);
            bridge.transform.localScale =
                new Vector3(
                    roadHalfWidth * 2f + 1.1f,
                    0.34f,
                    riverHalfWidth * 2.8f + 4f);
            bridge.GetComponent<Renderer>()
                .sharedMaterial = bridgeMaterial;
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
                    tree.transform.position +=
                        Vector3.up *
                        (terrainHeight -
                         scaledBounds.min.y);
                }

                CapsuleCollider trunk =
                    tree.AddComponent<CapsuleCollider>();
                float worldScale =
                    Mathf.Max(
                        0.0001f,
                        tree.transform.lossyScale.y);
                float trunkWorldHeight =
                    targetHeight * 0.68f;
                trunk.radius =
                    0.55f / worldScale;
                trunk.height =
                    trunkWorldHeight / worldScale;
                trunk.center =
                    tree.transform.InverseTransformPoint(
                        new Vector3(
                            tree.transform.position.x,
                            terrainHeight +
                            trunkWorldHeight * 0.5f,
                            tree.transform.position.z));
                generatedTreePositions.Add(point);
            generatedTreeCount++;
            }
        }

        private void CreateGroundScenery(
            System.Random random)
        {
            generatedGrassCount = 0;
            generatedUndergrowthCount = 0;
            generatedBoulderCount = 0;
            generatedTrailStoneCount = 0;

            List<GameObject> grasses =
                CollectValidPrefabs(grassPrefabs);
            List<GameObject> undergrowth =
                CollectValidPrefabs(
                    undergrowthPrefabs);
            List<GameObject> rocks =
                CollectValidPrefabs(rockPrefabs);

            if (grasses.Count > 0)
            {
                CreateGrassCoverage(
                    random,
                    grasses);
            }
            if (undergrowth.Count > 0)
            {
                CreateUndergrowth(
                    random,
                    undergrowth);
            }
            if (rocks.Count > 0)
            {
                CreateBoulders(
                    random,
                    rocks);
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
            int attempts = grassCount * 6;
            for (int attempt = 0;
                 attempt < attempts &&
                 generatedGrassCount < grassCount;
                 attempt++)
            {
                Vector2 point =
                    RandomDiscPoint(
                        random,
                        2.5f);
                if (SignedDistanceToRoad(point) <
                        0.28f ||
                    DistanceToPolyline(
                        point,
                        riverSamples) <
                        riverHalfWidth + 0.7f)
                {
                    continue;
                }

                float patch =
                    Mathf.PerlinNoise(
                        noiseOffsetA.x * 0.017f +
                        point.x * 0.075f,
                        noiseOffsetA.y * 0.017f +
                        point.y * 0.075f);
                float lowerZone =
                    Mathf.InverseLerp(
                        3.5f,
                        -3.5f,
                        RawLandHeight(
                            point.x,
                            point.y));
                float density =
                    Mathf.Clamp01(
                        0.46f +
                        patch * 0.50f +
                        lowerZone * 0.16f);
                if ((float)random.NextDouble() >
                    density)
                {
                    continue;
                }

                GrassMeshSource source =
                    sources[
                        generatedGrassCount <
                            sources.Count
                            ? generatedGrassCount %
                                sources.Count
                            : random.Next(
                                sources.Count)];
                float heightPatch =
                    Mathf.PerlinNoise(
                        noiseOffsetB.x * 0.011f +
                        point.x * 0.034f,
                        noiseOffsetB.y * 0.011f +
                        point.y * 0.034f);
                float targetHeight =
                    Mathf.Lerp(
                        0.20f,
                        0.74f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            heightPatch)) *
                    Mathf.Lerp(
                        0.86f,
                        1.14f,
                        (float)random.NextDouble());
                float uniformScale =
                    targetHeight /
                    Mathf.Max(
                        0.001f,
                        source.LocalBounds.size.y);
                float footprintScale =
                    uniformScale *
                    Mathf.Lerp(
                        1.65f,
                        2.20f,
                        (float)random.NextDouble());
                Quaternion rotation =
                    Quaternion.AngleAxis(
                        (float)random.NextDouble() *
                        360f,
                        Vector3.up) *
                    source.ImportedRotation;
                float terrainHeight =
                    TerrainHeight(
                        point.x,
                        point.y);
                Vector3 scale =
                    new Vector3(
                        footprintScale,
                        uniformScale,
                        footprintScale);
                Vector3 position =
                    new Vector3(
                        point.x,
                        terrainHeight -
                            source.LocalBounds.min.y *
                            uniformScale -
                            0.012f,
                        point.y);
                Matrix4x4 placement =
                    Matrix4x4.TRS(
                        position,
                        rotation,
                        scale);
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

                generatedGrassCount++;
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
            var accepted =
                new List<Vector2>(
                    undergrowthCount);
            int attempts =
                undergrowthCount * 10;
            for (int attempt = 0;
                 attempt < attempts &&
                 generatedUndergrowthCount <
                    undergrowthCount;
                 attempt++)
            {
                Vector2 point =
                    RandomDiscPoint(
                        random,
                        3f);
                if (SignedDistanceToRoad(point) <
                        0.85f ||
                    DistanceToPolyline(
                        point,
                        riverSamples) <
                        riverHalfWidth + 0.5f ||
                    HasNearbyTree(
                        accepted,
                        point,
                        1.35f))
                {
                    continue;
                }

                GameObject prefab =
                    prefabs[
                        generatedUndergrowthCount <
                            prefabs.Count
                            ? generatedUndergrowthCount %
                                prefabs.Count
                            : random.Next(
                                prefabs.Count)];
                float targetHeight =
                    UndergrowthHeight(
                        prefab.name,
                        random);
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
                    $"{prefab.name} " +
                    $"{generatedUndergrowthCount + 1:000}";
                accepted.Add(point);
                generatedUndergrowthCount++;
            }
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
                        true);
                if (boulder == null)
                {
                    continue;
                }

                boulder.name =
                    $"{prefab.name} Boulder " +
                    $"{generatedBoulderCount + 1:00}";
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
                        false);
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
            bool castShadows)
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
            instance.transform.position =
                new Vector3(
                    point.x,
                    terrainHeight,
                    point.y);
            Quaternion importedRotation =
                instance.transform.rotation;
            instance.transform.rotation =
                Quaternion.AngleAxis(
                    (float)random.NextDouble() *
                    360f,
                    Vector3.up) *
                importedRotation;

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
            instance.transform.position +=
                Vector3.up *
                (terrainHeight -
                 scaledBounds.min.y -
                 0.015f);

            if (addCollider &&
                TryGetRendererBounds(
                    renderers,
                    out Bounds finalBounds))
            {
                BoxCollider collider =
                    instance.AddComponent<BoxCollider>();
                collider.center =
                    instance.transform
                        .InverseTransformPoint(
                            finalBounds.center);
                Vector3 scale =
                    instance.transform.lossyScale;
                collider.size =
                    new Vector3(
                        finalBounds.size.x /
                            Mathf.Max(
                                0.0001f,
                                Mathf.Abs(scale.x)),
                        finalBounds.size.y /
                            Mathf.Max(
                                0.0001f,
                                Mathf.Abs(scale.y)),
                        finalBounds.size.z /
                            Mathf.Max(
                                0.0001f,
                                Mathf.Abs(scale.z)));
            }
            return instance;
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

        private static void ConfigureRaidFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor =
                new Color(
                    0.72f,
                    0.74f,
                    0.75f,
                    1f);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 62f;
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
                RoadPointAt(0.02f);
            Vector3 extraction =
                RoadPointAt(0.98f);
            MoveActor(
                player,
                RoadSurfacePoint(start, 1f));
            if (player != null)
            {
                Vector3 forward =
                    RoadPointAt(0.04f) -
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
                float[] positions =
                {
                    0.28f,
                    0.51f,
                    0.74f
                };
                for (int index = 0;
                     index < enemies.Length &&
                     index < positions.Length;
                     index++)
                {
                    EnemyBrain enemy = enemies[index];
                    if (enemy == null)
                    {
                        continue;
                    }

                    float t =
                        Mathf.Clamp01(
                            positions[index] +
                            Mathf.Lerp(
                                -0.035f,
                                0.035f,
                                (float)random.NextDouble()));
                    Vector3 spawn =
                        RoadPointAt(t);
                    MoveActor(
                        enemy.transform,
                        RoadSurfacePoint(spawn, 1f));
                    Vector3 forward =
                        RoadPointAt(
                            Mathf.Min(1f, t + 0.02f)) -
                        spawn;
                    enemy.transform.rotation =
                        Quaternion.LookRotation(
                            Vector3.ProjectOnPlane(
                                forward,
                                Vector3.up),
                            Vector3.up);
                    Vector3[] route =
                        BuildPatrolRoute(
                            Mathf.Max(0f, t - 0.11f),
                            Mathf.Min(1f, t + 0.11f),
                            7);
                    enemy.ConfigurePatrolRoute(
                        route,
                        route.Length / 2);
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
                        RoadPointAt(t),
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
            if (mainRoadSamples.Count == 0)
            {
                return Vector3.zero;
            }

            float scaled =
                Mathf.Clamp01(t) *
                (mainRoadSamples.Count - 1);
            int first =
                Mathf.FloorToInt(scaled);
            int second =
                Mathf.Min(
                    mainRoadSamples.Count - 1,
                    first + 1);
            return Vector3.Lerp(
                mainRoadSamples[first],
                mainRoadSamples[second],
                scaled - first);
        }

        private float DistanceToRoad(Vector2 point)
        {
            float main =
                DistanceToPolyline(
                    point,
                    mainRoadSamples);
            float fork =
                layout != null &&
                layout.HasRoadFork
                    ? DistanceToPolyline(
                        point,
                        forkRoadSamples)
                    : float.PositiveInfinity;
            return Mathf.Min(main, fork);
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
            if (layout != null &&
                layout.HasRoadFork &&
                TryClosestPoint(
                    point,
                    forkRoadSamples,
                    out Vector2 forkClosest,
                    out float forkDistance) &&
                (!found || forkDistance < distance))
            {
                closest = forkClosest;
                distance = forkDistance;
                found = true;
            }
            return found;
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
            float textureScale)
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
            material.color = Color.white;
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
                    Color.white);
            }
            return material;
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
