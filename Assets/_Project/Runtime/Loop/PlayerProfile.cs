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
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string profileId;
        [SerializeField] private string displayName = "Wanderer";
        [SerializeField] private string createdUtc;
        [SerializeField] private string lastSavedUtc;
        [SerializeField] private List<StorageEntry> storage = new List<StorageEntry>();
        [SerializeField] private WeaponInstanceRecord weaponOne;
        [SerializeField] private WeaponInstanceRecord weaponTwo;

        public int SchemaVersion => schemaVersion;
        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string CreatedUtc => createdUtc;
        public string LastSavedUtc => lastSavedUtc;
        public IReadOnlyList<StorageEntry> Storage => storage;
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
            return true;
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
