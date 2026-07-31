using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    [Serializable]
    public sealed class StorageEntry
    {
        [SerializeField] private string entryId;
        [SerializeField] private string definitionId;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, TextArea] private string customStateJson;

        public string EntryId => entryId;
        public string DefinitionId => definitionId;
        public int Quantity => quantity;
        public string CustomStateJson => customStateJson;

        public static StorageEntry Create(
            string definitionId,
            int quantity = 1,
            string customStateJson = "")
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("A storage definition ID is required.", nameof(definitionId));
            }

            return new StorageEntry
            {
                entryId = LoopDataUtility.CreateId(),
                definitionId = definitionId.Trim(),
                quantity = Math.Max(1, quantity),
                customStateJson = customStateJson ?? string.Empty,
            };
        }

        public StorageEntry Clone()
        {
            return new StorageEntry
            {
                entryId = entryId,
                definitionId = definitionId,
                quantity = quantity,
                customStateJson = customStateJson,
            };
        }

        internal void Normalize()
        {
            entryId = LoopDataUtility.EnsureId(entryId);
            definitionId = string.IsNullOrWhiteSpace(definitionId)
                ? "unknown-item"
                : definitionId.Trim();
            quantity = Math.Max(1, quantity);
            customStateJson ??= string.Empty;
        }
    }

    [Serializable]
    public sealed class WeaponInstanceRecord
    {
        [SerializeField] private string weaponInstanceId;
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private int experience;
        [SerializeField, TextArea] private string gridStateJson;

        public string WeaponInstanceId => weaponInstanceId;
        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public int Level => level;
        public int Experience => experience;

        // The Weapon Grid module owns this payload. The loop only persists it.
        public string GridStateJson => gridStateJson;

        public static WeaponInstanceRecord Create(string definitionId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("A weapon definition ID is required.", nameof(definitionId));
            }

            return new WeaponInstanceRecord
            {
                weaponInstanceId = LoopDataUtility.CreateId(),
                definitionId = definitionId.Trim(),
                displayName = string.IsNullOrWhiteSpace(displayName)
                    ? definitionId.Trim()
                    : displayName.Trim(),
                level = 1,
                gridStateJson = string.Empty,
            };
        }

        public void SetGridStateJson(string value)
        {
            gridStateJson = value ?? string.Empty;
        }

        public void AddExperience(int amount)
        {
            experience = Math.Max(0, experience + Math.Max(0, amount));
        }

        public void SetLevel(int value)
        {
            level = Math.Max(1, value);
        }

        public WeaponInstanceRecord Clone()
        {
            return new WeaponInstanceRecord
            {
                weaponInstanceId = weaponInstanceId,
                definitionId = definitionId,
                displayName = displayName,
                level = level,
                experience = experience,
                gridStateJson = gridStateJson,
            };
        }

        internal void Normalize(string fallbackDefinitionId, string fallbackDisplayName)
        {
            weaponInstanceId = LoopDataUtility.EnsureId(weaponInstanceId);
            definitionId = string.IsNullOrWhiteSpace(definitionId)
                ? fallbackDefinitionId
                : definitionId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? fallbackDisplayName
                : displayName.Trim();
            level = Math.Max(1, level);
            experience = Math.Max(0, experience);
            gridStateJson ??= string.Empty;
        }

        internal void RestoreFrom(WeaponInstanceRecord source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            WeaponInstanceRecord snapshot = source.Clone();
            weaponInstanceId = snapshot.weaponInstanceId;
            definitionId = snapshot.definitionId;
            displayName = snapshot.displayName;
            level = snapshot.level;
            experience = snapshot.experience;
            gridStateJson = snapshot.gridStateJson;
        }
    }

    [Serializable]
    public sealed class ChestStorageAssignment
    {
        [SerializeField] private string entryId;
        [SerializeField] private string chestId;

        public string EntryId => entryId;
        public string ChestId => chestId;

        internal static ChestStorageAssignment Create(
            string entryId,
            string chestId)
        {
            return new ChestStorageAssignment
            {
                entryId = entryId,
                chestId = chestId
            };
        }

        internal ChestStorageAssignment Clone()
        {
            return Create(entryId, chestId);
        }
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 3;
        public const int InventoryCapacity = 24;
        public const int ChestCapacity = 50;
        public const string DefaultChestId = "home-chest-1";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string profileId;
        [SerializeField] private string displayName = "Wanderer";
        [SerializeField] private string createdUtc;
        [SerializeField] private string lastSavedUtc;
        [SerializeField] private List<StorageEntry> storage = new List<StorageEntry>();
        [SerializeField] private List<string> inventoryEntryIds =
            new List<string>();
        [SerializeField] private List<ChestStorageAssignment>
            chestStorageAssignments =
                new List<ChestStorageAssignment>();
        [SerializeField] private WeaponInstanceRecord weaponOne;
        [SerializeField] private WeaponInstanceRecord weaponTwo;

        public int SchemaVersion => schemaVersion;
        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string CreatedUtc => createdUtc;
        public string LastSavedUtc => lastSavedUtc;
        public IReadOnlyList<StorageEntry> Storage => storage;
        public IReadOnlyList<string> InventoryEntryIds =>
            inventoryEntryIds;
        public IReadOnlyList<ChestStorageAssignment>
            ChestStorageAssignments =>
                chestStorageAssignments;
        public WeaponInstanceRecord WeaponOne => weaponOne;
        public WeaponInstanceRecord WeaponTwo => weaponTwo;

        public static PlayerProfile CreateNew(string profileId, string displayName = "Wanderer")
        {
            string timestamp = LoopDataUtility.UtcTimestamp();
            PlayerProfile profile = new PlayerProfile
            {
                profileId = string.IsNullOrWhiteSpace(profileId)
                    ? LoopDataUtility.CreateId()
                    : profileId.Trim(),
                displayName = string.IsNullOrWhiteSpace(displayName)
                    ? "Wanderer"
                    : displayName.Trim(),
                createdUtc = timestamp,
                lastSavedUtc = timestamp,
                weaponOne = WeaponInstanceRecord.Create("short-sword", "Short Sword"),
                weaponTwo = WeaponInstanceRecord.Create("hunting-bow", "Hunting Bow"),
            };
            profile.Normalize();
            return profile;
        }

        public WeaponInstanceRecord GetWeapon(int oneBasedSlot)
        {
            return oneBasedSlot switch
            {
                1 => weaponOne,
                2 => weaponTwo,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(oneBasedSlot),
                    "Weapon slots are one-based and limited to 1 or 2."),
            };
        }

        public void SetWeapon(int oneBasedSlot, WeaponInstanceRecord weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            switch (oneBasedSlot)
            {
                case 1:
                    weaponOne = weapon.Clone();
                    weaponOne.Normalize("short-sword", "Short Sword");
                    break;
                case 2:
                    weaponTwo = weapon.Clone();
                    weaponTwo.Normalize("hunting-bow", "Hunting Bow");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(oneBasedSlot),
                        "Weapon slots are one-based and limited to 1 or 2.");
            }
        }

        public void AddToStorage(StorageEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            StorageEntry copy = entry.Clone();
            copy.Normalize();
            storage.Add(copy);
            MoveToChest(copy.EntryId, DefaultChestId);
        }

        public bool RemoveStorageEntry(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return false;
            }

            int index = storage.FindIndex(entry =>
                string.Equals(entry.EntryId, entryId, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            storage.RemoveAt(index);
            inventoryEntryIds.RemoveAll(id =>
                string.Equals(id, entryId, StringComparison.Ordinal));
            RemoveChestAssignment(entryId);
            return true;
        }

        public StorageEntry FindStorageEntry(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return null;
            }

            return storage.Find(entry =>
                string.Equals(
                    entry.EntryId,
                    entryId,
                    StringComparison.Ordinal));
        }

        public bool IsInInventory(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) &&
                inventoryEntryIds.Exists(id =>
                    string.Equals(
                        id,
                        entryId,
                        StringComparison.Ordinal));
        }

        public bool TryMoveToInventory(string entryId)
        {
            if (FindStorageEntry(entryId) == null)
            {
                return false;
            }

            if (IsInInventory(entryId))
            {
                return true;
            }

            if (inventoryEntryIds.Count >= InventoryCapacity)
            {
                return false;
            }

            RemoveChestAssignment(entryId);
            inventoryEntryIds.Add(entryId);
            return true;
        }

        public bool MoveToStorage(string entryId)
        {
            return MoveToChest(entryId, DefaultChestId);
        }

        public bool MoveToChest(
            string entryId,
            string chestId)
        {
            if (string.IsNullOrWhiteSpace(entryId) ||
                string.IsNullOrWhiteSpace(chestId) ||
                FindStorageEntry(entryId) == null)
            {
                return false;
            }

            string normalizedChestId = chestId.Trim();
            ChestStorageAssignment existing =
                chestStorageAssignments.Find(assignment =>
                    assignment != null &&
                    string.Equals(
                        assignment.EntryId,
                        entryId,
                        StringComparison.Ordinal));
            if (existing != null &&
                string.Equals(
                    existing.ChestId,
                    normalizedChestId,
                    StringComparison.Ordinal))
            {
                inventoryEntryIds.RemoveAll(id =>
                    string.Equals(
                        id,
                        entryId,
                        StringComparison.Ordinal));
                return true;
            }

            if (GetChestEntryIds(normalizedChestId).Count >=
                ChestCapacity)
            {
                return false;
            }

            inventoryEntryIds.RemoveAll(id =>
                string.Equals(
                    id,
                    entryId,
                    StringComparison.Ordinal));
            RemoveChestAssignment(entryId);
            chestStorageAssignments.Add(
                ChestStorageAssignment.Create(
                    entryId,
                    normalizedChestId));
            return true;
        }

        public IReadOnlyList<string> GetChestEntryIds(
            string chestId)
        {
            var entryIds = new List<string>();
            if (string.IsNullOrWhiteSpace(chestId))
            {
                return entryIds;
            }

            for (int index = 0;
                 index < chestStorageAssignments.Count;
                 index++)
            {
                ChestStorageAssignment assignment =
                    chestStorageAssignments[index];
                if (assignment != null &&
                    string.Equals(
                        assignment.ChestId,
                        chestId,
                        StringComparison.Ordinal))
                {
                    entryIds.Add(assignment.EntryId);
                }
            }
            return entryIds;
        }

        public void Rename(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                displayName = value.Trim();
            }
        }

        public PlayerProfile Clone()
        {
            PlayerProfile clone = new PlayerProfile
            {
                schemaVersion = schemaVersion,
                profileId = profileId,
                displayName = displayName,
                createdUtc = createdUtc,
                lastSavedUtc = lastSavedUtc,
                storage = new List<StorageEntry>(storage.Count),
                inventoryEntryIds =
                    new List<string>(inventoryEntryIds),
                chestStorageAssignments =
                    new List<ChestStorageAssignment>(
                        chestStorageAssignments.Count),
                weaponOne = weaponOne?.Clone(),
                weaponTwo = weaponTwo?.Clone(),
            };

            foreach (StorageEntry entry in storage)
            {
                if (entry != null)
                {
                    clone.storage.Add(entry.Clone());
                }
            }

            foreach (ChestStorageAssignment assignment
                     in chestStorageAssignments)
            {
                if (assignment != null)
                {
                    clone.chestStorageAssignments.Add(
                        assignment.Clone());
                }
            }

            clone.Normalize();
            return clone;
        }

        public void Normalize()
        {
            schemaVersion = CurrentSchemaVersion;
            profileId = LoopDataUtility.EnsureId(profileId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Wanderer" : displayName.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc)
                ? LoopDataUtility.UtcTimestamp()
                : createdUtc;
            lastSavedUtc = string.IsNullOrWhiteSpace(lastSavedUtc) ? createdUtc : lastSavedUtc;
            storage ??= new List<StorageEntry>();
            storage.RemoveAll(entry => entry == null);
            foreach (StorageEntry entry in storage)
            {
                entry.Normalize();
            }

            inventoryEntryIds ??= new List<string>();
            var validEntryIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < storage.Count; index++)
            {
                validEntryIds.Add(storage[index].EntryId);
            }

            var normalizedInventoryIds = new List<string>(
                Mathf.Min(
                    InventoryCapacity,
                    inventoryEntryIds.Count));
            for (int index = 0;
                 index < inventoryEntryIds.Count &&
                 normalizedInventoryIds.Count < InventoryCapacity;
                 index++)
            {
                string entryId = inventoryEntryIds[index];
                if (!string.IsNullOrWhiteSpace(entryId) &&
                    validEntryIds.Contains(entryId) &&
                    !normalizedInventoryIds.Contains(entryId))
                {
                    normalizedInventoryIds.Add(entryId);
                }
            }

            inventoryEntryIds = normalizedInventoryIds;
            chestStorageAssignments ??=
                new List<ChestStorageAssignment>();
            var normalizedAssignments =
                new List<ChestStorageAssignment>();
            var assignedEntryIds =
                new HashSet<string>(StringComparer.Ordinal);
            var chestCounts =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);
            for (int index = 0;
                 index < chestStorageAssignments.Count;
                 index++)
            {
                ChestStorageAssignment assignment =
                    chestStorageAssignments[index];
                if (assignment == null ||
                    string.IsNullOrWhiteSpace(
                        assignment.EntryId) ||
                    string.IsNullOrWhiteSpace(
                        assignment.ChestId) ||
                    !validEntryIds.Contains(
                        assignment.EntryId) ||
                    inventoryEntryIds.Contains(
                        assignment.EntryId) ||
                    !assignedEntryIds.Add(
                        assignment.EntryId))
                {
                    continue;
                }

                string chestId = assignment.ChestId.Trim();
                chestCounts.TryGetValue(
                    chestId,
                    out int chestCount);
                if (chestCount >= ChestCapacity)
                {
                    assignedEntryIds.Remove(
                        assignment.EntryId);
                    continue;
                }

                normalizedAssignments.Add(
                    ChestStorageAssignment.Create(
                        assignment.EntryId,
                        chestId));
                chestCounts[chestId] = chestCount + 1;
            }

            for (int index = 0;
                 index < storage.Count;
                 index++)
            {
                string entryId = storage[index].EntryId;
                if (inventoryEntryIds.Contains(entryId) ||
                    assignedEntryIds.Contains(entryId))
                {
                    continue;
                }

                normalizedAssignments.Add(
                    ChestStorageAssignment.Create(
                        entryId,
                        DefaultChestId));
                assignedEntryIds.Add(entryId);
            }
            chestStorageAssignments = normalizedAssignments;
            weaponOne ??= WeaponInstanceRecord.Create("short-sword", "Short Sword");
            weaponTwo ??= WeaponInstanceRecord.Create("hunting-bow", "Hunting Bow");
            weaponOne.Normalize("short-sword", "Short Sword");
            weaponTwo.Normalize("hunting-bow", "Hunting Bow");
        }

        internal void MarkSaved()
        {
            lastSavedUtc = LoopDataUtility.UtcTimestamp();
        }

        internal void RestoreFrom(PlayerProfile source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            PlayerProfile snapshot = source.Clone();
            schemaVersion = snapshot.schemaVersion;
            profileId = snapshot.profileId;
            displayName = snapshot.displayName;
            createdUtc = snapshot.createdUtc;
            lastSavedUtc = snapshot.lastSavedUtc;

            storage ??= new List<StorageEntry>();
            storage.Clear();
            foreach (StorageEntry entry in snapshot.storage)
            {
                storage.Add(entry.Clone());
            }

            inventoryEntryIds ??= new List<string>();
            inventoryEntryIds.Clear();
            inventoryEntryIds.AddRange(snapshot.inventoryEntryIds);

            chestStorageAssignments ??=
                new List<ChestStorageAssignment>();
            chestStorageAssignments.Clear();
            foreach (ChestStorageAssignment assignment
                     in snapshot.chestStorageAssignments)
            {
                chestStorageAssignments.Add(
                    assignment.Clone());
            }

            if (weaponOne == null)
            {
                weaponOne = snapshot.weaponOne.Clone();
            }
            else
            {
                weaponOne.RestoreFrom(snapshot.weaponOne);
            }

            if (weaponTwo == null)
            {
                weaponTwo = snapshot.weaponTwo.Clone();
            }
            else
            {
                weaponTwo.RestoreFrom(snapshot.weaponTwo);
            }
        }

        private void RemoveChestAssignment(string entryId)
        {
            chestStorageAssignments.RemoveAll(assignment =>
                assignment != null &&
                string.Equals(
                    assignment.EntryId,
                    entryId,
                    StringComparison.Ordinal));
        }
    }

    internal static class LoopDataUtility
    {
        public static string CreateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string EnsureId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? CreateId() : value.Trim();
        }

        public static string UtcTimestamp()
        {
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
