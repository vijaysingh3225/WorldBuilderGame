using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class WeaponGridTests
    {
        private static readonly GridCoordinate[] RotatedRightOffsets =
        {
            new GridCoordinate(1, 0),
            new GridCoordinate(0, -1),
            new GridCoordinate(-1, 0),
            new GridCoordinate(0, 1)
        };

        [Test]
        public void Growth_WithSameSeed_ProducesSameConnectedGrid()
        {
            var first = new WeaponGridState("first", "First", 82419);
            var second = new WeaponGridState("second", "Second", 82419);
            var visited = new HashSet<GridCoordinate>
            {
                GridCoordinate.Root
            };

            for (int step = 0; step < 40; step++)
            {
                GridCoordinate firstCell = first.GrowOne();
                GridCoordinate secondCell = second.GrowOne();

                Assert.That(secondCell, Is.EqualTo(firstCell));
                Assert.That(
                    HasCardinalNeighbor(firstCell, visited),
                    Is.True,
                    $"Growth step {step} must attach to the existing grid.");
                Assert.That(
                    visited.Add(firstCell),
                    Is.True,
                    $"Growth step {step} must unlock a new coordinate.");
            }

            Assert.That(first.GrowthStep, Is.EqualTo(40));
            Assert.That(first.UnlockedCells, Has.Count.EqualTo(41));
            Assert.That(
                second.UnlockedCells,
                Is.EqualTo(first.UnlockedCells));
        }

        [Test]
        public void Placement_RotationAndRemoval_RespectGridValidation()
        {
            ArtifactDefinitionData domino = CreateDefinition(
                "domino",
                new[]
                {
                    GridCoordinate.Root,
                    new GridCoordinate(1, 0)
                },
                new ArtifactStatModifier(ArtifactStat.Damage, 1f));
            ArtifactDefinitionData single = CreateDefinition(
                "single",
                new[] { GridCoordinate.Root },
                new ArtifactStatModifier(ArtifactStat.MaxHealth, 1f));
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog =
                CreateCatalog(domino, single);

            var lockedGrid = new WeaponGridState(
                "locked",
                "Locked",
                101);
            Assert.That(
                lockedGrid.TryPlace(
                    ArtifactInstance.Create(domino.DefinitionId),
                    domino,
                    GridCoordinate.Root,
                    0,
                    catalog,
                    out string lockedReason),
                Is.False);
            Assert.That(lockedReason, Does.Contain("beyond"));
            GridCoordinate onlyNeighbor = lockedGrid.GrowOne();
            int onlyNeighborRotation = FindRotation(
                onlyNeighbor - GridCoordinate.Root);
            Assert.That(
                lockedGrid.TryPlace(
                    ArtifactInstance.Create(domino.DefinitionId),
                    domino,
                    GridCoordinate.Root,
                    onlyNeighborRotation,
                    catalog,
                    out string limitedPlaceReason),
                Is.True,
                limitedPlaceReason);
            int lockedRotation = (onlyNeighborRotation + 1) % 4;
            Assert.That(
                lockedGrid.TryRotateAt(
                    GridCoordinate.Root,
                    GridCoordinate.NormalizeRotation(
                        lockedRotation - onlyNeighborRotation),
                    catalog,
                    out string lockedRotationReason),
                Is.False);
            Assert.That(lockedRotationReason, Does.Contain("beyond"));

            var grid = new WeaponGridState("placement", "Placement", 337);
            for (int step = 0; step < 12; step++)
            {
                grid.GrowOne();
            }

            FindTwoDirectionAnchor(
                grid.UnlockedCells,
                out GridCoordinate anchor,
                out int initialRotation,
                out int rotatedRotation);
            ArtifactInstance placedInstance =
                ArtifactInstance.Create(domino.DefinitionId);

            Assert.That(
                grid.TryPlace(
                    placedInstance,
                    domino,
                    anchor,
                    initialRotation,
                    catalog,
                    out string placeReason),
                Is.True,
                placeReason);

            int rotationDelta = GridCoordinate.NormalizeRotation(
                rotatedRotation - initialRotation);
            Assert.That(
                grid.TryRotateAt(
                    anchor,
                    rotationDelta,
                    catalog,
                    out string rotateReason),
                Is.True,
                rotateReason);

            GridCoordinate rotatedCell =
                anchor + RotatedRightOffsets[rotatedRotation];
            ArtifactPlacement rotatedPlacement =
                grid.FindPlacementAt(rotatedCell, catalog);
            Assert.That(rotatedPlacement, Is.Not.Null);
            Assert.That(rotatedPlacement.Rotation, Is.EqualTo(rotatedRotation));
            Assert.That(
                rotatedPlacement.Artifact.InstanceId,
                Is.EqualTo(placedInstance.InstanceId));

            Assert.That(
                grid.TryPlace(
                    ArtifactInstance.Create(single.DefinitionId),
                    single,
                    rotatedCell,
                    0,
                    catalog,
                    out string overlapReason),
                Is.False);
            Assert.That(overlapReason, Does.Contain("occupied"));

            Assert.That(
                grid.TryRemoveAt(
                    rotatedCell,
                    catalog,
                    out ArtifactInstance removed),
                Is.True);
            Assert.That(removed.InstanceId, Is.EqualTo(placedInstance.InstanceId));
            Assert.That(grid.Placements, Is.Empty);
            Assert.That(
                grid.ValidatePlacements(catalog, out string validationReason),
                Is.True,
                validationReason);
        }

        [Test]
        public void Loadout_KeepsBothWeaponGridsIndependent()
        {
            var primary = new WeaponGridState(
                "weapon-one",
                "Sword",
                11);
            var secondary = new WeaponGridState(
                "weapon-two",
                "Bow",
                22);
            var loadout = new WeaponGridLoadoutState(primary, secondary);
            ArtifactDefinitionData damage = CreateDefinition(
                "damage",
                new[] { GridCoordinate.Root },
                new ArtifactStatModifier(ArtifactStat.Damage, 2f));
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog =
                CreateCatalog(damage);

            primary.GrowOne();
            primary.GrowOne();
            Assert.That(
                primary.TryPlace(
                    ArtifactInstance.Create(damage.DefinitionId),
                    damage,
                    GridCoordinate.Root,
                    0,
                    catalog,
                    out string reason),
                Is.True,
                reason);

            Assert.That(primary.UnlockedCells, Has.Count.EqualTo(3));
            Assert.That(primary.Placements, Has.Count.EqualTo(1));
            Assert.That(secondary.UnlockedCells, Has.Count.EqualTo(1));
            Assert.That(secondary.Placements, Is.Empty);

            Assert.That(loadout.Active, Is.SameAs(primary));
            Assert.That(loadout.SelectWeapon(1), Is.True);
            Assert.That(loadout.Active, Is.SameAs(secondary));
            Assert.That(primary.Placements, Has.Count.EqualTo(1));
        }

        [Test]
        public void RuntimeJsonRoundTrip_PreservesBothGridsAndActiveWeapon()
        {
            GameObject sourceOwner = new GameObject("weapon-grid-json-source");
            GameObject targetOwner = new GameObject("weapon-grid-json-target");
            try
            {
                WeaponGridRuntime source =
                    sourceOwner.AddComponent<WeaponGridRuntime>();
                source.InitializeSandboxDefaults(
                    primarySeed: 4101,
                    secondarySeed: 9104,
                    startingCells: 8);
                source.GrowWeapon(0, 3);
                source.SelectWeapon(1);
                Assert.That(
                    source.TryPlaceActive(
                        "keen-shard",
                        GridCoordinate.Root,
                        0,
                        out string placeReason),
                    Is.True,
                    placeReason);

                string json = source.ExportLoadoutJson();
                string primaryJson = source.ExportWeaponJson(0);
                WeaponGridRuntime target =
                    targetOwner.AddComponent<WeaponGridRuntime>();

                Assert.That(
                    target.ImportLoadoutJson(json, out string importReason),
                    Is.True,
                    importReason);
                Assert.That(target.ActiveWeaponIndex, Is.EqualTo(1));
                Assert.That(
                    target.Loadout.Primary.Seed,
                    Is.EqualTo(source.Loadout.Primary.Seed));
                Assert.That(
                    target.Loadout.Primary.GrowthStep,
                    Is.EqualTo(source.Loadout.Primary.GrowthStep));
                Assert.That(
                    target.Loadout.Primary.UnlockedCells,
                    Is.EqualTo(source.Loadout.Primary.UnlockedCells));
                Assert.That(
                    target.Loadout.Secondary.Placements,
                    Has.Count.EqualTo(1));
                Assert.That(
                    target.Loadout.Secondary.Placements[0]
                        .Artifact.InstanceId,
                    Is.EqualTo(
                        source.Loadout.Secondary.Placements[0]
                            .Artifact.InstanceId));

                target.ResetWeapon(0, 1);
                Assert.That(
                    target.ImportWeaponJson(
                        0,
                        primaryJson,
                        out string weaponImportReason),
                    Is.True,
                    weaponImportReason);
                Assert.That(
                    target.Loadout.Primary.UnlockedCells,
                    Is.EqualTo(source.Loadout.Primary.UnlockedCells));
            }
            finally
            {
                Object.DestroyImmediate(sourceOwner);
                Object.DestroyImmediate(targetOwner);
            }
        }

        [Test]
        public void ResolvedStats_UseActiveDamageAndCombinedUtilityBonuses()
        {
            ArtifactDefinitionData lightDamage = CreateDefinition(
                "light-damage",
                new[] { GridCoordinate.Root },
                new ArtifactStatModifier(ArtifactStat.Damage, 2f));
            ArtifactDefinitionData heavyDamage = CreateDefinition(
                "heavy-damage",
                new[] { GridCoordinate.Root },
                new ArtifactStatModifier(ArtifactStat.Damage, 5f));
            ArtifactDefinitionData health = CreateDefinition(
                "health",
                new[] { GridCoordinate.Root },
                new ArtifactStatModifier(ArtifactStat.MaxHealth, 10f));
            ArtifactDefinitionData speed = CreateDefinition(
                "speed",
                new[] { GridCoordinate.Root },
                new ArtifactStatModifier(ArtifactStat.MoveSpeed, 0.4f));
            var definitions = new[]
            {
                lightDamage,
                heavyDamage,
                health,
                speed
            };
            var loadout = new WeaponGridLoadoutState(
                new WeaponGridState("sword", "Sword", 71),
                new WeaponGridState("bow", "Bow", 72));
            GameObject owner = new GameObject("weapon-grid-stats");
            try
            {
                WeaponGridRuntime runtime =
                    owner.AddComponent<WeaponGridRuntime>();
                runtime.Configure(loadout, definitions);
                runtime.GrowWeapon(0);
                runtime.GrowWeapon(1);
                GridCoordinate primaryUtilityCell =
                    runtime.Loadout.Primary.UnlockedCells[1];
                GridCoordinate secondaryUtilityCell =
                    runtime.Loadout.Secondary.UnlockedCells[1];
                int modifierEventCount = 0;
                WeaponGridModifierSummary lastSummary = default;
                runtime.ModifiersChanged += summary =>
                {
                    modifierEventCount++;
                    lastSummary = summary;
                };

                AssertPlaced(
                    runtime,
                    0,
                    lightDamage.DefinitionId,
                    GridCoordinate.Root);
                AssertPlaced(
                    runtime,
                    0,
                    health.DefinitionId,
                    primaryUtilityCell);
                AssertPlaced(
                    runtime,
                    1,
                    heavyDamage.DefinitionId,
                    GridCoordinate.Root);
                AssertPlaced(
                    runtime,
                    1,
                    speed.DefinitionId,
                    secondaryUtilityCell);

                WeaponGridModifierSummary primarySummary =
                    runtime.GetModifierSummary();
                Assert.That(primarySummary.Primary.Damage, Is.EqualTo(2f));
                Assert.That(primarySummary.Primary.MaxHealth, Is.EqualTo(10f));
                Assert.That(primarySummary.Secondary.Damage, Is.EqualTo(5f));
                Assert.That(primarySummary.Secondary.MoveSpeed, Is.EqualTo(0.4f));
                Assert.That(primarySummary.Effective.Damage, Is.EqualTo(2f));
                Assert.That(primarySummary.Effective.MaxHealth, Is.EqualTo(10f));
                Assert.That(primarySummary.Effective.MoveSpeed, Is.EqualTo(0.4f));

                runtime.SelectWeapon(1);
                Assert.That(lastSummary.Effective.Damage, Is.EqualTo(5f));
                Assert.That(lastSummary.Effective.MaxHealth, Is.EqualTo(10f));
                Assert.That(lastSummary.Effective.MoveSpeed, Is.EqualTo(0.4f));
                Assert.That(modifierEventCount, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static ArtifactDefinitionData CreateDefinition(
            string id,
            IEnumerable<GridCoordinate> shape,
            params ArtifactStatModifier[] modifiers)
        {
            return new ArtifactDefinitionData(
                id,
                id,
                Color.white,
                shape,
                modifiers);
        }

        private static IReadOnlyDictionary<string, ArtifactDefinitionData>
            CreateCatalog(params ArtifactDefinitionData[] definitions)
        {
            return definitions.ToDictionary(
                definition => definition.DefinitionId);
        }

        private static bool HasCardinalNeighbor(
            GridCoordinate coordinate,
            HashSet<GridCoordinate> cells)
        {
            for (int index = 0; index < RotatedRightOffsets.Length; index++)
            {
                if (cells.Contains(coordinate + RotatedRightOffsets[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void FindTwoDirectionAnchor(
            IReadOnlyList<GridCoordinate> cells,
            out GridCoordinate anchor,
            out int firstRotation,
            out int secondRotation)
        {
            var unlocked = new HashSet<GridCoordinate>(cells);
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var rotations = new List<int>();
                for (int rotation = 0;
                    rotation < RotatedRightOffsets.Length;
                    rotation++)
                {
                    if (unlocked.Contains(
                        cells[cellIndex] + RotatedRightOffsets[rotation]))
                    {
                        rotations.Add(rotation);
                    }
                }

                if (rotations.Count >= 2)
                {
                    anchor = cells[cellIndex];
                    firstRotation = rotations[0];
                    secondRotation = rotations[1];
                    return;
                }
            }

            throw new AssertionException(
                "Expected a connected grid with an anchor having two neighbors.");
        }

        private static int FindRotation(GridCoordinate offset)
        {
            for (int rotation = 0;
                rotation < RotatedRightOffsets.Length;
                rotation++)
            {
                if (RotatedRightOffsets[rotation] == offset)
                {
                    return rotation;
                }
            }

            throw new AssertionException(
                $"Offset {offset} is not cardinal.");
        }

        private static void AssertPlaced(
            WeaponGridRuntime runtime,
            int weaponIndex,
            string definitionId,
            GridCoordinate coordinate)
        {
            ArtifactInstance artifact = runtime.CreateArtifact(definitionId);
            Assert.That(artifact, Is.Not.Null);
            Assert.That(
                runtime.TryPlace(
                    weaponIndex,
                    artifact,
                    coordinate,
                    0,
                    out string reason),
                Is.True,
                reason);
        }
    }
}
