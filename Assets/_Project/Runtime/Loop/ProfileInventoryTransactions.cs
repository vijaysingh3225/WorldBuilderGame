using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    public static class ProfileInventoryTransactions
    {
        public static bool TryTakeInventory(
            PlayerProfile profile,
            StorageEntry entry,
            int quantity,
            out StorageEntry taken)
        {
            return TryTake(
                profile,
                entry,
                quantity,
                profile != null && entry != null &&
                    profile.IsInInventory(entry.EntryId),
                out taken);
        }

        public static bool TryTakeChest(
            PlayerProfile profile,
            string chestId,
            StorageEntry entry,
            int quantity,
            out StorageEntry taken)
        {
            bool ownsEntry = false;
            if (profile != null && entry != null)
            {
                IReadOnlyList<string> ids = profile.GetChestEntryIds(chestId);
                for (int index = 0; index < ids.Count; index++)
                {
                    if (string.Equals(
                            ids[index],
                            entry.EntryId,
                            StringComparison.Ordinal))
                    {
                        ownsEntry = true;
                        break;
                    }
                }
            }
            return TryTake(profile, entry, quantity, ownsEntry, out taken);
        }

        public static bool TryTakeSecure(
            PlayerProfile profile,
            StorageEntry entry,
            int quantity,
            out StorageEntry taken)
        {
            return TryTake(
                profile,
                entry,
                quantity,
                profile != null && entry != null &&
                    profile.IsInSecureContainer(entry.EntryId),
                out taken);
        }

        public static int TryAddInventory(
            PlayerProfile profile,
            StorageEntry incoming,
            int targetSlot,
            bool autoStack)
        {
            return TryAdd(
                profile,
                incoming,
                targetSlot,
                autoStack,
                PlayerProfile.InventoryColumns,
                PlayerProfile.InventoryRows,
                () => GetInventoryEntries(profile),
                (entry, slot) => profile.TryMoveToInventory(
                    entry.EntryId,
                    slot));
        }

        public static int TryAddChest(
            PlayerProfile profile,
            string chestId,
            StorageEntry incoming,
            int targetSlot,
            bool autoStack)
        {
            return TryAdd(
                profile,
                incoming,
                targetSlot,
                autoStack,
                PlayerProfile.ChestColumns,
                PlayerProfile.ChestRows,
                () => GetChestEntries(profile, chestId),
                (entry, slot) => profile.MoveToChest(
                    entry.EntryId,
                    chestId,
                    slot));
        }

        public static int TryAddSecure(
            PlayerProfile profile,
            StorageEntry incoming,
            int targetSlot,
            bool autoStack)
        {
            return TryAdd(
                profile,
                incoming,
                targetSlot,
                autoStack,
                PlayerProfile.SecureColumns,
                PlayerProfile.SecureRows,
                () => GetSecureEntries(profile),
                (entry, slot) => profile.TryMoveToSecureContainer(
                    entry.EntryId,
                    slot));
        }

        private static bool TryTake(
            PlayerProfile profile,
            StorageEntry entry,
            int quantity,
            bool ownsEntry,
            out StorageEntry taken)
        {
            taken = null;
            if (profile == null || entry == null || quantity <= 0 ||
                !ownsEntry ||
                !ReferenceEquals(
                    profile.FindStorageEntry(entry.EntryId),
                    entry))
            {
                return false;
            }

            int amount = Mathf.Min(quantity, entry.Quantity);
            taken = amount == entry.Quantity
                ? entry.Clone()
                : entry.CreateSplitCopy(amount);
            entry.RemoveQuantity(amount);
            if (entry.Quantity <= 0)
            {
                profile.RemoveStorageEntry(entry.EntryId);
            }
            return true;
        }

        private static int TryAdd(
            PlayerProfile profile,
            StorageEntry incoming,
            int targetSlot,
            bool autoStack,
            int columns,
            int rows,
            Func<List<StorageEntry>> resolveEntries,
            Func<StorageEntry, int, bool> assignOwnership)
        {
            if (profile == null || incoming == null || incoming.Quantity <= 0)
            {
                return 0;
            }

            int remaining = incoming.Quantity;
            int moved = 0;
            int maximumStack = ItemDefinitionCatalog.MaximumStack(
                incoming.DefinitionId);
            if (autoStack)
            {
                List<StorageEntry> entries = resolveEntries();
                for (int index = 0;
                     index < entries.Count && remaining > 0;
                     index++)
                {
                    StorageEntry stack = entries[index];
                    if (!CanStack(stack, incoming) ||
                        stack.Quantity >= maximumStack)
                    {
                        continue;
                    }
                    int amount = Mathf.Min(
                        remaining,
                        maximumStack - stack.Quantity);
                    stack.SetQuantity(stack.Quantity + amount);
                    remaining -= amount;
                    moved += amount;
                }

                while (remaining > 0)
                {
                    entries = resolveEntries();
                    if (!ItemGridPlacement.
                            TryFindFirstAvailableSlotWithRotation(
                                entries,
                                incoming,
                                columns,
                                rows,
                                out int slot,
                                out int rotationQuarterTurns))
                    {
                        break;
                    }
                    int amount = Mathf.Min(remaining, maximumStack);
                    if (!TryStoreOwnedEntry(
                            profile,
                            incoming,
                            amount,
                            slot,
                            assignOwnership,
                            rotationQuarterTurns))
                    {
                        break;
                    }
                    remaining -= amount;
                    moved += amount;
                }
                return moved;
            }

            if (targetSlot < 0 || targetSlot >= columns * rows)
            {
                return 0;
            }
            List<StorageEntry> currentEntries = resolveEntries();
            StorageEntry occupant = ItemGridPlacement.GetEntryAtSlot(
                currentEntries,
                targetSlot,
                columns,
                rows);
            if (occupant == null)
            {
                if (!ItemGridPlacement.CanPlace(
                        currentEntries,
                        incoming,
                        targetSlot,
                        columns,
                        rows))
                {
                    return 0;
                }
                int amount = Mathf.Min(remaining, maximumStack);
                return TryStoreOwnedEntry(
                    profile,
                    incoming,
                    amount,
                    targetSlot,
                    assignOwnership,
                    incoming.RotationQuarterTurns)
                        ? amount
                        : 0;
            }
            if (!CanStack(occupant, incoming))
            {
                return 0;
            }
            int merged = Mathf.Min(
                remaining,
                maximumStack - occupant.Quantity);
            occupant.SetQuantity(occupant.Quantity + merged);
            return merged;
        }

        private static bool TryStoreOwnedEntry(
            PlayerProfile profile,
            StorageEntry incoming,
            int quantity,
            int slot,
            Func<StorageEntry, int, bool> assignOwnership,
            int rotationQuarterTurns)
        {
            StorageEntry added = quantity == incoming.Quantity
                ? incoming.Clone()
                : incoming.CreateSplitCopy(quantity);
            added.SetSlotIndex(slot);
            added.SetRotationQuarterTurns(rotationQuarterTurns);
            profile.AddToStorage(added);
            StorageEntry stored = profile.FindStorageEntry(added.EntryId);
            if (stored != null && assignOwnership(stored, slot))
            {
                return true;
            }
            profile.RemoveStorageEntry(added.EntryId);
            return false;
        }

        private static List<StorageEntry> GetInventoryEntries(
            PlayerProfile profile)
        {
            return GetEntries(profile, profile.InventoryEntryIds);
        }

        private static List<StorageEntry> GetChestEntries(
            PlayerProfile profile,
            string chestId)
        {
            return GetEntries(profile, profile.GetChestEntryIds(chestId));
        }

        private static List<StorageEntry> GetSecureEntries(
            PlayerProfile profile)
        {
            return GetEntries(profile, profile.SecureEntryIds);
        }

        private static List<StorageEntry> GetEntries(
            PlayerProfile profile,
            IReadOnlyList<string> ids)
        {
            var entries = new List<StorageEntry>(ids.Count);
            for (int index = 0; index < ids.Count; index++)
            {
                StorageEntry entry = profile.FindStorageEntry(ids[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private static bool CanStack(
            StorageEntry existing,
            StorageEntry incoming)
        {
            return existing != null && incoming != null &&
                ItemDefinitionCatalog.IsStackable(incoming.DefinitionId) &&
                string.Equals(
                    existing.DefinitionId,
                    incoming.DefinitionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existing.CustomStateJson,
                    incoming.CustomStateJson,
                    StringComparison.Ordinal);
        }
    }
}
