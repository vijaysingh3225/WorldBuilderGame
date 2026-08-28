using NUnit.Framework;
using System.Linq;
using System.Reflection;
using UnityEngine;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class ArtifactInstallationServiceTests
    {
        [Test]
        public void CombatLabAnvilProvidesUnlimitedCopiesOfEveryDefinition()
        {
            GameObject owner = new GameObject(
                "combat-lab-unlimited-anvil-test");
            try
            {
                WeaponGridRuntime runtime =
                    owner.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults();
                HomeAnvil anvil = owner.AddComponent<HomeAnvil>();
                anvil.ConfigureUnlimitedArtifactCatalog(null, runtime);

                Assert.That(anvil.UsesUnlimitedArtifactCatalog, Is.True);
                Assert.That(
                    anvil.UnlimitedArtifactDefinitionCount,
                    Is.EqualTo(3));
                Assert.That(
                    runtime.Definitions.Select(
                        definition => definition.DefinitionId),
                    Is.EquivalentTo(new[]
                    {
                        ItemDefinitionIds.OwlEyeSeal,
                        ItemDefinitionIds.ObsidianShard,
                        ItemDefinitionIds.WingedSeal
                    }));
                Assert.That(
                    runtime.Definitions.All(
                        definition => definition.Shape.Count == 1 &&
                            definition.Shape[0].Equals(
                                GridCoordinate.Root)),
                    Is.True,
                    "Every current artifact must occupy exactly one grid cell.");
                Assert.That(
                    runtime.Definitions.All(
                        definition => ItemDefinitionCatalog.LoadIcon(
                            definition.DefinitionId) != null),
                    Is.True,
                    "The anvil must use the same artifact icons as raid inventory.");
                Assert.That(
                    HomeAnvil.IsHoldingArtifact(
                        null,
                        ItemDefinitionIds.OwlEyeSeal),
                    Is.True,
                    "An unlimited catalog drag must count as a held artifact even though it has no storage entry.");
                Assert.That(
                    HomeAnvil.IsHoldingArtifact(null, null),
                    Is.False);
                Rect cursorRect =
                    InventoryItemPresentation.CalculateSingleCellCursorRect(
                        new Vector2(100f, 80f),
                        52f);
                Assert.That(cursorRect.center, Is.EqualTo(new Vector2(100f, 80f)));
                Assert.That(cursorRect.size, Is.EqualTo(new Vector2(52f, 52f)));

                const BindingFlags PrivateInstance =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo heldDefinition = typeof(HomeAnvil).GetField(
                    "heldDefinitionId",
                    PrivateInstance);
                MethodInfo install = typeof(HomeAnvil).GetMethod(
                    "TryInstallUnlimitedArtifact",
                    PrivateInstance);
                WeaponGridState state = runtime.Loadout.Primary;
                string definitionId =
                    runtime.Definitions[0].DefinitionId;

                heldDefinition.SetValue(anvil, definitionId);
                install.Invoke(
                    anvil,
                    new object[] { state.UnlockedCells[0] });
                heldDefinition.SetValue(anvil, definitionId);
                install.Invoke(
                    anvil,
                    new object[] { state.UnlockedCells[1] });

                Assert.That(state.Placements, Has.Count.EqualTo(2));
                Assert.That(
                    state.Placements[0].Artifact.DefinitionId,
                    Is.EqualTo(definitionId));
                Assert.That(
                    state.Placements[1].Artifact.DefinitionId,
                    Is.EqualTo(definitionId));
                Assert.That(
                    state.Placements[0].Artifact.InstanceId,
                    Is.Not.EqualTo(
                        state.Placements[1].Artifact.InstanceId));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CombatLabAnvilPlacementPersistsIntoInventoryWeaponGrid()
        {
            GameObject bootstrapOwner = new GameObject(
                "combat-lab-artifact-profile-bootstrap");
            GameObject systems = new GameObject(
                "combat-lab-artifact-profile-systems");
            try
            {
                GameplayLoopBootstrap bootstrap =
                    bootstrapOwner.AddComponent<GameplayLoopBootstrap>();
                Assert.That(
                    bootstrap.StartCombatLab(
                        PlayerProfile.CreateNew(
                            "combat-lab-artifact-profile")),
                    Is.True,
                    bootstrap.LastInitializationError);

                WeaponGridRuntime runtime =
                    systems.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults();
                WeaponGridProfileBinding binding =
                    systems.AddComponent<WeaponGridProfileBinding>();
                binding.Configure(runtime, bootstrap);
                HomeAnvil anvil = systems.AddComponent<HomeAnvil>();
                anvil.ConfigureUnlimitedArtifactCatalog(null, runtime);

                const BindingFlags PrivateInstance =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(HomeAnvil).GetField(
                        "heldDefinitionId",
                        PrivateInstance)
                    .SetValue(anvil, ItemDefinitionIds.OwlEyeSeal);
                typeof(HomeAnvil).GetMethod(
                        "TryInstallUnlimitedArtifact",
                        PrivateInstance)
                    .Invoke(
                        anvil,
                        new object[]
                        {
                            runtime.Loadout.Primary.UnlockedCells[0]
                        });

                PlayerProfile profile = bootstrap.Session.ActiveProfile;
                WeaponGridState persisted =
                    JsonUtility.FromJson<WeaponGridState>(
                        profile.WeaponOne.GridStateJson);
                Assert.That(persisted.Placements, Has.Count.EqualTo(1));
                Assert.That(
                    persisted.Placements[0].Artifact.DefinitionId,
                    Is.EqualTo(ItemDefinitionIds.OwlEyeSeal));

                WeaponGridSandboxToolkit toolkit =
                    systems.AddComponent<WeaponGridSandboxToolkit>();
                toolkit.SetRuntime(runtime);
                HomeInventoryController inventory =
                    systems.AddComponent<HomeInventoryController>();
                inventory.Configure(null, null, toolkit);
                FieldInfo inventoryToolkit =
                    typeof(HomeInventoryController).GetField(
                        "gridToolkit",
                        PrivateInstance);
                Assert.That(
                    inventoryToolkit.GetValue(inventory),
                    Is.SameAs(toolkit));
                Assert.That(
                    toolkit.Runtime.Loadout.Primary.Placements,
                    Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(systems);
                Object.DestroyImmediate(bootstrapOwner);
                typeof(GameplayLoopBootstrap).GetField(
                        "current",
                        BindingFlags.Static | BindingFlags.NonPublic)
                    ?.SetValue(null, null);
            }
        }

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
        public void ReopeningAnvilResetsBothWeaponGridViewports()
        {
            GameObject owner = new GameObject("anvil-grid-view-reset-test");
            try
            {
                HomeAnvil anvil = owner.AddComponent<HomeAnvil>();
                const BindingFlags PrivateInstance =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo panField = typeof(HomeAnvil).GetField(
                    "weaponGridPan",
                    PrivateInstance);
                FieldInfo zoomField = typeof(HomeAnvil).GetField(
                    "weaponGridZoom",
                    PrivateInstance);
                FieldInfo panningField = typeof(HomeAnvil).GetField(
                    "gridPanning",
                    PrivateInstance);
                MethodInfo resetMethod = typeof(HomeAnvil).GetMethod(
                    "ResetGridViewportState",
                    PrivateInstance);
                Assert.That(panField, Is.Not.Null);
                Assert.That(zoomField, Is.Not.Null);
                Assert.That(panningField, Is.Not.Null);
                Assert.That(resetMethod, Is.Not.Null);

                var pans = (Vector2[])panField.GetValue(anvil);
                var zooms = (float[])zoomField.GetValue(anvil);
                pans[0] = new Vector2(48f, -31f);
                pans[1] = new Vector2(-22f, 19f);
                zooms[0] = HomeAnvil.MaximumGridZoom;
                zooms[1] = HomeAnvil.MinimumGridZoom;
                panningField.SetValue(anvil, true);

                resetMethod.Invoke(anvil, null);

                Assert.That(pans, Is.All.EqualTo(Vector2.zero));
                Assert.That(zooms, Is.All.EqualTo(1f));
                Assert.That(panningField.GetValue(anvil), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PauseMenuDefersEscapeToOpenAnvil()
        {
            GameObject menuOwner = new GameObject(
                "anvil-pause-menu-guard-test");
            GameObject anvilOwner = new GameObject(
                "open-anvil-modal-test");
            try
            {
                SceneNavigationMenu menu =
                    menuOwner.AddComponent<SceneNavigationMenu>();
                HomeAnvil anvil = anvilOwner.AddComponent<HomeAnvil>();
                const BindingFlags PrivateInstance =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo anvilOpenField = typeof(HomeAnvil).GetField(
                    "isOpen",
                    PrivateInstance);
                FieldInfo menuAnvilField =
                    typeof(SceneNavigationMenu).GetField(
                        "homeAnvil",
                        PrivateInstance);
                MethodInfo modalCheck =
                    typeof(SceneNavigationMenu).GetMethod(
                        "IsModalUiOpen",
                        PrivateInstance);
                Assert.That(anvilOpenField, Is.Not.Null);
                Assert.That(menuAnvilField, Is.Not.Null);
                Assert.That(modalCheck, Is.Not.Null);

                anvilOpenField.SetValue(anvil, true);
                menuAnvilField.SetValue(menu, anvil);

                Assert.That(
                    modalCheck.Invoke(menu, null),
                    Is.True,
                    "The pause menu must not consume the same Escape press that closes the anvil.");
            }
            finally
            {
                HomeAnvil anvil = anvilOwner.GetComponent<HomeAnvil>();
                if (anvil != null)
                {
                    typeof(HomeAnvil).GetField(
                        "isOpen",
                        BindingFlags.Instance | BindingFlags.NonPublic)?
                        .SetValue(anvil, false);
                }
                Object.DestroyImmediate(menuOwner);
                Object.DestroyImmediate(anvilOwner);
            }
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
                ItemDefinitionCatalog.Category(ItemDefinitionIds.OwlEyeSeal),
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
            StorageEntry artifact = StorageEntry.Create(
                ItemDefinitionIds.OwlEyeSeal);
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
            StorageEntry artifact = StorageEntry.Create(
                ItemDefinitionIds.OwlEyeSeal);
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
                        ArtifactInstance.Create(ItemDefinitionIds.OwlEyeSeal),
                        GridCoordinate.Root,
                        0,
                        out string firstReason),
                    Is.True,
                    firstReason);
                Assert.That(
                    runtime.TryPlace(
                        0,
                        ArtifactInstance.Create(ItemDefinitionIds.ObsidianShard),
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
