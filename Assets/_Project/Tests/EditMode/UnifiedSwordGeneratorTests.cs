using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.EditMode
{
    [Category("ColumnBlade")]
    public sealed class UnifiedSwordGeneratorTests
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
        public void SeededTopLevelRollReachesEverySwordCategory()
        {
            // Root selection remains deterministic while covering both trees.
            UnifiedSwordCategory[] categories = Enumerable.Range(1, 1024)
                .Select(ShortSwordGeneratorLabController
                    .ResolveGeneratedCategory)
                .Distinct()
                .ToArray();

            Assert.That(categories,
                Is.EquivalentTo(Enum.GetValues(
                    typeof(UnifiedSwordCategory))));
        }

        [Test]
        public void OneControllerGeneratesEveryBladeFamilyOnOneSeedPath()
        {
            ShortSwordGeneratorLabController controller = CreateController(
                out ProceduralShortSwordGenerator shortSword,
                out ProceduralColumnBladeGenerator columnBlade);

            foreach (UnifiedSwordCategory expected in Enum.GetValues(
                         typeof(UnifiedSwordCategory)))
            {
                int seed = Enumerable.Range(1, 1024).First(candidate =>
                    ShortSwordGeneratorLabController.ResolveGeneratedCategory(
                        candidate) == expected);
                UnifiedSwordCategory actual = controller.Generate(seed);

                Assert.That(actual, Is.EqualTo(expected));
                bool column = expected >=
                    UnifiedSwordCategory.ColumnSquare;
                Assert.That(shortSword.gameObject.activeSelf, Is.Not.EqualTo(column));
                Assert.That(columnBlade.gameObject.activeSelf, Is.EqualTo(column));
                Assert.That(column
                        ? columnBlade.HasGeneratedSword
                        : shortSword.HasGeneratedSword,
                    Is.True);
            }
        }

        [Test]
        public void CategoryLockConstrainsLaterUnifiedGenerations()
        {
            ShortSwordGeneratorLabController controller = CreateController(
                out _,
                out ProceduralColumnBladeGenerator columnBlade);

            controller.ToggleCategoryLock(
                UnifiedSwordCategory.ColumnWideFlat);
            Assert.That(controller.Generate(11),
                Is.EqualTo(UnifiedSwordCategory.ColumnWideFlat));
            Assert.That(controller.Generate(912),
                Is.EqualTo(UnifiedSwordCategory.ColumnWideFlat));
            Assert.That(columnBlade.CurrentDefinition.ShapeCategory,
                Is.EqualTo(ColumnBladeShapeCategory.WideFlat));
        }

        [Test]
        public void FamilySpecificGenerationDoesNotDiscardChildLocks()
        {
            root = new GameObject("Short Sword");
            ProceduralShortSwordGenerator generator =
                root.AddComponent<ProceduralShortSwordGenerator>();
            ShortSwordGenerationBranchCatalog.TryGetGroup(
                ShortSwordGenerationDecision.GripColor,
                out ShortSwordGenerationBranchGroup colorGroup);
            // Lock a child option that is valid on this family branch.
            int compatibleColor = colorGroup.Options.First(option =>
                ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                    ShortSwordFamily.Piercer,
                    ShortSwordGenerationDecision.GripColor,
                    option.Value)).Value;
            generator.ToggleGenerationLock(
                ShortSwordGenerationDecision.GripColor,
                compatibleColor);

            ProceduralShortSwordDefinition definition =
                generator.GenerateForFamily(3221, ShortSwordFamily.Piercer);

            Assert.That(definition.Family, Is.EqualTo(ShortSwordFamily.Piercer));
            Assert.That(definition.GripColor,
                Is.EqualTo((ShortSwordGripColor)compatibleColor));
        }

        [Test]
        public void ShortSwordsUseTheColumnFurnitureProportionStandard()
        {
            const int seed = 2471;
            ProceduralShortSwordDefinition standard =
                ProceduralShortSwordGenerator.CreateDefinition(seed);
            ProceduralShortSwordDefinition source =
                ProceduralShortSwordGenerator.CreateDefinition(
                    seed,
                    null,
                    useColumnFurnitureStandard: false);
            float scale = ProceduralColumnBladeGenerator
                .ResolveFurnitureRadialScale(seed);

            Assert.That(standard.HandleRadius,
                Is.EqualTo(source.HandleRadius * scale).Within(0.000001f));
            Assert.That(standard.HiltRadius,
                Is.EqualTo(source.HiltRadius * scale).Within(0.000001f));
            Assert.That(standard.HandleProfile,
                Is.EqualTo(source.HandleProfile));
            Assert.That(standard.HiltProfile,
                Is.EqualTo(source.HiltProfile));
        }

        private ShortSwordGeneratorLabController CreateController(
            out ProceduralShortSwordGenerator shortSword,
            out ProceduralColumnBladeGenerator columnBlade)
        {
            root = new GameObject("Unified Sword Test");
            GameObject shortRoot = new GameObject("Short Branch");
            shortRoot.transform.SetParent(root.transform, false);
            shortSword = shortRoot.AddComponent<ProceduralShortSwordGenerator>();
            GameObject columnRoot = new GameObject("Column Branch");
            columnRoot.transform.SetParent(root.transform, false);
            columnBlade =
                columnRoot.AddComponent<ProceduralColumnBladeGenerator>();
            columnRoot.SetActive(false);
            ShortSwordGeneratorLabController controller =
                root.AddComponent<ShortSwordGeneratorLabController>();
            controller.Configure(
                shortSword,
                shortRoot.transform,
                columnBlade,
                columnRoot.transform);
            return controller;
        }
    }
}
