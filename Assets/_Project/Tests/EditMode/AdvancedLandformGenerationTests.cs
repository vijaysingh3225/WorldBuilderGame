using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests
{
    public sealed class AdvancedLandformGenerationTests
    {
        private const int ProductionSeed = 20260730;

        [Test]
        public void AdvancedLandformGraphIsDeterministicConnectedAndWalkable()
        {
            GameObject firstObject = new GameObject("First Advanced Graph");
            GameObject secondObject = new GameObject("Second Advanced Graph");
            try
            {
                ProceduralRaidGenerator first =
                    firstObject.AddComponent<ProceduralRaidGenerator>();
                ProceduralRaidGenerator second =
                    secondObject.AddComponent<ProceduralRaidGenerator>();
                Vector2 direction = new Vector2(0.37f, 0.93f).normalized;
                first.RebuildAdvancedLandformGraphForValidation(
                    ProductionSeed,
                    direction);
                second.RebuildAdvancedLandformGraphForValidation(
                    ProductionSeed,
                    direction);

                Assert.That(first.AdvancedLandformsEnabled, Is.True);
                Assert.That(first.AdvancedLandformRegions.Count,
                    Is.InRange(7, 9));
                Assert.That(first.AdvancedLandformConnections.Count,
                    Is.EqualTo(first.AdvancedLandformRegions.Count - 1),
                    "The trail graph must be a sparse branching tree, not a cross-map web.");
                Assert.That(
                    first.AdvancedFirstRiseCoverage,
                    Is.InRange(0.35f, 0.50f),
                    "The first elevated layer must cover a substantial but " +
                    "non-dominant portion of playable land.");
                Assert.That(first.AdvancedScenicAnchors.Count,
                    Is.GreaterThanOrEqualTo(3));
                Assert.That(first.AdvancedLandformRegions.Count,
                    Is.EqualTo(second.AdvancedLandformRegions.Count));
                Assert.That(first.AdvancedLandformConnections.Count,
                    Is.EqualTo(second.AdvancedLandformConnections.Count));

                var degree = new Dictionary<int, int>();
                int highlands = 0;
                int shelves = 0;
                int crowns = 0;
                for (int index = 0;
                     index < first.AdvancedLandformRegions.Count;
                     index++)
                {
                    ProceduralRaidGenerator.LandformRegion a =
                        first.AdvancedLandformRegions[index];
                    ProceduralRaidGenerator.LandformRegion b =
                        second.AdvancedLandformRegions[index];
                    Assert.That(a.Id, Is.EqualTo(b.Id));
                    Assert.That(a.Tier, Is.EqualTo(b.Tier));
                    Assert.That(a.Center.x, Is.EqualTo(b.Center.x).Within(0.0001f));
                    Assert.That(a.Center.y, Is.EqualTo(b.Center.y).Within(0.0001f));
                    Assert.That(a.TargetHeight,
                        Is.EqualTo(b.TargetHeight).Within(0.0001f));
                    Assert.That(a.ParentRegionId,
                        Is.EqualTo(b.ParentRegionId));
                    degree[a.Id] = 0;
                    if (a.Tier == ProceduralRaidGenerator.LandformTier.Highland)
                    {
                        highlands++;
                    }
                    else if (a.Tier ==
                             ProceduralRaidGenerator.LandformTier.MidShelf)
                    {
                        shelves++;
                    }
                    else if (a.Tier ==
                             ProceduralRaidGenerator.LandformTier.Crown)
                    {
                        crowns++;
                    }
                    if (a.Id == 0)
                    {
                        Assert.That(a.ParentRegionId, Is.EqualTo(-1));
                        continue;
                    }
                    Assert.That(a.ParentRegionId, Is.InRange(0, a.Id - 1));
                    ProceduralRaidGenerator.LandformRegion parent =
                        first.AdvancedLandformRegions[a.ParentRegionId];
                    Assert.That(
                        (int)a.Tier,
                        Is.EqualTo((int)parent.Tier + 1),
                        $"{a.Name} must sit exactly one topological layer above its parent.");
                    if (a.ParentRegionId > 0)
                    {
                        Assert.That(a.Radii.x,
                            Is.LessThan(parent.Radii.x * 0.62f));
                        Assert.That(a.Radii.y,
                            Is.LessThan(parent.Radii.y * 0.62f));
                    }
                }
                Assert.That(highlands, Is.EqualTo(3));
                Assert.That(shelves, Is.InRange(1, 2));
                Assert.That(crowns, Is.EqualTo(2));

                for (int index = 0;
                     index < first.AdvancedLandformConnections.Count;
                     index++)
                {
                    ProceduralRaidGenerator.LandformConnection a =
                        first.AdvancedLandformConnections[index];
                    ProceduralRaidGenerator.LandformConnection b =
                        second.AdvancedLandformConnections[index];
                    Assert.That(a.SourceRegionId,
                        Is.EqualTo(b.SourceRegionId));
                    Assert.That(a.DestinationRegionId,
                        Is.EqualTo(b.DestinationRegionId));
                    Assert.That(
                        first.AdvancedLandformRegions[a.DestinationRegionId]
                            .ParentRegionId,
                        Is.EqualTo(a.SourceRegionId),
                        $"{a.Name} must be a parent-to-child branch.");
                    Assert.That(a.Waypoints.Length,
                        Is.EqualTo(b.Waypoints.Length));
                    Assert.That(a.MaxGrade,
                        Is.LessThan(0.85f),
                        $"{a.Name} must stay below the authored pass grade limit.");
                    Assert.That(a.Width, Is.GreaterThanOrEqualTo(7.5f));
                    Assert.That(a.RiseStartProgress,
                        Is.InRange(0.05f, 0.80f));
                    Assert.That(a.RiseEndProgress,
                        Is.GreaterThan(a.RiseStartProgress + 0.19f));
                    AssertRouteIsSimpleAndRounded(a);
                    if (a.SourceRegionId > 0)
                    {
                        ProceduralRaidGenerator.LandformConnection incoming =
                            null;
                        for (int incomingIndex = 0;
                             incomingIndex <
                                first.AdvancedLandformConnections.Count;
                             incomingIndex++)
                        {
                            ProceduralRaidGenerator.LandformConnection candidate =
                                first.AdvancedLandformConnections[incomingIndex];
                            if (candidate.DestinationRegionId ==
                                a.SourceRegionId)
                            {
                                incoming = candidate;
                                break;
                            }
                        }
                        Assert.That(incoming, Is.Not.Null);
                        Assert.That(
                            Vector2.Distance(
                                a.Waypoints[0],
                                incoming.Waypoints[
                                    incoming.Waypoints.Length - 1]),
                            Is.LessThan(0.05f),
                            $"{a.Name} should visibly branch from its parent trail.");
                    }
                    degree[a.SourceRegionId]++;
                    degree[a.DestinationRegionId]++;
                    for (int point = 0; point < a.Waypoints.Length; point++)
                    {
                        Assert.That(a.Waypoints[point].x,
                            Is.EqualTo(b.Waypoints[point].x).Within(0.0001f));
                        Assert.That(a.Waypoints[point].y,
                            Is.EqualTo(b.Waypoints[point].y).Within(0.0001f));
                    }
                }

                Assert.That(
                    first.AdvancedVisibleTrails.Count,
                    Is.EqualTo(shelves),
                    "Graph-only validation should expose only the ascents " +
                    "from the island floor to the first rise.");
                for (int index = 0;
                     index < first.AdvancedVisibleTrails.Count;
                     index++)
                {
                    AssertVisibleRouteIsSimpleAndRounded(
                        first.AdvancedVisibleTrails[index],
                        $"Low-tier trail {index}");
                }

                foreach (ProceduralRaidGenerator.LandformRegion region in
                         first.AdvancedLandformRegions)
                {
                    if (region.Id > 0)
                    {
                        Assert.That(
                            degree[region.Id],
                            Is.GreaterThanOrEqualTo(1),
                            $"{region.Name} needs exactly one reachable parent branch.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void SeededTopologiesAlternateBroadCrescentAndSplitFirstRise()
        {
            int[] seeds = { 20260732, 20260730, 20260731 };
            int[] expectedFirstRiseCounts = { 1, 2, 2 };
            for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                GameObject generatorObject = new GameObject(
                    $"Topology Composition {seedIndex}");
                try
                {
                    ProceduralRaidGenerator generator =
                        generatorObject.AddComponent<ProceduralRaidGenerator>();
                    generator.RebuildAdvancedLandformGraphForValidation(
                        seeds[seedIndex],
                        new Vector2(0.37f, 0.93f).normalized);

                    int firstRiseCount = 0;
                    float strongestNotch = 0f;
                    foreach (ProceduralRaidGenerator.LandformRegion region in
                             generator.AdvancedLandformRegions)
                    {
                        if (region.Tier !=
                            ProceduralRaidGenerator.LandformTier.MidShelf)
                        {
                            continue;
                        }
                        firstRiseCount++;
                        strongestNotch = Mathf.Max(
                            strongestNotch,
                            region.NotchStrength);
                    }
                    Assert.That(
                        firstRiseCount,
                        Is.EqualTo(expectedFirstRiseCounts[seedIndex]));
                    Assert.That(
                        generator.AdvancedFirstRiseCoverage,
                        Is.InRange(0.35f, 0.50f),
                        $"Seed {seeds[seedIndex]} must keep first-rise " +
                        "coverage inside the authored 35-50% range.");
                    Assert.That(
                        generator.AdvancedFirstRiseCoverage,
                        Is.EqualTo(
                            ProceduralRaidGenerator.TargetFirstRiseCoverage)
                            .Within(0.015f));
                    if (firstRiseCount == 1)
                    {
                        Assert.That(
                            strongestNotch,
                            Is.GreaterThanOrEqualTo(0.30f),
                            "The single broad first rise must retain its " +
                            "seeded crescent-shaped opening.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(generatorObject);
                }
            }
        }

        [Test]
        public void ProductionGeneratorDefaultsToAdvancedAndKeepsLegacyFallbackAvailable()
        {
            GameObject generatorObject = new GameObject("Feature Flag Probe");
            try
            {
                ProceduralRaidGenerator generator =
                    generatorObject.AddComponent<ProceduralRaidGenerator>();
                Assert.That(generator.AdvancedLandformsEnabled, Is.True);
                Assert.That(generator.AdvancedLandformRegions, Is.Empty);
                generator.SetAdvancedLandformsEnabled(false);
                Assert.That(generator.AdvancedLandformsEnabled, Is.False);
                generator.SetAdvancedLandformsEnabled(true);
                Assert.That(generator.AdvancedLandformsEnabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(generatorObject);
            }
        }

        [Test]
        public void AdvancedProductionSeedBuildsCliffsRoutesAndScenicAnchors()
        {
            const string ScenePath =
                "Assets/_Project/Scenes/RaidPrototype.unity";
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            Assert.That(generator, Is.Not.Null);
            try
            {
                generator.SetAdvancedLandformsEnabled(true);
                generator.GenerateWithSeed(ProductionSeed);

                Transform generated = generator.transform.Find(
                    $"Generated Raid {ProductionSeed}");
                Assert.That(generated, Is.Not.Null);
                Assert.That(
                    generator.GeneratedTreeCount,
                    Is.EqualTo(generator.GeneratedTreeTarget),
                    "The production seed must fill the complete configured tree budget.");
                Assert.That(
                    generator.GeneratedTreeDensityCoverage,
                    Is.InRange(0.60f, 0.65f));
                Assert.That(
                    generator.GeneratedDenseForestTreeCount,
                    Is.GreaterThan(
                        generator.GeneratedTreeCount * 0.60f));
                Assert.That(
                    generator.GeneratedMediumWoodlandTreeCount,
                    Is.GreaterThan(
                        generator.GeneratedTreeCount * 0.10f));
                AssertForestHasNoDirectionalBias(generator);
                AssertForestBiomesFormFilledBlobs(generator);
                Assert.That(generated.Find("Terrain Island"), Is.Not.Null);
                Transform camps = generated.Find("Forest Camps");
                Assert.That(camps, Is.Not.Null);
                foreach (Light campLight in
                         camps.GetComponentsInChildren<Light>(true))
                {
                    Assert.That(
                        campLight.enabled,
                        Is.False,
                        $"Camp lighting must remain disabled: " +
                        campLight.name);
                }
                foreach (ParticleSystem campParticles in
                         camps.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(
                        campParticles.gameObject.activeSelf,
                        Is.False,
                        $"Camp visual effects must remain disabled: " +
                        campParticles.name);
                }
                Transform cliffs = generated.Find("Advanced Inland Cliffs");
                Assert.That(
                    cliffs,
                    Is.Null,
                    "Advanced elevation must use the terrain mesh only; " +
                    "no inland cliff-face overlay may be generated.");
                Assert.That(generator.AdvancedLandformRegions.Count,
                    Is.InRange(7, 9));
                Assert.That(generator.AdvancedLandformConnections.Count,
                    Is.EqualTo(generator.AdvancedLandformRegions.Count - 1));
                Assert.That(
                    generator.AdvancedFirstRiseCoverage,
                    Is.InRange(0.35f, 0.50f));
                Assert.That(generator.AdvancedVisibleTrails.Count,
                    Is.InRange(6, 8),
                    "The visible network needs two trunks and four to six branches.");
                foreach (ProceduralRaidGenerator.LandformConnection connection in
                         generator.AdvancedLandformConnections)
                {
                    AssertRouteIsSimpleAndRounded(connection);
                    Assert.That(
                        connection.MaxGrade,
                        Is.LessThan(1.1f),
                        "Non-excavating hierarchy approaches may use a " +
                        "short steep shoulder, but may not become vertical.");
                    if (generator.AdvancedLandformRegions[
                            connection.DestinationRegionId].Tier >
                        ProceduralRaidGenerator.LandformTier.MidShelf)
                    {
                        continue;
                    }
                    for (int point = 0;
                         point < connection.Waypoints.Length;
                         point++)
                    {
                        Vector2 xz = connection.Waypoints[point];
                        Assert.That(
                            generator.CurrentLayout.CoastRadiusAtAngle(
                                Mathf.Atan2(xz.y, xz.x)) - xz.magnitude,
                            Is.GreaterThan(2f),
                            $"{connection.Name} left the playable island.");
                    }
                    for (int segment = 0;
                         segment < connection.Waypoints.Length - 1;
                         segment++)
                    {
                        Vector2 start = connection.Waypoints[segment];
                        Vector2 end = connection.Waypoints[segment + 1];
                        int samples = Mathf.Max(
                            2,
                            Mathf.CeilToInt(Vector2.Distance(start, end) / 3f));
                        for (int sample = 0; sample <= samples; sample++)
                        {
                            Vector2 xz = Vector2.Lerp(
                                start,
                                end,
                                sample / (float)samples);
                            if (generator
                                    .AdvancedRiverValleyInfluenceForValidation(
                                        xz) <= 0.001f)
                            {
                                Assert.That(
                                    generator
                                        .AdvancedTerrainOffsetForValidation(
                                            xz),
                                    Is.GreaterThanOrEqualTo(-0.001f),
                                    $"{connection.Name} excavated below " +
                                    "the natural base terrain.");
                            }
                        }
                    }
                }
                foreach (List<Vector3> trail in
                         generator.AdvancedVisibleTrails)
                {
                    AssertVisibleRouteIsSimpleAndRounded(
                        trail,
                        "Production low-tier trail");
                    for (int point = 0; point < trail.Count; point++)
                    {
                        Assert.That(
                            generator.AdvancedTierAtForValidation(
                                new Vector2(trail[point].x, trail[point].z)),
                            Is.LessThanOrEqualTo(
                                ProceduralRaidGenerator.LandformTier.MidShelf),
                            "Visible trails may occupy only the island floor " +
                            "and first elevated layer.");
                        if (point == 0)
                        {
                            continue;
                        }
                        Vector3 previous = trail[point - 1];
                        float horizontalDistance = Vector2.Distance(
                            new Vector2(previous.x, previous.z),
                            new Vector2(trail[point].x, trail[point].z));
                        float grade = Mathf.Abs(
                            trail[point].y - previous.y) /
                            Mathf.Max(0.001f, horizontalDistance);
                        Assert.That(
                            grade,
                            Is.LessThanOrEqualTo(
                                ProceduralRaidGenerator.MaximumTrailGrade +
                                0.001f),
                            "Every visible trail segment must remain below " +
                            "the player's walkable slope limit.");
                    }
                }
                for (int branchIndex = 2;
                     branchIndex < generator.AdvancedVisibleTrails.Count;
                     branchIndex++)
                {
                    List<Vector3> branch =
                        generator.AdvancedVisibleTrails[branchIndex];
                    List<Vector3> parent =
                        generator.AdvancedVisibleTrails[
                            (branchIndex & 1) == 1 ? 1 : 0];
                    Vector3 parentDirection = ClosestRouteDirection(
                        branch[0],
                        parent,
                        out float junctionDistance);
                    Assert.That(
                        junctionDistance,
                        Is.LessThan(0.05f),
                        $"Branch {branchIndex - 2} must remain connected " +
                        "to its primary trail.");
                    Vector3 departure = Vector3.ProjectOnPlane(
                        branch[Mathf.Min(6, branch.Count - 1)] - branch[0],
                        Vector3.up).normalized;
                    float departureAngle = Mathf.Acos(Mathf.Clamp(
                        Mathf.Abs(Vector3.Dot(
                            departure,
                            parentDirection)),
                        -1f,
                        1f));
                    Assert.That(
                        departureAngle,
                        Is.GreaterThanOrEqualTo(0.40f),
                        $"Branch {branchIndex - 2} must leave in a clearly " +
                        "different direction instead of running parallel.");
                }

                Transform river = generated.Find("River");
                Assert.That(river, Is.Not.Null);
                Mesh riverMesh = river.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(riverMesh, Is.Not.Null);
                const int RiverVerticesAcross = 9;
                int riverPointCount =
                    riverMesh.vertexCount / RiverVerticesAcross;
                Assert.That(riverPointCount, Is.GreaterThan(12));
                Vector3[] riverVertices = riverMesh.vertices;
                float minimumInteriorWater = float.PositiveInfinity;
                float maximumInteriorWater = float.NegativeInfinity;
                int firstInterior = Mathf.CeilToInt(riverPointCount * 0.18f);
                int lastInterior = Mathf.FloorToInt(riverPointCount * 0.82f);
                for (int point = firstInterior;
                     point <= lastInterior;
                     point++)
                {
                    float height = riverVertices[
                        point * RiverVerticesAcross +
                        RiverVerticesAcross / 2].y;
                    minimumInteriorWater = Mathf.Min(
                        minimumInteriorWater,
                        height);
                    maximumInteriorWater = Mathf.Max(
                        maximumInteriorWater,
                        height);
                }
                Assert.That(
                    maximumInteriorWater - minimumInteriorWater,
                    Is.LessThan(0.2f),
                    "The advanced river must remain on one lowland datum " +
                    "instead of climbing the tiered terrain.");
                Vector3[] riverCenterline = generator.CurrentLayout.River;
                float riverLength = 0f;
                for (int point = 1; point < riverCenterline.Length; point++)
                {
                    riverLength += Vector3.Distance(
                        riverCenterline[point - 1],
                        riverCenterline[point]);
                }
                float directRiverDistance = Vector3.Distance(
                    riverCenterline[0],
                    riverCenterline[riverCenterline.Length - 1]);
                Assert.That(
                    riverLength / Mathf.Max(1f, directRiverDistance),
                    Is.GreaterThan(1.025f),
                    "The lowland river should visibly wind rather than cross " +
                    "the island as a near-straight line.");
                float minimumRiverHalfWidth = float.PositiveInfinity;
                float maximumRiverHalfWidth = float.NegativeInfinity;
                int nearStraightRun = 0;
                int longestNearStraightRun = 0;
                int bendDirectionChanges = 0;
                float previousTurnSign = 0f;
                for (int point = 0;
                     point < riverCenterline.Length;
                     point++)
                {
                    float width = generator.RiverHalfWidthForValidation(
                        new Vector2(
                            riverCenterline[point].x,
                            riverCenterline[point].z));
                    minimumRiverHalfWidth = Mathf.Min(
                        minimumRiverHalfWidth,
                        width);
                    maximumRiverHalfWidth = Mathf.Max(
                        maximumRiverHalfWidth,
                        width);
                    if (point <= 0 ||
                        point >= riverCenterline.Length - 1)
                    {
                        continue;
                    }
                    Vector2 incoming = new Vector2(
                        riverCenterline[point].x -
                            riverCenterline[point - 1].x,
                        riverCenterline[point].z -
                            riverCenterline[point - 1].z).normalized;
                    Vector2 outgoing = new Vector2(
                        riverCenterline[point + 1].x -
                            riverCenterline[point].x,
                        riverCenterline[point + 1].z -
                            riverCenterline[point].z).normalized;
                    float turn = incoming.x * outgoing.y -
                        incoming.y * outgoing.x;
                    nearStraightRun = Mathf.Abs(turn) < 0.025f
                        ? nearStraightRun + 1
                        : 0;
                    longestNearStraightRun = Mathf.Max(
                        longestNearStraightRun,
                        nearStraightRun);
                    float turnSign = Mathf.Abs(turn) >= 0.025f
                        ? Mathf.Sign(turn)
                        : 0f;
                    if (turnSign != 0f && previousTurnSign != 0f &&
                        turnSign != previousTurnSign)
                    {
                        bendDirectionChanges++;
                    }
                    if (turnSign != 0f)
                    {
                        previousTurnSign = turnSign;
                    }
                }
                Assert.That(
                    longestNearStraightRun,
                    Is.LessThanOrEqualTo(3),
                    "The river may not retain a long ruler-straight interior run.");
                Assert.That(
                    bendDirectionChanges,
                    Is.GreaterThanOrEqualTo(4),
                    "The river should alternate through several readable " +
                    "left/right bends.");
                Assert.That(
                    maximumRiverHalfWidth - minimumRiverHalfWidth,
                    Is.GreaterThan(0.10f),
                    "The visible river width should gently expand and narrow.");
                Assert.That(
                    maximumRiverHalfWidth / minimumRiverHalfWidth,
                    Is.LessThan(1.18f),
                    "River-width variation must stay within a restrained range.");
                Vector3 mouthDirection =
                    (riverCenterline[riverCenterline.Length - 1] -
                     riverCenterline[0]).normalized;
                for (int point = 1;
                     point < riverCenterline.Length;
                     point++)
                {
                    Vector3 segmentDirection =
                        (riverCenterline[point] -
                         riverCenterline[point - 1]).normalized;
                    Assert.That(
                        Vector3.Dot(segmentDirection, mouthDirection),
                        Is.GreaterThan(0.12f),
                        "The river may wave laterally but may not reverse into " +
                        "a U-bend or run back parallel to itself.");
                }
                Assert.That(generator.GeneratedBridgeCount,
                    Is.GreaterThan(0),
                    "The single trail topology still needs a real bridge " +
                    "across the low river corridor.");
                foreach (ProceduralRaidGenerator.ScenicAnchor anchor in
                         generator.AdvancedScenicAnchors)
                {
                    Vector3 world =
                        generator.AdvancedScenicAnchorWorldPosition(anchor);
                    Assert.That(float.IsNaN(world.y), Is.False);
                    Assert.That(anchor.ClearanceRadius,
                        Is.GreaterThanOrEqualTo(10f));
                }
            }
            finally
            {
                // Reload the saved fallback scene rather than leaving the
                // feature-enabled generated hierarchy in the shared suite.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static void AssertRouteIsSimpleAndRounded(
            ProceduralRaidGenerator.LandformConnection connection)
        {
            Vector2[] points = connection.Waypoints;
            Assert.That(points.Length, Is.GreaterThanOrEqualTo(8));
            for (int first = 0; first < points.Length - 1; first++)
            {
                Assert.That(
                    Vector2.Distance(points[first], points[first + 1]),
                    Is.GreaterThan(0.05f),
                    $"{connection.Name} contains a collapsed trail segment.");
                for (int second = first + 2;
                     second < points.Length - 1;
                     second++)
                {
                    if (first == 0 && second == points.Length - 2)
                    {
                        continue;
                    }
                    Assert.That(
                        SegmentsCrossStrictly(
                            points[first],
                            points[first + 1],
                            points[second],
                            points[second + 1]),
                        Is.False,
                        $"{connection.Name} crossed over itself.");
                }
            }
        }

        private static void AssertVisibleRouteIsSimpleAndRounded(
            IReadOnlyList<Vector3> points,
            string routeName)
        {
            Assert.That(points.Count, Is.GreaterThanOrEqualTo(8));
            for (int first = 0; first < points.Count - 1; first++)
            {
                Vector2 a = new Vector2(points[first].x, points[first].z);
                Vector2 b = new Vector2(
                    points[first + 1].x,
                    points[first + 1].z);
                Assert.That(
                    Vector2.Distance(a, b),
                    Is.GreaterThan(0.05f),
                    $"{routeName} contains a collapsed visible segment.");
                for (int second = first + 2;
                     second < points.Count - 1;
                     second++)
                {
                    Vector2 c = new Vector2(
                        points[second].x,
                        points[second].z);
                    Vector2 d = new Vector2(
                        points[second + 1].x,
                        points[second + 1].z);
                    Assert.That(
                        SegmentsCrossStrictly(a, b, c, d),
                        Is.False,
                        $"{routeName} crossed over itself.");
                }
            }
        }

        private static void AssertForestHasNoDirectionalBias(
            ProceduralRaidGenerator generator)
        {
            Vector2 direction = generator.UplandDirection.normalized;
            Vector2 lateral = new Vector2(-direction.y, direction.x);
            float positiveDensity = 0f;
            float negativeDensity = 0f;
            int samples = 0;
            for (int alongIndex = 1; alongIndex <= 7; alongIndex++)
            {
                float along = alongIndex / 8f *
                    generator.MapRadius * 0.72f;
                for (int lateralIndex = -5;
                     lateralIndex <= 5;
                     lateralIndex++)
                {
                    float across = lateralIndex / 5f *
                        generator.MapRadius * 0.48f;
                    positiveDensity += generator.TreeDensityMultiplierAt(
                        direction * along + lateral * across);
                    negativeDensity += generator.TreeDensityMultiplierAt(
                        -direction * along + lateral * across);
                    samples++;
                }
            }
            Assert.That(
                Mathf.Abs(positiveDensity - negativeDensity) /
                    Mathf.Max(1, samples),
                Is.LessThan(0.18f),
                "Forest density must not favor the high or low half of the map.");
        }

        private static void AssertForestBiomesFormFilledBlobs(
            ProceduralRaidGenerator generator)
        {
            const int Resolution = 41;
            var biomes = new ProceduralRaidGenerator.TreeDensityBiome[
                Resolution,
                Resolution];
            var inside = new bool[Resolution, Resolution];
            var counts = new int[3];
            var interiors = new int[3];
            int openSparseEdges = 0;
            int sparseDenseEdges = 0;
            float extent = generator.MapRadius;
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    Vector2 point = new Vector2(
                        Mathf.Lerp(-extent, extent, x / (Resolution - 1f)),
                        Mathf.Lerp(-extent, extent, z / (Resolution - 1f)));
                    float coast = generator.CurrentLayout.CoastRadiusAtAngle(
                        Mathf.Atan2(point.y, point.x));
                    if (point.magnitude > coast - 3f)
                    {
                        continue;
                    }
                    inside[x, z] = true;
                    biomes[x, z] = generator.TreeDensityBiomeAt(point);
                    counts[(int)biomes[x, z]]++;
                }
            }

            for (int z = 1; z < Resolution - 1; z++)
            {
                for (int x = 1; x < Resolution - 1; x++)
                {
                    if (!inside[x, z])
                    {
                        continue;
                    }
                    ProceduralRaidGenerator.TreeDensityBiome biome =
                        biomes[x, z];
                    if (inside[x - 1, z] && inside[x + 1, z] &&
                        inside[x, z - 1] && inside[x, z + 1] &&
                        biomes[x - 1, z] == biome &&
                        biomes[x + 1, z] == biome &&
                        biomes[x, z - 1] == biome &&
                        biomes[x, z + 1] == biome)
                    {
                        interiors[(int)biome]++;
                    }
                    if (inside[x + 1, z])
                    {
                        CountBiomeTransition(
                            biome,
                            biomes[x + 1, z],
                            ref openSparseEdges,
                            ref sparseDenseEdges);
                    }
                    if (inside[x, z + 1])
                    {
                        CountBiomeTransition(
                            biome,
                            biomes[x, z + 1],
                            ref openSparseEdges,
                            ref sparseDenseEdges);
                    }
                }
            }

            for (int biome = 0; biome < counts.Length; biome++)
            {
                Assert.That(counts[biome], Is.GreaterThan(20));
                Assert.That(
                    interiors[biome] / (float)counts[biome],
                    Is.GreaterThan(0.08f),
                    $"{(ProceduralRaidGenerator.TreeDensityBiome)biome} " +
                    "must contain filled interiors rather than contour lines.");
            }
            Assert.That(openSparseEdges, Is.GreaterThan(8));
            Assert.That(sparseDenseEdges, Is.GreaterThan(8));
        }

        private static void CountBiomeTransition(
            ProceduralRaidGenerator.TreeDensityBiome first,
            ProceduralRaidGenerator.TreeDensityBiome second,
            ref int openSparseEdges,
            ref int sparseDenseEdges)
        {
            int minimum = Mathf.Min((int)first, (int)second);
            int maximum = Mathf.Max((int)first, (int)second);
            if (minimum == 0 && maximum == 1)
            {
                openSparseEdges++;
            }
            else if (minimum == 1 && maximum == 2)
            {
                sparseDenseEdges++;
            }
        }

        private static bool ConnectionsShareRegion(
            ProceduralRaidGenerator.LandformConnection a,
            ProceduralRaidGenerator.LandformConnection b)
        {
            return a.SourceRegionId == b.SourceRegionId ||
                a.SourceRegionId == b.DestinationRegionId ||
                a.DestinationRegionId == b.SourceRegionId ||
                a.DestinationRegionId == b.DestinationRegionId;
        }

        private static bool RoutesCrossAwayFromEndpoints(
            IReadOnlyList<Vector3> a,
            IReadOnlyList<Vector3> b)
        {
            for (int first = 0; first < a.Count - 1; first++)
            {
                for (int second = 0;
                     second < b.Count - 1;
                     second++)
                {
                    if (SegmentsCrossStrictly(
                            new Vector2(a[first].x, a[first].z),
                            new Vector2(a[first + 1].x, a[first + 1].z),
                            new Vector2(b[second].x, b[second].z),
                            new Vector2(b[second + 1].x, b[second + 1].z)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static Vector3 ClosestRouteDirection(
            Vector3 point,
            IReadOnlyList<Vector3> route,
            out float distance)
        {
            distance = float.PositiveInfinity;
            Vector3 direction = Vector3.forward;
            Vector2 point2 = new Vector2(point.x, point.z);
            for (int index = 0; index < route.Count - 1; index++)
            {
                Vector2 start = new Vector2(
                    route[index].x,
                    route[index].z);
                Vector2 end = new Vector2(
                    route[index + 1].x,
                    route[index + 1].z);
                Vector2 segment = end - start;
                float progress = segment.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point2 - start, segment) /
                        segment.sqrMagnitude)
                    : 0f;
                float candidateDistance = Vector2.Distance(
                    point2,
                    start + segment * progress);
                if (candidateDistance >= distance)
                {
                    continue;
                }
                distance = candidateDistance;
                direction = new Vector3(
                    segment.x,
                    0f,
                    segment.y).normalized;
            }
            return direction;
        }

        private static float MinimumRouteSeparation(
            IReadOnlyList<Vector3> a,
            IReadOnlyList<Vector3> b)
        {
            float minimum = float.PositiveInfinity;
            for (int first = 0; first < a.Count; first++)
            {
                Vector2 point = new Vector2(a[first].x, a[first].z);
                for (int second = 0; second < b.Count - 1; second++)
                {
                    minimum = Mathf.Min(
                        minimum,
                        DistanceToSegment(
                            point,
                            new Vector2(b[second].x, b[second].z),
                            new Vector2(
                                b[second + 1].x,
                                b[second + 1].z)));
                }
            }
            return minimum;
        }

        private static float DistanceToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float progress = lengthSquared > 0.0001f
                ? Mathf.Clamp01(
                    Vector2.Dot(point - start, segment) /
                    lengthSquared)
                : 0f;
            return Vector2.Distance(
                point,
                start + segment * progress);
        }

        private static bool SegmentsCrossStrictly(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d)
        {
            Vector2 ab = b - a;
            Vector2 cd = d - c;
            float denominator = ab.x * cd.y - ab.y * cd.x;
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                return false;
            }
            Vector2 separation = c - a;
            float first =
                (separation.x * cd.y - separation.y * cd.x) /
                denominator;
            float second =
                (separation.x * ab.y - separation.y * ab.x) /
                denominator;
            const float EndpointEpsilon = 0.015f;
            return first > EndpointEpsilon &&
                first < 1f - EndpointEpsilon &&
                second > EndpointEpsilon &&
                second < 1f - EndpointEpsilon;
        }
    }
}
