using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    public enum RaidSessionState
    {
        Active = 0,
        Completed = 1,
    }

    public enum RaidCompletionReason
    {
        Extracted = 0,
        PlayerDied = 1,
        Abandoned = 2,
    }

    [Serializable]
    public sealed class RaidLaunchRequest
    {
        [SerializeField] private string raidSessionId;
        [SerializeField] private string profileId;
        [SerializeField] private string raidDefinitionId;
        [SerializeField] private string raidPresetId;
        [SerializeField] private int seed;
        [SerializeField] private bool commitOutcomeToProfile;
        [SerializeField] private string requestedUtc;
        [SerializeField] private List<string> carriedStorageEntryIds = new List<string>();

        public string RaidSessionId => raidSessionId;
        public string ProfileId => profileId;
        public string RaidDefinitionId => raidDefinitionId;
        public string RaidPresetId => raidPresetId;
        public int Seed => seed;
        public bool CommitOutcomeToProfile => commitOutcomeToProfile;
        public string RequestedUtc => requestedUtc;
        public IReadOnlyList<string> CarriedStorageEntryIds => carriedStorageEntryIds;

        public static RaidLaunchRequest Create(
            GameLaunchContext context,
            PlayerProfile profile,
            int? seedOverride = null,
            IEnumerable<string> carriedStorageEntryIds = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            int seed = seedOverride ??
                (context.HasFixedRaidSeed
                    ? context.RaidSeed
                    : GenerateSeed());

            RaidLaunchRequest request = new RaidLaunchRequest
            {
                raidSessionId = LoopDataUtility.CreateId(),
                profileId = profile.ProfileId,
                raidDefinitionId = context.RaidDefinitionId,
                raidPresetId = context.RaidPresetId,
                seed = seed,
                commitOutcomeToProfile = context.PersistenceEnabled,
                requestedUtc = LoopDataUtility.UtcTimestamp(),
            };

            if (carriedStorageEntryIds != null)
            {
                foreach (string entryId in carriedStorageEntryIds)
                {
                    if (!string.IsNullOrWhiteSpace(entryId))
                    {
                        request.carriedStorageEntryIds.Add(entryId.Trim());
                    }
                }
            }

            request.Normalize();
            return request;
        }

        public RaidLaunchRequest Clone()
        {
            RaidLaunchRequest clone = new RaidLaunchRequest
            {
                raidSessionId = raidSessionId,
                profileId = profileId,
                raidDefinitionId = raidDefinitionId,
                raidPresetId = raidPresetId,
                seed = seed,
                commitOutcomeToProfile = commitOutcomeToProfile,
                requestedUtc = requestedUtc,
                carriedStorageEntryIds = new List<string>(carriedStorageEntryIds),
            };
            clone.Normalize();
            return clone;
        }

        public void Normalize()
        {
            raidSessionId = LoopDataUtility.EnsureId(raidSessionId);
            profileId = LoopDataUtility.EnsureId(profileId);
            raidDefinitionId = string.IsNullOrWhiteSpace(raidDefinitionId)
                ? GameLaunchContext.DefaultRaidDefinition
                : raidDefinitionId.Trim();
            raidPresetId = string.IsNullOrWhiteSpace(raidPresetId)
                ? "raid-default"
                : raidPresetId.Trim();
            requestedUtc = string.IsNullOrWhiteSpace(requestedUtc)
                ? LoopDataUtility.UtcTimestamp()
                : requestedUtc;
            carriedStorageEntryIds ??= new List<string>();
            carriedStorageEntryIds.RemoveAll(string.IsNullOrWhiteSpace);
        }

        private static int GenerateSeed()
        {
            unchecked
            {
                return Guid.NewGuid().GetHashCode() ^ Environment.TickCount;
            }
        }
    }

    [Serializable]
    public sealed class RaidResult
    {
        [SerializeField] private string raidSessionId;
        [SerializeField] private string profileId;
        [SerializeField] private string raidDefinitionId;
        [SerializeField] private string raidPresetId;
        [SerializeField] private int seed;
        [SerializeField] private RaidCompletionReason completionReason;
        [SerializeField] private string completedUtc;
        [SerializeField, Min(0)] private int enemiesDefeated;
        [SerializeField, Min(0)] private int weaponOneExperience;
        [SerializeField, Min(0)] private int weaponTwoExperience;
        [SerializeField] private List<StorageEntry> discoveredStorageEntries =
            new List<StorageEntry>();
        [SerializeField] private List<StorageEntry> returnedStorageEntries =
            new List<StorageEntry>();
        [SerializeField] private List<string> lostStorageEntryIds = new List<string>();

        public string RaidSessionId => raidSessionId;
        public string ProfileId => profileId;
        public string RaidDefinitionId => raidDefinitionId;
        public string RaidPresetId => raidPresetId;
        public int Seed => seed;
        public RaidCompletionReason CompletionReason => completionReason;
        public string CompletedUtc => completedUtc;
        public int EnemiesDefeated => enemiesDefeated;
        public int WeaponOneExperience => weaponOneExperience;
        public int WeaponTwoExperience => weaponTwoExperience;
        public IReadOnlyList<StorageEntry> DiscoveredStorageEntries =>
            discoveredStorageEntries;
        public IReadOnlyList<StorageEntry> ReturnedStorageEntries =>
            returnedStorageEntries;
        public IReadOnlyList<string> LostStorageEntryIds => lostStorageEntryIds;
        public bool Extracted => completionReason == RaidCompletionReason.Extracted;
        public bool PlayerDied => completionReason == RaidCompletionReason.PlayerDied;

        public RaidResult Clone()
        {
            RaidResult clone = new RaidResult
            {
                raidSessionId = raidSessionId,
                profileId = profileId,
                raidDefinitionId = raidDefinitionId,
                raidPresetId = raidPresetId,
                seed = seed,
                completionReason = completionReason,
                completedUtc = completedUtc,
                enemiesDefeated = enemiesDefeated,
                weaponOneExperience = weaponOneExperience,
                weaponTwoExperience = weaponTwoExperience,
                discoveredStorageEntries = CloneEntries(discoveredStorageEntries),
                returnedStorageEntries = CloneEntries(returnedStorageEntries),
                lostStorageEntryIds = new List<string>(lostStorageEntryIds),
            };
            clone.Normalize();
            return clone;
        }

        internal static RaidResult Create(
            RaidLaunchRequest request,
            RaidCompletionReason reason,
            IReadOnlyList<StorageEntry> discoveredEntries,
            int enemiesDefeated,
            int weaponOneExperience,
            int weaponTwoExperience)
        {
            RaidResult result = new RaidResult
            {
                raidSessionId = request.RaidSessionId,
                profileId = request.ProfileId,
                raidDefinitionId = request.RaidDefinitionId,
                raidPresetId = request.RaidPresetId,
                seed = request.Seed,
                completionReason = reason,
                completedUtc = LoopDataUtility.UtcTimestamp(),
                enemiesDefeated = Math.Max(0, enemiesDefeated),
                weaponOneExperience = Math.Max(0, weaponOneExperience),
                weaponTwoExperience = Math.Max(0, weaponTwoExperience),
                discoveredStorageEntries = CloneEntries(discoveredEntries),
            };

            if (reason == RaidCompletionReason.Extracted)
            {
                result.returnedStorageEntries = CloneEntries(discoveredEntries);
            }
            else if (reason == RaidCompletionReason.PlayerDied)
            {
                result.lostStorageEntryIds =
                    new List<string>(request.CarriedStorageEntryIds);
            }

            result.Normalize();
            return result;
        }

        public void Normalize()
        {
            raidSessionId = LoopDataUtility.EnsureId(raidSessionId);
            profileId = LoopDataUtility.EnsureId(profileId);
            raidDefinitionId = string.IsNullOrWhiteSpace(raidDefinitionId)
                ? GameLaunchContext.DefaultRaidDefinition
                : raidDefinitionId.Trim();
            raidPresetId = string.IsNullOrWhiteSpace(raidPresetId)
                ? "raid-default"
                : raidPresetId.Trim();
            completedUtc = string.IsNullOrWhiteSpace(completedUtc)
                ? LoopDataUtility.UtcTimestamp()
                : completedUtc;
            enemiesDefeated = Math.Max(0, enemiesDefeated);
            weaponOneExperience = Math.Max(0, weaponOneExperience);
            weaponTwoExperience = Math.Max(0, weaponTwoExperience);
            discoveredStorageEntries = NormalizeEntries(discoveredStorageEntries);
            returnedStorageEntries = NormalizeEntries(returnedStorageEntries);
            lostStorageEntryIds ??= new List<string>();
            lostStorageEntryIds.RemoveAll(string.IsNullOrWhiteSpace);
        }

        private static List<StorageEntry> CloneEntries(
            IReadOnlyList<StorageEntry> source)
        {
            List<StorageEntry> entries = new List<StorageEntry>();
            if (source == null)
            {
                return entries;
            }

            for (int index = 0; index < source.Count; index++)
            {
                StorageEntry entry = source[index];
                if (entry != null)
                {
                    entries.Add(entry.Clone());
                }
            }

            return entries;
        }

        private static List<StorageEntry> NormalizeEntries(List<StorageEntry> source)
        {
            source ??= new List<StorageEntry>();
            source.RemoveAll(entry => entry == null);
            foreach (StorageEntry entry in source)
            {
                entry.Normalize();
            }

            return source;
        }
    }

    [Serializable]
    public sealed class RaidSession
    {
        [SerializeField] private RaidLaunchRequest launchRequest;
        [SerializeField] private RaidSessionState state = RaidSessionState.Active;
        [SerializeField] private string startedUtc;
        [SerializeField] private List<StorageEntry> collectedStorageEntries =
            new List<StorageEntry>();
        [SerializeField, Min(0)] private int enemiesDefeated;
        [SerializeField, Min(0)] private int weaponOneExperience;
        [SerializeField, Min(0)] private int weaponTwoExperience;

        public RaidLaunchRequest LaunchRequest => launchRequest;
        public RaidSessionState State => state;
        public string StartedUtc => startedUtc;
        public IReadOnlyList<StorageEntry> CollectedStorageEntries =>
            collectedStorageEntries;
        public int EnemiesDefeated => enemiesDefeated;
        public bool IsActive => state == RaidSessionState.Active;

        public RaidSession(RaidLaunchRequest request)
        {
            launchRequest = request?.Clone() ??
                throw new ArgumentNullException(nameof(request));
            startedUtc = LoopDataUtility.UtcTimestamp();
        }

        public void RecordLoot(StorageEntry entry)
        {
            RecordLoot(entry, null);
        }

        public void RecordLoot(
            StorageEntry entry,
            PlayerProfile profile)
        {
            EnsureActive();
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (GetAvailableCarriedCapacity(
                    entry,
                    profile) < entry.Quantity)
            {
                throw new InvalidOperationException(
                    "The player inventory is full.");
            }

            int moved = TryAddCarried(
                entry,
                -1,
                true,
                profile);
            if (moved != entry.Quantity)
            {
                throw new InvalidOperationException(
                    "The player inventory is full.");
            }
        }

        public IReadOnlyList<StorageEntry> GetCarriedEntries(
            PlayerProfile profile)
        {
            var entries = new List<StorageEntry>();
            for (int index = 0;
                 index < launchRequest.CarriedStorageEntryIds.Count;
                 index++)
            {
                StorageEntry entry = profile != null
                    ? profile.FindStorageEntry(
                        launchRequest.CarriedStorageEntryIds[index])
                    : null;
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            for (int index = 0;
                 index < collectedStorageEntries.Count;
                 index++)
            {
                StorageEntry entry = collectedStorageEntries[index];
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        public StorageEntry GetCarriedEntryAtSlot(
            int slotIndex,
            PlayerProfile profile)
        {
            if (slotIndex < 0 ||
                slotIndex >= PlayerProfile.InventoryCapacity)
            {
                return null;
            }
            IReadOnlyList<StorageEntry> entries =
                GetCarriedEntries(profile);
            return ItemGridPlacement.GetEntryAtSlot(
                entries,
                slotIndex,
                PlayerProfile.InventoryColumns,
                PlayerProfile.InventoryRows);
        }

        public bool TryTakeCarried(
            string entryId,
            int quantity,
            PlayerProfile profile,
            out StorageEntry taken)
        {
            EnsureActive();
            taken = null;
            if (string.IsNullOrWhiteSpace(entryId) || quantity <= 0)
            {
                return false;
            }

            if (profile != null)
            {
                StorageEntry profileEntry =
                    profile.FindStorageEntry(entryId);
                if (profileEntry != null &&
                    launchRequest.CarriedStorageEntryIds.Contains(entryId))
                {
                    int amount = Math.Min(quantity, profileEntry.Quantity);
                    taken = amount == profileEntry.Quantity
                        ? profileEntry.Clone()
                        : profileEntry.CreateSplitCopy(amount);
                    profileEntry.RemoveQuantity(amount);
                    if (profileEntry.Quantity <= 0)
                    {
                        profile.RemoveStorageEntry(entryId);
                    }
                    return true;
                }
            }

            StorageEntry collected = collectedStorageEntries.Find(entry =>
                entry != null &&
                string.Equals(
                    entry.EntryId,
                    entryId,
                    StringComparison.Ordinal));
            if (collected == null)
            {
                return false;
            }
            int collectedAmount = Math.Min(quantity, collected.Quantity);
            taken = collectedAmount == collected.Quantity
                ? collected.Clone()
                : collected.CreateSplitCopy(collectedAmount);
            collected.RemoveQuantity(collectedAmount);
            if (collected.Quantity <= 0)
            {
                collectedStorageEntries.Remove(collected);
            }
            return true;
        }

        public int TryAddCarried(
            StorageEntry incoming,
            int targetSlot,
            bool autoStack,
            PlayerProfile profile)
        {
            EnsureActive();
            if (incoming == null || incoming.Quantity <= 0)
            {
                return 0;
            }

            int remaining = incoming.Quantity;
            int moved = 0;
            int maximumStack = ItemDefinitionCatalog.MaximumStack(
                incoming.DefinitionId);
            if (autoStack)
            {
                IReadOnlyList<StorageEntry> carried =
                    GetCarriedEntries(profile);
                for (int index = 0;
                     index < carried.Count && remaining > 0;
                     index++)
                {
                    StorageEntry stack = carried[index];
                    if (!CanStack(stack, incoming) ||
                        stack.Quantity >= maximumStack)
                    {
                        continue;
                    }
                    int amount = Math.Min(
                        remaining,
                        maximumStack - stack.Quantity);
                    stack.SetQuantity(stack.Quantity + amount);
                    remaining -= amount;
                    moved += amount;
                }

                while (remaining > 0)
                {
                    int slot = FindAvailableCarriedSlot(
                        incoming,
                        profile);
                    if (slot < 0)
                    {
                        break;
                    }
                    int amount = Math.Min(remaining, maximumStack);
                    StorageEntry added = moved == 0 &&
                        amount == incoming.Quantity
                            ? incoming.Clone()
                            : incoming.CreateSplitCopy(amount);
                    added.SetSlotIndex(slot);
                    collectedStorageEntries.Add(added);
                    remaining -= amount;
                    moved += amount;
                }
                return moved;
            }

            if (targetSlot < 0 ||
                targetSlot >= PlayerProfile.InventoryCapacity)
            {
                return 0;
            }
            StorageEntry occupant = GetCarriedEntryAtSlot(
                targetSlot,
                profile);
            if (occupant == null)
            {
                if (!ItemGridPlacement.CanPlace(
                        GetCarriedEntries(profile),
                        incoming,
                        targetSlot,
                        PlayerProfile.InventoryColumns,
                        PlayerProfile.InventoryRows))
                {
                    return 0;
                }
                int amount = Math.Min(remaining, maximumStack);
                StorageEntry added = amount == incoming.Quantity
                    ? incoming.Clone()
                    : incoming.CreateSplitCopy(amount);
                added.SetSlotIndex(targetSlot);
                collectedStorageEntries.Add(added);
                return amount;
            }
            if (!CanStack(occupant, incoming))
            {
                return 0;
            }
            int merged = Math.Min(
                remaining,
                maximumStack - occupant.Quantity);
            occupant.SetQuantity(occupant.Quantity + merged);
            return merged;
        }

        private int FindAvailableCarriedSlot(
            StorageEntry candidate,
            PlayerProfile profile)
        {
            if (profile != null)
            {
                return ItemGridPlacement.FindFirstAvailableSlot(
                    GetCarriedEntries(profile),
                    candidate,
                    PlayerProfile.InventoryColumns,
                    PlayerProfile.InventoryRows);
            }
            for (int slot = launchRequest.CarriedStorageEntryIds.Count;
                 slot < PlayerProfile.InventoryCapacity;
                 slot++)
            {
                if (ItemGridPlacement.CanPlace(
                        collectedStorageEntries,
                        candidate,
                        slot,
                        PlayerProfile.InventoryColumns,
                        PlayerProfile.InventoryRows))
                {
                    return slot;
                }
            }
            return -1;
        }

        private int GetAvailableCarriedCapacity(
            StorageEntry incoming,
            PlayerProfile profile)
        {
            int maximumStack = ItemDefinitionCatalog.MaximumStack(
                incoming.DefinitionId);
            int available = 0;
            IReadOnlyList<StorageEntry> entries =
                GetCarriedEntries(profile);
            for (int index = 0; index < entries.Count; index++)
            {
                StorageEntry entry = entries[index];
                if (CanStack(entry, incoming))
                {
                    available += Math.Max(
                        0,
                        maximumStack - entry.Quantity);
                }
            }
            var simulated = new List<StorageEntry>(entries);
            if (profile == null)
            {
                for (int index = simulated.Count;
                     index < launchRequest.CarriedStorageEntryIds.Count;
                     index++)
                {
                    StorageEntry placeholder = StorageEntry.Create(
                        $"reserved-slot-{index}");
                    placeholder.SetSlotIndex(index);
                    simulated.Add(placeholder);
                }
            }
            while (true)
            {
                int slot = ItemGridPlacement.FindFirstAvailableSlot(
                    simulated,
                    incoming,
                    PlayerProfile.InventoryColumns,
                    PlayerProfile.InventoryRows);
                if (slot < 0)
                {
                    break;
                }
                StorageEntry placeholder = incoming.Clone();
                placeholder.SetSlotIndex(slot);
                simulated.Add(placeholder);
                available += maximumStack;
            }
            return available;
        }

        private static bool CanStack(
            StorageEntry existing,
            StorageEntry incoming)
        {
            return existing != null &&
                incoming != null &&
                ItemDefinitionCatalog.IsStackable(
                    incoming.DefinitionId) &&
                string.Equals(
                    existing.DefinitionId,
                    incoming.DefinitionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existing.CustomStateJson,
                    incoming.CustomStateJson,
                    StringComparison.Ordinal);
        }

        public int GetItemQuantity(
            string definitionId,
            PlayerProfile profile)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return 0;
            }

            int quantity = 0;
            if (profile != null)
            {
                for (int index = 0;
                     index < launchRequest.CarriedStorageEntryIds.Count;
                     index++)
                {
                    StorageEntry entry = profile.FindStorageEntry(
                        launchRequest.CarriedStorageEntryIds[index]);
                    if (entry != null &&
                        string.Equals(
                            entry.DefinitionId,
                            definitionId,
                            StringComparison.Ordinal))
                    {
                        quantity += entry.Quantity;
                    }
                }
            }

            for (int index = 0;
                 index < collectedStorageEntries.Count;
                 index++)
            {
                StorageEntry entry = collectedStorageEntries[index];
                if (entry != null &&
                    string.Equals(
                        entry.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    quantity += entry.Quantity;
                }
            }
            return quantity;
        }

        public bool TryConsumeItem(
            string definitionId,
            int quantity,
            PlayerProfile profile)
        {
            EnsureActive();
            int remaining = Math.Max(0, quantity);
            if (remaining == 0)
            {
                return true;
            }
            if (GetItemQuantity(definitionId, profile) < remaining)
            {
                return false;
            }

            if (profile != null)
            {
                for (int index = 0;
                     index < launchRequest.CarriedStorageEntryIds.Count &&
                     remaining > 0;
                     index++)
                {
                    StorageEntry entry = profile.FindStorageEntry(
                        launchRequest.CarriedStorageEntryIds[index]);
                    if (entry == null ||
                        !string.Equals(
                            entry.DefinitionId,
                            definitionId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    remaining -= entry.RemoveQuantity(remaining);
                    if (entry.Quantity <= 0)
                    {
                        profile.RemoveStorageEntry(entry.EntryId);
                    }
                }
            }

            for (int index = collectedStorageEntries.Count - 1;
                 index >= 0 && remaining > 0;
                 index--)
            {
                StorageEntry entry = collectedStorageEntries[index];
                if (entry == null ||
                    !string.Equals(
                        entry.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                remaining -= entry.RemoveQuantity(remaining);
                if (entry.Quantity <= 0)
                {
                    collectedStorageEntries.RemoveAt(index);
                }
            }
            return remaining == 0;
        }

        public void RecordEnemyDefeated(int count = 1)
        {
            EnsureActive();
            enemiesDefeated = Math.Max(0, enemiesDefeated + Math.Max(0, count));
        }

        public void AddWeaponExperience(int oneBasedSlot, int amount)
        {
            EnsureActive();
            int safeAmount = Math.Max(0, amount);
            switch (oneBasedSlot)
            {
                case 1:
                    weaponOneExperience += safeAmount;
                    break;
                case 2:
                    weaponTwoExperience += safeAmount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(oneBasedSlot),
                        "Weapon slots are one-based and limited to 1 or 2.");
            }
        }

        public RaidResult Complete(RaidCompletionReason reason)
        {
            EnsureActive();
            state = RaidSessionState.Completed;
            return RaidResult.Create(
                launchRequest,
                reason,
                collectedStorageEntries,
                enemiesDefeated,
                weaponOneExperience,
                weaponTwoExperience);
        }

        internal void ReopenAfterFailedCompletion()
        {
            state = RaidSessionState.Active;
        }

        private void EnsureActive()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("The raid session has already completed.");
            }
        }
    }
}
