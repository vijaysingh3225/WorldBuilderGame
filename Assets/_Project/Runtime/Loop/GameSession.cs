using System;
using System.Collections.Generic;

namespace WorldBuilder.Gameplay.Loop
{
    public sealed class GameSession
    {
        private readonly IPlayerProfileStore profileStore;
        private readonly IRaidOutcomeSink outcomeSink;

        public GameSession(
            GameLaunchContext launchContext,
            IPlayerProfileStore profileStore,
            PlayerProfile sandboxSeedProfile = null,
            IRaidOutcomeSink outcomeSink = null,
            bool allowProfileOverwrite = false)
        {
            LaunchContext = launchContext?.Clone() ??
                throw new ArgumentNullException(nameof(launchContext));
            LaunchContext.Normalize();
            this.profileStore = profileStore ??
                throw new ArgumentNullException(nameof(profileStore));

            if (LaunchContext.PersistenceEnabled && !profileStore.IsPersistent)
            {
                throw new ArgumentException(
                    "Persistent launch modes require a persistent profile store.",
                    nameof(profileStore));
            }

            if (LaunchContext.IsSandbox && profileStore.IsPersistent)
            {
                throw new ArgumentException(
                    "Sandbox launch modes must use a memory-only profile store.",
                    nameof(profileStore));
            }

            ActiveProfile = LoadInitialProfile(
                sandboxSeedProfile,
                allowProfileOverwrite);
            this.outcomeSink = outcomeSink ?? CreateDefaultOutcomeSink();
        }

        public event Action<PlayerProfile> ProfileChanged;
        public event Action<RaidSession> RaidStarted;
        public event Action<RaidResult, RaidOutcomeReceipt> RaidCompleted;

        public GameLaunchContext LaunchContext { get; }
        public PlayerProfile ActiveProfile { get; private set; }
        public RaidSession ActiveRaid { get; private set; }
        public IPlayerProfileStore ProfileStore => profileStore;
        public bool HasActiveRaid => ActiveRaid != null && ActiveRaid.IsActive;

        public RaidLaunchRequest CreateRaidLaunchRequest(
            int? seedOverride = null,
            IEnumerable<string> carriedStorageEntryIds = null)
        {
            return RaidLaunchRequest.Create(
                LaunchContext,
                ActiveProfile,
                seedOverride,
                carriedStorageEntryIds);
        }

        public RaidSession BeginRaid(RaidLaunchRequest request)
        {
            if (HasActiveRaid)
            {
                throw new InvalidOperationException("A raid is already active.");
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.Equals(
                    request.ProfileId,
                    ActiveProfile.ProfileId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The raid launch request belongs to a different player profile.");
            }

            ActiveRaid = new RaidSession(request);
            RaidStarted?.Invoke(ActiveRaid);
            return ActiveRaid;
        }

        public RaidSession BeginRaid(
            int? seedOverride = null,
            IEnumerable<string> carriedStorageEntryIds = null)
        {
            if (!ActiveProfile.TrySetInventoryStack(
                    ItemDefinitionIds.Arrow,
                    20))
            {
                throw new InvalidOperationException(
                    "A raid requires one open backpack slot for the arrow stack.");
            }
            var carriedIds = new List<string>();
            if (carriedStorageEntryIds != null)
            {
                foreach (string entryId in carriedStorageEntryIds)
                {
                    if (!string.IsNullOrWhiteSpace(entryId) &&
                        !carriedIds.Contains(entryId))
                    {
                        carriedIds.Add(entryId);
                    }
                }
            }
            else
            {
                carriedIds.AddRange(ActiveProfile.InventoryEntryIds);
            }

            for (int index = 0;
                 index < ActiveProfile.InventoryEntryIds.Count;
                 index++)
            {
                string entryId = ActiveProfile.InventoryEntryIds[index];
                StorageEntry entry = ActiveProfile.FindStorageEntry(entryId);
                if (entry != null &&
                    string.Equals(
                        entry.DefinitionId,
                        ItemDefinitionIds.Arrow,
                        StringComparison.Ordinal) &&
                    !carriedIds.Contains(entryId))
                {
                    carriedIds.Add(entryId);
                }
            }
            return BeginRaid(
                CreateRaidLaunchRequest(seedOverride, carriedIds));
        }

        public RaidResult CompleteActiveRaid(
            RaidCompletionReason completionReason,
            out RaidOutcomeReceipt receipt)
        {
            if (!HasActiveRaid)
            {
                throw new InvalidOperationException("There is no active raid to complete.");
            }

            RaidSession completingRaid = ActiveRaid;
            PlayerProfile profileSnapshot = ActiveProfile.Clone();
            RaidResult result;
            try
            {
                result = completingRaid.Complete(completionReason);
                receipt = outcomeSink.Apply(result, ActiveProfile);
            }
            catch
            {
                ActiveProfile.RestoreFrom(profileSnapshot);
                completingRaid.ReopenAfterFailedCompletion();
                receipt = null;
                throw;
            }

            ActiveRaid = null;
            ProfileChanged?.Invoke(ActiveProfile);
            RaidCompleted?.Invoke(result, receipt);
            return result;
        }

        public void SaveProfile()
        {
            profileStore.Save(LaunchContext.ProfileSlotId, ActiveProfile);
            ProfileChanged?.Invoke(ActiveProfile);
        }

        public PlayerProfile CreateProfileSnapshot()
        {
            return ActiveProfile.Clone();
        }

        private PlayerProfile LoadInitialProfile(
            PlayerProfile sandboxSeedProfile,
            bool allowProfileOverwrite)
        {
            switch (LaunchContext.Mode)
            {
                case GameLaunchMode.FreshGame:
                {
                    if (profileStore.Exists(LaunchContext.ProfileSlotId) &&
                        !allowProfileOverwrite)
                    {
                        throw new InvalidOperationException(
                            $"A saved profile already exists in slot " +
                            $"'{LaunchContext.ProfileSlotId}'. Explicit overwrite " +
                            "confirmation is required to start a fresh game.");
                    }

                    PlayerProfile profile = PlayerProfile.CreateNew(
                        LaunchContext.ProfileSlotId);
                    profileStore.Save(LaunchContext.ProfileSlotId, profile);
                    return profile;
                }
                case GameLaunchMode.Continue:
                {
                    if (!profileStore.TryLoad(
                            LaunchContext.ProfileSlotId,
                            out PlayerProfile profile))
                    {
                        throw new InvalidOperationException(
                            $"No saved profile exists in slot " +
                            $"'{LaunchContext.ProfileSlotId}'.");
                    }

                    return profile;
                }
                case GameLaunchMode.HomeSandbox:
                case GameLaunchMode.RaidSandbox:
                case GameLaunchMode.CombatLab:
                {
                    PlayerProfile profile = sandboxSeedProfile?.Clone() ??
                        PlayerProfile.CreateNew(LaunchContext.ProfileSlotId, "Developer");
                    profileStore.Save(LaunchContext.ProfileSlotId, profile);
                    return profile;
                }
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(LaunchContext.Mode),
                        LaunchContext.Mode,
                        "Unsupported launch mode.");
            }
        }

        private IRaidOutcomeSink CreateDefaultOutcomeSink()
        {
            if (LaunchContext.PersistenceEnabled)
            {
                return new PersistentRaidOutcomeSink(
                    profileStore,
                    LaunchContext.ProfileSlotId);
            }

            return new MemoryRaidOutcomeSink();
        }
    }
}
