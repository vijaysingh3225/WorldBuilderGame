using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class RaidMapReviewTests
    {
        [SetUp]
        public void EnsureReviewSceneExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    RaidMapReviewWindow.ReviewScenePath) == null)
            {
                RaidMapReviewWindow.BuildReviewSceneFromCommandLine();
            }
        }

        [Test]
        public void ReviewSceneUsesProductionGeneratorAndKeepsGameplayInert()
        {
            EditorSceneManager.OpenScene(
                RaidMapReviewWindow.ReviewScenePath);

            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            Assert.That(generator, Is.Not.Null);
            Assert.That(generator.enabled, Is.False);
            Assert.That(generator.SkyboxMaterial, Is.Not.Null);
            Assert.That(
                RenderSettings.skybox,
                Is.SameAs(generator.SkyboxMaterial));
            Assert.That(
                Object.FindObjectsByType<EnemyBrain>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .All(enemy => !enemy.gameObject.activeInHierarchy),
                Is.True);
            Assert.That(
                Object.FindObjectsByType<Camera>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .All(camera => !camera.gameObject.activeInHierarchy),
                Is.True);
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(RaidMapReviewWindow.ReviewScenePath));
            Assert.That(
                generator.transform.Cast<Transform>()
                    .Any(child =>
                        child.name.StartsWith("Generated Raid ")),
                Is.False,
                "The saved review scene must stay as a small template.");
        }

        [Test]
        public void ExplicitSeedRegenerationReplacesThePreviousPreview()
        {
            EditorSceneManager.OpenScene(
                RaidMapReviewWindow.ReviewScenePath);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);

            generator.SetGenerationQuality(
                ProceduralRaidGenerator.GenerationQuality.FastPreview);
            Assert.That(
                generator.EffectiveGrassCount,
                Is.LessThan(320000));
            generator.GenerateWithSeed(1701);
            Assert.That(generator.Seed, Is.EqualTo(1701));
            float habitatTotal = 0f;
            int representedHabitats = 0;
            foreach (ProceduralRaidGenerator.ForestHabitat habitat in
                     System.Enum.GetValues(
                         typeof(ProceduralRaidGenerator.ForestHabitat)))
            {
                float percentage =
                    generator.DominantHabitatPercentage(habitat);
                if (percentage > 1f)
                {
                    representedHabitats++;
                }
                habitatTotal += percentage;
            }
            Assert.That(habitatTotal, Is.EqualTo(100f).Within(0.15f));
            Assert.That(
                representedHabitats,
                Is.GreaterThanOrEqualTo(3),
                "Fast Preview should retain broad habitat variety without " +
                "claiming production-density canopy coverage.");

            generator.GenerateWithSeed(1702);
            Assert.That(generator.Seed, Is.EqualTo(1702));
            Assert.That(
                generator.transform.Cast<Transform>()
                    .Count(child =>
                        child.name.StartsWith("Generated Raid ")),
                Is.EqualTo(1));
            Transform previewRoot = generator.transform.Cast<Transform>()
                .Single(child =>
                    child.name.StartsWith("Generated Raid "));
            Assert.That(
                (previewRoot.gameObject.hideFlags &
                 HideFlags.DontSaveInEditor) != 0,
                Is.True);
            Assert.That(
                generator.EnsureGeneratedWithSeed(1702),
                Is.False,
                "A same-seed raid reset must retain immutable environment " +
                "geometry instead of rebuilding it.");
            Assert.That(
                generator.transform.Cast<Transform>()
                    .Single(child =>
                        child.name.StartsWith("Generated Raid ")),
                Is.SameAs(previewRoot));
        }

        [Test]
        public void TreeVisibilityToggleHidesOnlyTheGeneratedForest()
        {
            EditorSceneManager.OpenScene(
                RaidMapReviewWindow.ReviewScenePath);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            generator.SetGenerationQuality(
                ProceduralRaidGenerator.GenerationQuality.FastPreview);
            Transform forest =
                RaidMapReviewWindow.FindGeneratedForestRoot(generator);
            if (forest == null)
            {
                generator.GenerateWithSeed(20260730);
                forest = RaidMapReviewWindow.FindGeneratedForestRoot(
                    generator);
            }

            Assert.That(forest, Is.Not.Null);
            try
            {
                RaidMapReviewWindow.SetTreeVisibilityForReview(
                    generator,
                    false);
                Assert.That(
                    SceneVisibilityManager.instance.IsHidden(
                        forest.gameObject),
                    Is.True);
                Assert.That(
                    forest.gameObject.activeSelf,
                    Is.True,
                    "Review visibility must not change the saved map or production forest state.");
            }
            finally
            {
                RaidMapReviewWindow.SetTreeVisibilityForReview(
                    generator,
                    true);
            }

            Assert.That(
                SceneVisibilityManager.instance.IsHidden(
                    forest.gameObject),
                Is.False);
        }
    }
}
