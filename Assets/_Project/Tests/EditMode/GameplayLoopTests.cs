using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class GameplayLoopTests
    {
        [Test]
        public void InventoryMarksStoredItemsWithinFourBySixCapacity()
        {
            PlayerProfile profile =
                PlayerProfile.CreateNew("inventory-test");
            for (int index = 0;
                 index < PlayerProfile.InventoryCapacity + 1;
                 index++)
            {
                StorageEntry entry =
                    StorageEntry.Create($"artifact-{index}");
                profile.AddToStorage(entry);
                bool moved =
                    profile.TryMoveToInventory(entry.EntryId);
                Assert.That(
                    moved,
                    Is.EqualTo(
                        index < PlayerProfile.InventoryCapacity));
            }

            Assert.That(
                profile.InventoryEntryIds,
                Has.Count.EqualTo(24));
            string firstId = profile.InventoryEntryIds[0];
            Assert.That(profile.MoveToStorage(firstId), Is.True);
            Assert.That(profile.IsInInventory(firstId), Is.False);
        }

        [Test]
        public void HomeChestsKeepIndependentPersistentContents()
        {
            PlayerProfile profile =
                PlayerProfile.CreateNew("separate-chests");
            StorageEntry secondChestItem =
                StorageEntry.Create("artifact-second-chest");
            StorageEntry thirdChestItem =
                StorageEntry.Create("artifact-third-chest");
            profile.AddToStorage(secondChestItem);
            profile.AddToStorage(thirdChestItem);
            Assert.That(
                profile.TryMoveToInventory(
                    secondChestItem.EntryId),
                Is.True);
            Assert.That(
                profile.TryMoveToInventory(
                    thirdChestItem.EntryId),
                Is.True);

            Assert.That(
                profile.MoveToChest(
                    secondChestItem.EntryId,
                    "home-chest-2"),
                Is.True);
            Assert.That(
                profile.MoveToChest(
                    thirdChestItem.EntryId,
                    "home-chest-3"),
                Is.True);

            Assert.That(
                profile.GetChestEntryIds("home-chest-1"),
                Is.Empty);
            Assert.That(
                profile.GetChestEntryIds("home-chest-2"),
                Is.EqualTo(
                    new[] { secondChestItem.EntryId }));
            Assert.That(
                profile.GetChestEntryIds("home-chest-3"),
                Is.EqualTo(
                    new[] { thirdChestItem.EntryId }));
            Assert.That(
                profile.GetChestEntryIds("home-chest-4"),
                Is.Empty);

            var store =
                new JsonPlayerProfileStore(
                    temporaryDirectory);
            store.Save("separate-chests", profile);
            Assert.That(
                store.TryLoad(
                    "separate-chests",
                    out PlayerProfile reopened),
                Is.True);
            Assert.That(
                reopened.GetChestEntryIds("home-chest-2"),
                Is.EqualTo(
                    new[] { secondChestItem.EntryId }));
            Assert.That(
                reopened.GetChestEntryIds("home-chest-3"),
                Is.EqualTo(
                    new[] { thirdChestItem.EntryId }));
        }

        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "WorldBuilderGame-LoopTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            string systemTemporaryRoot = Path.GetFullPath(Path.GetTempPath());
            string resolvedTestDirectory = Path.GetFullPath(temporaryDirectory);
            if (resolvedTestDirectory.StartsWith(
                    systemTemporaryRoot,
                    StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(resolvedTestDirectory))
            {
                Directory.Delete(resolvedTestDirectory, recursive: true);
            }
        }

        [Test]
        public void RaidSandboxUsesMemoryOnlyNonPersistentState()
        {
            GameLaunchContext context =
                GameLaunchContext.CreateRaidSandbox("generation-test", 1427);
            MemoryPlayerProfileStore store = new MemoryPlayerProfileStore();

            GameSession session = new GameSession(context, store);

            Assert.That(context.Mode, Is.EqualTo(GameLaunchMode.RaidSandbox));
            Assert.That(context.IsSandbox, Is.True);
            Assert.That(context.PersistenceEnabled, Is.False);
            Assert.That(session.ProfileStore, Is.SameAs(store));
            Assert.That(session.ProfileStore.IsPersistent, Is.False);
            Assert.That(session.ActiveProfile, Is.Not.Null);
        }

        [Test]
        public void FreshAndContinueRoundTripThroughJsonStore()
        {
            const string SlotId = "round_trip";
            JsonPlayerProfileStore store =
                new JsonPlayerProfileStore(temporaryDirectory);
            GameSession fresh = new GameSession(
                GameLaunchContext.CreateFreshGame(SlotId),
                store);
            StorageEntry storedArtifact =
                StorageEntry.Create("artifact-health", 2, "{\"roll\":4}");
            fresh.ActiveProfile.AddToStorage(storedArtifact);
            fresh.ActiveProfile.WeaponOne.SetGridStateJson(
                "{\"weapon\":\"one\",\"columns\":4}");
            fresh.ActiveProfile.WeaponTwo.SetGridStateJson(
                "{\"weapon\":\"two\",\"columns\":5}");
            fresh.SaveProfile();

            GameSession continued = new GameSession(
                GameLaunchContext.CreateContinue(SlotId),
                new JsonPlayerProfileStore(temporaryDirectory));

            Assert.That(continued.ProfileStore.IsPersistent, Is.True);
            Assert.That(
                continued.ActiveProfile.ProfileId,
                Is.EqualTo(fresh.ActiveProfile.ProfileId));
            Assert.That(continued.ActiveProfile.Storage, Has.Count.EqualTo(1));
            Assert.That(
                continued.ActiveProfile.Storage[0].DefinitionId,
                Is.EqualTo("artifact-health"));
            Assert.That(continued.ActiveProfile.Storage[0].Quantity, Is.EqualTo(2));
            Assert.That(
                continued.ActiveProfile.GetChestEntryIds(
                    PlayerProfile.DefaultChestId),
                Is.EqualTo(
                    new[] { storedArtifact.EntryId }));
            Assert.That(
                continued.ActiveProfile.WeaponOne.GridStateJson,
                Is.EqualTo("{\"weapon\":\"one\",\"columns\":4}"));
            Assert.That(
                continued.ActiveProfile.WeaponTwo.GridStateJson,
                Is.EqualTo("{\"weapon\":\"two\",\"columns\":5}"));
        }

        [Test]
        public void FreshGameRequiresExplicitConfirmationBeforeReplacingSave()
        {
            const string SlotId = "protected_fresh";
            JsonPlayerProfileStore store =
                new JsonPlayerProfileStore(temporaryDirectory);
            GameSession original = new GameSession(
                GameLaunchContext.CreateFreshGame(SlotId),
                store);
            original.ActiveProfile.AddToStorage(
                StorageEntry.Create("artifact-keep-me"));
            original.SaveProfile();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new GameSession(
                        GameLaunchContext.CreateFreshGame(SlotId),
                        store));

            Assert.That(
                exception.Message,
                Does.Contain("Explicit overwrite confirmation"));
            Assert.That(store.TryLoad(SlotId, out PlayerProfile preserved), Is.True);
            Assert.That(
                preserved.Storage.Any(
                    entry => entry.DefinitionId == "artifact-keep-me"),
                Is.True);

            GameSession replacement = new GameSession(
                GameLaunchContext.CreateFreshGame(SlotId),
                store,
                allowProfileOverwrite: true);

            Assert.That(replacement.ActiveProfile.Storage, Is.Empty);
            Assert.That(store.Exists(SlotId), Is.True);
        }

        [Test]
        public void JsonStoreRecoversPreviousProfileFromBackup()
        {
            const string SlotId = "backup_recovery";
            JsonPlayerProfileStore store =
                new JsonPlayerProfileStore(temporaryDirectory);
            PlayerProfile profile = PlayerProfile.CreateNew(SlotId);
            profile.AddToStorage(StorageEntry.Create("artifact-first"));
            store.Save(SlotId, profile);
            profile.AddToStorage(StorageEntry.Create("artifact-second"));
            store.Save(SlotId, profile);

            string profilePath = Path.Combine(
                temporaryDirectory,
                SlotId + ".json");
            string backupPath = profilePath + ".bak";
            Assert.That(File.Exists(backupPath), Is.True);
            Assert.That(File.Exists(profilePath + ".tmp"), Is.False);
            File.WriteAllText(profilePath, string.Empty);

            Assert.That(store.TryLoad(SlotId, out PlayerProfile recovered), Is.True);
            Assert.That(
                recovered.Storage.Select(entry => entry.DefinitionId),
                Is.EquivalentTo(new[] { "artifact-first" }));
        }

        [Test]
        public void ExtractionAddsCollectedLootAndWeaponExperience()
        {
            GameSession session = CreateRaidSandboxSession();
            int initialWeaponOneExperience =
                session.ActiveProfile.WeaponOne.Experience;
            int initialWeaponTwoExperience =
                session.ActiveProfile.WeaponTwo.Experience;
            StorageEntry artifact = StorageEntry.Create("artifact-attack");
            RaidSession raid = session.BeginRaid(seedOverride: 9001);
            raid.RecordLoot(artifact);
            raid.RecordEnemyDefeated(2);
            raid.AddWeaponExperience(1, 7);
            raid.AddWeaponExperience(2, 4);

            RaidResult result = session.CompleteActiveRaid(
                RaidCompletionReason.Extracted,
                out RaidOutcomeReceipt receipt);

            Assert.That(result.Extracted, Is.True);
            Assert.That(result.EnemiesDefeated, Is.EqualTo(2));
            Assert.That(receipt.Persisted, Is.False);
            Assert.That(receipt.ItemsAdded, Is.EqualTo(1));
            Assert.That(
                session.ActiveProfile.Storage.Any(
                    entry => entry.EntryId == artifact.EntryId),
                Is.True);
            Assert.That(
                session.ActiveProfile.IsInInventory(
                    artifact.EntryId),
                Is.True);
            Assert.That(
                session.ActiveProfile.WeaponOne.Experience,
                Is.EqualTo(initialWeaponOneExperience + 7));
            Assert.That(
                session.ActiveProfile.WeaponTwo.Experience,
                Is.EqualTo(initialWeaponTwoExperience + 4));
        }

        [Test]
        public void RaidLootCannotExceedRemainingPlayerInventorySlots()
        {
            GameSession session = CreateRaidSandboxSession();
            for (int index = 0;
                 index < PlayerProfile.InventoryCapacity - 1;
                 index++)
            {
                StorageEntry carried =
                    StorageEntry.Create($"carried-{index}");
                session.ActiveProfile.AddToStorage(carried);
                Assert.That(
                    session.ActiveProfile.TryMoveToInventory(
                        carried.EntryId),
                    Is.True);
            }

            RaidSession raid = session.BeginRaid(
                carriedStorageEntryIds:
                    session.ActiveProfile.InventoryEntryIds);
            raid.RecordLoot(StorageEntry.Create("last-open-slot"));

            Assert.Throws<InvalidOperationException>(() =>
                raid.RecordLoot(
                    StorageEntry.Create("inventory-overflow")));
        }

        [Test]
        public void DeathRemovesCarriedStorageEntries()
        {
            GameSession session = CreateRaidSandboxSession();
            StorageEntry carriedArtifact = StorageEntry.Create("artifact-speed");
            StorageEntry safeArtifact = StorageEntry.Create("artifact-health");
            session.ActiveProfile.AddToStorage(carriedArtifact);
            session.ActiveProfile.AddToStorage(safeArtifact);
            RaidSession raid = session.BeginRaid(
                seedOverride: 17,
                carriedStorageEntryIds: new[] { carriedArtifact.EntryId });
            raid.RecordLoot(StorageEntry.Create("artifact-raid-loot"));

            RaidResult result = session.CompleteActiveRaid(
                RaidCompletionReason.PlayerDied,
                out RaidOutcomeReceipt receipt);

            Assert.That(result.PlayerDied, Is.True);
            Assert.That(result.ReturnedStorageEntries, Is.Empty);
            Assert.That(receipt.ItemsRemoved, Is.EqualTo(1));
            Assert.That(
                session.ActiveProfile.Storage.Any(
                    entry => entry.EntryId == carriedArtifact.EntryId),
                Is.False);
            Assert.That(
                session.ActiveProfile.Storage.Any(
                    entry => entry.EntryId == safeArtifact.EntryId),
                Is.True);
        }

        [Test]
        public void FailedRaidOutcomeRollsBackProfileAndReopensSameRaid()
        {
            ThrowOnceRaidOutcomeSink sink = new ThrowOnceRaidOutcomeSink();
            GameSession session = new GameSession(
                GameLaunchContext.CreateRaidSandbox("transaction-test", 500),
                new MemoryPlayerProfileStore(),
                outcomeSink: sink);
            PlayerProfile profileReference = session.ActiveProfile;
            WeaponInstanceRecord weaponOneReference =
                profileReference.WeaponOne;
            int initialExperience = weaponOneReference.Experience;
            RaidSession raid = session.BeginRaid(seedOverride: 501);
            raid.RecordLoot(StorageEntry.Create("artifact-transactional"));
            raid.AddWeaponExperience(1, 9);

            Assert.Throws<IOException>(() =>
                session.CompleteActiveRaid(
                    RaidCompletionReason.Extracted,
                    out _));

            Assert.That(session.ActiveProfile, Is.SameAs(profileReference));
            Assert.That(
                session.ActiveProfile.WeaponOne,
                Is.SameAs(weaponOneReference));
            Assert.That(session.ActiveRaid, Is.SameAs(raid));
            Assert.That(session.HasActiveRaid, Is.True);
            Assert.That(raid.IsActive, Is.True);
            Assert.That(session.ActiveProfile.Storage, Is.Empty);
            Assert.That(
                session.ActiveProfile.WeaponOne.Experience,
                Is.EqualTo(initialExperience));

            session.CompleteActiveRaid(
                RaidCompletionReason.Extracted,
                out RaidOutcomeReceipt receipt);

            Assert.That(receipt.ItemsAdded, Is.EqualTo(1));
            Assert.That(session.HasActiveRaid, Is.False);
            Assert.That(
                session.ActiveProfile.Storage.Count(
                    entry =>
                        entry.DefinitionId == "artifact-transactional"),
                Is.EqualTo(1));
            Assert.That(
                session.ActiveProfile.WeaponOne.Experience,
                Is.EqualTo(initialExperience + 9));
        }

        [Test]
        public void PlayerProfileExposesExactlyTwoWeaponSlots()
        {
            PlayerProfile profile = PlayerProfile.CreateNew("two-weapons");
            PropertyInfo[] weaponProperties = typeof(PlayerProfile)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property =>
                    property.PropertyType == typeof(WeaponInstanceRecord))
                .ToArray();

            Assert.That(
                weaponProperties.Select(property => property.Name),
                Is.EquivalentTo(new[] { "WeaponOne", "WeaponTwo" }));
            Assert.That(profile.WeaponOne, Is.SameAs(profile.GetWeapon(1)));
            Assert.That(profile.WeaponTwo, Is.SameAs(profile.GetWeapon(2)));
            Assert.That(
                profile.WeaponOne.WeaponInstanceId,
                Is.Not.EqualTo(profile.WeaponTwo.WeaponInstanceId));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.GetWeapon(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.GetWeapon(3));
        }

        [Test]
        public void PlayerDeathDoesNotShowRaidCompletionOverlay()
        {
            Assert.That(
                RaidPrototypeController.ShouldShowCompletionOverlay(
                    RaidCompletionReason.PlayerDied),
                Is.False);
            Assert.That(
                RaidPrototypeController.ShouldShowCompletionOverlay(
                    RaidCompletionReason.Extracted),
                Is.True);
            Assert.That(
                RaidPrototypeController.ShouldShowCompletionOverlay(
                    RaidCompletionReason.Abandoned),
                Is.True);
        }

        private static GameSession CreateRaidSandboxSession()
        {
            return new GameSession(
                GameLaunchContext.CreateRaidSandbox("loop-tests", 1234),
                new MemoryPlayerProfileStore());
        }

        private sealed class ThrowOnceRaidOutcomeSink : IRaidOutcomeSink
        {
            private readonly MemoryRaidOutcomeSink inner =
                new MemoryRaidOutcomeSink();
            private bool shouldThrow = true;

            public bool PersistsToDisk => false;

            public RaidOutcomeReceipt Apply(
                RaidResult result,
                PlayerProfile profile)
            {
                RaidOutcomeReceipt receipt = inner.Apply(result, profile);
                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new IOException("Simulated outcome persistence failure.");
                }

                return receipt;
            }
        }
    }
}
