using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public sealed partial class ProceduralRaidGenerator
    {
        public enum GenerationQuality
        {
            Production,
            FastPreview,
            BrowserDemo
        }

        [Header("Development Workflow")]
        [SerializeField]
        [Tooltip(
            "Production preserves the authored ecology budgets. Fast Preview " +
            "is intended only for the inert editor map reviewer.")]
        private GenerationQuality generationQuality =
            GenerationQuality.Production;

        private const int FastPreviewTerrainResolution = 193;
        private const int FastPreviewHabitatResolution = 97;
        private const int FastPreviewTreeCount = 900;
        private const int FastPreviewGrassCount = 60000;
        private const int FastPreviewUndergrowthCount = 2800;
        private const int FastPreviewGroundFloraCount = 3200;
        private const int FastPreviewBoulderCount = 180;
        private const int FastPreviewTrailStoneCount = 120;
        private const int BrowserDemoTerrainResolution = 161;
        private const int BrowserDemoHabitatResolution = 81;
        private const int BrowserDemoTreeCount = 600;
        private const int BrowserDemoGrassCount = 24000;
        private const int BrowserDemoUndergrowthCount = 1200;
        private const int BrowserDemoGroundFloraCount = 1400;
        private const int BrowserDemoBoulderCount = 120;
        private const int BrowserDemoTrailStoneCount = 80;

        private float[] terrainSurfaceHeights = Array.Empty<float>();
        private float[] terrainSurfaceRawHeights = Array.Empty<float>();
        private float[] terrainSurfaceSignedRoadDistances =
            Array.Empty<float>();
        private int terrainSurfaceResolution;
        private float terrainSurfaceExtent;
        private PointSpatialHash boulderSpatialHash;
        private readonly List<int> boulderInfluenceIndices =
            new List<int>(32);
        private float maximumBoulderInfluenceReach;

        public GenerationQuality CurrentGenerationQuality =>
            generationQuality;
        public int EffectiveTerrainResolution => ActiveTerrainResolution;
        public int EffectiveHabitatResolution => ActiveHabitatResolution;
        public int EffectiveTreeCount => ActiveTreeCount;
        public int EffectiveGrassCount => ActiveGrassCount;
        public int EffectiveUndergrowthCount => ActiveUndergrowthCount;
        public int EffectiveGroundFloraStudyCount =>
            ActiveGroundFloraStudyCount;

        private bool IsFastPreview =>
            generationQuality == GenerationQuality.FastPreview;
        private bool IsBrowserDemo =>
            generationQuality == GenerationQuality.BrowserDemo;
        private int ActiveTerrainResolution => IsBrowserDemo
            ? Mathf.Min(terrainResolution, BrowserDemoTerrainResolution)
            : IsFastPreview
                ? Mathf.Min(terrainResolution, FastPreviewTerrainResolution)
                : terrainResolution;
        private int ActiveHabitatResolution => IsBrowserDemo
            ? Mathf.Min(
                habitatFieldResolution,
                BrowserDemoHabitatResolution)
            : IsFastPreview
                ? Mathf.Min(
                    habitatFieldResolution,
                    FastPreviewHabitatResolution)
                : habitatFieldResolution;
        private int ActiveTreeCount => IsBrowserDemo
            ? Mathf.Min(treeCount, BrowserDemoTreeCount)
            : IsFastPreview
                ? Mathf.Min(treeCount, FastPreviewTreeCount)
                : treeCount;
        private int ActiveGrassCount => IsBrowserDemo
            ? Mathf.Min(grassCount, BrowserDemoGrassCount)
            : IsFastPreview
                ? Mathf.Min(grassCount, FastPreviewGrassCount)
                : grassCount;
        private int ActiveUndergrowthCount => IsBrowserDemo
            ? Mathf.Min(
                undergrowthCount,
                BrowserDemoUndergrowthCount)
            : IsFastPreview
                ? Mathf.Min(undergrowthCount, FastPreviewUndergrowthCount)
                : undergrowthCount;
        private int ActiveGroundFloraStudyCount => IsBrowserDemo
            ? Mathf.Min(
                groundFloraStudyCount,
                BrowserDemoGroundFloraCount)
            : IsFastPreview
                ? Mathf.Min(
                    groundFloraStudyCount,
                    FastPreviewGroundFloraCount)
                : groundFloraStudyCount;
        private int ActiveBoulderCount => IsBrowserDemo
            ? Mathf.Min(boulderCount, BrowserDemoBoulderCount)
            : IsFastPreview
                ? Mathf.Min(boulderCount, FastPreviewBoulderCount)
                : boulderCount;
        private int ActiveTrailStoneCount => IsBrowserDemo
            ? Mathf.Min(trailStoneCount, BrowserDemoTrailStoneCount)
            : IsFastPreview
                ? Mathf.Min(trailStoneCount, FastPreviewTrailStoneCount)
                : trailStoneCount;

        // The review scene has no live actors or physics. Runtime generation
        // always keeps authored collision, even in a development build.
        private bool ShouldCreateEnvironmentColliders =>
            Application.isPlaying || !IsFastPreview;

        public void SetGenerationQuality(GenerationQuality quality)
        {
            generationQuality = quality;
        }

        public bool EnsureGeneratedWithSeed(int seed)
        {
            if (generatedRoot != null &&
                layout != null &&
                layout.Seed == seed)
            {
                return false;
            }

            GenerateWithSeed(seed);
            return true;
        }

        private void ClearPerformanceCaches()
        {
            terrainSurfaceHeights = Array.Empty<float>();
            terrainSurfaceRawHeights = Array.Empty<float>();
            terrainSurfaceSignedRoadDistances = Array.Empty<float>();
            terrainSurfaceResolution = 0;
            terrainSurfaceExtent = 0f;
            boulderSpatialHash = null;
            boulderInfluenceIndices.Clear();
            maximumBoulderInfluenceReach = 0f;
        }

        private void BuildTerrainSurfaceCache()
        {
            terrainSurfaceResolution =
                Mathf.Max(1, ActiveTerrainResolution);
            terrainSurfaceExtent = IslandGenerationExtent;
            int width = terrainSurfaceResolution + 1;
            int sampleCount = width * width;
            if (terrainSurfaceHeights.Length != sampleCount)
            {
                terrainSurfaceHeights = new float[sampleCount];
                terrainSurfaceRawHeights = new float[sampleCount];
                terrainSurfaceSignedRoadDistances =
                    new float[sampleCount];
            }

            float diameter = terrainSurfaceExtent * 2f;
            for (int z = 0; z < width; z++)
            {
                float worldZ = -terrainSurfaceExtent +
                    diameter * z / terrainSurfaceResolution;
                for (int x = 0; x < width; x++)
                {
                    float worldX = -terrainSurfaceExtent +
                        diameter * x / terrainSurfaceResolution;
                    int index = z * width + x;
                    float rawHeight = RawLandHeight(worldX, worldZ);
                    terrainSurfaceRawHeights[index] = rawHeight;
                    terrainSurfaceHeights[index] =
                        EvaluateTerrainHeightFromRaw(
                            worldX,
                            worldZ,
                            rawHeight,
                            out float signedRoadDistance);
                    terrainSurfaceSignedRoadDistances[index] =
                        signedRoadDistance;
                }
            }
        }

        private bool TryGetTerrainSurfaceVertex(
            int index,
            out float height,
            out float signedRoadDistance)
        {
            if (index < 0 ||
                index >= terrainSurfaceHeights.Length ||
                terrainSurfaceSignedRoadDistances.Length !=
                    terrainSurfaceHeights.Length)
            {
                height = 0f;
                signedRoadDistance = float.PositiveInfinity;
                return false;
            }

            height = terrainSurfaceHeights[index];
            signedRoadDistance =
                terrainSurfaceSignedRoadDistances[index];
            return true;
        }

        private bool TrySampleTerrainSurfaceCache(
            float x,
            float z,
            out float height,
            out Vector3 normal)
        {
            int resolution = terrainSurfaceResolution;
            int width = resolution + 1;
            if (resolution < 1 ||
                terrainSurfaceHeights.Length != width * width ||
                x < -terrainSurfaceExtent ||
                x > terrainSurfaceExtent ||
                z < -terrainSurfaceExtent ||
                z > terrainSurfaceExtent)
            {
                height = 0f;
                normal = Vector3.up;
                return false;
            }

            float diameter = Mathf.Max(
                0.001f,
                terrainSurfaceExtent * 2f);
            float gridX = Mathf.Clamp(
                (x + terrainSurfaceExtent) /
                    diameter * resolution,
                0f,
                resolution);
            float gridZ = Mathf.Clamp(
                (z + terrainSurfaceExtent) /
                    diameter * resolution,
                0f,
                resolution);
            int x0 = Mathf.Min(
                Mathf.FloorToInt(gridX),
                resolution - 1);
            int z0 = Mathf.Min(
                Mathf.FloorToInt(gridZ),
                resolution - 1);
            float localX = Mathf.Clamp01(gridX - x0);
            float localZ = Mathf.Clamp01(gridZ - z0);
            float step = diameter / resolution;
            int index00 = z0 * width + x0;
            float height00 = terrainSurfaceHeights[index00];
            float height11 = terrainSurfaceHeights[
                (z0 + 1) * width + x0 + 1];
            if (localZ >= localX)
            {
                float height01 = terrainSurfaceHeights[
                    (z0 + 1) * width + x0];
                height =
                    height00 * (1f - localZ) +
                    height01 * (localZ - localX) +
                    height11 * localX;
                normal = Vector3.Cross(
                    new Vector3(0f, height01 - height00, step),
                    new Vector3(step, height11 - height00, step))
                    .normalized;
                return true;
            }

            float height10 = terrainSurfaceHeights[index00 + 1];
            height =
                height00 * (1f - localX) +
                height11 * localZ +
                height10 * (localX - localZ);
            normal = Vector3.Cross(
                new Vector3(step, height11 - height00, step),
                new Vector3(step, height10 - height00, 0f))
                .normalized;
            return true;
        }

        private float SampleCachedRawLandHeight(float x, float z)
        {
            int resolution = terrainSurfaceResolution;
            int width = resolution + 1;
            if (resolution < 1 ||
                terrainSurfaceRawHeights.Length != width * width ||
                x < -terrainSurfaceExtent ||
                x > terrainSurfaceExtent ||
                z < -terrainSurfaceExtent ||
                z > terrainSurfaceExtent)
            {
                return RawLandHeight(x, z);
            }

            float diameter = Mathf.Max(
                0.001f,
                terrainSurfaceExtent * 2f);
            float gridX = Mathf.Clamp(
                (x + terrainSurfaceExtent) /
                    diameter * resolution,
                0f,
                resolution);
            float gridZ = Mathf.Clamp(
                (z + terrainSurfaceExtent) /
                    diameter * resolution,
                0f,
                resolution);
            int x0 = Mathf.Min(
                Mathf.FloorToInt(gridX),
                resolution - 1);
            int z0 = Mathf.Min(
                Mathf.FloorToInt(gridZ),
                resolution - 1);
            float localX = Mathf.Clamp01(gridX - x0);
            float localZ = Mathf.Clamp01(gridZ - z0);
            int index00 = z0 * width + x0;
            float height00 = terrainSurfaceRawHeights[index00];
            float height11 = terrainSurfaceRawHeights[
                (z0 + 1) * width + x0 + 1];
            if (localZ >= localX)
            {
                float height01 = terrainSurfaceRawHeights[
                    (z0 + 1) * width + x0];
                return
                    height00 * (1f - localZ) +
                    height01 * (localZ - localX) +
                    height11 * localX;
            }

            float height10 = terrainSurfaceRawHeights[index00 + 1];
            return
                height00 * (1f - localX) +
                height11 * localZ +
                height10 * (localX - localZ);
        }

        private void BuildBoulderSpatialIndex()
        {
            boulderSpatialHash = new PointSpatialHash(
                Mathf.Max(2f, boulderInfluenceRadius));
            maximumBoulderInfluenceReach = 0f;
            for (int index = 0;
                 index < generatedBoulderPlacements.Count;
                 index++)
            {
                BoulderPlacement boulder =
                    generatedBoulderPlacements[index];
                boulderSpatialHash.Add(boulder.Position);
                maximumBoulderInfluenceReach = Mathf.Max(
                    maximumBoulderInfluenceReach,
                    boulder.Radius + boulderInfluenceRadius);
            }
        }

        private void CollectNearbyBoulderIndices(Vector2 point)
        {
            if (boulderSpatialHash == null ||
                maximumBoulderInfluenceReach <= 0f)
            {
                boulderInfluenceIndices.Clear();
                for (int index = 0;
                     index < generatedBoulderPlacements.Count;
                     index++)
                {
                    boulderInfluenceIndices.Add(index);
                }
                return;
            }

            boulderSpatialHash.CollectNearbyIndices(
                point,
                maximumBoulderInfluenceReach,
                boulderInfluenceIndices);
        }
    }
}
