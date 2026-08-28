using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class LootItemSystemTests
    {
        [Test]
        public void GridDividersFadeOnlyInsideOneMultiCellItem()
        {
            StorageEntry sword = StorageEntry.Create(
                ItemDefinitionIds.LootShortSword,
                1);
            StorageEntry anotherSword = StorageEntry.Create(
                ItemDefinitionIds.LootShortSword,
                1);
            StorageEntry arrows = StorageEntry.Create(
                ItemDefinitionIds.Arrow,
                12);

            Assert.That(
                HomeInventoryController.
                    AreCellsInsideSameMultiCellItem(sword, sword),
                Is.True,
                "Adjacent cells occupied by one sword should use the faded internal divider.");
            Assert.That(
                HomeInventoryController.
                    AreCellsInsideSameMultiCellItem(
                        sword,
                        anotherSword),
                Is.False,
                "Separate items must keep the divider between them.");
            Assert.That(
                HomeInventoryController.
                    AreCellsInsideSameMultiCellItem(arrows, arrows),
                Is.False,
                "A single-cell stack should preserve its normal cell border.");
            Assert.That(
                HomeInventoryController.MultiCellInternalDividerStrength,
                Is.InRange(0.10f, 0.20f),
                "The internal footprint grid should remain barely visible rather than disappearing or matching the full border.");
            Assert.That(
                HomeInventoryController.MultiCellInternalDividerColor,
                Is.Not.EqualTo(GameTypography.CellColor));
            Assert.That(
                HomeInventoryController.MultiCellInternalDividerColor,
                Is.Not.EqualTo(GameTypography.StorageBorderColor));
        }

        [Test]
        public void WeaponPreviewSnapshotsAreCompactGpuCachedImages()
        {
            GameObject owner = new GameObject(
                "weapon-preview-snapshot-test");
            var source = new RenderTexture(
                32,
                96,
                24,
                RenderTextureFormat.ARGB32);
            Texture2D probe = null;
            try
            {
                source.Create();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = source;
                GL.Clear(
                    true,
                    true,
                    new Color(0.72f, 0.28f, 0.12f, 1f));
                RenderTexture.active = previous;

                InventoryPreviewRenderer renderer =
                    owner.AddComponent<InventoryPreviewRenderer>();
                MethodInfo snapshotMethod =
                    typeof(InventoryPreviewRenderer).GetMethod(
                        "SnapshotItemPreview",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(snapshotMethod, Is.Not.Null);
                RenderTexture first = snapshotMethod.Invoke(
                    renderer,
                    new object[] { source, "test-sword-0" }) as RenderTexture;
                RenderTexture second = snapshotMethod.Invoke(
                    renderer,
                    new object[] { source, "test-sword-0" }) as RenderTexture;

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.SameAs(first));
                Assert.That(first.width, Is.EqualTo(16));
                Assert.That(first.height, Is.EqualTo(48));
                Assert.That(first.depth, Is.EqualTo(0));

                previous = RenderTexture.active;
                RenderTexture.active = first;
                probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                probe.ReadPixels(new Rect(8f, 24f, 1f, 1f), 0, 0);
                probe.Apply();
                RenderTexture.active = previous;
                Assert.That(probe.GetPixel(0, 0).a, Is.GreaterThan(0.95f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                source.Release();
                Object.DestroyImmediate(source);
                if (probe != null)
                {
                    Object.DestroyImmediate(probe);
                }
            }
        }

        [Test]
        public void LeftDragDistributionSplitsEveryItemAcrossVisitedCells()
        {
            int[] amounts = Enumerable.Range(0, 3)
                .Select(index =>
                    HomeInventoryController.
                        CalculateEvenDistributionAmount(
                            10,
                            3,
                            index))
                .ToArray();

            Assert.That(amounts, Is.EqualTo(new[] { 4, 3, 3 }));
            Assert.That(amounts.Sum(), Is.EqualTo(10));
            Assert.That(
                HomeInventoryController.
                    CalculateEvenDistributionAmount(2, 4, 3),
                Is.Zero,
                "Dragging across more cells than items must not create extra items.");
            Assert.That(
                HomeInventoryController.
                    CanAddEvenDistributionCell(2, 1),
                Is.True);
            Assert.That(
                HomeInventoryController.
                    CanAddEvenDistributionCell(2, 2),
                Is.False,
                "Every visibly selected cell must receive at least one item.");
        }

        [Test]
        public void ChestMaterialDefinitionsLoadTheirIconsAndStackLimits()
        {
            Assert.That(
                ItemDefinitionCatalog.LoadIcon(ItemDefinitionIds.IronIngot),
                Is.Not.Null);
            Assert.That(
                ItemDefinitionCatalog.LoadIcon(ItemDefinitionIds.Coal),
                Is.Not.Null);
            Assert.That(
                ItemDefinitionCatalog.LoadIcon(ItemDefinitionIds.CopperCoin),
                Is.Not.Null);
            Assert.That(
                ItemDefinitionCatalog.IsStackable(
                    ItemDefinitionIds.IronIngot),
                Is.False);
            Assert.That(
                ItemDefinitionCatalog.MaximumStack(
                    ItemDefinitionIds.IronIngot),
                Is.EqualTo(1));
            Assert.That(
                ItemDefinitionCatalog.MaximumStack(ItemDefinitionIds.Coal),
                Is.EqualTo(10));
            Assert.That(
                ItemDefinitionCatalog.MaximumStack(
                    ItemDefinitionIds.CopperCoin),
                Is.EqualTo(100));
        }

        [Test]
        public void RopeIsAStackableMaterialWithTwentyFourItemStacks()
        {
            Assert.That(
                ItemDefinitionCatalog.DisplayName(ItemDefinitionIds.Rope),
                Is.EqualTo("Rope"));
            Assert.That(
                ItemDefinitionCatalog.Category(ItemDefinitionIds.Rope),
                Is.EqualTo(ItemCategory.Material));
            Assert.That(
                ItemDefinitionCatalog.IsStackable(ItemDefinitionIds.Rope),
                Is.True);
            Assert.That(
                ItemDefinitionCatalog.MaximumStack(ItemDefinitionIds.Rope),
                Is.EqualTo(24));
            Assert.That(
                ItemDefinitionCatalog.LoadIcon(ItemDefinitionIds.Rope),
                Is.Not.Null,
                "The rope inventory icon must remain available through Resources.");
        }

        [Test]
        public void EveryDirectRaidSandboxStartsWithOneThirtyArrowStack()
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
            Assert.That(arrows[0].Quantity, Is.EqualTo(30));
            Assert.That(
                raid.LaunchRequest.CarriedStorageEntryIds,
                Does.Contain(arrows[0].EntryId));
            Assert.That(
                raid.GetItemQuantity(
                    ItemDefinitionIds.Arrow,
                    session.ActiveProfile),
                Is.EqualTo(30));
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
                Is.EqualTo(37));
            Assert.That(
                raid.TryConsumeItem(
                    ItemDefinitionIds.Arrow,
                    37,
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
        public void MultiCellGrabCannotWrapAcrossRowsWhenItsAnchorLeavesTheGrid()
        {
            Assert.That(
                ItemGridPlacement.TryCalculateAnchorSlot(
                    hoveredSlot: 4,
                    grabbedColumnOffset: 1,
                    grabbedRowOffset: 0,
                    columns: 4,
                    rows: 6,
                    out _),
                Is.False,
                "A left-edge hover cannot wrap a grabbed center/right tile into the preceding row.");

            Assert.That(
                ItemGridPlacement.TryCalculateAnchorSlot(
                    hoveredSlot: 5,
                    grabbedColumnOffset: 1,
                    grabbedRowOffset: 0,
                    columns: 4,
                    rows: 6,
                    out int validAnchor),
                Is.True);
            Assert.That(validAnchor, Is.EqualTo(4));
        }

        [Test]
        public void SmartAutoPlacementPreservesCurrentOrientationBeforeRotating()
        {
            StorageEntry sword = StorageEntry.Create(
                ItemDefinitionIds.LootShortSword);

            Assert.That(
                ItemGridPlacement.TryFindFirstAvailableSlotWithRotation(
                    new List<StorageEntry>(),
                    sword,
                    columns: 3,
                    rows: 3,
                    out int slot,
                    out int rotation),
                Is.True);
            Assert.That(slot, Is.Zero);
            Assert.That(rotation, Is.Zero);
            Assert.That(
                sword.RotationQuarterTurns,
                Is.Zero,
                "Searching alternate orientations must not mutate the source item.");
        }

        [Test]
        public void SmartAutoPlacementRotatesMultiCellItemsWhenOnlyRotatedFootprintsFit()
        {
            var emptyGrid = new List<StorageEntry>();
            StorageEntry sword = StorageEntry.Create(
                ItemDefinitionIds.LootShortSword);
            StorageEntry bow = StorageEntry.Create(
                ItemDefinitionIds.LootHuntingBow);

            Assert.That(
                ItemGridPlacement.TryFindFirstAvailableSlotWithRotation(
                    emptyGrid,
                    sword,
                    columns: 3,
                    rows: 2,
                    out int swordSlot,
                    out int swordRotation),
                Is.True);
            Assert.That(swordSlot, Is.Zero);
            Assert.That(swordRotation, Is.EqualTo(1));

            Assert.That(
                ItemGridPlacement.TryFindFirstAvailableSlotWithRotation(
                    emptyGrid,
                    bow,
                    columns: 3,
                    rows: 2,
                    out int bowSlot,
                    out int bowRotation),
                Is.True);
            Assert.That(bowSlot, Is.Zero);
            Assert.That(bowRotation, Is.EqualTo(1));
        }

        [Test]
        public void SmartAutoTransferStoresTheSelectedRotation()
        {
            GameObject sourceObject = new GameObject(
                "smart-auto-transfer-rotation-test");
            try
            {
                RaidLootContainer source =
                    sourceObject.AddComponent<RaidLootContainer>();
                const BindingFlags fields =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo columns = typeof(RaidLootContainer).GetField(
                    "columns",
                    fields);
                FieldInfo rows = typeof(RaidLootContainer).GetField(
                    "rows",
                    fields);
                Assert.That(columns, Is.Not.Null);
                Assert.That(rows, Is.Not.Null);
                columns.SetValue(source, 3);
                rows.SetValue(source, 2);

                int moved = source.TryAdd(
                    StorageEntry.Create(ItemDefinitionIds.LootShortSword),
                    -1,
                    true);

                Assert.That(moved, Is.EqualTo(1));
                StorageEntry stored = source.Entries.Single();
                Assert.That(stored.SlotIndex, Is.Zero);
                Assert.That(stored.RotationQuarterTurns, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
            }
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
                Is.EqualTo(30));
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
            Assert.That(arrows[1].Quantity, Is.EqualTo(16));
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
                Is.EqualTo(30));
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
            Assert.That(arrows[0].Quantity, Is.EqualTo(33));
        }

        [Test]
        public void CorpsesAndCampChestsUseRequestedGridsAndLootRanges()
        {
            GameObject sourceObject = new GameObject("loot-source-test");
            GameObject swordsmanObject = new GameObject(
                "swordsman-loot-source-test");
            try
            {
                RaidLootContainer source =
                    sourceObject.AddComponent<RaidLootContainer>();
                EnemyBrain swordsman =
                    swordsmanObject.AddComponent<EnemyBrain>();
                swordsman.ConfigureCampGuardLoadout(
                    EnemyBrain.WeaponLoadout.SwordOnly);
                bool sawCorpseHealthPack = false;
                bool sawEmptyCorpseUtilitySlot = false;
                bool sawCorpseCoins = false;
                bool sawCorpseWithoutCoins = false;
                bool sawChestHealthPack = false;
                bool sawEmptyChestUtilitySlot = false;
                bool sawChestIron = false;
                bool sawChestWithoutIron = false;
                bool sawChestCoal = false;
                bool sawChestWithoutCoal = false;
                bool sawChestCoins = false;
                bool sawChestWithoutCoins = false;
                bool sawChestArtifact = false;
                bool sawChestWithoutArtifact = false;
                bool sawChestWithOneArtifact = false;
                bool sawChestWithTwoArtifacts = false;
                bool sawOwlEyeSeal = false;
                bool sawWingedSeal = false;
                bool sawObsidianShard = false;
                bool sawChestLootAwayFromLeadingCells = false;
                for (int seed = 0; seed < 64; seed++)
                {
                    source.ConfigureCorpse(null, seed);
                    Assert.That(source.Columns, Is.EqualTo(4));
                    Assert.That(source.Rows, Is.EqualTo(6));
                    StorageEntry corpseArrows = source.Entries.Single(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Arrow);
                    Assert.That(corpseArrows.Quantity, Is.InRange(1, 10));
                    StorageEntry corpseBow = source.Entries.Single(entry =>
                        entry.DefinitionId == ItemDefinitionIds.LootHuntingBow);
                    Assert.That(corpseBow.Quantity, Is.EqualTo(1));
                    Assert.That(
                        ItemGridPlacement.TryGetOccupiedSlots(
                            ItemDefinitionCatalog.GetFootprint(
                                corpseBow.DefinitionId,
                                corpseBow.RotationQuarterTurns),
                            corpseBow.SlotIndex,
                            source.Columns,
                            source.Rows,
                            out int[] bowSlots),
                        Is.True);
                    Assert.That(bowSlots, Has.Length.EqualTo(6));
                    Assert.That(
                        bowSlots.All(slot =>
                            source.GetEntryAtSlot(slot)?.EntryId ==
                            corpseBow.EntryId),
                        Is.True,
                        "Every occupied bow cell must resolve to the guard's lootable weapon.");
                    Assert.That(
                        LootWeaponData.TryParse(
                            corpseBow.CustomStateJson,
                            out LootWeaponData bowData),
                        Is.True);
                    Assert.That(bowData.Level, Is.InRange(1, 5));
                    Assert.That(bowData.GridStateJson, Is.Not.Empty);
                    bool corpseHasHealth = source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.HealthPack);
                    sawCorpseHealthPack |= corpseHasHealth;
                    sawEmptyCorpseUtilitySlot |= !corpseHasHealth;
                    Assert.That(source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.IronIngot),
                        Is.False,
                        "Defeated AI must never carry chest-only iron.");
                    Assert.That(source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Coal),
                        Is.False,
                        "Defeated AI must never carry chest-only coal.");
                    StorageEntry corpseCoins = source.Entries.SingleOrDefault(
                        entry => entry.DefinitionId ==
                            ItemDefinitionIds.CopperCoin);
                    sawCorpseCoins |= corpseCoins != null;
                    sawCorpseWithoutCoins |= corpseCoins == null;
                    if (corpseCoins != null)
                    {
                        Assert.That(
                            corpseCoins.Quantity,
                            Is.InRange(
                                RaidLootContainer.GuardMinimumCoins,
                                RaidLootContainer.GuardMaximumCoins));
                    }

                    source.ConfigureCorpse(swordsman, seed);
                    StorageEntry corpseSword = source.Entries.Single(entry =>
                        entry.DefinitionId ==
                            ItemDefinitionIds.LootShortSword);
                    Assert.That(
                        ItemGridPlacement.TryGetOccupiedSlots(
                            ItemDefinitionCatalog.GetFootprint(
                                corpseSword.DefinitionId,
                                corpseSword.RotationQuarterTurns),
                            corpseSword.SlotIndex,
                            source.Columns,
                            source.Rows,
                            out int[] swordSlots),
                        Is.True);
                    Assert.That(swordSlots, Has.Length.EqualTo(3));
                    Assert.That(
                        swordSlots.All(slot =>
                            source.GetEntryAtSlot(slot)?.EntryId ==
                            corpseSword.EntryId),
                        Is.True,
                        "Every occupied sword cell must resolve to the swordsman's lootable weapon.");
                    Assert.That(
                        LootWeaponData.TryParse(
                            corpseSword.CustomStateJson,
                            out LootWeaponData swordData),
                        Is.True);
                    Assert.That(
                        source.SpawnedWeaponDefinitionId,
                        Is.EqualTo(ItemDefinitionIds.LootShortSword));
                    Assert.That(
                        source.SpawnedWeaponVisualSeed,
                        Is.EqualTo(swordData.VisualSeed),
                        "The loot payload must preserve the exact unrestricted " +
                        "short sword shown in the guard's hand.");

                    source.ConfigureChest("Camp Chest", seed);
                    Assert.That(source.SpawnedWeaponDefinitionId, Is.Empty);
                    Assert.That(source.Columns, Is.EqualTo(4));
                    Assert.That(source.Rows, Is.EqualTo(4));
                    StorageEntry chestArrows = source.Entries.Single(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Arrow);
                    Assert.That(chestArrows.Quantity, Is.InRange(1, 20));
                    bool chestHasHealth = source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.HealthPack);
                    sawChestHealthPack |= chestHasHealth;
                    sawEmptyChestUtilitySlot |= !chestHasHealth;
                    StorageEntry[] ingots = source.Entries.Where(entry =>
                            entry.DefinitionId == ItemDefinitionIds.IronIngot)
                        .ToArray();
                    StorageEntry coal = source.Entries.SingleOrDefault(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Coal);
                    StorageEntry coins = source.Entries.SingleOrDefault(entry =>
                        entry.DefinitionId == ItemDefinitionIds.CopperCoin);
                    sawChestIron |= ingots.Length > 0;
                    sawChestWithoutIron |= ingots.Length == 0;
                    sawChestCoal |= coal != null;
                    sawChestWithoutCoal |= coal == null;
                    sawChestCoins |= coins != null;
                    sawChestWithoutCoins |= coins == null;
                    StorageEntry[] artifacts = source.Entries.Where(entry =>
                            RaidLootContainer.ChestArtifactPool.Contains(
                                entry.DefinitionId))
                        .ToArray();
                    sawChestArtifact |= artifacts.Length > 0;
                    sawChestWithoutArtifact |= artifacts.Length == 0;
                    sawChestWithOneArtifact |= artifacts.Length == 1;
                    sawChestWithTwoArtifacts |= artifacts.Length == 2;
                    sawOwlEyeSeal |= artifacts.Any(entry =>
                        entry.DefinitionId ==
                            ItemDefinitionIds.OwlEyeSeal);
                    sawWingedSeal |= artifacts.Any(entry =>
                        entry.DefinitionId ==
                            ItemDefinitionIds.WingedSeal);
                    sawObsidianShard |= artifacts.Any(entry =>
                        entry.DefinitionId ==
                            ItemDefinitionIds.ObsidianShard);
                    sawChestLootAwayFromLeadingCells |=
                        source.Entries.Any(entry => entry.SlotIndex >= 8);
                    Assert.That(ingots.Length, Is.InRange(0, 3));
                    Assert.That(
                        ingots.All(entry => entry.Quantity == 1),
                        Is.True,
                        "Every non-stackable ingot needs its own cell.");
                    if (coal != null)
                    {
                        Assert.That(coal.Quantity, Is.InRange(1, 10));
                    }
                    if (coins != null)
                    {
                        Assert.That(coins.Quantity, Is.InRange(1, 10));
                    }
                    Assert.That(artifacts.Length, Is.InRange(0, 2));
                    Assert.That(
                        artifacts.All(artifact => artifact.Quantity == 1),
                        Is.True);
                    Assert.That(
                        source.Entries.Select(entry => entry.SlotIndex)
                            .Distinct()
                            .Count(),
                        Is.EqualTo(source.Entries.Count),
                        "Randomized chest placement must never overlap entries.");
                }

                Assert.That(sawCorpseHealthPack, Is.True);
                Assert.That(sawEmptyCorpseUtilitySlot, Is.True);
                Assert.That(sawCorpseCoins, Is.True);
                Assert.That(sawCorpseWithoutCoins, Is.True);
                Assert.That(sawChestHealthPack, Is.True);
                Assert.That(sawEmptyChestUtilitySlot, Is.True);
                Assert.That(sawChestIron, Is.True);
                Assert.That(sawChestWithoutIron, Is.True);
                Assert.That(sawChestCoal, Is.True);
                Assert.That(sawChestWithoutCoal, Is.True);
                Assert.That(sawChestCoins, Is.True);
                Assert.That(sawChestWithoutCoins, Is.True);
                Assert.That(sawChestArtifact, Is.True);
                Assert.That(sawChestWithoutArtifact, Is.True);
                Assert.That(sawChestWithOneArtifact, Is.True);
                Assert.That(sawChestWithTwoArtifacts, Is.True);
                Assert.That(sawOwlEyeSeal, Is.True);
                Assert.That(sawWingedSeal, Is.True);
                Assert.That(sawObsidianShard, Is.True);
                Assert.That(
                    sawChestLootAwayFromLeadingCells,
                    Is.True,
                    "Generated chest loot should be distributed across the grid rather than packed into its leading cells.");
                Assert.That(
                    RaidLootContainer.ChestArtifactChance,
                    Is.EqualTo(0.30f));
                Assert.That(
                    RaidLootContainer.ChestArtifactRollCount,
                    Is.EqualTo(2));
                Assert.That(
                    RaidLootContainer.ChestArtifactPool,
                    Is.EquivalentTo(new[]
                    {
                        ItemDefinitionIds.OwlEyeSeal,
                        ItemDefinitionIds.WingedSeal,
                        ItemDefinitionIds.ObsidianShard
                    }),
                    "Each successful 30% roll must select evenly from the " +
                    "complete three-artifact pool.");
                Assert.That(
                    ItemDefinitionCatalog.Category(ItemDefinitionIds.OwlEyeSeal),
                    Is.EqualTo(ItemCategory.Artifact));
                Assert.That(
                    ItemDefinitionCatalog.Category(ItemDefinitionIds.WingedSeal),
                    Is.EqualTo(ItemCategory.Artifact));
                Assert.That(
                    ItemDefinitionCatalog.Category(
                        ItemDefinitionIds.ObsidianShard),
                    Is.EqualTo(ItemCategory.Artifact));
                Assert.That(
                    ItemDefinitionCatalog.MaximumStack(ItemDefinitionIds.OwlEyeSeal),
                    Is.EqualTo(1));
                Assert.That(
                    ItemDefinitionCatalog.MaximumStack(ItemDefinitionIds.WingedSeal),
                    Is.EqualTo(1));
                Assert.That(
                    ItemDefinitionCatalog.MaximumStack(
                        ItemDefinitionIds.ObsidianShard),
                    Is.EqualTo(1));
                Assert.That(
                    ItemDefinitionCatalog.MaximumStack(
                        ItemDefinitionIds.IronIngot),
                    Is.EqualTo(1));
                Assert.That(
                    ItemDefinitionCatalog.MaximumStack(
                        ItemDefinitionIds.Coal),
                    Is.EqualTo(10));
                Assert.That(
                    ItemDefinitionCatalog.MaximumStack(
                        ItemDefinitionIds.CopperCoin),
                    Is.EqualTo(100));
                Assert.That(
                    ItemDefinitionCatalog.GetFootprint(
                        ItemDefinitionIds.LootShortSword,
                        0).Count,
                    Is.EqualTo(3));
                Assert.That(
                    ItemDefinitionCatalog.GetFootprint(
                        ItemDefinitionIds.LootHuntingBow,
                        0).Count,
                    Is.EqualTo(6));
                Assert.That(
                    ItemDefinitionCatalog.LoadIcon(
                        ItemDefinitionIds.OwlEyeSeal),
                    Is.Not.Null);
                Assert.That(
                    ItemDefinitionCatalog.LoadIcon(
                        ItemDefinitionIds.WingedSeal),
                    Is.Not.Null);
                Assert.That(
                    ItemDefinitionCatalog.LoadIcon(
                        ItemDefinitionIds.ObsidianShard),
                    Is.Not.Null);
                ArtifactDefinitionData wingedDefinition =
                    WeaponGridRuntime.CreateSandboxDefinitions()
                        .Single(definition => definition.DefinitionId ==
                            ItemDefinitionIds.WingedSeal);
                Assert.That(
                    wingedDefinition.Shape,
                    Is.EqualTo(new[] { GridCoordinate.Root }));
                Assert.That(
                    wingedDefinition.Modifiers.Any(modifier =>
                        modifier.Stat == ArtifactStat.MoveSpeed &&
                        Mathf.Approximately(modifier.Amount, 0.25f)),
                    Is.True);
                ArtifactDefinitionData obsidianDefinition =
                    WeaponGridRuntime.CreateSandboxDefinitions()
                        .Single(definition => definition.DefinitionId ==
                            ItemDefinitionIds.ObsidianShard);
                Assert.That(
                    obsidianDefinition.Shape,
                    Is.EqualTo(new[] { GridCoordinate.Root }));
                Assert.That(
                    obsidianDefinition.Modifiers.Any(modifier =>
                        modifier.Stat == ArtifactStat.Damage &&
                        Mathf.Approximately(modifier.Amount, 1f)),
                    Is.True);
                Assert.That(
                    ItemDefinitionCatalog.LoadBackpackIcon(),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(swordsmanObject);
            }
        }

        [Test]
        public void GuardsAndChestsEachRollTenPercentRopeLoot()
        {
            GameObject sourceObject = new GameObject("rope-loot-source-test");
            try
            {
                RaidLootContainer source =
                    sourceObject.AddComponent<RaidLootContainer>();
                int guardRopes = 0;
                int chestRopes = 0;
                const int sampleCount = 1000;
                for (int seed = 0; seed < sampleCount; seed++)
                {
                    source.ConfigureCorpse(null, seed);
                    guardRopes += source.Entries.Count(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Rope);

                    source.ConfigureChest("Camp Chest", seed);
                    chestRopes += source.Entries.Count(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Rope);
                }

                Assert.That(
                    RaidLootContainer.GuardRopeChance,
                    Is.EqualTo(0.10f));
                Assert.That(
                    RaidLootContainer.ChestRopeChance,
                    Is.EqualTo(0.10f));
                Assert.That(guardRopes, Is.InRange(70, 130));
                Assert.That(chestRopes, Is.InRange(70, 130));
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
                for (int index = 0; index < 30; index++)
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
                int expectedQuantity = 30 + arrows.Quantity;

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

        [Test]
        public void HideoutTransactionsSupportTheSamePickupSplitAndPlacementFlow()
        {
            PlayerProfile profile = PlayerProfile.CreateNew(
                "hideout-transaction-profile");
            StorageEntry arrows = StorageEntry.Create(
                ItemDefinitionIds.Arrow,
                9);
            profile.AddToStorage(arrows);
            Assert.That(
                profile.TryMoveToInventory(arrows.EntryId, 8),
                Is.True);
            StorageEntry source = profile.GetInventoryEntryAtSlot(8);

            Assert.That(
                ProfileInventoryTransactions.TryTakeInventory(
                    profile,
                    source,
                    5,
                    out StorageEntry held),
                Is.True);
            Assert.That(held.Quantity, Is.EqualTo(5));
            Assert.That(
                profile.GetInventoryEntryAtSlot(8).Quantity,
                Is.EqualTo(4));
            Assert.That(
                ProfileInventoryTransactions.TryAddChest(
                    profile,
                    PlayerProfile.DefaultChestId,
                    held,
                    12,
                    false),
                Is.EqualTo(5));
            Assert.That(
                profile.GetChestEntryIds(PlayerProfile.DefaultChestId)
                    .Select(profile.FindStorageEntry)
                    .Single(entry => entry.SlotIndex == 12)
                    .Quantity,
                Is.EqualTo(5));
        }

        [Test]
        public void HideoutTransactionsCanRearrangeAFullStackInPlace()
        {
            PlayerProfile profile = PlayerProfile.CreateNew(
                "hideout-rearrange-profile");
            StorageEntry health = StorageEntry.Create(
                ItemDefinitionIds.HealthPack,
                3);
            profile.AddToStorage(health);
            Assert.That(
                profile.TryMoveToInventory(health.EntryId, 2),
                Is.True);
            StorageEntry source = profile.GetInventoryEntryAtSlot(2);

            Assert.That(
                ProfileInventoryTransactions.TryTakeInventory(
                    profile,
                    source,
                    source.Quantity,
                    out StorageEntry held),
                Is.True);
            Assert.That(profile.GetInventoryEntryAtSlot(2), Is.Null);
            Assert.That(
                ProfileInventoryTransactions.TryAddInventory(
                    profile,
                    held,
                    17,
                    false),
                Is.EqualTo(3));
            Assert.That(
                profile.GetInventoryEntryAtSlot(17)?.EntryId,
                Is.EqualTo(health.EntryId));
        }

        [Test]
        public void LootFocusUsesInvisibleLowerTorsoPoint()
        {
            Vector3 point = LootInteractionPresentation.CalculateAimPoint(
                1920f,
                1080f);
            Assert.That(point.x, Is.EqualTo(960f));
            Assert.That(point.y, Is.EqualTo(464.4f).Within(0.001f));
        }

        [Test]
        public void LootFocusTracksThePlayersCenteredTorsoOnScreen()
        {
            GameObject cameraObject = new GameObject("loot-focus-camera");
            GameObject playerObject = new GameObject("loot-focus-player");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 1.6f, -8f);
                camera.transform.rotation = Quaternion.identity;
                playerObject.transform.position = new Vector3(-1.4f, 0f, 0f);
                CharacterController controller =
                    playerObject.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.center = Vector3.up;

                Vector3 torso = playerObject.transform.TransformPoint(
                    controller.center +
                    Vector3.up * controller.height *
                    LootInteractionPresentation.TorsoHeightOffset);
                Vector3 expectedViewport =
                    camera.WorldToViewportPoint(torso);
                Vector3 point = LootInteractionPresentation.CalculateAimPoint(
                    camera,
                    playerObject.transform,
                    1920f,
                    1080f);

                Assert.That(
                    point.x,
                    Is.EqualTo(expectedViewport.x * 1920f).Within(0.001f));
                Assert.That(
                    point.y,
                    Is.EqualTo(expectedViewport.y * 1080f).Within(0.001f));
                Assert.That(
                    point.x,
                    Is.LessThan(960f),
                    "A left-composed third-person player needs the loot " +
                    "cursor on the player, not at screen center to their right.");
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void LootFocusRequiresVeryCloseRange()
        {
            Assert.That(
                LootInteractionPresentation.DefaultDistance,
                Is.EqualTo(2.25f));
            Assert.That(
                LootInteractionPresentation.AimPointViewportY,
                Is.EqualTo(0.43f));
            Assert.That(
                LootInteractionPresentation.AimPointViewportX,
                Is.EqualTo(0.5f));
        }

        [Test]
        public void CorpseFocusCanHitVisibleRendererBetweenRagdollCapsules()
        {
            GameObject corpse = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            try
            {
                corpse.name = "corpse-renderer-fallback-test";
                corpse.transform.position = new Vector3(0f, 0f, 5f);
                Object.DestroyImmediate(corpse.GetComponent<Collider>());

                Assert.That(
                    LootInteractionPresentation.TryIntersectRendererBounds(
                        new Ray(Vector3.zero, Vector3.forward),
                        corpse.transform,
                        out float distance),
                    Is.True);
                Assert.That(distance, Is.EqualTo(4.5f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(corpse);
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
