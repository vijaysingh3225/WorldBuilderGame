using NUnit.Framework;
using System.Linq;
using UnityEngine;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class ArtifactInstallationServiceTests
    {
        [Test]
        public void ForgeUsesSharedCellsAndClampedWeaponGridZoom()
        {
            Assert.That(HomeAnvil.ForgeCellSize, Is.EqualTo(52f));
            Assert.That(
                HomeAnvil.CalculateGridZoom(1f, -100f),
                Is.EqualTo(HomeAnvil.MaximumGridZoom));
            Assert.That(
                HomeAnvil.CalculateGridZoom(1f, 100f),
                Is.EqualTo(HomeAnvil.MinimumGridZoom));
        }

        [Test]
        public void ForgeUsesMoreScreenHeightAndScrollsLongStats()
        {
            Rect content = HomeAnvil.CalculateForgeContentRect(
                new Rect(0f, 0f, 1483f, 687f));

            Assert.That(content.y, Is.LessThan(80f));
            Assert.That(content.height, Is.GreaterThan(560f));
            Assert.That(content.yMax, Is.LessThan(687f));
            Assert.That(
                HomeAnvil.ShouldScrollWeaponDetails(430f, 0),
                Is.True);
            Assert.That(
                HomeAnvil.ShouldScrollWeaponDetails(700f, 0),
                Is.False);
            Assert.That(
                HomeAnvil.ShouldScrollWeaponDetails(600f, 8),
                Is.True);
        }

        [Test]
        public void ForgeGivesTheWeaponWorkspaceMostOfTheWidth()
        {
            const float contentWidth = 1200f;

            float libraryWidth = HomeAnvil.CalculateArtifactLibraryWidth(
                contentWidth);

            Assert.That(
                libraryWidth,
                Is.EqualTo(contentWidth *
                    HomeAnvil.ArtifactLibraryWidthFraction));
            Assert.That(libraryWidth, Is.LessThan(contentWidth / 3f));
            Assert.That(
                HomeAnvil.CalculateArtifactLibraryWidth(400f),
                Is.EqualTo(190f));
        }

        [Test]
        public void NewHomeSandboxStartsWithThirtyArrows()
        {
            var session = new GameSession(
                GameLaunchContext.CreateHomeSandbox("home-arrows"),
                new MemoryPlayerProfileStore());

            StorageEntry arrows = session.ActiveProfile.InventoryEntryIds
                .Select(session.ActiveProfile.FindStorageEntry)
                .Single(entry => entry.DefinitionId == ItemDefinitionIds.Arrow);
            Assert.That(arrows.Quantity, Is.EqualTo(30));
        }

        [Test]
        public void CatalogSeparatesArtifactsFromOrdinaryItems()
        {
            Assert.That(
                ItemDefinitionCatalog.Category(ItemDefinitionIds.KeenShard),
                Is.EqualTo(ItemCategory.Artifact));
            Assert.That(
                ItemDefinitionCatalog.IsArtifact(ItemDefinitionIds.Arrow),
                Is.False);
            Assert.That(
                ItemDefinitionCatalog.Category(ItemDefinitionIds.IronIngot),
                Is.EqualTo(ItemCategory.Material));
        }

        [Test]
        public void AnvilTransactionMovesArtifactFromInventoryIntoWeaponGrid()
        {
            PlayerProfile profile = PlayerProfile.CreateNew("anvil-profile");
            StorageEntry artifact = StorageEntry.Create(ItemDefinitionIds.KeenShard);
            profile.AddToStorage(artifact);
            Assert.That(profile.TryMoveToInventory(artifact.EntryId), Is.True);

            GameObject host = new GameObject("Weapon Grid Test");
            try
            {
                WeaponGridRuntime runtime = host.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults(startingCells: 1);

                Assert.That(
                    ArtifactInstallationService.TryInstall(
                        profile,
                        runtime,
                        0,
                        profile.FindStorageEntry(artifact.EntryId),
                        PlayerProfile.DefaultChestId,
                        GridCoordinate.Root,
                        0,
                        out string reason),
                    Is.True,
                    reason);
                Assert.That(profile.FindStorageEntry(artifact.EntryId), Is.Null);
                Assert.That(runtime.Loadout.Primary.Placements, Has.Count.EqualTo(1));
                Assert.That(
                    runtime.Loadout.Primary.Placements[0].Artifact.InstanceId,
                    Is.EqualTo(artifact.EntryId));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AnvilRejectsNonArtifactWithoutMutatingStorageOrGrid()
        {
            PlayerProfile profile = PlayerProfile.CreateNew("anvil-profile");
            StorageEntry arrow = StorageEntry.Create(ItemDefinitionIds.Arrow, 1);
            profile.AddToStorage(arrow);
            Assert.That(profile.TryMoveToInventory(arrow.EntryId), Is.True);

            GameObject host = new GameObject("Weapon Grid Test");
            try
            {
                WeaponGridRuntime runtime = host.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults(startingCells: 1);

                Assert.That(
                    ArtifactInstallationService.TryInstall(
                        profile,
                        runtime,
                        0,
                        profile.FindStorageEntry(arrow.EntryId),
                        PlayerProfile.DefaultChestId,
                        GridCoordinate.Root,
                        0,
                        out string reason),
                    Is.False);
                StringAssert.Contains("not an artifact", reason);
                Assert.That(profile.FindStorageEntry(arrow.EntryId), Is.Not.Null);
                Assert.That(runtime.Loadout.Primary.Placements, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RemovedArtifactReturnsWithItsStableInstanceId()
        {
            PlayerProfile profile = PlayerProfile.CreateNew("anvil-profile");
            StorageEntry artifact = StorageEntry.Create(ItemDefinitionIds.KeenShard);
            profile.AddToStorage(artifact);
            profile.TryMoveToInventory(artifact.EntryId);
            GameObject host = new GameObject("Weapon Grid Test");
            try
            {
                WeaponGridRuntime runtime = host.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults(startingCells: 1);
                ArtifactInstallationService.TryInstall(
                    profile, runtime, 0, profile.FindStorageEntry(artifact.EntryId),
                    PlayerProfile.DefaultChestId, GridCoordinate.Root, 0, out _);
                ArtifactPlacement placement = runtime.Loadout.Primary.Placements[0];

                Assert.That(
                    ArtifactInstallationService.TryReturnToStorage(
                        profile,
                        runtime,
                        0,
                        placement,
                        PlayerProfile.DefaultChestId,
                        out string reason),
                    Is.True,
                    reason);
                Assert.That(profile.IsInInventory(artifact.EntryId), Is.True);
                Assert.That(profile.FindStorageEntry(artifact.EntryId), Is.Not.Null);
                Assert.That(runtime.Loadout.Primary.Placements, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TouchingDamageArtifactsCompleteEdgeChainPattern()
        {
            GameObject host = new GameObject("Weapon Grid Test");
            try
            {
                WeaponGridRuntime runtime = host.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults(startingCells: 1);
                GridCoordinate second = runtime.GrowWeapon(0);
                Assert.That(
                    runtime.TryPlace(
                        0,
                        ArtifactInstance.Create(ItemDefinitionIds.KeenShard),
                        GridCoordinate.Root,
                        0,
                        out string firstReason),
                    Is.True,
                    firstReason);
                Assert.That(
                    runtime.TryPlace(
                        0,
                        ArtifactInstance.Create(ItemDefinitionIds.ArtifactPowerShard),
                        second,
                        0,
                        out string secondReason),
                    Is.True,
                    secondReason);

                Assert.That(
                    ArtifactPatternResolver.ResolveCompleted(
                        runtime.Loadout.Primary,
                        runtime.Definitions),
                    Does.Contain("EDGE CHAIN"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
