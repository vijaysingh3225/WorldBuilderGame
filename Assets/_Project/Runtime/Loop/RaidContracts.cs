using System;
using System.Collections.Generic;
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
            EnsureActive();
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            StorageEntry copy = entry.Clone();
            copy.Normalize();
            collectedStorageEntries.Add(copy);
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
