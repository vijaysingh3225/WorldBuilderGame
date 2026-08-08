using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class RaidLootContainer : MonoBehaviour
    {
        public const float GuardCoinChance = 0.40f;
        public const int GuardMinimumCoins = 1;
        public const int GuardMaximumCoins = 5;
        public const float ChestArtifactChance = 0.30f;

        public enum LootSourceKind
        {
            Corpse,
            Chest
        }

        [SerializeField] private LootSourceKind sourceKind;
        [SerializeField] private string displayName = "Loot";
        [SerializeField, Min(1)] private int columns = 4;
        [SerializeField, Min(1)] private int rows = 4;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f;
        [SerializeField] private List<StorageEntry> entries =
            new List<StorageEntry>();
        [SerializeField] private bool available;

        private Health ownerHealth;
        private Transform player;
        private HomeInventoryController inventory;
        private float nextResolveAt;

        public LootSourceKind SourceKind => sourceKind;
        public string DisplayName => displayName;
        public int Columns => columns;
        public int Rows => rows;
        public bool IsAvailable => available;
        public bool IsEmpty => entries.Count == 0;
        public IReadOnlyList<StorageEntry> Entries => entries;
        public bool PlayerInRange =>
            available &&
            player != null &&
            Vector3.SqrMagnitude(
                player.position - transform.position) <=
            Mathf.Min(
                interactionDistance,
                LootInteractionPresentation.DefaultDistance) *
            Mathf.Min(
                interactionDistance,
                LootInteractionPresentation.DefaultDistance);
        public bool CanInteract =>
            available &&
            LootInteractionPresentation.IsFocused(
                player,
                transform,
                interactionDistance,
                sourceKind == LootSourceKind.Corpse);

        public void ConfigureChest(string label, int seed)
        {
            DetachHealth();
            sourceKind = LootSourceKind.Chest;
            displayName = string.IsNullOrWhiteSpace(label)
                ? "Camp Chest"
                : label.Trim();
            columns = 4;
            rows = 4;
            available = true;
            GenerateContents(
                seed,
                includeArrows: true,
                1,
                20,
                includeChestMaterials: true);
        }

        public void ConfigureCorpse(EnemyBrain enemy, int seed)
        {
            DetachHealth();
            sourceKind = LootSourceKind.Corpse;
            displayName = enemy != null &&
                !string.IsNullOrWhiteSpace(enemy.name)
                    ? enemy.name
                    : "Fallen Raider";
            columns = 4;
            rows = 6;
            bool archer = enemy == null ||
                enemy.ConfiguredWeaponLoadout !=
                    EnemyBrain.WeaponLoadout.SwordOnly;
            GenerateContents(
                seed,
                archer,
                1,
                10,
                includeChestMaterials: false);
            string weaponDefinitionId = archer
                ? ItemDefinitionIds.LootHuntingBow
                : ItemDefinitionIds.LootShortSword;
            LootWeaponData weaponData = LootWeaponData.Create(
                weaponDefinitionId,
                seed ^ 0x5F3759DF);
            AddGeneratedEntry(StorageEntry.Create(
                weaponDefinitionId,
                customStateJson: JsonUtility.ToJson(weaponData)));
            ownerHealth = enemy != null
                ? enemy.GetComponent<Health>()
                : GetComponent<Health>();
            if (ownerHealth != null)
            {
                ownerHealth.Died += HandleOwnerDied;
                available = !ownerHealth.IsAlive;
            }
            else
            {
                available = true;
            }
        }

        public bool Contains(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) &&
                entries.Exists(entry =>
                    entry != null &&
                    string.Equals(
                        entry.EntryId,
                        entryId,
                        StringComparison.Ordinal));
        }

        public StorageEntry GetEntryAtSlot(int slotIndex)
        {
            return ItemGridPlacement.GetEntryAtSlot(
                entries,
                slotIndex,
                columns,
                rows);
        }

        public bool TryTake(
            string entryId,
            int quantity,
            out StorageEntry taken)
        {
            taken = null;
            StorageEntry entry = entries.Find(candidate =>
                candidate != null &&
                string.Equals(
                    candidate.EntryId,
                    entryId,
                    StringComparison.Ordinal));
            if (entry == null || quantity <= 0)
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
                entries.Remove(entry);
            }
            return true;
        }

        public int TryAdd(
            StorageEntry incoming,
            int targetSlot,
            bool autoStack)
        {
            if (incoming == null || incoming.Quantity <= 0)
            {
                return 0;
            }

            int remaining = incoming.Quantity;
            int moved = 0;
            int capacity = columns * rows;
            int maximumStack = ItemDefinitionCatalog.MaximumStack(
                incoming.DefinitionId);
            if (autoStack)
            {
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
                    int slot = FindAvailableSlot(incoming);
                    if (slot < 0)
                    {
                        break;
                    }
                    int amount = Mathf.Min(remaining, maximumStack);
                    StorageEntry added = moved == 0 &&
                        amount == incoming.Quantity
                            ? incoming.Clone()
                            : incoming.CreateSplitCopy(amount);
                    added.SetSlotIndex(slot);
                    entries.Add(added);
                    remaining -= amount;
                    moved += amount;
                }
                return moved;
            }

            if (targetSlot < 0 || targetSlot >= capacity)
            {
                return 0;
            }
            StorageEntry occupant = GetEntryAtSlot(targetSlot);
            if (occupant == null)
            {
                if (!ItemGridPlacement.CanPlace(
                        entries,
                        incoming,
                        targetSlot,
                        columns,
                        rows))
                {
                    return 0;
                }
                int amount = Mathf.Min(remaining, maximumStack);
                StorageEntry added = amount == incoming.Quantity
                    ? incoming.Clone()
                    : incoming.CreateSplitCopy(amount);
                added.SetSlotIndex(targetSlot);
                entries.Add(added);
                return amount;
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

        public bool RemoveTransferredEntry(string entryId)
        {
            int index = entries.FindIndex(entry =>
                entry != null &&
                string.Equals(
                    entry.EntryId,
                    entryId,
                    StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            StorageEntry removed = entries[index];
            entries.RemoveAt(index);
            GameplayEventLog.Publish(
                "raid-loot-transferred",
                gameObject,
                $"source={sourceKind};item={removed.DefinitionId};" +
                $"quantity={removed.Quantity}");
            return true;
        }

        private void Update()
        {
            if (!available)
            {
                return;
            }

            ResolveInteractionReferences();
            if (!CanInteract ||
                inventory == null ||
                inventory.IsOpen ||
                !PlayerControlBindings.WasPressedThisFrame(
                    Keyboard.current,
                    PlayerControl.Interact) ||
                !IsBestFocusedSource())
            {
                return;
            }

            inventory.OpenLoot(this);
        }

        private void OnGUI()
        {
            if (!CanInteract ||
                inventory == null ||
                inventory.IsOpen)
            {
                return;
            }

            if (IsBestFocusedSource())
            {
                LootInteractionPresentation.DrawPrompt(
                    sourceKind == LootSourceKind.Chest
                        ? "Open Chest"
                        : "Loot Body");
            }
        }

        private void OnDestroy()
        {
            DetachHealth();
        }

        private void HandleOwnerDied(DamageRequest request)
        {
            available = true;
            enabled = true;
            GameplayEventLog.Publish(
                "raid-corpse-loot-ready",
                gameObject,
                $"items={entries.Count}");
        }

        private void GenerateContents(
            int seed,
            bool includeArrows,
            int minimumArrows,
            int maximumArrows,
            bool includeChestMaterials)
        {
            entries.Clear();
            var random = new System.Random(seed);
            if (includeArrows)
            {
                StorageEntry arrows = StorageEntry.Create(
                    ItemDefinitionIds.Arrow,
                    random.Next(
                        Mathf.Max(1, minimumArrows),
                        Mathf.Max(minimumArrows, maximumArrows) + 1));
                arrows.SetSlotIndex(0);
                entries.Add(arrows);
            }
            if (random.NextDouble() < 0.5d)
            {
                StorageEntry healthPack = StorageEntry.Create(
                    ItemDefinitionIds.HealthPack);
                healthPack.SetSlotIndex(includeArrows ? 1 : 0);
                entries.Add(healthPack);
            }
            if (includeChestMaterials &&
                random.NextDouble() < ChestArtifactChance)
            {
                AddGeneratedEntry(
                    StorageEntry.Create(ItemDefinitionIds.OwlEyeSeal));
            }
            if (!includeChestMaterials)
            {
                if (random.NextDouble() < GuardCoinChance)
                {
                    AddGeneratedEntry(
                        StorageEntry.Create(
                            ItemDefinitionIds.CopperCoin,
                            random.Next(
                                GuardMinimumCoins,
                                GuardMaximumCoins + 1)));
                }
                return;
            }
            if (random.NextDouble() < 0.5d)
            {
                int ingotCount = random.Next(1, 4);
                for (int index = 0; index < ingotCount; index++)
                {
                    AddGeneratedEntry(
                        StorageEntry.Create(ItemDefinitionIds.IronIngot));
                }
            }
            if (random.NextDouble() < 0.5d)
            {
                AddGeneratedEntry(
                    StorageEntry.Create(
                        ItemDefinitionIds.Coal,
                        random.Next(1, 11)));
            }
            if (random.NextDouble() < 0.5d)
            {
                AddGeneratedEntry(
                    StorageEntry.Create(
                        ItemDefinitionIds.CopperCoin,
                        random.Next(1, 11)));
            }
        }

        private void AddGeneratedEntry(StorageEntry entry)
        {
            int slot = FindAvailableSlot(entry);
            if (slot < 0)
            {
                return;
            }
            entry.SetSlotIndex(slot);
            entries.Add(entry);
        }

        private int FindAvailableSlot(StorageEntry candidate)
        {
            return ItemGridPlacement.FindFirstAvailableSlot(
                entries,
                candidate,
                columns,
                rows);
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

        private void ResolveInteractionReferences()
        {
            if (player != null && inventory != null)
            {
                return;
            }
            if (Time.unscaledTime < nextResolveAt)
            {
                return;
            }

            nextResolveAt = Time.unscaledTime + 0.5f;
            if (player == null)
            {
                GameObject playerObject =
                    GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null
                    ? playerObject.transform
                    : null;
            }
            inventory ??= FindFirstObjectByType<HomeInventoryController>();
        }

        private bool IsBestFocusedSource()
        {
            RaidLootContainer[] sources =
                FindObjectsByType<RaidLootContainer>(
                    FindObjectsSortMode.None);
            float bestPriority = float.PositiveInfinity;
            RaidLootContainer nearest = null;
            for (int index = 0; index < sources.Length; index++)
            {
                RaidLootContainer source = sources[index];
                source?.ResolveInteractionReferences();
                if (source == null || !source.IsAvailable ||
                    !LootInteractionPresentation.TryGetFocusScore(
                        source.player,
                        source.transform,
                        source.interactionDistance,
                        out float focusScore,
                        source.sourceKind == LootSourceKind.Corpse))
                {
                    continue;
                }

                if (focusScore < bestPriority)
                {
                    bestPriority = focusScore;
                    nearest = source;
                }
            }
            return ReferenceEquals(nearest, this);
        }

        private void DetachHealth()
        {
            if (ownerHealth != null)
            {
                ownerHealth.Died -= HandleOwnerDied;
                ownerHealth = null;
            }
        }
    }
}
