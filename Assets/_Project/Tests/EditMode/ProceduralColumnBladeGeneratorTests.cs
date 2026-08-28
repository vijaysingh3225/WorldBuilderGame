using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.EditMode
{
    // Focused coverage for the independent Column Blade generator boundary.
    [Category("ColumnBlade")]
    public sealed class ProceduralColumnBladeGeneratorTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DefinitionIsDeterministicAndMaterialDoesNotReshuffleForm()
        {
            ProceduralColumnBladeDefinition stone =
                ProceduralColumnBladeGenerator.CreateDefinition(
                    8061,
                    ColumnBladeMaterial.Stone);
            ProceduralColumnBladeDefinition repeated =
                ProceduralColumnBladeGenerator.CreateDefinition(
                    8061,
                    ColumnBladeMaterial.Stone);
            ProceduralColumnBladeDefinition obsidian =
                ProceduralColumnBladeGenerator.CreateDefinition(
                    8061,
                    ColumnBladeMaterial.Obsidian);

            Assert.That(repeated.BladeLength, Is.EqualTo(stone.BladeLength));
            Assert.That(repeated.BladeWidth, Is.EqualTo(stone.BladeWidth));
            Assert.That(repeated.SectionProfile, Is.EqualTo(stone.SectionProfile));
            Assert.That(repeated.EdgeStyle, Is.EqualTo(stone.EdgeStyle));
            Assert.That(repeated.GuardProfile, Is.EqualTo(stone.GuardProfile));
            Assert.That(obsidian.BladeMaterial, Is.EqualTo(ColumnBladeMaterial.Obsidian));
            Assert.That(obsidian.BladeLength, Is.EqualTo(stone.BladeLength));
            Assert.That(obsidian.BladeWidth, Is.EqualTo(stone.BladeWidth));
            Assert.That(obsidian.BladeThickness, Is.EqualTo(stone.BladeThickness));
            Assert.That(obsidian.AccentPalette, Is.EqualTo(stone.AccentPalette));
            Assert.That(obsidian.SectionProfile, Is.EqualTo(stone.SectionProfile));
            Assert.That(obsidian.EdgeStyle, Is.EqualTo(stone.EdgeStyle));
            Assert.That(obsidian.GuardProfile, Is.EqualTo(stone.GuardProfile));
        }

        [Test]
        public void BladeMaterialIsRandomUntilExplicitlyLocked()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            Assert.That(generator.SelectedBladeMaterial, Is.Null);
            Assert.That(generator.IsBladeMaterialLocked, Is.False);

            var generatedMaterials = new HashSet<ColumnBladeMaterial>();
            foreach (int seed in Enumerable.Range(1, 128))
            {
                ProceduralColumnBladeDefinition definition =
                    generator.Generate(seed);
                generatedMaterials.Add(definition.BladeMaterial);
                Assert.That(
                    definition.BladeMaterial,
                    Is.EqualTo(
                        ProceduralColumnBladeGenerator.ResolveBladeMaterial(
                            seed)));
            }
            Assert.That(generatedMaterials,
                Is.EquivalentTo(new[]
                {
                    ColumnBladeMaterial.Stone,
                    ColumnBladeMaterial.Wood,
                    ColumnBladeMaterial.Obsidian
                }));

            generator.ToggleBladeMaterialLock(
                ColumnBladeMaterial.Wood,
                regenerateCurrent: false);
            Assert.That(generator.SelectedBladeMaterial,
                Is.EqualTo(ColumnBladeMaterial.Wood));
            Assert.That(generator.Generate(9017).BladeMaterial,
                Is.EqualTo(ColumnBladeMaterial.Wood));

            generator.ToggleBladeMaterialLock(
                ColumnBladeMaterial.Wood,
                regenerateCurrent: false);
            Assert.That(generator.SelectedBladeMaterial, Is.Null);
            Assert.That(generator.Generate(9017).BladeMaterial,
                Is.EqualTo(
                    ProceduralColumnBladeGenerator.ResolveBladeMaterial(
                        9017)));
        }

        [Test]
        public void TextureTransformIsSeededDeterministicAndMaterialIndependent()
        {
            ColumnBladeTextureTransform first =
                ProceduralColumnBladeGenerator.ResolveTextureTransform(8061);
            ColumnBladeTextureTransform repeated =
                ProceduralColumnBladeGenerator.ResolveTextureTransform(8061);
            ColumnBladeTextureTransform next =
                ProceduralColumnBladeGenerator.ResolveTextureTransform(8062);

            Assert.That(repeated.Scale, Is.EqualTo(first.Scale));
            Assert.That(repeated.Offset, Is.EqualTo(first.Offset));
            Assert.That(next.AsShaderVector, Is.Not.EqualTo(first.AsShaderVector));
            Assert.That(first.Scale.x, Is.InRange(0.18f, 0.32f));
            Assert.That(first.Scale.y, Is.EqualTo(first.Scale.x));
            Assert.That(
                first.Offset.x,
                Is.InRange(0f, 1f - first.Scale.x));
            Assert.That(
                first.Offset.y,
                Is.InRange(0f, 1f - first.Scale.y));
        }

        [Test]
        public void StoneBladeUsesOneFlatTexturedSurface()
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(
                ShortSwordGeneratorLabSceneBuilder.ColumnBladeStoneMaterialPath);
            Texture2D stoneTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ShortSwordGeneratorLabSceneBuilder.ColumnBladeStoneTexturePath);
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ConfigureMaterials(stone, stone, stone, stone, stone);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.None);

            ProceduralColumnBladeDefinition definition =
                generator.Generate(5197);
            GameObject bladePart = BladePart(generator);
            Mesh mesh = bladePart.GetComponent<MeshFilter>().sharedMesh;
            Renderer renderer = bladePart.GetComponent<Renderer>();

            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(renderer.sharedMaterial, Is.SameAs(stone));
            Assert.That(
                renderer.sharedMaterial.GetTexture("_BaseMap"),
                Is.SameAs(stoneTexture));
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(
                properties.GetTexture("_BaseMap"),
                Is.Not.SameAs(Texture2D.whiteTexture),
                "Stone must retain its atlas instead of a flat-color override.");

            int broadFaceVertices = 0;
            int unchangedBroadFaceVertices = 0;
            for (int index = 0; index < mesh.vertexCount; index++)
            {
                if (Mathf.Abs(mesh.normals[index].z) < 0.999f)
                {
                    continue;
                }
                broadFaceVertices++;
                Assert.That(
                    Mathf.Abs(mesh.vertices[index].z),
                    Is.LessThanOrEqualTo(
                        definition.BladeThickness * 0.5f + 0.00001f),
                    "A chip must cut inward rather than add face relief.");
                if (Mathf.Abs(
                        Mathf.Abs(mesh.vertices[index].z) -
                        definition.BladeThickness * 0.5f) < 0.00001f)
                {
                    unchangedBroadFaceVertices++;
                }
            }
            Assert.That(broadFaceVertices, Is.GreaterThan(0));
            Assert.That(
                unchangedBroadFaceVertices,
                Is.GreaterThan(broadFaceVertices * 0.82f),
                "Chips must stay in narrow edge bands instead of creasing the broad face.");
        }

        [Test]
        public void StoneChipsAreDeterministicAsymmetricAndReachMultipleEdges()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(
                ColumnBladeMaterial.Stone,
                regenerateCurrent: false);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.None);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(5197);
            Mesh first = BladePart(generator)
                .GetComponent<MeshFilter>()
                .sharedMesh;
            Vector3[] firstVertices = first.vertices.ToArray();
            float bottom = first.bounds.min.y;
            float top = first.bounds.max.y;
            Vector3[] chipVertices = firstVertices
                .Where(vertex =>
                    vertex.y > bottom + definition.BladeLength * 0.12f &&
                    vertex.y < top - definition.BladeLength * 0.08f)
                .ToArray();
            int irregularRings = chipVertices
                .GroupBy(vertex => Mathf.RoundToInt(vertex.y * 100000f))
                .Count(group => group.Select(vertex =>
                    new Vector2(vertex.x, vertex.z)).Distinct().Count() > 4);

            Assert.That(
                irregularRings,
                Is.GreaterThanOrEqualTo(definition.StoneChipCount),
                "Each chip should introduce its own perimeter transition ring.");
            HashSet<Vector2> baseline = firstVertices
                .Where(vertex => Mathf.Abs(vertex.y - bottom) < 0.00001f)
                .Select(vertex => new Vector2(vertex.x, vertex.z))
                .ToHashSet();
            IGrouping<int, Vector3>[] rings = chipVertices
                .GroupBy(vertex => Mathf.RoundToInt(vertex.y * 100000f))
                .ToArray();
            IGrouping<int, Vector3>[] orderedRings = rings
                .OrderBy(ring => ring.Key)
                .ToArray();
            var chipHalfHeights = new List<float>();
            for (int ringIndex = 1;
                 ringIndex < orderedRings.Length - 1;
                 ringIndex++)
            {
                int movedCorners = orderedRings[ringIndex]
                    .Select(vertex => new Vector2(vertex.x, vertex.z))
                    .Distinct()
                    .Count(point => !baseline.Contains(point));
                if (movedCorners != 1)
                {
                    continue;
                }
                chipHalfHeights.Add(Mathf.Min(
                    orderedRings[ringIndex].Key -
                        orderedRings[ringIndex - 1].Key,
                    orderedRings[ringIndex + 1].Key -
                        orderedRings[ringIndex].Key));
            }
            Vector2[] damagedCorners = rings
                .Select(ring => ring
                    .Select(vertex => new Vector2(vertex.x, vertex.z))
                    .Distinct()
                    .Where(point => !baseline.Contains(point))
                    .ToArray())
                .Where(points => points.Length > 0)
                .SelectMany(points => points)
                .ToArray();
            Assert.That(
                rings.Count(ring => ring
                    .Select(vertex => new Vector2(vertex.x, vertex.z))
                    .Distinct()
                    .Count(point => !baseline.Contains(point)) == 1),
                Is.EqualTo(definition.StoneChipCount),
                "Each event should move one longitudinal corner only.");
            Assert.That(chipHalfHeights, Has.Count.EqualTo(
                definition.StoneChipCount));
            Assert.That(
                chipHalfHeights.Max(),
                Is.GreaterThan(chipHalfHeights.Min() * 4f),
                "Every blade should visibly mix short and long chips.");
            Assert.That(
                rings.All(ring => ring
                    .Select(vertex => new Vector2(vertex.x, vertex.z))
                    .Distinct()
                    .Count(point => !baseline.Contains(point)) <= 1),
                Is.True,
                "No chip may pull a strip across a flat face.");
            Assert.That(
                damagedCorners.Any(point => point.x < 0f) &&
                damagedCorners.Any(point => point.x > 0f) &&
                damagedCorners.Any(point => point.y < 0f) &&
                damagedCorners.Any(point => point.y > 0f),
                Is.True,
                "Damage should be distributed around independent meeting edges.");
            Assert.That(
                firstVertices.Any(vertex =>
                    firstVertices.All(other =>
                        Mathf.Abs(other.x + vertex.x) > 0.00001f ||
                        Mathf.Abs(other.y - vertex.y) > 0.00001f ||
                        Mathf.Abs(other.z - vertex.z) > 0.00001f)),
                Is.True,
                "Stone chips should break bilateral symmetry.");

            generator.Generate(5197);
            Mesh repeated = BladePart(generator)
                .GetComponent<MeshFilter>()
                .sharedMesh;
            Assert.That(repeated.vertices, Is.EqualTo(firstVertices));
        }

        [TestCase(
            ColumnBladeMaterial.Stone,
            ShortSwordGeneratorLabSceneBuilder.ColumnBladeStoneTexturePath,
            ShortSwordGeneratorLabSceneBuilder.ColumnBladeStoneMaterialPath)]
        [TestCase(
            ColumnBladeMaterial.Wood,
            ShortSwordGeneratorLabSceneBuilder.ColumnBladeWoodTexturePath,
            ShortSwordGeneratorLabSceneBuilder.ColumnBladeWoodMaterialPath)]
        [TestCase(
            ColumnBladeMaterial.Obsidian,
            ShortSwordGeneratorLabSceneBuilder.ColumnBladeObsidianTexturePath,
            ShortSwordGeneratorLabSceneBuilder.ColumnBladeObsidianMaterialPath)]
        public void ImportedBladeMaterialUsesMatchingTexture(
            ColumnBladeMaterial bladeMaterial,
            string texturePath,
            string materialPath)
        {
            Texture2D expected =
                AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            Assert.That(expected, Is.Not.Null, bladeMaterial.ToString());
            Assert.That(material, Is.Not.Null, bladeMaterial.ToString());
            Texture actual = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.mainTexture;
            Assert.That(actual, Is.SameAs(expected), bladeMaterial.ToString());
            TextureImporter importer =
                AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, bladeMaterial.ToString());
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(
                importer.npotScale,
                Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [TestCase(ColumnBladeMaterial.Stone)]
        [TestCase(ColumnBladeMaterial.Wood)]
        [TestCase(ColumnBladeMaterial.Obsidian)]
        public void GeneratorBuildsFourSeatedHardEdgedParts(
            ColumnBladeMaterial material)
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(material, regenerateCurrent: false);

            ProceduralColumnBladeDefinition definition = generator.Generate(5197);

            Assert.That(generator.HasGeneratedSword, Is.True);
            Assert.That(definition.BladeMaterial, Is.EqualTo(material));
            Assert.That(
                generator.GeneratedParts.Select(part => part.name),
                Is.EquivalentTo(new[]
                {
                    ProceduralColumnBladeGenerator.BladePartName,
                    ProceduralColumnBladeGenerator.GuardPartName,
                    ProceduralColumnBladeGenerator.HandlePartName,
                    ProceduralColumnBladeGenerator.PommelPartName
                }));
            Assert.That(
                generator.GeneratedParts.All(part =>
                    part.GetComponent<MeshFilter>()?.sharedMesh != null &&
                    part.GetComponent<MeshRenderer>() != null),
                Is.True);

            Bounds blade = BoundsOf(
                generator,
                ProceduralColumnBladeGenerator.BladePartName);
            Bounds guard = BoundsOf(
                generator,
                ProceduralColumnBladeGenerator.GuardPartName);
            Bounds handle = BoundsOf(
                generator,
                ProceduralColumnBladeGenerator.HandlePartName);
            Bounds pommel = BoundsOf(
                generator,
                ProceduralColumnBladeGenerator.PommelPartName);
            Assert.That(blade.min.y, Is.LessThan(guard.max.y));
            Assert.That(handle.max.y, Is.GreaterThan(guard.min.y));
            Assert.That(
                pommel.max.y,
                Is.GreaterThanOrEqualTo(handle.min.y - 0.001f));
            Assert.That(guard.size.x, Is.GreaterThan(blade.size.x));
            Assert.That(guard.size.z, Is.GreaterThan(blade.size.z));
            Assert.That(
                blade.size.x,
                Is.EqualTo(definition.BladeWidth).Within(0.00001f));
            Assert.That(
                blade.size.y,
                Is.EqualTo(definition.BladeLength).Within(0.00001f));
            Assert.That(
                blade.size.z,
                Is.EqualTo(definition.BladeThickness).Within(0.00001f));
            Assert.That(
                guard.size.x,
                Is.EqualTo(definition.GuardWidth).Within(0.00001f));
            Assert.That(
                guard.size.y,
                Is.EqualTo(definition.GuardHeight).Within(0.00001f));
            Assert.That(
                guard.size.z,
                Is.EqualTo(definition.GuardDepth).Within(0.00001f));
            Assert.That(
                guard.size.x - blade.size.x,
                Is.EqualTo(guard.size.z - blade.size.z).Within(0.00001f),
                "The top-down guard must keep equal clearance around X and Z.");
            Assert.That(
                (guard.size.x - blade.size.x) * 0.5f,
                Is.LessThanOrEqualTo(0.02541f),
                "Guard clearance must remain within one inch per side.");
            GameObject bladePart = generator.GeneratedParts.Single(part =>
                part.name == ProceduralColumnBladeGenerator.BladePartName);
            Assert.That(
                bladePart.transform.childCount,
                Is.EqualTo(definition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.StraightLine
                        ? definition.EngravingTermination ==
                            ColumnBladeEngravingTermination.Circle
                                ? 1
                                : 1
                        : 0),
                "Circle engravings add one connected floor beside the line inlay.");

            foreach (MeshFilter filter in
                     root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), filter.name);
                Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
                foreach (Vector3 normal in mesh.normals)
                {
                    Assert.That(IsFinite(normal), Is.True, filter.name);
                    Assert.That(
                        normal.sqrMagnitude,
                        Is.EqualTo(1f).Within(0.00001f),
                        filter.name);
                }
                if (filter.name != ProceduralColumnBladeGenerator.BladePartName &&
                    filter.name != ProceduralColumnBladeGenerator.GuardPartName)
                {
                    continue;
                }
                Assert.That(mesh.uv, Has.Length.EqualTo(mesh.vertexCount));
                foreach (Vector2 uv in mesh.uv)
                {
                    Assert.That(float.IsNaN(uv.x), Is.False, filter.name);
                    Assert.That(float.IsNaN(uv.y), Is.False, filter.name);
                    Assert.That(uv.x, Is.InRange(0f, 1f), filter.name);
                    Assert.That(uv.y, Is.InRange(0f, 1f), filter.name);
                }
            }
        }

        [Test]
        public void BladeUvsCoverEveryFaceAndRendererTransformStaysWithSeed()
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(
                ShortSwordGeneratorLabSceneBuilder.ColumnBladeStoneMaterialPath);
            Material wood = AssetDatabase.LoadAssetAtPath<Material>(
                ShortSwordGeneratorLabSceneBuilder.ColumnBladeWoodMaterialPath);
            Material obsidian = AssetDatabase.LoadAssetAtPath<Material>(
                ShortSwordGeneratorLabSceneBuilder.ColumnBladeObsidianMaterialPath);
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ConfigureMaterials(stone, wood, obsidian, stone, wood);

            generator.Generate(5197);
            GameObject firstBlade = BladePart(generator);
            Mesh mesh = firstBlade.GetComponent<MeshFilter>().sharedMesh;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector2 a = uvs[triangles[index]];
                Vector2 b = uvs[triangles[index + 1]];
                Vector2 c = uvs[triangles[index + 2]];
                float doubledArea = Mathf.Abs(
                    (b.x - a.x) * (c.y - a.y) -
                    (b.y - a.y) * (c.x - a.x));
                Assert.That(
                    doubledArea,
                    Is.GreaterThan(0.000001f),
                    $"Collapsed blade UV triangle {index / 3}.");
            }

            var properties = new MaterialPropertyBlock();
            firstBlade.GetComponent<Renderer>().GetPropertyBlock(properties);
            Vector4 stoneTransform = properties.GetVector("_BaseMap_ST");
            Assert.That(stoneTransform.x, Is.LessThan(1f));
            Assert.That(stoneTransform.y, Is.EqualTo(stoneTransform.x));
            foreach (Vector2 uv in uvs)
            {
                Vector2 sampled = new Vector2(
                    uv.x * stoneTransform.x + stoneTransform.z,
                    uv.y * stoneTransform.y + stoneTransform.w);
                Assert.That(sampled.x, Is.InRange(0f, 1f));
                Assert.That(sampled.y, Is.InRange(0f, 1f));
            }

            generator.SetBladeMaterial(ColumnBladeMaterial.Wood);
            GameObject woodBlade = BladePart(generator);
            properties.Clear();
            woodBlade.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(
                properties.GetVector("_BaseMap_ST"),
                Is.EqualTo(stoneTransform));
            Assert.That(
                woodBlade.GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(wood));

            generator.Generate(5198);
            GameObject nextBlade = BladePart(generator);
            properties.Clear();
            nextBlade.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(
                properties.GetVector("_BaseMap_ST"),
                Is.Not.EqualTo(stoneTransform));
        }

        [Test]
        public void SeedPoolIncludesSlabsBlocksEdgesAndAllGuardFamilies()
        {
            var sections = new HashSet<ColumnBladeSectionProfile>();
            var categories = new HashSet<ColumnBladeShapeCategory>();
            var edges = new HashSet<ColumnBladeEdgeStyle>();
            var guards = new HashSet<ColumnBladeGuardProfile>();
            for (int seed = 1; seed <= 256; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(seed);
                sections.Add(definition.SectionProfile);
                categories.Add(definition.ShapeCategory);
                edges.Add(definition.EdgeStyle);
                guards.Add(definition.GuardProfile);

                float sectionRatio = definition.BladeThickness /
                    definition.BladeCoreWidth;
                if (definition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock)
                {
                    Assert.That(
                        definition.SectionProfile,
                        Is.EqualTo(ColumnBladeSectionProfile.BalancedBlock));
                    Assert.That(
                        definition.BladeThickness / definition.BladeWidth,
                        Is.InRange(0.70f, 0.94f));
                    Assert.That(
                        definition.BladeThickness,
                        Is.LessThanOrEqualTo(0.08501f));
                }
                else if (definition.ShapeCategory ==
                         ColumnBladeShapeCategory.FlatThin)
                {
                    Assert.That(
                        definition.SectionProfile,
                        Is.EqualTo(ColumnBladeSectionProfile.FlatSlab));
                    Assert.That(sectionRatio, Is.InRange(0.16f, 0.28f));
                }
                else
                {
                    Assert.That(
                        definition.SectionProfile,
                        Is.EqualTo(ColumnBladeSectionProfile.FlatSlab));
                    Assert.That(sectionRatio, Is.InRange(0.098f, 0.25f));
                    Assert.That(
                        definition.BladeThickness,
                        Is.LessThanOrEqualTo(0.029f),
                        "Wide blades must remain decisively flat.");
                }
                Assert.That(
                    definition.BladeWidth,
                    Is.LessThanOrEqualTo(0.166f),
                    $"Seed {seed} exceeded the capped overall width.");

                Assert.That(
                    definition.GuardWidth,
                    Is.GreaterThan(definition.BladeWidth),
                    $"Seed {seed} blade width escaped its guard.");
                Assert.That(
                    definition.GuardDepth,
                    Is.GreaterThan(definition.BladeThickness),
                    $"Seed {seed} blade depth escaped its guard.");
                if (definition.GuardProfile ==
                    ColumnBladeGuardProfile.Ring)
                {
                    Assert.That(definition.ShapeCategory,
                        Is.Not.EqualTo(
                            ColumnBladeShapeCategory.SquareBlock));
                    Assert.That(definition.GuardHeight,
                        Is.GreaterThan(definition.GuardDepth * 2f));
                    Assert.That(
                        ProceduralColumnBladeGenerator
                            .ResolveRingGuardRimThickness(definition),
                        Is.InRange(0.014f, 0.024f));
                }
                else
                {
                    Assert.That(
                        definition.GuardWidth - definition.BladeWidth,
                        Is.EqualTo(
                            definition.GuardDepth -
                            definition.BladeThickness).Within(0.00001f),
                        $"Seed {seed} guard clearance was not even.");
                    Assert.That(
                        (definition.GuardWidth - definition.BladeWidth) *
                            0.5f,
                        Is.LessThanOrEqualTo(0.02541f),
                        $"Seed {seed} exceeded one inch of guard clearance per side.");
                }
            }

            Assert.That(
                sections,
                Is.EquivalentTo(Enum.GetValues(typeof(ColumnBladeSectionProfile))));
            Assert.That(
                categories,
                Is.EquivalentTo(Enum.GetValues(typeof(ColumnBladeShapeCategory))));
            Assert.That(
                edges,
                Is.EquivalentTo(Enum.GetValues(typeof(ColumnBladeEdgeStyle))));
            Assert.That(
                guards,
                Is.EquivalentTo(Enum.GetValues(typeof(ColumnBladeGuardProfile))));
        }

        [Test]
        public void ShapeCategoryLockGuaranteesExactlyTheRequestedProfile()
        {
            foreach (ColumnBladeShapeCategory category in
                     Enum.GetValues(typeof(ColumnBladeShapeCategory)))
            {
                for (int seed = 1; seed <= 64; seed++)
                {
                    ProceduralColumnBladeDefinition definition =
                        ProceduralColumnBladeGenerator.CreateDefinition(
                            seed,
                            ColumnBladeMaterial.Stone,
                            category);
                    Assert.That(definition.ShapeCategory, Is.EqualTo(category));
                }
            }

            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.WideFlat);
            Assert.That(
                generator.Generate(4103).ShapeCategory,
                Is.EqualTo(ColumnBladeShapeCategory.WideFlat));
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.WideFlat);
            Assert.That(generator.LockedShapeCategory, Is.Null);

            generator.ToggleTopProfileLock(ColumnBladeTopProfile.Flat);
            Assert.That(
                generator.Generate(4104).TopProfile,
                Is.EqualTo(ColumnBladeTopProfile.Flat));
            generator.ToggleTopProfileLock(ColumnBladeTopProfile.SteepSlant);
            Assert.That(
                generator.Generate(4104).TopProfile,
                Is.EqualTo(ColumnBladeTopProfile.SteepSlant));
            Assert.That(
                generator.LockedTopProfile,
                Is.EqualTo(ColumnBladeTopProfile.SteepSlant));
        }

        [Test]
        public void RingGuardIsRestrictedToFlatBladeFamilies()
        {
            foreach (int seed in Enumerable.Range(1, 128))
            {
                ProceduralColumnBladeDefinition square =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        requiredShapeCategory:
                            ColumnBladeShapeCategory.SquareBlock,
                        requiredGuardProfile:
                            ColumnBladeGuardProfile.Ring);
                Assert.That(square.GuardProfile,
                    Is.Not.EqualTo(ColumnBladeGuardProfile.Ring));
            }

            foreach (ColumnBladeShapeCategory shape in new[]
                     {
                         ColumnBladeShapeCategory.FlatThin,
                         ColumnBladeShapeCategory.WideFlat
                     })
            {
                ProceduralColumnBladeDefinition flat =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        6113 + (int)shape,
                        requiredShapeCategory: shape,
                        requiredGuardProfile:
                            ColumnBladeGuardProfile.Ring);
                Assert.That(flat.GuardProfile,
                    Is.EqualTo(ColumnBladeGuardProfile.Ring));
            }

            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.SquareBlock);
            generator.Generate(6121);
            generator.ToggleGuardProfileLock(ColumnBladeGuardProfile.Ring);
            Assert.That(generator.LockedGuardProfile, Is.Null);
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.FlatThin);
            generator.Generate(6121);
            generator.ToggleGuardProfileLock(ColumnBladeGuardProfile.Ring);
            Assert.That(generator.LockedGuardProfile,
                Is.EqualTo(ColumnBladeGuardProfile.Ring));
        }

        [Test]
        public void RingGuardIsAClosedFacetedAnnulusWithSeatedJoints()
        {
            foreach (ColumnBladeShapeCategory shape in new[]
                     {
                         ColumnBladeShapeCategory.FlatThin,
                         ColumnBladeShapeCategory.WideFlat
                     })
            {
                ProceduralColumnBladeGenerator generator = CreateGenerator();
                generator.ToggleShapeCategoryLock(shape);
                generator.ToggleGuardProfileLock(
                    ColumnBladeGuardProfile.Ring);
                ProceduralColumnBladeDefinition definition =
                    generator.Generate(6203 + (int)shape * 31);
                GameObject guard = generator.GeneratedParts.Single(part =>
                    part.name ==
                        ProceduralColumnBladeGenerator.GuardPartName);
                Mesh mesh = guard.GetComponent<MeshFilter>().sharedMesh;

                Assert.That(mesh.bounds.size.x,
                    Is.EqualTo(definition.GuardWidth).Within(0.00001f));
                Assert.That(mesh.bounds.size.y,
                    Is.EqualTo(definition.GuardHeight).Within(0.00001f));
                Assert.That(mesh.bounds.size.z,
                    Is.EqualTo(definition.GuardDepth).Within(0.00001f));
                Assert.That(mesh.normals.Any(normal => normal.z > 0.99f),
                    Is.True);
                Assert.That(mesh.normals.Any(normal => normal.z < -0.99f),
                    Is.True);
                float rim = ProceduralColumnBladeGenerator
                    .ResolveRingGuardRimThickness(definition);
                Assert.That(definition.GuardWidth,
                    Is.GreaterThanOrEqualTo(
                        definition.BladeWidth + rim));

                float bladeSeatWidth = ProceduralColumnBladeGenerator
                    .ResolveRingGuardBladeSeatWidth(definition);
                float handleSeatWidth = ProceduralColumnBladeGenerator
                    .ResolveRingGuardHandleSeatWidth(definition);
                Assert.That(handleSeatWidth,
                    Is.GreaterThan(definition.HandleWidth));
                Assert.That(bladeSeatWidth,
                    Is.LessThan(definition.GuardWidth));
                Assert.That(handleSeatWidth,
                    Is.LessThan(definition.GuardWidth));

                Bounds blade = BoundsOf(
                    generator,
                    ProceduralColumnBladeGenerator.BladePartName);
                Bounds bladeJoint = BoundsOf(
                    generator,
                    ProceduralColumnBladeGenerator.BladeRingJointPartName);
                Bounds handle = BoundsOf(
                    generator,
                    ProceduralColumnBladeGenerator.HandlePartName);
                Assert.That(blade.min.y,
                    Is.EqualTo(mesh.bounds.max.y).Within(0.00001f));
                Assert.That(bladeJoint.max.y,
                    Is.EqualTo(mesh.bounds.max.y).Within(0.00001f));
                Assert.That(bladeJoint.min.y,
                    Is.LessThan(mesh.bounds.max.y));
                Assert.That(handle.max.y,
                    Is.EqualTo(mesh.bounds.min.y).Within(0.00001f));
                Assert.That(handle.size.x,
                    Is.LessThanOrEqualTo(handleSeatWidth + 0.003f));
                Assert.That(mesh.vertices.All(IsFinite), Is.True);

                UnityEngine.Object.DestroyImmediate(
                    generator.gameObject);
                root = null;
            }
        }

        [Test]
        public void RingJointContinuesBladeWidthDepthAndInsetToTheSeat()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(
                ColumnBladeMaterial.Obsidian,
                regenerateCurrent: false);
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.WideFlat);
            generator.ToggleGuardProfileLock(ColumnBladeGuardProfile.Ring);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.SilhouetteInset);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(6289);

            Bounds joint = BoundsOf(
                generator,
                ProceduralColumnBladeGenerator.BladeRingJointPartName);
            Assert.That(joint.size.x,
                Is.EqualTo(definition.BladeWidth).Within(0.00001f));
            Assert.That(joint.size.z,
                Is.EqualTo(definition.BladeThickness).Within(0.00001f));

            Transform inset = BladePart(generator).transform.Find(
                ProceduralColumnBladeGenerator.SilhouetteInsetPartName);
            Assert.That(inset, Is.Not.Null);
            Mesh insetMesh = inset.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(insetMesh.bounds.min.y,
                Is.EqualTo(
                    ProceduralColumnBladeGenerator.ResolveBladeBottomY(
                        definition)).Within(0.00001f));
        }

        [Test]
        public void RingGuardHasThreeCoordinatedColorsPerBladeMaterial()
        {
            foreach (ColumnBladeMaterial material in Enum.GetValues(
                         typeof(ColumnBladeMaterial)))
            {
                var colors = new HashSet<Color>();
                for (int variant = 0; variant < 3; variant++)
                {
                    Color color = ProceduralColumnBladeGenerator
                        .ResolveRingGuardColor(material, variant);
                    colors.Add(color);
                    Assert.That(Vector4.Distance(
                            color,
                            ProceduralColumnBladeGenerator
                                .ResolveBladeColor(material)),
                        Is.GreaterThan(0.08f));
                }
                Assert.That(colors.Count, Is.EqualTo(3));
            }

            Assert.That(
                Enumerable.Range(1, 256)
                    .Select(ProceduralColumnBladeGenerator
                        .ResolveRingGuardColorVariant)
                    .ToHashSet(),
                Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void EngravingBranchIncludesLinesAndSilhouetteInsets()
        {
            var styles = new HashSet<ColumnBladeEngravingStyle>();
            var terminations = new HashSet<ColumnBladeEngravingTermination>();
            for (int seed = 1; seed <= 512; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(seed);
                styles.Add(definition.PrimaryEngraving);
                if (definition.PrimaryEngraving !=
                    ColumnBladeEngravingStyle.None)
                {
                    if (definition.PrimaryEngraving ==
                        ColumnBladeEngravingStyle.StraightLine)
                    {
                        terminations.Add(definition.EngravingTermination);
                    }
                    else
                    {
                        Assert.That(definition.PrimaryEngraving,
                            Is.EqualTo(
                                ColumnBladeEngravingStyle.SilhouetteInset));
                        Assert.That(definition.EngravingTermination,
                            Is.EqualTo(ColumnBladeEngravingTermination.Full));
                        Assert.That(definition.EngravingPath,
                            Is.EqualTo(ColumnBladeEngravingPath.Single));
                    }
                }
            }

            Assert.That(styles,
                Is.EquivalentTo(Enum.GetValues(
                    typeof(ColumnBladeEngravingStyle))));
            Assert.That(terminations,
                Is.EquivalentTo(new[]
                {
                    ColumnBladeEngravingTermination.Full,
                    ColumnBladeEngravingTermination.Circle
                }));

            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Single);
            Assert.That(generator.Generate(4131).PrimaryEngraving,
                Is.EqualTo(ColumnBladeEngravingStyle.StraightLine));
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.None);
            Assert.That(generator.Generate(4131).PrimaryEngraving,
                Is.EqualTo(ColumnBladeEngravingStyle.None));
        }

        [Test]
        [Category("ColumnBladeInset")]
        public void SilhouetteInsetIsRecessedAndSafeAcrossEveryBladeFamily()
        {
            foreach (ColumnBladeShapeCategory shape in Enum.GetValues(
                         typeof(ColumnBladeShapeCategory)))
            {
                foreach (ColumnBladeMaterial material in Enum.GetValues(
                             typeof(ColumnBladeMaterial)))
                {
                    ProceduralColumnBladeGenerator generator =
                        CreateGenerator();
                    generator.SetBladeMaterial(material, false);
                    generator.ToggleShapeCategoryLock(shape);
                    generator.ToggleEngravingStyleLock(
                        ColumnBladeEngravingStyle.SilhouetteInset);
                    ProceduralColumnBladeDefinition definition =
                        generator.Generate(7301 + (int)shape * 17);
                    GameObject blade = BladePart(generator);
                    Transform floor = blade.transform.Find(
                        ProceduralColumnBladeGenerator
                            .SilhouetteInsetPartName);

                    Assert.That(definition.PrimaryEngraving,
                        Is.EqualTo(
                            ColumnBladeEngravingStyle.SilhouetteInset));
                    Assert.That(definition.EngravingPath,
                        Is.EqualTo(ColumnBladeEngravingPath.Single));
                    Assert.That(definition.EngravingTermination,
                        Is.EqualTo(ColumnBladeEngravingTermination.Full));
                    Assert.That(floor, Is.Not.Null);
                    Mesh bladeMesh = blade.GetComponent<MeshFilter>()
                        .sharedMesh;
                    Mesh floorMesh = floor.GetComponent<MeshFilter>()
                        .sharedMesh;
                    Assert.That(bladeMesh.vertices.All(IsFinite), Is.True);
                    Assert.That(floorMesh.vertices.All(IsFinite), Is.True);
                    Assert.That(bladeMesh.triangles.Length,
                        Is.GreaterThan(0));
                    Assert.That(floorMesh.triangles.Length,
                        Is.GreaterThan(0));

                    float margin = ProceduralColumnBladeGenerator
                        .ResolveSilhouetteInsetMargin(definition);
                    Assert.That(margin, Is.GreaterThanOrEqualTo(0.015f));
                    Assert.That(margin, Is.LessThanOrEqualTo(0.0254f));
                    float expectedBottom =
                        ProceduralColumnBladeGenerator
                            .ResolveBladeBottomY(definition);
                    if (definition.GuardProfile !=
                        ColumnBladeGuardProfile.Ring)
                    {
                        expectedBottom += margin +
                            ProceduralColumnBladeGenerator
                                .ResolveSilhouetteWallRun(definition);
                    }
                    Assert.That(floorMesh.vertices.Min(point => point.y),
                        Is.EqualTo(expectedBottom).Within(0.0001f));

                    Vector3[] normals = floorMesh.normals;
                    Assert.That(normals.Any(normal =>
                            Mathf.Abs(normal.z) > 0.99f),
                        Is.True,
                        $"{shape} needs inset floors on both broad faces.");
                    if (shape == ColumnBladeShapeCategory.SquareBlock)
                    {
                        Assert.That(normals.Any(normal =>
                                Mathf.Abs(normal.x) > 0.99f),
                            Is.True,
                            "Square blades need recessed side faces.");
                        Assert.That(normals.Any(normal =>
                                Mathf.Abs(normal.y) > 0.70f),
                            Is.True,
                            "Square blades need a recessed top floor.");
                    }
                    else
                    {
                        Assert.That(normals.All(normal =>
                                Mathf.Abs(normal.z) > 0.99f),
                            Is.True,
                            $"{shape} should recess only its flat faces.");
                    }

                    var properties = new MaterialPropertyBlock();
                    floor.GetComponent<Renderer>()
                        .GetPropertyBlock(properties);
                    Assert.That(Vector4.Distance(
                            properties.GetColor("_BaseColor"),
                            ProceduralColumnBladeGenerator
                                .ResolveSilhouetteInsetColor(material)),
                        Is.LessThan(0.00001f));

                    UnityEngine.Object.DestroyImmediate(
                        generator.gameObject);
                    root = null;
                }
            }
        }

        [Test]
        public void SquareBladesAlwaysRejectTwinSideEdges()
        {
            foreach (int seed in Enumerable.Range(1, 128))
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        ColumnBladeShapeCategory.SquareBlock,
                        null,
                        null,
                        null,
                        ColumnBladeEdgeStyle.TwinSideEdges);
                Assert.That(definition.EdgeStyle,
                    Is.EqualTo(ColumnBladeEdgeStyle.Plain));
                Assert.That(definition.BladeEdgeWidth, Is.EqualTo(0f));
                Assert.That(definition.BladeWidth,
                    Is.EqualTo(definition.BladeCoreWidth));
            }

            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.SquareBlock);
            generator.Generate(8179);
            generator.ToggleEdgeStyleLock(
                ColumnBladeEdgeStyle.TwinSideEdges);
            Assert.That(generator.LockedEdgeStyle, Is.Null);
            UnityEngine.Object.DestroyImmediate(generator.gameObject);
            root = null;
        }

        [Test]
        public void SilhouetteWallProfilesAreDeterministicAndGeometric()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(ColumnBladeMaterial.Obsidian, false);
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.WideFlat);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.SilhouetteInset);

            float previousRun = -1f;
            foreach (ColumnBladeSilhouetteWallProfile profile in
                     Enum.GetValues(
                         typeof(ColumnBladeSilhouetteWallProfile)))
            {
                generator.ToggleSilhouetteWallProfileLock(profile);
                ProceduralColumnBladeDefinition definition =
                    generator.Generate(8117);
                Assert.That(definition.SilhouetteWallProfile,
                    Is.EqualTo(profile));
                float run = ProceduralColumnBladeGenerator
                    .ResolveSilhouetteWallRun(definition);
                Assert.That(run, Is.GreaterThan(previousRun));
                previousRun = run;

                Mesh blade = BladePart(generator)
                    .GetComponent<MeshFilter>().sharedMesh;
                if (profile == ColumnBladeSilhouetteWallProfile.Straight)
                {
                    Assert.That(run, Is.EqualTo(0f));
                }
                else
                {
                    Assert.That(blade.normals.Any(normal =>
                            Mathf.Abs(normal.y) > 0.05f &&
                            Mathf.Abs(normal.z) > 0.05f),
                        Is.True,
                        $"{profile} needs physically sloped inset walls.");
                }
                generator.ToggleSilhouetteWallProfileLock(profile);
            }
            UnityEngine.Object.DestroyImmediate(generator.gameObject);
            root = null;
        }

        [Test]
        public void SquareSilhouetteCapHasAVisibleTopFacingFloor()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(ColumnBladeMaterial.Wood, false);
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.SquareBlock);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.SilhouetteInset);
            generator.ToggleSilhouetteWallProfileLock(
                ColumnBladeSilhouetteWallProfile.DramaticSlant);
            generator.Generate(8221);

            Mesh floor = BladePart(generator).transform.Find(
                    ProceduralColumnBladeGenerator.SilhouetteInsetPartName)
                .GetComponent<MeshFilter>().sharedMesh;
            float topArea = 0f;
            int[] triangles = floor.triangles;
            Vector3[] vertices = floor.vertices;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 first = vertices[triangles[index]];
                Vector3 second = vertices[triangles[index + 1]];
                Vector3 third = vertices[triangles[index + 2]];
                Vector3 cross = Vector3.Cross(second - first, third - first);
                if (cross.normalized.y > 0.30f)
                {
                    topArea += cross.magnitude * 0.5f;
                }
            }
            Assert.That(topArea, Is.GreaterThan(0.00015f));
            UnityEngine.Object.DestroyImmediate(generator.gameObject);
            root = null;
        }

        [Test]
        public void FlatBladeSideBevelWrapsAcrossTerminalTop()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(ColumnBladeMaterial.Obsidian, false);
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.FlatThin);
            generator.ToggleEdgeStyleLock(
                ColumnBladeEdgeStyle.TwinSideEdges);
            generator.ToggleTopProfileLock(ColumnBladeTopProfile.Flat);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.None);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(8293);
            Mesh blade = BladePart(generator)
                .GetComponent<MeshFilter>().sharedMesh;

            Assert.That(ProceduralColumnBladeGenerator
                    .ResolveTopEdgeWrapDrop(definition),
                Is.GreaterThan(0f));
            float maximumY = blade.vertices.Max(vertex => vertex.y);
            float halfCore = definition.BladeCoreWidth * 0.5f;
            Assert.That(blade.vertices.Any(vertex =>
                    Mathf.Abs(vertex.y - maximumY) < 0.00001f &&
                    Mathf.Abs(vertex.x) <= halfCore + 0.0001f),
                Is.True);
            Assert.That(blade.vertices.Any(vertex =>
                    Mathf.Abs(vertex.x) > halfCore + 0.0001f &&
                    vertex.y < maximumY - 0.0002f),
                Is.True,
                "The cutting wedges should turn down across the top cap.");
            UnityEngine.Object.DestroyImmediate(generator.gameObject);
            root = null;
        }

        [Test]
        public void StraightEngravingCanForkSymmetricallyAtSeededHeights()
        {
            var paths = new HashSet<ColumnBladeEngravingPath>();
            var forkFractions = new HashSet<float>();
            for (int seed = 1; seed <= 512; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        null,
                        null,
                        ColumnBladeEngravingStyle.StraightLine);
                paths.Add(definition.EngravingPath);
                if (definition.EngravingPath ==
                    ColumnBladeEngravingPath.Forked)
                {
                    forkFractions.Add(definition.EngravingForkFraction);
                    Assert.That(definition.EngravingForkFraction,
                        Is.InRange(0.22f, 0.44f));
                    Assert.That(definition.EngravingForkHalfSpacing,
                        Is.GreaterThan(0f));
                }
            }
            Assert.That(paths, Is.EquivalentTo(Enum.GetValues(
                typeof(ColumnBladeEngravingPath))));
            Assert.That(forkFractions.Count, Is.GreaterThan(8),
                "Forks should begin at visibly different seeded heights.");

            int seedWithNoCircle = Enumerable.Range(1, 512).First(seed =>
            {
                ProceduralColumnBladeDefinition candidate =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        null,
                        null,
                        ColumnBladeEngravingStyle.StraightLine,
                        ColumnBladeEngravingPath.Forked);
                return candidate.EngravingTermination !=
                    ColumnBladeEngravingTermination.Circle;
            });
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Forked);
            ProceduralColumnBladeDefinition forked =
                generator.Generate(seedWithNoCircle);
            Assert.That(forked.EngravingPath,
                Is.EqualTo(ColumnBladeEngravingPath.Forked));

            float bottom = ProceduralColumnBladeGenerator
                .ResolveBladeBottomY(forked);
            float spacing = forked.EngravingForkHalfSpacing;
            float halfWidth = ProceduralColumnBladeGenerator
                .ResolveEngravingWidth(forked) * 0.5f;
            float floor = forked.BladeThickness * 0.5f -
                ProceduralColumnBladeGenerator.ResolveEngravingDepth(forked) +
                0.000025f;
            Mesh inlay = BladePart(generator).transform
                .Find("Engraving Floor Inlay")
                .GetComponent<MeshFilter>().sharedMesh;
            Assert.That(inlay.vertices.Any(vertex =>
                    Mathf.Abs(vertex.x - (-spacing - halfWidth)) < 0.00001f &&
                    Mathf.Abs(vertex.y - bottom) < 0.00001f &&
                    Mathf.Abs(Mathf.Abs(vertex.z) - floor) < 0.00001f),
                Is.True,
                "The left recessed branch must reach the guard.");
            Assert.That(inlay.vertices.Any(vertex =>
                    Mathf.Abs(vertex.x - (spacing + halfWidth)) < 0.00001f &&
                    Mathf.Abs(vertex.y - bottom) < 0.00001f &&
                    Mathf.Abs(Mathf.Abs(vertex.z) - floor) < 0.00001f),
                Is.True,
                "The right recessed branch must mirror the left one.");

            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Forked);
            Assert.That(generator.LockedEngravingPath, Is.Null);
        }

        [Test]
        public void StraightEngravingIsADeepSquareTrenchInTheBladeMesh()
        {
            int seed = Enumerable.Range(1, 512).First(candidate =>
            {
                ProceduralColumnBladeDefinition candidateDefinition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        candidate,
                        ColumnBladeMaterial.Stone,
                        null,
                        null,
                        ColumnBladeEngravingStyle.StraightLine);
                return candidateDefinition.EngravingTermination !=
                    ColumnBladeEngravingTermination.Circle;
            });
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(ColumnBladeMaterial.Stone, false);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Single);
            ProceduralColumnBladeDefinition definition = generator.Generate(seed);
            GameObject blade = BladePart(generator);
            Mesh mesh = blade.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            float face = definition.BladeThickness * 0.5f;
            float depth = ProceduralColumnBladeGenerator
                .ResolveEngravingDepth(definition);
            float floor = face - depth;
            float engravingWidth = ProceduralColumnBladeGenerator
                .ResolveEngravingWidth(definition);
            float halfWidth = engravingWidth * 0.5f;
            float bladeBottom = ProceduralColumnBladeGenerator
                .ResolveBladeBottomY(definition);
            Assert.That(depth, Is.InRange(0.004f, 0.012f));
            Assert.That(depth,
                Is.LessThanOrEqualTo(definition.BladeThickness * 0.38001f));
            Assert.That(engravingWidth, Is.InRange(0.00585f, 0.022f));

            bool HasFloor(int side) => vertices.Any(vertex =>
                Mathf.Abs(vertex.x) <= halfWidth + 0.00001f &&
                Mathf.Abs(vertex.z - side * floor) < 0.00001f);
            Assert.That(HasFloor(1), Is.True,
                "The front face needs a visibly recessed flat floor.");
            Assert.That(HasFloor(-1), Is.True,
                "The rear face needs the identical recessed floor.");
            Assert.That(vertices.Any(vertex =>
                    Mathf.Abs(vertex.y - bladeBottom) < 0.00001f &&
                    Mathf.Abs(Mathf.Abs(vertex.z) - floor) < 0.00001f),
                Is.True,
                "The trench must begin directly at the guard.");

            bool hasFlatVerticalWall = false;
            for (int index = 0; index < vertices.Length; index++)
            {
                if (Mathf.Abs(normals[index].x) > 0.99f &&
                    Mathf.Abs(Mathf.Abs(vertices[index].x) - halfWidth) <
                        0.00001f &&
                    Mathf.Abs(vertices[index].z) >= floor - 0.00001f &&
                    Mathf.Abs(vertices[index].z) <= face + 0.00001f)
                {
                    hasFlatVerticalWall = true;
                    break;
                }
            }
            Assert.That(hasFlatVerticalWall, Is.True,
                "Square engraving sides should produce a crisp lighting break.");

            foreach (Vector3 vertex in vertices.Where(vertex =>
                         Mathf.Abs(Mathf.Abs(vertex.z) - floor) < 0.00001f &&
                         Mathf.Abs(vertex.x) <= halfWidth + 0.00001f))
            {
                Assert.That(vertices.Any(other =>
                        Mathf.Abs(other.x - vertex.x) < 0.00001f &&
                        Mathf.Abs(other.y - vertex.y) < 0.00001f &&
                        Mathf.Abs(other.z + vertex.z) < 0.00001f),
                    Is.True,
                    $"Missing mirrored trench floor vertex for {vertex}.");
            }
        }

        [Test]
        public void StraightLineTerminationOnlyUsesFullOrCircle()
        {
            var terminations = new HashSet<ColumnBladeEngravingTermination>();
            for (int seed = 1; seed <= 256; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        null,
                        null,
                        ColumnBladeEngravingStyle.StraightLine);
                terminations.Add(definition.EngravingTermination);
                Assert.That(definition.EngravingTermination,
                    Is.EqualTo(ColumnBladeEngravingTermination.Full)
                        .Or.EqualTo(
                            ColumnBladeEngravingTermination.Circle));
                if (definition.EngravingTermination ==
                         ColumnBladeEngravingTermination.Circle)
                {
                    Assert.That(ProceduralColumnBladeGenerator
                            .ResolveEngravingCircleRadius(definition),
                        Is.InRange(0.016f, 0.030f));
                }
            }

            Assert.That(terminations,
                Is.EquivalentTo(new[]
                {
                    ColumnBladeEngravingTermination.Full,
                    ColumnBladeEngravingTermination.Circle
                }));
        }

        [Test]
        public void PlainFullLinesVaryFromTheExistingMinimumWidth()
        {
            var scales = new HashSet<float>();
            for (int seed = 1; seed <= 512; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        null,
                        null,
                        ColumnBladeEngravingStyle.StraightLine,
                        ColumnBladeEngravingPath.Single);
                if (definition.EngravingTermination !=
                    ColumnBladeEngravingTermination.Full)
                {
                    Assert.That(definition.EngravingWidthScale,
                        Is.EqualTo(1f));
                    continue;
                }

                scales.Add(definition.EngravingWidthScale);
                Assert.That(definition.EngravingWidthScale,
                    Is.InRange(1f, 2.25f));
                ProceduralColumnBladeDefinition minimum = definition;
                minimum.EngravingWidthScale = 1f;
                float minimumWidth = ProceduralColumnBladeGenerator
                    .ResolveEngravingWidth(minimum);
                float generatedWidth = ProceduralColumnBladeGenerator
                    .ResolveEngravingWidth(definition);
                Assert.That(generatedWidth,
                    Is.GreaterThanOrEqualTo(minimumWidth));
                Assert.That(generatedWidth,
                    Is.LessThanOrEqualTo(
                        definition.BladeCoreWidth * 0.22001f));
            }
            Assert.That(scales.Count, Is.GreaterThan(32));
            Assert.That(scales.Max(), Is.GreaterThan(2.1f));

            foreach (ColumnBladeEngravingPath path in new[]
                     {
                         ColumnBladeEngravingPath.Forked
                     })
            {
                for (int seed = 1; seed <= 64; seed++)
                {
                    ProceduralColumnBladeDefinition fork =
                        ProceduralColumnBladeGenerator.CreateDefinition(
                            seed,
                            ColumnBladeMaterial.Stone,
                            null,
                            null,
                            ColumnBladeEngravingStyle.StraightLine,
                            path);
                    Assert.That(fork.EngravingWidthScale,
                        Is.EqualTo(1f));
                }
            }
        }

        [Test]
        public void SquareFullStraightLinesCanEngraveAllFourFaces()
        {
            // The four-face outcome is seeded, not universal.
            var outcomes = new HashSet<bool>();
            int fourFaceSeed = -1;
            for (int seed = 1; seed <= 1024; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        ColumnBladeShapeCategory.SquareBlock,
                        null,
                        ColumnBladeEngravingStyle.StraightLine,
                        ColumnBladeEngravingPath.Single,
                        ColumnBladeEdgeStyle.Plain);
                if (definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Full)
                {
                    outcomes.Add(definition.EngravingAllFourSides);
                    if (definition.EngravingAllFourSides && fourFaceSeed < 0)
                    {
                        fourFaceSeed = seed;
                    }
                }
                else
                {
                    Assert.That(definition.EngravingAllFourSides, Is.False);
                }
            }
            Assert.That(outcomes, Is.EquivalentTo(new[] { false, true }));
            Assert.That(fourFaceSeed, Is.GreaterThan(0));

            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleShapeCategoryLock(
                ColumnBladeShapeCategory.SquareBlock);
            generator.ToggleEdgeStyleLock(ColumnBladeEdgeStyle.Plain);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Single);
            ProceduralColumnBladeDefinition generated =
                generator.Generate(fourFaceSeed);
            Assert.That(generated.EngravingAllFourSides, Is.True);

            Mesh floor = BladePart(generator).transform
                .Find("Engraving Floor Inlay")
                .GetComponent<MeshFilter>().sharedMesh;
            Assert.That(floor.vertexCount, Is.EqualTo(24),
                "Four planar floor quads become eight flat-shaded triangles.");
            float depth = ProceduralColumnBladeGenerator
                .ResolveEngravingDepth(generated);
            float broadFloor = generated.BladeThickness * 0.5f -
                depth + 0.000025f;
            float sideFloor = generated.BladeWidth * 0.5f -
                depth + 0.000025f;
            Vector3[] vertices = floor.vertices;
            Assert.That(vertices.Any(vertex =>
                Mathf.Abs(vertex.z - broadFloor) < 0.00001f), Is.True);
            Assert.That(vertices.Any(vertex =>
                Mathf.Abs(vertex.z + broadFloor) < 0.00001f), Is.True);
            Assert.That(vertices.Any(vertex =>
                Mathf.Abs(vertex.x - sideFloor) < 0.00001f), Is.True);
            Assert.That(vertices.Any(vertex =>
                Mathf.Abs(vertex.x + sideFloor) < 0.00001f), Is.True);
        }

        [Test]
        public void FourFaceEngravingOnlyAppliesToPlainSquareFullSingleLines()
        {
            for (int seed = 1; seed <= 128; seed++)
            {
                Assert.That(ProceduralColumnBladeGenerator
                        .ResolveEngravingAllFourSides(
                            seed,
                            ColumnBladeShapeCategory.FlatThin,
                            ColumnBladeEdgeStyle.Plain,
                            ColumnBladeEngravingStyle.StraightLine,
                            ColumnBladeEngravingTermination.Full,
                            ColumnBladeEngravingPath.Single),
                    Is.False);
                Assert.That(ProceduralColumnBladeGenerator
                        .ResolveEngravingAllFourSides(
                            seed,
                            ColumnBladeShapeCategory.SquareBlock,
                            ColumnBladeEdgeStyle.Plain,
                            ColumnBladeEngravingStyle.StraightLine,
                            ColumnBladeEngravingTermination.Circle,
                            ColumnBladeEngravingPath.Single),
                    Is.False);
                Assert.That(ProceduralColumnBladeGenerator
                        .ResolveEngravingAllFourSides(
                            seed,
                            ColumnBladeShapeCategory.SquareBlock,
                            ColumnBladeEdgeStyle.Plain,
                            ColumnBladeEngravingStyle.StraightLine,
                            ColumnBladeEngravingTermination.Full,
                            ColumnBladeEngravingPath.Forked),
                    Is.False);
            }
        }

        [Test]
        public void FullLineTerminationOpensThroughTheActualTopCut()
        {
            int fullSeed = Enumerable.Range(1, 512).First(seed =>
                ProceduralColumnBladeGenerator.CreateDefinition(
                    seed,
                    ColumnBladeMaterial.Stone,
                    null,
                    ColumnBladeTopProfile.SteepSlant,
                    ColumnBladeEngravingStyle.StraightLine)
                    .EngravingTermination ==
                        ColumnBladeEngravingTermination.Full);

            foreach (ColumnBladeEngravingPath path in Enum.GetValues(
                         typeof(ColumnBladeEngravingPath)))
            {
                ProceduralColumnBladeGenerator generator = CreateGenerator();
                generator.ToggleTopProfileLock(
                    ColumnBladeTopProfile.SteepSlant);
                generator.ToggleEngravingStyleLock(
                    ColumnBladeEngravingStyle.StraightLine);
                generator.ToggleEngravingPathLock(path);
                ProceduralColumnBladeDefinition definition =
                    generator.Generate(fullSeed);
                Mesh floor = BladePart(generator).transform
                    .Find("Engraving Floor Inlay")
                    .GetComponent<MeshFilter>().sharedMesh;
                float bottom = ProceduralColumnBladeGenerator
                    .ResolveBladeBottomY(definition);
                float centerTop = bottom + definition.BladeLength -
                    definition.TopSlantRise * 0.5f;
                float halfWidth = ProceduralColumnBladeGenerator
                    .ResolveEngravingWidth(definition) * 0.5f;
                float leftTop = centerTop +
                    ProceduralColumnBladeGenerator
                        .ResolveEngravingTerminationYOffset(
                            definition,
                            -halfWidth);
                float rightTop = centerTop +
                    ProceduralColumnBladeGenerator
                        .ResolveEngravingTerminationYOffset(
                            definition,
                            halfWidth);
                Assert.That(floor.vertices.Any(vertex =>
                        Mathf.Abs(vertex.x + halfWidth) < 0.00001f &&
                        Mathf.Abs(vertex.y - leftTop) < 0.00001f),
                    Is.True,
                    $"{path} must reach the left side of the top cut.");
                Assert.That(floor.vertices.Any(vertex =>
                        Mathf.Abs(vertex.x - halfWidth) < 0.00001f &&
                        Mathf.Abs(vertex.y - rightTop) < 0.00001f),
                    Is.True,
                    $"{path} must reach the right side of the top cut.");
                float maximumY = bottom + definition.BladeLength;
                Mesh bladeMesh = BladePart(generator)
                    .GetComponent<MeshFilter>().sharedMesh;
                foreach (Vector3 vertex in bladeMesh.vertices)
                {
                    float normalized = Mathf.InverseLerp(
                        -definition.BladeWidth * 0.5f,
                        definition.BladeWidth * 0.5f,
                        vertex.x);
                    if (definition.TopSlantDirection < 0)
                    {
                        normalized = 1f - normalized;
                    }
                    float expectedTop = maximumY -
                        definition.TopSlantRise * (1f - normalized);
                    Assert.That(vertex.y,
                        Is.LessThanOrEqualTo(expectedTop + 0.00001f),
                        $"{path} produced geometry above the slanted top at " +
                        $"x={vertex.x:0.000000}.");
                }
                UnityEngine.Object.DestroyImmediate(generator.gameObject);
                root = null;
            }
        }

        [Test]
        public void EngravingFillIsAlwaysGoldAndOnlyCoversTheTrenchFloor()
        {
            var fills = new HashSet<ColumnBladeEngravingFill>();
            for (int seed = 1; seed <= 256; seed++)
            {
                ColumnBladeEngravingFill first =
                    ProceduralColumnBladeGenerator.ResolveEngravingFill(seed);
                Assert.That(
                    ProceduralColumnBladeGenerator.ResolveEngravingFill(seed),
                    Is.EqualTo(first));
                fills.Add(first);
            }
            Assert.That(fills, Is.EquivalentTo(new[]
            {
                ColumnBladeEngravingFill.MutedGold
            }));

            int coloredSeed = Enumerable.Range(1, 512).First(seed =>
            {
                ProceduralColumnBladeDefinition candidate =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone,
                        null,
                        null,
                        ColumnBladeEngravingStyle.StraightLine);
                return candidate.EngravingTermination !=
                           ColumnBladeEngravingTermination.Circle &&
                    !candidate.EngravingAllFourSides;
            });
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Single);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(coloredSeed);
            Transform floor = BladePart(generator).transform.Find(
                "Engraving Floor Inlay");
            Assert.That(floor, Is.Not.Null);
            Mesh floorMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            float expectedFloor = definition.BladeThickness * 0.5f -
                ProceduralColumnBladeGenerator.ResolveEngravingDepth(
                    definition) + 0.000025f;
            Assert.That(floorMesh.bounds.size.x,
                Is.EqualTo(ProceduralColumnBladeGenerator
                    .ResolveEngravingWidth(definition)).Within(0.00001f));
            foreach (Vector3 vertex in floorMesh.vertices)
            {
                Assert.That(Mathf.Abs(vertex.z),
                    Is.EqualTo(expectedFloor).Within(0.00001f),
                    "The inlay must not climb onto either trench wall.");
            }
            var properties = new MaterialPropertyBlock();
            floor.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(Vector4.Distance(
                    properties.GetColor("_BaseColor"),
                    ProceduralColumnBladeGenerator.ResolveEngravingFillColor(
                        definition.EngravingFill)),
                Is.LessThan(0.00001f));
        }

        [Test]
        public void CircleTerminationUsesOneCleanContinuousAnnulus()
        {
            int circleSeed = Enumerable.Range(1, 512).First(seed =>
                ProceduralColumnBladeGenerator.CreateDefinition(
                    seed,
                    ColumnBladeMaterial.Stone,
                    null,
                    null,
                    ColumnBladeEngravingStyle.StraightLine)
                    .EngravingTermination ==
                        ColumnBladeEngravingTermination.Circle);
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Single);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(circleSeed);
            Transform floor = BladePart(generator).transform.Find(
                "Engraving Floor Inlay");
            Assert.That(floor, Is.Not.Null);
            Assert.That(BladePart(generator).transform.Find(
                "Engraving Recess Base"), Is.Null);
            Assert.That(BladePart(generator).transform.Find(
                "Engraving Recess Mask"), Is.Null);
            Assert.That(BladePart(generator).transform.Find(
                "Engraving Inner Walls"), Is.Null);
            Mesh floorMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(floorMesh.vertexCount,
                Is.EqualTo(ProceduralColumnBladeGenerator
                    .EngravingCircleSegments * 2 * 6 + 12),
                "The incoming line and returned loop must occupy one floor mesh.");

            float bottom = ProceduralColumnBladeGenerator
                .ResolveBladeBottomY(definition);
            float centerY = bottom + definition.BladeLength *
                definition.EngravingEndFraction;
            float lineWidth = ProceduralColumnBladeGenerator
                .ResolveEngravingWidth(definition);
            float radius = ProceduralColumnBladeGenerator
                .ResolveEngravingCircleRadius(definition);
            float brushOffset = lineWidth * 0.5f /
                Mathf.Cos(Mathf.PI / ProceduralColumnBladeGenerator
                    .EngravingCircleSegments);
            float expectedInner = radius - brushOffset;
            float expectedOuter = radius + brushOffset;
            Vector3[] circleVertices = floorMesh.vertices.Where(vertex =>
            {
                float radialDistance = new Vector2(
                    vertex.x,
                    vertex.y - centerY).magnitude;
                return Mathf.Min(
                    Mathf.Abs(radialDistance - expectedInner),
                    Mathf.Abs(radialDistance - expectedOuter)) < 0.00001f;
            }).ToArray();
            Assert.That(circleVertices.Length,
                Is.EqualTo(ProceduralColumnBladeGenerator
                    .EngravingCircleSegments * 2 * 6));
            foreach (Vector3 vertex in circleVertices)
            {
                float radialDistance = new Vector2(
                    vertex.x,
                    vertex.y - centerY).magnitude;
                Assert.That(
                    Mathf.Min(
                        Mathf.Abs(radialDistance - expectedInner),
                        Mathf.Abs(radialDistance - expectedOuter)),
                    Is.LessThan(0.00001f),
                    "The sampled brush must keep one constant-width inner or " +
                    "outer perimeter.");
            }
            // The terminal ring and incoming line are one continuous cut.
            float expectedFloor = definition.BladeThickness * 0.5f -
                ProceduralColumnBladeGenerator.ResolveEngravingDepth(
                    definition) + 0.000025f;
            foreach (Vector3 vertex in floorMesh.vertices)
            {
                Assert.That(
                    Mathf.Abs(vertex.z),
                    Is.EqualTo(expectedFloor).Within(0.00001f),
                    "The circle floor must share the line channel's recessed depth.");
            }
            Vector3[] floorVertices = floorMesh.vertices;
            Vector3[] floorNormals = floorMesh.normals;
            for (int index = 0; index < floorVertices.Length; index++)
            {
                Assert.That(
                    Mathf.Sign(floorNormals[index].z),
                    Is.EqualTo(Mathf.Sign(floorVertices[index].z)),
                    "Every engraving floor triangle must face outward like " +
                    "the proven straight-line floor; otherwise Unity culls it.");
            }
            float junctionY = centerY - radius;
            Assert.That(floorMesh.vertices.Any(vertex =>
                    Mathf.Abs(vertex.y - junctionY) < 0.00001f &&
                    Mathf.Abs(Mathf.Abs(vertex.x) - lineWidth * 0.5f) <
                        0.00001f),
                Is.True,
                "The incoming line must end on the loop centerline, where the " +
                "single stroke turns into the circle.");
            var properties = new MaterialPropertyBlock();
            floor.GetComponent<Renderer>().GetPropertyBlock(properties);
            Color expectedEngravingColor = ProceduralColumnBladeGenerator
                .ResolveEngravingFillColor(
                    ColumnBladeEngravingFill.MutedGold);
            Assert.That(Vector4.Distance(
                    properties.GetColor("_BaseColor"),
                    expectedEngravingColor),
                Is.LessThan(0.00001f));
            Assert.That(BladePart(generator).transform.childCount,
                Is.EqualTo(1),
                "A line-to-circle engraving must have one renderer and one material.");

            Mesh bladeMesh = BladePart(generator)
                .GetComponent<MeshFilter>().sharedMesh;
            Vector3[] bladeVertices = bladeMesh.vertices;
            Vector3[] bladeNormals = bladeMesh.normals;
            float surface = definition.BladeThickness * 0.5f;
            float depth = ProceduralColumnBladeGenerator
                .ResolveEngravingDepth(definition);
            var exactInnerEdgeSegments = new HashSet<int>();
            bool hasCircleFloor = false;
            bool hasCircleWall = false;
            bool hasUntouchedCenter = false;
            bool hasInternalJoinWall = false;
            float outerJoinY = centerY - Mathf.Sqrt(
                expectedOuter * expectedOuter -
                lineWidth * lineWidth * 0.25f);
            var recessedSectors = new bool[16];
            for (int index = 0; index < bladeVertices.Length; index++)
            {
                Vector3 vertex = bladeVertices[index];
                float radialDistance = new Vector2(
                    vertex.x,
                    vertex.y - centerY).magnitude;
                float pathDistance = Mathf.Abs(radialDistance - radius);
                float absoluteZ = Mathf.Abs(vertex.z);
                if (Mathf.Abs(radialDistance - expectedInner) < 0.00001f &&
                    Mathf.Abs(absoluteZ - (surface + 0.000004f)) < 0.00001f)
                {
                    float edgeAngle = Mathf.Atan2(
                        vertex.y - centerY,
                        vertex.x);
                    int edgeSegment = Mathf.RoundToInt(
                        (edgeAngle + Mathf.PI) / (Mathf.PI * 2f) *
                        ProceduralColumnBladeGenerator
                            .EngravingCircleSegments) %
                        ProceduralColumnBladeGenerator
                            .EngravingCircleSegments;
                    exactInnerEdgeSegments.Add(edgeSegment);
                }
                if (pathDistance < lineWidth * 0.35f &&
                    Mathf.Abs(absoluteZ - (surface - depth)) < 0.00005f)
                {
                    hasCircleFloor = true;
                    float angle = Mathf.Atan2(
                        vertex.y - centerY,
                        vertex.x);
                    int sector = Mathf.FloorToInt(
                        (angle + Mathf.PI) / (Mathf.PI * 2f) *
                        recessedSectors.Length) % recessedSectors.Length;
                    recessedSectors[sector] = true;
                }
                if (pathDistance > lineWidth * 0.5f &&
                    pathDistance < lineWidth * 0.7f &&
                    absoluteZ < surface - 0.00005f &&
                    absoluteZ > surface - depth + 0.00005f &&
                    Mathf.Abs(bladeNormals[index].z) < 0.995f)
                {
                    hasCircleWall = true;
                }
                if (vertex.y > centerY &&
                    radialDistance < radius - lineWidth &&
                    Mathf.Abs(absoluteZ - surface) < 0.00001f)
                {
                    hasUntouchedCenter = true;
                }
                if (vertex.y > outerJoinY + 0.00001f &&
                    vertex.y < junctionY - 0.00001f &&
                    Mathf.Abs(Mathf.Abs(vertex.x) - lineWidth * 0.5f) <
                        0.00001f &&
                    Mathf.Abs(bladeNormals[index].x) > 0.9f &&
                    Mathf.Abs(bladeNormals[index].z) < 0.2f)
                {
                    hasInternalJoinWall = true;
                }
            }
            Assert.That(hasCircleFloor, Is.True,
                "The blade mesh itself must reach the same recessed floor below the circle.");
            Assert.That(exactInnerEdgeSegments.Count,
                Is.EqualTo(ProceduralColumnBladeGenerator
                    .EngravingCircleSegments),
                "The visible inner wall must use one exact circular boundary " +
                "instead of an uneven sampled cut edge.");
            Assert.That(recessedSectors.Count(value => value),
                Is.EqualTo(recessedSectors.Length),
                "Every circle sector must be carved; existing line triangles " +
                "must not cover pieces of the gold floor.");
            Assert.That(hasCircleWall, Is.True,
                "The circle must have blade-material side faces with real lighting normals.");
            Assert.That(hasUntouchedCenter, Is.True,
                "The center of the outlined circle must remain at the blade surface.");
            Assert.That(hasInternalJoinWall, Is.False,
                "Straight trench sidewalls must stop at the circle's outer " +
                "intersection instead of protruding into the gold loop.");
        }

        [Test]
        public void FlatEngravingEndRunsParallelToTheBladeTopCut()
        {
            int seed = Enumerable.Range(1, 1024).First(candidate =>
            {
                ProceduralColumnBladeDefinition value =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        candidate,
                        ColumnBladeMaterial.Stone,
                        null,
                        ColumnBladeTopProfile.SteepSlant,
                        ColumnBladeEngravingStyle.StraightLine);
                return value.EngravingTermination !=
                    ColumnBladeEngravingTermination.Circle;
            });
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.ToggleTopProfileLock(ColumnBladeTopProfile.SteepSlant);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.StraightLine);
            generator.ToggleEngravingPathLock(
                ColumnBladeEngravingPath.Single);
            ProceduralColumnBladeDefinition definition = generator.Generate(seed);
            Mesh floor = BladePart(generator).transform
                .Find("Engraving Floor Inlay")
                .GetComponent<MeshFilter>().sharedMesh;
            float halfWidth = ProceduralColumnBladeGenerator
                .ResolveEngravingWidth(definition) * 0.5f;
            float leftTop = floor.vertices
                .Where(vertex => Mathf.Abs(vertex.x + halfWidth) < 0.00001f)
                .Max(vertex => vertex.y);
            float rightTop = floor.vertices
                .Where(vertex => Mathf.Abs(vertex.x - halfWidth) < 0.00001f)
                .Max(vertex => vertex.y);
            float expectedDifference =
                definition.TopSlantDirection * definition.TopSlantRise *
                (halfWidth * 2f) / definition.BladeWidth;
            Assert.That(rightTop - leftTop,
                Is.EqualTo(expectedDifference).Within(0.00001f));
        }

        [Test]
        public void SlantedTopsAlwaysRiseFromRightToLeft()
        {
            foreach (ColumnBladeTopProfile profile in new[]
                     {
                         ColumnBladeTopProfile.SlightSlant,
                         ColumnBladeTopProfile.SteepSlant
                     })
            {
                foreach (int seed in Enumerable.Range(1, 128))
                {
                    ProceduralColumnBladeDefinition definition =
                        ProceduralColumnBladeGenerator.CreateDefinition(
                            seed,
                            ColumnBladeMaterial.Stone,
                            null,
                            profile);
                    Assert.That(
                        definition.TopSlantDirection,
                        Is.EqualTo(-1),
                        $"Seed {seed} generated a backwards {profile} top.");
                }
            }
        }

        [Test]
        public void TwinSideEdgesRemainBilateralAndPartOfOneBladeMesh()
        {
            int edgedSeed = Enumerable.Range(1, 256).First(seed =>
                ProceduralColumnBladeGenerator.CreateDefinition(seed).EdgeStyle ==
                    ColumnBladeEdgeStyle.TwinSideEdges);
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(ColumnBladeMaterial.Obsidian, false);
            generator.ToggleEngravingStyleLock(
                ColumnBladeEngravingStyle.None);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(edgedSeed);
            GameObject bladePart = generator.GeneratedParts.Single(part =>
                part.name == ProceduralColumnBladeGenerator.BladePartName);
            Mesh blade = bladePart.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = blade.vertices;

            Assert.That(definition.BladeEdgeWidth, Is.GreaterThan(0f));
            Assert.That(
                bladePart.transform.childCount,
                Is.EqualTo(definition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.StraightLine
                        ? definition.EngravingTermination ==
                            ColumnBladeEngravingTermination.Circle
                                ? 1
                                : 1
                        : 0));
            Assert.That(blade.bounds.center.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(blade.bounds.center.z, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(
                vertices.Any(vertex =>
                    Mathf.Abs(vertex.x - definition.BladeWidth * 0.5f) <
                        0.00001f &&
                    Mathf.Abs(vertex.z) < 0.00001f),
                Is.True);
            Assert.That(
                vertices.Any(vertex =>
                    Mathf.Abs(vertex.x + definition.BladeWidth * 0.5f) <
                        0.00001f &&
                    Mathf.Abs(vertex.z) < 0.00001f),
                Is.True);
            float regularBodyTop = blade.bounds.max.y -
                definition.TopSlantRise - 0.012f;
            foreach (Vector3 vertex in vertices.Where(vertex =>
                         vertex.y < regularBodyTop))
            {
                Assert.That(
                    vertices.Any(other =>
                        Mathf.Abs(other.x + vertex.x) < 0.00001f &&
                        Mathf.Abs(other.y - vertex.y) < 0.00001f &&
                        Mathf.Abs(other.z - vertex.z) < 0.00001f),
                    Is.True,
                    $"Missing bilateral partner for {vertex}.");
            }

        }

        [Test]
        public void BladeTopVariesBetweenFlatSlightAndSteepStraightCuts()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.SetBladeMaterial(ColumnBladeMaterial.Obsidian, false);
            var seen = new HashSet<ColumnBladeTopProfile>();
            foreach (int seed in Enumerable.Range(1, 256))
            {
                ProceduralColumnBladeDefinition definition =
                    generator.Generate(seed);
                Mesh blade = BladePart(generator)
                    .GetComponent<MeshFilter>().sharedMesh;
                seen.Add(definition.TopProfile);
                Assert.That(
                    blade.bounds.size.y,
                    Is.InRange(
                        definition.BladeLength - 0.0021f,
                        definition.BladeLength + 0.00001f),
                    "The top chamfer may shorten a slanted cap slightly, but " +
                    "must never extend geometry above its authored cut plane.");
                if (definition.TopProfile == ColumnBladeTopProfile.Flat)
                {
                    Assert.That(definition.TopSlantRise, Is.Zero);
                }
                else if (definition.TopProfile ==
                         ColumnBladeTopProfile.SlightSlant)
                {
                    Assert.That(definition.TopSlantRise, Is.InRange(0.018f, 0.040f));
                }
                else
                {
                    Assert.That(definition.TopSlantRise, Is.InRange(0.055f, 0.105f));
                }
            }
            Assert.That(
                seen,
                Is.EquivalentTo(Enum.GetValues(typeof(ColumnBladeTopProfile))));
        }

        [Test]
        public void FlatStoneTopRemainsOneCleanLevelWithoutTerminalDents()
        {
            int seed = Enumerable.Range(1, 256).First(candidate =>
                ProceduralColumnBladeGenerator.CreateDefinition(candidate)
                    .TopProfile == ColumnBladeTopProfile.Flat);
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            ProceduralColumnBladeDefinition definition = generator.Generate(seed);
            Mesh blade = BladePart(generator)
                .GetComponent<MeshFilter>().sharedMesh;
            float top = blade.bounds.max.y;
            int highLevelCount = blade.vertices
                .Where(vertex => vertex.y >
                    top - definition.BladeLength * 0.08f)
                .Select(vertex => Mathf.RoundToInt(vertex.y * 100000f))
                .Distinct()
                .Count();

            Assert.That(definition.TopProfile, Is.EqualTo(
                ColumnBladeTopProfile.Flat));
            Assert.That(
                highLevelCount,
                Is.LessThanOrEqualTo(2),
                "A flat stone cap should contain only its top and chamfer levels.");
        }

        [Test]
        public void FurnitureUsesShortSwordDesignAndExactBladeRatio()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            ProceduralColumnBladeDefinition definition = generator.Generate(2471);
            ProceduralShortSwordDefinition shortSword =
                ProceduralShortSwordGenerator.CreateDefinition(2471);
            GameObject handle = generator.GeneratedParts.Single(part =>
                part.name == ProceduralColumnBladeGenerator.HandlePartName);
            Assert.That(
                definition.HandleProfile, Is.EqualTo(shortSword.HandleProfile));
            Assert.That(
                definition.HandleCrossSection,
                Is.EqualTo(shortSword.HandleCrossSection));
            Assert.That(definition.GripStyle, Is.EqualTo(shortSword.GripStyle));
            Assert.That(
                definition.PommelProfile, Is.EqualTo(shortSword.HiltProfile));
            Assert.That(handle.transform.childCount, Is.GreaterThan(0));
            float expectedScale = definition.BladeLength / shortSword.BladeLength;
            Assert.That(
                handle.transform.parent.localScale.y,
                Is.EqualTo(expectedScale).Within(0.00001f));
            Assert.That(
                definition.FurnitureRadialScale,
                Is.InRange(0.58f, 0.66f));
            Assert.That(
                handle.transform.parent.localScale.x,
                Is.EqualTo(definition.FurnitureRadialScale).Within(0.00001f));
            Assert.That(
                handle.transform.parent.localScale.z,
                Is.EqualTo(definition.FurnitureRadialScale).Within(0.00001f));
            Assert.That(definition.HandleWidth, Is.LessThanOrEqualTo(0.0423f));
            Assert.That(definition.PommelWidth, Is.LessThanOrEqualTo(0.075f));

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds assembled = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
            {
                assembled.Encapsulate(renderer.bounds);
            }
            Assert.That(
                assembled.size.y,
                Is.EqualTo(definition.AssembledLength).Within(0.012f));
        }

        [Test]
        public void FurniturePopulationIncludesShortSwordGripAndPommelFamilies()
        {
            ProceduralColumnBladeDefinition[] definitions =
                Enumerable.Range(1, 4096)
                    .Select(seed => ProceduralColumnBladeGenerator
                        .CreateDefinition(seed))
                    .ToArray();
            ProceduralShortSwordDefinition[] shortSwords =
                Enumerable.Range(1, 4096)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            Assert.That(
                definitions.Select(value => value.HandleProfile).Distinct(),
                Is.EquivalentTo(
                    shortSwords.Select(value => value.HandleProfile).Distinct()));
            Assert.That(
                definitions.Select(value => value.GripStyle).Distinct(),
                Is.EquivalentTo(
                    shortSwords.Select(value => value.GripStyle).Distinct()));
            Assert.That(
                definitions.Select(value => value.PommelProfile).Distinct(),
                Is.EquivalentTo(
                    shortSwords.Select(value => value.HiltProfile).Distinct()));
            Assert.That(
                definitions.Select(value => value.PommelProfile).Distinct().Count(),
                Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void GuardRetainsTransitionFacesAndPommelUsesShortSwordMesh()
        {
            ProceduralColumnBladeGenerator generator = CreateGenerator();
            generator.Generate(2471);
            Mesh guard = generator.GeneratedParts
                .Single(part =>
                    part.name == ProceduralColumnBladeGenerator.GuardPartName)
                .GetComponent<MeshFilter>()
                .sharedMesh;
            Mesh pommel = generator.GeneratedParts
                .Single(part =>
                    part.name == ProceduralColumnBladeGenerator.PommelPartName)
                .GetComponent<MeshFilter>()
                .sharedMesh;

            Assert.That(
                HasHorizontalTransitionNormal(guard),
                Is.True,
                "The guard should have narrow top and bottom transition faces.");
            Assert.That(pommel.vertexCount, Is.GreaterThan(3));
            Assert.That(pommel.normals, Has.Length.EqualTo(pommel.vertexCount));
        }

        [Test]
        public void ChamfersAndStoneChipsStayProportionalAndDeterministic()
        {
            var observedChipCounts = new HashSet<int>();
            float shallowestResolvedChip = float.MaxValue;
            float deepestResolvedChip = float.MinValue;
            for (int seed = 1; seed <= 128; seed++)
            {
                ProceduralColumnBladeDefinition definition =
                    ProceduralColumnBladeGenerator.CreateDefinition(
                        seed,
                        ColumnBladeMaterial.Stone);
                float bladeChamfer = ProceduralColumnBladeGenerator
                    .ResolveBladeChamferWidth(definition);
                float guardChamfer = ProceduralColumnBladeGenerator
                    .ResolveGuardChamferWidth(definition);
                Assert.That(bladeChamfer, Is.GreaterThan(0f));
                Assert.That(
                    bladeChamfer,
                    Is.LessThanOrEqualTo(
                        Mathf.Min(
                            definition.BladeCoreWidth,
                            definition.BladeThickness) * 0.03001f));
                Assert.That(guardChamfer, Is.InRange(0.0001f, 0.0011251f));
                Assert.That(definition.StoneChipCount, Is.InRange(7, 11));
                Assert.That(
                    definition.StoneChipDepth,
                    Is.LessThanOrEqualTo(definition.BladeWidth * 0.17001f));
                Assert.That(
                    ProceduralColumnBladeGenerator.ResolveStoneChipCount(seed),
                    Is.EqualTo(definition.StoneChipCount));
                observedChipCounts.Add(definition.StoneChipCount);
                float resolvedChip = ProceduralColumnBladeGenerator
                    .ResolveStoneChipDepth(seed, 0.16f);
                shallowestResolvedChip = Mathf.Min(
                    shallowestResolvedChip,
                    resolvedChip);
                deepestResolvedChip = Mathf.Max(
                    deepestResolvedChip,
                    resolvedChip);
            }

            Assert.That(observedChipCounts, Does.Contain(7));
            Assert.That(observedChipCounts, Does.Contain(11));
            Assert.That(shallowestResolvedChip, Is.LessThan(0.003f));
            Assert.That(deepestResolvedChip, Is.GreaterThan(0.020f));
        }

        [Test]
        public void StoneChipDepthIncreasesWithChipWidth()
        {
            float narrow = ProceduralColumnBladeGenerator
                .ResolveStoneChipDepthFactor(0f);
            float small = ProceduralColumnBladeGenerator
                .ResolveStoneChipDepthFactor(0.25f);
            float medium = ProceduralColumnBladeGenerator
                .ResolveStoneChipDepthFactor(0.5f);
            float wide = ProceduralColumnBladeGenerator
                .ResolveStoneChipDepthFactor(1f);

            Assert.That(narrow, Is.EqualTo(0.10f).Within(0.00001f));
            Assert.That(small, Is.GreaterThan(narrow));
            Assert.That(medium, Is.GreaterThan(small));
            Assert.That(wide, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(
                small,
                Is.LessThan(0.30f),
                "A narrow chip must never receive a deep-cut factor.");
        }

        [Test]
        public void ColumnBladeCaptureMatrixIsFixedCompleteAndRepeatable()
        {
            string[] first = ShortSwordGeneratorLabSceneBuilder
                .GetColumnBladeCaptureFileNames();
            string[] repeated = ShortSwordGeneratorLabSceneBuilder
                .GetColumnBladeCaptureFileNames();
            Assert.That(first, Has.Length.EqualTo(12));
            Assert.That(first.Distinct().Count(), Is.EqualTo(first.Length));
            Assert.That(repeated, Is.EqualTo(first));
            foreach (ColumnBladeMaterial material in
                     Enum.GetValues(typeof(ColumnBladeMaterial)))
            {
                Assert.That(
                    first.Count(name => name.StartsWith(material.ToString())),
                    Is.EqualTo(4));
            }
            Assert.That(first.Count(name => name.Contains("front-three-quarter")), Is.EqualTo(6));
            Assert.That(first.Count(name => name.Contains("side-readability")), Is.EqualTo(6));
        }

        [Test]
        public void LabControllerDefaultsToShortSwordAndSwitchesRoots()
        {
            root = new GameObject("Column Blade Lab Controller Test");
            GameObject shortRoot = new GameObject("Short Sword Root");
            shortRoot.transform.SetParent(root.transform, false);
            ProceduralShortSwordGenerator shortGenerator =
                shortRoot.AddComponent<ProceduralShortSwordGenerator>();
            GameObject columnRoot = new GameObject("Column Blade Root");
            columnRoot.transform.SetParent(root.transform, false);
            ProceduralColumnBladeGenerator columnGenerator =
                columnRoot.AddComponent<ProceduralColumnBladeGenerator>();
            columnRoot.SetActive(false);
            ShortSwordGeneratorLabController controller =
                root.AddComponent<ShortSwordGeneratorLabController>();
            controller.Configure(
                shortGenerator,
                shortRoot.transform,
                columnGenerator,
                columnRoot.transform);

            Assert.That(controller.SelectedFamily, Is.EqualTo(SwordGeneratorFamily.ShortSword));
            Assert.That(shortRoot.activeSelf, Is.True);
            Assert.That(columnRoot.activeSelf, Is.False);

            controller.SelectFamily(SwordGeneratorFamily.ColumnBlade);

            Assert.That(controller.SelectedFamily, Is.EqualTo(SwordGeneratorFamily.ColumnBlade));
            Assert.That(shortRoot.activeSelf, Is.False);
            Assert.That(columnRoot.activeSelf, Is.True);
            Assert.That(columnGenerator.HasGeneratedSword, Is.True);

            controller.SelectFamily(SwordGeneratorFamily.ShortSword);

            Assert.That(shortRoot.activeSelf, Is.True);
            Assert.That(columnRoot.activeSelf, Is.False);
            Assert.That(shortGenerator.HasGeneratedSword, Is.True);
        }

        [Test]
        public void MaterialPaletteContainsOnlyTheThreeFoundationMaterials()
        {
            Assert.That(
                Enum.GetValues(typeof(ColumnBladeMaterial)),
                Has.Length.EqualTo(3));
            Assert.That(
                Enum.GetValues(typeof(ColumnBladeMaterial)),
                Is.EquivalentTo(new[]
                {
                    ColumnBladeMaterial.Stone,
                    ColumnBladeMaterial.Wood,
                    ColumnBladeMaterial.Obsidian
                }));
        }

        private ProceduralColumnBladeGenerator CreateGenerator()
        {
            root = new GameObject("Procedural Column Blade Test");
            return root.AddComponent<ProceduralColumnBladeGenerator>();
        }

        private static Bounds BoundsOf(
            ProceduralColumnBladeGenerator generator,
            string partName)
        {
            return generator.GeneratedParts
                .Single(part => part.name == partName)
                .GetComponent<Renderer>()
                .bounds;
        }

        private static GameObject BladePart(
            ProceduralColumnBladeGenerator generator)
        {
            return generator.GeneratedParts.Single(part =>
                part.name == ProceduralColumnBladeGenerator.BladePartName);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) &&
                !float.IsInfinity(value.z);
        }

        private static bool HasHorizontalTransitionNormal(Mesh mesh)
        {
            return mesh.normals.Any(normal =>
                Mathf.Abs(normal.y) > 0.05f &&
                Mathf.Abs(normal.y) < 0.99f &&
                new Vector2(normal.x, normal.z).sqrMagnitude > 0.01f);
        }

    }
}
