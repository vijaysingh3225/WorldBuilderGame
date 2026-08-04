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
        }

        [Test]
        public void ExplicitSeedRegenerationReplacesThePreviousPreview()
        {
            EditorSceneManager.OpenScene(
                RaidMapReviewWindow.ReviewScenePath);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);

            generator.GenerateWithSeed(1701);
            Assert.That(generator.Seed, Is.EqualTo(1701));
            float habitatTotal = 0f;
            foreach (ProceduralRaidGenerator.ForestHabitat habitat in
                     System.Enum.GetValues(
                         typeof(ProceduralRaidGenerator.ForestHabitat)))
            {
                float percentage =
                    generator.DominantHabitatPercentage(habitat);
                Assert.That(percentage, Is.GreaterThan(1f), habitat.ToString());
                habitatTotal += percentage;
            }
            Assert.That(habitatTotal, Is.EqualTo(100f).Within(0.15f));

            generator.GenerateWithSeed(1702);
            Assert.That(generator.Seed, Is.EqualTo(1702));
            Assert.That(
                generator.transform.Cast<Transform>()
                    .Count(child =>
                        child.name.StartsWith("Generated Raid ")),
                Is.EqualTo(1));
        }
    }
}
