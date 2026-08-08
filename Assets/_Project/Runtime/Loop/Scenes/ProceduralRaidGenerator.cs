using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    public sealed class ProceduralRaidGenerator : MonoBehaviour
    {
        public enum ForestHabitat
        {
            MossyLoam,
            CanopyDuff,
            MossCarpet,
            CreepingGroundcover,
            StonyLichenSoil
        }

        public enum ForestFloorDebugMode
        {
            None,
            DominantHabitat,
            MossyLoamWeight,
            CanopyDuffWeight,
            MossCarpetWeight,
            CreepingGroundcoverWeight,
            StonyLichenSoilWeight,
            GrassDensity,
            CanopyInfluence,
            BoulderInfluence,
            MoistureTendency,
            FoliageColonies
        }

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
            public Vector3 PlayerSpawn;
            public Vector3 ExtractionPoint;
            public float[] CoastRadii;
            public float MaximumCoastRadius;

            public Vector3 PlayerStart => PlayerSpawn;

            public Vector3 Extraction => ExtractionPoint;

            public float CoastRadiusAtAngle(float angle)
            {
                return SampleIslandCoastRadius(
                    CoastRadii,
                    angle);
            }
        }

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private EnemyBrain[] enemies;
        [SerializeField] private ExtractionZone extractionZone;
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject[] grassPrefabs;
        [SerializeField] private GameObject[] undergrowthPrefabs;
        [SerializeField] private GameObject[] groundFloraStudyPrefabs;
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField] private GameObject bridgePrefab;
        [Header("Forest Camps")]
        [SerializeField] private EnemyBrain[] campGuardPool;
        [SerializeField] private GameObject campTentPrefab;
        [SerializeField] private GameObject campfirePrefab;
        [SerializeField] private GameObject campPotPrefab;
        [SerializeField] private GameObject campDryingRackPrefab;
        [SerializeField] private GameObject campFirewoodPrefab;
        [SerializeField] private GameObject campChestPrefab;
        [SerializeField] private GameObject campBenchPrefab;
        [SerializeField] private GameObject campBarrelPrefab;
        [SerializeField] private GameObject campWoodenBoxPrefab;
        [SerializeField] private GameObject campOuterSpikePrefabA;
        [SerializeField] private GameObject campOuterSpikePrefabB;
        [SerializeField] private GameObject campInnerBarricadePrefabA;
        [SerializeField] private GameObject campInnerBarricadePrefabB;
        [SerializeField] private Mesh campSwordBladeMesh;
        [SerializeField] private Material campSwordBladeMaterial;
        [SerializeField] private Material campSwordGuardMaterial;
        [SerializeField] private Material campSwordGripMaterial;
        [SerializeField] private Material campStructureMaterial;
        [SerializeField] private Material campItemMaterial;
        [Header("Materials")]
        [SerializeField] private Material forestGroundMaterial;
        [SerializeField] private Material bareGroundMaterial;
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
        [SerializeField] private Material skyboxMaterial;
        [Header("Forest Habitat Textures")]
        [SerializeField] private Texture2D mossyLoamTexture;
        [SerializeField] private Texture2D canopyDuffTexture;
        [SerializeField] private Texture2D mossCarpetTexture;
        [SerializeField] private Texture2D creepingGroundcoverTexture;
        [SerializeField] private Texture2D stonyLichenSoilTexture;
        [Header("Forest Habitat Field")]
        [SerializeField, Range(65, 257)] private int habitatFieldResolution = 129;
        [SerializeField, Range(8f, 28f)] private float macroPatchScale = 17f;
        [SerializeField, Range(4f, 14f)] private float secondaryPatchScale = 7.5f;
        [SerializeField, Range(3f, 10f)] private float canopyInfluenceRadius = 6.4f;
        [SerializeField, Range(2f, 9f)] private float boulderInfluenceRadius = 5.8f;
        [SerializeField, Range(1.2f, 4f)] private float habitatWeightSharpness = 2.35f;
        [SerializeField, Range(1f, 12f)] private float habitatTextureTiling = 6.5f;
        [SerializeField, Range(0.5f, 2.5f)] private float habitatBrightness = 1.55f;
        [SerializeField, Range(1f, 3f)] private float habitatBlendContrast = 1.35f;
        [SerializeField] private ForestFloorDebugMode forestFloorDebugMode;
        [Header("Generation")]
        [SerializeField, Min(30f)] private float mapRadius = 144f;
        [SerializeField, Range(24, 384)] private int terrainResolution = 256;
        [SerializeField, Range(2f, 10f)] private float regionalElevationAmplitude = 6.2f;
        [SerializeField, Range(1f, 7f)] private float directionalElevationRise = 4.2f;
        [SerializeField, Range(80, 1800)] private int treeCount = 1200;
        [SerializeField, Range(0.5f, 3f)] private float treeScaleMultiplier = 1.75f;
        [SerializeField, Range(8000, 140000)] private int grassCount = 128000;
        [SerializeField, Range(40, 6000)] private int undergrowthCount = 4200;
        [SerializeField, Range(400, 8000)] private int groundFloraStudyCount = 4800;
        [SerializeField, Range(3f, 9f)] private float groundFloraColonySpacing = 5.4f;
        [SerializeField, Range(0.45f, 0.8f)] private float groundFloraGeneralShare = 0.70f;
        [SerializeField, Range(0.05f, 0.3f)] private float groundFloraTreePocketShare = 0.18f;
        [SerializeField, Range(10, 260)] private int boulderCount = 192;
        [SerializeField, Range(10, 240)] private int trailStoneCount = 168;
        [SerializeField, Min(1f)] private float roadHalfWidth = 1.8f;
        [SerializeField, Min(1f)] private float riverHalfWidth = 3.1f;
        [SerializeField, Min(0.5f)] private float treeClearance = 5.8f;
        [SerializeField] private int fallbackSeed = 20260730;

        private const float RoadIndentation = 0.18f;
        private const float RoadShoulderWidth = 2.2f;
        private const float GrassRoadInteriorLimit = -1.35f;
        private const float GrassRiverClearance = 0.78f;
        private const float RiverWaterBankOverlap = 1.15f;
        private const int IslandCoastSampleCount = 256;
        private const float CoastSandWidth = 8.5f;
        private const float CoastPlacementInset = 3.2f;
        private const float CoastBarrierInset = 0.65f;
        private const float OceanDepthBelowShore = 0.72f;
        private const float OceanVisualRadiusMultiplier = 5f;
        private const float OceanShoreOverlap = 1.15f;
        private const int CoastBarrierSegments = 128;
        private const int IslandShapeSeedSalt = 0x35a91c7;
        private const float BridgeCrossSectionScale = 0.46f;
        private const float BridgeExtraWidthScale = 1.65f;
        private const float BridgeDeckLift = 0.35f;
        private const float MinimumBridgeSeparation = 26f;
        private const float MinimumForkRiverClearance = 18f;
        private const float MinimumRouteDestinationSeparation = 52f;
        private const float MinimumDestinationAngle = 0.52f;
        private const float MinimumBranchDepartureAngle = 0.58f;
        private const float ParallelRouteClearance = 15f;
        private const float CrossingStraightHalfLength = 8f;
        private const float CrossingApproachHalfLength = 17f;
        public const float MinimumGuardPatrolSeparation = 26f;
        private const float GuardPatrolHalfLength = 18f;
        private const int GuardPlacementAttempts = 128;
        private const int GrassPlacementsPerBatch = 768;
        public const float GrassCoverageMultiplier = 2f;
        private const float EnvironmentChunkSize = 20f;
        // Local placement and surface rules only care about splines inside
        // this radius. Queries outside it intentionally return infinity;
        // review-facing measurements use the uncapped exact query instead.
        // Raise this before introducing any local rule with a larger range.
        private const float LocalSplineQueryDistance = 8f;
        private const float OuterSpawnInnerRadiusRatio = 0.70f;
        private const float OuterSpawnOuterRadiusRatio = 0.86f;
        private const float SpawnRiverClearance = 8f;
        private const float SpawnSolidSceneryClearance = 7.5f;
        private const float ExtractionOppositeArc = 0.70f;
        private const int OuterSpawnPlacementAttempts = 64;
        private const int MinimumCampCount = 2;
        private const int MaximumCampCount = 4;
        private const int MaximumCampGuardCount = 3;
        private const int CampPlacementAttempts = 320;
        private const float MinimumCampSeparation = 34f;
        private const float CampTrailMinimumDistance = 9f;
        private const float CampTrailMaximumDistance = 34f;
        private const float CampRiverClearance = 13f;
        public const float FireflyMapChance = 0.14f;
        private const int FireflySeedSalt = 0x51f17e;
        private const int FireflyPlacementAttempts = 72;
        private const float FireflyZoneRadius = 5.2f;
        private const float LevelTwoCampChance = 0.5f;
        private const float LevelTwoCampClearingRadius = 17.2f;
        private const float LevelTwoBenchTargetSize = 1.7625f;
        private const float CookingSpitOverFireChance = 0.35f;
        private const float CookingSpitNearFireDistance = 1.65f;
        private const float LevelOneFirewoodEdgeInset = 1.8f;
        public const float LevelOneWoodenBoxChance = 0.5f;
        private const float CampWoodenBoxTargetSize = 1.45f;
        public const float ObeliskTreeClearance = 6f;
        private const float ObeliskBoulderClearance = 2.6f;
        private const float ObeliskRiverClearance = 5f;

        private sealed class GrassMeshSource
        {
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
            public Vector2 ShelterDirection;
        }

        private sealed class CampSite
        {
            public Vector2 Center;
            public float Rotation;
            public float ClearingRadius;
            public float GroundHeight;
            public Vector2 GroundSlope;
            public int GuardCount;
            public int TentCount;
            public bool IsLevelTwo;
            public bool[] BowGuards;
            public int WoodenBoxCount;
        }

        private struct HabitatSample
        {
            public Vector4 PrimaryWeights;
            public float StonyWeight;
            public float GrassDensity;
            public float CanopyInfluence;
            public float BoulderInfluence;
            public float MoistureTendency;

            public float Weight(int index)
            {
                return index == 0
                    ? PrimaryWeights.x
                    : index == 1
                        ? PrimaryWeights.y
                        : index == 2
                            ? PrimaryWeights.z
                            : index == 3
                                ? PrimaryWeights.w
                                : StonyWeight;
            }

            public int DominantIndex
            {
                get
                {
                    int dominant = 0;
                    float maximum = PrimaryWeights.x;
                    for (int index = 1; index < 5; index++)
                    {
                        float weight = Weight(index);
                        if (weight <= maximum)
                        {
                            continue;
                        }
                        maximum = weight;
                        dominant = index;
                    }
                    return dominant;
                }
            }
        }

        private struct TrailRiverCrossing
        {
            public Vector3 Point;
            public Vector3 RoadDirection;
            public Vector3 RiverDirection;
        }

        private struct BridgeNavigationRoute
        {
            public Vector2 Center;
            public Vector2 AcrossDirection;
            public float HalfLength;
            public float HalfWidth;
            public Transform BridgeRoot;
            public float ReferenceDeckHeight;
        }

        private sealed class PointSpatialHash
        {
            private readonly float cellSize;
            private readonly Dictionary<Vector2Int,
                List<int>> cells =
                    new Dictionary<Vector2Int,
                        List<int>>();
            private readonly List<Vector2> points =
                new List<Vector2>();

            public PointSpatialHash(float size)
            {
                cellSize = Mathf.Max(0.1f, size);
            }

            public bool HasNearby(
                Vector2 candidate,
                float spacing)
            {
                float spacingSquared = spacing * spacing;
                Vector2Int center = Cell(candidate);
                int radius = Mathf.CeilToInt(
                    spacing / cellSize);
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (!cells.TryGetValue(
                                center + new Vector2Int(x, y),
                                out List<int> pointIndices))
                        {
                            continue;
                        }
                        foreach (int pointIndex in pointIndices)
                        {
                            if ((points[pointIndex] - candidate)
                                    .sqrMagnitude <
                                spacingSquared)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            public void Add(Vector2 point)
            {
                Vector2Int cell = Cell(point);
                if (!cells.TryGetValue(
                        cell,
                        out List<int> pointIndices))
                {
                    pointIndices = new List<int>();
                    cells.Add(cell, pointIndices);
                }
                pointIndices.Add(points.Count);
                points.Add(point);
            }

            public void CollectNearbyIndices(
                Vector2 candidate,
                float spacing,
                List<int> results)
            {
                results.Clear();
                float spacingSquared = spacing * spacing;
                Vector2Int center = Cell(candidate);
                int radius = Mathf.CeilToInt(
                    spacing / cellSize);
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (!cells.TryGetValue(
                                center + new Vector2Int(x, y),
                                out List<int> pointIndices))
                        {
                            continue;
                        }
                        foreach (int pointIndex in pointIndices)
                        {
                            if ((points[pointIndex] - candidate)
                                    .sqrMagnitude <
                                spacingSquared)
                            {
                                results.Add(pointIndex);
                            }
                        }
                    }
                }
                results.Sort();
            }

            public float MaximumLinearInfluence(
                Vector2 candidate,
                float spacing)
            {
                float maximumInfluence = 0f;
                float spacingSquared = spacing * spacing;
                Vector2Int center = Cell(candidate);
                int radius = Mathf.CeilToInt(
                    spacing / cellSize);
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (!cells.TryGetValue(
                                center + new Vector2Int(x, y),
                                out List<int> pointIndices))
                        {
                            continue;
                        }
                        foreach (int pointIndex in pointIndices)
                        {
                            float distanceSquared =
                                (points[pointIndex] - candidate)
                                .sqrMagnitude;
                            if (distanceSquared >= spacingSquared)
                            {
                                continue;
                            }
                            maximumInfluence = Mathf.Max(
                                maximumInfluence,
                                1f - Mathf.Sqrt(distanceSquared) /
                                spacing);
                        }
                    }
                }
                return maximumInfluence;
            }

            private Vector2Int Cell(Vector2 point)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(point.x / cellSize),
                    Mathf.FloorToInt(point.y / cellSize));
            }
        }

        private sealed class PolylineQuery
        {
            private const float CellSize = 12f;

            private struct Segment
            {
                public Vector2 A;
                public Vector2 Delta;
                public float LengthSquared;
                public float MinimumX;
                public float MaximumX;
                public float MinimumY;
                public float MaximumY;
            }

            private readonly List<Segment> segments =
                new List<Segment>();
            private readonly Dictionary<Vector2Int, List<int>> cells =
                new Dictionary<Vector2Int, List<int>>();
            private int[] visitStamps = Array.Empty<int>();
            private int visitStamp;
            private readonly List<int> querySegmentIndices =
                new List<int>(64);

            public int Count => segments.Count;

            public void Clear()
            {
                segments.Clear();
                cells.Clear();
                visitStamp = 0;
            }

            public void Add(List<Vector3> line)
            {
                if (line == null || line.Count < 2)
                {
                    return;
                }

                for (int index = 0;
                     index < line.Count - 1;
                     index++)
                {
                    Vector2 a = ToXZ(line[index]);
                    Vector2 b = ToXZ(line[index + 1]);
                    Vector2 delta = b - a;
                    float lengthSquared =
                        delta.sqrMagnitude;
                    int segmentIndex = segments.Count;
                    var segment = new Segment
                        {
                            A = a,
                            Delta = delta,
                            LengthSquared = lengthSquared,
                            MinimumX = Mathf.Min(a.x, b.x),
                            MaximumX = Mathf.Max(a.x, b.x),
                            MinimumY = Mathf.Min(a.y, b.y),
                            MaximumY = Mathf.Max(a.y, b.y)
                        };
                    segments.Add(segment);
                    AddToCells(segmentIndex, segment);
                }
                if (visitStamps.Length < segments.Count)
                {
                    Array.Resize(
                        ref visitStamps,
                        Mathf.NextPowerOfTwo(segments.Count));
                }
            }

            private void AddToCells(
                int segmentIndex,
                Segment segment)
            {
                int minimumX = Mathf.FloorToInt(
                    segment.MinimumX / CellSize);
                int maximumX = Mathf.FloorToInt(
                    segment.MaximumX / CellSize);
                int minimumY = Mathf.FloorToInt(
                    segment.MinimumY / CellSize);
                int maximumY = Mathf.FloorToInt(
                    segment.MaximumY / CellSize);
                for (int y = minimumY; y <= maximumY; y++)
                {
                    for (int x = minimumX; x <= maximumX; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!cells.TryGetValue(
                                cell,
                                out List<int> cellSegments))
                        {
                            cellSegments = new List<int>(8);
                            cells.Add(cell, cellSegments);
                        }
                        cellSegments.Add(segmentIndex);
                    }
                }
            }

            public bool TryClosestPoint(
                Vector2 point,
                out Vector2 closest,
                out float distance)
            {
                closest = Vector2.zero;
                distance = float.PositiveInfinity;
                if (segments.Count == 0)
                {
                    return false;
                }

                float bestDistanceSquared =
                    float.PositiveInfinity;
                for (int index = 0;
                     index < segments.Count;
                     index++)
                {
                    Segment segment = segments[index];
                    float boundsOffsetX =
                        point.x < segment.MinimumX
                            ? segment.MinimumX - point.x
                            : point.x > segment.MaximumX
                                ? point.x - segment.MaximumX
                                : 0f;
                    float boundsOffsetY =
                        point.y < segment.MinimumY
                            ? segment.MinimumY - point.y
                            : point.y > segment.MaximumY
                                ? point.y - segment.MaximumY
                                : 0f;
                    if (boundsOffsetX * boundsOffsetX +
                            boundsOffsetY * boundsOffsetY >=
                        bestDistanceSquared)
                    {
                        continue;
                    }

                    float t = segment.LengthSquared > 0.0001f
                        ? Mathf.Clamp01(
                            Vector2.Dot(
                                point - segment.A,
                                segment.Delta) /
                            segment.LengthSquared)
                        : 0f;
                    Vector2 candidate =
                        segment.A + segment.Delta * t;
                    float candidateDistanceSquared =
                        (point - candidate).sqrMagnitude;
                    if (candidateDistanceSquared >=
                        bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared =
                        candidateDistanceSquared;
                    closest = candidate;
                }

                distance = Mathf.Sqrt(bestDistanceSquared);
                return true;
            }

            public bool TryClosestPointWithin(
                Vector2 point,
                float maximumDistance,
                out Vector2 closest,
                out float distance)
            {
                closest = Vector2.zero;
                distance = float.PositiveInfinity;
                if (segments.Count == 0 || maximumDistance < 0f)
                {
                    return false;
                }

                visitStamp++;
                if (visitStamp == int.MaxValue)
                {
                    Array.Clear(
                        visitStamps,
                        0,
                        visitStamps.Length);
                    visitStamp = 1;
                }

                float bestDistanceSquared =
                    maximumDistance * maximumDistance;
                querySegmentIndices.Clear();
                int minimumX = Mathf.FloorToInt(
                    (point.x - maximumDistance) / CellSize);
                int maximumX = Mathf.FloorToInt(
                    (point.x + maximumDistance) / CellSize);
                int minimumY = Mathf.FloorToInt(
                    (point.y - maximumDistance) / CellSize);
                int maximumY = Mathf.FloorToInt(
                    (point.y + maximumDistance) / CellSize);
                bool found = false;
                for (int y = minimumY; y <= maximumY; y++)
                {
                    for (int x = minimumX; x <= maximumX; x++)
                    {
                        if (!cells.TryGetValue(
                                new Vector2Int(x, y),
                                out List<int> cellSegments))
                        {
                            continue;
                        }

                        for (int cellIndex = 0;
                             cellIndex < cellSegments.Count;
                             cellIndex++)
                        {
                            int segmentIndex =
                                cellSegments[cellIndex];
                            if (visitStamps[segmentIndex] ==
                                visitStamp)
                            {
                                continue;
                            }
                            visitStamps[segmentIndex] = visitStamp;
                            querySegmentIndices.Add(segmentIndex);
                        }
                    }
                }

                querySegmentIndices.Sort();
                for (int queryIndex = 0;
                     queryIndex < querySegmentIndices.Count;
                     queryIndex++)
                {
                    Segment segment = segments[
                        querySegmentIndices[queryIndex]];
                    float t = segment.LengthSquared > 0.0001f
                        ? Mathf.Clamp01(
                            Vector2.Dot(
                                point - segment.A,
                                segment.Delta) /
                            segment.LengthSquared)
                        : 0f;
                    Vector2 candidate =
                        segment.A + segment.Delta * t;
                    float candidateDistanceSquared =
                        (point - candidate).sqrMagnitude;
                    if (candidateDistanceSquared >=
                        bestDistanceSquared)
                    {
                        continue;
                    }

                    found = true;
                    bestDistanceSquared =
                        candidateDistanceSquared;
                    closest = candidate;
                }

                if (!found)
                {
                    return false;
                }
                distance = Mathf.Sqrt(bestDistanceSquared);
                return true;
            }
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
        private readonly Vector2[] obeliskPositions =
            new Vector2[4];
        private readonly List<BoulderPlacement>
            generatedBoulderPlacements =
                new List<BoulderPlacement>();
        private readonly List<Vector2>
            generatedFoliageAnchors =
                new List<Vector2>();
        private readonly List<Vector2>
            generatedFireflyZoneCenters =
                new List<Vector2>();
        private readonly List<CampSite> campSites =
            new List<CampSite>();
        private readonly List<BridgeNavigationRoute>
            bridgeNavigationRoutes =
                new List<BridgeNavigationRoute>();
        private readonly RaycastHit[] bridgeSupportHits =
            new RaycastHit[16];
        private readonly Dictionary<Mesh, Mesh>
            treeCollisionMeshCache =
                new Dictionary<Mesh, Mesh>();
        private readonly Dictionary<Vector2Int,
            List<CombineInstance>> grassChunkInstances =
                new Dictionary<Vector2Int,
                    List<CombineInstance>>();
        private readonly List<UnityEngine.Object>
            generatedRuntimeResources =
                new List<UnityEngine.Object>();
        private readonly PolylineQuery roadQuery =
            new PolylineQuery();
        private readonly PolylineQuery riverQuery =
            new PolylineQuery();
        private readonly List<int> treeInfluenceIndices =
            new List<int>(64);
        private readonly Dictionary<string, double>
            generationStageMilliseconds =
                new Dictionary<string, double>();

        private RaidLayout layout;
        private Transform generatedRoot;
        private int generatedTreeCount;
        private int generatedGrassCount;
        private int[] generatedGrassVariantCounts =
            Array.Empty<int>();
        private int generatedUndergrowthCount;
        private int generatedGroundFloraStudyCount;
        private int generatedGroundFloraColonyCount;
        private int generatedGroundFloraTreePocketCount;
        private int generatedGroundFloraBoulderPocketCount;
        private int generatedBoulderCount;
        private int generatedTrailStoneCount;
        private int generatedBushGroupCount;
        private int generatedFlowerPatchCount;
        private int generatedBoulderGrassCount;
        private int generatedTreeBaseGrassCount;
        private int generatedPlantEdgeGrassCount;
        private int generatedTreeBaseFoliageCount;
        private int generatedBoulderBaseFoliageCount;
        private int generatedBushClusterMemberCount;
        private int generatedFlowerClusterMemberCount;
        private int generatedGroundCoverPatchCount;
        private int generatedTrailTransitionGrassCount;
        private int generatedGuardGroupCount;
        private int generatedGuardPairCount;
        private int generatedCampCount;
        private int generatedCampGuardCount;
        private int generatedCampTentCount;
        private int generatedCampBowGuardCount;
        private int generatedCampSwordGuardCount;
        private int generatedCampWoodenBoxCount;
        private int generatedBridgeCount;
        private int generatedFireflyZoneCount;
        private int generatedRendererCount;
        private int generatedColliderCount;
        private Vector2 noiseOffsetA;
        private Vector2 noiseOffsetB;
        private float oceanWaterLevel;
        private Vector2 elevationDirection;
        private HabitatSample[] habitatField = Array.Empty<HabitatSample>();
        private int habitatGridSize;
        private float habitatFieldExtent;
        private float[] dominantHabitatPercentages = new float[5];
        private float[] groundFloraSelectionWeights =
            Array.Empty<float>();
        private Material terrainRuntimeMaterial;
        private PointSpatialHash treeSpatialHash;
        private PointSpatialHash foliageSpatialHash;
        private double lastGenerationMilliseconds;
        private double grassChunkCombineMilliseconds;
        private double grassChunkTintMilliseconds;
        private double grassChunkFinalizeMilliseconds;

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
        public int GroundFloraStudyVariantCount =>
            groundFloraStudyPrefabs != null
                ? groundFloraStudyPrefabs.Length
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
        public int GeneratedGroundFloraStudyCount =>
            generatedGroundFloraStudyCount;
        public int GeneratedGroundFloraColonyCount =>
            generatedGroundFloraColonyCount;
        public int GeneratedGroundFloraTreePocketCount =>
            generatedGroundFloraTreePocketCount;
        public int GeneratedGroundFloraBoulderPocketCount =>
            generatedGroundFloraBoulderPocketCount;
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
        public int GeneratedBoulderBaseFoliageCount =>
            generatedBoulderBaseFoliageCount;
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
        public int GeneratedCampCount => generatedCampCount;
        public int GeneratedCampGuardCount =>
            generatedCampGuardCount;
        public int GeneratedCampTentCount =>
            generatedCampTentCount;
        public int GeneratedCampBowGuardCount =>
            generatedCampBowGuardCount;
        public int GeneratedCampSwordGuardCount =>
            generatedCampSwordGuardCount;
        public int GeneratedCampWoodenBoxCount =>
            generatedCampWoodenBoxCount;
        public int GeneratedLevelTwoCampCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < campSites.Count; index++)
                {
                    if (campSites[index].IsLevelTwo)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
        public IReadOnlyList<Vector2> CampCenters
        {
            get
            {
                var centers = new Vector2[campSites.Count];
                for (int index = 0; index < campSites.Count; index++)
                {
                    centers[index] = campSites[index].Center;
                }
                return centers;
            }
        }
        public float CampClearingRadius(int campIndex)
        {
            return campIndex >= 0 && campIndex < campSites.Count
                ? campSites[campIndex].ClearingRadius
                : 0f;
        }
        public int CampLevel(int campIndex)
        {
            if (campIndex < 0 || campIndex >= campSites.Count)
            {
                return 0;
            }
            return campSites[campIndex].IsLevelTwo ? 2 : 1;
        }
        public int CampTentCount(int campIndex)
        {
            return campIndex >= 0 && campIndex < campSites.Count
                ? campSites[campIndex].TentCount
                : 0;
        }
        public int CampWoodenBoxCount(int campIndex)
        {
            return campIndex >= 0 && campIndex < campSites.Count
                ? campSites[campIndex].WoodenBoxCount
                : 0;
        }
        public bool IsInsideEnemyRiverExclusion(
            Vector3 worldPoint,
            float padding = 0f)
        {
            Vector2 point = ToXZ(worldPoint);
            if (IsInsideBridgeNavigationLane(point, 0.15f))
            {
                return false;
            }
            return DistanceToRiverExact(point) <
                riverHalfWidth + 0.55f + Mathf.Max(0f, padding);
        }

        public bool IsEnemyNavigationPositionSafe(
            Vector3 worldPoint,
            float padding = 0f)
        {
            Vector2 point = ToXZ(worldPoint);
            for (int index = 0;
                 index < bridgeNavigationRoutes.Count;
                 index++)
            {
                BridgeNavigationRoute route =
                    bridgeNavigationRoutes[index];
                if (!IsInsideBridgeNavigationLane(
                        point,
                        route,
                        -Mathf.Max(0f, padding)))
                {
                    continue;
                }
                int hitCount = Physics.RaycastNonAlloc(
                    new Vector3(
                        worldPoint.x,
                        route.ReferenceDeckHeight + 5f,
                        worldPoint.z),
                    Vector3.down,
                    bridgeSupportHits,
                    14f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                float deckHeight = float.NegativeInfinity;
                for (int hitIndex = 0;
                     hitIndex < hitCount;
                     hitIndex++)
                {
                    Collider collider =
                        bridgeSupportHits[hitIndex].collider;
                    if (collider == null ||
                        route.BridgeRoot == null ||
                        !collider.transform.IsChildOf(
                            route.BridgeRoot))
                    {
                        continue;
                    }
                    deckHeight = Mathf.Max(
                        deckHeight,
                        bridgeSupportHits[hitIndex].point.y);
                }
                return !float.IsNegativeInfinity(deckHeight) &&
                    worldPoint.y >= deckHeight - 0.65f;
            }
            return DistanceToRiverExact(point) >=
                riverHalfWidth + 0.55f + Mathf.Max(0f, padding);
        }

        public bool TryResolveEnemyRiverWaypoint(
            Vector3 from,
            Vector3 destination,
            out Vector3 waypoint)
        {
            waypoint = destination;
            if (bridgeNavigationRoutes.Count == 0)
            {
                return false;
            }

            Vector2 fromPoint = ToXZ(from);
            Vector2 destinationPoint = ToXZ(destination);
            for (int index = 0;
                 index < bridgeNavigationRoutes.Count;
                 index++)
            {
                BridgeNavigationRoute activeRoute =
                    bridgeNavigationRoutes[index];
                if (!IsInsideBridgeNavigationLane(
                        fromPoint,
                        activeRoute,
                        0.2f))
                {
                    continue;
                }

                float destinationSide = Mathf.Sign(
                    Vector2.Dot(
                        destinationPoint - activeRoute.Center,
                        activeRoute.AcrossDirection));
                if (Mathf.Approximately(destinationSide, 0f))
                {
                    destinationSide = 1f;
                }
                Vector2 exit = activeRoute.Center +
                    activeRoute.AcrossDirection *
                    destinationSide * activeRoute.HalfLength;
                waypoint = SurfacePoint(
                    new Vector3(exit.x, 0f, exit.y),
                    1f);
                return true;
            }

            if (!PathTouchesRiverOutsideBridge(
                    fromPoint,
                    destinationPoint))
            {
                return false;
            }

            float bestScore = float.PositiveInfinity;
            Vector2 bestEntry = fromPoint;
            Vector2 bestExit = destinationPoint;
            for (int index = 0;
                 index < bridgeNavigationRoutes.Count;
                 index++)
            {
                BridgeNavigationRoute route =
                    bridgeNavigationRoutes[index];
                float fromSide = Mathf.Sign(
                    Vector2.Dot(
                        fromPoint - route.Center,
                        route.AcrossDirection));
                if (Mathf.Approximately(fromSide, 0f))
                {
                    fromSide = 1f;
                }
                Vector2 entry = route.Center +
                    route.AcrossDirection *
                    fromSide * route.HalfLength;
                Vector2 exit = route.Center -
                    route.AcrossDirection *
                    fromSide * route.HalfLength;
                float score = Vector2.Distance(fromPoint, entry) +
                    Vector2.Distance(exit, destinationPoint);
                if (score >= bestScore)
                {
                    continue;
                }
                bestScore = score;
                bestEntry = entry;
                bestExit = exit;
            }

            Vector2 selected = Vector2.Distance(
                    fromPoint,
                    bestEntry) > 1.05f
                ? bestEntry
                : bestExit;
            waypoint = SurfacePoint(
                new Vector3(selected.x, 0f, selected.y),
                1f);
            return true;
        }
        public int GeneratedBridgeCount =>
            generatedBridgeCount;
        public int GeneratedFireflyZoneCount =>
            generatedFireflyZoneCount;
        public IReadOnlyList<Vector2> FireflyZoneCenters =>
            generatedFireflyZoneCenters;
        public int GeneratedRendererCount =>
            generatedRendererCount;
        public int GeneratedColliderCount =>
            generatedColliderCount;
        public double LastGenerationMilliseconds =>
            lastGenerationMilliseconds;
        public IReadOnlyDictionary<string, double>
            GenerationStageMilliseconds =>
                generationStageMilliseconds;
        public RaidLayout CurrentLayout => layout;
        public GameObject BridgePrefab => bridgePrefab;
        public Material SkyboxMaterial => skyboxMaterial;
        public float MapRadius => mapRadius;
        public ForestFloorDebugMode HabitatDebugMode =>
            forestFloorDebugMode;
        public IReadOnlyList<Vector2> ObeliskPositions =>
            obeliskPositions;

        public static bool ShouldGenerateFireflies(int seed)
        {
            var random = new System.Random(
                unchecked(seed ^ FireflySeedSalt));
            return random.NextDouble() < FireflyMapChance;
        }

        public float DominantHabitatPercentage(
            ForestHabitat habitat)
        {
            int index = (int)habitat;
            return index >= 0 &&
                index < dominantHabitatPercentages.Length
                    ? dominantHabitatPercentages[index]
                    : 0f;
        }

        public float DistanceToNearestTrail(
            Vector3 worldPoint)
        {
            return DistanceToRoad(
                new Vector2(
                    worldPoint.x,
                    worldPoint.z));
        }

        public float SampleTerrainHeight(float x, float z)
        {
            return TerrainHeight(x, z);
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
            Material forestBareGround,
            Material dirtRoad,
            Material water,
            Material bridge,
            Material bark,
            Material birchBark,
            Material leaves,
            Material pineLeaves,
            Material grassDetails,
            Material plantDetails,
            Material rocks,
            Material raidSkybox)
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
            bareGroundMaterial = forestBareGround;
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
            skyboxMaterial = raidSkybox;
        }

        public void ConfigureForestFloorTextures(
            Texture2D loam,
            Texture2D duff,
            Texture2D moss,
            Texture2D groundcover,
            Texture2D stonySoil)
        {
            mossyLoamTexture = loam;
            canopyDuffTexture = duff;
            mossCarpetTexture = moss;
            creepingGroundcoverTexture = groundcover;
            stonyLichenSoilTexture = stonySoil;
        }

        public void ConfigureGroundFloraStudies(
            GameObject[] studyPrefabs)
        {
            groundFloraStudyPrefabs = studyPrefabs;
        }

        public void ConfigureForestCamps(
            EnemyBrain[] guardPool,
            GameObject tent,
            GameObject fire,
            GameObject pot,
            GameObject dryingRack,
            GameObject firewood,
            GameObject chest,
            GameObject bench,
            GameObject barrel,
            GameObject woodenBox,
            GameObject outerSpikeA,
            GameObject outerSpikeB,
            GameObject innerBarricadeA,
            GameObject innerBarricadeB,
            Mesh swordBlade,
            Material swordBladeMaterial,
            Material swordGuardMaterial,
            Material swordGripMaterial,
            Material structureMaterial,
            Material itemMaterial)
        {
            campGuardPool = guardPool;
            campTentPrefab = tent;
            campfirePrefab = fire;
            campPotPrefab = pot;
            campDryingRackPrefab = dryingRack;
            campFirewoodPrefab = firewood;
            campChestPrefab = chest;
            campBenchPrefab = bench;
            campBarrelPrefab = barrel;
            campWoodenBoxPrefab = woodenBox;
            campOuterSpikePrefabA = outerSpikeA;
            campOuterSpikePrefabB = outerSpikeB;
            campInnerBarricadePrefabA = innerBarricadeA;
            campInnerBarricadePrefabB = innerBarricadeB;
            campSwordBladeMesh = swordBlade;
            campSwordBladeMaterial = swordBladeMaterial;
            campSwordGuardMaterial = swordGuardMaterial;
            campSwordGripMaterial = swordGripMaterial;
            campStructureMaterial = structureMaterial;
            campItemMaterial = itemMaterial;
        }

        public void SetForestFloorDebugMode(
            ForestFloorDebugMode mode)
        {
            forestFloorDebugMode = mode;
            ApplyForestFloorDebugMode();
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

        private void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                // Edit-mode previews can be serialized with the review scene.
                // Their inline meshes must survive script-domain reloads.
                return;
            }
            ReleaseGeneratedRuntimeResources();
            foreach (Mesh collisionMesh in
                     treeCollisionMeshCache.Values)
            {
                if (collisionMesh == null)
                {
                    continue;
                }
                Destroy(collisionMesh);
            }
            treeCollisionMeshCache.Clear();
        }

        [ContextMenu("Generate Raid")]
        public void Generate()
        {
            GenerateWithSeed(ResolveSeed());
        }

        public void GenerateWithSeed(int seed)
        {
            generationStageMilliseconds.Clear();
            var generationTimer = Stopwatch.StartNew();
            double previousStageEnd = 0d;
            ConfigureRaidAtmosphere();
            layout = CreateLayout(seed, mapRadius);
            var random = new System.Random(seed);
            noiseOffsetA = new Vector2(
                random.Next(-10000, 10001),
                random.Next(-10000, 10001));
            noiseOffsetB = new Vector2(
                random.Next(-10000, 10001),
                random.Next(-10000, 10001));
            float elevationAngle =
                Mathf.Repeat(
                    noiseOffsetA.x * 0.000173f +
                    noiseOffsetB.y * 0.000119f,
                    1f) *
                Mathf.PI * 2f;
            elevationDirection = new Vector2(
                Mathf.Cos(elevationAngle),
                Mathf.Sin(elevationAngle));
            oceanWaterLevel = RawLandHeight(0f, 0f) - 5.2f;

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
            RebuildPolylineQueries();
            ResolveObeliskPositions();
            ResolveCampSites(
                new System.Random(
                    unchecked(seed ^ (int)0x6d2b79f5)));
            RecordGenerationStage(
                "layout",
                generationTimer,
                ref previousStageEnd);

            if (generatedRoot == null)
            {
                generatedRoot =
                    FindExistingGeneratedRoot();
            }
            if (generatedRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(generatedRoot.gameObject);
                }
            }
            ReleaseGeneratedRuntimeResources();

            generatedRoot =
                new GameObject(
                    $"Generated Raid {seed}").transform;
            generatedRoot.SetParent(transform, false);
            RecordGenerationStage(
                "cleanup",
                generationTimer,
                ref previousStageEnd);

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
            RecordGenerationStage(
                "materials",
                generationTimer,
                ref previousStageEnd);

            CreateForest(random);
            RecordGenerationStage(
                "forest",
                generationTimer,
                ref previousStageEnd);
            CreateGroundScenery(random);
            RecordGenerationStage(
                "ground-scenery",
                generationTimer,
                ref previousStageEnd);
            CreateTerrain(
                forestRuntime,
                bareGroundMaterial,
                roadRuntime);
            RecordGenerationStage(
                "terrain",
                generationTimer,
                ref previousStageEnd);
            CreateRibbon(
                "River",
                riverSamples,
                riverHalfWidth +
                    RiverWaterBankOverlap,
                waterRuntime,
                false);
            CreateOcean(waterRuntime);
            CreateBridges();
            CreateIslandShoreBoundary();
            CreateForestCamps(
                new System.Random(
                    unchecked(seed ^ (int)0x43f17a2d)));
            CreateRareFireflyZone(seed);
            RecordGenerationStage(
                "river-bridges-and-camps",
                generationTimer,
                ref previousStageEnd);
            PlaceActorsAndObjectives(random);
            RecordGenerationStage(
                "actors-and-objectives",
                generationTimer,
                ref previousStageEnd);
            ConfigureEnvironmentCulling();
            CacheGenerationMetrics();
            RecordGenerationStage(
                "finalization",
                generationTimer,
                ref previousStageEnd);
            generationTimer.Stop();
            lastGenerationMilliseconds =
                generationTimer.Elapsed.TotalMilliseconds;
            UnityEngine.Debug.Log(
                $"Raid generation {seed} completed in " +
                $"{lastGenerationMilliseconds:0.0} ms " +
                $"({FormatGenerationStages()}).",
                this);
            GameplayEventLog.Publish(
                "raid-generated",
                gameObject,
                $"seed={seed}; trees={generatedTreeCount}; " +
                $"grass={generatedGrassCount}; " +
                $"undergrowth={generatedUndergrowthCount}; " +
                $"groundFlora={generatedGroundFloraStudyCount}; " +
                $"groundFloraColonies={generatedGroundFloraColonyCount}; " +
                $"habitats={FormatHabitatPercentages()}; " +
                $"boulders={generatedBoulderCount}; " +
                $"trailStones={generatedTrailStoneCount}; " +
                $"renderers={generatedRendererCount}; " +
                $"colliders={generatedColliderCount}; " +
                $"guardGroups={generatedGuardGroupCount}; " +
                $"guardPairs={generatedGuardPairCount}; " +
                $"camps={generatedCampCount}; " +
                $"levelTwoCamps={GeneratedLevelTwoCampCount}; " +
                $"campGuards={generatedCampGuardCount}; " +
                $"campBows={generatedCampBowGuardCount}; " +
                $"campSwords={generatedCampSwordGuardCount}; " +
                $"fireflyZones={generatedFireflyZoneCount}; " +
                $"fork={layout.HasRoadFork}; " +
                $"crossing={layout.RiverCrossesRoad}; " +
                $"generationMs={lastGenerationMilliseconds:0.0}; " +
                $"stages={FormatGenerationStages()}");
        }

        private void RecordGenerationStage(
            string stage,
            Stopwatch timer,
            ref double previousStageEnd)
        {
            double stageEnd =
                timer.Elapsed.TotalMilliseconds;
            generationStageMilliseconds[stage] =
                stageEnd - previousStageEnd;
            previousStageEnd = stageEnd;
        }

        private string FormatGenerationStages()
        {
            var parts = new List<string>(
                generationStageMilliseconds.Count);
            foreach (KeyValuePair<string, double> stage in
                     generationStageMilliseconds)
            {
                parts.Add(
                    $"{stage.Key} {stage.Value:0.0}ms");
            }
            return string.Join(", ", parts);
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

        private void RebuildPolylineQueries()
        {
            roadQuery.Clear();
            roadQuery.Add(mainRoadSamples);
            roadQuery.Add(forkRoadSamples);
            roadQuery.Add(branchRoadASamples);
            roadQuery.Add(branchRoadBSamples);
            roadQuery.Add(branchRoadCSamples);
            riverQuery.Clear();
            riverQuery.Add(riverSamples);
        }

        private T TrackRuntimeResource<T>(T resource)
            where T : UnityEngine.Object
        {
            if (resource != null)
            {
                generatedRuntimeResources.Add(resource);
            }
            return resource;
        }

        private void ReleaseGeneratedRuntimeResources()
        {
            for (int index = 0;
                 index < generatedRuntimeResources.Count;
                 index++)
            {
                UnityEngine.Object resource =
                    generatedRuntimeResources[index];
                if (resource == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(resource);
                }
                else
                {
                    DestroyImmediate(resource);
                }
            }
            generatedRuntimeResources.Clear();
            terrainRuntimeMaterial = null;
        }

        private void ConfigureEnvironmentCulling()
        {
            RaidEnvironmentCuller culler =
                GetComponent<RaidEnvironmentCuller>();
            if (culler == null)
            {
                culler = gameObject.AddComponent<
                    RaidEnvironmentCuller>();
            }
            culler.Configure(
                player,
                generatedRoot.Find("Dense Stylized Forest"),
                generatedRoot.Find("Batched Meadow Grass"),
                generatedRoot.Find(
                    "Shrubs Flowers and Ground Cover"),
                generatedRoot.Find("Boulders"),
                generatedRoot.Find("Trail and Edge Stones"));
        }

        private void CacheGenerationMetrics()
        {
            generatedRendererCount = 0;
            foreach (Renderer renderer in
                     generatedRoot.GetComponentsInChildren<
                         Renderer>(false))
            {
                if (renderer.enabled)
                {
                    generatedRendererCount++;
                }
            }
            generatedColliderCount = 0;
            foreach (Collider collider in
                     generatedRoot.GetComponentsInChildren<
                         Collider>(false))
            {
                if (collider.enabled)
                {
                    generatedColliderCount++;
                }
            }
        }

        public static RaidLayout CreateLayout(
            int seed,
            float radius)
        {
            float[] coastRadii = CreateIslandCoastRadii(
                seed,
                radius);
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

            Vector3[] river = CreateBoundaryRiver(
                random,
                radius,
                mainAngle +
                    Mathf.PI * 0.5f +
                    Mathf.Lerp(
                        -0.22f,
                        0.22f,
                        (float)random.NextDouble()));
            StraightenRiverCrossings(road, river);
            var bridgeAnchors = new List<Vector3>();
            var routeDestinations = new List<Vector3>
            {
                road[0],
                road[road.Length - 1]
            };
            var routeNetwork = new List<Vector3[]>
            {
                road
            };
            RegisterCrossingAnchors(
                road,
                river,
                bridgeAnchors);

            bool hasSecondPrimary =
                random.NextDouble() < 0.68;
            Vector3[] fork = Array.Empty<Vector3>();
            if (hasSecondPrimary)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3[] candidate = CreateBoundaryRoad(
                        random,
                        radius,
                        mainAngle +
                            Mathf.Lerp(
                                0.82f,
                                1.95f,
                                (float)random.NextDouble()),
                        23,
                        Mathf.Lerp(
                            -radius * 0.34f,
                            radius * 0.34f,
                            (float)random.NextDouble()));
                    StraightenRiverCrossings(candidate, river);
                    if (!IsDistinctRouteDestination(
                            candidate[0],
                            routeDestinations) ||
                        !IsDistinctRouteDestination(
                            candidate[candidate.Length - 1],
                            routeDestinations) ||
                        !RoadsIntersect(candidate, road) ||
                        !CrossingsRespectSpacing(
                            candidate,
                            river,
                            bridgeAnchors))
                    {
                        continue;
                    }

                    fork = candidate;
                    RegisterCrossingAnchors(
                        fork,
                        river,
                        bridgeAnchors);
                    routeDestinations.Add(fork[0]);
                    routeDestinations.Add(
                        fork[fork.Length - 1]);
                    routeNetwork.Add(fork);
                    break;
                }
            }

            Vector3[] branchA = CreatePurposefulBranchRoad(
                random,
                radius,
                road,
                Mathf.Lerp(
                    0.26f,
                    0.43f,
                    (float)random.NextDouble()),
                random.NextDouble() < 0.5 ? -1f : 1f,
                river,
                bridgeAnchors,
                routeDestinations,
                routeNetwork);
            Vector3[] branchB =
                random.NextDouble() <
                    (fork.Length > 0 ? 0.42 : 0.62)
                    ? CreatePurposefulBranchRoad(
                        random,
                        radius,
                        fork.Length > 0 ? fork : road,
                        Mathf.Lerp(
                            0.52f,
                            0.72f,
                            (float)random.NextDouble()),
                        random.NextDouble() < 0.5 ? -1f : 1f,
                        river,
                        bridgeAnchors,
                        routeDestinations,
                        routeNetwork)
                    : Array.Empty<Vector3>();
            Vector3[] branchC = Array.Empty<Vector3>();

            Vector3 playerSpawn = FindOuterSpawnPoint(
                random,
                radius,
                river,
                null,
                Mathf.PI);
            float oppositeAngle =
                Mathf.Atan2(
                    playerSpawn.z,
                    playerSpawn.x) +
                Mathf.PI;
            Vector3 extraction = FindOuterSpawnPoint(
                random,
                radius,
                river,
                oppositeAngle,
                ExtractionOppositeArc);

            WarpPolylineToIsland(road, radius, coastRadii);
            WarpPolylineToIsland(fork, radius, coastRadii);
            WarpPolylineToIsland(branchA, radius, coastRadii);
            WarpPolylineToIsland(branchB, radius, coastRadii);
            WarpPolylineToIsland(branchC, radius, coastRadii);
            WarpPolylineToIsland(river, radius, coastRadii);
            playerSpawn = WarpPointToIsland(
                playerSpawn,
                radius,
                coastRadii);
            extraction = WarpPointToIsland(
                extraction,
                radius,
                coastRadii);
            ExtendRiverMouthToOcean(
                river,
                coastRadii);
            StraightenRiverCrossings(road, river);
            StraightenRiverCrossings(fork, river);
            StraightenRiverCrossings(branchA, river);
            StraightenRiverCrossings(branchB, river);
            StraightenRiverCrossings(branchC, river);
            SnapBranchStartToRoute(branchA, road);
            SnapBranchStartToRoute(
                branchB,
                fork.Length > 0 ? fork : road);

            float maximumCoastRadius = 0f;
            for (int index = 0;
                 index < coastRadii.Length;
                 index++)
            {
                maximumCoastRadius = Mathf.Max(
                    maximumCoastRadius,
                    coastRadii[index]);
            }

            return new RaidLayout
            {
                Seed = seed,
                HasRoadFork = fork.Length > 0,
                RiverCrossesRoad = true,
                MainRoad = road,
                ForkRoad = fork,
                BranchRoadA = branchA,
                BranchRoadB = branchB,
                BranchRoadC = branchC,
                River = river,
                PlayerSpawn = playerSpawn,
                ExtractionPoint = extraction,
                CoastRadii = coastRadii,
                MaximumCoastRadius = maximumCoastRadius
            };
        }

        public static float[] CreateIslandCoastRadii(
            int seed,
            float equalAreaRadius)
        {
            var random = new System.Random(
                unchecked(seed ^ IslandShapeSeedSalt));
            float phaseOne = RandomAngle(random);
            float phaseTwo = RandomAngle(random);
            float phaseThree = RandomAngle(random);
            float phaseFour = RandomAngle(random);
            float phaseFive = RandomAngle(random);
            float amplitudeOne = Mathf.Lerp(
                0.045f,
                0.095f,
                (float)random.NextDouble());
            float amplitudeTwo = Mathf.Lerp(
                0.075f,
                0.135f,
                (float)random.NextDouble());
            float amplitudeThree = Mathf.Lerp(
                0.045f,
                0.09f,
                (float)random.NextDouble());
            float amplitudeFour = Mathf.Lerp(
                0.018f,
                0.052f,
                (float)random.NextDouble());
            float amplitudeFive = Mathf.Lerp(
                0.01f,
                0.032f,
                (float)random.NextDouble());
            var radii = new float[IslandCoastSampleCount];
            float squaredTotal = 0f;
            for (int index = 0;
                 index < radii.Length;
                 index++)
            {
                float angle = index * Mathf.PI * 2f /
                    radii.Length;
                float shape = 1f +
                    Mathf.Sin(angle + phaseOne) * amplitudeOne +
                    Mathf.Cos(angle * 2f + phaseTwo) * amplitudeTwo +
                    Mathf.Sin(angle * 3f + phaseThree) * amplitudeThree +
                    Mathf.Cos(angle * 4f + phaseFour) * amplitudeFour +
                    Mathf.Sin(angle * 5f + phaseFive) * amplitudeFive;
                radii[index] = Mathf.Clamp(shape, 0.72f, 1.30f);
                squaredTotal += radii[index] * radii[index];
            }

            float areaNormalization = equalAreaRadius *
                Mathf.Sqrt(radii.Length / squaredTotal);
            for (int index = 0;
                 index < radii.Length;
                 index++)
            {
                radii[index] *= areaNormalization;
            }
            return radii;
        }

        public static float SampleIslandCoastRadius(
            float[] coastRadii,
            float angle)
        {
            if (coastRadii == null || coastRadii.Length == 0)
            {
                return 0f;
            }
            float normalized = Mathf.Repeat(
                angle / (Mathf.PI * 2f),
                1f) * coastRadii.Length;
            int first = Mathf.FloorToInt(normalized) %
                coastRadii.Length;
            int second = (first + 1) % coastRadii.Length;
            return Mathf.Lerp(
                coastRadii[first],
                coastRadii[second],
                normalized - Mathf.Floor(normalized));
        }

        private static float RandomAngle(System.Random random)
        {
            return (float)random.NextDouble() * Mathf.PI * 2f;
        }

        private static Vector3 WarpPointToIsland(
            Vector3 point,
            float sourceRadius,
            float[] coastRadii)
        {
            float distance = new Vector2(point.x, point.z).magnitude;
            if (distance <= 0.0001f)
            {
                return point;
            }
            float angle = Mathf.Atan2(point.z, point.x);
            float coastRadius = SampleIslandCoastRadius(
                coastRadii,
                angle);
            float warpedDistance = distance /
                Mathf.Max(0.001f, sourceRadius) * coastRadius;
            return new Vector3(
                Mathf.Cos(angle) * warpedDistance,
                point.y,
                Mathf.Sin(angle) * warpedDistance);
        }

        private static void WarpPolylineToIsland(
            Vector3[] points,
            float sourceRadius,
            float[] coastRadii)
        {
            if (points == null)
            {
                return;
            }
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = WarpPointToIsland(
                    points[index],
                    sourceRadius,
                    coastRadii);
            }
        }

        private static void ExtendRiverMouthToOcean(
            Vector3[] river,
            float[] coastRadii)
        {
            if (river == null || river.Length < 2)
            {
                return;
            }
            int[] endpoints = { 0, river.Length - 1 };
            for (int endpointIndex = 0;
                 endpointIndex < endpoints.Length;
                 endpointIndex++)
            {
                int index = endpoints[endpointIndex];
                Vector3 point = river[index];
                float angle = Mathf.Atan2(point.z, point.x);
                float radius = SampleIslandCoastRadius(
                    coastRadii,
                    angle) + 2.5f;
                river[index] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    point.y,
                    Mathf.Sin(angle) * radius);
            }
        }

        private static void SnapBranchStartToRoute(
            Vector3[] branch,
            Vector3[] route)
        {
            if (branch == null || branch.Length == 0 ||
                route == null || route.Length < 2)
            {
                return;
            }
            Vector3 point = branch[0];
            Vector3 closest = route[0];
            float closestSquared = float.PositiveInfinity;
            for (int index = 0; index < route.Length - 1; index++)
            {
                Vector3 segment = route[index + 1] - route[index];
                float progress = segment.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(
                        Vector3.Dot(point - route[index], segment) /
                        segment.sqrMagnitude)
                    : 0f;
                Vector3 candidate = route[index] + segment * progress;
                float distanceSquared =
                    (point - candidate).sqrMagnitude;
                if (distanceSquared < closestSquared)
                {
                    closestSquared = distanceSquared;
                    closest = candidate;
                }
            }
            branch[0] = closest;
        }


        private static Vector3 FindOuterSpawnPoint(
            System.Random random,
            float mapRadius,
            Vector3[] river,
            float? preferredAngle,
            float angleHalfRange)
        {
            Vector3 bestCandidate = Vector3.zero;
            float bestRiverDistance = float.MinValue;
            for (int attempt = 0;
                 attempt < OuterSpawnPlacementAttempts;
                 attempt++)
            {
                float angle = preferredAngle.HasValue
                    ? preferredAngle.Value +
                        Mathf.Lerp(
                            -angleHalfRange,
                            angleHalfRange,
                            (float)random.NextDouble())
                    : Mathf.Lerp(
                        0f,
                        Mathf.PI * 2f,
                        (float)random.NextDouble());
                float radialRatio = Mathf.Sqrt(
                    Mathf.Lerp(
                        OuterSpawnInnerRadiusRatio *
                            OuterSpawnInnerRadiusRatio,
                        OuterSpawnOuterRadiusRatio *
                            OuterSpawnOuterRadiusRatio,
                        (float)random.NextDouble()));
                var candidate = new Vector3(
                    Mathf.Cos(angle) * mapRadius * radialRatio,
                    0f,
                    Mathf.Sin(angle) * mapRadius * radialRatio);
                float riverDistance = DistanceToPolyline(
                    candidate,
                    river);
                if (riverDistance > bestRiverDistance)
                {
                    bestCandidate = candidate;
                    bestRiverDistance = riverDistance;
                }
                if (riverDistance >= SpawnRiverClearance)
                {
                    return candidate;
                }
            }

            return bestCandidate;
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
            float exitAngle)
        {
            const int PointCount = 13;
            var points = new Vector3[PointCount];
            Vector3 start = PointOnPolyline(
                sourceRoad,
                sourceT);
            Vector3 end = new Vector3(
                Mathf.Sin(exitAngle) * (radius - 2f),
                0f,
                Mathf.Cos(exitAngle) * (radius - 2f));
            Vector3 sourceTangent = PolylineDirectionAt(
                sourceRoad,
                sourceT);
            Vector3 destinationDirection = Vector3.ProjectOnPlane(
                end - start,
                Vector3.up).normalized;
            float side = Mathf.Sign(
                Vector3.Dot(
                    Vector3.Cross(
                        sourceTangent,
                        destinationDirection),
                    Vector3.up));
            if (Mathf.Approximately(side, 0f))
            {
                side = random.NextDouble() < 0.5 ? -1f : 1f;
            }
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

        private static Vector3[] CreatePurposefulBranchRoad(
            System.Random random,
            float radius,
            Vector3[] sourceRoad,
            float preferredSourceT,
            float preferredSide,
            Vector3[] river,
            List<Vector3> bridgeAnchors,
            List<Vector3> routeDestinations,
            List<Vector3[]> routeNetwork)
        {
            List<float> destinationCenters = FindDestinationGapCenters(
                routeDestinations);
            int attemptsPerSector = 18;
            int totalAttempts =
                destinationCenters.Count * attemptsPerSector;
            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                int sectorIndex =
                    attempt % destinationCenters.Count;
                int sectorAttempt =
                    attempt / destinationCenters.Count;
                float offsetStep =
                    (sectorAttempt + 1) / 2f * 0.04f;
                float offsetSign = sectorAttempt == 0
                    ? 0f
                    : sectorAttempt % 2 == 1 ? 1f : -1f;
                float sourceT = Mathf.Clamp(
                    preferredSourceT + offsetStep * offsetSign,
                    0.16f,
                    0.84f);
                Vector3 sourcePoint = PointOnPolyline(
                    sourceRoad,
                    sourceT);
                if (DistanceToPolyline(sourcePoint, river) <
                    MinimumForkRiverClearance)
                {
                    continue;
                }

                float angleStep =
                    (sectorAttempt + 1) / 2f * 0.045f;
                float angleSign = sectorAttempt == 0
                    ? 0f
                    : sectorAttempt % 2 == 1
                        ? preferredSide
                        : -preferredSide;
                float exitAngle = destinationCenters[sectorIndex] +
                    angleStep * angleSign;
                Vector3[] candidate = CreateBranchRoad(
                    random,
                    radius,
                    sourceRoad,
                    sourceT,
                    exitAngle);
                StraightenRiverCrossings(candidate, river);
                if (!IsDistinctRouteDestination(
                        candidate[candidate.Length - 1],
                        routeDestinations) ||
                    !HasPurposefulDeparture(
                        candidate,
                        sourceRoad) ||
                    IsRedundantRouteCorridor(
                        candidate,
                        routeNetwork) ||
                    !CrossingsRespectSpacing(
                        candidate,
                        river,
                        bridgeAnchors))
                {
                    continue;
                }

                RegisterCrossingAnchors(
                    candidate,
                    river,
                    bridgeAnchors);
                routeDestinations.Add(
                    candidate[candidate.Length - 1]);
                routeNetwork.Add(candidate);
                return candidate;
            }

            return Array.Empty<Vector3>();
        }

        private static List<float> FindDestinationGapCenters(
            List<Vector3> destinations)
        {
            var angles = new List<float>(destinations.Count);
            for (int index = 0; index < destinations.Count; index++)
            {
                angles.Add(NormalizeAngle(
                    Mathf.Atan2(
                        destinations[index].x,
                        destinations[index].z)));
            }
            angles.Sort();
            if (angles.Count == 0)
            {
                return new List<float> { 0f };
            }

            var centers = new List<float>(angles.Count);
            var sizes = new List<float>(angles.Count);
            for (int index = 0; index < angles.Count; index++)
            {
                float start = angles[index];
                float end = index == angles.Count - 1
                    ? angles[0] + Mathf.PI * 2f
                    : angles[index + 1];
                float gap = end - start;
                float center = NormalizeAngle(start + gap * 0.5f);
                int insertion = 0;
                while (insertion < sizes.Count &&
                    sizes[insertion] >= gap)
                {
                    insertion++;
                }
                sizes.Insert(insertion, gap);
                centers.Insert(insertion, center);
            }
            return centers;
        }

        private static bool HasPurposefulDeparture(
            Vector3[] candidate,
            Vector3[] sourceRoad)
        {
            int directionIndex = Mathf.Min(
                candidate.Length - 1,
                3);
            Vector3 departure = Vector3.ProjectOnPlane(
                candidate[directionIndex] - candidate[0],
                Vector3.up).normalized;
            Vector3 sourceDirection = ClosestPolylineDirection(
                candidate[0],
                sourceRoad,
                out _);
            float angle = Mathf.Acos(Mathf.Clamp(
                Mathf.Abs(Vector3.Dot(
                    departure,
                    sourceDirection)),
                -1f,
                1f));
            return angle >= MinimumBranchDepartureAngle;
        }

        private static bool IsRedundantRouteCorridor(
            Vector3[] candidate,
            List<Vector3[]> routeNetwork)
        {
            for (int roadIndex = 0;
                 roadIndex < routeNetwork.Count;
                 roadIndex++)
            {
                int consecutiveParallelSamples = 0;
                for (int index = 2;
                     index < candidate.Length - 1;
                     index++)
                {
                    Vector3 candidateDirection = Vector3.ProjectOnPlane(
                        candidate[index + 1] -
                            candidate[index - 1],
                        Vector3.up).normalized;
                    Vector3 routeDirection = ClosestPolylineDirection(
                        candidate[index],
                        routeNetwork[roadIndex],
                        out float distance);
                    bool followsSameCorridor =
                        distance < ParallelRouteClearance &&
                        Mathf.Abs(Vector3.Dot(
                            candidateDirection,
                            routeDirection)) > 0.88f;
                    consecutiveParallelSamples = followsSameCorridor
                        ? consecutiveParallelSamples + 1
                        : 0;
                    if (consecutiveParallelSamples >= 3)
                    {
                        return true;
                    }
                }
            }
            return false;
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
                float lengthSquared = segment.sqrMagnitude;
                float progress = lengthSquared > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point2 - start, segment) /
                        lengthSquared)
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

        private static bool IsDistinctRouteDestination(
            Vector3 candidate,
            List<Vector3> destinations)
        {
            for (int index = 0; index < destinations.Count; index++)
            {
                if (Vector3.Distance(
                        candidate,
                        destinations[index]) <
                        MinimumRouteDestinationSeparation ||
                    AngularDistance(
                        Mathf.Atan2(candidate.x, candidate.z),
                        Mathf.Atan2(
                            destinations[index].x,
                            destinations[index].z)) <
                        MinimumDestinationAngle)
                {
                    return false;
                }
            }
            return true;
        }

        private static float AngularDistance(float first, float second)
        {
            float difference = Mathf.Abs(
                NormalizeAngle(first) - NormalizeAngle(second));
            return Mathf.Min(
                difference,
                Mathf.PI * 2f - difference);
        }

        private static float NormalizeAngle(float angle)
        {
            float circle = Mathf.PI * 2f;
            angle %= circle;
            return angle < 0f ? angle + circle : angle;
        }

        private static Vector3 PolylineDirectionAt(
            Vector3[] points,
            float t)
        {
            float step = 1f / Mathf.Max(2f, points.Length - 1f);
            Vector3 before = PointOnPolyline(
                points,
                Mathf.Max(0f, t - step));
            Vector3 after = PointOnPolyline(
                points,
                Mathf.Min(1f, t + step));
            Vector3 direction = Vector3.ProjectOnPlane(
                after - before,
                Vector3.up);
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
        }

        private static float FindClearestForkParameter(
            Vector3[] sourceRoad,
            Vector3[] river,
            float preferredSourceT)
        {
            float bestT = Mathf.Clamp(
                preferredSourceT,
                0.12f,
                0.88f);
            float bestScore = float.NegativeInfinity;
            for (int sample = 0; sample <= 24; sample++)
            {
                float candidateT = Mathf.Lerp(
                    0.12f,
                    0.88f,
                    sample / 24f);
                float clearance = DistanceToPolyline(
                    PointOnPolyline(sourceRoad, candidateT),
                    river);
                float preferencePenalty =
                    Mathf.Abs(candidateT - preferredSourceT) * 8f;
                float score = clearance - preferencePenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestT = candidateT;
                }
            }
            return bestT;
        }

        private static void StraightenRiverCrossings(
            Vector3[] road,
            Vector3[] river)
        {
            for (int pass = 0; pass < 24; pass++)
            {
                if (!TryFindNonPerpendicularIntersection(
                        road,
                        river,
                        out int roadSegment,
                        out int riverSegment,
                        out Vector3 crossing))
                {
                    return;
                }

                Vector3 riverDirection = Vector3.ProjectOnPlane(
                    river[riverSegment + 1] - river[riverSegment],
                    Vector3.up).normalized;
                Vector3 crossingDirection = Vector3.Cross(
                    Vector3.up,
                    riverDirection).normalized;
                Vector3 originalDirection = Vector3.ProjectOnPlane(
                    road[roadSegment + 1] - road[roadSegment],
                    Vector3.up).normalized;
                if (Vector3.Dot(crossingDirection, originalDirection) < 0f)
                {
                    crossingDirection = -crossingDirection;
                }

                SetCrossingControlPoint(
                    road,
                    roadSegment - 1,
                    crossing - crossingDirection * CrossingApproachHalfLength);
                SetCrossingControlPoint(
                    road,
                    roadSegment,
                    crossing - crossingDirection * CrossingStraightHalfLength);
                SetCrossingControlPoint(
                    road,
                    roadSegment + 1,
                    crossing + crossingDirection * CrossingStraightHalfLength);
                SetCrossingControlPoint(
                    road,
                    roadSegment + 2,
                    crossing + crossingDirection * CrossingApproachHalfLength);
            }
        }

        private static bool TryFindNonPerpendicularIntersection(
            Vector3[] road,
            Vector3[] river,
            out int roadSegment,
            out int riverSegment,
            out Vector3 crossing)
        {
            for (roadSegment = 0;
                 roadSegment < road.Length - 1;
                 roadSegment++)
            {
                Vector3 roadDirection = Vector3.ProjectOnPlane(
                    road[roadSegment + 1] - road[roadSegment],
                    Vector3.up).normalized;
                for (riverSegment = 0;
                     riverSegment < river.Length - 1;
                     riverSegment++)
                {
                    if (!TryFindSegmentIntersection(
                            road[roadSegment],
                            road[roadSegment + 1],
                            river[riverSegment],
                            river[riverSegment + 1],
                            out crossing))
                    {
                        continue;
                    }
                    Vector3 riverDirection = Vector3.ProjectOnPlane(
                        river[riverSegment + 1] - river[riverSegment],
                        Vector3.up).normalized;
                    if (Mathf.Abs(Vector3.Dot(
                            roadDirection,
                            riverDirection)) > 0.01f)
                    {
                        return true;
                    }
                }
            }

            roadSegment = -1;
            riverSegment = -1;
            crossing = Vector3.zero;
            return false;
        }

        private static void SetCrossingControlPoint(
            Vector3[] road,
            int index,
            Vector3 point)
        {
            if (index <= 0 || index >= road.Length - 1)
            {
                return;
            }

            road[index] = point;
        }

        private static bool CrossingsRespectSpacing(
            Vector3[] road,
            Vector3[] river,
            List<Vector3> acceptedCrossings)
        {
            var candidateCrossings = new List<Vector3>();
            FindArrayIntersections(
                road,
                river,
                candidateCrossings);
            for (int index = 0; index < candidateCrossings.Count; index++)
            {
                for (int accepted = 0;
                     accepted < acceptedCrossings.Count;
                     accepted++)
                {
                    if (Vector3.Distance(
                            candidateCrossings[index],
                            acceptedCrossings[accepted]) <
                        MinimumBridgeSeparation)
                    {
                        return false;
                    }
                }

                for (int other = 0; other < index; other++)
                {
                    if (Vector3.Distance(
                            candidateCrossings[index],
                            candidateCrossings[other]) <
                        MinimumBridgeSeparation)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool RoadsIntersect(
            Vector3[] firstRoad,
            Vector3[] secondRoad)
        {
            for (int first = 0;
                 first < firstRoad.Length - 1;
                 first++)
            {
                for (int second = 0;
                     second < secondRoad.Length - 1;
                     second++)
                {
                    if (TryFindSegmentIntersection(
                            firstRoad[first],
                            firstRoad[first + 1],
                            secondRoad[second],
                            secondRoad[second + 1],
                            out _))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void RegisterCrossingAnchors(
            Vector3[] road,
            Vector3[] river,
            List<Vector3> acceptedCrossings)
        {
            FindArrayIntersections(
                road,
                river,
                acceptedCrossings);
        }

        private static void FindArrayIntersections(
            Vector3[] road,
            Vector3[] river,
            List<Vector3> results)
        {
            for (int roadIndex = 0;
                 roadIndex < road.Length - 1;
                 roadIndex++)
            {
                for (int riverIndex = 0;
                     riverIndex < river.Length - 1;
                     riverIndex++)
                {
                    if (TryFindSegmentIntersection(
                            road[roadIndex],
                            road[roadIndex + 1],
                            river[riverIndex],
                            river[riverIndex + 1],
                            out Vector3 crossing))
                    {
                        results.Add(crossing);
                    }
                }
            }
        }

        private static bool TryFindSegmentIntersection(
            Vector3 roadStart3,
            Vector3 roadEnd3,
            Vector3 riverStart3,
            Vector3 riverEnd3,
            out Vector3 crossing)
        {
            Vector2 roadStart = ToXZ(roadStart3);
            Vector2 roadDelta = ToXZ(roadEnd3) - roadStart;
            Vector2 riverStart = ToXZ(riverStart3);
            Vector2 riverDelta = ToXZ(riverEnd3) - riverStart;
            float denominator = Cross2D(roadDelta, riverDelta);
            if (Mathf.Abs(denominator) <= 0.00001f)
            {
                crossing = Vector3.zero;
                return false;
            }

            Vector2 separation = riverStart - roadStart;
            float roadT = Cross2D(separation, riverDelta) / denominator;
            float riverT = Cross2D(separation, roadDelta) / denominator;
            if (roadT < 0f || roadT > 1f ||
                riverT < 0f || riverT > 1f)
            {
                crossing = Vector3.zero;
                return false;
            }

            Vector2 point = roadStart + roadDelta * roadT;
            crossing = new Vector3(
                point.x,
                Mathf.Lerp(roadStart3.y, roadEnd3.y, roadT),
                point.y);
            return true;
        }

        private static float DistanceToPolyline(
            Vector3 point,
            Vector3[] line)
        {
            float closest = float.PositiveInfinity;
            Vector2 point2 = ToXZ(point);
            for (int index = 0; index < line.Length - 1; index++)
            {
                Vector2 start = ToXZ(line[index]);
                Vector2 end = ToXZ(line[index + 1]);
                Vector2 segment = end - start;
                float lengthSquared = segment.sqrMagnitude;
                float progress = lengthSquared > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point2 - start, segment) /
                        lengthSquared)
                    : 0f;
                closest = Mathf.Min(
                    closest,
                    Vector2.Distance(
                        point2,
                        start + segment * progress));
            }
            return closest;
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
            Material bareMaterial,
            Material dirtRoadMaterial)
        {
            var terrainTimer = Stopwatch.StartNew();
            double previousTerrainStageEnd = 0d;
            int width = terrainResolution + 1;
            var vertices =
                new Vector3[width * width];
            var uv =
                new Vector2[vertices.Length];
            var roadField =
                new float[vertices.Length];
            float terrainExtent = IslandGenerationExtent;
            float diameter = terrainExtent * 2f;
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float worldX =
                        -terrainExtent +
                        diameter * x / terrainResolution;
                    float worldZ =
                        -terrainExtent +
                        diameter * z / terrainResolution;
                    int index = z * width + x;
                    vertices[index] =
                        new Vector3(
                            worldX,
                            TerrainHeight(
                                worldX,
                                worldZ,
                                out float signedRoadDistance),
                            worldZ);
                    uv[index] =
                        new Vector2(
                            worldX / 12f,
                            worldZ / 12f);
                    roadField[index] = signedRoadDistance;
                }
            }
            RecordGenerationStage(
                "terrain-height-field",
                terrainTimer,
                ref previousTerrainStageEnd);

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
                        -terrainExtent +
                        diameter *
                        (x + 0.5f) /
                        terrainResolution;
                    float centerZ =
                        -terrainExtent +
                        diameter *
                        (z + 0.5f) /
                        terrainResolution;
                    if (!IsInsideIsland(
                            new Vector2(centerX, centerZ),
                            0f))
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
            RecordGenerationStage(
                "terrain-topology",
                terrainTimer,
                ref previousTerrainStageEnd);

            Mesh mesh = TrackRuntimeResource(new Mesh
            {
                name = "Procedural Raid Island"
            });
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(meshVertices);
            mesh.SetUVs(0, meshUv);
            var terrainColors =
                new List<Color>(
                    meshVertices.Count);
            var habitatWeights =
                new List<Vector4>(
                    meshVertices.Count);
            var habitatSignals =
                new List<Vector4>(
                    meshVertices.Count);
            var habitatDebug =
                new List<Vector2>(
                    meshVertices.Count);
            for (int vertexIndex = 0;
                 vertexIndex < meshVertices.Count;
                 vertexIndex++)
            {
                Vector3 vertex =
                    meshVertices[vertexIndex];
                terrainColors.Add(
                    vertexIndex < roadField.Length
                        ? TerrainBlendTintAt(
                            vertex.x,
                            vertex.z,
                            roadField[vertexIndex])
                        : TerrainBlendTintAt(
                            vertex.x,
                            vertex.z));
                HabitatSample habitat = ForestHabitatAt(
                    new Vector2(vertex.x, vertex.z));
                habitatWeights.Add(habitat.PrimaryWeights);
                habitatSignals.Add(
                    new Vector4(
                        habitat.GrassDensity,
                        habitat.CanopyInfluence,
                        habitat.BoulderInfluence,
                        habitat.MoistureTendency));
                habitatDebug.Add(
                    new Vector2(
                        FoliageColonyInfluenceAt(
                            new Vector2(vertex.x, vertex.z)),
                        habitat.StonyWeight));
            }
            RecordGenerationStage(
                "terrain-attributes",
                terrainTimer,
                ref previousTerrainStageEnd);
            mesh.SetColors(terrainColors);
            mesh.SetUVs(1, habitatWeights);
            mesh.SetUVs(2, habitatSignals);
            mesh.SetUVs(3, habitatDebug);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(
                groundTriangles,
                0);
            mesh.SetTriangles(
                roadTriangles,
                1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            RecordGenerationStage(
                "terrain-mesh-finalize",
                terrainTimer,
                ref previousTerrainStageEnd);

            GameObject terrain =
                new GameObject("Terrain Island");
            terrain.transform.SetParent(
                generatedRoot,
                false);
            terrain.AddComponent<MeshFilter>()
                .sharedMesh = mesh;
            Material terrainBlend =
                CreateTerrainBlendMaterial(
                    groundMaterial,
                    bareMaterial,
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
            RecordGenerationStage(
                "terrain-collider",
                terrainTimer,
                ref previousTerrainStageEnd);
        }

        private float FoliageColonyInfluenceAt(Vector2 point)
        {
            if (foliageSpatialHash != null)
            {
                return foliageSpatialHash.MaximumLinearInfluence(
                    point,
                    4.5f);
            }

            float influence = 0f;
            for (int index = 0;
                 index < generatedFoliageAnchors.Count;
                 index++)
            {
                float distance = Vector2.Distance(
                    point,
                    generatedFoliageAnchors[index]);
                if (distance >= 4.5f)
                {
                    continue;
                }
                influence = Mathf.Max(
                    influence,
                    1f - distance / 4.5f);
            }
            return influence;
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

            Mesh mesh = TrackRuntimeResource(new Mesh
            {
                name = name
            });
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
            bridgeNavigationRoutes.Clear();
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
                            MinimumBridgeSeparation)
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
                    Vector3 bridgeDirection =
                        ResolvePerpendicularCrossingDirection(
                            crossing.RoadDirection,
                            crossing.RiverDirection);
                    CreateBridgeAt(
                        crossing.Point,
                        bridgeDirection,
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
                Vector3 riverPrevious =
                    riverSamples[Mathf.Max(0, riverIndex - 1)];
                Vector3 riverNext =
                    riverSamples[Mathf.Min(
                        riverSamples.Count - 1,
                        riverIndex + 1)];
                direction = ResolvePerpendicularCrossingDirection(
                    direction,
                    Vector3.ProjectOnPlane(
                        riverNext - riverPrevious,
                        Vector3.up).normalized);
                CreateBridgeAt(point, direction, 1);
            }
        }

        private static Vector3 ResolvePerpendicularCrossingDirection(
            Vector3 roadDirection,
            Vector3 riverDirection)
        {
            Vector3 direction = Vector3.Cross(
                Vector3.up,
                riverDirection).normalized;
            if (direction.sqrMagnitude < 0.001f)
            {
                return roadDirection;
            }
            return Vector3.Dot(direction, roadDirection) < 0f
                ? -direction
                : direction;
        }

        private void CreateBridgeAt(
            Vector3 point,
            Vector3 direction,
            int bridgeNumber)
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(
                direction,
                Vector3.up).normalized;
            if (flatDirection.sqrMagnitude < 0.001f)
            {
                flatDirection = Vector3.forward;
            }
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
            }
            else
            {
                ConfigureImportedBridge(
                    bridge,
                    point,
                    direction);
            }
            RegisterBridgeNavigationRoute(
                bridge,
                point,
                flatDirection);
        }

        private void RegisterBridgeNavigationRoute(
            GameObject bridge,
            Vector3 point,
            Vector3 flatDirection)
        {
            Physics.SyncTransforms();
            float referenceDeckHeight =
                TryFindBridgeDeckHeight(
                    bridge,
                    point,
                    out float measuredDeckHeight)
                    ? measuredDeckHeight
                    : BridgeDeckHeight(point.x, point.z);
            bridgeNavigationRoutes.Add(
                new BridgeNavigationRoute
                {
                    Center = ToXZ(point),
                    AcrossDirection = new Vector2(
                        flatDirection.x,
                        flatDirection.z),
                    HalfLength = riverHalfWidth + 3.2f,
                    HalfWidth = Mathf.Max(
                        1.35f,
                        roadHalfWidth * 0.80f),
                    BridgeRoot = bridge.transform,
                    ReferenceDeckHeight = referenceDeckHeight
                });
        }

        private void CreateOcean(Material waterRuntime)
        {
            float extent = Mathf.Max(
                mapRadius * OceanVisualRadiusMultiplier,
                IslandGenerationExtent + mapRadius * 3f);
            int ringCount = IslandCoastSampleCount;
            var vertices = new Vector3[ringCount * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[ringCount * 6];
            for (int index = 0; index < ringCount; index++)
            {
                float angle = index * Mathf.PI * 2f / ringCount;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                float coastRadius = Mathf.Max(
                    1f,
                    layout.CoastRadiusAtAngle(angle) -
                        OceanShoreOverlap);
                float squareRadius = extent / Mathf.Max(
                    0.001f,
                    Mathf.Max(
                        Mathf.Abs(cosine),
                        Mathf.Abs(sine)));
                vertices[index] = new Vector3(
                    cosine * coastRadius,
                    oceanWaterLevel,
                    sine * coastRadius);
                vertices[ringCount + index] = new Vector3(
                    cosine * squareRadius,
                    oceanWaterLevel,
                    sine * squareRadius);
                uv[index] = new Vector2(
                    vertices[index].x / 8f,
                    vertices[index].z / 8f);
                uv[ringCount + index] = new Vector2(
                    vertices[ringCount + index].x / 8f,
                    vertices[ringCount + index].z / 8f);

                int next = (index + 1) % ringCount;
                int triangle = index * 6;
                triangles[triangle] = index;
                triangles[triangle + 1] = next;
                triangles[triangle + 2] = ringCount + next;
                triangles[triangle + 3] = index;
                triangles[triangle + 4] = ringCount + next;
                triangles[triangle + 5] = ringCount + index;
            }
            Mesh mesh = TrackRuntimeResource(new Mesh
            {
                name = "Endless Ocean Surface"
            });
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject ocean = new GameObject("Endless Ocean");
            ocean.transform.SetParent(generatedRoot, false);
            ocean.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = ocean.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateOceanMaterial(
                waterRuntime);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private Material CreateOceanMaterial(Material source)
        {
            Material material = TrackRuntimeResource(
                source != null
                    ? new Material(source)
                    : new Material(
                        Shader.Find(
                            "Universal Render Pipeline/Lit")));
            material.name = "Procedural Deep Ocean";
            Color deepBlue = new Color(
                0.024f,
                0.098f,
                0.212f,
                1f);
            Color blueCurrent = new Color(
                0.043f,
                0.216f,
                0.40f,
                1f);
            if (material.HasProperty("_DeepColor"))
            {
                material.SetColor("_DeepColor", deepBlue);
            }
            if (material.HasProperty("_CurrentColor"))
            {
                material.SetColor(
                    "_CurrentColor",
                    blueCurrent);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", deepBlue);
            }
            return material;
        }

        private void CreateIslandShoreBoundary()
        {
            Transform root = new GameObject(
                "Island Shoreline Boundary").transform;
            root.SetParent(generatedRoot, false);
            for (int segment = 0;
                 segment < CoastBarrierSegments;
                 segment++)
            {
                float startAngle = segment * Mathf.PI * 2f /
                    CoastBarrierSegments;
                float endAngle = (segment + 1f) * Mathf.PI * 2f /
                    CoastBarrierSegments;
                Vector3 start = CoastPoint(
                    startAngle,
                    CoastBarrierInset);
                Vector3 end = CoastPoint(
                    endAngle,
                    CoastBarrierInset);
                Vector3 center = (start + end) * 0.5f;
                Vector3 tangent = Vector3.ProjectOnPlane(
                    end - start,
                    Vector3.up).normalized;
                center.y = Mathf.Max(
                    oceanWaterLevel,
                    TerrainHeight(center.x, center.z)) + 5f;
                GameObject section = new GameObject(
                    $"Shore Collider {segment + 1:000}");
                section.transform.SetParent(root, false);
                section.transform.position = center;
                section.transform.rotation = Quaternion.FromToRotation(
                    Vector3.right,
                    tangent);
                BoxCollider collider = section.AddComponent<BoxCollider>();
                collider.size = new Vector3(
                    Vector3.Distance(start, end) + 0.8f,
                    18f,
                    1.1f);
            }
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

        private void ResolveCampSites(System.Random random)
        {
            campSites.Clear();
            int targetCount = random.Next(
                MinimumCampCount,
                MaximumCampCount + 1);
            float minimumRadius = mapRadius * 0.28f;
            float maximumRadius = mapRadius * 0.72f;
            for (int campIndex = 0;
                 campIndex < targetCount;
                 campIndex++)
            {
                bool isLevelTwo =
                    random.NextDouble() < LevelTwoCampChance;
                float footprintRadius = isLevelTwo ? 12.5f : 7.5f;
                float minimumNormalY = isLevelTwo ? 0.95f : 0.91f;
                float maximumHeightRange = isLevelTwo ? 1.3f : 1.65f;
                for (int attempt = 0;
                     attempt < CampPlacementAttempts;
                     attempt++)
                {
                    float angle =
                        (float)random.NextDouble() *
                        Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(Mathf.Lerp(
                        minimumRadius * minimumRadius,
                        maximumRadius * maximumRadius,
                        (float)random.NextDouble()));
                    var point = new Vector2(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius);
                    float trailDistance = DistanceToRoad(point);
                    Vector3 groundNormal =
                        TerrainNormalAt(point.x, point.y);
                    if (!IsInsideIsland(
                            point,
                            footprintRadius + 2f) ||
                        trailDistance < CampTrailMinimumDistance ||
                        trailDistance > CampTrailMaximumDistance ||
                        DistanceToRiverExact(point) <
                            CampRiverClearance ||
                        Vector2.Distance(
                            point,
                            ToXZ(layout.PlayerStart)) < 34f ||
                        Vector2.Distance(
                            point,
                            ToXZ(layout.Extraction)) < 30f ||
                        IsInsideObeliskClearance(point, 18f) ||
                        groundNormal.y < minimumNormalY ||
                        CampFootprintHeightRange(
                            point,
                            footprintRadius) > maximumHeightRange ||
                        HasNearbyCamp(point, MinimumCampSeparation))
                    {
                        continue;
                    }

                    int guardCount = random.Next(
                        1,
                        MaximumCampGuardCount + 1);
                    int tentCount = isLevelTwo
                        ? random.Next(3, 5)
                        : random.Next(2, 4);
                    var bowGuards = new bool[guardCount];
                    for (int guardIndex = 0;
                         guardIndex < guardCount;
                         guardIndex++)
                    {
                        bowGuards[guardIndex] =
                            random.NextDouble() < 0.5;
                    }
                    campSites.Add(new CampSite
                    {
                        Center = point,
                        Rotation =
                            (float)random.NextDouble() * 360f,
                        ClearingRadius = isLevelTwo
                            ? LevelTwoCampClearingRadius
                            : 10.2f + tentCount * 0.85f,
                        GroundHeight = TerrainHeight(point.x, point.y),
                        GroundSlope = new Vector2(
                            -groundNormal.x /
                                Mathf.Max(0.01f, groundNormal.y),
                            -groundNormal.z /
                                Mathf.Max(0.01f, groundNormal.y)),
                        GuardCount = guardCount,
                        TentCount = tentCount,
                        IsLevelTwo = isLevelTwo,
                        BowGuards = bowGuards
                    });
                    break;
                }
            }
        }

        private float CampFootprintHeightRange(
            Vector2 center,
            float radius)
        {
            float minimum = TerrainHeight(center.x, center.y);
            float maximum = minimum;
            for (int sample = 0; sample < 12; sample++)
            {
                float angle = sample * Mathf.PI * 2f / 12f;
                float sampleRadius = (sample & 1) == 0
                    ? radius
                    : radius * 0.55f;
                float height = TerrainHeight(
                    center.x + Mathf.Cos(angle) * sampleRadius,
                    center.y + Mathf.Sin(angle) * sampleRadius);
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }
            return maximum - minimum;
        }

        private bool HasNearbyCamp(
            Vector2 point,
            float minimumDistance)
        {
            float minimumSquared =
                minimumDistance * minimumDistance;
            for (int index = 0; index < campSites.Count; index++)
            {
                if ((campSites[index].Center - point).sqrMagnitude <
                    minimumSquared)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsInsideCampClearing(
            Vector2 point,
            float padding = 0f)
        {
            for (int index = 0; index < campSites.Count; index++)
            {
                float radius =
                    Mathf.Max(0f, campSites[index].ClearingRadius + padding);
                if ((campSites[index].Center - point).sqrMagnitude <
                    radius * radius)
                {
                    return true;
                }
            }
            return false;
        }

        private float CampGroundBlendAt(Vector2 point)
        {
            float blend = 0f;
            for (int index = 0; index < campSites.Count; index++)
            {
                CampSite site = campSites[index];
                float outerRadius = Mathf.Max(
                    0.01f,
                    site.ClearingRadius - 0.45f);
                float innerRadius = outerRadius * 0.58f;
                float edge = Mathf.InverseLerp(
                    innerRadius,
                    outerRadius,
                    Vector2.Distance(point, site.Center));
                float breakup = Mathf.PerlinNoise(
                    noiseOffsetB.x * 0.031f + point.x * 0.19f,
                    noiseOffsetB.y * 0.031f + point.y * 0.19f);
                float broadVariation = Mathf.Lerp(
                    0.72f,
                    1f,
                    breakup);
                blend = Mathf.Max(
                    blend,
                    (1f - Mathf.SmoothStep(0f, 1f, edge)) *
                    broadVariation);
            }
            return blend;
        }

        private void CreateForestCamps(System.Random random)
        {
            generatedCampCount = 0;
            generatedCampTentCount = 0;
            generatedCampWoodenBoxCount = 0;
            Transform root =
                new GameObject("Forest Camps").transform;
            root.SetParent(generatedRoot, false);

            for (int campIndex = 0;
                 campIndex < campSites.Count;
                 campIndex++)
            {
                CampSite site = campSites[campIndex];
                Transform camp = new GameObject(
                    $"Forest Camp {campIndex + 1} - " +
                    $"Level {(site.IsLevelTwo ? 2 : 1)}").transform;
                camp.SetParent(root, false);

                GameObject fire = CreateCampProp(
                    campfirePrefab,
                    camp,
                    site.Center,
                    1.4875f,
                    false,
                    campItemMaterial,
                    site.Rotation,
                    random,
                    true);
                if (fire != null)
                {
                    fire.name = "Central Campfire";
                }
                CreateCampfireEffect(camp, site.Center);

                Vector2 campForward =
                    DirectionFromAngle(site.Rotation);
                Vector2 campSide = new Vector2(
                    -campForward.y,
                    campForward.x);
                Vector2 firstTentDirection = campForward;
                Vector2 firstTentPoint = site.Center +
                    firstTentDirection * 5.8f;
                var tentPoints = new List<Vector2>(site.TentCount);
                for (int tentIndex = 0;
                     tentIndex < site.TentCount;
                     tentIndex++)
                {
                    float centeredIndex =
                        tentIndex - (site.TentCount - 1) * 0.5f;
                    float tentAngle = site.Rotation +
                        centeredIndex *
                            (site.IsLevelTwo ? 44f : 58f) +
                        Mathf.Lerp(
                            -2.5f,
                            2.5f,
                            (float)random.NextDouble());
                    Vector2 direction = DirectionFromAngle(tentAngle);
                    if (tentIndex == 0)
                    {
                        firstTentDirection = direction;
                    }
                    Vector2 tentPoint = site.Center + direction *
                        Mathf.Lerp(
                            site.IsLevelTwo ? 8.65f : 6.5f,
                            site.IsLevelTwo ? 9.15f : 7.1f,
                            (float)random.NextDouble());
                    tentPoints.Add(tentPoint);
                    if (tentIndex == 0)
                    {
                        firstTentPoint = tentPoint;
                    }
                    GameObject tent = CreateCampProp(
                        campTentPrefab,
                        camp,
                        tentPoint,
                        4.8f,
                        false,
                        campStructureMaterial,
                        YawFacingDirection(site.Center - tentPoint),
                        random,
                        true,
                        true);
                    if (tent != null)
                    {
                        tent.name = $"Guard Tent {tentIndex + 1}";
                        generatedCampTentCount++;
                    }
                }

                Vector2 side = new Vector2(
                    -firstTentDirection.y,
                    firstTentDirection.x);
                Vector2 potPoint = site.IsLevelTwo
                    ? site.Center + campForward * 0.65f +
                        campSide * 0.9f
                    : site.Center + side * 0.95f +
                        firstTentDirection * 0.25f;
                bool cookingSpitOverFire =
                    random.NextDouble() < CookingSpitOverFireChance;
                float cookingSpitSide =
                    random.NextDouble() < 0.5d ? -1f : 1f;
                Vector2 cookingSpitPoint = cookingSpitOverFire
                    ? site.Center
                    : site.Center +
                        campSide *
                            (cookingSpitSide *
                             CookingSpitNearFireDistance) +
                        campForward * 0.18f;
                Vector2 firewoodPoint = site.IsLevelTwo
                    ? site.Center + campForward * 3.35f -
                        campSide * 0.55f
                    : site.Center + side *
                        (site.ClearingRadius -
                         LevelOneFirewoodEdgeInset) -
                        firstTentDirection * 0.35f;
                CreateNamedCampProp(
                    campPotPrefab,
                    camp,
                    potPoint,
                    0.55f,
                    campItemMaterial,
                    site.Rotation + 30f,
                    "Cooking Pot",
                    random);
                CreateNamedCampProp(
                    campDryingRackPrefab,
                    camp,
                    cookingSpitPoint,
                    2.2f,
                    campItemMaterial,
                    site.Rotation + 90f,
                    "Cooking Spit",
                    random);
                CreateNamedCampProp(
                    campFirewoodPrefab,
                    camp,
                    firewoodPoint,
                    2.875f,
                    campItemMaterial,
                    site.Rotation - 18f,
                    "Firewood Pile",
                    random);
                if (site.IsLevelTwo)
                {
                    CreateLevelTwoCampDressing(
                        camp,
                        site,
                        campIndex,
                        campForward,
                        campSide,
                        random);
                }
                else
                {
                    Vector2 chestPoint = FindCampChestPoint(
                        site,
                        tentPoints,
                        firstTentPoint,
                        firstTentDirection,
                        side);
                    CreateLootableCampChest(
                        camp,
                        chestPoint,
                        site.Rotation + 90f,
                        "Camp Chest",
                        $"Camp Chest {campIndex + 1}",
                        random);
                    if (campWoodenBoxPrefab != null &&
                        random.NextDouble() <
                            LevelOneWoodenBoxChance)
                    {
                        Vector2 boxPoint =
                            FindLevelOneWoodenBoxPoint(
                                site,
                                tentPoints,
                                chestPoint,
                                firewoodPoint,
                                random);
                        GameObject box = CreateNamedCampProp(
                            campWoodenBoxPrefab,
                            camp,
                            boxPoint,
                            CampWoodenBoxTargetSize,
                            campItemMaterial,
                            site.Rotation +
                                Mathf.Lerp(
                                    -35f,
                                    35f,
                                    (float)random.NextDouble()),
                            "Wooden Box 1",
                            random);
                        if (box != null)
                        {
                            site.WoodenBoxCount = 1;
                            generatedCampWoodenBoxCount++;
                        }
                    }
                }
                generatedCampCount++;
            }
        }

        private void CreateLevelTwoCampDressing(
            Transform camp,
            CampSite site,
            int campIndex,
            Vector2 forward,
            Vector2 side,
            System.Random random)
        {
            CreateNamedCampProp(
                campBenchPrefab,
                camp,
                site.Center + side * 2.65f - forward * 0.15f,
                LevelTwoBenchTargetSize,
                campStructureMaterial,
                YawFacingDirection(-side),
                "Campfire Bench 1",
                random);
            CreateNamedCampProp(
                campBenchPrefab,
                camp,
                site.Center - side * 2.65f + forward * 0.2f,
                LevelTwoBenchTargetSize,
                campStructureMaterial,
                YawFacingDirection(side),
                "Campfire Bench 2",
                random);
            CreateNamedCampProp(
                campPotPrefab,
                camp,
                site.Center - side * 1.05f - forward * 0.2f,
                0.5f,
                campItemMaterial,
                site.Rotation - 20f,
                "Cooking Pot 2",
                random);

            float boxClusterSign =
                random.NextDouble() < 0.5d ? -1f : 1f;
            Vector2 boxClusterDirection = DirectionFromAngle(
                site.Rotation + boxClusterSign * 52f);
            Vector2 boxClusterCenter = site.Center +
                boxClusterDirection * 13.35f;
            List<GameObject> woodenBoxes =
                CreateLevelTwoWoodenBoxCluster(
                    camp,
                    site,
                    boxClusterCenter,
                    boxClusterDirection,
                    random);

            bool chestOnBox =
                woodenBoxes.Count >= 3 &&
                random.NextDouble() < 0.5d;
            Vector2 clusterChestPoint = chestOnBox
                ? ToXZ(woodenBoxes[2].transform.position)
                : boxClusterCenter + boxClusterDirection * 1.65f;
            GameObject clusteredChest = CreateLootableCampChest(
                camp,
                clusterChestPoint,
                YawFacingDirection(
                    site.Center - clusterChestPoint) + 90f,
                "Camp Chest 1 - Box Supplies",
                $"Level Two Camp {campIndex + 1} Chest 1",
                random);
            if (chestOnBox && clusteredChest != null)
            {
                PlacePropOnTop(
                    clusteredChest,
                    woodenBoxes[2]);
            }

            Vector2 normalChestDirection = DirectionFromAngle(
                site.Rotation - boxClusterSign * 52f);
            Vector2 normalChestPoint = site.Center +
                normalChestDirection * 12.85f;
            CreateLootableCampChest(
                camp,
                normalChestPoint,
                YawFacingDirection(
                    site.Center - normalChestPoint) + 90f,
                "Camp Chest 2",
                $"Level Two Camp {campIndex + 1} Chest 2",
                random);

            for (int barrelIndex = 0; barrelIndex < 2; barrelIndex++)
            {
                float sign = barrelIndex == 0 ? -1f : 1f;
                Vector2 direction = DirectionFromAngle(
                    site.Rotation + sign * 78f);
                CreateNamedCampProp(
                    campBarrelPrefab,
                    camp,
                    site.Center + direction * 10.75f,
                    1.35f,
                    campItemMaterial,
                    site.Rotation + sign * 12f,
                    $"Supply Barrel {barrelIndex + 1}",
                    random);
            }

            float[] innerAngles = { -112f, 112f };
            GameObject[] innerPrefabs =
            {
                campInnerBarricadePrefabA,
                campInnerBarricadePrefabB
            };
            for (int index = 0; index < innerAngles.Length; index++)
            {
                Vector2 direction = DirectionFromAngle(
                    site.Rotation + innerAngles[index]);
                Vector2 point = site.Center + direction * 12.4f;
                GameObject barricade = CreateNamedCampProp(
                    innerPrefabs[index],
                    camp,
                    point,
                    index == 0 ? 3.85f : 2.45f,
                    campStructureMaterial,
                    YawFacingDirection(direction) + 90f,
                    $"Inner Weapon Rack {index + 1}",
                    random);
                CreateCampSwordDisplay(
                    camp,
                    barricade,
                    site.Center,
                    YawFacingDirection(direction) +
                        (index == 0 ? 72f : 108f),
                    $"Rack Sword {index + 1}");
            }

            float[] outerAngles = { -140f, 180f, 140f };
            for (int index = 0; index < outerAngles.Length; index++)
            {
                Vector2 direction = DirectionFromAngle(
                    site.Rotation + outerAngles[index]);
                GameObject prefab = (index & 1) == 0
                    ? campOuterSpikePrefabA
                    : campOuterSpikePrefabB;
                CreateNamedCampProp(
                    prefab,
                    camp,
                    site.Center + direction * 15.15f,
                    index == 1 ? 3.35f : 3.05f,
                    campStructureMaterial,
                    YawFacingDirection(direction),
                    $"Outer Log Defense {index + 1}",
                    random);
            }
        }

        private List<GameObject> CreateLevelTwoWoodenBoxCluster(
            Transform camp,
            CampSite site,
            Vector2 center,
            Vector2 outward,
            System.Random random)
        {
            int targetCount = random.Next(2, 5);
            var boxes = new List<GameObject>(targetCount);
            Vector2 across = new Vector2(-outward.y, outward.x);
            if (random.NextDouble() < 0.5d)
            {
                across = -across;
            }

            Vector2[] groundOffsets =
            {
                Vector2.zero,
                Vector2.zero,
                across * 1.12f + outward * 0.10f,
                -across * 0.62f - outward * 0.92f
            };
            for (int index = 0; index < targetCount; index++)
            {
                Vector2 point = center + groundOffsets[index];
                GameObject box = CreateNamedCampProp(
                    campWoodenBoxPrefab,
                    camp,
                    point,
                    CampWoodenBoxTargetSize,
                    campItemMaterial,
                    site.Rotation +
                        Mathf.Lerp(
                            -24f,
                            24f,
                            (float)random.NextDouble()),
                    $"Wooden Box {index + 1}",
                    random);
                if (box == null)
                {
                    continue;
                }

                if (index == 1 && boxes.Count > 0)
                {
                    box.transform.position +=
                        new Vector3(
                            across.x * 0.08f,
                            0f,
                            across.y * 0.08f);
                    PlacePropOnTop(box, boxes[0]);
                }
                boxes.Add(box);
            }

            site.WoodenBoxCount = boxes.Count;
            generatedCampWoodenBoxCount += boxes.Count;
            return boxes;
        }

        private static void PlacePropOnTop(
            GameObject prop,
            GameObject support)
        {
            if (prop == null || support == null ||
                !TryGetRendererBounds(
                    prop.GetComponentsInChildren<Renderer>(true),
                    out Bounds propBounds) ||
                !TryGetRendererBounds(
                    support.GetComponentsInChildren<Renderer>(true),
                    out Bounds supportBounds))
            {
                return;
            }

            prop.transform.position += Vector3.up *
                (supportBounds.max.y - propBounds.min.y + 0.015f);
        }

        private static Vector2 FindLevelOneWoodenBoxPoint(
            CampSite site,
            IReadOnlyList<Vector2> tentPoints,
            Vector2 chestPoint,
            Vector2 firewoodPoint,
            System.Random random)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = site.Rotation +
                    (float)random.NextDouble() * 360f;
                Vector2 candidate = site.Center +
                    DirectionFromAngle(angle) *
                    Mathf.Lerp(
                        site.ClearingRadius * 0.66f,
                        site.ClearingRadius * 0.82f,
                        (float)random.NextDouble());
                if (Vector2.Distance(candidate, chestPoint) < 2.15f ||
                    Vector2.Distance(candidate, firewoodPoint) < 1.9f)
                {
                    continue;
                }

                bool clear = true;
                for (int index = 0; index < tentPoints.Count; index++)
                {
                    if (Vector2.Distance(
                            candidate,
                            tentPoints[index]) < 2.55f)
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear)
                {
                    return candidate;
                }
            }

            return site.Center -
                DirectionFromAngle(site.Rotation) *
                (site.ClearingRadius * 0.76f);
        }

        private GameObject CreateLootableCampChest(
            Transform camp,
            Vector2 point,
            float yaw,
            string objectName,
            string lootLabel,
            System.Random random)
        {
            GameObject chest = CreateNamedCampProp(
                campChestPrefab,
                camp,
                point,
                1.2f,
                campItemMaterial,
                yaw,
                objectName,
                random);
            if (chest == null)
            {
                return null;
            }

            RaidLootContainer loot =
                chest.GetComponent<RaidLootContainer>() ??
                chest.AddComponent<RaidLootContainer>();
            loot.ConfigureChest(lootLabel, random.Next());
            return chest;
        }

        private void CreateCampSwordDisplay(
            Transform camp,
            GameObject support,
            Vector2 campCenter,
            float yaw,
            string objectName)
        {
            if (support == null)
            {
                return;
            }

            Renderer[] supportRenderers =
                support.GetComponentsInChildren<Renderer>(true);
            if (!TryGetRendererBounds(
                    supportRenderers,
                    out Bounds supportBounds))
            {
                return;
            }

            Transform sword = new GameObject(objectName).transform;
            sword.SetParent(camp, false);
            const float swordScale = 1.18f;
            float bladeTipLocalHeight = campSwordBladeMesh != null
                ? 0.215f + campSwordBladeMesh.bounds.max.y
                : RaidShortSwordPresentation.LegacyAverageLength;
            float swordLength = bladeTipLocalHeight * swordScale;
            Vector2 supportPoint = new Vector2(
                supportBounds.center.x,
                supportBounds.center.z);
            Vector2 awayFromCamp = supportPoint - campCenter;
            if (awayFromCamp.sqrMagnitude < 0.0001f)
            {
                awayFromCamp = DirectionFromAngle(yaw);
            }
            awayFromCamp.Normalize();

            float horizontalLean = swordLength * 0.56f;
            Vector2 tipPoint = supportPoint +
                awayFromCamp * horizontalLean;
            float tipHeight = TerrainHeight(tipPoint.x, tipPoint.y) +
                0.025f;
            float verticalLean = Mathf.Sqrt(Mathf.Max(
                0.01f,
                swordLength * swordLength -
                    horizontalLean * horizontalLean));
            Vector3 hiltPoint = new Vector3(
                supportPoint.x,
                tipHeight + verticalLean,
                supportPoint.y);
            Vector3 bladeDirection = new Vector3(
                tipPoint.x,
                tipHeight,
                tipPoint.y) - hiltPoint;

            sword.position = hiltPoint;
            sword.rotation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    bladeDirection.normalized) *
                Quaternion.AngleAxis(yaw, Vector3.up);
            sword.localScale = Vector3.one * swordScale;

            RaidShortSwordPresentation.Replace(
                sword,
                unchecked(Seed * 486187739 +
                    Mathf.RoundToInt(supportBounds.center.x * 100f) * 7919 +
                    Mathf.RoundToInt(supportBounds.center.z * 100f)),
                swordLength / swordScale,
                campSwordBladeMaterial ?? campItemMaterial,
                campSwordGuardMaterial ?? campItemMaterial,
                campSwordGripMaterial ?? campItemMaterial);
        }

        private static void CreateCampSwordPrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Vector2 FindCampChestPoint(
            CampSite site,
            IReadOnlyList<Vector2> tentPoints,
            Vector2 firstTentPoint,
            Vector2 firstTentDirection,
            Vector2 tentSide)
        {
            // The tents fan toward +tentSide. Try the open side first, then
            // farther behind the first tent if another tent happens to swing
            // into that space because of its placement jitter.
            Vector2[] candidates =
            {
                firstTentPoint - tentSide * 3.75f +
                    firstTentDirection * 0.35f,
                firstTentPoint + firstTentDirection * 3.65f -
                    tentSide * 0.75f,
                firstTentPoint - tentSide * 4.35f
            };
            const float minimumTentClearance = 3.45f;
            float minimumTentClearanceSquared =
                minimumTentClearance * minimumTentClearance;
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Length;
                 candidateIndex++)
            {
                bool clear = true;
                for (int tentIndex = 0;
                     tentIndex < tentPoints.Count;
                     tentIndex++)
                {
                    if ((candidates[candidateIndex] -
                         tentPoints[tentIndex]).sqrMagnitude <
                        minimumTentClearanceSquared)
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear)
                {
                    return candidates[candidateIndex];
                }
            }
            return site.Center + firstTentDirection * 9.6f -
                tentSide * 1.1f;
        }

        private GameObject CreateCampProp(
            GameObject prefab,
            Transform parent,
            Vector2 point,
            float targetSize,
            bool normalizeByHeight,
            Material material,
            float yaw,
            System.Random random,
            bool addCollider,
            bool conformToSlope = false)
        {
            return CreateSceneryInstance(
                prefab,
                parent,
                point,
                targetSize,
                normalizeByHeight,
                material,
                random,
                addCollider,
                true,
                conformToSlope,
                yaw);
        }

        private static float YawFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }
            direction.Normalize();
            return Mathf.Atan2(direction.x, direction.y) *
                Mathf.Rad2Deg;
        }

        private GameObject CreateNamedCampProp(
            GameObject prefab,
            Transform parent,
            Vector2 point,
            float targetSize,
            Material material,
            float yaw,
            string name,
            System.Random random)
        {
            GameObject prop = CreateCampProp(
                prefab,
                parent,
                point,
                targetSize,
                false,
                material,
                yaw,
                random,
                true,
                true);
            if (prop != null)
            {
                prop.name = name;
            }
            return prop;
        }

        private void CreateCampfireEffect(
            Transform parent,
            Vector2 point)
        {
            GameObject effect = new GameObject("Animated Fire");
            effect.transform.SetParent(parent, false);
            effect.transform.position = SurfacePoint(
                new Vector3(point.x, 0f, point.y),
                0.34f);

            ParticleSystem particles =
                effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                0.34f,
                0.68f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                0.24f,
                0.62f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.085f,
                0.215f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.34f, 0.035f, 0.95f),
                new Color(1f, 0.78f, 0.16f, 0.95f));
            main.maxParticles = 64;
            main.simulationSpace =
                ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 48f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.15f;
            shape.angle = 7f;
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.07f;
            noise.frequency = 1.25f;
            noise.scrollSpeed = 0.22f;
            ParticleSystem.ColorOverLifetimeModule colors =
                particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        new Color(1f, 0.82f, 0.22f), 0f),
                    new GradientColorKey(
                        new Color(1f, 0.18f, 0.015f), 0.72f),
                    new GradientColorKey(
                        new Color(0.16f, 0.02f, 0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.75f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            colors.color = gradient;

            Shader shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader != null)
            {
                var material = new Material(shader)
                {
                    name = "Generated Campfire Flame"
                };
                Texture2D flameTexture =
                    CreateSoftCampfireParticleTexture();
                generatedRuntimeResources.Add(flameTexture);
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", flameTexture);
                }
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", flameTexture);
                }
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Surface", 1f);
                material.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha);
                material.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                generatedRuntimeResources.Add(material);
                ParticleSystemRenderer renderer =
                    particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
            }

            Light fireLight = effect.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.42f, 0.12f);
            fireLight.intensity = 1.25f;
            fireLight.range = 4.5f;
            fireLight.shadows = LightShadows.None;
            particles.Play();
        }

        private static Texture2D CreateSoftCampfireParticleTexture()
        {
            const int size = 32;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Generated Soft Campfire Particle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedX =
                        ((x + 0.5f) / size - 0.5f) * 2f;
                    float normalizedY =
                        ((y + 0.5f) / size - 0.5f) * 2f;
                    float distance = Mathf.Sqrt(
                        normalizedX * normalizedX +
                        normalizedY * normalizedY);
                    float alpha = Mathf.Pow(
                        Mathf.Clamp01(1f - distance),
                        2.15f);
                    pixels[y * size + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void CreateRareFireflyZone(int seed)
        {
            generatedFireflyZoneCount = 0;
            generatedFireflyZoneCenters.Clear();
            var random = new System.Random(
                unchecked(seed ^ FireflySeedSalt));
            if (random.NextDouble() >= FireflyMapChance)
            {
                return;
            }

            for (int attempt = 0;
                 attempt < FireflyPlacementAttempts;
                 attempt++)
            {
                float angle = (float)random.NextDouble() *
                    Mathf.PI * 2f;
                float radius = Mathf.Sqrt(
                    Mathf.Lerp(
                        0.03f,
                        0.61f,
                        (float)random.NextDouble())) *
                    mapRadius;
                var point = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius);
                HabitatSample habitat = ForestHabitatAt(point);
                if (habitat.CanopyInfluence < 0.28f ||
                    habitat.MoistureTendency < 0.46f ||
                    DistanceToRoad(point) < 7.5f ||
                    IsInsideCampClearing(point, 3f) ||
                    IsInsideSpawnSolidClearance(point) ||
                    IsInsideObeliskClearance(point, 11f))
                {
                    continue;
                }

                CreateFireflyParticles(point, seed);
                generatedFireflyZoneCenters.Add(point);
                generatedFireflyZoneCount = 1;
                return;
            }
        }

        private void CreateFireflyParticles(
            Vector2 point,
            int seed)
        {
            GameObject effect = new GameObject(
                "Rare Firefly Pocket");
            effect.transform.SetParent(generatedRoot, false);
            effect.transform.position = SurfacePoint(
                new Vector3(point.x, 0f, point.y),
                0.75f);

            ParticleSystem particles =
                effect.AddComponent<ParticleSystem>();
            particles.useAutoRandomSeed = false;
            particles.randomSeed = unchecked((uint)seed ^ 0x91e10da5u);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 9f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                6.5f,
                10.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                0.035f,
                0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.035f,
                0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.78f, 0.14f, 0.82f),
                new Color(1f, 0.94f, 0.48f, 0.96f));
            main.gravityModifier = -0.006f;
            main.maxParticles = 22;
            main.simulationSpace =
                ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                1.35f,
                1.9f);
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                FireflyZoneRadius * 2f,
                1.45f,
                FireflyZoneRadius * 2f);
            shape.randomDirectionAmount = 1f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = 0.24f;
            noise.strengthY = 0.12f;
            noise.strengthZ = 0.24f;
            noise.frequency = 0.24f;
            noise.scrollSpeed = 0.09f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule colors =
                particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        new Color(1f, 0.76f, 0.10f), 0f),
                    new GradientColorKey(
                        new Color(1f, 0.94f, 0.42f), 0.5f),
                    new GradientColorKey(
                        new Color(1f, 0.72f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.92f, 0.12f),
                    new GradientAlphaKey(0.08f, 0.34f),
                    new GradientAlphaKey(0.78f, 0.56f),
                    new GradientAlphaKey(0.04f, 0.77f),
                    new GradientAlphaKey(0.68f, 0.91f),
                    new GradientAlphaKey(0f, 1f)
                });
            colors.color = gradient;

            Shader shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader != null)
            {
                Material material = TrackRuntimeResource(
                    new Material(shader)
                    {
                        name = "Generated Firefly Glow"
                    });
                Texture2D glowTexture = TrackRuntimeResource(
                    CreateSoftCampfireParticleTexture());
                glowTexture.name = "Generated Soft Firefly Glow";
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", glowTexture);
                }
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", glowTexture);
                }
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }
                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat(
                        "_SrcBlend",
                        (float)BlendMode.SrcAlpha);
                }
                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat(
                        "_DstBlend",
                        (float)BlendMode.One);
                }
                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 0f);
                }
                material.EnableKeyword(
                    "_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                ParticleSystemRenderer renderer =
                    particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            particles.Play();
        }

        private static Vector2 DirectionFromAngle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));
        }

        private void CreateForest(System.Random random)
        {
            generatedTreeCount = 0;
            generatedTreePositions.Clear();
            treeSpatialHash = null;
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
            float minimumSpacing = 1.95f;
            var treeSpacing =
                new PointSpatialHash(minimumSpacing);
            treeSpatialHash = treeSpacing;
            for (int attempt = 0;
                 attempt < attempts &&
                 generatedTreeCount < treeCount;
                 attempt++)
            {
                Vector2 point = RandomDiscPoint(
                    random,
                    CoastPlacementInset);
                if (DistanceToRoadWithin(
                        point,
                        treeClearance) <
                        treeClearance ||
                    DistanceToRiver(point) <
                        riverHalfWidth + 2f ||
                    Vector2.Distance(
                        point,
                        ToXZ(layout.PlayerStart)) <
                        7.5f ||
                    Vector2.Distance(
                        point,
                        ToXZ(layout.Extraction)) <
                        7.5f ||
                    IsInsideObeliskClearance(
                        point,
                        ObeliskTreeClearance) ||
                    IsInsideCampClearing(point, 1.1f) ||
                    treeSpacing.HasNearby(
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
                        (float)random.NextDouble()) *
                    treeScaleMultiplier;
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
                            0.62f * treeScaleMultiplier,
                            0.62f * treeScaleMultiplier,
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
                treeSpacing.Add(point);
                generatedTreeCount++;
            }
        }

        private void CreateGroundScenery(
            System.Random random)
        {
            var sceneryTimer = Stopwatch.StartNew();
            double previousSceneryStageEnd = 0d;
            generatedGrassCount = 0;
            generatedGrassVariantCounts =
                grassPrefabs != null
                    ? new int[grassPrefabs.Length]
                    : Array.Empty<int>();
            generatedUndergrowthCount = 0;
            generatedGroundFloraStudyCount = 0;
            generatedGroundFloraColonyCount = 0;
            generatedGroundFloraTreePocketCount = 0;
            generatedGroundFloraBoulderPocketCount = 0;
            generatedBoulderCount = 0;
            generatedTrailStoneCount = 0;
            generatedBushGroupCount = 0;
            generatedFlowerPatchCount = 0;
            generatedBoulderGrassCount = 0;
            generatedTreeBaseGrassCount = 0;
            generatedPlantEdgeGrassCount = 0;
            generatedTreeBaseFoliageCount = 0;
            generatedBoulderBaseFoliageCount = 0;
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
            List<GameObject> groundFloraStudies =
                CollectValidPrefabs(
                    groundFloraStudyPrefabs);
            List<GameObject> rocks =
                CollectValidPrefabs(rockPrefabs);

            if (rocks.Count > 0)
            {
                CreateBoulders(
                    random,
                    rocks);
            }
            RecordGenerationStage(
                "scenery-boulders",
                sceneryTimer,
                ref previousSceneryStageEnd);
            BuildForestHabitatField();
            RecordGenerationStage(
                "scenery-habitat-field",
                sceneryTimer,
                ref previousSceneryStageEnd);
            if (undergrowth.Count > 0)
            {
                CreateUndergrowth(
                    random,
                    undergrowth);
            }
            RecordGenerationStage(
                "scenery-undergrowth",
                sceneryTimer,
                ref previousSceneryStageEnd);
            if (groundFloraStudies.Count > 0)
            {
                CreateGroundFloraStudies(
                    random,
                    groundFloraStudies);
            }
            RecordGenerationStage(
                "scenery-ground-flora",
                sceneryTimer,
                ref previousSceneryStageEnd);
            if (grasses.Count > 0)
            {
                CreateGrassCoverage(
                    random,
                    grasses);
            }
            RecordGenerationStage(
                "scenery-grass",
                sceneryTimer,
                ref previousSceneryStageEnd);
            if (rocks.Count > 0)
            {
                CreateTrailStones(
                    random,
                    rocks);
            }
            foliageSpatialHash = new PointSpatialHash(4.5f);
            for (int index = 0;
                 index < generatedFoliageAnchors.Count;
                 index++)
            {
                foliageSpatialHash.Add(
                    generatedFoliageAnchors[index]);
            }
            RecordGenerationStage(
                "scenery-trail-stones",
                sceneryTimer,
                ref previousSceneryStageEnd);
        }

        private void CreateGrassCoverage(
            System.Random random,
            List<GameObject> prefabs)
        {
            var grassTimer = Stopwatch.StartNew();
            grassChunkCombineMilliseconds = 0d;
            grassChunkTintMilliseconds = 0d;
            grassChunkFinalizeMilliseconds = 0d;
            grassChunkInstances.Clear();
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
            int generatedBaseGrassCount = 0;
            float usableRadius = mapRadius - 2.5f;
            float gridExtent = IslandGenerationExtent - 2.5f;
            float cellSpacing =
                Mathf.Sqrt(
                    Mathf.PI * usableRadius *
                    usableRadius /
                    (grassCount * 2.40f *
                        GrassCoverageMultiplier));
            int cellsAcross =
                Mathf.CeilToInt(
                    gridExtent * 2f /
                    cellSpacing);
            float gridStart =
                -gridExtent + cellSpacing * 0.5f;
            // Evaluate the complete island. Stopping when a global
            // placement target was reached produced a visible straight
            // cutoff in whichever rows happened to be visited last.
            for (int row = 0;
                 row < cellsAcross;
                 row++)
            {
                float stagger =
                    (row & 1) == 0
                        ? 0f
                        : cellSpacing * 0.5f;
                for (int column = 0;
                     column < cellsAcross;
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
                    if (!IsInsideIsland(
                            point,
                            CoastPlacementInset))
                    {
                        continue;
                    }
                    float signedRoadDistance =
                        SignedDistanceToRoad(point);
                    if (signedRoadDistance <
                        GrassRoadInteriorLimit ||
                        DistanceToRiver(point) <
                        riverHalfWidth +
                            GrassRiverClearance)
                    {
                        continue;
                    }

                    HabitatSample habitat =
                        ForestHabitatAt(point);
                    float density =
                        habitat.GrassDensity;
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
                    float campGroundBlend =
                        CampGroundBlendAt(point);
                    baseHeight *= Mathf.Lerp(
                        1f,
                        0.18f,
                        campGroundBlend);
                    baseFootprint *= Mathf.Lerp(
                        1f,
                        0.88f,
                        campGroundBlend);
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
                        habitat.Weight(
                            (int)ForestHabitat
                                .CreepingGroundcover) < 0.42f &&
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
                            batch);
                        batch.Clear();
                        placementsInBatch = 0;
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
                HabitatSample boulderHabitat =
                    ForestHabitatAt(boulder.Position);
                if (boulderHabitat.GrassDensity < 0.30f ||
                    random.NextDouble() > 0.46)
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
                float shelterAngle = Mathf.Atan2(
                    boulder.ShelterDirection.y,
                    boulder.ShelterDirection.x);
                for (int pocketIndex = 0;
                     pocketIndex < pocketCount;
                     pocketIndex++)
                {
                    float angle =
                        shelterAngle +
                        Mathf.Lerp(
                            -0.82f,
                            0.82f,
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
                    if (!IsInsideIsland(
                            point,
                            CoastPlacementInset) ||
                        SignedDistanceToRoad(point) <
                            GrassRoadInteriorLimit ||
                        DistanceToRiver(point) <
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
                            batch);
                        batch.Clear();
                        placementsInBatch = 0;
                    }
                }
            }

            for (int treeIndex = 0;
                 treeIndex < generatedTreePositions.Count;
                 treeIndex += 3)
            {
                HabitatSample treeHabitat =
                    ForestHabitatAt(
                        generatedTreePositions[treeIndex]);
                if (treeHabitat.GrassDensity < 0.40f ||
                    random.NextDouble() > 0.58)
                {
                    continue;
                }
                generatedTreeBaseGrassCount +=
                    AppendHabitatGrassPocket(
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
                        ref placementsInBatch);
            }

            for (int anchorIndex = 0;
                 anchorIndex <
                    generatedFoliageAnchors.Count;
                 anchorIndex++)
            {
                HabitatSample edgeHabitat =
                    ForestHabitatAt(
                        generatedFoliageAnchors[anchorIndex]);
                if (edgeHabitat.Weight(
                        (int)ForestHabitat
                            .CreepingGroundcover) > 0.44f)
                {
                    continue;
                }
                generatedPlantEdgeGrassCount +=
                    AppendHabitatGrassPocket(
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
                        ref placementsInBatch);
            }

            if (placementsInBatch > 0)
            {
                CreateGrassBatch(
                    batch);
            }
            generationStageMilliseconds[
                "scenery-grass-placement"] =
                    grassTimer.Elapsed.TotalMilliseconds;
            FlushGrassBatches(root);
            generationStageMilliseconds[
                "scenery-grass-chunk-combine"] =
                    grassChunkCombineMilliseconds;
            generationStageMilliseconds[
                "scenery-grass-chunk-tint"] =
                    grassChunkTintMilliseconds;
            generationStageMilliseconds[
                "scenery-grass-chunk-finalize"] =
                    grassChunkFinalizeMilliseconds;
        }

        private int AppendHabitatGrassPocket(
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
            ref int placementsInBatch)
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
                if (!IsInsideIsland(
                        point,
                        CoastPlacementInset) ||
                    SignedDistanceToRoad(point) <
                        GrassRoadInteriorLimit ||
                    DistanceToRiver(point) <
                        riverHalfWidth +
                        GrassRiverClearance)
                {
                    continue;
                }
                if (ForestHabitatAt(point).GrassDensity < 0.28f)
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
                        batch);
                    batch.Clear();
                    placementsInBatch = 0;
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
                        ImportedRotation =
                            prefab.transform.rotation,
                        LocalBounds = bounds,
                        Parts = parts.ToArray()
                    });
            }
            return sources;
        }

        private void CreateGrassBatch(
            List<CombineInstance> instances)
        {
            if (instances.Count == 0)
            {
                return;
            }

            for (int index = 0; index < instances.Count; index++)
            {
                CombineInstance instance = instances[index];
                Vector4 position =
                    instance.transform.GetColumn(3);
                Vector2Int chunk = new Vector2Int(
                    Mathf.FloorToInt(
                        position.x / EnvironmentChunkSize),
                    Mathf.FloorToInt(
                        position.z / EnvironmentChunkSize));
                if (!grassChunkInstances.TryGetValue(
                        chunk,
                        out List<CombineInstance> chunkInstances))
                {
                    chunkInstances =
                        new List<CombineInstance>(1024);
                    grassChunkInstances.Add(
                        chunk,
                        chunkInstances);
                }
                chunkInstances.Add(instance);
            }
        }

        private void FlushGrassBatches(Transform parent)
        {
            var chunks = new List<Vector2Int>(
                grassChunkInstances.Keys);
            chunks.Sort(
                (left, right) =>
                {
                    int y = left.y.CompareTo(right.y);
                    return y != 0
                        ? y
                        : left.x.CompareTo(right.x);
                });
            foreach (Vector2Int chunk in chunks)
            {
                CreateGrassChunk(
                    parent,
                    chunk,
                    grassChunkInstances[chunk]);
            }
            grassChunkInstances.Clear();
        }

        private void CreateGrassChunk(
            Transform parent,
            Vector2Int chunk,
            List<CombineInstance> instances)
        {
            var chunkTimer = Stopwatch.StartNew();
            Mesh mesh = TrackRuntimeResource(new Mesh
            {
                name =
                    $"Meadow Grass Chunk " +
                    $"{chunk.x},{chunk.y}",
                indexFormat = IndexFormat.UInt32
            });
            mesh.CombineMeshes(
                instances.ToArray(),
                true,
                true,
                false);
            double combineEnd =
                chunkTimer.Elapsed.TotalMilliseconds;
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
            double tintEnd =
                chunkTimer.Elapsed.TotalMilliseconds;
            mesh.RecalculateBounds();
            if (Application.isPlaying)
            {
                mesh.UploadMeshData(true);
            }

            GameObject batch =
                new GameObject(
                    $"Meadow Grass Chunk " +
                    $"{chunk.x},{chunk.y}");
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
            double finalizeEnd =
                chunkTimer.Elapsed.TotalMilliseconds;
            grassChunkCombineMilliseconds += combineEnd;
            grassChunkTintMilliseconds +=
                tintEnd - combineEnd;
            grassChunkFinalizeMilliseconds +=
                finalizeEnd - tintEnd;
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
                    undergrowthCount * 0.60f);
            int clusterAttempts =
                undergrowthCount * 6;
            int clusterIndex = 0;
            for (int attempt = 0;
                 attempt < clusterAttempts &&
                 generatedUndergrowthCount <
                    generalUndergrowthTarget;
                 attempt++)
            {
                Vector2 center =
                    RandomDiscPoint(
                        random,
                        3.4f);
                if (SignedDistanceToRoad(center) <
                        1.15f ||
                    DistanceToRiver(center) <
                        riverHalfWidth + 0.72f ||
                    IsInsideCampClearing(center, -0.25f) ||
                    HasNearbyTree(
                        clusterCenters,
                        center,
                        5.2f))
                {
                    continue;
                }

                HabitatSample colonyHabitat =
                    ForestHabitatAt(center);
                int dominantHabitat =
                    colonyHabitat.DominantIndex;
                List<GameObject> choices;
                if (dominantHabitat ==
                    (int)ForestHabitat.CreepingGroundcover)
                {
                    choices = groundCover.Count > 0
                        ? groundCover
                        : prefabs;
                }
                else if (dominantHabitat ==
                        (int)ForestHabitat.MossCarpet ||
                    dominantHabitat ==
                        (int)ForestHabitat.CanopyDuff)
                {
                    choices = groundCover.Count > 0 &&
                        random.NextDouble() < 0.68
                            ? groundCover
                            : bushes.Count > 0
                                ? bushes
                                : prefabs;
                }
                else if (dominantHabitat ==
                    (int)ForestHabitat.StonyLichenSoil)
                {
                    if (random.NextDouble() < 0.34)
                    {
                        continue;
                    }
                    choices = groundCover.Count > 0
                        ? groundCover
                        : bushes.Count > 0
                            ? bushes
                            : prefabs;
                }
                else
                {
                    int clusterType = clusterIndex % 3;
                    choices = clusterType == 0 && bushes.Count > 0
                        ? bushes
                        : clusterType == 1 && flowers.Count > 0
                            ? flowers
                            : groundCover.Count > 0
                                ? groundCover
                                : prefabs;
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
                        ? random.Next(7, 14)
                        : flowerCluster
                            ? random.Next(14, 25)
                            : random.Next(10, 19);
                desiredGroupSize =
                    Mathf.Min(
                        desiredGroupSize,
                        generalUndergrowthTarget -
                        generatedUndergrowthCount);
                float clusterRadius =
                    bushCluster
                        ? 2.15f
                        : flowerCluster
                            ? 2.62f
                            : 2.05f;
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
                        DistanceToRiver(point) <
                            riverHalfWidth + 0.48f ||
                        IsInsideBoulderCore(point) ||
                        IsInsideCampClearing(point, -0.25f))
                    {
                        continue;
                    }

                    GameObject memberPrefab = prefab;
                    if (member > 3 &&
                        random.NextDouble() < 0.12)
                    {
                        List<GameObject> compatible =
                            flowerCluster && groundCover.Count > 0
                                ? groundCover
                                : !flowerCluster && flowers.Count > 0
                                    ? flowers
                                    : choices;
                        memberPrefab = compatible[
                            random.Next(compatible.Count)];
                    }
                    float targetHeight =
                        UndergrowthHeight(
                            memberPrefab.name,
                            random) *
                        Mathf.Lerp(
                            0.82f,
                            1.18f,
                            (float)random.NextDouble());
                    GameObject detail =
                        CreateSceneryInstance(
                            memberPrefab,
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
                        $"{memberPrefab.name} {groupLabel} " +
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
            int treeHabitatTarget =
                Mathf.RoundToInt(
                    undergrowthCount * 0.80f);
            for (int attempt = 0;
                 attempt < treeAttempts &&
                 generatedUndergrowthCount <
                    treeHabitatTarget;
                 attempt++)
            {
                Vector2 treeCenter =
                    generatedTreePositions[
                        (treeStart + attempt * 7) %
                        generatedTreePositions.Count];
                HabitatSample treeHabitat =
                    ForestHabitatAt(treeCenter);
                if (treeHabitat.CanopyInfluence < 0.38f ||
                    random.NextDouble() > 0.38)
                {
                    continue;
                }
                List<GameObject> choices =
                    groundCover.Count > 0 &&
                    (treeHabitat.MoistureTendency > 0.46f ||
                     random.NextDouble() < 0.62)
                        ? groundCover
                        : bushes.Count > 0
                            ? bushes
                            : prefabs;
                GameObject prefab =
                    choices[random.Next(choices.Count)];
                int groupSize =
                    Mathf.Min(
                        random.Next(4, 9),
                        treeHabitatTarget -
                        generatedUndergrowthCount);
                int placedAtTree = 0;
                float pocketAngle = Mathf.Lerp(
                    -Mathf.PI,
                    Mathf.PI,
                    Mathf.PerlinNoise(
                        noiseOffsetA.x * 0.023f +
                            treeCenter.x * 0.091f,
                        noiseOffsetA.y * 0.023f +
                            treeCenter.y * 0.091f));
                for (int member = 0;
                     member < groupSize;
                     member++)
                {
                    float angle =
                        pocketAngle +
                        Mathf.Lerp(
                            -0.78f,
                            0.78f,
                            (float)random.NextDouble());
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
                        DistanceToRiver(point) <
                            riverHalfWidth + 0.45f ||
                        IsInsideBoulderCore(point) ||
                        IsInsideCampClearing(point, -0.25f))
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

            int boulderStart =
                generatedBoulderPlacements.Count > 0
                    ? random.Next(
                        generatedBoulderPlacements.Count)
                    : 0;
            int boulderAttempts =
                generatedBoulderPlacements.Count * 3;
            for (int attempt = 0;
                 attempt < boulderAttempts &&
                 generatedUndergrowthCount < undergrowthCount;
                 attempt++)
            {
                BoulderPlacement boulder =
                    generatedBoulderPlacements[
                        (boulderStart + attempt * 5) %
                        generatedBoulderPlacements.Count];
                HabitatSample boulderHabitat =
                    ForestHabitatAt(boulder.Position);
                if (boulderHabitat.BoulderInfluence < 0.28f ||
                    random.NextDouble() > 0.56)
                {
                    continue;
                }
                List<GameObject> choices =
                    boulderHabitat.MoistureTendency > 0.44f &&
                    groundCover.Count > 0
                        ? groundCover
                        : bushes.Count > 0 &&
                          random.NextDouble() < 0.68
                            ? bushes
                            : flowers.Count > 0
                                ? flowers
                                : prefabs;
                GameObject prefab =
                    choices[random.Next(choices.Count)];
                int groupSize = Mathf.Min(
                    random.Next(4, 9),
                    undergrowthCount -
                        generatedUndergrowthCount);
                int placedAtBoulder = 0;
                float shelterAngle = Mathf.Atan2(
                    boulder.ShelterDirection.y,
                    boulder.ShelterDirection.x);
                for (int member = 0;
                     member < groupSize;
                     member++)
                {
                    float angle =
                        shelterAngle +
                        Mathf.Lerp(
                            -0.86f,
                            0.86f,
                            (float)random.NextDouble());
                    float distance = Mathf.Lerp(
                        boulder.Radius * 0.78f + 0.18f,
                        boulder.Radius + 1.45f,
                        Mathf.Sqrt(
                            (float)random.NextDouble()));
                    Vector2 point =
                        boulder.Position +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        distance;
                    if (SignedDistanceToRoad(point) < 0.72f ||
                        DistanceToRiver(point) <
                            riverHalfWidth + 0.45f ||
                        IsInsideBoulderCore(point) ||
                        IsInsideCampClearing(point, -0.25f))
                    {
                        continue;
                    }

                    float targetHeight =
                        UndergrowthHeight(
                            prefab.name,
                            random) *
                        Mathf.Lerp(
                            0.86f,
                            1.22f,
                            (float)random.NextDouble());
                    GameObject detail = CreateSceneryInstance(
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
                        $"{prefab.name} Boulder Habitat " +
                        $"{attempt + 1:000}-" +
                        $"{placedAtBoulder + 1:00}";
                    generatedUndergrowthCount++;
                    generatedBoulderBaseFoliageCount++;
                    placedAtBoulder++;
                }
            }
            CombineUndergrowthIntoSpatialChunks(root);
        }

        private void CreateGroundFloraStudies(
            System.Random random,
            List<GameObject> prefabs)
        {
            Transform root =
                new GameObject(
                    "Habitat Ground Flora Studies")
                    .transform;
            root.SetParent(generatedRoot, false);
            Material floraMaterial = null;
            Renderer sourceRenderer =
                prefabs[0].GetComponentInChildren<Renderer>(true);
            if (sourceRenderer != null)
            {
                floraMaterial = sourceRenderer.sharedMaterial;
            }

            var colonySpacing = new PointSpatialHash(
                groundFloraColonySpacing);
            foreach (Vector2 existingAnchor in generatedFoliageAnchors)
            {
                colonySpacing.Add(existingAnchor);
            }

            int generalTarget = Mathf.RoundToInt(
                groundFloraStudyCount *
                groundFloraGeneralShare);
            int generalAttempts =
                groundFloraStudyCount * 7;
            for (int attempt = 0;
                 attempt < generalAttempts &&
                 generatedGroundFloraStudyCount < generalTarget;
                 attempt++)
            {
                Vector2 center = RandomDiscPoint(random, 3.8f);
                if (!GroundFloraPointIsEligible(center, 1.08f) ||
                    colonySpacing.HasNearby(
                        center,
                        groundFloraColonySpacing))
                {
                    continue;
                }

                HabitatSample habitat = ForestHabitatAt(center);
                int primaryIndex = SelectGroundFloraVariant(
                    prefabs,
                    habitat,
                    random,
                    false,
                    false);
                if (primaryIndex < 0 ||
                    GroundFloraCompatibility(
                        prefabs[primaryIndex].name,
                        habitat,
                        false,
                        false) < 0.24f)
                {
                    continue;
                }

                string primaryName =
                    prefabs[primaryIndex].name;
                bool fernColony = IsFernStudy(primaryName);
                int desiredGroupSize = Mathf.Min(
                    fernColony
                        ? random.Next(10, 19)
                        : random.Next(18, 33),
                    generalTarget -
                        generatedGroundFloraStudyCount);
                float colonyRadius = fernColony
                    ? Mathf.Lerp(1.65f, 2.55f,
                        (float)random.NextDouble())
                    : Mathf.Lerp(2.25f, 3.75f,
                        (float)random.NextDouble());
                int placed = 0;
                for (int member = 0;
                     member < desiredGroupSize;
                     member++)
                {
                    float normalizedDistance = member == 0
                        ? 0f
                        : Mathf.Sqrt(
                            (float)random.NextDouble());
                    float angle =
                        (float)random.NextDouble() *
                        Mathf.PI * 2f;
                    Vector2 point = center +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        colonyRadius * normalizedDistance;
                    if (!GroundFloraPointIsEligible(point, 0.72f))
                    {
                        continue;
                    }

                    int variantIndex = primaryIndex;
                    if (member > 5 &&
                        random.NextDouble() < 0.11)
                    {
                        int secondary = SelectGroundFloraVariant(
                            prefabs,
                            ForestHabitatAt(point),
                            random,
                            false,
                            false);
                        if (secondary >= 0)
                        {
                            variantIndex = secondary;
                        }
                    }
                    HabitatSample pointHabitat =
                        ForestHabitatAt(point);
                    if (GroundFloraCompatibility(
                            prefabs[variantIndex].name,
                            pointHabitat,
                            false,
                            false) < 0.17f)
                    {
                        continue;
                    }

                    float scaleFalloff = Mathf.Lerp(
                        0.48f,
                        1.20f,
                        Mathf.Pow(
                            1f - normalizedDistance,
                            0.68f));
                    if (PlaceGroundFloraStudy(
                            prefabs[variantIndex],
                            root,
                            point,
                            scaleFalloff,
                            random,
                            $"Colony {generatedGroundFloraColonyCount + 1:000}",
                            placed + 1))
                    {
                        placed++;
                    }
                }

                if (placed > 0)
                {
                    colonySpacing.Add(center);
                    generatedFoliageAnchors.Add(center);
                    generatedGroundFloraColonyCount++;
                }
            }

            int treeTarget = Mathf.RoundToInt(
                groundFloraStudyCount * Mathf.Clamp01(
                    groundFloraGeneralShare +
                    groundFloraTreePocketShare));
            int treeStart = generatedTreePositions.Count > 0
                ? random.Next(generatedTreePositions.Count)
                : 0;
            int treeAttempts = generatedTreePositions.Count * 3;
            for (int attempt = 0;
                 attempt < treeAttempts &&
                 generatedGroundFloraStudyCount < treeTarget;
                 attempt++)
            {
                Vector2 treeCenter = generatedTreePositions[
                    (treeStart + attempt * 11) %
                    generatedTreePositions.Count];
                HabitatSample habitat =
                    ForestHabitatAt(treeCenter);
                if (habitat.CanopyInfluence < 0.34f ||
                    random.NextDouble() > 0.34 ||
                    colonySpacing.HasNearby(treeCenter, 3.25f))
                {
                    continue;
                }

                int primaryIndex = SelectGroundFloraVariant(
                    prefabs,
                    habitat,
                    random,
                    true,
                    false);
                if (primaryIndex < 0)
                {
                    continue;
                }
                int groupSize = Mathf.Min(
                    random.Next(6, 13),
                    treeTarget -
                        generatedGroundFloraStudyCount);
                float pocketAngle = Mathf.Lerp(
                    -Mathf.PI,
                    Mathf.PI,
                    Mathf.PerlinNoise(
                        noiseOffsetB.x * 0.019f +
                            treeCenter.x * 0.074f,
                        noiseOffsetB.y * 0.019f +
                            treeCenter.y * 0.074f));
                int placed = 0;
                for (int member = 0;
                     member < groupSize;
                     member++)
                {
                    float radial = Mathf.Sqrt(
                        (float)random.NextDouble());
                    float angle = pocketAngle +
                        Mathf.Lerp(
                            -0.72f,
                            0.72f,
                            (float)random.NextDouble());
                    Vector2 point = treeCenter +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        Mathf.Lerp(0.32f, 1.62f, radial);
                    if (!GroundFloraPointIsEligible(point, 0.66f))
                    {
                        continue;
                    }
                    float scaleFalloff = Mathf.Lerp(
                        0.58f,
                        1.18f,
                        1f - radial);
                    if (PlaceGroundFloraStudy(
                            prefabs[primaryIndex],
                            root,
                            point,
                            scaleFalloff,
                            random,
                            $"Tree Pocket {attempt + 1:000}",
                            placed + 1))
                    {
                        placed++;
                        generatedGroundFloraTreePocketCount++;
                    }
                }
                if (placed > 0)
                {
                    colonySpacing.Add(treeCenter);
                    generatedFoliageAnchors.Add(treeCenter);
                }
            }

            int boulderStart =
                generatedBoulderPlacements.Count > 0
                    ? random.Next(
                        generatedBoulderPlacements.Count)
                    : 0;
            int boulderAttempts =
                generatedBoulderPlacements.Count * 6;
            for (int attempt = 0;
                 attempt < boulderAttempts &&
                 generatedGroundFloraStudyCount <
                    groundFloraStudyCount;
                 attempt++)
            {
                BoulderPlacement boulder =
                    generatedBoulderPlacements[
                        (boulderStart + attempt * 7) %
                        generatedBoulderPlacements.Count];
                HabitatSample habitat =
                    ForestHabitatAt(boulder.Position);
                if (habitat.BoulderInfluence < 0.24f ||
                    random.NextDouble() > 0.62)
                {
                    continue;
                }
                Vector2 pocketCenter =
                    boulder.Position +
                    boulder.ShelterDirection *
                    (boulder.Radius + 0.62f);
                if (colonySpacing.HasNearby(pocketCenter, 2.9f))
                {
                    continue;
                }
                int primaryIndex = SelectGroundFloraVariant(
                    prefabs,
                    ForestHabitatAt(pocketCenter),
                    random,
                    false,
                    true);
                if (primaryIndex < 0)
                {
                    continue;
                }
                int groupSize = Mathf.Min(
                    random.Next(6, 13),
                    groundFloraStudyCount -
                        generatedGroundFloraStudyCount);
                int placed = 0;
                float shelterAngle = Mathf.Atan2(
                    boulder.ShelterDirection.y,
                    boulder.ShelterDirection.x);
                for (int member = 0;
                     member < groupSize;
                     member++)
                {
                    float radial = Mathf.Sqrt(
                        (float)random.NextDouble());
                    float angle = shelterAngle +
                        Mathf.Lerp(
                            -0.82f,
                            0.82f,
                            (float)random.NextDouble());
                    float distance = Mathf.Lerp(
                        boulder.Radius * 0.72f + 0.18f,
                        boulder.Radius + 1.72f,
                        radial);
                    Vector2 point = boulder.Position +
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        distance;
                    if (!GroundFloraPointIsEligible(point, 0.64f))
                    {
                        continue;
                    }
                    float scaleFalloff = Mathf.Lerp(
                        0.56f,
                        1.16f,
                        1f - radial);
                    if (PlaceGroundFloraStudy(
                            prefabs[primaryIndex],
                            root,
                            point,
                            scaleFalloff,
                            random,
                            $"Boulder Pocket {attempt + 1:000}",
                            placed + 1))
                    {
                        placed++;
                        generatedGroundFloraBoulderPocketCount++;
                    }
                }
                if (placed > 0)
                {
                    colonySpacing.Add(pocketCenter);
                    generatedFoliageAnchors.Add(pocketCenter);
                }
            }

            CombineSceneryIntoSpatialChunks(
                root,
                "Ground Flora",
                floraMaterial);
        }

        private bool GroundFloraPointIsEligible(
            Vector2 point,
            float trailClearance)
        {
            return IsInsideIsland(
                    point,
                    CoastPlacementInset) &&
                SignedDistanceToRoad(point) >= trailClearance &&
                DistanceToRiver(point) >=
                    riverHalfWidth + 0.52f &&
                !IsInsideBoulderCore(point) &&
                !IsInsideCampClearing(point, -0.35f) &&
                !IsInsideSpawnSolidClearance(point);
        }

        private int SelectGroundFloraVariant(
            List<GameObject> prefabs,
            HabitatSample habitat,
            System.Random random,
            bool treePocket,
            bool boulderPocket)
        {
            float total = 0f;
            if (groundFloraSelectionWeights.Length <
                prefabs.Count)
            {
                groundFloraSelectionWeights =
                    new float[prefabs.Count];
            }
            for (int index = 0; index < prefabs.Count; index++)
            {
                groundFloraSelectionWeights[index] =
                    GroundFloraCompatibility(
                    prefabs[index].name,
                    habitat,
                    treePocket,
                    boulderPocket);
                total += groundFloraSelectionWeights[index];
            }
            if (total <= 0.001f)
            {
                return -1;
            }
            float choice =
                (float)random.NextDouble() * total;
            for (int index = 0; index < prefabs.Count; index++)
            {
                choice -= groundFloraSelectionWeights[index];
                if (choice <= 0f)
                {
                    return index;
                }
            }
            return prefabs.Count - 1;
        }

        private static float GroundFloraCompatibility(
            string prefabName,
            HabitatSample habitat,
            bool treePocket,
            bool boulderPocket)
        {
            float loam = habitat.Weight(
                (int)ForestHabitat.MossyLoam);
            float duff = habitat.Weight(
                (int)ForestHabitat.CanopyDuff);
            float moss = habitat.Weight(
                (int)ForestHabitat.MossCarpet);
            float groundcover = habitat.Weight(
                (int)ForestHabitat.CreepingGroundcover);
            float stony = habitat.Weight(
                (int)ForestHabitat.StonyLichenSoil);
            float weight;
            if (ContainsIgnoreCase(prefabName, "shortsoft"))
            {
                weight = loam * 0.92f +
                    groundcover * 0.24f +
                    habitat.GrassDensity * 0.28f;
            }
            else if (ContainsIgnoreCase(prefabName, "wispy"))
            {
                weight = loam * 0.64f +
                    groundcover * 0.28f +
                    (1f - habitat.CanopyInfluence) * 0.32f;
            }
            else if (ContainsIgnoreCase(prefabName, "broadwoodland"))
            {
                weight = loam * 0.48f +
                    duff * 0.64f +
                    habitat.CanopyInfluence * 0.34f;
            }
            else if (ContainsIgnoreCase(prefabName, "tallarching"))
            {
                weight = loam * 0.74f +
                    moss * 0.28f +
                    habitat.BoulderInfluence * 0.26f;
            }
            else if (ContainsIgnoreCase(prefabName, "paleseed"))
            {
                weight = loam * 0.46f +
                    stony * 0.58f +
                    (1f - habitat.MoistureTendency) * 0.34f;
            }
            else if (ContainsIgnoreCase(prefabName, "drystraw"))
            {
                weight = duff * 0.62f +
                    stony * 0.72f +
                    (1f - habitat.MoistureTendency) * 0.48f;
            }
            else if (ContainsIgnoreCase(prefabName, "sedge"))
            {
                weight = moss * 0.74f +
                    groundcover * 0.56f +
                    habitat.MoistureTendency * 0.46f;
            }
            else if (ContainsIgnoreCase(prefabName, "patchedge"))
            {
                float transition = 1f - Mathf.Clamp01(
                    Mathf.Abs(habitat.GrassDensity - 0.50f) * 2.5f);
                weight = loam * 0.42f +
                    groundcover * 0.70f +
                    transition * 0.42f;
            }
            else if (ContainsIgnoreCase(prefabName, "bracken"))
            {
                weight = duff * 0.62f +
                    moss * 0.72f +
                    habitat.CanopyInfluence * 0.44f +
                    habitat.MoistureTendency * 0.24f;
            }
            else if (ContainsIgnoreCase(prefabName, "youngfern"))
            {
                weight = moss * 0.76f +
                    loam * 0.34f +
                    habitat.MoistureTendency * 0.42f;
            }
            else if (ContainsIgnoreCase(
                prefabName,
                "lowwoodlandfern"))
            {
                weight = moss * 0.72f +
                    duff * 0.58f +
                    habitat.CanopyInfluence * 0.40f;
            }
            else
            {
                weight = loam * 0.52f +
                    moss * 0.54f +
                    groundcover * 0.52f;
            }

            if (treePocket)
            {
                weight *= ContainsIgnoreCase(prefabName, "fern") ||
                    ContainsIgnoreCase(prefabName, "broadwoodland") ||
                    ContainsIgnoreCase(prefabName, "tallarching") ||
                    ContainsIgnoreCase(prefabName, "mosaic")
                        ? 1.75f
                        : ContainsIgnoreCase(prefabName, "seed") ||
                          ContainsIgnoreCase(prefabName, "straw")
                            ? 0.22f
                            : 0.72f;
            }
            if (boulderPocket)
            {
                weight *= ContainsIgnoreCase(prefabName, "fern") ||
                    ContainsIgnoreCase(prefabName, "sedge") ||
                    ContainsIgnoreCase(prefabName, "tallarching") ||
                    ContainsIgnoreCase(prefabName, "mosaic")
                        ? 1.82f
                        : ContainsIgnoreCase(prefabName, "wispy") ||
                          ContainsIgnoreCase(prefabName, "seed")
                            ? 0.38f
                            : 0.74f;
            }
            return Mathf.Max(0.01f, weight);
        }

        private bool PlaceGroundFloraStudy(
            GameObject prefab,
            Transform root,
            Vector2 point,
            float scaleFalloff,
            System.Random random,
            string groupLabel,
            int memberIndex)
        {
            float targetHeight =
                GroundFloraBaseHeight(prefab.name) *
                scaleFalloff *
                Mathf.Lerp(
                    0.88f,
                    1.12f,
                    (float)random.NextDouble());
            GameObject detail = CreateSceneryInstance(
                prefab,
                root,
                point,
                targetHeight,
                true,
                null,
                random,
                false,
                true,
                true);
            if (detail == null)
            {
                return false;
            }
            detail.name =
                $"{prefab.name} {groupLabel}-" +
                $"{memberIndex:00}";
            generatedGroundFloraStudyCount++;
            return true;
        }

        private static float GroundFloraBaseHeight(string name)
        {
            if (ContainsIgnoreCase(name, "tallarching")) return 1.16f;
            if (ContainsIgnoreCase(name, "seedhead")) return 1.02f;
            if (ContainsIgnoreCase(name, "bracken")) return 0.92f;
            if (ContainsIgnoreCase(name, "wispy")) return 0.80f;
            if (ContainsIgnoreCase(name, "straw")) return 0.76f;
            if (ContainsIgnoreCase(name, "mosaic")) return 0.74f;
            if (ContainsIgnoreCase(name, "broadwoodland")) return 0.68f;
            if (ContainsIgnoreCase(name, "youngfern")) return 0.56f;
            if (ContainsIgnoreCase(name, "shortsoft")) return 0.48f;
            if (ContainsIgnoreCase(name, "sedge")) return 0.42f;
            if (ContainsIgnoreCase(name, "lowwoodlandfern")) return 0.38f;
            return 0.68f;
        }

        private static bool ContainsIgnoreCase(
            string source,
            string value)
        {
            return source != null &&
                source.IndexOf(
                    value,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFernStudy(string name)
        {
            return name.IndexOf(
                "fern",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf(
                    "bracken",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CombineUndergrowthIntoSpatialChunks(
            Transform root)
        {
            CombineSceneryIntoSpatialChunks(
                root,
                "Undergrowth",
                plantDetailMaterial);
        }

        private void CombineSceneryIntoSpatialChunks(
            Transform root,
            string chunkLabel,
            Material batchMaterial)
        {
            var chunks = new Dictionary<Vector2Int,
                List<CombineInstance>>();
            var originalChildren = new List<GameObject>();
            for (int childIndex = 0;
                 childIndex < root.childCount;
                 childIndex++)
            {
                originalChildren.Add(
                    root.GetChild(childIndex).gameObject);
            }

            Matrix4x4 rootInverse = root.worldToLocalMatrix;
            foreach (GameObject child in originalChildren)
            {
                MeshFilter[] filters =
                    child.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter filter in filters)
                {
                    MeshRenderer renderer =
                        filter.GetComponent<MeshRenderer>();
                    if (renderer == null ||
                        !renderer.enabled ||
                        filter.sharedMesh == null ||
                        filter.name.StartsWith(
                            "UCX_",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (batchMaterial == null)
                    {
                        batchMaterial = renderer.sharedMaterial;
                    }

                    Vector3 worldPosition =
                        filter.transform.position;
                    Vector2Int chunk = new Vector2Int(
                        Mathf.FloorToInt(
                            worldPosition.x /
                            EnvironmentChunkSize),
                        Mathf.FloorToInt(
                            worldPosition.z /
                            EnvironmentChunkSize));
                    if (!chunks.TryGetValue(
                            chunk,
                            out List<CombineInstance> instances))
                    {
                        instances = new List<CombineInstance>(128);
                        chunks.Add(chunk, instances);
                    }

                    Matrix4x4 transformMatrix =
                        rootInverse *
                        filter.transform.localToWorldMatrix;
                    for (int subMesh = 0;
                         subMesh < filter.sharedMesh.subMeshCount;
                         subMesh++)
                    {
                        instances.Add(
                            new CombineInstance
                            {
                                mesh = filter.sharedMesh,
                                subMeshIndex = subMesh,
                                transform = transformMatrix
                            });
                    }
                }
                child.SetActive(false);
            }

            var chunkKeys = new List<Vector2Int>(chunks.Keys);
            chunkKeys.Sort(
                (left, right) =>
                {
                    int y = left.y.CompareTo(right.y);
                    return y != 0
                        ? y
                        : left.x.CompareTo(right.x);
                });
            foreach (Vector2Int chunk in chunkKeys)
            {
                Mesh mesh = TrackRuntimeResource(new Mesh
                {
                    name =
                        $"{chunkLabel} Chunk {chunk.x},{chunk.y}",
                    indexFormat = IndexFormat.UInt32
                });
                mesh.CombineMeshes(
                    chunks[chunk].ToArray(),
                    true,
                    true,
                    false);
                mesh.RecalculateBounds();
                if (Application.isPlaying)
                {
                    mesh.UploadMeshData(true);
                }

                GameObject batch = new GameObject(
                    $"{chunkLabel} Chunk {chunk.x},{chunk.y}");
                batch.transform.SetParent(root, false);
                batch.AddComponent<MeshFilter>()
                    .sharedMesh = mesh;
                MeshRenderer renderer =
                    batch.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batchMaterial;
                renderer.shadowCastingMode =
                    ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            foreach (GameObject original in originalChildren)
            {
                if (Application.isPlaying)
                {
                    Destroy(original);
                }
                else
                {
                    DestroyImmediate(original);
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
                    DistanceToRiver(point) <
                        riverHalfWidth + 0.25f ||
                    IsInsideSpawnSolidClearance(point) ||
                    IsInsideObeliskClearance(
                        point,
                        ObeliskBoulderClearance) ||
                    IsInsideCampClearing(point, 0.4f) ||
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
                    float habitatShelterAngle =
                        (float)random.NextDouble() *
                        Mathf.PI * 2f;
                    generatedBoulderPlacements.Add(
                        new BoulderPlacement
                        {
                            Position = point,
                            Radius = Mathf.Max(
                                boulderBounds.extents.x,
                                boulderBounds.extents.z),
                            ShelterDirection = new Vector2(
                                Mathf.Cos(habitatShelterAngle),
                                Mathf.Sin(habitatShelterAngle))
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
                if (DistanceToRiver(point) <
                        riverHalfWidth + 0.2f ||
                    IsInsideSpawnSolidClearance(point))
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

        private bool IsInsideSpawnSolidClearance(
            Vector2 point)
        {
            if (layout == null)
            {
                return false;
            }

            return
                Vector2.Distance(
                    point,
                    ToXZ(layout.PlayerStart)) <
                    SpawnSolidSceneryClearance ||
                Vector2.Distance(
                    point,
                    ToXZ(layout.Extraction)) <
                    SpawnSolidSceneryClearance;
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
            bool conformToSlope = false,
            float forcedYaw = float.NaN)
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
            float yaw = float.IsNaN(forcedYaw)
                ? (float)random.NextDouble() * 360f
                : forcedYaw;
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
                float settlingDepth =
                    Mathf.Min(
                        0.055f,
                        scaledBounds.size.y * 0.025f);
                Vector3 terrainNormal = TerrainNormalAt(
                    point.x,
                    point.y);
                terrainNormal = Vector3.RotateTowards(
                    Vector3.up,
                    terrainNormal,
                    Mathf.Deg2Rad * 22f,
                    0f).normalized;
                AlignVisibleBaseToSurfacePlane(
                    instance.transform,
                    renderers,
                    new Vector3(
                        point.x,
                        terrainHeight,
                        point.y),
                    terrainNormal,
                    settlingDepth);
            }
            else
            {
                instance.transform.position +=
                    Vector3.up *
                    (groundedBaseHeight -
                     scaledBounds.min.y);
            }

            if (addCollider)
            {
                AddExactVisibleMeshColliders(
                    renderers);
            }
            return instance;
        }

        private static void AlignVisibleBaseToSurfacePlane(
            Transform instance,
            Renderer[] renderers,
            Vector3 planePoint,
            Vector3 planeNormal,
            float settlingDepth)
        {
            float minimumDistance = float.PositiveInfinity;
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                MeshFilter filter = renderer != null
                    ? renderer.GetComponent<MeshFilter>()
                    : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0
                            ? bounds.min.x
                            : bounds.max.x,
                        (corner & 2) == 0
                            ? bounds.min.y
                            : bounds.max.y,
                        (corner & 4) == 0
                            ? bounds.min.z
                            : bounds.max.z);
                    Vector3 worldCorner =
                        filter.transform.TransformPoint(localCorner);
                    minimumDistance = Mathf.Min(
                        minimumDistance,
                        Vector3.Dot(
                            worldCorner - planePoint,
                            planeNormal));
                }
            }

            if (minimumDistance == float.PositiveInfinity)
            {
                return;
            }

            instance.position += planeNormal *
                (-minimumDistance - settlingDepth);
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
            float extent = IslandGenerationExtent;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var point = new Vector2(
                    Mathf.Lerp(
                        -extent,
                        extent,
                        (float)random.NextDouble()),
                    Mathf.Lerp(
                        -extent,
                        extent,
                        (float)random.NextDouble()));
                if (IsInsideIsland(point, edgeMargin))
                {
                    return point;
                }
            }
            return Vector2.zero;
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
                    0.20f,
                    0.42f,
                    t);
            }
            if (lower.Contains("flower"))
            {
                return Mathf.Lerp(
                    0.34f,
                    0.72f,
                    t);
            }
            return Mathf.Lerp(
                0.48f,
                1.02f,
                t);
        }

        private void ConfigureRaidAtmosphere()
        {
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor =
                new Color(
                    0.22f,
                    0.36f,
                    0.34f,
                    1f);
            RenderSettings.fogStartDistance = 21f;
            RenderSettings.fogEndDistance = 90f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor =
                new Color(0.34f, 0.48f, 0.46f, 1f);
            RenderSettings.ambientEquatorColor =
                new Color(0.20f, 0.34f, 0.31f, 1f);
            RenderSettings.ambientGroundColor =
                new Color(0.11f, 0.22f, 0.17f, 1f);
            RenderSettings.ambientIntensity = 1.02f;
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
                light.shadowStrength = 0.60f;
                light.transform.rotation =
                    Quaternion.Euler(62f, -42f, 0f);
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.farClipPlane =
                    Mathf.Max(
                        camera.farClipPlane,
                        mapRadius * 2.25f);
                camera.clearFlags =
                    skyboxMaterial != null
                        ? CameraClearFlags.Skybox
                        : CameraClearFlags.SolidColor;
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
            color.postExposure.Override(0.48f);
            color.contrast.Override(7f);
            color.saturation.Override(-16f);
            color.colorFilter.Override(
                new Color(0.82f, 0.98f, 0.91f, 1f));

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
            Vector3 start = layout != null
                ? layout.PlayerStart
                : Vector3.zero;
            Vector3 extraction = layout != null
                ? layout.Extraction
                : Vector3.zero;
            MoveActor(
                player,
                SurfacePoint(start, 1f));
            if (player != null)
            {
                Vector3 forward =
                    extraction - start;
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
                    SurfacePoint(
                        extraction,
                        0.12f);
            }

            if (enemies != null)
            {
                generatedGuardGroupCount = 0;
                generatedGuardPairCount = 0;
                var roads = new List<List<Vector3>>();
                var occupiedPatrolRoutes =
                    new List<Vector3[]>();
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
                        (remaining > Mathf.Max(
                             1,
                             5 - generatedGuardGroupCount) ||
                         (generatedGuardPairCount == 0 &&
                          generatedGuardGroupCount >= 1) ||
                         random.NextDouble() < 0.55);
                    int groupSize = pair ? 2 : 1;
                    List<Vector3> patrolRoad = null;
                    float t = 0.5f;
                    Vector3 groupCenter = Vector3.zero;
                    Vector3[] route = null;
                    for (int attempt = 0;
                         attempt < GuardPlacementAttempts;
                         attempt++)
                    {
                        int roadIndex =
                            (generatedGuardGroupCount +
                             random.Next(0, roads.Count)) %
                            roads.Count;
                        List<Vector3> candidateRoad =
                            roads[roadIndex];
                        float candidateT = Mathf.Lerp(
                            0.17f,
                            0.83f,
                            (float)random.NextDouble());
                        Vector3 candidateCenter =
                            RoadPointAt(
                                candidateRoad,
                                candidateT);
                        if (Vector3.Distance(
                                candidateCenter,
                                start) < 32f ||
                            Vector3.Distance(
                                candidateCenter,
                                extraction) < 24f ||
                            DistanceToRiverExact(
                                ToXZ(candidateCenter)) <
                                riverHalfWidth + 2.2f)
                        {
                            continue;
                        }

                        float candidateSpan = Mathf.Clamp(
                            GuardPatrolHalfLength /
                            Mathf.Max(
                                1f,
                                PolylineLength(candidateRoad)),
                            0.04f,
                            0.16f);
                        Vector3[] candidateRoute =
                            BuildPatrolRoute(
                                candidateRoad,
                                Mathf.Max(
                                    0.04f,
                                    candidateT - candidateSpan),
                                Mathf.Min(
                                    0.96f,
                                    candidateT + candidateSpan),
                                3);
                        if (!IsPatrolRouteSeparated(
                                candidateRoute,
                                occupiedPatrolRoutes,
                                MinimumGuardPatrolSeparation))
                        {
                            continue;
                        }

                        patrolRoad = candidateRoad;
                        t = candidateT;
                        groupCenter = candidateCenter;
                        route = candidateRoute;
                        break;
                    }

                    if (patrolRoad == null || route == null)
                    {
                        break;
                    }

                    Vector3 tangent = RoadTangentAt(
                        patrolRoad,
                        t);
                    Vector3 side = Vector3.Cross(
                        Vector3.up,
                        tangent).normalized;
                    var groupMembers = new List<EnemyBrain>(groupSize);
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
                        enemy.gameObject.SetActive(true);

                        EnemyDamageProfile damageProfile =
                            enemy.GetComponent<EnemyDamageProfile>();
                        if (damageProfile == null)
                        {
                            damageProfile = enemy.gameObject.AddComponent<
                                EnemyDamageProfile>();
                        }
                        damageProfile.Configure(
                            EnemyCombatVariant.RaidEnemy);
                        EnemyBrain.WeaponLoadout patrolLoadout =
                            random.NextDouble() < 0.5d
                                ? EnemyBrain.WeaponLoadout.BowOnly
                                : EnemyBrain.WeaponLoadout.SwordOnly;
                        enemy.ConfigureCampGuardLoadout(patrolLoadout);

                        float lateral = groupSize == 2
                            ? member == 0 ? -0.72f : 0.72f
                            : 0f;
                        Vector3 spawn =
                            groupCenter + side * lateral;
                        Vector3[] memberRoute =
                            OffsetPatrolRoute(route, lateral);
                        MoveActor(
                            enemy.transform,
                            RoadSurfacePoint(spawn, 1f));
                        enemy.transform.rotation =
                            Quaternion.LookRotation(
                                tangent,
                                Vector3.up);
                        enemy.ConfigurePatrolRoute(
                            memberRoute,
                            memberRoute.Length / 2);
                        groupMembers.Add(enemy);
                    }
                    if (groupMembers.Count == 2)
                    {
                        groupMembers[0].ConfigurePatrolConversationPartner(
                            groupMembers[1]);
                        groupMembers[1].ConfigurePatrolConversationPartner(
                            groupMembers[0]);
                    }

                    generatedGuardGroupCount++;
                    if (pair)
                    {
                        generatedGuardPairCount++;
                    }
                    occupiedPatrolRoutes.Add(route);
                }

                while (enemyIndex < enemies.Length)
                {
                    EnemyBrain unusedEnemy = enemies[enemyIndex++];
                    if (unusedEnemy != null)
                    {
                        unusedEnemy.gameObject.SetActive(false);
                    }
                }
            }

            PlaceCampGuards();

            RaidObelisk[] obelisks =
                FindObjectsByType<RaidObelisk>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Array.Sort(
                obelisks,
                (left, right) =>
                    left.QuadrantIndex.CompareTo(
                        right.QuadrantIndex));
            for (int index = 0;
                 index < obelisks.Length &&
                 index < obeliskPositions.Length;
                 index++)
            {
                Vector2 point = obeliskPositions[index];
                obelisks[index].transform.position =
                    SurfacePoint(
                        new Vector3(point.x, 0f, point.y),
                        0f);
                obelisks[index].transform.rotation =
                    Quaternion.Euler(
                        0f,
                        45f + index * 90f,
                        0f);
            }
        }

        private void PlaceCampGuards()
        {
            generatedCampGuardCount = 0;
            generatedCampBowGuardCount = 0;
            generatedCampSwordGuardCount = 0;
            int poolIndex = 0;
            for (int campIndex = 0;
                 campIndex < campSites.Count;
                 campIndex++)
            {
                CampSite site = campSites[campIndex];
                for (int guardIndex = 0;
                     guardIndex < site.GuardCount &&
                     campGuardPool != null &&
                     poolIndex < campGuardPool.Length;
                     guardIndex++, poolIndex++)
                {
                    EnemyBrain enemy = campGuardPool[poolIndex];
                    if (enemy == null)
                    {
                        continue;
                    }

                    bool bow = site.BowGuards[guardIndex];
                    enemy.gameObject.SetActive(true);
                    EnemyDamageProfile damageProfile =
                        enemy.GetComponent<EnemyDamageProfile>();
                    if (damageProfile == null)
                    {
                        damageProfile = enemy.gameObject.AddComponent<
                            EnemyDamageProfile>();
                    }
                    damageProfile.Configure(
                        EnemyCombatVariant.RaidEnemy);
                    enemy.ConfigureCampGuardLoadout(
                        bow
                            ? EnemyBrain.WeaponLoadout.BowOnly
                            : EnemyBrain.WeaponLoadout.SwordOnly);

                    float guardAngle = site.Rotation + 154f +
                        guardIndex * (52f / Mathf.Max(
                            1,
                            site.GuardCount - 1));
                    Vector2 direction = DirectionFromAngle(guardAngle);
                    Vector2 side = new Vector2(
                        -direction.y,
                        direction.x);
                    Vector2 spawnPoint = site.Center +
                        direction * (2.7f + guardIndex * 0.35f);
                    MoveActor(
                        enemy.transform,
                        SurfacePoint(
                            new Vector3(
                                spawnPoint.x,
                                0f,
                                spawnPoint.y),
                            1f));
                    Vector3 towardFire = new Vector3(
                        site.Center.x - spawnPoint.x,
                        0f,
                        site.Center.y - spawnPoint.y);
                    if (towardFire.sqrMagnitude > 0.001f)
                    {
                        enemy.transform.rotation =
                            Quaternion.LookRotation(
                                towardFire.normalized,
                                Vector3.up);
                    }

                    Vector2 routeA = spawnPoint;
                    Vector2 routeB = site.Center +
                        direction * 3.75f + side * 1.25f;
                    Vector2 routeC = site.Center +
                        direction * 3.15f - side * 1.05f;
                    enemy.ConfigurePatrolRoute(
                        new[]
                        {
                            SurfacePoint(
                                new Vector3(routeA.x, 0f, routeA.y),
                                1f),
                            SurfacePoint(
                                new Vector3(routeB.x, 0f, routeB.y),
                                1f),
                            SurfacePoint(
                                new Vector3(routeC.x, 0f, routeC.y),
                                1f)
                        },
                        0);
                    generatedCampGuardCount++;
                    if (bow)
                    {
                        generatedCampBowGuardCount++;
                    }
                    else
                    {
                        generatedCampSwordGuardCount++;
                    }
                }
            }

            if (campGuardPool == null)
            {
                return;
            }
            while (poolIndex < campGuardPool.Length)
            {
                EnemyBrain unused = campGuardPool[poolIndex++];
                if (unused != null)
                {
                    unused.gameObject.SetActive(false);
                }
            }
        }

        private void ResolveObeliskPositions()
        {
            uint angleHash = unchecked(
                (uint)Seed * 747796405u +
                2891336453u);
            float preferredAngle = Mathf.Lerp(
                12f,
                30f,
                (angleHash & 0xffffu) / 65535f);
            float bestScore = float.NegativeInfinity;
            float bestAngle = preferredAngle;
            float bestRadius = mapRadius * 0.52f;
            for (int angleIndex = 0;
                 angleIndex < 17;
                 angleIndex++)
            {
                float baseAngle = Mathf.Lerp(
                    7f,
                    38f,
                    angleIndex / 16f);
                for (int radiusIndex = 0;
                     radiusIndex < 9;
                     radiusIndex++)
                {
                    float radius = mapRadius * Mathf.Lerp(
                        0.46f,
                        0.60f,
                        radiusIndex / 8f);
                    float minimumRiverDistance =
                        float.PositiveInfinity;
                    float minimumEndpointDistance =
                        float.PositiveInfinity;
                    for (int index = 0; index < 4; index++)
                    {
                        float radians =
                            (baseAngle + index * 90f) *
                            Mathf.Deg2Rad;
                        Vector2 point = new Vector2(
                            Mathf.Cos(radians) * radius,
                            Mathf.Sin(radians) * radius);
                        minimumRiverDistance = Mathf.Min(
                            minimumRiverDistance,
                            DistanceToRiverExact(point));
                        minimumEndpointDistance = Mathf.Min(
                            minimumEndpointDistance,
                            Vector2.Distance(
                                point,
                                ToXZ(layout.PlayerStart)),
                            Vector2.Distance(
                                point,
                                ToXZ(layout.Extraction)));
                    }

                    float score =
                        Mathf.Min(
                            minimumRiverDistance -
                                (riverHalfWidth +
                                 ObeliskRiverClearance),
                            minimumEndpointDistance - 12f) -
                        Mathf.Abs(baseAngle - preferredAngle) *
                            0.025f;
                    if (minimumRiverDistance <
                            riverHalfWidth +
                            ObeliskRiverClearance ||
                        minimumEndpointDistance < 12f ||
                        score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestAngle = baseAngle;
                    bestRadius = radius;
                }
            }

            for (int index = 0; index < 4; index++)
            {
                float radians =
                    (bestAngle + index * 90f) *
                    Mathf.Deg2Rad;
                obeliskPositions[index] = new Vector2(
                    Mathf.Cos(radians) * bestRadius,
                    Mathf.Sin(radians) * bestRadius);
            }
        }

        private bool IsInsideObeliskClearance(
            Vector2 point,
            float clearance)
        {
            float clearanceSquared = clearance * clearance;
            for (int index = 0;
                 index < obeliskPositions.Length;
                 index++)
            {
                if ((obeliskPositions[index] - point)
                    .sqrMagnitude < clearanceSquared)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPatrolRouteSeparated(
            Vector3[] candidate,
            List<Vector3[]> occupiedRoutes,
            float minimumSeparation)
        {
            float minimumSquared =
                minimumSeparation * minimumSeparation;
            foreach (Vector3[] occupied in occupiedRoutes)
            {
                foreach (Vector3 candidatePoint in candidate)
                {
                    foreach (Vector3 occupiedPoint in occupied)
                    {
                        Vector3 delta =
                            candidatePoint - occupiedPoint;
                        delta.y = 0f;
                        if (delta.sqrMagnitude < minimumSquared)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
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

        private Vector3[] OffsetPatrolRoute(
            Vector3[] route,
            float lateralOffset)
        {
            if (route == null || route.Length == 0 ||
                Mathf.Abs(lateralOffset) < 0.001f)
            {
                return route;
            }

            var offsetRoute = new Vector3[route.Length];
            for (int index = 0; index < route.Length; index++)
            {
                int previous = Mathf.Max(0, index - 1);
                int next = Mathf.Min(route.Length - 1, index + 1);
                Vector3 tangent = Vector3.ProjectOnPlane(
                    route[next] - route[previous],
                    Vector3.up).normalized;
                Vector3 side = Vector3.Cross(
                    Vector3.up,
                    tangent).normalized;
                Vector3 point = route[index] + side * lateralOffset;
                offsetRoute[index] = SurfacePoint(point, 1f);
            }

            return offsetRoute;
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

        private float IslandGenerationExtent =>
            layout != null && layout.MaximumCoastRadius > 0f
                ? layout.MaximumCoastRadius + 2.5f
                : mapRadius;

        private float CoastRadiusAt(Vector2 point)
        {
            return layout != null
                ? layout.CoastRadiusAtAngle(
                    Mathf.Atan2(point.y, point.x))
                : mapRadius;
        }

        private float DistanceInsideCoast(Vector2 point)
        {
            return CoastRadiusAt(point) - point.magnitude;
        }

        private bool IsInsideIsland(
            Vector2 point,
            float inset)
        {
            return DistanceInsideCoast(point) >= inset;
        }

        private Vector3 CoastPoint(float angle, float inset)
        {
            float radius = Mathf.Max(
                1f,
                layout.CoastRadiusAtAngle(angle) - inset);
            return new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
        }

        private float TerrainHeight(
            float x,
            float z)
        {
            return TerrainHeight(
                x,
                z,
                out _);
        }

        private float TerrainHeight(
            float x,
            float z,
            out float signedRoadDistance)
        {
            float height =
                RawLandHeight(x, z);
            signedRoadDistance = float.PositiveInfinity;
            float riverDistance =
                DistanceToRiver(new Vector2(x, z));
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
                signedRoadDistance =
                    roadDistance - localRoadHalfWidth;
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

            height = ApplyLevelTwoCampTerrainFit(
                new Vector2(x, z),
                height);

            float coastDistance = DistanceInsideCoast(
                new Vector2(x, z));
            if (coastDistance < CoastSandWidth + 3.5f)
            {
                float coastBlend = Mathf.SmoothStep(
                    0f,
                    1f,
                    1f - Mathf.InverseLerp(
                        -1.5f,
                        CoastSandWidth + 3.5f,
                        coastDistance));
                float shoreHeight = oceanWaterLevel +
                    Mathf.Lerp(
                        -OceanDepthBelowShore,
                        0.16f,
                        Mathf.Clamp01(coastDistance / 1.5f));
                height = Mathf.Lerp(
                    height,
                    shoreHeight,
                    coastBlend);
            }
            return height;
        }

        private float ApplyLevelTwoCampTerrainFit(
            Vector2 point,
            float originalHeight)
        {
            float height = originalHeight;
            for (int index = 0; index < campSites.Count; index++)
            {
                CampSite site = campSites[index];
                if (!site.IsLevelTwo)
                {
                    continue;
                }

                Vector2 offset = point - site.Center;
                float outerRadius = site.ClearingRadius + 1.8f;
                float distance = offset.magnitude;
                if (distance >= outerRadius)
                {
                    continue;
                }

                float innerRadius = site.ClearingRadius * 0.52f;
                float blend = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        innerRadius,
                        outerRadius,
                        distance));
                float fittedPlane = site.GroundHeight +
                    Vector2.Dot(site.GroundSlope, offset);
                height = Mathf.Lerp(
                    height,
                    fittedPlane,
                    blend * 0.72f);
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
            float regionalNoise =
                Mathf.PerlinNoise(
                    noiseOffsetA.x * 0.00073f + x * 0.0044f,
                    noiseOffsetA.y * 0.00073f + z * 0.0044f);
            float regionalShape =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0.24f,
                        0.76f,
                        regionalNoise));
            float directionalPosition =
                Vector2.Dot(
                    new Vector2(x, z),
                    elevationDirection) /
                Mathf.Max(1f, mapRadius);
            float directionalShape =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        -0.68f,
                        0.68f,
                        directionalPosition));
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
                (regionalShape - 0.5f) * regionalElevationAmplitude +
                (directionalShape - 0.5f) * directionalElevationRise +
                (broad - 0.5f) * 8.0f +
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
            float inlandHeight = RawLandHeight(x, z) - 1.55f;
            float coastDistance = DistanceInsideCoast(
                new Vector2(x, z));
            float oceanBlend = 1f - Mathf.InverseLerp(
                2f,
                CoastSandWidth + 5f,
                coastDistance);
            return Mathf.Lerp(
                inlandHeight,
                oceanWaterLevel + 0.035f,
                Mathf.SmoothStep(0f, 1f, oceanBlend));
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
            return roadQuery.TryClosestPoint(
                    point,
                    out _,
                    out float distance)
                ? distance
                : float.PositiveInfinity;
        }

        private float DistanceToRoadWithin(
            Vector2 point,
            float maximumDistance)
        {
            return roadQuery.TryClosestPointWithin(
                    point,
                    maximumDistance,
                    out _,
                    out float distance)
                ? distance
                : float.PositiveInfinity;
        }

        private float DistanceToRiver(Vector2 point)
        {
            return riverQuery.TryClosestPointWithin(
                    point,
                    LocalSplineQueryDistance,
                    out _,
                    out float distance)
                ? distance
                : float.PositiveInfinity;
        }

        private float DistanceToRiverExact(Vector2 point)
        {
            return riverQuery.TryClosestPoint(
                    point,
                    out _,
                    out float distance)
                ? distance
                : float.PositiveInfinity;
        }

        private bool PathTouchesRiverOutsideBridge(
            Vector2 from,
            Vector2 destination)
        {
            float distance = Vector2.Distance(from, destination);
            int samples = Mathf.Max(2, Mathf.CeilToInt(distance / 1.1f));
            for (int sample = 0; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(
                    from,
                    destination,
                    sample / (float)samples);
                if (DistanceToRiverExact(point) >=
                        riverHalfWidth + 0.55f ||
                    IsInsideBridgeNavigationLane(point, 0.15f))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private bool IsInsideBridgeNavigationLane(
            Vector2 point,
            float padding)
        {
            for (int index = 0;
                 index < bridgeNavigationRoutes.Count;
                 index++)
            {
                if (IsInsideBridgeNavigationLane(
                        point,
                        bridgeNavigationRoutes[index],
                        padding))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsInsideBridgeNavigationLane(
            Vector2 point,
            BridgeNavigationRoute route,
            float padding)
        {
            Vector2 offset = point - route.Center;
            float along = Mathf.Abs(
                Vector2.Dot(offset, route.AcrossDirection));
            Vector2 side = new Vector2(
                -route.AcrossDirection.y,
                route.AcrossDirection.x);
            float lateral = Mathf.Abs(Vector2.Dot(offset, side));
            return along <= route.HalfLength + padding &&
                lateral <= route.HalfWidth + padding;
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
            return roadQuery.TryClosestPointWithin(
                point,
                LocalSplineQueryDistance,
                out closest,
                out distance);
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
                            roadDelta.y).normalized,
                        RiverDirection = new Vector3(
                            riverDelta.x,
                            0f,
                            riverDelta.y).normalized
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

        private Material CreateTexturedMaterial(
            Material source,
            int seed,
            Color dark,
            Color light,
            float textureScale,
            bool preserveSourceTint)
        {
            Material material = TrackRuntimeResource(
                source != null
                    ? new Material(source)
                    : new Material(
                        Shader.Find(
                            "Universal Render Pipeline/Lit")));
            material.name =
                source != null
                    ? $"{source.name} Runtime"
                    : "Procedural Raid Material";
            Texture texture;
            if (source != null && source.mainTexture != null)
            {
                texture = source.mainTexture;
            }
            else
            {
                texture = TrackRuntimeResource(
                    CreateNoiseTexture(
                        seed,
                        dark,
                        light));
            }
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

        private Material CreateTerrainBlendMaterial(
            Material ground,
            Material bareGround,
            Material road)
        {
            Shader shader =
                Shader.Find(
                    "WorldBuilder/Terrain Road Blend Lit");
            if (shader == null)
            {
                return ground;
            }

            Material material = TrackRuntimeResource(
                new Material(shader)
            {
                name = "Procedural Ground And Trail Blend"
            });
            Texture groundTexture =
                ground != null
                    ? ground.mainTexture
                    : Texture2D.whiteTexture;
            Texture roadTexture =
                road != null
                    ? road.mainTexture
                    : Texture2D.whiteTexture;
            Texture bareGroundTexture =
                bareGround != null
                    ? bareGround.mainTexture
                    : groundTexture;
            Texture loam = mossyLoamTexture != null
                ? mossyLoamTexture
                : groundTexture;
            Texture duff = canopyDuffTexture != null
                ? canopyDuffTexture
                : bareGroundTexture;
            Texture moss = mossCarpetTexture != null
                ? mossCarpetTexture
                : loam;
            Texture groundcover =
                creepingGroundcoverTexture != null
                    ? creepingGroundcoverTexture
                    : moss;
            Texture stony = stonyLichenSoilTexture != null
                ? stonyLichenSoilTexture
                : bareGroundTexture;
            material.SetTexture("_MossyLoamMap", loam);
            material.SetTexture("_CanopyDuffMap", duff);
            material.SetTexture("_MossCarpetMap", moss);
            material.SetTexture("_GroundcoverMap", groundcover);
            material.SetTexture("_StonyLichenMap", stony);
            material.SetTexture(
                "_RoadMap",
                roadTexture);
            material.SetFloat(
                "_HabitatTiling",
                habitatTextureTiling);
            material.SetFloat(
                "_HabitatBrightness",
                habitatBrightness);
            material.SetFloat(
                "_HabitatBlendContrast",
                habitatBlendContrast);
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
            terrainRuntimeMaterial = material;
            ApplyForestFloorDebugMode();
            return material;
        }

        private void ApplyForestFloorDebugMode()
        {
            if (terrainRuntimeMaterial != null &&
                terrainRuntimeMaterial.HasProperty(
                    "_HabitatDebugMode"))
            {
                terrainRuntimeMaterial.SetFloat(
                    "_HabitatDebugMode",
                    (float)forestFloorDebugMode);
            }
        }

        private Material CreateRiverMaterial(
            Material source,
            int seed)
        {
            Material material = TrackRuntimeResource(
                source != null
                    ? new Material(source)
                    : new Material(
                        Shader.Find(
                            "Universal Render Pipeline/Lit")));
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
            return TerrainBlendTintAt(
                worldX,
                worldZ,
                SignedDistanceToRoad(
                    new Vector2(worldX, worldZ)));
        }

        private Color TerrainBlendTintAt(
            float worldX,
            float worldZ,
            float signedRoadDistance)
        {
            float roadBlend = RoadSurfaceBlendAt(
                worldX,
                worldZ,
                signedRoadDistance);
            Color meadowTint =
                TerrainTintAt(
                    worldX,
                    worldZ);
            Color blendedTint =
                Color.Lerp(
                    meadowTint,
                    Color.white,
                    roadBlend * 0.92f);
            float coastDistance = DistanceInsideCoast(
                new Vector2(worldX, worldZ));
            float sandBlend = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0.4f,
                    CoastSandWidth,
                    coastDistance));
            Color sandTint = new Color(
                0.79f,
                0.67f,
                0.45f,
                1f);
            blendedTint = Color.Lerp(
                blendedTint,
                sandTint,
                sandBlend * (1f - roadBlend));
            blendedTint.a = roadBlend;
            return blendedTint;
        }

        private void BuildForestHabitatField()
        {
            habitatGridSize = Mathf.Clamp(
                habitatFieldResolution,
                65,
                257);
            habitatField = new HabitatSample[
                habitatGridSize * habitatGridSize];
            Array.Clear(
                dominantHabitatPercentages,
                0,
                dominantHabitatPercentages.Length);
            habitatFieldExtent = IslandGenerationExtent;
            float diameter = habitatFieldExtent * 2f;
            int sampleCount = 0;
            for (int z = 0; z < habitatGridSize; z++)
            {
                for (int x = 0; x < habitatGridSize; x++)
                {
                    Vector2 point = new Vector2(
                        -habitatFieldExtent +
                            diameter * x /
                            (habitatGridSize - 1f),
                        -habitatFieldExtent +
                            diameter * z /
                            (habitatGridSize - 1f));
                    HabitatSample sample =
                        EvaluateForestHabitat(point);
                    habitatField[z * habitatGridSize + x] = sample;
                    if (IsInsideIsland(point, 0f))
                    {
                        dominantHabitatPercentages[
                            sample.DominantIndex]++;
                        sampleCount++;
                    }
                }
            }
            if (sampleCount > 0)
            {
                for (int index = 0;
                     index < dominantHabitatPercentages.Length;
                     index++)
                {
                    dominantHabitatPercentages[index] =
                        dominantHabitatPercentages[index] *
                        100f / sampleCount;
                }
            }
        }

        private HabitatSample EvaluateForestHabitat(
            Vector2 point)
        {
            float macro = Mathf.PerlinNoise(
                noiseOffsetA.x * 0.017f +
                    point.x / macroPatchScale,
                noiseOffsetA.y * 0.017f +
                    point.y / macroPatchScale);
            float secondary = Mathf.PerlinNoise(
                noiseOffsetB.x * 0.021f +
                    point.x / secondaryPatchScale + 13.4f,
                noiseOffsetB.y * 0.021f +
                    point.y / secondaryPatchScale + 63.8f);
            float broadGrass = Mathf.Clamp01(
                0.16f + macro * 0.62f +
                secondary * 0.22f);
            float canopy = CanopyInfluenceAt(point);
            float boulder = BoulderInfluenceAt(point);
            Vector3 normal = TerrainNormalAt(point.x, point.y);
            float slope = Mathf.InverseLerp(
                0.035f,
                0.29f,
                1f - normal.y);
            float height = RawLandHeight(point.x, point.y);
            float surroundingHeight =
                (RawLandHeight(point.x + 5f, point.y) +
                 RawLandHeight(point.x - 5f, point.y) +
                 RawLandHeight(point.x, point.y + 5f) +
                 RawLandHeight(point.x, point.y - 5f)) * 0.25f;
            float lowGround = 1f -
                Mathf.InverseLerp(-3.6f, 3.8f, height);
            float depression = Mathf.InverseLerp(
                -0.15f,
                1.2f,
                surroundingHeight - height);
            float moisture = Mathf.Clamp01(
                lowGround * 0.52f +
                depression * 0.28f +
                canopy * 0.12f +
                boulder * 0.18f);
            float filteredLight = 1f -
                Mathf.Clamp01(
                    Mathf.Abs(canopy - 0.48f) * 2.15f);
            float grassTransition = 1f -
                Mathf.Clamp01(
                    Mathf.Abs(broadGrass - 0.52f) * 2.6f);
            float exposedRise = Mathf.InverseLerp(
                0.3f,
                4.8f,
                height);

            float loamScore =
                0.83f + broadGrass * 0.665f +
                (1f - slope) * 0.08f;
            float mossScore =
                0.44f + moisture * 1.04f +
                canopy * 0.18f + boulder * 0.34f +
                (1f - macro) * 0.30f;
            float groundcoverScore =
                0.39f + filteredLight * 0.53f +
                grassTransition * 0.46f +
                Mathf.SmoothStep(0.55f, 0.82f, macro) *
                    (1f - canopy * 0.45f) * 0.28f;
            float stonyScore =
                0.48f + slope * 1.28f +
                boulder * 0.88f + exposedRise * 0.35f +
                Mathf.SmoothStep(0.60f, 0.86f, secondary) * 0.68f;

            int first = 1;
            int second = 2;
            float firstScore = mossScore;
            float secondScore = groundcoverScore;
            if (secondScore > firstScore)
            {
                (first, second) = (second, first);
                (firstScore, secondScore) =
                    (secondScore, firstScore);
            }
            if (stonyScore > firstScore)
            {
                second = first;
                secondScore = firstScore;
                first = 3;
                firstScore = stonyScore;
            }
            else if (stonyScore > secondScore)
            {
                second = 3;
                secondScore = stonyScore;
            }

            float loamWeight = Mathf.Pow(
                loamScore,
                habitatWeightSharpness);
            float firstWeight = Mathf.Pow(
                firstScore,
                habitatWeightSharpness);
            float secondWeight = Mathf.Pow(
                secondScore * 0.62f,
                habitatWeightSharpness);
            float duffWeight = 0f;
            float mossWeight = 0f;
            float groundcoverWeight = 0f;
            float stonyWeight = 0f;
            switch (first)
            {
                case 0: duffWeight = firstWeight; break;
                case 1: mossWeight = firstWeight; break;
                case 2: groundcoverWeight = firstWeight; break;
                default: stonyWeight = firstWeight; break;
            }
            switch (second)
            {
                case 0: duffWeight = secondWeight; break;
                case 1: mossWeight = secondWeight; break;
                case 2: groundcoverWeight = secondWeight; break;
                default: stonyWeight = secondWeight; break;
            }
            float total =
                loamWeight +
                duffWeight +
                mossWeight +
                groundcoverWeight +
                stonyWeight;
            total = Mathf.Max(0.0001f, total);
            loamWeight /= total;
            duffWeight /= total;
            mossWeight /= total;
            groundcoverWeight /= total;
            stonyWeight /= total;
            float grassDensity = Mathf.Clamp01(
                loamWeight * 0.98f +
                mossWeight * 0.44f +
                groundcoverWeight * 0.32f +
                stonyWeight * 0.10f);
            grassDensity *= Mathf.Lerp(
                0.78f,
                1.08f,
                broadGrass);
            float holeNoise = Mathf.PerlinNoise(
                noiseOffsetA.x * 0.029f + point.x * 0.16f,
                noiseOffsetA.y * 0.029f + point.y * 0.16f);
            grassDensity *= Mathf.Lerp(
                0.58f,
                1f,
                Mathf.SmoothStep(0.18f, 0.72f, holeNoise));

            return new HabitatSample
            {
                PrimaryWeights = new Vector4(
                    loamWeight,
                    duffWeight,
                    mossWeight,
                    groundcoverWeight),
                StonyWeight = stonyWeight,
                GrassDensity = Mathf.Clamp01(grassDensity),
                CanopyInfluence = canopy,
                BoulderInfluence = boulder,
                MoistureTendency = moisture
            };
        }

        private float CanopyInfluenceAt(Vector2 point)
        {
            float influence = 0f;
            float accumulated = 0f;
            float radiusSquared =
                canopyInfluenceRadius * canopyInfluenceRadius;
            if (treeSpatialHash != null)
            {
                treeSpatialHash.CollectNearbyIndices(
                    point,
                    canopyInfluenceRadius,
                    treeInfluenceIndices);
            }
            else
            {
                treeInfluenceIndices.Clear();
                for (int index = 0;
                     index < generatedTreePositions.Count;
                     index++)
                {
                    treeInfluenceIndices.Add(index);
                }
            }

            for (int nearbyIndex = 0;
                 nearbyIndex < treeInfluenceIndices.Count;
                 nearbyIndex++)
            {
                int index = treeInfluenceIndices[nearbyIndex];
                float distanceSquared =
                    (generatedTreePositions[index] - point)
                    .sqrMagnitude;
                if (distanceSquared >= radiusSquared)
                {
                    continue;
                }
                float falloff = 1f -
                    Mathf.Sqrt(distanceSquared) /
                    canopyInfluenceRadius;
                falloff = falloff * falloff *
                    (3f - 2f * falloff);
                influence = Mathf.Max(influence, falloff);
                accumulated += falloff * 0.42f;
            }
            return Mathf.Clamp01(
                Mathf.Max(influence, accumulated));
        }

        private float BoulderInfluenceAt(Vector2 point)
        {
            float influence = 0f;
            for (int index = 0;
                 index < generatedBoulderPlacements.Count;
                 index++)
            {
                BoulderPlacement boulder =
                    generatedBoulderPlacements[index];
                Vector2 offset = point - boulder.Position;
                float reach = boulder.Radius +
                    boulderInfluenceRadius;
                float distance = offset.magnitude;
                if (distance >= reach)
                {
                    continue;
                }
                float radial = 1f -
                    Mathf.InverseLerp(
                        boulder.Radius * 0.48f,
                        reach,
                        distance);
                float directional = offset.sqrMagnitude > 0.001f
                    ? Mathf.InverseLerp(
                        -0.65f,
                        0.85f,
                        Vector2.Dot(
                            offset.normalized,
                            boulder.ShelterDirection))
                    : 0.5f;
                influence = Mathf.Max(
                    influence,
                    Mathf.Clamp01(radial) *
                    Mathf.Lerp(0.34f, 1f, directional));
            }
            return influence;
        }

        private HabitatSample ForestHabitatAt(Vector2 point)
        {
            if (habitatField == null ||
                habitatField.Length == 0 ||
                habitatGridSize < 2)
            {
                return ApplyCampGroundHabitat(
                    EvaluateForestHabitat(point),
                    CampGroundBlendAt(point));
            }
            float fieldExtent = Mathf.Max(
                1f,
                habitatFieldExtent);
            float x = Mathf.Clamp01(
                (point.x + fieldExtent) / (fieldExtent * 2f)) *
                (habitatGridSize - 1);
            float z = Mathf.Clamp01(
                (point.y + fieldExtent) / (fieldExtent * 2f)) *
                (habitatGridSize - 1);
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            int x1 = Mathf.Min(x0 + 1, habitatGridSize - 1);
            int z1 = Mathf.Min(z0 + 1, habitatGridSize - 1);
            float tx = x - x0;
            float tz = z - z0;
            HabitatSample a = LerpHabitat(
                habitatField[z0 * habitatGridSize + x0],
                habitatField[z0 * habitatGridSize + x1],
                tx);
            HabitatSample b = LerpHabitat(
                habitatField[z1 * habitatGridSize + x0],
                habitatField[z1 * habitatGridSize + x1],
                tx);
            return ApplyCampGroundHabitat(
                LerpHabitat(a, b, tz),
                CampGroundBlendAt(point));
        }

        private static HabitatSample ApplyCampGroundHabitat(
            HabitatSample habitat,
            float blend)
        {
            if (blend <= 0.001f)
            {
                return habitat;
            }
            float textureBlend = blend * 0.76f;
            Vector4 targetPrimary = new Vector4(
                0.76f,
                0f,
                0.10f,
                0.08f);
            float targetStony = 0.06f;
            habitat.PrimaryWeights = Vector4.Lerp(
                habitat.PrimaryWeights,
                targetPrimary,
                textureBlend);
            habitat.StonyWeight = Mathf.Lerp(
                habitat.StonyWeight,
                targetStony,
                textureBlend);
            float total = Mathf.Max(
                0.0001f,
                habitat.PrimaryWeights.x +
                habitat.PrimaryWeights.y +
                habitat.PrimaryWeights.z +
                habitat.PrimaryWeights.w +
                habitat.StonyWeight);
            habitat.PrimaryWeights /= total;
            habitat.StonyWeight /= total;
            return habitat;
        }

        private static HabitatSample LerpHabitat(
            HabitatSample a,
            HabitatSample b,
            float t)
        {
            Vector4 primary = Vector4.Lerp(
                a.PrimaryWeights,
                b.PrimaryWeights,
                t);
            float stony = Mathf.Lerp(
                a.StonyWeight,
                b.StonyWeight,
                t);
            float total = Mathf.Max(
                0.0001f,
                primary.x + primary.y +
                primary.z + primary.w + stony);
            return new HabitatSample
            {
                PrimaryWeights = primary / total,
                StonyWeight = stony / total,
                GrassDensity = Mathf.Lerp(
                    a.GrassDensity,
                    b.GrassDensity,
                    t),
                CanopyInfluence = Mathf.Lerp(
                    a.CanopyInfluence,
                    b.CanopyInfluence,
                    t),
                BoulderInfluence = Mathf.Lerp(
                    a.BoulderInfluence,
                    b.BoulderInfluence,
                    t),
                MoistureTendency = Mathf.Lerp(
                    a.MoistureTendency,
                    b.MoistureTendency,
                    t)
            };
        }

        private string FormatHabitatPercentages()
        {
            return
                $"loam {dominantHabitatPercentages[0]:0.0}%, " +
                $"duff {dominantHabitatPercentages[1]:0.0}%, " +
                $"moss {dominantHabitatPercentages[2]:0.0}%, " +
                $"groundcover {dominantHabitatPercentages[3]:0.0}%, " +
                $"stony {dominantHabitatPercentages[4]:0.0}%";
        }

        private float BareGroundBlendAt(
            float worldX,
            float worldZ)
        {
            // Broad colonies establish readable clearings while the second
            // octave breaks their edges up naturally. This exact field is
            // also used as the probability of placing grass, so the floor
            // texture and the visible blades cannot describe different
            // vegetation patterns.
            float broadPatch = Mathf.PerlinNoise(
                noiseOffsetA.x * 0.013f +
                    worldX * 0.043f +
                    31.7f,
                noiseOffsetA.y * 0.013f +
                    worldZ * 0.043f +
                    17.3f);
            float edgeNoise = Mathf.PerlinNoise(
                noiseOffsetB.x * 0.019f +
                    worldX * 0.118f +
                    73.1f,
                noiseOffsetB.y * 0.019f +
                    worldZ * 0.118f +
                    9.6f);
            float patchNoise =
                broadPatch * 0.78f +
                edgeNoise * 0.22f;
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0.56f,
                    0.70f,
                    patchNoise));
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
            return RoadSurfaceBlendAt(
                worldX,
                worldZ,
                signedDistance);
        }

        private float RoadSurfaceBlendAt(
            float worldX,
            float worldZ,
            float signedDistance)
        {
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
