using System;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    [Serializable]
    public sealed class RaidOutcomeReceipt
    {
        [SerializeField] private string raidSessionId;
        [SerializeField] private bool persisted;
        [SerializeField, Min(0)] private int itemsAdded;
        [SerializeField, Min(0)] private int itemsRemoved;

        public string RaidSessionId => raidSessionId;
        public bool Persisted => persisted;
        public int ItemsAdded => itemsAdded;
        public int ItemsRemoved => itemsRemoved;

        internal static RaidOutcomeReceipt Create(
            string raidSessionId,
            bool persisted,
            int itemsAdded,
            int itemsRemoved)
        {
            return new RaidOutcomeReceipt
            {
                raidSessionId = raidSessionId,
                persisted = persisted,
                itemsAdded = Math.Max(0, itemsAdded),
                itemsRemoved = Math.Max(0, itemsRemoved),
            };
        }
    }

    public interface IRaidOutcomeSink
    {
        bool PersistsToDisk { get; }
        RaidOutcomeReceipt Apply(RaidResult result, PlayerProfile profile);
    }

    public sealed class PersistentRaidOutcomeSink : IRaidOutcomeSink
    {
        private readonly IPlayerProfileStore profileStore;
        private readonly string profileSlotId;

        public PersistentRaidOutcomeSink(
            IPlayerProfileStore profileStore,
            string profileSlotId)
        {
            this.profileStore = profileStore ??
                throw new ArgumentNullException(nameof(profileStore));
            if (!profileStore.IsPersistent)
            {
                throw new ArgumentException(
                    "A persistent outcome sink requires a persistent profile store.",
                    nameof(profileStore));
            }

            this.profileSlotId = ProfileSlotUtility.Validate(profileSlotId);
        }

        public bool PersistsToDisk => true;

        public RaidOutcomeReceipt Apply(RaidResult result, PlayerProfile profile)
        {
            RaidOutcomeReceipt receipt = RaidOutcomeRules.Apply(
                result,
                profile,
                persisted: true);
            profileStore.Save(profileSlotId, profile);
            return receipt;
        }
    }

    public sealed class MemoryRaidOutcomeSink : IRaidOutcomeSink
    {
        public bool PersistsToDisk => false;
        public RaidResult LastResult { get; private set; }

        public RaidOutcomeReceipt Apply(RaidResult result, PlayerProfile profile)
        {
            RaidOutcomeReceipt receipt = RaidOutcomeRules.Apply(
                result,
                profile,
                persisted: false);
            LastResult = result.Clone();
            return receipt;
        }
    }

    internal static class RaidOutcomeRules
    {
        public static RaidOutcomeReceipt Apply(
            RaidResult result,
            PlayerProfile profile,
            bool persisted)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (!string.Equals(
                    result.ProfileId,
                    profile.ProfileId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A raid result cannot be applied to a different player profile.");
            }

            int added = 0;
            foreach (StorageEntry entry in result.ReturnedStorageEntries)
            {
                profile.AddToStorage(entry);
                added++;
            }

            int removed = 0;
            foreach (string entryId in result.LostStorageEntryIds)
            {
                if (profile.RemoveStorageEntry(entryId))
                {
                    removed++;
                }
            }

            profile.WeaponOne.AddExperience(result.WeaponOneExperience);
            profile.WeaponTwo.AddExperience(result.WeaponTwoExperience);
            return RaidOutcomeReceipt.Create(
                result.RaidSessionId,
                persisted,
                added,
                removed);
        }
    }
}
