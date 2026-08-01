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
            Assert.That(
                Object.FindFirstObjectByType<HomeInventoryController>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindObjectsByType<HomeStorageChest>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(4));
            HomeStorageChest[] chests =
                Object.FindObjectsByType<HomeStorageChest>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .OrderBy(chest => chest.ChestId)
                    .ToArray();
            Assert.That(
                chests.Select(chest => chest.ChestId),
                Is.EqualTo(new[]
                {
                    "home-chest-1",
                    "home-chest-2",
                    "home-chest-3",
                    "home-chest-4"
                }));
            HomeGridOccupant[] chestOccupants =
                chests.Select(chest =>
                        chest.GetComponentInParent<
                            HomeGridOccupant>())
                    .ToArray();
            Assert.That(
                chestOccupants,
                Has.All.Not.Null);
            Assert.That(
                chestOccupants.Select(occupant =>
                    occupant.Cell.y),
                Has.All.EqualTo(
                    chestOccupants[0].Cell.y));
            Assert.That(
                chestOccupants.Select(occupant =>
                        occupant.Cell.x)
                    .OrderBy(value => value),
                Is.EqualTo(new[] { -4, -3, -2, -1 }));
            Assert.That(
                chests,
                Has.All.Matches<HomeStorageChest>(
                    chest =>
                        chest.GetComponentInParent<
                                HomeGridOccupant>()
                            .transform
                            .GetComponentInChildren<Renderer>(
                                true) != null));
            foreach (HomeStorageChest chest in chests)
            {
                Transform chestRoot =
                    chest.GetComponentInParent<
                            HomeGridOccupant>()
                        .transform;
                Renderer renderer =
                    chestRoot.GetComponentInChildren<Renderer>(
                        true);
                Assert.That(
                    renderer.bounds.min.y,
                    Is.EqualTo(0f).Within(0.04f));
                Assert.That(
                    renderer.bounds.size.x,
                    Is.LessThanOrEqualTo(2.2f));
                Assert.That(
                    renderer.bounds.size.z,
                    Is.LessThanOrEqualTo(1.7f));
                Assert.That(
                    renderer.bounds.size.y,
                    Is.GreaterThan(0.5f));
                GameObject source =
                    PrefabUtility
                        .GetCorrespondingObjectFromSource(
                            renderer.gameObject);
                Assert.That(source, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(source),
                    Does.EndWith(
                        "/Environment/Chest/Chest.fbx"));
            }
            Assert.That(
                Object.FindFirstObjectByType<HomeRaidDoor>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            HomeRaidDoor raidDoor =
                Object.FindFirstObjectByType<HomeRaidDoor>(
                    FindObjectsInactive.Include);
            HomeGridOccupant gateOccupant =
                raidDoor.GetComponentInParent<
                    HomeGridOccupant>();
            Assert.That(gateOccupant, Is.Not.Null);
            Assert.That(
                gateOccupant.Footprint,
                Is.EqualTo(new Vector2Int(3, 1)));
            HomeGridOccupant[] allOccupants =
                Object.FindObjectsByType<HomeGridOccupant>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(allOccupants, Has.Length.EqualTo(5));
            Vector2Int[] occupiedCells =
                allOccupants
                    .SelectMany(occupant =>
                        occupant.OccupiedCells())
                    .ToArray();
            Assert.That(
                occupiedCells.Distinct().Count(),
                Is.EqualTo(occupiedCells.Length),
                "Home grid occupants must not overlap cells.");
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
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
            Assert.That(enemies, Has.Length.EqualTo(12));
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

            AssertProceduralRaidGenerator(player, enemies);
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
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
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
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
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

        private static void AssertProceduralRaidGenerator(
            GameObject player,
            EnemyBrain[] enemies)
        {
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            Assert.That(generator, Is.Not.Null);
            SerializedObject serialized =
                new SerializedObject(generator);
            Assert.That(
                serialized.FindProperty("player")
                    .objectReferenceValue,
                Is.SameAs(player.transform));
            Assert.That(
                serialized.FindProperty("enemies")
                    .arraySize,
                Is.EqualTo(enemies.Length));
            Assert.That(
                serialized.FindProperty("treePrefabs")
                    .arraySize,
                Is.EqualTo(11),
                "Every supplied birch, broadleaf, and pine variant must drive runtime forest generation.");
            Assert.That(
                serialized.FindProperty("treeBarkMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("birchBarkMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("treeLeavesMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("pineLeavesMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("treeCount")
                    .intValue,
                Is.GreaterThanOrEqualTo(300));
            Assert.That(
                serialized.FindProperty("mapRadius")
                    .floatValue,
                Is.EqualTo(144f).Within(0.01f),
                "Doubling the old 72 m radius produces four times its playable area.");
            Assert.That(
                serialized.FindProperty("terrainResolution")
                    .intValue,
                Is.EqualTo(256),
                "The expanded disc should preserve the old terrain sampling scale.");
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
