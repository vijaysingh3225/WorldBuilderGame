using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
            Assert.That(enemies, Has.Length.EqualTo(8));
            Assert.That(
                enemies.All(enemy => !enemy.enabled),
                Is.True,
                "Serialized raid enemies should remain inert until the runtime raid controller starts their patrols.");
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
                enemies.Min(
                    enemy => Vector3.Distance(
                        enemy.transform.position,
                        player.transform.position)),
                Is.GreaterThanOrEqualTo(20f));

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

        [UnityTest]
        public IEnumerator RaidArchersStartPatrollingWithBowOnlyLoadouts()
        {
            Open(GameplaySceneRegistry.RaidPrototypeScenePath);
            yield return new EnterPlayMode();
            yield return null;

            EnemyBrain[] enemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(enemies, Has.Length.EqualTo(8));
            Vector3[] startingPositions =
                enemies.Select(enemy => enemy.transform.position).ToArray();

            float patrolSampleAt = Time.time + 3f;
            while (Time.time < patrolSampleAt)
            {
                yield return null;
            }

            int movedEnemies = 0;
            Health playerHealth =
                GameObject.FindGameObjectWithTag("Player")
                    .GetComponent<Health>();
            string movementDiagnostics =
                $"playerHealth={playerHealth.Current:0.0},timeScale={Time.timeScale:0.0},playing={Application.isPlaying}; ";
            foreach (EnemyBrain enemy in enemies)
            {
                Assert.That(enemy.enabled, Is.True);
                Assert.That(enemy.IsActivated, Is.True);
                ThirdPersonMotor motor =
                    enemy.GetComponent<ThirdPersonMotor>();
                Assert.That(motor, Is.Not.Null);
                Assert.That(
                    motor.WalkSpeed,
                    Is.EqualTo(ThirdPersonMotor.DefaultWalkSpeed)
                        .Within(0.001f));

                TwoSlotWeaponPresenter loadout =
                    enemy.GetComponentInChildren<
                        TwoSlotWeaponPresenter>(true);
                Assert.That(loadout, Is.Not.Null);
                Assert.That(loadout.BowIsEquipped, Is.True);
                Assert.That(loadout.SwordIsVisible, Is.False);

                int index = System.Array.IndexOf(enemies, enemy);
                if (Vector3.Distance(
                        startingPositions[index],
                        enemy.transform.position) > 0.35f)
                {
                    movedEnemies++;
                }
                movementDiagnostics +=
                    $"{enemy.name}:state={enemy.CurrentState}," +
                    $"active={enemy.gameObject.activeInHierarchy}," +
                    $"motor={motor.enabled},ground={motor.HasGroundControl}," +
                    $"target={motor.TargetHorizontalSpeed:0.00}," +
                    $"speed={motor.HorizontalSpeed:0.00}," +
                    $"moved={Vector3.Distance(startingPositions[index], enemy.transform.position):0.00}; ";
            }

            Assert.That(
                movedEnemies,
                Is.GreaterThanOrEqualTo(4),
                "Most guards should be visibly progressing along their patrol routes after the initial pauses. " +
                movementDiagnostics);
            yield return new ExitPlayMode();
        }

        [Test]
        public void CombatLabRetainsDiagnosticsAndAddsSharedGrid()
        {
            Open(GameplaySceneRegistry.CombatLabScenePath);

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(player, Is.Not.Null);
            EnemyBrain[] trainingTargets =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(
                trainingTargets,
                Has.Length.EqualTo(6),
                "The expanded lab should retain the primary duel dummy and provide five additional ranged/elevated targets.");
            Assert.That(
                GameObject.Find(
                    "Environment/01 - Central Duel Yard"),
                Is.Not.Null);
            Assert.That(
                GameObject.Find(
                    "Environment/02 - Shooting Range"),
                Is.Not.Null);
            Assert.That(
                GameObject.Find(
                    "Environment/03 - Close Quarters Course"),
                Is.Not.Null);
            Assert.That(
                GameObject.Find(
                    "Environment/04 - Traversal And Elevation"),
                Is.Not.Null);

            GameObject firingLine = GameObject.Find(
                "Environment/02 - Shooting Range/" +
                "Shooting Range Firing Line");
            Assert.That(firingLine, Is.Not.Null);
            float[] expectedRanges = { 15f, 30f, 45f, 60f };
            for (int index = 0;
                 index < expectedRanges.Length;
                 index++)
            {
                GameObject target = GameObject.Find(
                    $"Ranged Training Targets/" +
                    $"Range Target - {expectedRanges[index]:0}m");
                Assert.That(target, Is.Not.Null);
                Assert.That(
                    target.transform.position.z -
                        firingLine.transform.position.z,
                    Is.EqualTo(
                        expectedRanges[index])
                        .Within(0.01f));
            }
            Assert.That(
                GameObject.Find(
                    "Ranged Training Targets/" +
                    "Elevated Target - 3m Platform")
                    .transform.position.y,
                Is.EqualTo(4f).Within(0.01f));

            Renderer labFloor = GameObject.Find(
                    "Environment/Lab Floor")
                .GetComponent<Renderer>();
            Assert.That(
                labFloor.bounds.size.x,
                Is.GreaterThanOrEqualTo(100f));
            Assert.That(
                labFloor.bounds.size.z,
                Is.GreaterThanOrEqualTo(115f));
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.CombatLab);
        }

        [UnityTest]
        public IEnumerator ExpandedCombatLabStartsWithPassiveTargets()
        {
            Open(GameplaySceneRegistry.CombatLabScenePath);
            yield return new EnterPlayMode();
            yield return null;

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            EnemyBrain[] trainingTargets =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.activeInHierarchy, Is.True);
            Assert.That(trainingTargets, Has.Length.EqualTo(6));
            Assert.That(
                trainingTargets.All(target =>
                    !target.IsActivated &&
                    target.CurrentState ==
                        EnemyBrain.EnemyState.Idle),
                Is.True,
                "Every extra range target must remain a passive diagnostic dummy until explicitly activated.");
            Assert.That(
                Camera.main,
                Is.Not.Null);

            yield return new ExitPlayMode();
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
            Material skybox = serialized.FindProperty("skyboxMaterial")
                .objectReferenceValue as Material;
            Assert.That(skybox, Is.Not.Null);
            Assert.That(
                skybox.shader.name,
                Is.EqualTo("Skybox/Panoramic"));
            Assert.That(
                AssetDatabase.GetAssetPath(
                    skybox.GetTexture("_MainTex")),
                Is.EqualTo(
                    "Assets/_Project/Art/Environment/Skybox/" +
                    "Sky129/sky_129_2k.png"));
            Assert.That(
                serialized.FindProperty("treeCount")
                    .intValue,
                Is.EqualTo(1500),
                "The Raid should exceed the original per-area tree density to break long forest sightlines.");
            Assert.That(
                serialized.FindProperty("grassCount").intValue,
                Is.EqualTo(128000));
            Assert.That(
                serialized.FindProperty("undergrowthCount").intValue,
                Is.EqualTo(2800));
            Assert.That(
                serialized.FindProperty("boulderCount").intValue,
                Is.EqualTo(192));
            Assert.That(
                serialized.FindProperty("trailStoneCount").intValue,
                Is.EqualTo(168));
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
