using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class GameplaySceneInfrastructureTests
    {
        [Test]
        public void BuildSettingsKeepEveryPrototypeSceneInLoopOrder()
        {
            string[] paths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(
                paths.Take(4),
                Is.EqualTo(new[]
                {
                    GameplaySceneRegistry.BootstrapScenePath,
                    GameplaySceneRegistry.HomeBaseScenePath,
                    GameplaySceneRegistry.RaidPrototypeScenePath,
                    GameplaySceneRegistry.CombatLabScenePath
                }));
        }

        [Test]
        public void BootstrapSceneProvidesLaunchMenuWithoutAutoStarting()
        {
            Open(GameplaySceneRegistry.BootstrapScenePath);

            GameplayLoopBootstrap bootstrap =
                Object.FindFirstObjectByType<GameplayLoopBootstrap>(
                    FindObjectsInactive.Include);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<BootstrapMenuController>(
                    FindObjectsInactive.Include),
                Is.Not.Null);

            SerializedObject serialized =
                new SerializedObject(bootstrap);
            Assert.That(
                serialized.FindProperty("initializeOnAwake").boolValue,
                Is.False);
        }

        [Test]
        public void HomeBaseContainsPlayerStorageLoopAndSharedGrid()
        {
            Open(GameplaySceneRegistry.HomeBaseScenePath);

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(
                player,
                Is.Not.Null);
            HomeBaseController homeBase =
                Object.FindFirstObjectByType<HomeBaseController>(
                    FindObjectsInactive.Include);
            Assert.That(homeBase, Is.Not.Null);
            SerializedObject serialized =
                new SerializedObject(homeBase);
            Assert.That(
                serialized.FindProperty("playerInput")
                    .objectReferenceValue,
                Is.SameAs(player.GetComponent<PlayerInputSource>()));
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.HomeSandbox);
        }

        [Test]
        public void RaidPrototypeContainsEnemiesLootExtractionAndSharedGrid()
        {
            Open(GameplaySceneRegistry.RaidPrototypeScenePath);

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(player, Is.Not.Null);
            EnemyBrain[] enemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(enemies, Has.Length.EqualTo(3));
            Assert.That(
                enemies.All(enemy => !enemy.enabled),
                Is.True,
                "Raid enemies should remain inert until proximity activation.");
            Assert.That(
                Object.FindObjectsByType<RaidPickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(3));
            Assert.That(
                Object.FindFirstObjectByType<ExtractionZone>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            RaidPrototypeController controller =
                Object.FindFirstObjectByType<RaidPrototypeController>(
                    FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            Assert.That(
                controller.EnemyActivationRadius,
                Is.EqualTo(18f).Within(0.01f));
            Assert.That(
                enemies.Min(
                    enemy => Vector3.Distance(
                        enemy.transform.position,
                        player.transform.position)),
                Is.GreaterThan(controller.EnemyActivationRadius));

            AssertExactHillCollision("West Hill");
            AssertExactHillCollision("East Hill");
            AssertRaidTreeCover();
            BowAimCrosshairPresenter crosshair =
                Object.FindFirstObjectByType<BowAimCrosshairPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(
                crosshair,
                Is.Not.Null,
                "Raid bow aiming should use the shared crosshair presenter.");
            Assert.That(
                crosshair.BowWeapon,
                Is.SameAs(
                    player.GetComponentInChildren<BowWeapon>(true)));
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.RaidSandbox);
        }

        [Test]
        public void CombatLabRetainsDiagnosticsAndAddsSharedGrid()
        {
            Open(GameplaySceneRegistry.CombatLabScenePath);

            Assert.That(
                GameObject.FindGameObjectWithTag("Player"),
                Is.Not.Null);
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.CombatLab);
        }

        private static void AssertSharedGrid()
        {
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridRuntime>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridProfileBinding>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridCombatBridge>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
        }

        private static void AssertDirectMode(GameLaunchMode expected)
        {
            GameplayLoopBootstrap bootstrap =
                Object.FindFirstObjectByType<GameplayLoopBootstrap>(
                    FindObjectsInactive.Include);
            Assert.That(bootstrap, Is.Not.Null);
            SerializedObject serialized =
                new SerializedObject(bootstrap);
            Assert.That(
                serialized.FindProperty("directSceneLaunchMode")
                    .enumValueIndex,
                Is.EqualTo((int)expected));
        }

        private static void AssertExactHillCollision(string hillName)
        {
            GameObject hill = GameObject.Find(hillName);
            Assert.That(hill, Is.Not.Null);
            Assert.That(
                hill.GetComponent<SphereCollider>(),
                Is.Null,
                $"{hillName} should not keep the primitive sphere collider.");
            MeshCollider collider =
                hill.GetComponent<MeshCollider>();
            MeshFilter filter = hill.GetComponent<MeshFilter>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(filter, Is.Not.Null);
            Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
            Assert.That(collider.convex, Is.False);
        }

        private static void AssertRaidTreeCover()
        {
            Transform[] coverTrees =
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Where(transform =>
                        transform.name.StartsWith("Cover Tree "))
                    .ToArray();
            Assert.That(
                coverTrees,
                Has.Length.GreaterThanOrEqualTo(24),
                "The raid route needs frequent hard cover against ranged AI.");

            for (int index = 0; index < coverTrees.Length; index++)
            {
                Transform trunk =
                    coverTrees[index].Find("Trunk");
                Assert.That(
                    trunk,
                    Is.Not.Null,
                    $"{coverTrees[index].name} is missing its cover trunk.");
                MeshFilter filter =
                    trunk.GetComponent<MeshFilter>();
                MeshCollider collider =
                    trunk.GetComponent<MeshCollider>();
                MeshRenderer renderer =
                    trunk.GetComponent<MeshRenderer>();
                Assert.That(filter, Is.Not.Null);
                Assert.That(collider, Is.Not.Null);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(
                    collider.sharedMesh,
                    Is.SameAs(filter.sharedMesh));
                Assert.That(
                    filter.sharedMesh.vertexCount,
                    Is.LessThanOrEqualTo(120),
                    "Tree trunks should keep a clearly faceted low-poly mesh.");
                Assert.That(
                    renderer.bounds.size.y,
                    Is.GreaterThanOrEqualTo(7f));
                Assert.That(
                    Mathf.Min(
                        renderer.bounds.size.x,
                        renderer.bounds.size.z),
                    Is.GreaterThanOrEqualTo(2.2f),
                    "Tree trunks must be wide enough to function as cover.");
            }
        }

        private static Scene Open(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            return scene;
        }
    }
}
