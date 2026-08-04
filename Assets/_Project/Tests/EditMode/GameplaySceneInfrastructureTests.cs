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
        [TestCase(1920f, 1080f)]
        [TestCase(1454f, 676f)]
        [TestCase(1280f, 720f)]
        [TestCase(1024f, 768f)]
        public void InventoryPanelExactlyFillsCommonScreenSizes(
            float screenWidth,
            float screenHeight)
        {
            Rect panel = HomeInventoryController.CalculatePanelRect(
                screenWidth,
                screenHeight);

            Assert.That(panel, Is.EqualTo(
                new Rect(0f, 0f, screenWidth, screenHeight)));
        }

        [Test]
        public void InventoryUsesThreeEqualColumnsAndOneStorageCellSize()
        {
            const float screenWidth = 1500f;
            float spacing =
                HomeInventoryController.CalculateInventorySectionSpacing(
                    screenWidth);
            Rect content = new Rect(
                spacing +
                    HomeInventoryController.InventoryHorizontalAlignmentOffset,
                124f,
                screenWidth - spacing * 2f,
                554f);
            Rect equipment =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    0,
                    spacing);
            Rect backpack =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    1,
                    spacing);
            Rect loot =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    2,
                    spacing);

            Assert.That(equipment.width, Is.EqualTo(backpack.width));
            Assert.That(backpack.width, Is.EqualTo(loot.width));
            Assert.That(
                backpack.center.x,
                Is.EqualTo(
                    screenWidth * 0.5f +
                    HomeInventoryController.InventoryHorizontalAlignmentOffset));
            Assert.That(
                backpack.x - equipment.xMax,
                Is.EqualTo(spacing * 0.25f));
            Assert.That(
                loot.x - backpack.xMax,
                Is.EqualTo(spacing * 0.25f));
            Assert.That(
                backpack.center.x - equipment.center.x,
                Is.EqualTo(loot.center.x - backpack.center.x));

            float sharedCellSize =
                HomeInventoryController.CalculateSharedStorageCellSize(
                    backpack.width,
                    backpack.height);
            Assert.That(sharedCellSize, Is.GreaterThan(0f));
            Assert.That(
                sharedCellSize * 5f + 5f * 4f,
                Is.LessThanOrEqualTo(loot.width - 32f));
            Assert.That(
                sharedCellSize * 6f + 5f * 5f,
                Is.LessThanOrEqualTo(backpack.height - 56f));
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1454f, 676f)]
        [TestCase(1280f, 720f)]
        [TestCase(1024f, 768f)]
        public void WeaponGridWindowStaysInsideCommonScreenSizes(
            float screenWidth,
            float screenHeight)
        {
            Rect panel = WeaponGridSandboxToolkit.CalculateWindowRect(
                screenWidth,
                screenHeight);

            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(16f));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(16f));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(screenWidth - 16f));
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(screenHeight - 16f));
            Assert.That(panel.width, Is.LessThanOrEqualTo(900f));
            Assert.That(panel.height, Is.LessThanOrEqualTo(540f));
        }

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
            AssertInventoryLayout();
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
            ThirdPersonMotor playerMotor =
                player.GetComponent<ThirdPersonMotor>();
            Assert.That(playerMotor, Is.Not.Null);
            Assert.That(
                playerMotor.WalkSpeed,
                Is.EqualTo(ThirdPersonMotor.DefaultPlayerWalkSpeed)
                    .Within(0.001f));
            Assert.That(
                playerMotor.SprintSpeed,
                Is.EqualTo(ThirdPersonMotor.DefaultSprintSpeed)
                    .Within(0.001f));
            Material playerMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/Player.mat");
            Assert.That(playerMaterial, Is.Not.Null);
            Assert.That(
                playerMaterial.GetTexture("_BaseMap"),
                Is.Null);
            Assert.That(
                playerMaterial.GetTexture("_BumpMap"),
                Is.Null);
            Assert.That(
                Vector4.Distance(
                    playerMaterial.GetColor("_BaseColor"),
                    new Color(0.36f, 0.36f, 0.36f, 1f)),
                Is.LessThan(0.001f));
            EnemyBrain[] allRaidEnemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            EnemyBrain[] enemies = allRaidEnemies
                .Where(enemy => enemy.name.StartsWith("Raider "))
                .ToArray();
            Assert.That(enemies, Has.Length.EqualTo(8));
            Assert.That(
                allRaidEnemies.Count(
                    enemy => enemy.name.StartsWith(
                        "Camp Guard Pool ")),
                Is.EqualTo(9));
            foreach (EnemyBrain enemy in enemies)
            {
                SerializedObject perception =
                    new SerializedObject(enemy);
                Assert.That(
                    perception.FindProperty("passiveSightRange")
                        .floatValue,
                    Is.EqualTo(32f).Within(0.001f));
                Assert.That(
                    perception.FindProperty("passiveViewAngle")
                        .floatValue,
                    Is.EqualTo(100f).Within(0.001f));
                Assert.That(
                    perception.FindProperty("forestSightRange")
                        .floatValue,
                    Is.EqualTo(18f).Within(0.001f));
                Assert.That(
                    perception.FindProperty("runningHearingRange")
                        .floatValue,
                    Is.EqualTo(16f).Within(0.001f));
            }
            Assert.That(
                enemies.All(enemy => !enemy.enabled),
                Is.True,
                "Serialized raid enemies should remain inert until the runtime raid controller starts their patrols.");
            Assert.That(
                Object.FindObjectsByType<RaidPickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty,
                "The former floating Raid pickups should no longer exist.");
            RaidObelisk[] obelisks =
                Object.FindObjectsByType<RaidObelisk>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(obelisks, Has.Length.EqualTo(4));
            Assert.That(
                obelisks.Select(obelisk => obelisk.QuadrantIndex)
                    .Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(
                obelisks.Select(obelisk => obelisk.MonumentColor)
                    .Distinct().Count(),
                Is.EqualTo(4));
            foreach (RaidObelisk obelisk in obelisks)
            {
                Assert.That(obelisk.IsActivated, Is.False);
                Assert.That(
                    obelisk.GetComponent<BoxCollider>().isTrigger,
                    Is.True);
                Assert.That(
                    obelisk.GetComponentInChildren<MeshFilter>()
                        .sharedMesh.name,
                    Is.EqualTo("Raid Obelisk"));
                Assert.That(
                    obelisk.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty);
                SerializedObject serializedObelisk =
                    new SerializedObject(obelisk);
                Assert.That(
                    serializedObelisk.FindProperty(
                            "activatedEmissionMultiplier")
                        .floatValue,
                    Is.EqualTo(22f));
                Light glow =
                    obelisk.GetComponentInChildren<Light>(true);
                Assert.That(glow, Is.Not.Null);
                Assert.That(glow.enabled, Is.False);
                Assert.That(glow.intensity, Is.EqualTo(18f));
                Assert.That(glow.range, Is.EqualTo(20f));
            }
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
                crosshair.BowWeapon.ArrowFlybyClip,
                Is.Not.Null,
                "Raid arrows should carry the spatial flyby cue.");
            Assert.That(
                AssetDatabase.GetAssetPath(
                    crosshair.BowWeapon.ArrowFlybyClip),
                Is.EqualTo(
                    "Assets/_Project/Audio/SFX/Arrow Flyby.mp3"));
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            AssertInventoryLayout();
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.RaidSandbox);
        }

        [UnityTest]
        public IEnumerator RaidArchersStartPatrollingWithBowOnlyLoadouts()
        {
            Open(GameplaySceneRegistry.RaidPrototypeScenePath);
            yield return new EnterPlayMode();
            yield return null;

            EnemyBrain[] allRaidEnemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            EnemyBrain[] enemies = allRaidEnemies
                .Where(enemy => enemy.name.StartsWith("Raider "))
                .ToArray();
            Assert.That(enemies, Has.Length.EqualTo(8));
            EnemyBrain[] activeCampGuards = allRaidEnemies
                .Where(enemy =>
                    enemy.name.StartsWith("Camp Guard Pool ") &&
                    enemy.gameObject.activeSelf)
                .ToArray();
            Assert.That(activeCampGuards.Length, Is.InRange(1, 9));
            foreach (EnemyBrain campGuard in activeCampGuards)
            {
                Assert.That(
                    campGuard.ConfiguredWeaponLoadout,
                    Is.Not.EqualTo(
                        EnemyBrain.WeaponLoadout.Adaptive));
                TwoSlotWeaponPresenter campLoadout =
                    campGuard.GetComponentInChildren<
                        TwoSlotWeaponPresenter>(true);
                Assert.That(campLoadout, Is.Not.Null);
                if (campGuard.ConfiguredWeaponLoadout ==
                    EnemyBrain.WeaponLoadout.BowOnly)
                {
                    Assert.That(campLoadout.BowIsEquipped, Is.True);
                    Assert.That(campLoadout.BowIsVisible, Is.True);
                    Assert.That(campLoadout.SwordIsVisible, Is.False);
                }
                else
                {
                    Assert.That(campLoadout.BowIsEquipped, Is.False);
                    Assert.That(
                        campLoadout.BowIsVisible,
                        Is.False,
                        "Sword-only camp guards must not carry a bow on their back.");
                    Assert.That(campLoadout.SwordIsVisible, Is.True);
                }
            }
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
                Assert.That(loadout.BowIsVisible, Is.True);
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

        private static void AssertInventoryLayout()
        {
            Assert.That(
                Object.FindFirstObjectByType<HomeInventoryController>(
                    FindObjectsInactive.Include),
                Is.Not.Null,
                "Home and raid scenes should share the backpack-first Tab inventory.");
            WeaponGridSandboxToolkit toolkit =
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>(
                    FindObjectsInactive.Include);
            Assert.That(toolkit, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(toolkit);
            Assert.That(
                serialized.FindProperty("toggleWithTab").boolValue,
                Is.False,
                "Tab belongs to the inventory; weapon grids open from equipped weapon cards.");
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
                serialized.FindProperty("campGuardPool").arraySize,
                Is.EqualTo(9));
            string[] campPrefabFields =
            {
                "campTentPrefab",
                "campfirePrefab",
                "campPotPrefab",
                "campDryingRackPrefab",
                "campFirewoodPrefab",
                "campChestPrefab"
            };
            foreach (string field in campPrefabFields)
            {
                Assert.That(
                    serialized.FindProperty(field)
                        .objectReferenceValue,
                    Is.Not.Null,
                    field);
            }
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
            string[] habitatTextureFields =
            {
                "mossyLoamTexture",
                "canopyDuffTexture",
                "mossCarpetTexture",
                "creepingGroundcoverTexture",
                "stonyLichenSoilTexture"
            };
            foreach (string field in habitatTextureFields)
            {
                Texture2D texture = serialized.FindProperty(field)
                    .objectReferenceValue as Texture2D;
                Assert.That(texture, Is.Not.Null, field);
                Assert.That(
                    AssetDatabase.GetAssetPath(texture),
                    Does.StartWith(
                        "Assets/_Project/Art/Environment/" +
                        "RaidSurfaces/Forest"));
            }
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
                serialized.FindProperty("treeScaleMultiplier")
                    .floatValue,
                Is.EqualTo(1.75f).Within(0.001f));
            Assert.That(
                serialized.FindProperty("grassCount").intValue,
                Is.EqualTo(128000));
            Assert.That(
                serialized.FindProperty("undergrowthCount").intValue,
                Is.EqualTo(4200));
            Assert.That(
                serialized.FindProperty("groundFloraStudyPrefabs")
                    .arraySize,
                Is.EqualTo(GroundFloraStudyAssetBuilder.StudyCount));
            Assert.That(
                serialized.FindProperty("groundFloraStudyCount")
                    .intValue,
                Is.EqualTo(4800));
            Assert.That(
                serialized.FindProperty("groundFloraGeneralShare")
                    .floatValue,
                Is.EqualTo(0.70f).Within(0.001f));
            Assert.That(
                serialized.FindProperty("groundFloraTreePocketShare")
                    .floatValue,
                Is.EqualTo(0.18f).Within(0.001f));
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
