using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class LootItemSystemTests
    {
        [Test]
        public void EveryRaidStartsWithOneTwentyArrowStack()
        {
            GameSession session = CreateSession();

            RaidSession raid = session.BeginRaid(seedOverride: 41);

            StorageEntry[] arrows = session.ActiveProfile.InventoryEntryIds
                .Select(session.ActiveProfile.FindStorageEntry)
                .Where(entry =>
                    entry != null &&
                    entry.DefinitionId == ItemDefinitionIds.Arrow)
                .ToArray();
            Assert.That(arrows, Has.Length.EqualTo(1));
            Assert.That(arrows[0].Quantity, Is.EqualTo(20));
            Assert.That(
                raid.LaunchRequest.CarriedStorageEntryIds,
                Does.Contain(arrows[0].EntryId));
            Assert.That(
                raid.GetItemQuantity(
                    ItemDefinitionIds.Arrow,
                    session.ActiveProfile),
                Is.EqualTo(20));
        }

        [Test]
        public void LootedArrowsStackAndAreConsumedAuthoritatively()
        {
            GameSession session = CreateSession();
            RaidSession raid = session.BeginRaid(seedOverride: 42);
            raid.RecordLoot(
                StorageEntry.Create(ItemDefinitionIds.Arrow, 7),
                session.ActiveProfile);

            Assert.That(
                raid.GetItemQuantity(
                    ItemDefinitionIds.Arrow,
                    session.ActiveProfile),
                Is.EqualTo(27));
            Assert.That(
                raid.TryConsumeItem(
                    ItemDefinitionIds.Arrow,
                    27,
                    session.ActiveProfile),
                Is.True);
            Assert.That(
                raid.GetItemQuantity(
                    ItemDefinitionIds.Arrow,
                    session.ActiveProfile),
                Is.Zero);
        }

        [Test]
        public void InventoryEntriesKeepArbitrarySlotsAcrossProfileClone()
        {
            PlayerProfile profile = PlayerProfile.CreateNew("slot-profile");
            StorageEntry healthPack = StorageEntry.Create(
                ItemDefinitionIds.HealthPack);
            profile.AddToStorage(healthPack);

            Assert.That(
                profile.TryMoveToInventory(healthPack.EntryId, 17),
                Is.True);
            Assert.That(
                profile.GetInventoryEntryAtSlot(17)?.EntryId,
                Is.EqualTo(healthPack.EntryId));

            PlayerProfile clone = profile.Clone();
            Assert.That(
                clone.GetInventoryEntryAtSlot(17)?.EntryId,
                Is.EqualTo(healthPack.EntryId));
            Assert.That(clone.GetInventoryEntryAtSlot(0), Is.Null);
        }

        [Test]
        public void ArbitraryLFootprintRotatesAndValidatesEveryOccupiedTile()
        {
            Vector2Int[] lShape =
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2)
            };

            Vector2Int[] rotated =
                ItemDefinitionCatalog.RotateFootprint(lShape, 1);
            Assert.That(
                rotated,
                Is.EquivalentTo(new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0),
                    new Vector2Int(0, 1)
                }));
            Assert.That(
                ItemDefinitionCatalog.RotateFootprint(lShape, 4),
                Is.EquivalentTo(lShape));

            Assert.That(
                ItemGridPlacement.TryGetOccupiedSlots(
                    lShape,
                    5,
                    4,
                    4,
                    out int[] occupied),
                Is.True);
            Assert.That(occupied, Is.EquivalentTo(new[] { 5, 9, 13, 14 }));
            Assert.That(
                ItemGridPlacement.TryGetOccupiedSlots(
                    lShape,
                    9,
                    4,
                    4,
                    out _),
                Is.False);

            Vector2Int rotatedGrab =
                ItemDefinitionCatalog.RotateFootprintOffsetClockwise(
                    lShape,
                    new Vector2Int(0, 2));
            Assert.That(rotatedGrab, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void ItemQuarterTurnPersistsAcrossClones()
        {
            StorageEntry entry = StorageEntry.Create(
                ItemDefinitionIds.HealthPack);
            entry.RotateClockwise();
            entry.RotateClockwise();

            StorageEntry clone = entry.Clone();
            Assert.That(entry.RotationQuarterTurns, Is.EqualTo(2));
            Assert.That(clone.RotationQuarterTurns, Is.EqualTo(2));

            clone.RotateClockwise();
            clone.RotateClockwise();
            Assert.That(clone.RotationQuarterTurns, Is.Zero);
        }

        [Test]
        public void ExactPlacementKeepsSeparateArrowStacksAndCapsAtSixtyFour()
        {
            GameSession session = CreateSession();
            RaidSession raid = session.BeginRaid(seedOverride: 420);

            int firstMove = raid.TryAddCarried(
                StorageEntry.Create(ItemDefinitionIds.Arrow, 50),
                9,
                false,
                session.ActiveProfile);
            int secondMove = raid.TryAddCarried(
                StorageEntry.Create(ItemDefinitionIds.Arrow, 20),
                9,
                false,
                session.ActiveProfile);

            Assert.That(firstMove, Is.EqualTo(50));
            Assert.That(secondMove, Is.EqualTo(14));
            Assert.That(
                raid.GetCarriedEntryAtSlot(9, session.ActiveProfile).Quantity,
                Is.EqualTo(64));
            Assert.That(
                raid.GetCarriedEntryAtSlot(0, session.ActiveProfile).Quantity,
                Is.EqualTo(20));
            Assert.That(
                ItemDefinitionCatalog.MaximumStack(ItemDefinitionIds.Arrow),
                Is.EqualTo(64));
        }

        [Test]
        public void RightClickPrimitivesTakeLargerHalfAndPlaceOne()
        {
            GameSession session = CreateSession();
            RaidSession raid = session.BeginRaid(seedOverride: 421);
            Assert.That(
                raid.TryAddCarried(
                    StorageEntry.Create(ItemDefinitionIds.Arrow, 9),
                    8,
                    false,
                    session.ActiveProfile),
                Is.EqualTo(9));
            StorageEntry source = raid.GetCarriedEntryAtSlot(
                8,
                session.ActiveProfile);

            Assert.That(
                raid.TryTakeCarried(
                    source.EntryId,
                    5,
                    session.ActiveProfile,
                    out StorageEntry held),
                Is.True);
            Assert.That(held.Quantity, Is.EqualTo(5));
            Assert.That(
                raid.GetCarriedEntryAtSlot(8, session.ActiveProfile).Quantity,
                Is.EqualTo(4));
            Assert.That(
                raid.TryAddCarried(
                    StorageEntry.Create(
                        held.DefinitionId,
                        1,
                        held.CustomStateJson),
                    12,
                    false,
                    session.ActiveProfile),
                Is.EqualTo(1));
            Assert.That(
                raid.GetCarriedEntryAtSlot(12, session.ActiveProfile).Quantity,
                Is.EqualTo(1));
        }

        [Test]
        public void SmartTransferFillsExistingStackThenUsesAnotherSlot()
        {
            GameSession session = CreateSession();
            RaidSession raid = session.BeginRaid(seedOverride: 422);

            Assert.That(
                raid.TryAddCarried(
                    StorageEntry.Create(ItemDefinitionIds.Arrow, 50),
                    -1,
                    true,
                    session.ActiveProfile),
                Is.EqualTo(50));

            StorageEntry[] arrows = raid.GetCarriedEntries(
                    session.ActiveProfile)
                .Where(entry =>
                    entry.DefinitionId == ItemDefinitionIds.Arrow)
                .OrderByDescending(entry => entry.Quantity)
                .ToArray();
            Assert.That(arrows, Has.Length.EqualTo(2));
            Assert.That(arrows[0].Quantity, Is.EqualTo(64));
            Assert.That(arrows[1].Quantity, Is.EqualTo(6));
        }

        [Test]
        public void ExtractionPreservesSeparateStacksAndTheirSlots()
        {
            GameSession session = CreateSession();
            RaidSession raid = session.BeginRaid(seedOverride: 423);
            Assert.That(
                raid.TryAddCarried(
                    StorageEntry.Create(ItemDefinitionIds.Arrow, 5),
                    15,
                    false,
                    session.ActiveProfile),
                Is.EqualTo(5));

            session.CompleteActiveRaid(
                RaidCompletionReason.Extracted,
                out _);

            StorageEntry[] arrows = session.ActiveProfile.InventoryEntryIds
                .Select(session.ActiveProfile.FindStorageEntry)
                .Where(entry =>
                    entry != null &&
                    entry.DefinitionId == ItemDefinitionIds.Arrow)
                .ToArray();
            Assert.That(arrows, Has.Length.EqualTo(2));
            Assert.That(
                session.ActiveProfile.GetInventoryEntryAtSlot(0)?.Quantity,
                Is.EqualTo(20));
            Assert.That(
                session.ActiveProfile.GetInventoryEntryAtSlot(15)?.Quantity,
                Is.EqualTo(5));
        }

        [Test]
        public void ExtractedArrowLootMergesBackIntoOneCarriedStack()
        {
            GameSession session = CreateSession();
            RaidSession raid = session.BeginRaid(seedOverride: 43);
            raid.RecordLoot(
                StorageEntry.Create(ItemDefinitionIds.Arrow, 5),
                session.ActiveProfile);
            Assert.That(
                raid.TryConsumeItem(
                    ItemDefinitionIds.Arrow,
                    2,
                    session.ActiveProfile),
                Is.True);

            session.CompleteActiveRaid(
                RaidCompletionReason.Extracted,
                out _);

            StorageEntry[] arrows = session.ActiveProfile.InventoryEntryIds
                .Select(session.ActiveProfile.FindStorageEntry)
                .Where(entry =>
                    entry != null &&
                    entry.DefinitionId == ItemDefinitionIds.Arrow)
                .ToArray();
            Assert.That(arrows, Has.Length.EqualTo(1));
            Assert.That(arrows[0].Quantity, Is.EqualTo(23));
        }

        [Test]
        public void CorpsesAndCampChestsUseRequestedGridsAndLootRanges()
        {
            GameObject sourceObject = new GameObject("loot-source-test");
            try
            {
                RaidLootContainer source =
                    sourceObject.AddComponent<RaidLootContainer>();
                bool sawCorpseHealthPack = false;
                bool sawEmptyCorpseUtilitySlot = false;
                bool sawChestHealthPack = false;
                bool sawEmptyChestUtilitySlot = false;
                for (int seed = 0; seed < 64; seed++)
                {
                    source.ConfigureCorpse(null, seed);
                    Assert.That(source.Columns, Is.EqualTo(4));
                    Assert.That(source.Rows, Is.EqualTo(6));
                    StorageEntry corpseArrows = source.Entries.Single(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Arrow);
                    Assert.That(corpseArrows.Quantity, Is.InRange(1, 10));
                    bool corpseHasHealth = source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.HealthPack);
                    sawCorpseHealthPack |= corpseHasHealth;
                    sawEmptyCorpseUtilitySlot |= !corpseHasHealth;

                    source.ConfigureChest("Camp Chest", seed);
                    Assert.That(source.Columns, Is.EqualTo(4));
                    Assert.That(source.Rows, Is.EqualTo(4));
                    StorageEntry chestArrows = source.Entries.Single(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Arrow);
                    Assert.That(chestArrows.Quantity, Is.InRange(1, 20));
                    bool chestHasHealth = source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.HealthPack);
                    sawChestHealthPack |= chestHasHealth;
                    sawEmptyChestUtilitySlot |= !chestHasHealth;
                }

                Assert.That(sawCorpseHealthPack, Is.True);
                Assert.That(sawEmptyCorpseUtilitySlot, Is.True);
                Assert.That(sawChestHealthPack, Is.True);
                Assert.That(sawEmptyChestUtilitySlot, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
            }
        }

        [Test]
        public void EmptyRaidAmmoHidesTheNockedArrowAndRejectsAShot()
        {
            GameObject player = new GameObject("ammo-test-player");
            GameObject bowObject = new GameObject("ammo-test-bow");
            GameObject nockedArrow = new GameObject("ammo-test-nocked-arrow");
            GameObject systems = new GameObject("ammo-test-systems");
            try
            {
                player.tag = "Player";
                bowObject.transform.SetParent(player.transform, false);
                nockedArrow.transform.SetParent(bowObject.transform, false);
                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                RaidPrototypeController controller =
                    systems.AddComponent<RaidPrototypeController>();
                GameSession session = CreateSession();
                session.BeginRaid(seedOverride: 44);
                typeof(RaidPrototypeController)
                    .GetField(
                        "session",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(controller, session);

                bow.Configure(
                    null,
                    player.transform,
                    bowObject.transform,
                    nockedArrow.transform);
                bow.SetWeaponEquipped(true);
                Assert.That(nockedArrow.activeSelf, Is.True);
                for (int index = 0; index < 20; index++)
                {
                    Assert.That(controller.TryConsumePlayerArrow(), Is.True);
                }

                bow.SetWeaponEquipped(true);
                Assert.That(controller.ArrowCount, Is.Zero);
                Assert.That(bow.HasAmmunition, Is.False);
                Assert.That(bow.ArrowReady, Is.False);
                Assert.That(nockedArrow.activeSelf, Is.False);

                typeof(BowWeapon)
                    .GetMethod(
                        "FireArrow",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(bow, new object[] { 1f });
                Assert.That(bow.FiredArrowCount, Is.Zero);
                Assert.That(bow.LastFiredProjectile, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(systems);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void AcceptedDragTransferRemovesTheWorldItemAndAddsRaidLoot()
        {
            GameObject systems = new GameObject("loot-transfer-systems");
            GameObject sourceObject = new GameObject("loot-transfer-source");
            try
            {
                GameSession session = CreateSession();
                RaidSession raid = session.BeginRaid(seedOverride: 45);
                RaidPrototypeController controller =
                    systems.AddComponent<RaidPrototypeController>();
                typeof(RaidPrototypeController)
                    .GetField(
                        "session",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(controller, session);
                RaidLootContainer source =
                    sourceObject.AddComponent<RaidLootContainer>();
                source.ConfigureChest("Camp Chest", 45);
                StorageEntry arrows = source.Entries.Single(entry =>
                    entry.DefinitionId == ItemDefinitionIds.Arrow);
                int expectedQuantity = 20 + arrows.Quantity;

                Assert.That(
                    controller.TryTransferLoot(
                        source,
                        arrows,
                        out string message),
                    Is.True,
                    message);
                Assert.That(source.Contains(arrows.EntryId), Is.False);
                Assert.That(
                    raid.GetItemQuantity(
                        ItemDefinitionIds.Arrow,
                        session.ActiveProfile),
                    Is.EqualTo(expectedQuantity));
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(systems);
            }
        }

        private static GameSession CreateSession()
        {
            return new GameSession(
                GameLaunchContext.CreateRaidSandbox(
                    "loot-item-tests",
                    40),
                new MemoryPlayerProfileStore());
        }
    }

}
