using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    public sealed partial class ProceduralRaidGenerator
    {
        public enum LandformTier
        {
            Lowland,
            MidShelf,
            Highland,
            Crown
        }

        public enum LandformRegionType
        {
            Plain,
            Valley,
            Shelf,
            Highland,
            Ridge,
            Gorge
        }

        public enum LandformTraversalType
        {
            BroadPass,
            Switchback,
            ShelfTrail,
            RidgeTrail
        }

        [Serializable]
        public sealed class LandformRegion
        {
            public int Id;
            public string Name;
            public LandformTier Tier;
            public LandformRegionType Type;
            public Vector2 Center;
            public Vector2 Radii;
            public float RotationDegrees;
            public float TargetHeight;
            public float EdgeBlendWidth;
            public float ScenicImportance;
            public int ParentRegionId;
            public float BoundaryPhase;
            public float BoundaryWobble;
            public float NotchAngle;
            public float NotchStrength;
        }

        [Serializable]
        public sealed class LandformConnection
        {
            public int Id;
            public string Name;
            public int SourceRegionId;
            public int DestinationRegionId;
            public LandformTraversalType TraversalType;
            public Vector2[] Waypoints;
            public float Width;
            public float MaxGrade;
            public float RiseStartProgress;
            public float RiseEndProgress;
        }

        [Serializable]
        public sealed class ScenicAnchor
        {
            public int Id;
            public string Name;
            public int RegionId;
            public Vector2 Position;
            public Vector2 LookDirection;
            public float ClearanceRadius;
        }

        private const float AdvancedCliffGapPadding = 3.5f;
        private const float AdvancedScenicDefaultClearance = 12f;
        private const int AdvancedCliffSamples = 48;
        private const float AdvancedRiverValleyInnerPadding = 8.5f;
        private const float AdvancedRiverValleyOuterPadding = 32f;
        private const int AdvancedRouteSmoothingPasses = 1;
        private const float AdvancedRouteTerrainHalfWidth = 7.5f;
        public const float TargetDenseForestCoverage = 0.625f;
        public const float TargetOpenPlainCoverage = 0.125f;
        public const float TargetFirstRiseCoverage = 0.425f;
        private const int AdvancedTreeCoverageResolution = 65;
        private const int AdvancedTierCoverageResolution = 81;

        private readonly List<LandformRegion> advancedLandformRegions =
            new List<LandformRegion>(9);
        private readonly List<LandformConnection> advancedLandformConnections =
            new List<LandformConnection>(8);
        private readonly List<ScenicAnchor> advancedScenicAnchors =
            new List<ScenicAnchor>(4);
        private readonly List<List<Vector3>> advancedLandformRoads =
            new List<List<Vector3>>(8);
        private readonly PolylineQuery advancedLandformRouteQuery =
            new PolylineQuery();
        private float advancedTreeOpenThreshold = 0.30f;
        private float advancedTreeDenseThreshold = 0.52f;
        private float advancedFirstRiseCoverage;

        public IReadOnlyList<LandformRegion> AdvancedLandformRegions =>
            advancedLandformRegions;
        public IReadOnlyList<LandformConnection> AdvancedLandformConnections =>
            advancedLandformConnections;
        public IReadOnlyList<ScenicAnchor> AdvancedScenicAnchors =>
            advancedScenicAnchors;
        public IReadOnlyList<List<Vector3>> AdvancedVisibleTrails =>
            advancedLandformRoads;
        public float AdvancedFirstRiseCoverage => advancedFirstRiseCoverage;

        private void ConfigureAdvancedLandformGraph(int seed)
        {
            advancedLandformRegions.Clear();
            advancedLandformConnections.Clear();
            advancedScenicAnchors.Clear();
            advancedLandformRoads.Clear();
            advancedLandformRouteQuery.Clear();
            advancedFirstRiseCoverage = 0f;
            if (!enableAdvancedLandforms)
            {
                return;
            }

            Vector2 up = elevationDirection.sqrMagnitude > 0.001f
                ? elevationDirection.normalized
                : Vector2.up;
            Vector2 right = new Vector2(up.y, -up.x);
            var random = new System.Random(
                unchecked(seed ^ (int)0x4d7f21b9));
            float radius = Mathf.Max(60f, mapRadius);

            Vector2 rootCenter = layout != null
                ? Vector2.Lerp(Vector2.zero, ToXZ(layout.PlayerStart), 0.34f)
                : -up * radius * 0.20f;
            AddAdvancedRegion(
                "Lowland Trailhead",
                LandformTier.Lowland,
                LandformRegionType.Plain,
                rootCenter,
                new Vector2(radius * 0.20f, radius * 0.16f),
                DirectionAngle(up),
                0f,
                18f,
                0.25f,
                -1,
                random,
                0f);

            int composition = Mathf.Abs(seed % 3);
            var firstRiseIds = new List<int>(2);
            if (composition == 1)
            {
                firstRiseIds.Add(AddAdvancedRegion(
                    "Crescent First Rise",
                    LandformTier.MidShelf,
                    LandformRegionType.Shelf,
                    up * radius * 0.06f - right * radius * 0.03f,
                    new Vector2(radius * 0.61f, radius * 0.43f),
                    DirectionAngle(right) + 9f,
                    7.8f,
                    11.5f,
                    0.72f,
                    0,
                    random,
                    0.34f));
            }
            else
            {
                float spread = composition == 0 ? 0.30f : 0.25f;
                firstRiseIds.Add(AddAdvancedRegion(
                    "Western First Rise",
                    LandformTier.MidShelf,
                    LandformRegionType.Shelf,
                    -right * radius * spread + up * radius * 0.05f,
                    new Vector2(radius * 0.42f, radius * 0.32f),
                    DirectionAngle(up) + 16f,
                    7.6f,
                    11.5f,
                    0.70f,
                    0,
                    random,
                    0.10f));
                firstRiseIds.Add(AddAdvancedRegion(
                    "Eastern First Rise",
                    LandformTier.MidShelf,
                    LandformRegionType.Shelf,
                    right * radius * spread - up * radius * 0.10f,
                    new Vector2(radius * 0.38f, radius * 0.30f),
                    DirectionAngle(up) - 14f,
                    8.4f,
                    11f,
                    0.74f,
                    0,
                    random,
                    0.08f));
            }

            CalibrateAdvancedFirstRiseCoverage(firstRiseIds);

            var upperShelfIds = new List<int>(3);
            for (int parentIndex = 0;
                 parentIndex < firstRiseIds.Count;
                 parentIndex++)
            {
                LandformRegion parent =
                    advancedLandformRegions[firstRiseIds[parentIndex]];
                int childCount = firstRiseIds.Count == 1
                    ? 3
                    : parentIndex == 0 ? 2 : 1;
                for (int childIndex = 0;
                     childIndex < childCount;
                     childIndex++)
                {
                    float centered = childIndex - (childCount - 1) * 0.5f;
                    Vector2 localOffset = Rotate(
                        new Vector2(
                            centered * parent.Radii.x * 0.48f,
                            (childIndex % 2 == 0 ? 1f : -1f) *
                                parent.Radii.y * 0.16f),
                        parent.RotationDegrees * Mathf.Deg2Rad);
                    Vector2 upperShelfMaximumRadii =
                        firstRiseIds.Count == 1
                            ? new Vector2(radius * 0.293f, radius * 0.206f)
                            : parentIndex == 0
                                ? new Vector2(
                                    radius * 0.202f,
                                    radius * 0.154f)
                                : new Vector2(
                                    radius * 0.182f,
                                    radius * 0.144f);
                    upperShelfIds.Add(AddAdvancedRegion(
                        $"Upper Shelf {upperShelfIds.Count + 1}",
                        LandformTier.Highland,
                        LandformRegionType.Highland,
                        parent.Center + localOffset,
                        new Vector2(
                            Mathf.Min(
                                parent.Radii.x * 0.48f,
                                upperShelfMaximumRadii.x),
                            Mathf.Min(
                                parent.Radii.y * 0.48f,
                                upperShelfMaximumRadii.y)),
                        parent.RotationDegrees + centered * 17f,
                        16f + upperShelfIds.Count * 0.65f,
                        9.5f,
                        0.86f,
                        parent.Id,
                        random,
                        0.05f));
                }
            }

            int crownCount = Mathf.Min(2, upperShelfIds.Count);
            for (int crownIndex = 0;
                 crownIndex < crownCount;
                 crownIndex++)
            {
                int shelfListIndex = crownIndex == 0
                    ? 0
                    : upperShelfIds.Count - 1;
                LandformRegion parent =
                    advancedLandformRegions[upperShelfIds[shelfListIndex]];
                Vector2 offset = Rotate(
                    new Vector2(
                        parent.Radii.x * (crownIndex == 0 ? -0.08f : 0.10f),
                        parent.Radii.y * 0.07f),
                    parent.RotationDegrees * Mathf.Deg2Rad);
                AddAdvancedRegion(
                    $"Crown {crownIndex + 1}",
                    LandformTier.Crown,
                    LandformRegionType.Highland,
                    parent.Center + offset,
                    new Vector2(
                        parent.Radii.x * 0.48f,
                        parent.Radii.y * 0.46f),
                    parent.RotationDegrees + 11f,
                    24f + crownIndex * 1.8f,
                    7.5f,
                    1f,
                    parent.Id,
                    random,
                    0.03f);
            }

            // The legacy river is authored before the advanced landform masks
            // exist. Re-route it now that the actual first-rise footprint is
            // known, so the water occupies the island floor and bends around
            // raised territories instead of cutting a straight valley through
            // them.
            RerouteAdvancedRiverThroughLowlands(seed);

            advancedLandformRegions[0].Center =
                ProjectAdvancedWaypointToDryBank(
                    advancedLandformRegions[0].Center,
                    11);

            for (int index = 1;
                 index < advancedLandformRegions.Count;
                 index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                Vector2 axis = region.Center -
                    advancedLandformRegions[region.ParentRegionId].Center;
                Vector2 lateral = new Vector2(-axis.y, axis.x).normalized;
                if (((seed + index * 17) & 1) == 0)
                {
                    lateral = -lateral;
                }
                LandformTraversalType traversal = region.Tier ==
                        LandformTier.MidShelf
                    ? LandformTraversalType.BroadPass
                    : region.Tier == LandformTier.Crown
                        ? LandformTraversalType.Switchback
                        : LandformTraversalType.ShelfTrail;
                AddAdvancedConnection(
                    advancedLandformConnections.Count,
                    $"{advancedLandformRegions[region.ParentRegionId].Name} to {region.Name}",
                    region.ParentRegionId,
                    region.Id,
                    traversal,
                    lateral,
                    region.Tier == LandformTier.MidShelf ? 10f : 8.5f);
            }

            BuildAdvancedScenicAnchors(up, right);
            BuildAdvancedRoadLists();
        }

        private int AddAdvancedRegion(
            string name,
            LandformTier tier,
            LandformRegionType type,
            Vector2 center,
            Vector2 radii,
            float rotationDegrees,
            float targetHeight,
            float edgeBlendWidth,
            float scenicImportance,
            int parentRegionId,
            System.Random random,
            float notchStrength)
        {
            int id = advancedLandformRegions.Count;
            advancedLandformRegions.Add(new LandformRegion
            {
                Id = id,
                Name = name,
                Tier = tier,
                Type = type,
                Center = center,
                Radii = radii,
                RotationDegrees = rotationDegrees,
                TargetHeight = targetHeight,
                EdgeBlendWidth = edgeBlendWidth,
                ScenicImportance = scenicImportance,
                ParentRegionId = parentRegionId,
                BoundaryPhase = (float)random.NextDouble() * Mathf.PI * 2f,
                BoundaryWobble = Mathf.Lerp(
                    0.08f,
                    0.15f,
                    (float)random.NextDouble()),
                NotchAngle = (float)random.NextDouble() * Mathf.PI * 2f,
                NotchStrength = notchStrength
            });
            return id;
        }

        private void CalibrateAdvancedFirstRiseCoverage(
            IReadOnlyList<int> firstRiseIds)
        {
            if (firstRiseIds == null || firstRiseIds.Count == 0)
            {
                advancedFirstRiseCoverage = 0f;
                return;
            }

            // Region silhouettes vary by seed and are clipped by the island
            // coast, so ellipse area alone is not a reliable coverage budget.
            // Scale only the first rise against sampled playable land. Upper
            // shelves are created afterwards with their own footprint caps.
            for (int iteration = 0; iteration < 6; iteration++)
            {
                advancedFirstRiseCoverage =
                    MeasureAdvancedFirstRiseCoverage(firstRiseIds);
                if (Mathf.Abs(
                        advancedFirstRiseCoverage -
                        TargetFirstRiseCoverage) <= 0.004f)
                {
                    break;
                }

                float scale = Mathf.Clamp(
                    Mathf.Sqrt(
                        TargetFirstRiseCoverage /
                        Mathf.Max(0.01f, advancedFirstRiseCoverage)),
                    0.88f,
                    1.30f);
                for (int index = 0; index < firstRiseIds.Count; index++)
                {
                    LandformRegion region =
                        advancedLandformRegions[firstRiseIds[index]];
                    region.Radii *= scale;
                }
            }
            advancedFirstRiseCoverage =
                MeasureAdvancedFirstRiseCoverage(firstRiseIds);
        }

        private float MeasureAdvancedFirstRiseCoverage(
            IReadOnlyList<int> firstRiseIds)
        {
            float extent = IslandGenerationExtent;
            float diameter = extent * 2f;
            int islandSamples = 0;
            int raisedSamples = 0;
            for (int z = 0; z < AdvancedTierCoverageResolution; z++)
            {
                for (int x = 0; x < AdvancedTierCoverageResolution; x++)
                {
                    Vector2 point = new Vector2(
                        -extent + diameter * x /
                            (AdvancedTierCoverageResolution - 1f),
                        -extent + diameter * z /
                            (AdvancedTierCoverageResolution - 1f));
                    if (!IsInsideIsland(point, 0f))
                    {
                        continue;
                    }

                    islandSamples++;
                    float strongestInfluence = 0f;
                    for (int index = 0;
                         index < firstRiseIds.Count;
                         index++)
                    {
                        strongestInfluence = Mathf.Max(
                            strongestInfluence,
                            RawAdvancedRegionInfluence(
                                advancedLandformRegions[firstRiseIds[index]],
                                point));
                    }
                    if (strongestInfluence >= 0.5f)
                    {
                        raisedSamples++;
                    }
                }
            }
            return islandSamples > 0
                ? raisedSamples / (float)islandSamples
                : 0f;
        }

        private void AddAdvancedConnection(
            int id,
            string name,
            int sourceId,
            int destinationId,
            LandformTraversalType traversalType,
            Vector2 bendDirection,
            float width,
            bool requiresBridge = false)
        {
            LandformRegion source = advancedLandformRegions[sourceId];
            LandformRegion destination = advancedLandformRegions[destinationId];
            Vector2 entryDirection = destination.Center - source.Center;
            if (entryDirection.sqrMagnitude <= 0.001f)
            {
                float fallbackAngle =
                    (destination.Id * 137.5f + Seed * 0.031f) *
                    Mathf.Deg2Rad;
                entryDirection = new Vector2(
                    Mathf.Cos(fallbackAngle),
                    Mathf.Sin(fallbackAngle));
            }
            entryDirection.Normalize();
            float childRadius = Mathf.Min(
                destination.Radii.x,
                destination.Radii.y);
            Vector2 routeStart = source.Center;
            Vector2 routeEnd = destination.Center -
                entryDirection * childRadius * 0.34f;
            if (sourceId > 0)
            {
                for (int index = 0;
                     index < advancedLandformConnections.Count;
                     index++)
                {
                    LandformConnection incoming =
                        advancedLandformConnections[index];
                    if (incoming.DestinationRegionId == sourceId)
                    {
                        routeStart = incoming.Waypoints[
                            incoming.Waypoints.Length - 1];
                        break;
                    }
                }
            }
            int pointCount = traversalType == LandformTraversalType.Switchback
                ? 5
                : traversalType == LandformTraversalType.BroadPass
                    ? 5
                    : 4;
            var points = new Vector2[pointCount];
            Vector2 axis = routeEnd - routeStart;
            Vector2 lateral = bendDirection.sqrMagnitude > 0.001f
                ? bendDirection.normalized
                : new Vector2(-axis.y, axis.x).normalized;
            float amplitude = traversalType == LandformTraversalType.Switchback
                ? Mathf.Min(
                    Mathf.Min(source.Radii.x, source.Radii.y) * 0.45f,
                    Mathf.Max(
                        10f,
                        Mathf.Abs(
                            destination.TargetHeight -
                            source.TargetHeight) * 1.80f))
                : traversalType == LandformTraversalType.BroadPass
                    ? Mathf.Min(8f, axis.magnitude * 0.08f)
                    : Mathf.Min(6f, axis.magnitude * 0.06f);
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                float progress = pointIndex / (float)(pointCount - 1);
                float edgeInset = Mathf.Sin(progress * Mathf.PI);
                float lateralSign = traversalType ==
                        LandformTraversalType.Switchback
                    ? (pointIndex % 2 == 0 ? -1f : 1f)
                    : Mathf.Sin(progress * Mathf.PI * 2f);
                points[pointIndex] = Vector2.Lerp(
                        routeStart,
                        routeEnd,
                        progress) +
                    lateral * amplitude * lateralSign * edgeInset;
                points[pointIndex] = ProjectAdvancedWaypointToDryBank(
                    points[pointIndex],
                    sourceId * 17 + destinationId * 31);
            }

            int routeSalt = sourceId * 17 + destinationId * 31;
            Vector2[] rawPoints = (Vector2[])points.Clone();
            if (requiresBridge)
            {
                ShapeRawAdvancedRouteAtBridge(
                    rawPoints,
                    routeStart,
                    routeEnd);
            }
            points = FinalizeAdvancedRoute(rawPoints, routeSalt);
            if (AdvancedRouteCrossesUnrelatedConnection(
                    points,
                    sourceId,
                    destinationId))
            {
                Vector2[] mirroredRaw = MirrorAdvancedRoute(
                    rawPoints,
                    routeStart,
                    routeEnd);
                Vector2[] mirrored = FinalizeAdvancedRoute(
                    mirroredRaw,
                    routeSalt + 1);
                if (!AdvancedRouteCrossesUnrelatedConnection(
                        mirrored,
                        sourceId,
                        destinationId))
                {
                    points = mirrored;
                }
            }
            ResolveAdvancedRiseWindow(
                source,
                destination,
                points,
                out float riseStart,
                out float riseEnd);
            float grade = CalculateAdvancedConnectionGrade(
                source,
                destination,
                points,
                riseStart,
                riseEnd);
            advancedLandformConnections.Add(new LandformConnection
            {
                Id = id,
                Name = name,
                SourceRegionId = sourceId,
                DestinationRegionId = destinationId,
                TraversalType = traversalType,
                Waypoints = points,
                Width = width,
                MaxGrade = grade,
                RiseStartProgress = riseStart,
                RiseEndProgress = riseEnd
            });
        }

        private void ResolveAdvancedRiseWindow(
            LandformRegion source,
            LandformRegion destination,
            Vector2[] points,
            out float riseStart,
            out float riseEnd)
        {
            riseStart = 0.38f;
            riseEnd = 0.68f;
            const int Samples = 64;
            bool foundStart = false;
            for (int sample = 0; sample <= Samples; sample++)
            {
                float progress = sample / (float)Samples;
                Vector2 point = PointAlongPolyline(points, progress);
                float influence = AdvancedRegionInfluence(
                    destination,
                    point);
                if (!foundStart && influence >= 0.10f)
                {
                    riseStart = Mathf.Max(0.08f, progress - 0.08f);
                    foundStart = true;
                }
                if (foundStart && influence >= 0.88f)
                {
                    riseEnd = Mathf.Min(0.94f, progress + 0.06f);
                    break;
                }
            }
            float sourceWorldHeight = BaseLandHeight(
                    points[0].x,
                    points[0].y) +
                source.TargetHeight;
            float destinationWorldHeight = BaseLandHeight(
                    points[points.Length - 1].x,
                    points[points.Length - 1].y) +
                destination.TargetHeight;
            // SmoothStep reaches 1.5 times its average slope. Size the
            // transition from the real endpoint elevations (not just the tier
            // offsets), with enough margin to keep every authored ascent below
            // the walkable grade limit even when the base terrain is uneven.
            float minimumSpan = Mathf.Clamp(
                Mathf.Abs(destinationWorldHeight - sourceWorldHeight) *
                    1.95f / Mathf.Max(1f, PolylineLength(points)),
                0.30f,
                0.76f);
            if (riseEnd - riseStart < minimumSpan)
            {
                float middle = (riseStart + riseEnd) * 0.5f;
                riseStart = Mathf.Max(
                    0.05f,
                    middle - minimumSpan * 0.5f);
                riseEnd = Mathf.Min(
                    0.95f,
                    riseStart + minimumSpan);
                riseStart = Mathf.Max(
                    0.05f,
                    riseEnd - minimumSpan);
            }
            if (source.Tier == LandformTier.Lowland &&
                destination.Tier == LandformTier.MidShelf)
            {
                // With downward excavation disabled, the pass must meet the
                // first-rise surface by lifting a long approach from below.
                // A short boundary-local window would leave the untouched
                // shelf face as a sudden drop even though the abstract route
                // profile itself was within grade.
                riseStart = Mathf.Min(riseStart, 0.06f);
                riseEnd = Mathf.Max(riseEnd, 0.90f);
            }
        }

        private void ShapeRawAdvancedRouteAtBridge(
            Vector2[] points,
            Vector2 source,
            Vector2 destination)
        {
            if (points == null || points.Length < 5)
            {
                return;
            }

            var crossings = new List<TrailRiverCrossing>();
            CollectLegacyRiverCrossings(mainRoadSamples, crossings);
            CollectLegacyRiverCrossings(forkRoadSamples, crossings);
            CollectLegacyRiverCrossings(branchRoadASamples, crossings);
            CollectLegacyRiverCrossings(branchRoadBSamples, crossings);
            CollectLegacyRiverCrossings(branchRoadCSamples, crossings);
            if (crossings.Count == 0)
            {
                return;
            }

            int selected = 0;
            float bestScore = float.PositiveInfinity;
            for (int index = 0; index < crossings.Count; index++)
            {
                Vector2 bridge = ToXZ(crossings[index].Point);
                float score = DistanceToSegment(
                    bridge,
                    source,
                    destination) * 2f +
                    Vector2.Distance(source, bridge) * 0.08f +
                    Vector2.Distance(destination, bridge) * 0.08f;
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = index;
                }
            }

            TrailRiverCrossing crossing = crossings[selected];
            Vector2 across = new Vector2(
                -crossing.RiverDirection.z,
                crossing.RiverDirection.x).normalized;
            Vector2 sourceToBridge = ToXZ(crossing.Point) - source;
            if (Vector2.Dot(across, sourceToBridge) < 0f)
            {
                across = -across;
            }

            Vector2 center = ToXZ(crossing.Point);
            points[1] = center - across * CrossingApproachHalfLength;
            points[2] = center;
            points[3] = center + across * CrossingApproachHalfLength;
        }

        private Vector2[] FinalizeAdvancedRoute(
            Vector2[] rawPoints,
            int routeSalt)
        {
            Vector2[] points = SmoothAdvancedRoute(rawPoints);
            ShapeAdvancedRiverCrossings(points);
            KeepAdvancedNonCrossingRouteOnDryBank(points, routeSalt);
            return points;
        }

        private static Vector2[] MirrorAdvancedRoute(
            Vector2[] source,
            Vector2 start,
            Vector2 end)
        {
            var mirrored = new Vector2[source.Length];
            Vector2 axis = (end - start).normalized;
            for (int index = 0; index < source.Length; index++)
            {
                Vector2 offset = source[index] - start;
                Vector2 onAxis = start + axis * Vector2.Dot(offset, axis);
                mirrored[index] = onAxis * 2f - source[index];
            }
            mirrored[0] = start;
            mirrored[mirrored.Length - 1] = end;
            return mirrored;
        }

        private bool AdvancedRouteCrossesUnrelatedConnection(
            Vector2[] candidate,
            int sourceId,
            int destinationId)
        {
            for (int connectionIndex = 0;
                 connectionIndex < advancedLandformConnections.Count;
                 connectionIndex++)
            {
                LandformConnection existing =
                    advancedLandformConnections[connectionIndex];
                if (existing.SourceRegionId == sourceId ||
                    existing.SourceRegionId == destinationId ||
                    existing.DestinationRegionId == sourceId ||
                    existing.DestinationRegionId == destinationId)
                {
                    continue;
                }
                for (int first = 0;
                     first < candidate.Length - 1;
                     first++)
                {
                    for (int second = 0;
                         second < existing.Waypoints.Length - 1;
                         second++)
                    {
                        if (TryFindSegmentIntersection(
                                new Vector3(candidate[first].x, 0f, candidate[first].y),
                                new Vector3(candidate[first + 1].x, 0f, candidate[first + 1].y),
                                new Vector3(existing.Waypoints[second].x, 0f, existing.Waypoints[second].y),
                                new Vector3(existing.Waypoints[second + 1].x, 0f, existing.Waypoints[second + 1].y),
                                out _))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void BuildAdvancedScenicAnchors(Vector2 up, Vector2 right)
        {
            int anchorId = 0;
            for (int index = 1;
                 index < advancedLandformRegions.Count;
                 index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                if (region.Tier != LandformTier.Crown &&
                    anchorId >= 3)
                {
                    continue;
                }
                Vector2 outward = region.Center.sqrMagnitude > 0.01f
                    ? region.Center.normalized
                    : (anchorId & 1) == 0 ? up : right;
                AddAdvancedScenicAnchor(
                    anchorId,
                    $"{region.Name} Outlook",
                    region.Id,
                    region.Center + outward *
                        Mathf.Min(region.Radii.x, region.Radii.y) * 0.52f,
                    outward);
                anchorId++;
                if (anchorId >= 4)
                {
                    break;
                }
            }
        }

        private void AddAdvancedScenicAnchor(
            int id,
            string name,
            int regionId,
            Vector2 position,
            Vector2 lookDirection)
        {
            advancedScenicAnchors.Add(new ScenicAnchor
            {
                Id = id,
                Name = name,
                RegionId = regionId,
                Position = position,
                LookDirection = lookDirection.normalized,
                ClearanceRadius = AdvancedScenicDefaultClearance
            });
        }

        private void BuildAdvancedRoadLists()
        {
            // Validation-only graph rebuilds do not have the production
            // coast-to-coast spline network. Keep the low-tier ascents visible
            // there so graph tests can still inspect the hierarchy in
            // isolation.
            if (mainRoadSamples.Count < 2)
            {
                for (int index = 0;
                     index < advancedLandformConnections.Count;
                     index++)
                {
                    LandformConnection connection =
                        advancedLandformConnections[index];
                    if (IsVisibleLowTierConnection(connection))
                    {
                        var validationTrail = new List<Vector3>(
                            connection.Waypoints.Length);
                        for (int point = 0;
                             point < connection.Waypoints.Length;
                             point++)
                        {
                            Vector2 waypoint = connection.Waypoints[point];
                            validationTrail.Add(new Vector3(
                                waypoint.x,
                                0f,
                                waypoint.y));
                        }
                        CommitAdvancedLowTierTrail(
                            validationTrail,
                            true,
                            24f);
                    }
                }
                return;
            }

            // The production grammar is deliberately small and complete:
            // two coast-to-coast trunks and four to six purposeful branches.
            // Routes are moved around upper shelves as whole curves instead
            // of being clipped into isolated fragments. River crossings stay
            // in the curve and receive bridges later in CreateBridges.
            List<Vector3> firstTrunk =
                BuildContinuousAdvancedLowTierTrail(mainRoadSamples, 0);
            CommitAdvancedLowTierTrail(firstTrunk);

            List<Vector3> secondTrunk =
                BuildContinuousAdvancedLowTierTrail(forkRoadSamples, 1);
            // The two primary routes form one intentional crossroads.
            CommitAdvancedLowTierTrail(secondTrunk, true);

            Vector3[][] branches = layout != null
                ? layout.BranchRoads
                : null;
            if (branches == null || branches.Length == 0)
            {
                branches = new[]
                {
                    layout?.BranchRoadA,
                    layout?.BranchRoadB,
                    layout?.BranchRoadC
                };
            }
            for (int index = 0; index < branches.Length; index++)
            {
                Vector3[] branch = branches[index];
                if (branch == null || branch.Length < 2)
                {
                    continue;
                }
                var samples = new List<Vector3>();
                SampleSpline(branch, 3, samples);
                List<Vector3> continuous =
                    BuildContinuousAdvancedLowTierTrail(
                        samples,
                        17 + index);
                List<Vector3> parent = (index & 1) == 1 &&
                        secondTrunk != null && secondTrunk.Count > 1
                    ? secondTrunk
                    : firstTrunk;
                SnapAdvancedBranchToProcessedTrunk(continuous, parent);
                EnforceAdvancedBranchDeparture(
                    continuous,
                    parent,
                    701 + index * 43);
                CommitAdvancedLowTierTrail(continuous, true);
            }
        }

        private List<Vector3> BuildContinuousAdvancedLowTierTrail(
            IReadOnlyList<Vector3> source,
            int routeSalt)
        {
            if (source == null || source.Count < 2)
            {
                return null;
            }

            var points = new List<Vector2>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                points.Add(ProjectAdvancedTrailToLowTier(
                    ToXZ(source[index]),
                    routeSalt + index * 31));
            }

            // Relax the displaced span as a curve. Re-project after each
            // relaxation so smoothing cannot drift back through an upper
            // shelf and recreate a clipped or deeply carved notch.
            for (int pass = 0; pass < 5; pass++)
            {
                var relaxed = new List<Vector2>(points);
                for (int index = 1; index < points.Count - 1; index++)
                {
                    Vector2 neighbourAverage =
                        (points[index - 1] + points[index + 1]) * 0.5f;
                    relaxed[index] = ProjectAdvancedTrailToLowTier(
                        Vector2.Lerp(points[index], neighbourAverage, 0.38f),
                        routeSalt + index * 31 + pass * 101);
                }
                points = relaxed;
            }

            var result = new List<Vector3>(points.Count * 2);
            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector2 start = points[index];
                Vector2 end = points[index + 1];
                int subdivisions = Mathf.Max(
                    1,
                    Mathf.CeilToInt(Vector2.Distance(start, end) / 5f));
                for (int sample = index == 0 ? 0 : 1;
                     sample <= subdivisions;
                     sample++)
                {
                    Vector2 point = ProjectAdvancedTrailToLowTier(
                        Vector2.Lerp(
                            start,
                            end,
                            sample / (float)subdivisions),
                        routeSalt + index * 131 + sample * 17);
                    result.Add(new Vector3(point.x, 0f, point.y));
                }
            }
            RemoveAdvancedTrailLoops(result);
            return result;
        }

        private static void RemoveAdvancedTrailLoops(List<Vector3> trail)
        {
            if (trail == null || trail.Count < 4)
            {
                return;
            }
            bool removed;
            do
            {
                removed = false;
                for (int first = 0;
                     first < trail.Count - 3 && !removed;
                     first++)
                {
                    for (int second = first + 2;
                         second < trail.Count - 1;
                         second++)
                    {
                        if (!TryFindSegmentIntersection(
                                trail[first],
                                trail[first + 1],
                                trail[second],
                                trail[second + 1],
                                out Vector3 intersection))
                        {
                            continue;
                        }
                        trail.RemoveRange(
                            first + 1,
                            second - first);
                        trail.Insert(first + 1, intersection);
                        removed = true;
                        break;
                    }
                }
            }
            while (removed && trail.Count >= 4);
        }

        private Vector2 ProjectAdvancedTrailToLowTier(
            Vector2 point,
            int routeSalt)
        {
            const float ClearanceInfluence = 0.045f;
            const float GradientSample = 2.5f;
            for (int iteration = 0; iteration < 36; iteration++)
            {
                float influence = UpperTierInfluence(point);
                if (influence < ClearanceInfluence)
                {
                    break;
                }

                Vector2 gradient = new Vector2(
                    UpperTierInfluence(point + Vector2.right * GradientSample) -
                    UpperTierInfluence(point - Vector2.right * GradientSample),
                    UpperTierInfluence(point + Vector2.up * GradientSample) -
                    UpperTierInfluence(point - Vector2.up * GradientSample));
                Vector2 outward = gradient.sqrMagnitude > 0.00001f
                    ? -gradient.normalized
                    : AdvancedTrailUpperTierFallbackDirection(
                        point,
                        routeSalt);
                point += outward * Mathf.Lerp(2.4f, 4.2f, influence);
                point = ClampAdvancedTrailInsideIsland(point, 2f);
            }
            return ClampAdvancedTrailInsideIsland(point, 0.5f);
        }

        private Vector2 AdvancedTrailUpperTierFallbackDirection(
            Vector2 point,
            int routeSalt)
        {
            LandformRegion strongest = null;
            float strongestInfluence = 0f;
            for (int index = 1; index < advancedLandformRegions.Count; index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                if (region.Tier <= LandformTier.MidShelf)
                {
                    continue;
                }
                float influence = AdvancedRegionInfluence(region, point);
                if (influence > strongestInfluence)
                {
                    strongestInfluence = influence;
                    strongest = region;
                }
            }
            Vector2 outward = strongest != null
                ? point - strongest.Center
                : point;
            if (outward.sqrMagnitude > 0.0001f)
            {
                return outward.normalized;
            }
            float angle = (routeSalt * 137.5f + Seed * 0.031f) *
                Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private Vector2 ClampAdvancedTrailInsideIsland(
            Vector2 point,
            float inset)
        {
            float angle = Mathf.Atan2(point.y, point.x);
            float radius = layout != null && layout.CoastRadii != null
                ? layout.CoastRadiusAtAngle(angle)
                : mapRadius;
            float maximum = Mathf.Max(1f, radius - inset);
            return point.magnitude <= maximum
                ? point
                : point.normalized * maximum;
        }

        private static void SnapAdvancedBranchToProcessedTrunk(
            List<Vector3> branch,
            List<Vector3> trunk)
        {
            if (branch == null || branch.Count < 2 ||
                trunk == null || trunk.Count < 2)
            {
                return;
            }
            if (TryClosestPoint(
                    ToXZ(branch[0]),
                    trunk,
                    out Vector2 closest,
                    out _))
            {
                branch[0] = new Vector3(closest.x, 0f, closest.y);
            }
        }

        private void EnforceAdvancedBranchDeparture(
            List<Vector3> branch,
            List<Vector3> trunk,
            int routeSalt)
        {
            if (branch == null || branch.Count < 8 ||
                trunk == null || trunk.Count < 2 ||
                !TryClosestAdvancedTrailTangent(
                    ToXZ(branch[0]),
                    trunk,
                    out Vector2 parentTangent))
            {
                return;
            }

            Vector2 start = ToXZ(branch[0]);
            Vector2 currentDeparture =
                (ToXZ(branch[Mathf.Min(6, branch.Count - 1)]) - start)
                .normalized;
            if (Mathf.Acos(Mathf.Clamp(
                    Mathf.Abs(Vector2.Dot(
                        currentDeparture,
                        parentTangent)),
                    -1f,
                    1f)) >= 0.48f)
            {
                return;
            }

            Vector2 towardDestination =
                (ToXZ(branch[branch.Count - 1]) - start).normalized;
            float preferredSign = Mathf.Sign(
                parentTangent.x * towardDestination.y -
                parentTangent.y * towardDestination.x);
            if (Mathf.Approximately(preferredSign, 0f))
            {
                preferredSign = (routeSalt & 1) == 0 ? 1f : -1f;
            }

            Vector2 bestAnchor = start;
            float bestScore = float.NegativeInfinity;
            float[] candidateAngles = { 58f, 76f, 94f };
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0
                    ? preferredSign
                    : -preferredSign;
                for (int angleIndex = 0;
                     angleIndex < candidateAngles.Length;
                     angleIndex++)
                {
                    Vector2 direction = Rotate(
                        parentTangent,
                        side * candidateAngles[angleIndex] * Mathf.Deg2Rad);
                    Vector2 anchor = ProjectAdvancedTrailToLowTier(
                        start + direction * 32f,
                        routeSalt + sideIndex * 17 + angleIndex * 31);
                    Vector2 actual = (anchor - start).normalized;
                    float departureAngle = Mathf.Acos(Mathf.Clamp(
                        Mathf.Abs(Vector2.Dot(actual, parentTangent)),
                        -1f,
                        1f));
                    float destinationAlignment = Vector2.Dot(
                        actual,
                        towardDestination);
                    float score = departureAngle * 2f +
                        destinationAlignment * 0.25f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestAnchor = anchor;
                    }
                }
            }

            int joinIndex = 1;
            while (joinIndex < branch.Count - 1 &&
                Vector2.Distance(start, ToXZ(branch[joinIndex])) < 42f)
            {
                joinIndex++;
            }
            var rebuilt = new List<Vector3>(branch.Count + 6);
            rebuilt.Add(branch[0]);
            for (int step = 1; step <= 6; step++)
            {
                Vector2 point = ProjectAdvancedTrailToLowTier(
                    Vector2.Lerp(
                        start,
                        bestAnchor,
                        step / 6f),
                    routeSalt + step * 59);
                rebuilt.Add(new Vector3(point.x, 0f, point.y));
            }
            for (int index = joinIndex; index < branch.Count; index++)
            {
                if (Vector3.Distance(
                        rebuilt[rebuilt.Count - 1],
                        branch[index]) > 0.05f)
                {
                    rebuilt.Add(branch[index]);
                }
            }
            branch.Clear();
            branch.AddRange(rebuilt);
            RemoveAdvancedTrailLoops(branch);
        }

        private static bool TryClosestAdvancedTrailTangent(
            Vector2 point,
            List<Vector3> trail,
            out Vector2 tangent)
        {
            tangent = Vector2.up;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < trail.Count - 1; index++)
            {
                Vector2 start = ToXZ(trail[index]);
                Vector2 segment = ToXZ(trail[index + 1]) - start;
                float progress = segment.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point - start, segment) /
                        segment.sqrMagnitude)
                    : 0f;
                float distance = Vector2.Distance(
                    point,
                    start + segment * progress);
                if (distance >= bestDistance)
                {
                    continue;
                }
                bestDistance = distance;
                tangent = segment.normalized;
            }
            return !float.IsPositiveInfinity(bestDistance) &&
                tangent.sqrMagnitude > 0.001f;
        }

        private void CommitAdvancedLowTierTrail(
            List<Vector3> trail,
            bool allowCrossing = false,
            float minimumLength = 48f)
        {
            if (trail == null || trail.Count < 2 ||
                PolylineLength(trail) < minimumLength ||
                (!allowCrossing &&
                 AdvancedTrailCrossesExistingAwayFromFork(trail)))
            {
                return;
            }
            advancedLandformRoads.Add(trail);
            advancedLandformRouteQuery.Add(trail);
        }

        private bool IsVisibleLowTierConnection(LandformConnection connection)
        {
            return connection != null &&
                advancedLandformRegions[connection.SourceRegionId].Tier <=
                    LandformTier.MidShelf &&
                advancedLandformRegions[connection.DestinationRegionId].Tier <=
                    LandformTier.MidShelf;
        }

        private void AddAdvancedLowTierTrailSegments(Vector2[] source)
        {
            if (source == null || source.Length < 2)
            {
                return;
            }
            AddAdvancedLowTierTrailSegmentsCore(
                index => source[index],
                source.Length);
        }

        private void AddAdvancedLowTierTrailSegments(List<Vector3> source)
        {
            if (source == null || source.Count < 2)
            {
                return;
            }
            AddAdvancedLowTierTrailSegmentsCore(
                index => ToXZ(source[index]),
                source.Count);
        }

        private void AddAdvancedLowTierTrailSegmentsCore(
            Func<int, Vector2> pointAt,
            int count)
        {
            List<Vector3> segment = null;
            for (int index = 0; index < count; index++)
            {
                Vector2 point = pointAt(index);
                bool valid = IsCleanLowTierTrailPoint(point);
                if (valid && segment != null && segment.Count > 0)
                {
                    Vector2 previous = ToXZ(segment[segment.Count - 1]);
                    for (int sample = 1; sample <= 4; sample++)
                    {
                        if (!IsCleanLowTierTrailPoint(
                                Vector2.Lerp(previous, point, sample / 4f)))
                        {
                            valid = false;
                            break;
                        }
                    }
                }
                if (!valid)
                {
                    CommitAdvancedLowTierTrailSegment(segment);
                    segment = null;
                    continue;
                }
                segment ??= new List<Vector3>(count);
                segment.Add(new Vector3(point.x, 0f, point.y));
            }
            CommitAdvancedLowTierTrailSegment(segment);
        }

        private void CommitAdvancedLowTierTrailSegment(List<Vector3> segment)
        {
            if (segment == null || segment.Count < 2 ||
                PolylineLength(segment) < 24f ||
                AdvancedTrailCrossesExistingAwayFromFork(segment))
            {
                return;
            }
            advancedLandformRoads.Add(segment);
            advancedLandformRouteQuery.Add(segment);
        }

        private bool AdvancedTrailCrossesExistingAwayFromFork(
            List<Vector3> candidate)
        {
            Vector2 candidateStart = ToXZ(candidate[0]);
            Vector2 candidateEnd = ToXZ(candidate[candidate.Count - 1]);
            for (int roadIndex = 0;
                 roadIndex < advancedLandformRoads.Count;
                 roadIndex++)
            {
                List<Vector3> existing = advancedLandformRoads[roadIndex];
                Vector2 existingStart = ToXZ(existing[0]);
                Vector2 existingEnd = ToXZ(existing[existing.Count - 1]);
                for (int first = 0; first < candidate.Count - 1; first++)
                {
                    for (int second = 0; second < existing.Count - 1; second++)
                    {
                        if (!TryFindSegmentIntersection(
                                candidate[first],
                                candidate[first + 1],
                                existing[second],
                                existing[second + 1],
                                out Vector3 intersection))
                        {
                            continue;
                        }
                        Vector2 point = ToXZ(intersection);
                        bool cleanFork =
                            Vector2.Distance(point, candidateStart) < 1f ||
                            Vector2.Distance(point, candidateEnd) < 1f ||
                            Vector2.Distance(point, existingStart) < 1f ||
                            Vector2.Distance(point, existingEnd) < 1f;
                        if (!cleanFork)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool IsCleanLowTierTrailPoint(Vector2 point)
        {
            return IsInsideIsland(point, 2f) &&
                UpperTierInfluence(point) < 0.06f &&
                !IsWithinRiverDistance(point, riverHalfWidth + 2f);
        }

        private float UpperTierInfluence(Vector2 point)
        {
            float influence = 0f;
            for (int index = 1; index < advancedLandformRegions.Count; index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                if (region.Tier <= LandformTier.MidShelf)
                {
                    continue;
                }
                influence = Mathf.Max(
                    influence,
                    AdvancedRegionInfluence(region, point));
            }
            return influence;
        }

        private void AddAdvancedLandformRoutesToRoadQuery()
        {
            if (!enableAdvancedLandforms)
            {
                return;
            }
            for (int index = 0; index < advancedLandformRoads.Count; index++)
            {
                roadQuery.Add(advancedLandformRoads[index]);
            }
        }

        private float AdvancedLandformHeight(Vector2 point)
        {
            float targetHeight = 0f;
            for (int index = 0;
                 index < advancedLandformRegions.Count;
                 index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                float influence = AdvancedRegionInfluence(region, point);
                if (region.TargetHeight < 0f)
                {
                    targetHeight += region.TargetHeight * influence;
                    continue;
                }
                float relief = region.Tier == LandformTier.Crown
                    ? AdvancedPlateauRelief(region, point) * influence
                    : region.Tier == LandformTier.Highland
                        ? AdvancedPlateauRelief(region, point) *
                            influence * 0.35f
                        : 0f;
                float candidate = region.TargetHeight * influence + relief;
                if (candidate > targetHeight)
                {
                    targetHeight = candidate;
                }
            }

            // Establish the broad river valley before cutting authored
            // traversal corridors through it. Otherwise a trail following
            // the valley edge inherits that lateral drop even when its
            // longitudinal route profile is safe. The immediate channel
            // still wins below, so open water is never raised by a trail.
            float riverLowlandInfluence =
                AdvancedRiverLowlandInfluence(point);
            // Flatten the complete valley to the river datum, rather than
            // merely removing the tier uplift. That leaves no inherited base
            // noise for the water to visibly undercut at the bank.
            float riverDatumOffset = BaseLandHeight(0f, 0f) -
                BaseLandHeight(point.x, point.y);
            targetHeight = Mathf.Lerp(
                targetHeight,
                riverDatumOffset,
                riverLowlandInfluence);

            float routeSearchDistance =
                AdvancedRouteTerrainHalfWidth + AdvancedCliffGapPadding;
            if (UpperTierInfluence(point) < 0.06f &&
                TryClosestAdvancedConnection(
                    point,
                    routeSearchDistance,
                    out LandformConnection connection,
                    out float routeDistance,
                    out float routeProgress))
            {
                float corridorInfluence = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        AdvancedRouteTerrainHalfWidth,
                        routeSearchDistance,
                        routeDistance));
                float routeWorldHeight =
                    AdvancedRouteWorldHeight(
                        point,
                        connection,
                        routeProgress);
                // The corridor owns the complete centerline profile. Merely
                // blending the uplift contribution left the base noise under
                // the trail, which could create controller-breaking spikes
                // even when the abstract graph grade was safe.
                float routeHeight = routeWorldHeight -
                    BaseLandHeight(point.x, point.y);
                // A traversal corridor may lift natural ground into a broad
                // ramp, but it must never excavate a trench below the base
                // terrain. The previous unrestricted blend could pull a
                // shelf approach several metres downward and create the
                // abrupt "super-down" cuts visible in map review.
                routeHeight = Mathf.Max(0f, routeHeight);
                targetHeight = Mathf.Lerp(
                    targetHeight,
                    Mathf.Max(targetHeight, routeHeight),
                    corridorInfluence);
            }

            return targetHeight;
        }

        private float AdvancedRiverLowlandInfluence(Vector2 point)
        {
            float inner = riverHalfWidth +
                AdvancedRiverValleyInnerPadding;
            float outer = riverHalfWidth +
                AdvancedRiverValleyOuterPadding;
            if (!riverQuery.TryClosestPointWithin(
                    point,
                    outer,
                    out _,
                    out float distance))
            {
                return 0f;
            }
            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(inner, outer, distance));
        }

        private float AdvancedRiverWaterHeight(float x, float z)
        {
            // The legacy river followed RawLandHeight point-by-point. That
            // is correct for rolling terrain but turns an uplift boundary
            // into a steep water ramp. Advanced maps use one stable lowland
            // datum, with the existing coast blend handling both mouths.
            return BaseLandHeight(0f, 0f) - 1.55f;
        }

        private void RerouteAdvancedRiverThroughLowlands(int seed)
        {
            if (layout == null || layout.River == null ||
                layout.River.Length < 7)
            {
                return;
            }

            Vector3[] river = layout.River;
            var original = (Vector3[])river.Clone();
            Vector2 mouthA = ToXZ(original[0]);
            Vector2 mouthB = ToXZ(original[original.Length - 1]);
            Vector2 axis = mouthB - mouthA;
            if (axis.sqrMagnitude < 1f)
            {
                return;
            }
            axis.Normalize();
            Vector2 across = new Vector2(-axis.y, axis.x);
            float maximumOffset = mapRadius * 0.36f;
            float windingAmplitude = mapRadius * 0.082f;
            float maximumOffsetStep = mapRadius * 0.095f;
            const float OffsetStep = 3f;
            int halfOffsetCount = Mathf.CeilToInt(
                maximumOffset / OffsetStep);
            int offsetCount = halfOffsetCount * 2 + 1;
            int pointCount = river.Length;
            var costs = new float[pointCount, offsetCount];
            var previousOffsets = new int[pointCount, offsetCount];
            for (int point = 0; point < pointCount; point++)
            {
                for (int offsetIndex = 0;
                     offsetIndex < offsetCount;
                     offsetIndex++)
                {
                    costs[point, offsetIndex] = float.PositiveInfinity;
                    previousOffsets[point, offsetIndex] = -1;
                }
            }
            costs[0, halfOffsetCount] = 0f;
            float phase = Mathf.Repeat(seed * 0.000173f, 1f) *
                Mathf.PI * 2f;
            for (int index = 1; index < pointCount; index++)
            {
                float progress = index / (pointCount - 1f);
                Vector2 center = Vector2.Lerp(mouthA, mouthB, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float preferredOffset =
                    (Mathf.Sin(
                         progress * Mathf.PI * 2f * 3.15f + phase) *
                         windingAmplitude +
                     Mathf.Sin(
                         progress * Mathf.PI * 2f * 1.45f - phase * 0.37f) *
                         windingAmplitude * 0.28f) * envelope;

                int firstOffset = index == pointCount - 1
                    ? halfOffsetCount
                    : 0;
                int lastOffset = index == pointCount - 1
                    ? halfOffsetCount
                    : offsetCount - 1;
                for (int offsetIndex = firstOffset;
                     offsetIndex <= lastOffset;
                     offsetIndex++)
                {
                    float offset =
                        (offsetIndex - halfOffsetCount) * OffsetStep;
                    Vector2 candidate = center + across * offset;
                    if (index < pointCount - 1 &&
                        !IsInsideIsland(candidate, 7f))
                    {
                        continue;
                    }
                    float raised = AdvancedFirstRiseInfluence(candidate);
                    float windingCost = Mathf.Abs(
                        offset - preferredOffset) * 0.72f;
                    float pointCost = raised * raised * 12000f +
                        windingCost;
                    for (int previousIndex = 0;
                         previousIndex < offsetCount;
                         previousIndex++)
                    {
                        float priorCost = costs[index - 1, previousIndex];
                        if (float.IsPositiveInfinity(priorCost))
                        {
                            continue;
                        }
                        float previousOffset =
                            (previousIndex - halfOffsetCount) * OffsetStep;
                        float change = Mathf.Abs(offset - previousOffset);
                        if (change > maximumOffsetStep)
                        {
                            continue;
                        }
                        float score = priorCost + pointCost +
                            change * 0.42f + change * change * 0.020f;
                        if (score < costs[index, offsetIndex])
                        {
                            costs[index, offsetIndex] = score;
                            previousOffsets[index, offsetIndex] =
                                previousIndex;
                        }
                    }
                }
            }

            int selectedOffset = halfOffsetCount;
            for (int index = pointCount - 1; index >= 1; index--)
            {
                float progress = index / (pointCount - 1f);
                Vector2 center = Vector2.Lerp(mouthA, mouthB, progress);
                float offset =
                    (selectedOffset - halfOffsetCount) * OffsetStep;
                Vector2 selected = center + across * offset;
                river[index] = new Vector3(
                    selected.x,
                    river[index].y,
                    selected.y);
                int previous = previousOffsets[index, selectedOffset];
                selectedOffset = previous >= 0
                    ? previous
                    : halfOffsetCount;
            }

            // Remove point-to-point kinks without allowing the smoothing pass
            // to drift back onto the first rise.
            for (int pass = 0; pass < 1; pass++)
            {
                var smoothed = (Vector3[])river.Clone();
                for (int index = 1; index < river.Length - 1; index++)
                {
                    Vector2 current = ToXZ(river[index]);
                    Vector2 candidate =
                        ToXZ(river[index - 1]) * 0.20f +
                        current * 0.60f +
                        ToXZ(river[index + 1]) * 0.20f;
                    if (IsInsideIsland(candidate, 7f) &&
                        AdvancedFirstRiseInfluence(candidate) <=
                            AdvancedFirstRiseInfluence(current) + 0.025f)
                    {
                        smoothed[index] = new Vector3(
                            candidate.x,
                            river[index].y,
                            candidate.y);
                    }
                }
                Array.Copy(smoothed, river, river.Length);
            }

            riverSamples.Clear();
            SampleSpline(river, 5, riverSamples);
            riverQuery.Clear();
            riverQuery.Add(riverSamples);
        }

        private float AdvancedFirstRiseInfluence(Vector2 point)
        {
            float influence = 0f;
            for (int index = 1; index < advancedLandformRegions.Count; index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                if (region.Tier != LandformTier.MidShelf)
                {
                    continue;
                }
                influence = Mathf.Max(
                    influence,
                    AdvancedRegionInfluence(region, point));
            }
            return influence;
        }

        private LandformTier AdvancedTierAt(Vector2 point)
        {
            int regionId = ResolveNearestAdvancedRegionId(point);
            return regionId >= 0 && regionId < advancedLandformRegions.Count
                ? advancedLandformRegions[regionId].Tier
                : LandformTier.Lowland;
        }

        public LandformTier AdvancedTierAtForValidation(Vector2 point)
        {
            return AdvancedTierAt(point);
        }

        public float AdvancedTerrainOffsetForValidation(Vector2 point)
        {
            return AdvancedLandformHeight(point);
        }

        public float AdvancedRiverValleyInfluenceForValidation(
            Vector2 point)
        {
            return AdvancedRiverLowlandInfluence(point);
        }

        private bool TryClosestAdvancedConnection(
            Vector2 point,
            float searchDistance,
            out LandformConnection connection,
            out float distance,
            out float progress)
        {
            connection = null;
            distance = searchDistance;
            progress = 0f;
            for (int index = 0;
                 index < advancedLandformConnections.Count;
                 index++)
            {
                LandformConnection candidate =
                    advancedLandformConnections[index];
                if (!IsVisibleLowTierConnection(candidate))
                {
                    continue;
                }
                if (!TryClosestPointOnPolyline(
                        point,
                        candidate.Waypoints,
                        out _,
                        out float candidateDistance,
                        out float candidateProgress) ||
                    candidateDistance > distance)
                {
                    continue;
                }
                connection = candidate;
                distance = candidateDistance;
                progress = candidateProgress;
            }
            return connection != null;
        }

        private float AdvancedRouteWorldHeight(
            Vector2 point,
            LandformConnection connection,
            float progress)
        {
            LandformRegion source =
                advancedLandformRegions[connection.SourceRegionId];
            LandformRegion destination =
                advancedLandformRegions[connection.DestinationRegionId];
            float rise = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    connection.RiseStartProgress,
                    connection.RiseEndProgress,
                    progress));
            float sourceWorldHeight = BaseLandHeight(
                    connection.Waypoints[0].x,
                    connection.Waypoints[0].y) +
                source.TargetHeight;
            float destinationWorldHeight = BaseLandHeight(
                    connection.Waypoints[
                        connection.Waypoints.Length - 1].x,
                    connection.Waypoints[
                        connection.Waypoints.Length - 1].y) +
                destination.TargetHeight;
            // A child-tier connection is an ascent. Base noise can otherwise
            // put its nominally higher endpoint below the source and turn the
            // authored pass into a sudden downhill excavation. Preserve the
            // source datum and only rise from it.
            destinationWorldHeight = Mathf.Max(
                destinationWorldHeight,
                sourceWorldHeight);
            float tierOffset = Mathf.Lerp(
                sourceWorldHeight,
                destinationWorldHeight,
                rise);
            return tierOffset;
        }

        private Vector2 ProjectAdvancedWaypointToDryBank(
            Vector2 point,
            int sideSalt)
        {
            if (!TryClosestAdvancedRiverPoint(
                    point,
                    out Vector2 closest,
                    out Vector2 tangent,
                    out float distance))
            {
                return point;
            }
            float requiredClearance = riverHalfWidth + 4.2f;
            if (distance >= requiredClearance)
            {
                return point;
            }

            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            Vector2 offset = point - closest;
            float side = Vector2.Dot(offset, normal);
            if (Mathf.Abs(side) <= 0.001f)
            {
                side = (sideSalt & 1) == 0 ? 1f : -1f;
            }
            return closest + normal * Mathf.Sign(side) * requiredClearance;
        }

        private static Vector2[] SmoothAdvancedRoute(Vector2[] source)
        {
            if (source == null || source.Length < 3)
            {
                return source;
            }

            var points = new List<Vector2>(source);
            for (int pass = 0; pass < AdvancedRouteSmoothingPasses; pass++)
            {
                var rounded = new List<Vector2>(points.Count * 2);
                rounded.Add(points[0]);
                for (int index = 0; index < points.Count - 1; index++)
                {
                    Vector2 start = points[index];
                    Vector2 end = points[index + 1];
                    rounded.Add(Vector2.Lerp(start, end, 0.25f));
                    rounded.Add(Vector2.Lerp(start, end, 0.75f));
                }
                rounded.Add(points[points.Count - 1]);
                points = rounded;
            }
            return points.ToArray();
        }

        private void ShapeAdvancedRiverCrossings(Vector2[] points)
        {
            if (points == null ||
                points.Length < 4 ||
                riverSamples.Count < 2)
            {
                return;
            }

            var road = new Vector3[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                road[index] = new Vector3(
                    points[index].x,
                    0f,
                    points[index].y);
            }
            var unsnappedRoad = (Vector3[])road.Clone();
            SnapAdvancedCrossingToNearbyLegacyBridge(road);
            // Advanced routes are authored after Catmull-Rom river sampling.
            // Shape them against that final runtime polyline rather than the
            // sparse layout controls, or the visible crossing, bridge search,
            // terrain carve, and navigation lane can disagree by metres.
            StraightenRiverCrossings(road, riverSamples.ToArray());
            if (AdvancedRouteSelfIntersects(road))
            {
                Array.Copy(unsnappedRoad, road, road.Length);
                StraightenRiverCrossings(road, riverSamples.ToArray());
                if (AdvancedRouteSelfIntersects(road))
                {
                    Array.Copy(unsnappedRoad, road, road.Length);
                }
            }
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = ToXZ(road[index]);
            }
        }

        private void SnapAdvancedCrossingToNearbyLegacyBridge(Vector3[] road)
        {
            var routeCrossings = new List<TrailRiverCrossing>();
            FindPolylineIntersections(
                new List<Vector3>(road),
                riverSamples,
                routeCrossings);
            if (routeCrossings.Count == 0)
            {
                return;
            }

            var legacyCrossings = new List<TrailRiverCrossing>();
            CollectLegacyRiverCrossings(mainRoadSamples, legacyCrossings);
            CollectLegacyRiverCrossings(forkRoadSamples, legacyCrossings);
            CollectLegacyRiverCrossings(branchRoadASamples, legacyCrossings);
            CollectLegacyRiverCrossings(branchRoadBSamples, legacyCrossings);
            CollectLegacyRiverCrossings(branchRoadCSamples, legacyCrossings);
            for (int crossingIndex = 0;
                 crossingIndex < routeCrossings.Count;
                 crossingIndex++)
            {
                TrailRiverCrossing routeCrossing =
                    routeCrossings[crossingIndex];
                int nearestLegacy = -1;
                float nearestDistance = 8f;
                for (int legacyIndex = 0;
                     legacyIndex < legacyCrossings.Count;
                     legacyIndex++)
                {
                    float distance = Vector3.Distance(
                        routeCrossing.Point,
                        legacyCrossings[legacyIndex].Point);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestLegacy = legacyIndex;
                    }
                }
                if (nearestLegacy < 0)
                {
                    continue;
                }

                TrailRiverCrossing sharedCrossing =
                    legacyCrossings[nearestLegacy];
                Vector3 crossingDirection = Vector3.Cross(
                    Vector3.up,
                    sharedCrossing.RiverDirection).normalized;
                if (Vector3.Dot(
                        crossingDirection,
                        routeCrossing.RoadDirection) < 0f)
                {
                    crossingDirection = -crossingDirection;
                }
                int roadSegment = ClosestAdvancedRouteSegment(
                    road,
                    routeCrossing.Point);
                var candidate = (Vector3[])road.Clone();
                SetCrossingControlPoint(
                    candidate,
                    roadSegment - 1,
                    sharedCrossing.Point - crossingDirection *
                    CrossingApproachHalfLength);
                SetCrossingControlPoint(
                    candidate,
                    roadSegment,
                    sharedCrossing.Point - crossingDirection *
                    CrossingStraightHalfLength);
                SetCrossingControlPoint(
                    candidate,
                    roadSegment + 1,
                    sharedCrossing.Point + crossingDirection *
                    CrossingStraightHalfLength);
                SetCrossingControlPoint(
                    candidate,
                    roadSegment + 2,
                    sharedCrossing.Point + crossingDirection *
                    CrossingApproachHalfLength);
                if (!AdvancedRouteSelfIntersects(candidate))
                {
                    Array.Copy(candidate, road, road.Length);
                }
            }
        }

        private static bool AdvancedRouteSelfIntersects(Vector3[] road)
        {
            for (int first = 0; first < road.Length - 1; first++)
            {
                for (int second = first + 2;
                     second < road.Length - 1;
                     second++)
                {
                    if (TryFindSegmentIntersection(
                            road[first],
                            road[first + 1],
                            road[second],
                            road[second + 1],
                            out _))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CollectLegacyRiverCrossings(
            List<Vector3> road,
            List<TrailRiverCrossing> crossings)
        {
            if (road.Count > 1)
            {
                FindPolylineIntersections(road, riverSamples, crossings);
            }
        }

        private static int ClosestAdvancedRouteSegment(
            Vector3[] road,
            Vector3 point)
        {
            int bestSegment = 0;
            float bestDistance = float.PositiveInfinity;
            Vector2 target = new Vector2(point.x, point.z);
            for (int index = 0; index < road.Length - 1; index++)
            {
                float distance = DistanceToSegment(
                    target,
                    new Vector2(road[index].x, road[index].z),
                    new Vector2(road[index + 1].x, road[index + 1].z));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSegment = index;
                }
            }
            return bestSegment;
        }

        private void KeepAdvancedNonCrossingRouteOnDryBank(
            Vector2[] points,
            int sideSalt)
        {
            if (points == null ||
                points.Length < 2 ||
                AdvancedRouteIntersectsRiverCenterline(points))
            {
                return;
            }

            // Corner cutting can pull a rounded same-bank route back into
            // the river shoulder even though all of its original controls
            // were dry. Re-project the final dense polyline; genuine river
            // crossings are deliberately left intact for bridge creation.
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = ProjectAdvancedWaypointToDryBank(
                    points[index],
                    sideSalt);
            }
        }

        private bool AdvancedRouteIntersectsRiverCenterline(
            Vector2[] points)
        {
            for (int routeSegment = 0;
                 routeSegment < points.Length - 1;
                 routeSegment++)
            {
                Vector3 routeStart = new Vector3(
                    points[routeSegment].x,
                    0f,
                    points[routeSegment].y);
                Vector3 routeEnd = new Vector3(
                    points[routeSegment + 1].x,
                    0f,
                    points[routeSegment + 1].y);
                for (int riverSegment = 0;
                     riverSegment < riverSamples.Count - 1;
                     riverSegment++)
                {
                    if (TryFindSegmentIntersection(
                            routeStart,
                            routeEnd,
                            riverSamples[riverSegment],
                            riverSamples[riverSegment + 1],
                            out _))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryClosestAdvancedRiverPoint(
            Vector2 point,
            out Vector2 closest,
            out Vector2 tangent,
            out float distance)
        {
            closest = point;
            tangent = elevationDirection.sqrMagnitude > 0.001f
                ? elevationDirection.normalized
                : Vector2.up;
            distance = float.PositiveInfinity;
            if (riverSamples.Count < 2)
            {
                return false;
            }
            for (int index = 0; index < riverSamples.Count - 1; index++)
            {
                Vector2 start = ToXZ(riverSamples[index]);
                Vector2 delta = ToXZ(riverSamples[index + 1]) - start;
                float lengthSquared = delta.sqrMagnitude;
                float progress = lengthSquared > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(
                        point - start,
                        delta) / lengthSquared)
                    : 0f;
                Vector2 candidate = start + delta * progress;
                float candidateDistance = Vector2.Distance(point, candidate);
                if (candidateDistance >= distance)
                {
                    continue;
                }
                closest = candidate;
                tangent = lengthSquared > 0.0001f
                    ? delta.normalized
                    : tangent;
                distance = candidateDistance;
            }
            return !float.IsPositiveInfinity(distance);
        }

        private float CalculateAdvancedConnectionGrade(
            LandformRegion source,
            LandformRegion destination,
            Vector2[] points,
            float riseStart,
            float riseEnd)
        {
            float maximum = 0f;
            float totalLength = Mathf.Max(0.01f, PolylineLength(points));
            float traversed = 0f;
            float sourceWorldHeight = BaseLandHeight(
                    points[0].x,
                    points[0].y) +
                source.TargetHeight;
            float destinationWorldHeight = BaseLandHeight(
                    points[points.Length - 1].x,
                    points[points.Length - 1].y) +
                destination.TargetHeight;
            destinationWorldHeight = Mathf.Max(
                destinationWorldHeight,
                sourceWorldHeight);
            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                float length = Vector2.Distance(
                    points[segment],
                    points[segment + 1]);
                int samples = Mathf.Max(2, Mathf.CeilToInt(length / 3f));
                Vector2 previous = points[segment];
                float previousProgress = traversed / totalLength;
                float previousHeight = Mathf.Lerp(
                        sourceWorldHeight,
                        destinationWorldHeight,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(
                                riseStart,
                                riseEnd,
                                previousProgress)));
                for (int sample = 1; sample <= samples; sample++)
                {
                    float segmentProgress = sample / (float)samples;
                    Vector2 current = Vector2.Lerp(
                        points[segment],
                        points[segment + 1],
                        segmentProgress);
                    float progress = (traversed +
                        length * segmentProgress) / totalLength;
                    float currentHeight = Mathf.Lerp(
                            sourceWorldHeight,
                            destinationWorldHeight,
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                Mathf.InverseLerp(
                                    riseStart,
                                    riseEnd,
                                    progress)));
                    maximum = Mathf.Max(
                        maximum,
                        Mathf.Abs(currentHeight - previousHeight) /
                        Mathf.Max(0.01f, Vector2.Distance(previous, current)));
                    previous = current;
                    previousHeight = currentHeight;
                }
                traversed += length;
            }
            return maximum;
        }

        private float AdvancedPlateauRelief(
            LandformRegion region,
            Vector2 point)
        {
            float broad = Mathf.PerlinNoise(
                noiseOffsetB.x * 0.0013f + point.x * 0.010f + region.Id * 3.1f,
                noiseOffsetB.y * 0.0013f + point.y * 0.010f + region.Id * 1.7f);
            return (broad - 0.5f) * 1.7f;
        }

        private float AdvancedRegionInfluence(
            LandformRegion region,
            Vector2 point)
        {
            float influence = RawAdvancedRegionInfluence(region, point);
            if (region.ParentRegionId > 0)
            {
                LandformRegion parent =
                    advancedLandformRegions[region.ParentRegionId];
                influence *= AdvancedRegionInfluence(parent, point);
            }
            return influence;
        }

        private float RawAdvancedRegionInfluence(
            LandformRegion region,
            Vector2 point)
        {
            Vector2 warped = point + AdvancedDomainWarp(point, region.Id);
            Vector2 local = Rotate(
                warped - region.Center,
                -region.RotationDegrees * Mathf.Deg2Rad);
            float angle = Mathf.Atan2(
                local.y / Mathf.Max(1f, region.Radii.y),
                local.x / Mathf.Max(1f, region.Radii.x));
            float winding =
                Mathf.Sin(angle * 3f + region.BoundaryPhase) *
                    region.BoundaryWobble +
                Mathf.Sin(angle * 5f - region.BoundaryPhase * 0.73f) *
                    region.BoundaryWobble * 0.48f +
                Mathf.Sin(angle * 7f + region.BoundaryPhase * 1.31f) *
                    region.BoundaryWobble * 0.22f;
            float notchAlignment = Mathf.Max(
                0f,
                Mathf.Cos(angle - region.NotchAngle));
            float notch = region.NotchStrength *
                notchAlignment * notchAlignment *
                notchAlignment * notchAlignment;
            float boundaryScale = Mathf.Max(
                0.42f,
                1f + winding - notch);
            float normalized = new Vector2(
                local.x / Mathf.Max(1f, region.Radii.x),
                local.y / Mathf.Max(1f, region.Radii.y)).magnitude /
                boundaryScale;
            float approximateDistance =
                (normalized - 1f) * Mathf.Min(region.Radii.x, region.Radii.y);
            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    -region.EdgeBlendWidth,
                    region.EdgeBlendWidth,
                    approximateDistance));
        }

        private Vector2 AdvancedDomainWarp(Vector2 point, int salt)
        {
            float scale = 0.011f;
            float x = Mathf.PerlinNoise(
                noiseOffsetA.x * 0.001f + point.x * scale + salt * 2.7f,
                noiseOffsetA.y * 0.001f + point.y * scale + 13.1f) - 0.5f;
            float y = Mathf.PerlinNoise(
                noiseOffsetB.x * 0.001f + point.x * scale + 31.7f,
                noiseOffsetB.y * 0.001f + point.y * scale + salt * 1.9f) - 0.5f;
            // A modest warp keeps tier edges organic without pinching a
            // region into the thin, isolated growths seen in the review.
            return new Vector2(x, y) * 3.5f;
        }

        private float AdvancedTreeDensityMultiplier(Vector2 point)
        {
            if (IsInsideAdvancedScenicClearance(point))
            {
                return 0f;
            }

            // Forest coverage is deliberately independent from landform tier.
            // Plains, shelves, valleys, and highlands all receive the same
            // seed-rotated patch field, so elevation can never turn one whole
            // side into the forest side of the island.
            float patchSignal = AdvancedTreePatchSignal(point);
            if (patchSignal <= advancedTreeOpenThreshold)
            {
                return 0f;
            }

            if (patchSignal < advancedTreeDenseThreshold)
            {
                float transition = Mathf.InverseLerp(
                    advancedTreeOpenThreshold,
                    advancedTreeDenseThreshold,
                    patchSignal);
                return Mathf.Lerp(
                    0.24f,
                    0.70f,
                    Mathf.SmoothStep(0f, 1f, transition));
            }

            float denseTransition = Mathf.InverseLerp(
                advancedTreeDenseThreshold,
                0.78f,
                patchSignal);
            return Mathf.Lerp(
                0.84f,
                1f,
                Mathf.SmoothStep(0f, 1f, denseTransition));
        }

        private float AdvancedTreePatchSignal(Vector2 point)
        {
            float rotation = Mathf.Repeat(
                noiseOffsetA.x * 0.000173f +
                noiseOffsetB.y * 0.000119f,
                1f) * Mathf.PI * 2f;
            Vector2 warpPoint = Rotate(point, rotation + 0.79f);
            float warpX = Mathf.PerlinNoise(
                noiseOffsetA.x * 0.0021f + warpPoint.x * 0.0055f + 81.7f,
                noiseOffsetB.y * 0.0021f + warpPoint.y * 0.0055f + 26.4f);
            float warpY = Mathf.PerlinNoise(
                noiseOffsetB.x * 0.0023f + warpPoint.x * 0.0055f + 12.6f,
                noiseOffsetA.y * 0.0023f + warpPoint.y * 0.0055f + 64.9f);
            Vector2 warpedPoint = point + new Vector2(
                warpX - 0.5f,
                warpY - 0.5f) * 38f;
            Vector2 macroPoint = Rotate(warpedPoint, rotation);
            Vector2 supportPoint = Rotate(warpedPoint, rotation + 2.17f);
            Vector2 detailPoint = Rotate(warpedPoint, rotation - 1.31f);
            float macro = Mathf.PerlinNoise(
                noiseOffsetA.x * 0.0037f + macroPoint.x * 0.0105f + 41.2f,
                noiseOffsetA.y * 0.0037f + macroPoint.y * 0.0105f + 17.6f);
            float support = Mathf.PerlinNoise(
                noiseOffsetB.x * 0.0049f + supportPoint.x * 0.0205f + 9.8f,
                noiseOffsetB.y * 0.0049f + supportPoint.y * 0.0205f + 53.1f);
            float detail = Mathf.PerlinNoise(
                noiseOffsetA.y * 0.0053f + detailPoint.x * 0.038f + 73.4f,
                noiseOffsetB.x * 0.0053f + detailPoint.y * 0.038f + 28.7f);
            return macro * 0.72f + support * 0.22f + detail * 0.06f;
        }

        private void ConfigureAdvancedTreeMosaic()
        {
            if (!enableAdvancedLandforms || layout == null)
            {
                return;
            }

            float extent = IslandGenerationExtent;
            float diameter = extent * 2f;
            var eligibleSignals = new List<float>(
                AdvancedTreeCoverageResolution *
                AdvancedTreeCoverageResolution);
            int islandSamples = 0;
            int forcedOpenSamples = 0;
            for (int z = 0; z < AdvancedTreeCoverageResolution; z++)
            {
                for (int x = 0; x < AdvancedTreeCoverageResolution; x++)
                {
                    Vector2 point = new Vector2(
                        -extent + diameter * x /
                            (AdvancedTreeCoverageResolution - 1f),
                        -extent + diameter * z /
                            (AdvancedTreeCoverageResolution - 1f));
                    if (!IsInsideIsland(point, CoastPlacementInset))
                    {
                        continue;
                    }

                    islandSamples++;
                    if (IsInsideAdvancedScenicClearance(point))
                    {
                        forcedOpenSamples++;
                        continue;
                    }
                    eligibleSignals.Add(AdvancedTreePatchSignal(point));
                }
            }

            if (eligibleSignals.Count == 0 || islandSamples == 0)
            {
                return;
            }

            eligibleSignals.Sort();
            int desiredDenseSamples = Mathf.Min(
                eligibleSignals.Count,
                Mathf.RoundToInt(
                    islandSamples * TargetDenseForestCoverage));
            int denseStart = Mathf.Clamp(
                eligibleSignals.Count - desiredDenseSamples,
                0,
                eligibleSignals.Count - 1);
            advancedTreeDenseThreshold = eligibleSignals[denseStart];

            int desiredOpenSamples = Mathf.RoundToInt(
                islandSamples * TargetOpenPlainCoverage);
            int additionalOpenSamples = Mathf.Clamp(
                desiredOpenSamples - forcedOpenSamples,
                0,
                Mathf.Max(0, denseStart - 1));
            advancedTreeOpenThreshold = additionalOpenSamples > 0
                ? eligibleSignals[additionalOpenSamples - 1]
                : eligibleSignals[0] - 0.001f;
        }

        private float AdvancedCliffBandInfluence(Vector2 point)
        {
            float influence = 0f;
            for (int index = 0;
                 index < advancedLandformRegions.Count;
                 index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                if (region.Tier == LandformTier.Lowland)
                {
                    continue;
                }
                Vector2 local = Rotate(
                    point - region.Center,
                    -region.RotationDegrees * Mathf.Deg2Rad);
                float normalized = new Vector2(
                    local.x / Mathf.Max(1f, region.Radii.x),
                    local.y / Mathf.Max(1f, region.Radii.y)).magnitude;
                float band = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Abs(normalized - 1f) / 0.18f);
                influence = Mathf.Max(influence, band);
            }
            return IsInsideAdvancedTraversalLane(point, 2f)
                ? 0f
                : influence;
        }

        private bool IsInsideAdvancedScenicClearance(Vector2 point)
        {
            for (int index = 0; index < advancedScenicAnchors.Count; index++)
            {
                ScenicAnchor anchor = advancedScenicAnchors[index];
                Vector2 offset = point - anchor.Position;
                float angle = Mathf.Atan2(offset.y, offset.x);
                float boundaryScale = 1f +
                    Mathf.Sin(angle * 3f + anchor.Id * 1.71f) * 0.17f +
                    Mathf.Sin(angle * 5f - anchor.Id * 0.93f) * 0.08f;
                if (offset.magnitude <=
                    anchor.ClearanceRadius * boundaryScale)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsInsideAdvancedTraversalLane(
            Vector2 point,
            float padding)
        {
            if (!enableAdvancedLandforms)
            {
                return false;
            }
            return advancedLandformRouteQuery.TryClosestPointWithin(
                point,
                Mathf.Max(
                    0.5f,
                    AdvancedRouteTerrainHalfWidth + padding),
                out _,
                out _);
        }

        private bool TryResolveAdvancedLandformRoute(
            Vector3 from,
            Vector3 destination,
            out Vector3 entryWaypoint,
            out Vector3 exitWaypoint)
        {
            entryWaypoint = destination;
            exitWaypoint = destination;
            if (advancedLandformConnections.Count == 0)
            {
                return false;
            }

            int sourceRegion = ResolveNearestAdvancedRegionId(ToXZ(from));
            int destinationRegion = ResolveNearestAdvancedRegionId(
                ToXZ(destination));
            if (sourceRegion < 0 ||
                destinationRegion < 0 ||
                sourceRegion == destinationRegion)
            {
                return false;
            }

            if (!TryFindFirstAdvancedConnection(
                    sourceRegion,
                    destinationRegion,
                    out LandformConnection connection,
                    out bool forward))
            {
                return false;
            }
            TryClosestPointOnPolyline(
                ToXZ(from),
                connection.Waypoints,
                out _,
                out float distanceToRoute,
                out float rawProgress);
            float orientedProgress = forward
                ? rawProgress
                : 1f - rawProgress;
            int segmentCount = connection.Waypoints.Length - 1;
            int segmentIndex = distanceToRoute <= connection.Width + 8f
                ? Mathf.Clamp(
                    Mathf.FloorToInt(
                        Mathf.Clamp(orientedProgress, 0f, 0.9999f) *
                        segmentCount),
                    0,
                    segmentCount - 1)
                : 0;
            int entryIndex = forward
                ? segmentIndex
                : connection.Waypoints.Length - 1 - segmentIndex;
            int direction = forward ? 1 : -1;
            const float MinimumCommittedHandoffDistance = 10f;
            Vector2 fromPoint = ToXZ(from);
            Vector2 requestedDestination = ToXZ(destination);
            // Rounded render paths contain deliberately short smoothing
            // segments. Search a small, safe chord of the same authored route
            // that is long enough to commit through the ascent and also makes
            // net progress toward the requested region. This avoids both
            // micro-handoffs and unsafe shortcuts across a river or coastline.
            for (int candidateEntryIndex = entryIndex;
                 candidateEntryIndex >= 0 &&
                 candidateEntryIndex < connection.Waypoints.Length;
                 candidateEntryIndex -= direction)
            {
                Vector2 entry =
                    connection.Waypoints[candidateEntryIndex];
                if (!PathStaysInsideIsland(fromPoint, entry, 0.6f) ||
                    PathTouchesRiverOutsideBridgeLane(
                        fromPoint,
                        entry,
                        CombinedTraversalNavigationPadding))
                {
                    continue;
                }
                float entryDestinationDistance = Vector2.Distance(
                    entry,
                    requestedDestination);
                for (int candidateExitIndex =
                         candidateEntryIndex + direction;
                     candidateExitIndex >= 0 &&
                     candidateExitIndex < connection.Waypoints.Length;
                     candidateExitIndex += direction)
                {
                    Vector2 exit =
                        connection.Waypoints[candidateExitIndex];
                    if (Vector2.Distance(entry, exit) <
                            MinimumCommittedHandoffDistance ||
                        Vector2.Distance(exit, requestedDestination) >=
                            entryDestinationDistance ||
                        PathTouchesRiverOutsideBridgeLane(
                            entry,
                            exit,
                            CombinedTraversalNavigationPadding) ||
                        !PathStaysInsideIsland(entry, exit, 0.6f))
                    {
                        continue;
                    }

                    entryWaypoint = SurfacePoint(
                        new Vector3(entry.x, 0f, entry.y),
                        1f);
                    exitWaypoint = SurfacePoint(
                        new Vector3(exit.x, 0f, exit.y),
                        1f);
                    return true;
                }
            }
            return false;
        }

        private int ResolveNearestAdvancedRegionId(Vector2 point)
        {
            // Topological membership must follow the same nested masks that
            // build terrain. Nearest-center classification assigns base-ground
            // points to distant crowns and can send an agent down an unrelated
            // branch, especially when the broad first rise is expanded.
            int bestId = 0;
            LandformTier bestTier = LandformTier.Lowland;
            float bestInfluence = 0f;
            for (int index = 1;
                 index < advancedLandformRegions.Count;
                 index++)
            {
                LandformRegion region = advancedLandformRegions[index];
                float influence = AdvancedRegionInfluence(region, point);
                if (influence < 0.5f ||
                    region.Tier < bestTier ||
                    (region.Tier == bestTier &&
                     influence <= bestInfluence))
                {
                    continue;
                }
                bestTier = region.Tier;
                bestInfluence = influence;
                bestId = region.Id;
            }
            return bestId;
        }

        private bool TryFindFirstAdvancedConnection(
            int sourceRegion,
            int destinationRegion,
            out LandformConnection firstConnection,
            out bool forward)
        {
            firstConnection = null;
            forward = true;
            var queue = new Queue<int>();
            var visited = new bool[advancedLandformRegions.Count];
            var previousConnection = new int[advancedLandformRegions.Count];
            var previousRegion = new int[advancedLandformRegions.Count];
            for (int index = 0; index < previousConnection.Length; index++)
            {
                previousConnection[index] = -1;
                previousRegion[index] = -1;
            }
            queue.Enqueue(sourceRegion);
            visited[sourceRegion] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == destinationRegion)
                {
                    break;
                }
                for (int index = 0;
                     index < advancedLandformConnections.Count;
                     index++)
                {
                    LandformConnection candidate =
                        advancedLandformConnections[index];
                    int next = candidate.SourceRegionId == current
                        ? candidate.DestinationRegionId
                        : candidate.DestinationRegionId == current
                            ? candidate.SourceRegionId
                            : -1;
                    if (next < 0 || visited[next])
                    {
                        continue;
                    }
                    visited[next] = true;
                    previousConnection[next] = index;
                    previousRegion[next] = current;
                    queue.Enqueue(next);
                }
            }
            if (!visited[destinationRegion])
            {
                return false;
            }

            int cursor = destinationRegion;
            int firstIndex = -1;
            int nextRegion = destinationRegion;
            while (previousRegion[cursor] >= 0)
            {
                firstIndex = previousConnection[cursor];
                nextRegion = cursor;
                if (previousRegion[cursor] == sourceRegion)
                {
                    break;
                }
                cursor = previousRegion[cursor];
            }
            if (firstIndex < 0)
            {
                return false;
            }
            firstConnection = advancedLandformConnections[firstIndex];
            forward = firstConnection.SourceRegionId == sourceRegion &&
                firstConnection.DestinationRegionId == nextRegion;
            return true;
        }

        private void CreateAdvancedInlandCliffs(Material terrainMaterial)
        {
            if (!enableAdvancedLandforms ||
                terrainMaterial == null ||
                advancedLandformRegions.Count == 0)
            {
                return;
            }

            var vertices = new List<Vector3>(2048);
            var uv = new List<Vector2>(2048);
            var colors = new List<Color>(2048);
            var triangles = new List<int>(4096);
            for (int regionIndex = 0;
                 regionIndex < advancedLandformRegions.Count;
                 regionIndex++)
            {
                LandformRegion region = advancedLandformRegions[regionIndex];
                if (region.Tier == LandformTier.Lowland)
                {
                    continue;
                }
                for (int sample = 0; sample < AdvancedCliffSamples; sample++)
                {
                    float angleA = sample * Mathf.PI * 2f /
                        AdvancedCliffSamples;
                    float angleB = (sample + 1) * Mathf.PI * 2f /
                        AdvancedCliffSamples;
                    Vector2 edgeA = AdvancedRegionBoundaryPoint(region, angleA);
                    Vector2 edgeB = AdvancedRegionBoundaryPoint(region, angleB);
                    Vector2 middle = (edgeA + edgeB) * 0.5f;
                    if (IsInsideAdvancedTraversalLane(
                            middle,
                            AdvancedCliffGapPadding) ||
                        IsInsideAdvancedScenicClearance(middle))
                    {
                        continue;
                    }

                    Vector2 outwardA = (edgeA - region.Center).normalized;
                    Vector2 outwardB = (edgeB - region.Center).normalized;
                    Vector2 topA2 = edgeA - outwardA * 1.3f;
                    Vector2 topB2 = edgeB - outwardB * 1.3f;
                    Vector2 bottomA2 = edgeA + outwardA * 2.1f;
                    Vector2 bottomB2 = edgeB + outwardB * 2.1f;
                    float topAHeight = TerrainHeight(topA2.x, topA2.y) - 0.18f;
                    float topBHeight = TerrainHeight(topB2.x, topB2.y) - 0.18f;
                    float bottomAHeight = TerrainHeight(bottomA2.x, bottomA2.y) +
                        0.12f;
                    float bottomBHeight = TerrainHeight(bottomB2.x, bottomB2.y) +
                        0.12f;
                    if (Mathf.Max(
                            topAHeight - bottomAHeight,
                            topBHeight - bottomBHeight) < 2.4f)
                    {
                        continue;
                    }

                    Vector2 middleA2 = Vector2.Lerp(topA2, bottomA2, 0.56f) +
                        outwardA * (0.35f + 0.25f *
                            Mathf.Sin(sample * 1.71f + region.Id));
                    Vector2 middleB2 = Vector2.Lerp(topB2, bottomB2, 0.56f) +
                        outwardB * (0.35f + 0.25f *
                            Mathf.Sin((sample + 1) * 1.71f + region.Id));
                    int first = vertices.Count;
                    vertices.Add(new Vector3(topA2.x, topAHeight, topA2.y));
                    vertices.Add(new Vector3(topB2.x, topBHeight, topB2.y));
                    vertices.Add(new Vector3(
                        middleA2.x,
                        Mathf.Lerp(topAHeight, bottomAHeight, 0.48f),
                        middleA2.y));
                    vertices.Add(new Vector3(
                        middleB2.x,
                        Mathf.Lerp(topBHeight, bottomBHeight, 0.48f),
                        middleB2.y));
                    vertices.Add(new Vector3(
                        bottomA2.x,
                        bottomAHeight,
                        bottomA2.y));
                    vertices.Add(new Vector3(
                        bottomB2.x,
                        bottomBHeight,
                        bottomB2.y));
                    for (int vertex = 0; vertex < 6; vertex++)
                    {
                        Vector3 value = vertices[first + vertex];
                        uv.Add(new Vector2(
                            (sample + (vertex % 2)) * 0.45f,
                            value.y / 8f));
                        colors.Add(new Color(1f, 1f, 1f, 0f));
                    }
                    AddDoubleSidedQuad(triangles, first, first + 1, first + 2, first + 3);
                    AddDoubleSidedQuad(triangles, first + 2, first + 3, first + 4, first + 5);
                }
            }
            if (vertices.Count == 0)
            {
                return;
            }

            Mesh mesh = TrackRuntimeResource(new Mesh
            {
                name = "Advanced Inland Cliff Mesh",
                indexFormat = IndexFormat.UInt32
            });
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject root = new GameObject("Advanced Inland Cliffs");
            root.transform.SetParent(generatedRoot, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            // Keep the temporary tier faces deliberately untextured. The
            // terrain's top-projected habitat maps were visibly streaking
            // down these steep sides; final rock detailing can replace this
            // neutral low-poly treatment later.
            Shader cliffShader = Shader.Find(
                "Universal Render Pipeline/Lit");
            Material material = TrackRuntimeResource(
                cliffShader != null
                    ? new Material(cliffShader)
                    : new Material(terrainMaterial));
            material.name = "Advanced Inland Cliff Material";
            Color cliffColor = new Color(0.29f, 0.31f, 0.28f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", cliffColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", cliffColor);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            root.AddComponent<MeshCollider>().sharedMesh = mesh;
            CreateAdvancedTalus(root.transform);
        }

        private void CreateAdvancedTalus(Transform cliffRoot)
        {
            if (rockPrefabs == null || rockPrefabs.Length == 0)
            {
                return;
            }
            Transform talusRoot = new GameObject("Talus Fields").transform;
            talusRoot.SetParent(cliffRoot, false);
            for (int regionIndex = 0;
                 regionIndex < advancedLandformRegions.Count;
                 regionIndex++)
            {
                LandformRegion region = advancedLandformRegions[regionIndex];
                if (region.Tier == LandformTier.Lowland)
                {
                    continue;
                }
                for (int sample = 2;
                     sample < AdvancedCliffSamples;
                     sample += 8)
                {
                    float angle = sample * Mathf.PI * 2f /
                        AdvancedCliffSamples;
                    Vector2 edge = AdvancedRegionBoundaryPoint(region, angle);
                    if (IsInsideAdvancedTraversalLane(edge, 6f) ||
                        IsInsideAdvancedScenicClearance(edge))
                    {
                        continue;
                    }
                    Vector2 outward = (edge - region.Center).normalized;
                    Vector2 point = edge + outward *
                        (3.1f + 1.2f * Mathf.Sin(sample * 2.17f));
                    if (!IsInsideIsland(point, 3f))
                    {
                        continue;
                    }
                    int prefabIndex = Mathf.Abs(
                        Seed * 31 + region.Id * 17 + sample * 7) %
                        rockPrefabs.Length;
                    GameObject prefab = rockPrefabs[prefabIndex];
                    if (prefab == null)
                    {
                        continue;
                    }
                    GameObject rock = Instantiate(prefab, talusRoot);
                    rock.name = $"Talus {region.Id}-{sample}";
                    float scale = 0.72f +
                        Mathf.Repeat(
                            region.Id * 0.37f + sample * 0.113f,
                            1f) * 0.78f;
                    rock.transform.SetPositionAndRotation(
                        new Vector3(
                            point.x,
                            TerrainMeshHeight(point.x, point.y) - 0.06f,
                            point.y),
                        Quaternion.Euler(
                            0f,
                            Mathf.Repeat(sample * 137.5f, 360f),
                            Mathf.Lerp(-12f, 12f,
                                Mathf.Repeat(sample * 0.371f, 1f))));
                    rock.transform.localScale *= scale;
                }
            }
        }

        private void CreateAdvancedWaterfall(Material waterMaterial)
        {
            if (!enableAdvancedLandforms ||
                waterMaterial == null ||
                riverSamples.Count < 2)
            {
                return;
            }
            int bestIndex = -1;
            float bestDrop = 0f;
            for (int index = 0; index < riverSamples.Count - 1; index++)
            {
                Vector2 a = ToXZ(riverSamples[index]);
                Vector2 b = ToXZ(riverSamples[index + 1]);
                float drop = Mathf.Abs(
                    AdvancedLandformHeight(a) - AdvancedLandformHeight(b));
                if (drop > bestDrop)
                {
                    bestDrop = drop;
                    bestIndex = index;
                }
            }
            if (bestIndex < 0 || bestDrop < 2.25f)
            {
                return;
            }

            Vector2 start = ToXZ(riverSamples[bestIndex]);
            Vector2 end = ToXZ(riverSamples[bestIndex + 1]);
            Vector2 center = (start + end) * 0.5f;
            Vector2 direction = (end - start).normalized;
            Vector2 lateral = new Vector2(-direction.y, direction.x);
            float top = Mathf.Max(
                WaterHeight(start.x, start.y),
                WaterHeight(end.x, end.y));
            float bottom = Mathf.Min(
                WaterHeight(start.x, start.y),
                WaterHeight(end.x, end.y));
            float halfWidth = riverHalfWidth * 0.86f;
            var vertices = new[]
            {
                new Vector3(
                    center.x - lateral.x * halfWidth,
                    top,
                    center.y - lateral.y * halfWidth),
                new Vector3(
                    center.x + lateral.x * halfWidth,
                    top,
                    center.y + lateral.y * halfWidth),
                new Vector3(
                    center.x - lateral.x * halfWidth,
                    bottom - 0.15f,
                    center.y - lateral.y * halfWidth),
                new Vector3(
                    center.x + lateral.x * halfWidth,
                    bottom - 0.15f,
                    center.y + lateral.y * halfWidth)
            };
            Mesh mesh = TrackRuntimeResource(new Mesh
            {
                name = "Advanced Waterfall Mesh"
            });
            mesh.vertices = vertices;
            mesh.uv = new[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 1, 3, 2,
                0, 2, 1, 1, 2, 3
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject waterfall = new GameObject("Advanced Gorge Waterfall");
            waterfall.transform.SetParent(generatedRoot, false);
            waterfall.AddComponent<MeshFilter>().sharedMesh = mesh;
            waterfall.AddComponent<MeshRenderer>().sharedMaterial = waterMaterial;
        }

        public Vector3 AdvancedScenicAnchorWorldPosition(ScenicAnchor anchor)
        {
            if (anchor == null)
            {
                return Vector3.zero;
            }
            return new Vector3(
                anchor.Position.x,
                TerrainHeight(anchor.Position.x, anchor.Position.y) + 0.35f,
                anchor.Position.y);
        }

        public void RebuildAdvancedLandformGraphForValidation(
            int seed,
            Vector2 direction)
        {
            enableAdvancedLandforms = true;
            elevationDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.up;
            noiseOffsetA = new Vector2(seed * 0.37f, seed * 0.17f);
            noiseOffsetB = new Vector2(seed * 0.11f, seed * 0.29f);
            ConfigureAdvancedLandformGraph(seed);
        }

        private Vector2 ClampAdvancedRegionCenter(
            Vector2 center,
            float maximumRadius)
        {
            return center.magnitude <= maximumRadius
                ? center
                : center.normalized * maximumRadius;
        }

        private Vector2 AdvancedRegionBoundaryPoint(
            LandformRegion region,
            float angle)
        {
            float brokenEdge = 1f +
                Mathf.Sin(angle * 3f + region.Id * 1.7f) * 0.035f +
                Mathf.Sin(angle * 7f - region.Id * 0.9f) * 0.022f;
            Vector2 local = new Vector2(
                Mathf.Cos(angle) * region.Radii.x * brokenEdge,
                Mathf.Sin(angle) * region.Radii.y * brokenEdge);
            return region.Center + Rotate(
                local,
                region.RotationDegrees * Mathf.Deg2Rad);
        }

        private static Vector2 Rotate(Vector2 value, float radians)
        {
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                value.x * cosine - value.y * sine,
                value.x * sine + value.y * cosine);
        }

        private static float DirectionAngle(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static float PolylineLength(Vector2[] points)
        {
            float length = 0f;
            for (int index = 0; index < points.Length - 1; index++)
            {
                length += Vector2.Distance(points[index], points[index + 1]);
            }
            return length;
        }

        private static bool TryClosestPointOnPolyline(
            Vector2 point,
            Vector2[] line,
            out Vector2 closest,
            out float distance,
            out float progress)
        {
            closest = point;
            distance = float.PositiveInfinity;
            progress = 0f;
            if (line == null || line.Length < 2)
            {
                return false;
            }
            float totalLength = PolylineLength(line);
            float traversed = 0f;
            for (int index = 0; index < line.Length - 1; index++)
            {
                Vector2 segment = line[index + 1] - line[index];
                float segmentLength = segment.magnitude;
                float segmentProgress = segmentLength > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(
                        point - line[index],
                        segment) / (segmentLength * segmentLength))
                    : 0f;
                Vector2 candidate = line[index] + segment * segmentProgress;
                float candidateDistance = Vector2.Distance(point, candidate);
                if (candidateDistance < distance)
                {
                    closest = candidate;
                    distance = candidateDistance;
                    progress = totalLength > 0.0001f
                        ? (traversed + segmentLength * segmentProgress) /
                            totalLength
                        : 0f;
                }
                traversed += segmentLength;
            }
            return true;
        }

        private static Vector2 PointAlongPolyline(
            Vector2[] line,
            float progress)
        {
            if (line == null || line.Length == 0)
            {
                return Vector2.zero;
            }
            if (line.Length == 1)
            {
                return line[0];
            }

            float totalLength = PolylineLength(line);
            float target = Mathf.Clamp01(progress) * totalLength;
            float traversed = 0f;
            for (int index = 0; index < line.Length - 1; index++)
            {
                float segmentLength = Vector2.Distance(
                    line[index],
                    line[index + 1]);
                if (traversed + segmentLength >= target)
                {
                    float segmentProgress = segmentLength > 0.0001f
                        ? (target - traversed) / segmentLength
                        : 0f;
                    return Vector2.Lerp(
                        line[index],
                        line[index + 1],
                        segmentProgress);
                }
                traversed += segmentLength;
            }
            return line[line.Length - 1];
        }

        private static void AddDoubleSidedQuad(
            List<int> triangles,
            int topA,
            int topB,
            int bottomA,
            int bottomB)
        {
            triangles.Add(topA);
            triangles.Add(topB);
            triangles.Add(bottomA);
            triangles.Add(topB);
            triangles.Add(bottomB);
            triangles.Add(bottomA);
            triangles.Add(topA);
            triangles.Add(bottomA);
            triangles.Add(topB);
            triangles.Add(topB);
            triangles.Add(bottomA);
            triangles.Add(bottomB);
        }
    }
}
