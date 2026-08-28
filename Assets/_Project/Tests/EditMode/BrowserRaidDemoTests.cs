using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class BrowserRaidDemoTests
    {
        [Test]
        public void BrowserBuildUsesAStableOutputAndRaidSeed()
        {
            Assert.That(
                BrowserRaidDemoBuild.DefaultOutputPath,
                Is.EqualTo("Artifacts/RaidBrowserDemo"));
            Assert.That(BrowserRaidDemoController.FixedRaidSeed, Is.EqualTo(30817));
        }

        [Test]
        public void BrowserGenerationReportsProgressAndUsesReducedBudgets()
        {
            EditorSceneManager.OpenScene(
                GameplaySceneRegistry.RaidPrototypeScenePath,
                OpenSceneMode.Single);
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>();
            Assert.That(generator, Is.Not.Null);

            generator.SetGenerationQuality(
                ProceduralRaidGenerator.GenerationQuality.BrowserDemo);
            var progress = new List<float>();
            IEnumerator routine = generator.GenerateStaged(
                (_, value) => progress.Add(value));
            while (routine.MoveNext())
            {
            }

            Assert.That(generator.IsGenerated, Is.True);
            Assert.That(generator.EffectiveTerrainResolution, Is.EqualTo(161));
            Assert.That(generator.EffectiveHabitatResolution, Is.EqualTo(81));
            Assert.That(generator.GeneratedTreeTarget, Is.EqualTo(600));
            Assert.That(progress, Has.Count.GreaterThan(5));
            Assert.That(progress[0], Is.EqualTo(0f));
            Assert.That(progress[^1], Is.EqualTo(1f));
            for (int index = 1; index < progress.Count; index++)
            {
                Assert.That(
                    progress[index],
                    Is.GreaterThanOrEqualTo(progress[index - 1]));
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }
    }
}
