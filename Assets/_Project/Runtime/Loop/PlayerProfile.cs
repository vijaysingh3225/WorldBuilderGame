using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    [Serializable]
    public sealed class StorageEntry
    {
        [SerializeField] private string entryId;
        [SerializeField] private string definitionId;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private int slotIndex = -1;
        [SerializeField, Range(0, 3)] private int rotationQuarterTurns;
        [SerializeField, TextArea] private string customStateJson;

        public string EntryId => entryId;
        public string DefinitionId => definitionId;
        public int Quantity => quantity;
        public int SlotIndex => slotIndex;
        public int RotationQuarterTurns => rotationQuarterTurns;
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
                slotIndex = -1,
                rotationQuarterTurns = 0,
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
                slotIndex = slotIndex,
                rotationQuarterTurns = rotationQuarterTurns,
                customStateJson = customStateJson,
            };
        }

        internal void SetQuantity(int value)
        {
            quantity = Math.Max(0, value);
        }

        internal void SetSlotIndex(int value)
        {
            slotIndex = value;
        }

        internal void SetRotationQuarterTurns(int value)
        {
            rotationQuarterTurns = ((value % 4) + 4) % 4;
        }

        public void RotateClockwise()
        {
            SetRotationQuarterTurns(rotationQuarterTurns + 1);
        }

        internal int RemoveQuantity(int amount)
        {
            int removed = Math.Min(quantity, Math.Max(0, amount));
            quantity -= removed;
            return removed;
        }

        internal StorageEntry CreateSplitCopy(int splitQuantity)
        {
            StorageEntry copy = Create(
                definitionId,
                Math.Max(1, splitQuantity),
                customStateJson);
            copy.SetSlotIndex(slotIndex);
            copy.SetRotationQuarterTurns(rotationQuarterTurns);
            return copy;
        }

        internal void Normalize()
        {
            entryId = LoopDataUtility.EnsureId(entryId);
            definitionId = string.IsNullOrWhiteSpace(definitionId)
                ? "unknown-item"
                : definitionId.Trim();
            quantity = Math.Max(1, quantity);
            slotIndex = Math.Max(-1, slotIndex);
            rotationQuarterTurns =
                ((rotationQuarterTurns % 4) + 4) % 4;
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
        public const int CurrentSchemaVersion = 6;
        public const int InventoryColumns = 4;
        public const int InventoryRows = 6;
        public const int InventoryCapacity = 24;
        public const int ChestColumns = 5;
        public const int ChestRows = 10;
        public const int ChestCapacity = 50;
        public const int SecureColumns = 2;
        public const int SecureRows = 2;
        public const int SecureCapacity = 4;
        public const string DefaultChestId = "home-chest-1";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string profileId;
        [SerializeField] private string displayName = "Wanderer";
        [SerializeField] private string createdUtc;
        [SerializeField] private string lastSavedUtc;
        [SerializeField] private List<StorageEntry> storage = new List<StorageEntry>();
        [SerializeField] private List<string> inventoryEntryIds =
            new List<string>();
        [SerializeField] private List<string> secureEntryIds =
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
        public IReadOnlyList<string> SecureEntryIds => secureEntryIds;
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
            secureEntryIds.RemoveAll(id =>
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

        public bool IsInSecureContainer(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) &&
                secureEntryIds.Exists(id =>
                    string.Equals(
                        id,
                        entryId,
                        StringComparison.Ordinal));
        }

        public bool TryMoveToSecureContainer(
            string entryId,
            int preferredSlot = -1)
        {
            StorageEntry entry = FindStorageEntry(entryId);
            if (entry == null)
            {
                return false;
            }
            if (IsInSecureContainer(entryId))
            {
                return preferredSlot < 0 ||
                    TryMoveSecureEntryToSlot(entryId, preferredSlot);
            }
            if (secureEntryIds.Count >= SecureCapacity)
            {
                return false;
            }
            int slot = ResolveAvailableSecureSlot(entry, preferredSlot);
            if (slot < 0)
            {
                return false;
            }
            inventoryEntryIds.RemoveAll(id =>
                string.Equals(id, entryId, StringComparison.Ordinal));
            RemoveChestAssignment(entryId);
            entry.SetSlotIndex(slot);
            secureEntryIds.Add(entryId);
            return true;
        }

        public StorageEntry GetSecureEntryAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SecureCapacity)
            {
                return null;
            }
            return ItemGridPlacement.GetEntryAtSlot(
                GetEntries(secureEntryIds),
                slotIndex,
                SecureColumns,
                SecureRows);
        }

        public bool TryMoveSecureEntryToSlot(
            string entryId,
            int targetSlot)
        {
            StorageEntry entry = FindStorageEntry(entryId);
            if (entry == null || !IsInSecureContainer(entryId) ||
                targetSlot < 0 || targetSlot >= SecureCapacity ||
                !ItemGridPlacement.CanPlace(
                    GetEntries(secureEntryIds),
                    entry,
                    targetSlot,
                    SecureColumns,
                    SecureRows,
                    entry.EntryId))
            {
                return false;
            }
            entry.SetSlotIndex(targetSlot);
            return true;
        }

        public bool TryMoveToInventory(string entryId)
        {
            return TryMoveToInventory(entryId, -1);
        }

        public bool TryMoveToInventory(
            string entryId,
            int preferredSlot)
        {
            StorageEntry entry = FindStorageEntry(entryId);
            if (entry == null)
            {
                return false;
            }

            if (IsInInventory(entryId))
            {
                return preferredSlot < 0 ||
                    TryMoveInventoryEntryToSlot(
                        entryId,
                        preferredSlot);
            }

            if (inventoryEntryIds.Count >= InventoryCapacity)
            {
                return false;
            }

            int slot = ResolveAvailableInventorySlot(
                entry,
                preferredSlot);
            if (slot < 0)
            {
                return false;
            }
            RemoveChestAssignment(entryId);
            secureEntryIds.RemoveAll(id =>
                string.Equals(id, entryId, StringComparison.Ordinal));
            entry.SetSlotIndex(slot);
            inventoryEntryIds.Add(entryId);
            return true;
        }

        public StorageEntry GetInventoryEntryAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= InventoryCapacity)
            {
                return null;
            }
            return ItemGridPlacement.GetEntryAtSlot(
                GetEntries(inventoryEntryIds),
                slotIndex,
                InventoryColumns,
                InventoryRows);
        }

        public bool TryMoveInventoryEntryToSlot(
            string entryId,
            int targetSlot)
        {
            StorageEntry entry = FindStorageEntry(entryId);
            if (entry == null ||
                !IsInInventory(entryId) ||
                targetSlot < 0 ||
                targetSlot >= InventoryCapacity)
            {
                return false;
            }
            if (!ItemGridPlacement.CanPlace(
                    GetEntries(inventoryEntryIds),
                    entry,
                    targetSlot,
                    InventoryColumns,
                    InventoryRows,
                    entry.EntryId))
            {
                return false;
            }
            entry.SetSlotIndex(targetSlot);
            return true;
        }

        public bool TrySetInventoryStack(
            string definitionId,
            int quantity)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return false;
            }

            string normalizedDefinitionId = definitionId.Trim();
            StorageEntry stack = null;
            var duplicateIds = new List<string>();
            for (int index = 0; index < inventoryEntryIds.Count; index++)
            {
                StorageEntry entry = FindStorageEntry(
                    inventoryEntryIds[index]);
                if (entry == null ||
                    !string.Equals(
                        entry.DefinitionId,
                        normalizedDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (stack == null)
                {
                    stack = entry;
                }
                else
                {
                    duplicateIds.Add(entry.EntryId);
                }
            }

            for (int index = 0; index < duplicateIds.Count; index++)
            {
                RemoveStorageEntry(duplicateIds[index]);
            }

            int safeQuantity = Math.Min(
                Math.Max(0, quantity),
                ItemDefinitionCatalog.MaximumStack(
                    normalizedDefinitionId));
            if (safeQuantity == 0)
            {
                return stack == null || RemoveStorageEntry(stack.EntryId);
            }

            if (stack != null)
            {
                stack.SetQuantity(safeQuantity);
                return true;
            }

            if (inventoryEntryIds.Count >= InventoryCapacity)
            {
                return false;
            }

            StorageEntry created = StorageEntry.Create(
                normalizedDefinitionId,
                safeQuantity);
            int createdSlot = ResolveAvailableInventorySlot(
                created,
                -1);
            if (createdSlot < 0)
            {
                return false;
            }
            storage.Add(created);
            created.SetSlotIndex(createdSlot);
            inventoryEntryIds.Add(created.EntryId);
            return true;
        }

        public bool TryMergeInventoryStack(StorageEntry incoming)
        {
            if (incoming == null ||
                !ItemDefinitionCatalog.IsStackable(
                    incoming.DefinitionId))
            {
                return false;
            }

            for (int index = 0;
                 index < inventoryEntryIds.Count;
                 index++)
            {
                StorageEntry entry = FindStorageEntry(
                    inventoryEntryIds[index]);
                if (entry != null &&
                    string.Equals(
                        entry.DefinitionId,
                        incoming.DefinitionId,
                        StringComparison.Ordinal) &&
                    entry.Quantity + incoming.Quantity <=
                        ItemDefinitionCatalog.MaximumStack(
                            incoming.DefinitionId))
                {
                    entry.SetQuantity(
                        entry.Quantity + incoming.Quantity);
                    return true;
                }
            }
            return false;
        }

        public bool MoveToStorage(string entryId)
        {
            return MoveToChest(entryId, DefaultChestId);
        }

        public bool MoveToChest(
            string entryId,
            string chestId)
        {
            return MoveToChest(entryId, chestId, -1);
        }

        public bool MoveToChest(
            string entryId,
            string chestId,
            int preferredSlot)
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
                return preferredSlot < 0 ||
                    TryMoveChestEntryToSlot(
                        entryId,
                        normalizedChestId,
                        preferredSlot);
            }

            if (GetChestEntryIds(normalizedChestId).Count >=
                ChestCapacity)
            {
                return false;
            }

            StorageEntry entry = FindStorageEntry(entryId);
            int destinationSlot = ResolveAvailableChestSlot(
                normalizedChestId,
                entry,
                preferredSlot);
            if (destinationSlot < 0)
            {
                return false;
            }
            inventoryEntryIds.RemoveAll(id =>
                string.Equals(
                    id,
                    entryId,
                    StringComparison.Ordinal));
            secureEntryIds.RemoveAll(id =>
                string.Equals(
                    id,
                    entryId,
                    StringComparison.Ordinal));
            RemoveChestAssignment(entryId);
            entry.SetSlotIndex(destinationSlot);
            chestStorageAssignments.Add(
                ChestStorageAssignment.Create(
                    entryId,
                    normalizedChestId));
            return true;
        }

        public bool TryMoveChestEntryToSlot(
            string entryId,
            string chestId,
            int targetSlot)
        {
            StorageEntry entry = FindStorageEntry(entryId);
            if (entry == null || string.IsNullOrWhiteSpace(chestId) ||
                targetSlot < 0 || targetSlot >= ChestCapacity)
            {
                return false;
            }
            string normalizedChestId = chestId.Trim();
            IReadOnlyList<string> chestIds = GetChestEntryIds(
                normalizedChestId);
            bool assigned = false;
            for (int index = 0; index < chestIds.Count; index++)
            {
                if (string.Equals(
                        chestIds[index],
                        entryId,
                        StringComparison.Ordinal))
                {
                    assigned = true;
                    break;
                }
            }
            if (!assigned ||
                !ItemGridPlacement.CanPlace(
                    GetEntries(chestIds),
                    entry,
                    targetSlot,
                    ChestColumns,
                    ChestRows,
                    entry.EntryId))
            {
                return false;
            }
            entry.SetSlotIndex(targetSlot);
            return true;
        }

        private int ResolveAvailableInventorySlot(
            StorageEntry entry,
            int preferredSlot)
        {
            if (preferredSlot >= 0 &&
                ItemGridPlacement.CanPlace(
                    GetEntries(inventoryEntryIds),
                    entry,
                    preferredSlot,
                    InventoryColumns,
                    InventoryRows))
            {
                return preferredSlot;
            }
            return ItemGridPlacement.FindFirstAvailableSlot(
                GetEntries(inventoryEntryIds),
                entry,
                InventoryColumns,
                InventoryRows);
        }

        private int ResolveAvailableSecureSlot(
            StorageEntry entry,
            int preferredSlot)
        {
            if (preferredSlot >= 0 &&
                ItemGridPlacement.CanPlace(
                    GetEntries(secureEntryIds),
                    entry,
                    preferredSlot,
                    SecureColumns,
                    SecureRows))
            {
                return preferredSlot;
            }
            return ItemGridPlacement.FindFirstAvailableSlot(
                GetEntries(secureEntryIds),
                entry,
                SecureColumns,
                SecureRows);
        }

        private int ResolveAvailableChestSlot(
            string chestId,
            StorageEntry entry,
            int preferredSlot)
        {
            List<StorageEntry> entries = GetEntries(
                GetChestEntryIds(chestId));
            if (preferredSlot >= 0 &&
                ItemGridPlacement.CanPlace(
                    entries,
                    entry,
                    preferredSlot,
                    ChestColumns,
                    ChestRows))
            {
                return preferredSlot;
            }
            return ItemGridPlacement.FindFirstAvailableSlot(
                entries,
                entry,
                ChestColumns,
                ChestRows);
        }

        private List<StorageEntry> GetEntries(
            IReadOnlyList<string> entryIds)
        {
            var entries = new List<StorageEntry>(entryIds.Count);
            for (int index = 0; index < entryIds.Count; index++)
            {
                StorageEntry entry = FindStorageEntry(entryIds[index]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            return entries;
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
                secureEntryIds = new List<string>(secureEntryIds),
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
            secureEntryIds ??= new List<string>();
            var validEntryIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < storage.Count; index++)
            {
                validEntryIds.Add(storage[index].EntryId);
            }

            var normalizedSecureIds = new List<string>(
                Mathf.Min(SecureCapacity, secureEntryIds.Count));
            for (int index = 0;
                 index < secureEntryIds.Count &&
                 normalizedSecureIds.Count < SecureCapacity;
                 index++)
            {
                string entryId = secureEntryIds[index];
                if (!string.IsNullOrWhiteSpace(entryId) &&
                    validEntryIds.Contains(entryId) &&
                    !normalizedSecureIds.Contains(entryId))
                {
                    normalizedSecureIds.Add(entryId);
                }
            }
            secureEntryIds = normalizedSecureIds;
            NormalizeSlots(
                secureEntryIds,
                SecureColumns,
                SecureRows);

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
                    !secureEntryIds.Contains(entryId) &&
                    !normalizedInventoryIds.Contains(entryId))
                {
                    normalizedInventoryIds.Add(entryId);
                }
            }

            inventoryEntryIds = normalizedInventoryIds;
            NormalizeSlots(
                inventoryEntryIds,
                InventoryColumns,
                InventoryRows);
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
                    secureEntryIds.Contains(
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
                    secureEntryIds.Contains(entryId) ||
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
            foreach (string chestId in chestStorageAssignments
                         .ConvertAll(assignment => assignment.ChestId)
                         .Distinct(StringComparer.Ordinal))
            {
                NormalizeSlots(
                    GetChestEntryIds(chestId),
                    ChestColumns,
                    ChestRows);
            }
            weaponOne ??= WeaponInstanceRecord.Create("short-sword", "Short Sword");
            weaponTwo ??= WeaponInstanceRecord.Create("hunting-bow", "Hunting Bow");
            weaponOne.Normalize("short-sword", "Short Sword");
            weaponTwo.Normalize("hunting-bow", "Hunting Bow");
        }

        private void NormalizeSlots(
            IReadOnlyList<string> entryIds,
            int columns,
            int rows)
        {
            var placed = new List<StorageEntry>();
            var needsSlot = new List<StorageEntry>();
            for (int index = 0; index < entryIds.Count; index++)
            {
                StorageEntry entry = FindStorageEntry(entryIds[index]);
                if (entry == null)
                {
                    continue;
                }
                if (ItemGridPlacement.CanPlace(
                        placed,
                        entry,
                        entry.SlotIndex,
                        columns,
                        rows))
                {
                    placed.Add(entry);
                    continue;
                }
                needsSlot.Add(entry);
            }

            for (int index = 0; index < needsSlot.Count; index++)
            {
                StorageEntry entry = needsSlot[index];
                int slot = ItemGridPlacement.FindFirstAvailableSlot(
                    placed,
                    entry,
                    columns,
                    rows);
                if (slot < 0)
                {
                    entry.SetSlotIndex(-1);
                    continue;
                }
                entry.SetSlotIndex(slot);
                placed.Add(entry);
            }
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

            secureEntryIds ??= new List<string>();
            secureEntryIds.Clear();
            secureEntryIds.AddRange(snapshot.secureEntryIds);

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
