using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    /// <summary>
    /// Pure serializable state for one weapon. It has no scene references and can be
    /// embedded directly in profile, raid-session, or sandbox data.
    /// </summary>
    [Serializable]
    public sealed class WeaponGridState
    {
        private static readonly GridCoordinate[] CardinalDirections =
        {
            new GridCoordinate(1, 0),
            new GridCoordinate(0, 1),
            new GridCoordinate(-1, 0),
            new GridCoordinate(0, -1)
        };

        [SerializeField] private string weaponInstanceId;
        [SerializeField] private string displayName;
        [SerializeField] private int seed = 1337;
        [SerializeField, Min(0)] private int growthStep;
        [SerializeField] private List<GridCoordinate> unlockedCells =
            new List<GridCoordinate>();
        [SerializeField] private List<ArtifactPlacement> placements =
            new List<ArtifactPlacement>();

        public WeaponGridState()
        {
        }

        public WeaponGridState(string weaponInstanceId, string displayName, int seed)
        {
            this.weaponInstanceId = weaponInstanceId;
            this.displayName = displayName;
            Reset(seed);
        }

        public string WeaponInstanceId => weaponInstanceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? "Weapon"
            : displayName;
        public int Seed => seed;
        public int GrowthStep => growthStep;
        public IReadOnlyList<GridCoordinate> UnlockedCells => unlockedCells;
        public IReadOnlyList<ArtifactPlacement> Placements => placements;

        public void SetWeaponIdentity(
            string owningWeaponInstanceId,
            string owningDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(owningWeaponInstanceId))
            {
                throw new ArgumentException(
                    "An owning weapon instance ID is required.",
                    nameof(owningWeaponInstanceId));
            }

            weaponInstanceId = owningWeaponInstanceId.Trim();
            if (!string.IsNullOrWhiteSpace(owningDisplayName))
            {
                displayName = owningDisplayName.Trim();
            }
        }

        public void EnsureInitialized(string fallbackName, int fallbackSeed)
        {
            weaponInstanceId = string.IsNullOrWhiteSpace(weaponInstanceId)
                ? Guid.NewGuid().ToString("N")
                : weaponInstanceId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? fallbackName
                : displayName.Trim();
            unlockedCells ??= new List<GridCoordinate>();
            placements ??= new List<ArtifactPlacement>();
            if (unlockedCells.Count == 0)
            {
                seed = fallbackSeed;
                growthStep = 0;
                unlockedCells.Add(GridCoordinate.Root);
            }

            DeduplicateUnlockedCells();
            if (!ContainsCell(GridCoordinate.Root))
            {
                unlockedCells.Insert(0, GridCoordinate.Root);
            }

            for (int index = placements.Count - 1; index >= 0; index--)
            {
                ArtifactPlacement placement = placements[index];
                if (placement?.Artifact == null)
                {
                    placements.RemoveAt(index);
                    continue;
                }

                placement.Artifact.EnsureValid();
            }
        }

        public void Reset(int newSeed)
        {
            seed = newSeed;
            growthStep = 0;
            unlockedCells ??= new List<GridCoordinate>();
            placements ??= new List<ArtifactPlacement>();
            unlockedCells.Clear();
            unlockedCells.Add(GridCoordinate.Root);
            placements.Clear();
        }

        public bool ContainsCell(GridCoordinate coordinate)
        {
            return unlockedCells != null && unlockedCells.Contains(coordinate);
        }

        /// <summary>
        /// Adds one cell from the cardinal frontier. Given the same seed and number of
        /// growth calls, the exact same grid is produced.
        /// </summary>
        public GridCoordinate GrowOne()
        {
            EnsureInitialized(DisplayName, seed);
            var unlocked = new HashSet<GridCoordinate>(unlockedCells);
            var frontier = new List<GridCoordinate>();
            var frontierSet = new HashSet<GridCoordinate>();
            for (int cellIndex = 0; cellIndex < unlockedCells.Count; cellIndex++)
            {
                GridCoordinate cell = unlockedCells[cellIndex];
                for (int directionIndex = 0;
                    directionIndex < CardinalDirections.Length;
                    directionIndex++)
                {
                    GridCoordinate candidate =
                        cell + CardinalDirections[directionIndex];
                    if (!unlocked.Contains(candidate) && frontierSet.Add(candidate))
                    {
                        frontier.Add(candidate);
                    }
                }
            }

            frontier.Sort(CompareCoordinates);
            int chosenIndex = DeterministicIndex(seed, growthStep, frontier.Count);
            GridCoordinate chosen = frontier[chosenIndex];
            unlockedCells.Add(chosen);
            growthStep++;
            return chosen;
        }

        public bool TryPlace(
            ArtifactInstance artifact,
            ArtifactDefinitionData definition,
            GridCoordinate anchor,
            int rotation,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            out string reason)
        {
            if (artifact == null)
            {
                reason = "Artifact instance is missing.";
                return false;
            }

            artifact.EnsureValid();
            if (definition == null ||
                !string.Equals(
                    artifact.DefinitionId,
                    definition.DefinitionId,
                    StringComparison.Ordinal))
            {
                reason = "Artifact definition does not match the instance.";
                return false;
            }

            for (int index = 0; index < placements.Count; index++)
            {
                if (string.Equals(
                    placements[index].Artifact.InstanceId,
                    artifact.InstanceId,
                    StringComparison.Ordinal))
                {
                    reason = "That artifact instance is already placed.";
                    return false;
                }
            }

            var occupied = BuildOccupiedCellSet(catalog, null);
            foreach (GridCoordinate offset in definition.GetRotatedShape(rotation))
            {
                GridCoordinate cell = anchor + offset;
                if (!ContainsCell(cell))
                {
                    reason = $"Artifact extends beyond the unlocked grid at {cell}.";
                    return false;
                }

                if (occupied.Contains(cell))
                {
                    reason = $"Grid cell {cell} is already occupied.";
                    return false;
                }
            }

            placements.Add(new ArtifactPlacement(artifact, anchor, rotation));
            reason = string.Empty;
            return true;
        }

        public bool TryRemoveAt(
            GridCoordinate coordinate,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            out ArtifactInstance removed)
        {
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                ArtifactPlacement placement = placements[index];
                if (!TryGetDefinition(placement, catalog, out ArtifactDefinitionData definition))
                {
                    continue;
                }

                foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                {
                    if (cell != coordinate)
                    {
                        continue;
                    }

                    removed = placement.Artifact;
                    placements.RemoveAt(index);
                    return true;
                }
            }

            removed = null;
            return false;
        }

        public bool TryRemoveInstance(string instanceId, out ArtifactInstance removed)
        {
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                ArtifactPlacement placement = placements[index];
                if (!string.Equals(
                    placement.Artifact.InstanceId,
                    instanceId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                removed = placement.Artifact;
                placements.RemoveAt(index);
                return true;
            }

            removed = null;
            return false;
        }

        public bool TryRotateAt(
            GridCoordinate coordinate,
            int quarterTurnDelta,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            out string reason)
        {
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                ArtifactPlacement placement = placements[index];
                if (!TryGetDefinition(
                    placement,
                    catalog,
                    out ArtifactDefinitionData definition))
                {
                    continue;
                }

                bool containsCoordinate = false;
                foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                {
                    if (cell == coordinate)
                    {
                        containsCoordinate = true;
                        break;
                    }
                }

                if (!containsCoordinate)
                {
                    continue;
                }

                int newRotation = GridCoordinate.NormalizeRotation(
                    placement.Rotation + quarterTurnDelta);
                var occupied = BuildOccupiedCellSet(
                    catalog,
                    placement.Artifact.InstanceId);
                foreach (GridCoordinate offset in definition.GetRotatedShape(newRotation))
                {
                    GridCoordinate cell = placement.Anchor + offset;
                    if (!ContainsCell(cell))
                    {
                        reason =
                            $"Rotated artifact extends beyond the grid at {cell}.";
                        return false;
                    }

                    if (occupied.Contains(cell))
                    {
                        reason =
                            $"Rotated artifact would overlap the cell at {cell}.";
                        return false;
                    }
                }

                placements[index] = new ArtifactPlacement(
                    placement.Artifact,
                    placement.Anchor,
                    newRotation);
                reason = string.Empty;
                return true;
            }

            reason = $"No artifact occupies {coordinate}.";
            return false;
        }

        public ArtifactPlacement FindPlacementAt(
            GridCoordinate coordinate,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog)
        {
            for (int index = placements.Count - 1; index >= 0; index--)
            {
                ArtifactPlacement placement = placements[index];
                if (!TryGetDefinition(placement, catalog, out ArtifactDefinitionData definition))
                {
                    continue;
                }

                foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                {
                    if (cell == coordinate)
                    {
                        return placement;
                    }
                }
            }

            return null;
        }

        public WeaponGridModifiers ResolveModifiers(
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog)
        {
            var resolved = new WeaponGridModifiers();
            for (int placementIndex = 0;
                placementIndex < placements.Count;
                placementIndex++)
            {
                ArtifactPlacement placement = placements[placementIndex];
                if (!TryGetDefinition(placement, catalog, out ArtifactDefinitionData definition))
                {
                    continue;
                }

                IReadOnlyList<ArtifactStatModifier> modifiers = definition.Modifiers;
                for (int modifierIndex = 0;
                    modifierIndex < modifiers.Count;
                    modifierIndex++)
                {
                    ArtifactStatModifier modifier = modifiers[modifierIndex];
                    resolved.Add(modifier.Stat, modifier.Amount);
                }
            }

            return resolved;
        }

        public bool ValidatePlacements(
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            out string reason)
        {
            var occupied = new HashSet<GridCoordinate>();
            for (int index = 0; index < placements.Count; index++)
            {
                ArtifactPlacement placement = placements[index];
                if (!TryGetDefinition(placement, catalog, out ArtifactDefinitionData definition))
                {
                    reason = $"Missing definition for placement {index}.";
                    return false;
                }

                foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                {
                    if (!ContainsCell(cell))
                    {
                        reason = $"Placement {index} extends outside the grid at {cell}.";
                        return false;
                    }

                    if (!occupied.Add(cell))
                    {
                        reason = $"Placements overlap at {cell}.";
                        return false;
                    }
                }
            }

            reason = string.Empty;
            return true;
        }

        private HashSet<GridCoordinate> BuildOccupiedCellSet(
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            string ignoredInstanceId)
        {
            var occupied = new HashSet<GridCoordinate>();
            for (int index = 0; index < placements.Count; index++)
            {
                ArtifactPlacement placement = placements[index];
                if (!string.IsNullOrEmpty(ignoredInstanceId) &&
                    string.Equals(
                        placement.Artifact.InstanceId,
                        ignoredInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryGetDefinition(placement, catalog, out ArtifactDefinitionData definition))
                {
                    continue;
                }

                foreach (GridCoordinate cell in placement.OccupiedCells(definition))
                {
                    occupied.Add(cell);
                }
            }

            return occupied;
        }

        private static bool TryGetDefinition(
            ArtifactPlacement placement,
            IReadOnlyDictionary<string, ArtifactDefinitionData> catalog,
            out ArtifactDefinitionData definition)
        {
            definition = null;
            return placement?.Artifact != null &&
                catalog != null &&
                catalog.TryGetValue(placement.Artifact.DefinitionId, out definition);
        }

        private void DeduplicateUnlockedCells()
        {
            var unique = new HashSet<GridCoordinate>();
            for (int index = unlockedCells.Count - 1; index >= 0; index--)
            {
                if (!unique.Add(unlockedCells[index]))
                {
                    unlockedCells.RemoveAt(index);
                }
            }
        }

        private static int CompareCoordinates(GridCoordinate left, GridCoordinate right)
        {
            int xComparison = left.X.CompareTo(right.X);
            return xComparison != 0
                ? xComparison
                : left.Y.CompareTo(right.Y);
        }

        private static int DeterministicIndex(int sourceSeed, int step, int count)
        {
            if (count <= 1)
            {
                return 0;
            }

            unchecked
            {
                uint value = (uint)sourceSeed;
                value ^= (uint)(step + 1) * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (int)(value % (uint)count);
            }
        }
    }

    [Serializable]
    public sealed class WeaponGridLoadoutState
    {
        [SerializeField] private WeaponGridState primary;
        [SerializeField] private WeaponGridState secondary;
        [SerializeField, Range(0, 1)] private int activeWeaponIndex;

        public WeaponGridLoadoutState()
        {
        }

        public WeaponGridLoadoutState(
            WeaponGridState primary,
            WeaponGridState secondary,
            int activeWeaponIndex = 0)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.activeWeaponIndex = Mathf.Clamp(activeWeaponIndex, 0, 1);
        }

        public WeaponGridState Primary => primary;
        public WeaponGridState Secondary => secondary;
        public int ActiveWeaponIndex => activeWeaponIndex;
        public WeaponGridState Active => GetWeapon(activeWeaponIndex);

        public void EnsureInitialized()
        {
            primary ??= new WeaponGridState(
                Guid.NewGuid().ToString("N"),
                "Weapon 1",
                1337);
            secondary ??= new WeaponGridState(
                Guid.NewGuid().ToString("N"),
                "Weapon 2",
                7331);
            primary.EnsureInitialized("Weapon 1", 1337);
            secondary.EnsureInitialized("Weapon 2", 7331);
            activeWeaponIndex = Mathf.Clamp(activeWeaponIndex, 0, 1);
        }

        public WeaponGridState GetWeapon(int weaponIndex)
        {
            return weaponIndex == 1 ? secondary : primary;
        }

        public bool SelectWeapon(int weaponIndex)
        {
            int clamped = Mathf.Clamp(weaponIndex, 0, 1);
            if (activeWeaponIndex == clamped)
            {
                return false;
            }

            activeWeaponIndex = clamped;
            return true;
        }

        public void ReplaceWeapon(int weaponIndex, WeaponGridState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (weaponIndex == 1)
            {
                state.EnsureInitialized("Weapon 2", 7331);
                secondary = state;
            }
            else
            {
                state.EnsureInitialized("Weapon 1", 1337);
                primary = state;
            }
        }
    }
}
