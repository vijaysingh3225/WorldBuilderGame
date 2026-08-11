using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests
{
    public sealed class ProceduralShortSwordGeneratorTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SameSeedProducesTheSameCoherentDefinition()
        {
            ProceduralShortSwordDefinition first =
                ProceduralShortSwordGenerator.CreateDefinition(4021);
            ProceduralShortSwordDefinition second =
                ProceduralShortSwordGenerator.CreateDefinition(4021);

            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(second.BladeProfile, Is.EqualTo(first.BladeProfile));
            Assert.That(second.BladeBackStyle, Is.EqualTo(first.BladeBackStyle));
            Assert.That(second.GuardProfile, Is.EqualTo(first.GuardProfile));
            Assert.That(second.GuardConstruction, Is.EqualTo(first.GuardConstruction));
            Assert.That(second.GuardCurveSegments, Is.EqualTo(first.GuardCurveSegments));
            Assert.That(second.GuardCrossSectionSides, Is.EqualTo(first.GuardCrossSectionSides));
            Assert.That(second.GuardCrossSectionRotation, Is.EqualTo(first.GuardCrossSectionRotation));
            Assert.That(second.HandleProfile, Is.EqualTo(first.HandleProfile));
            Assert.That(second.HiltProfile, Is.EqualTo(first.HiltProfile));
            Assert.That(second.MetalFamily, Is.EqualTo(first.MetalFamily));
            Assert.That(second.GripStyle, Is.EqualTo(first.GripStyle));
            Assert.That(second.GripColor, Is.EqualTo(first.GripColor));
            Assert.That(second.OrnamentStyle, Is.EqualTo(first.OrnamentStyle));
            Assert.That(second.GemFamily, Is.EqualTo(first.GemFamily));
            Assert.That(second.GemCut, Is.EqualTo(first.GemCut));
            Assert.That(second.DirectionSign, Is.EqualTo(first.DirectionSign));
            Assert.That(second.BladeLength, Is.EqualTo(first.BladeLength));
            Assert.That(second.BladeWidth, Is.EqualTo(first.BladeWidth));
            Assert.That(second.HandleLength, Is.EqualTo(first.HandleLength));
            Assert.That(second.HiltRadius, Is.EqualTo(first.HiltRadius));
        }

        [Test]
        public void GeneratedSwordHasExactlyFourNamedMeshParts()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1701);

            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(
                generator.GeneratedParts.Select(part => part.name),
                Is.EqualTo(new[]
                {
                    ProceduralShortSwordGenerator.BladePartName,
                    ProceduralShortSwordGenerator.GuardPartName,
                    ProceduralShortSwordGenerator.HandlePartName,
                    ProceduralShortSwordGenerator.HiltPartName
                }));
            Assert.That(
                generator.GeneratedParts.All(part =>
                    part.GetComponent<MeshFilter>()?.sharedMesh != null &&
                    part.GetComponent<MeshRenderer>() != null),
                Is.True);
        }

        [Test]
        public void CrackPreviewPartitionsTheExactBladeSurfaceWithoutExternalPieces()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1701);
            GameObject blade = generator.GeneratedParts.Single(part =>
                part.name == ProceduralShortSwordGenerator.BladePartName);
            float intactArea = SurfaceArea(
                blade.GetComponent<MeshFilter>().sharedMesh);

            int created = generator.CrackBlade();
            Transform[] fracturePieces = blade.GetComponentsInChildren<Transform>()
                .Where(item => item != blade.transform)
                .ToArray();
            Transform[] sections = fracturePieces
                .Where(item => item.name.Contains("Section"))
                .ToArray();
            Transform[] branches = fracturePieces
                .Where(item => item.name.Contains("Branch"))
                .ToArray();

            Assert.That(generator.IsBladeCracked, Is.True);
            Assert.That(generator.FractureRevision, Is.EqualTo(1));
            Assert.That(blade.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(created, Is.EqualTo(fracturePieces.Length));
            Assert.That(sections.Length, Is.InRange(3, 4));
            Assert.That(generator.MainFractureCount, Is.InRange(2, 3));
            Assert.That(generator.MissingFracturePieceCount, Is.InRange(1, 2));
            Assert.That(generator.MinimumFractureSegmentRise, Is.GreaterThan(0.018f));
            Assert.That(
                branches.Length,
                Is.EqualTo(
                    generator.MainFractureCount -
                    generator.MissingFracturePieceCount));
            Assert.That(
                fracturePieces.Any(item => item.name.Contains("Shard")),
                Is.False);
            float remainingArea = fracturePieces.Sum(item =>
                SurfaceArea(item.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(remainingArea, Is.LessThan(intactArea * 0.995f));
            Assert.That(remainingArea, Is.GreaterThan(intactArea * 0.82f));
            Assert.That(
                branches.All(item =>
                    Mathf.Abs(item.localPosition.x) <= 0.0121f),
                Is.True);
            Assert.That(
                fracturePieces.All(item =>
                    Mathf.Abs(item.localPosition.z) <= 0.00001f),
                Is.True,
                "Fractured pieces must remain in the original blade plane.");
            float[] sectionOffsets = sections
                .Select(item => item.localPosition.y)
                .OrderBy(value => value)
                .ToArray();
            float[] sectionGaps = sectionOffsets
                .Skip(1)
                .Select((value, index) => value - sectionOffsets[index])
                .ToArray();
            Assert.That(
                sectionGaps.Max() - sectionGaps.Min(),
                Is.LessThan(0.00001f),
                "Major blade pieces must use even separation spacing.");
            Assert.That(
                fracturePieces.All(item =>
                    item.name.StartsWith(
                        ProceduralShortSwordGenerator.BladeFracturePrefix) &&
                    item.GetComponent<MeshFilter>()?.sharedMesh.vertexCount > 3),
                Is.True);
            Assert.That(
                sections.Select(item => item.localPosition.y).Distinct().Count(),
                Is.EqualTo(sections.Length));
        }

        [Test]
        public void CrackButtonRerollsAndNewSwordRestoresAnIntactBlade()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(4021);
            generator.CrackBlade();
            GameObject blade = generator.GeneratedParts.Single(part =>
                part.name == ProceduralShortSwordGenerator.BladePartName);
            Vector3[] firstPositions = blade.transform.Cast<Transform>()
                .Select(item => item.localPosition)
                .ToArray();

            generator.CrackBlade();
            Vector3[] secondPositions = blade.transform.Cast<Transform>()
                .Select(item => item.localPosition)
                .ToArray();
            Assert.That(generator.FractureRevision, Is.EqualTo(2));
            Assert.That(secondPositions, Is.Not.EqualTo(firstPositions));

            generator.RestoreBlade();
            Assert.That(generator.IsBladeCracked, Is.False);
            Assert.That(blade.transform.childCount, Is.EqualTo(0));
            Assert.That(blade.GetComponent<MeshRenderer>().enabled, Is.True);

            generator.CrackBlade();
            generator.GenerateNext();
            GameObject nextBlade = generator.GeneratedParts.Single(part =>
                part.name == ProceduralShortSwordGenerator.BladePartName);
            Assert.That(generator.IsBladeCracked, Is.False);
            Assert.That(generator.FractureRevision, Is.EqualTo(0));
            Assert.That(nextBlade.transform.childCount, Is.EqualTo(0));
            Assert.That(nextBlade.GetComponent<MeshRenderer>().enabled, Is.True);
        }

        [Test]
        public void FourPartsMeetWithoutVisibleAssemblyGaps()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(2334);

            Bounds blade = BoundsOf(generator, ProceduralShortSwordGenerator.BladePartName);
            Bounds guard = BoundsOf(generator, ProceduralShortSwordGenerator.GuardPartName);
            Bounds handle = BoundsOf(generator, ProceduralShortSwordGenerator.HandlePartName);
            Bounds hilt = BoundsOf(generator, ProceduralShortSwordGenerator.HiltPartName);

            Assert.That(blade.min.y, Is.LessThanOrEqualTo(guard.max.y));
            Assert.That(blade.min.y, Is.GreaterThanOrEqualTo(guard.min.y - 0.02f));
            Assert.That(handle.max.y, Is.GreaterThanOrEqualTo(guard.min.y));
            Assert.That(
                hilt.max.y,
                Is.GreaterThanOrEqualTo(handle.min.y - 0.00001f));
            Assert.That(blade.max.y, Is.GreaterThan(0.90f));
            Assert.That(hilt.min.y, Is.LessThan(-0.30f));
        }

        [Test]
        public void BladeAndHandleSeatInsideTheGuardWithoutCrossingIt()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            foreach (int seed in Enumerable.Range(2300, 512))
            {
                ProceduralShortSwordDefinition sword = generator.Generate(seed);
                Bounds blade = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.BladePartName);
                Bounds guard = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.GuardPartName);
                Bounds handle = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.HandlePartName);
                Bounds hilt = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.HiltPartName);

                for (int sample = 0; sample < 9; sample++)
                {
                    float x = Mathf.Lerp(
                        -sword.BladeWidth * 0.55f,
                        sword.BladeWidth * 0.55f,
                        sample / 8f);
                    ProceduralShortSwordGenerator.ResolveGuardVerticalEnvelopeAtX(
                        sword,
                        x,
                        out float guardBottom,
                        out float guardTop);
                    float bladeSeat =
                        ProceduralShortSwordGenerator.ResolveBladeSeatHeightAtX(
                            sword,
                            x);
                    Assert.That(
                        bladeSeat,
                        Is.GreaterThan(guardBottom),
                        $"Seed {seed} blade passed through the guard bottom at x={x}.");
                    Assert.That(
                        bladeSeat,
                        Is.LessThan(guardTop),
                        $"Seed {seed} blade bottom remained visible above the guard at x={x}.");
                }
                float handleTopRadius =
                    ProceduralShortSwordGenerator.ResolveHandleEndRadius(
                        sword.HandleRadius,
                        sword.HandleProfile,
                        top: true);
                float expectedHandleTop = Enumerable.Range(0, 8)
                    .Select(side =>
                        ProceduralShortSwordGenerator.ResolveHandleSeatHeightAtX(
                            sword,
                            Mathf.Cos(side / 8f * Mathf.PI * 2f) *
                                handleTopRadius))
                    .Append(
                        ProceduralShortSwordGenerator.ResolveHandleSeatHeight(sword))
                    .Max();
                Assert.That(
                    handle.max.y,
                    Is.EqualTo(expectedHandleTop).Within(0.00001f),
                    $"Seed {seed} handle must rise into its fitted guard seat.");
                for (int sample = 0; sample < 9; sample++)
                {
                    float x = Mathf.Lerp(
                        -handleTopRadius,
                        handleTopRadius,
                        sample / 8f);
                    ProceduralShortSwordGenerator.ResolveGuardVerticalEnvelopeAtX(
                        sword,
                        x,
                        out float handleGuardBottom,
                        out float handleGuardTop);
                    float handleSeat = ProceduralShortSwordGenerator
                        .ResolveHandleSeatHeightAtX(sword, x);
                    Assert.That(handleSeat, Is.GreaterThan(handleGuardBottom));
                    Assert.That(handleSeat, Is.LessThan(handleGuardTop));
                }
                Assert.That(blade.min.y, Is.GreaterThan(guard.min.y));
                Assert.That(blade.min.y, Is.LessThan(guard.max.y));
                Assert.That(
                    handle.min.y,
                    Is.EqualTo(-sword.HandleLength).Within(0.00001f));
                Assert.That(
                    hilt.max.y,
                    Is.EqualTo(-sword.HandleLength).Within(0.00001f),
                    $"Seed {seed} pommel must begin at the handle's bottom face.");
            }
        }

        [Test]
        public void VariationStaysInsideShortSwordProportions()
        {
            var definitions = Enumerable.Range(3000, 4096)
                .Select(ProceduralShortSwordGenerator.CreateDefinition)
                .ToArray();

            Assert.That(definitions.Min(value => value.BladeLength), Is.GreaterThanOrEqualTo(0.94f));
            Assert.That(definitions.Max(value => value.BladeLength), Is.LessThanOrEqualTo(1.08f));
            Assert.That(definitions.Min(value => value.BladeWidth), Is.GreaterThanOrEqualTo(0.074f));
            Assert.That(definitions.Max(value => value.BladeWidth), Is.LessThanOrEqualTo(0.112f));
            Assert.That(definitions.Min(value => value.GuardSpan), Is.GreaterThanOrEqualTo(0.255f));
            Assert.That(definitions.Max(value => value.GuardSpan), Is.LessThanOrEqualTo(0.375f));
            Assert.That(definitions.Min(value => value.GuardHeight), Is.GreaterThanOrEqualTo(0.014f));
            Assert.That(definitions.Max(value => value.GuardHeight), Is.LessThanOrEqualTo(0.055f));
            Assert.That(definitions.Select(value => value.BladeProfile).Distinct().Count(), Is.EqualTo(5));
            Assert.That(definitions.Select(value => value.BladeBackStyle).Distinct().Count(), Is.EqualTo(3));
            Assert.That(definitions.Select(value => value.GuardProfile).Distinct().Count(), Is.EqualTo(6));
            Assert.That(definitions.Select(value => value.GuardConstruction).Distinct().Count(), Is.EqualTo(6));
            Assert.That(definitions.Select(value => value.GuardCrossSectionSides).Distinct(), Is.EquivalentTo(new[] { 4, 6, 8, 10, 12 }));
            Assert.That(
                definitions.Select(value => value.GuardCurveSegments).Distinct(),
                Is.EquivalentTo(new[] { 6, 8, 10, 12, 14 }));
            Assert.That(definitions.Select(value => value.HandleProfile).Distinct().Count(), Is.EqualTo(3));
            Assert.That(definitions.Select(value => value.HiltProfile).Distinct().Count(), Is.EqualTo(5));
            Assert.That(definitions.Select(value => value.MetalFamily).Distinct().Count(), Is.EqualTo(4));
            Assert.That(definitions.Select(value => value.GripStyle).Distinct().Count(), Is.EqualTo(4));
            Assert.That(definitions.Select(value => value.OrnamentStyle).Distinct().Count(), Is.EqualTo(3));
            Assert.That(definitions.Select(value => value.GemCut).Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void GuardCrossSectionsSpanBothFlatOrientationsAndIntermediateAngles()
        {
            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(12000, 4096)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            float[] normalizedRotations = swords
                .Select(sword =>
                    sword.GuardCrossSectionRotation /
                    (Mathf.PI / sword.GuardCrossSectionSides))
                .ToArray();

            Assert.That(normalizedRotations.Min(), Is.LessThan(0.03f));
            Assert.That(normalizedRotations.Max(), Is.GreaterThan(0.97f));
            Assert.That(
                normalizedRotations.Count(value => value > 0.35f && value < 0.65f),
                Is.GreaterThan(800));
        }

        [Test]
        public void GuardAndHiltConnectionsAlwaysCoverTheHandleEnds()
        {
            foreach (ProceduralShortSwordDefinition sword in
                     Enumerable.Range(6200, 512)
                         .Select(ProceduralShortSwordGenerator.CreateDefinition))
            {
                float handleTopRadius =
                    ProceduralShortSwordGenerator.ResolveHandleEndRadius(
                        sword.HandleRadius,
                        sword.HandleProfile,
                        top: true);
                float handleBottomRadius =
                    ProceduralShortSwordGenerator.ResolveHandleEndRadius(
                        sword.HandleRadius,
                        sword.HandleProfile,
                        top: false);

                Assert.That(sword.GuardDepth * 0.5f, Is.GreaterThan(handleTopRadius));
                Assert.That(sword.GuardSpan, Is.GreaterThan(handleTopRadius * 2f));
                Assert.That(sword.HiltRadius, Is.GreaterThan(handleBottomRadius));
                Assert.That(
                    ProceduralShortSwordGenerator.ResolveHiltConnectionRadius(
                        sword),
                    Is.GreaterThan(handleBottomRadius));
            }
        }

        [Test]
        public void GuardMassAndStyleAdaptToTheBlade()
        {
            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(8000, 1024)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            ProceduralShortSwordDefinition[] narrow = swords
                .Where(sword => sword.BladeWidth < 0.083f)
                .ToArray();
            ProceduralShortSwordDefinition[] broad = swords
                .Where(sword => sword.BladeWidth > 0.103f)
                .ToArray();

            Assert.That(
                broad.Average(sword => sword.GuardHeight),
                Is.GreaterThan(narrow.Average(sword => sword.GuardHeight) + 0.004f));
            Assert.That(
                broad.Average(sword => sword.GuardSpan),
                Is.GreaterThan(narrow.Average(sword => sword.GuardSpan) + 0.035f));

            Assert.That(
                swords.Where(sword =>
                        sword.BladeProfile != ShortSwordBladeProfile.ForwardSwept &&
                        sword.BladeProfile != ShortSwordBladeProfile.ClipPoint)
                    .All(sword =>
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.DirectionalSweep &&
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.OffsetLeaf),
                Is.True);
            Assert.That(
                swords.Where(sword =>
                        sword.BladeBackStyle == ShortSwordBladeBackStyle.Sawback)
                    .All(sword =>
                        sword.BladeProfile == ShortSwordBladeProfile.ForwardSwept ||
                        sword.BladeProfile == ShortSwordBladeProfile.ClipPoint),
                Is.True);
            Assert.That(
                swords.Where(sword =>
                        sword.GuardConstruction ==
                            ShortSwordGuardConstruction.DirectionalSweep ||
                        sword.GuardConstruction ==
                            ShortSwordGuardConstruction.OffsetLeaf)
                    .All(sword =>
                        sword.BladeProfile == ShortSwordBladeProfile.ForwardSwept ||
                        sword.BladeProfile == ShortSwordBladeProfile.ClipPoint),
                Is.True);
        }

        [Test]
        public void SlantedGuardDropsTowardItsDirectionalBladeTip()
        {
            ProceduralShortSwordDefinition slanted =
                Enumerable.Range(15000, 2048)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .First(sword =>
                        sword.GuardConstruction ==
                            ShortSwordGuardConstruction.DirectionalSweep);
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(slanted.Seed);

            Mesh blade = generator.GeneratedParts.Single(part =>
                    part.name == ProceduralShortSwordGenerator.BladePartName)
                .GetComponent<MeshFilter>()
                .sharedMesh;
            float top = blade.vertices.Max(vertex => vertex.y);
            float tipX = blade.vertices
                .Where(vertex => Mathf.Abs(vertex.y - top) < 0.0001f)
                .Average(vertex => vertex.x);
            Assert.That(Mathf.Sign(tipX), Is.EqualTo(slanted.DirectionSign));

            Mesh guard = generator.GeneratedParts.Single(part =>
                    part.name == ProceduralShortSwordGenerator.GuardPartName)
                .GetComponent<MeshFilter>()
                .sharedMesh;
            float left = guard.vertices.Min(vertex => vertex.x);
            float right = guard.vertices.Max(vertex => vertex.x);
            float leftY = guard.vertices
                .Where(vertex => Mathf.Abs(vertex.x - left) < 0.0001f)
                .Average(vertex => vertex.y);
            float rightY = guard.vertices
                .Where(vertex => Mathf.Abs(vertex.x - right) < 0.0001f)
                .Average(vertex => vertex.y);
            Assert.That(
                slanted.DirectionSign > 0 ? rightY : leftY,
                Is.LessThan(slanted.DirectionSign > 0 ? leftY : rightY));
        }

        [Test]
        public void DirectionalGuardsTurnBackUpAtTheirEnds()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            foreach (ShortSwordGuardConstruction construction in new[]
                     {
                         ShortSwordGuardConstruction.DirectionalSweep,
                         ShortSwordGuardConstruction.OffsetLeaf
                     })
            {
                ProceduralShortSwordDefinition sword =
                    Enumerable.Range(15000, 4096)
                        .Select(ProceduralShortSwordGenerator.CreateDefinition)
                        .First(value => value.GuardConstruction == construction);
                generator.Generate(sword.Seed);
                Mesh guard = generator.GeneratedParts.Single(part =>
                        part.name == ProceduralShortSwordGenerator.GuardPartName)
                    .GetComponent<MeshFilter>()
                    .sharedMesh;
                float[] sideRings = guard.vertices
                    .Select(vertex => vertex.x)
                    .Distinct()
                    .Where(x => Mathf.Sign(x) == sword.DirectionSign)
                    .OrderBy(x => Mathf.Abs(x))
                    .ToArray();
                float tipX = sideRings.Last();
                float shoulderX = sideRings
                    .OrderBy(x => Mathf.Abs(Mathf.Abs(x / tipX) - 0.65f))
                    .First();
                float tipY = guard.vertices
                    .Where(vertex => vertex.x == tipX)
                    .Average(vertex => vertex.y);
                float shoulderY = guard.vertices
                    .Where(vertex => vertex.x == shoulderX)
                    .Average(vertex => vertex.y);

                Assert.That(
                    tipY,
                    Is.GreaterThan(shoulderY + 0.006f),
                    $"{construction} must visibly turn back toward the blade at its tip.");
            }
        }

        [Test]
        public void PreviewZoomMovesTowardCursorAndStaysBounded()
        {
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomFieldOfView(37f, 120f, 0.0015f, 22f, 58f),
                Is.LessThan(37f));
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomFieldOfView(37f, -120f, 0.0015f, 22f, 58f),
                Is.GreaterThan(37f));
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomFieldOfView(37f, 100000f, 0.0015f, 22f, 58f),
                Is.EqualTo(22f));
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomFieldOfView(37f, -100000f, 0.0015f, 22f, 58f),
                Is.EqualTo(58f));
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomFieldOfView(37f, 120f, 0.0045f, 12f, 58f),
                Is.LessThan(23f));
        }

        [Test]
        public void RegenerationReplacesInsteadOfAccumulatingParts()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            ProceduralShortSwordDefinition first = generator.Generate(90);
            ProceduralShortSwordDefinition second = generator.GenerateNext();

            Assert.That(second.Seed, Is.EqualTo(first.Seed + 1));
            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(root.transform.childCount, Is.EqualTo(4));
        }

        [Test]
        public void GenerationRemovesUntrackedOrphanSwordParts()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var orphanBlade = new GameObject(
                ProceduralShortSwordGenerator.BladePartName);
            orphanBlade.transform.SetParent(root.transform, false);
            orphanBlade.AddComponent<MeshFilter>();

            generator.Generate(2104);

            Assert.That(root.transform.childCount, Is.EqualTo(4));
            Assert.That(
                root.transform.Cast<Transform>().Count(child =>
                    child.name == ProceduralShortSwordGenerator.BladePartName),
                Is.EqualTo(1));
            Assert.That(orphanBlade == null, Is.True);
        }

        [Test]
        public void EveryShapeFamilyBuildsValidFiniteMeshes()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            for (int seed = 5100; seed < 5196; seed++)
            {
                generator.Generate(seed);
                foreach (MeshFilter filter in
                         root.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    Assert.That(
                        mesh.vertexCount,
                        Is.GreaterThan(7),
                        $"Seed {seed}, mesh {filter.name}");
                    Assert.That(
                        mesh.triangles.Length,
                        Is.GreaterThan(11),
                        $"Seed {seed}, mesh {filter.name}");
                    Assert.That(float.IsNaN(mesh.bounds.size.x), Is.False);
                    Assert.That(float.IsNaN(mesh.bounds.size.y), Is.False);
                    Assert.That(float.IsNaN(mesh.bounds.size.z), Is.False);
                    Assert.That(mesh.bounds.size.sqrMagnitude, Is.GreaterThan(0.0001f));
                }
            }
        }

        [Test]
        public void EveryTriangleOwnsHardFaceVerticesAndNormals()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1201);

            foreach (MeshFilter filter in
                     root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(
                    mesh.vertexCount,
                    Is.EqualTo(mesh.triangles.Length),
                    $"{filter.name} must not share smoothed vertices between polygon faces.");
                Vector3[] normals = mesh.normals;
                for (int index = 0; index < normals.Length; index += 3)
                {
                    Assert.That(normals[index + 1], Is.EqualTo(normals[index]));
                    Assert.That(normals[index + 2], Is.EqualTo(normals[index]));
                }
            }
        }

        [Test]
        public void CuratedDetailsAvoidJointCollarsAndGuardMatchesPommelMetal()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var seenGrips = new System.Collections.Generic.HashSet<ShortSwordGripStyle>();
            var seenOrnaments = new System.Collections.Generic.HashSet<ShortSwordOrnamentStyle>();
            for (int seed = 7000; seed < 7512; seed++)
            {
                ProceduralShortSwordDefinition sword = generator.Generate(seed);
                seenGrips.Add(sword.GripStyle);
                seenOrnaments.Add(sword.OrnamentStyle);
                Assert.That(
                    root.GetComponentsInChildren<Transform>()
                        .Any(item => item.name == "Guard Collar"),
                    Is.False);
                Assert.That(
                    root.GetComponentsInChildren<Transform>()
                        .Any(item => item.name == "Pommel Connection Ring"),
                    Is.False);
                if (sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem)
                {
                    string[] jewelNames = root.GetComponentsInChildren<Transform>()
                        .Select(item => item.name)
                        .Where(name => name.EndsWith("Guard Jewel"))
                        .ToArray();
                    Assert.That(
                        jewelNames,
                        Is.EquivalentTo(new[]
                        {
                            "Front Guard Jewel",
                            "Rear Guard Jewel"
                        }));
                    Mesh guardMesh = generator.GeneratedParts.Single(
                            part => part.name ==
                                ProceduralShortSwordGenerator.GuardPartName)
                        .GetComponent<MeshFilter>()
                        .sharedMesh;
                    Mesh frontJewel = root.GetComponentsInChildren<MeshFilter>()
                        .Single(filter => filter.name == "Front Guard Jewel")
                        .sharedMesh;
                    Assert.That(
                        frontJewel.bounds.size.y,
                        Is.LessThan(sword.GuardHeight * 0.50f));
                    Assert.That(
                        frontJewel.bounds.min.z,
                        Is.LessThanOrEqualTo(guardMesh.bounds.max.z));
                }

                GameObject guardObject = generator.GeneratedParts.Single(
                    part => part.name == ProceduralShortSwordGenerator.GuardPartName);
                Assert.That(
                    guardObject.transform.childCount,
                    Is.EqualTo(
                        sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem
                            ? 2
                            : 0),
                    $"Seed {seed} must not add non-jewel guard patterns.");

                Renderer guard = generator.GeneratedParts.Single(
                    part => part.name == ProceduralShortSwordGenerator.GuardPartName)
                    .GetComponent<Renderer>();
                Renderer hilt = generator.GeneratedParts.Single(
                    part => part.name == ProceduralShortSwordGenerator.HiltPartName)
                    .GetComponent<Renderer>();
                var guardProperties = new MaterialPropertyBlock();
                var hiltProperties = new MaterialPropertyBlock();
                guard.GetPropertyBlock(guardProperties);
                hilt.GetPropertyBlock(hiltProperties);
                Assert.That(
                    hiltProperties.GetColor("_BaseColor"),
                    Is.EqualTo(guardProperties.GetColor("_BaseColor")));
            }
            Assert.That(seenGrips, Has.Count.EqualTo(4));
            Assert.That(seenOrnaments, Does.Contain(ShortSwordOrnamentStyle.Plain));
            Assert.That(seenOrnaments, Does.Contain(ShortSwordOrnamentStyle.PommelGem));

            ProceduralShortSwordDefinition guardJewelSword =
                Enumerable.Range(10000, 50000)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .First(sword =>
                        sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem);
            generator.Generate(guardJewelSword.Seed);
            GameObject jeweledGuard = generator.GeneratedParts.Single(
                part => part.name == ProceduralShortSwordGenerator.GuardPartName);
            Assert.That(jeweledGuard.transform.childCount, Is.EqualTo(2));
            Assert.That(
                jeweledGuard.transform.Cast<Transform>()
                    .Select(child => child.name),
                Is.EquivalentTo(new[]
                {
                    "Front Guard Jewel",
                    "Rear Guard Jewel"
                }));
        }

        [Test]
        public void JewelsAreRareAndGuardJewelsOnlyUseViableFaces()
        {
            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(10000, 32768)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            ProceduralShortSwordDefinition[] jeweled = swords
                .Where(sword =>
                    sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem ||
                    sword.OrnamentStyle == ShortSwordOrnamentStyle.PommelGem)
                .ToArray();
            float jewelRate = jeweled.Length / (float)swords.Length;

            Assert.That(jewelRate, Is.InRange(0.05f, 0.10f));
            Assert.That(
                swords.Where(sword =>
                        sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem)
                    .All(sword =>
                        sword.GuardHeight >= 0.028f &&
                        sword.GuardSpan >= 0.300f &&
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.DirectionalSweep &&
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.OffsetLeaf),
                Is.True);
            Assert.That(
                jeweled.Select(sword => sword.GemCut).Distinct().Count(),
                Is.EqualTo(5));
        }

        [Test]
        public void BladeUsesUniformFacetBandsInsteadOfLargeSmoothFaces()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1201);
            Mesh blade = generator.GeneratedParts
                .Single(part =>
                    part.name == ProceduralShortSwordGenerator.BladePartName)
                .GetComponent<MeshFilter>()
                .sharedMesh;

            int faceCount = blade.triangles.Length / 3;
            Assert.That(faceCount, Is.InRange(120, 240));
            Assert.That(
                ProceduralShortSwordGenerator.TargetFacetLength,
                Is.InRange(0.045f, 0.060f));
        }

        [Test]
        public void RaidPresentationReplacesLegacyVisualAtRequestedLength()
        {
            root = new GameObject("Legacy Raid Sword");
            GameObject legacyBlade = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            legacyBlade.name = "Pointed Blade";
            legacyBlade.transform.SetParent(root.transform, false);

            RaidShortSwordPresentation presentation =
                RaidShortSwordPresentation.Replace(
                    root.transform,
                    7719,
                    1.6f);

            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.Seed, Is.EqualTo(7719));
            Assert.That(
                root.GetComponentsInChildren<Transform>(true)
                    .Any(part => part.name == "Pointed Blade"),
                Is.False);
            Assert.That(presentation.Generator.HasGeneratedSword, Is.True);
            float displayedLength = presentation.Generator
                .CurrentDefinition.TotalLength *
                presentation.Generator.transform.localScale.y;
            Assert.That(displayedLength, Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(
                presentation.GripCenterHeight,
                Is.EqualTo(
                    RaidShortSwordPresentation.LegacyGripCenterHeight)
                    .Within(0.0001f));
            Assert.That(
                presentation.BladeLength,
                Is.GreaterThan(0.5f));
            Assert.That(presentation.BladeHitbox, Is.Not.Null);
            Assert.That(
                RaidShortSwordPresentation.Replace(root.transform, 77),
                Is.SameAs(presentation));
        }

        [Test]
        public void HandlesAreShorterAndGripDetailsFollowTheirSurface()
        {
            ProceduralShortSwordDefinition[] swords = Enumerable.Range(1, 80)
                .Select(ProceduralShortSwordGenerator.CreateDefinition)
                .ToArray();

            Assert.That(swords.Average(sword => sword.HandleLength),
                Is.LessThan(0.235f));
            foreach (ProceduralShortSwordDefinition sword in swords)
            {
                float middleRadius =
                    ProceduralShortSwordGenerator.ResolveHandleSurfaceRadius(
                        sword,
                        0.5f);
                Assert.That(middleRadius, Is.GreaterThan(0.020f));
                Assert.That(middleRadius, Is.LessThan(0.036f));
            }
        }

        [Test]
        public void CrossWrappedGripUsesOneRaisedCordWithoutSelfOverlap()
        {
            int seed = Enumerable.Range(1, 300).First(candidate =>
                ProceduralShortSwordGenerator.CreateDefinition(candidate)
                    .GripStyle == ShortSwordGripStyle.CrossWrappedCord);
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(seed);
            Transform handle = generator.GeneratedParts.Single(part =>
                    part.name == ProceduralShortSwordGenerator.HandlePartName)
                .transform;

            Assert.That(
                handle.Cast<Transform>()
                    .Count(child => child.name == "Cord Wrap"),
                Is.EqualTo(1));
            Assert.That(
                handle.Cast<Transform>()
                    .Any(child => child.name == "Counter Cord Wrap"),
                Is.False);
        }

        private ProceduralShortSwordGenerator CreateGenerator()
        {
            root = new GameObject("Procedural Sword Test");
            return root.AddComponent<ProceduralShortSwordGenerator>();
        }

        private static Bounds BoundsOf(
            ProceduralShortSwordGenerator generator,
            string partName)
        {
            return generator.GeneratedParts
                .Single(part => part.name == partName)
                .GetComponent<MeshFilter>()
                .sharedMesh.bounds;
        }

        private static float SurfaceArea(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float area = 0f;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return area;
        }
    }
}
