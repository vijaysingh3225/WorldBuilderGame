using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    public enum ItemCategory
    {
        Unknown = 0,
        Ammunition = 1,
        Consumable = 2,
        Material = 3,
        Artifact = 4,
        Currency = 5,
        Weapon = 6
    }

    public static class ItemDefinitionIds
    {
        public const string Arrow = "arrow";
        public const string HealthPack = "health-pack";
        public const string IronIngot = "iron-ingot";
        public const string Coal = "coal";
        public const string CopperCoin = "copper-coin";
        public const string OwlEyeSeal = "owl-eye-seal";
        public const string LootShortSword = "loot-short-sword";
        public const string LootHuntingBow = "loot-hunting-bow";
        public const string ArtifactPowerShard = "artifact-power-shard";
        public const string KeenShard = "keen-shard";
        public const string IronBond = "iron-bond";
        public const string WindStep = "wind-step";
        public const string RazorLine = "razor-line";
        public const string WayfarerKnot = "wayfarer-knot";
    }

    public static class ItemDefinitionCatalog
    {
        private static Texture2D arrowIcon;
        private static Texture2D healthPackIcon;
        private static Texture2D ironIngotIcon;
        private static Texture2D coalIcon;
        private static Texture2D copperCoinIcon;
        private static Texture2D owlEyeSealIcon;

        public static string DisplayName(string definitionId)
        {
            return definitionId switch
            {
                ItemDefinitionIds.Arrow => "Arrow",
                ItemDefinitionIds.HealthPack => "Health Pack",
                ItemDefinitionIds.IronIngot => "Iron Ingot",
                ItemDefinitionIds.Coal => "Coal",
                ItemDefinitionIds.CopperCoin => "Copper Coin",
                ItemDefinitionIds.OwlEyeSeal => "Owl Eye Seal",
                ItemDefinitionIds.LootShortSword => "Raider Short Sword",
                ItemDefinitionIds.LootHuntingBow => "Raider Hunting Bow",
                ItemDefinitionIds.ArtifactPowerShard => "Power Shard",
                ItemDefinitionIds.KeenShard => "Keen Shard",
                ItemDefinitionIds.IronBond => "Iron Bond",
                ItemDefinitionIds.WindStep => "Wind Step",
                ItemDefinitionIds.RazorLine => "Razor Line",
                ItemDefinitionIds.WayfarerKnot => "Wayfarer Knot",
                _ => string.IsNullOrWhiteSpace(definitionId)
                    ? "Unknown Item"
                    : definitionId.Trim()
            };
        }

        public static ItemCategory Category(string definitionId)
        {
            return definitionId switch
            {
                ItemDefinitionIds.Arrow => ItemCategory.Ammunition,
                ItemDefinitionIds.HealthPack => ItemCategory.Consumable,
                ItemDefinitionIds.IronIngot => ItemCategory.Material,
                ItemDefinitionIds.Coal => ItemCategory.Material,
                ItemDefinitionIds.CopperCoin => ItemCategory.Currency,
                ItemDefinitionIds.OwlEyeSeal => ItemCategory.Artifact,
                ItemDefinitionIds.LootShortSword => ItemCategory.Weapon,
                ItemDefinitionIds.LootHuntingBow => ItemCategory.Weapon,
                ItemDefinitionIds.ArtifactPowerShard => ItemCategory.Artifact,
                ItemDefinitionIds.KeenShard => ItemCategory.Artifact,
                ItemDefinitionIds.IronBond => ItemCategory.Artifact,
                ItemDefinitionIds.WindStep => ItemCategory.Artifact,
                ItemDefinitionIds.RazorLine => ItemCategory.Artifact,
                ItemDefinitionIds.WayfarerKnot => ItemCategory.Artifact,
                _ => ItemCategory.Unknown
            };
        }

        public static bool IsArtifact(string definitionId)
        {
            return Category(definitionId) == ItemCategory.Artifact;
        }

        public static bool IsWeapon(string definitionId)
        {
            return Category(definitionId) == ItemCategory.Weapon;
        }

        public static bool IsStackable(string definitionId)
        {
            return string.Equals(
                definitionId,
                ItemDefinitionIds.Arrow,
                StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    ItemDefinitionIds.HealthPack,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    ItemDefinitionIds.Coal,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    ItemDefinitionIds.CopperCoin,
                    StringComparison.Ordinal);
        }

        public static int MaximumStack(string definitionId)
        {
            return definitionId switch
            {
                ItemDefinitionIds.Arrow => 64,
                ItemDefinitionIds.HealthPack => 64,
                ItemDefinitionIds.Coal => 10,
                ItemDefinitionIds.CopperCoin => 100,
                _ => 1
            };
        }

        public static IReadOnlyList<Vector2Int> GetFootprint(
            string definitionId,
            int quarterTurns)
        {
            if (definitionId == ItemDefinitionIds.LootShortSword)
            {
                return RotateFootprint(
                    new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 2)
                    },
                    quarterTurns);
            }
            if (definitionId == ItemDefinitionIds.LootHuntingBow)
            {
                return RotateFootprint(
                    new[]
                    {
                        new Vector2Int(0, 0), new Vector2Int(1, 0),
                        new Vector2Int(0, 1), new Vector2Int(1, 1),
                        new Vector2Int(0, 2), new Vector2Int(1, 2)
                    },
                    quarterTurns);
            }

            // Current prototype items are one cell. New item definitions can
            // return any normalized tile set here, including concave shapes.
            return RotateFootprint(
                new[] { Vector2Int.zero },
                quarterTurns);
        }

        public static Vector2Int[] RotateFootprint(
            IReadOnlyList<Vector2Int> footprint,
            int quarterTurns)
        {
            if (footprint == null || footprint.Count == 0)
            {
                return new[] { Vector2Int.zero };
            }

            int rotations = ((quarterTurns % 4) + 4) % 4;
            var result = new Vector2Int[footprint.Count];
            for (int index = 0; index < footprint.Count; index++)
            {
                Vector2Int point = footprint[index];
                for (int turn = 0; turn < rotations; turn++)
                {
                    point = new Vector2Int(-point.y, point.x);
                }
                result[index] = point;
            }
            NormalizeFootprint(result);
            return result;
        }

        public static Vector2Int RotateFootprintOffsetClockwise(
            IReadOnlyList<Vector2Int> currentFootprint,
            Vector2Int currentOffset)
        {
            if (currentFootprint == null ||
                currentFootprint.Count == 0)
            {
                return Vector2Int.zero;
            }

            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int index = 0; index < currentFootprint.Count; index++)
            {
                Vector2Int rotated = new Vector2Int(
                    -currentFootprint[index].y,
                    currentFootprint[index].x);
                minimumX = Math.Min(minimumX, rotated.x);
                minimumY = Math.Min(minimumY, rotated.y);
            }
            Vector2Int rotatedOffset = new Vector2Int(
                -currentOffset.y,
                currentOffset.x);
            return new Vector2Int(
                rotatedOffset.x - minimumX,
                rotatedOffset.y - minimumY);
        }

        private static void NormalizeFootprint(Vector2Int[] footprint)
        {
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int index = 0; index < footprint.Length; index++)
            {
                minimumX = Math.Min(minimumX, footprint[index].x);
                minimumY = Math.Min(minimumY, footprint[index].y);
            }
            for (int index = 0; index < footprint.Length; index++)
            {
                footprint[index] = new Vector2Int(
                    footprint[index].x - minimumX,
                    footprint[index].y - minimumY);
            }
        }

        public static Texture2D LoadIcon(string definitionId)
        {
            return definitionId switch
            {
                ItemDefinitionIds.Arrow =>
                    arrowIcon ??= Resources.Load<Texture2D>(
                        "Inventory Icons/Arrow Icon"),
                ItemDefinitionIds.HealthPack =>
                    healthPackIcon ??= Resources.Load<Texture2D>(
                        "Inventory Icons/Health Pack Icon"),
                ItemDefinitionIds.IronIngot =>
                    ironIngotIcon ??= Resources.Load<Texture2D>(
                        "Inventory Icons/Iron Ingot Icon"),
                ItemDefinitionIds.Coal =>
                    coalIcon ??= Resources.Load<Texture2D>(
                        "Inventory Icons/Coal Icon"),
                ItemDefinitionIds.CopperCoin =>
                    copperCoinIcon ??= Resources.Load<Texture2D>(
                        "Inventory Icons/Copper Tree Coin Icon"),
                ItemDefinitionIds.OwlEyeSeal =>
                    owlEyeSealIcon ??= Resources.Load<Texture2D>(
                        "Inventory Icons/Owl I Seal Icon") ??
                    Resources.Load<Texture2D>(
                        "Inventory Icons/Owl Eye Seal Icon"),
                _ => null
            };
        }
    }

    public static class ItemGridPlacement
    {
        public static StorageEntry GetEntryAtSlot(
            IReadOnlyList<StorageEntry> entries,
            int slotIndex,
            int columns,
            int rows)
        {
            if (entries == null ||
                slotIndex < 0 ||
                slotIndex >= columns * rows)
            {
                return null;
            }
            for (int index = 0; index < entries.Count; index++)
            {
                StorageEntry entry = entries[index];
                if (entry != null &&
                    OccupiesSlot(entry, slotIndex, columns, rows))
                {
                    return entry;
                }
            }
            return null;
        }

        public static bool OccupiesSlot(
            StorageEntry entry,
            int slotIndex,
            int columns,
            int rows)
        {
            if (entry == null || entry.SlotIndex < 0)
            {
                return false;
            }
            IReadOnlyList<Vector2Int> footprint =
                ItemDefinitionCatalog.GetFootprint(
                    entry.DefinitionId,
                    entry.RotationQuarterTurns);
            if (!TryGetOccupiedSlots(
                    footprint,
                    entry.SlotIndex,
                    columns,
                    rows,
                    out int[] slots))
            {
                return false;
            }
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] == slotIndex)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CanPlace(
            IReadOnlyList<StorageEntry> entries,
            StorageEntry candidate,
            int anchorSlot,
            int columns,
            int rows,
            string ignoredEntryId = null)
        {
            if (candidate == null)
            {
                return false;
            }
            IReadOnlyList<Vector2Int> footprint =
                ItemDefinitionCatalog.GetFootprint(
                    candidate.DefinitionId,
                    candidate.RotationQuarterTurns);
            if (!TryGetOccupiedSlots(
                    footprint,
                    anchorSlot,
                    columns,
                    rows,
                    out int[] candidateSlots))
            {
                return false;
            }
            for (int index = 0; index < candidateSlots.Length; index++)
            {
                StorageEntry occupant = GetEntryAtSlot(
                    entries,
                    candidateSlots[index],
                    columns,
                    rows);
                if (occupant != null &&
                    !string.Equals(
                        occupant.EntryId,
                        ignoredEntryId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        public static int FindFirstAvailableSlot(
            IReadOnlyList<StorageEntry> entries,
            StorageEntry candidate,
            int columns,
            int rows)
        {
            for (int slot = 0; slot < columns * rows; slot++)
            {
                if (CanPlace(
                        entries,
                        candidate,
                        slot,
                        columns,
                        rows))
                {
                    return slot;
                }
            }
            return -1;
        }

        public static bool TryGetOccupiedSlots(
            IReadOnlyList<Vector2Int> footprint,
            int anchorSlot,
            int columns,
            int rows,
            out int[] occupiedSlots)
        {
            occupiedSlots = Array.Empty<int>();
            if (footprint == null ||
                footprint.Count == 0 ||
                columns <= 0 ||
                rows <= 0 ||
                anchorSlot < 0 ||
                anchorSlot >= columns * rows)
            {
                return false;
            }

            int anchorColumn = anchorSlot % columns;
            int anchorRow = anchorSlot / columns;
            occupiedSlots = new int[footprint.Count];
            var unique = new HashSet<int>();
            for (int index = 0; index < footprint.Count; index++)
            {
                int column = anchorColumn + footprint[index].x;
                int row = anchorRow + footprint[index].y;
                if (column < 0 || column >= columns ||
                    row < 0 || row >= rows)
                {
                    occupiedSlots = Array.Empty<int>();
                    return false;
                }
                int slot = row * columns + column;
                if (!unique.Add(slot))
                {
                    occupiedSlots = Array.Empty<int>();
                    return false;
                }
                occupiedSlots[index] = slot;
            }
            return true;
        }
    }
}
