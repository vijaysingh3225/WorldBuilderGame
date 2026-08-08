using System;
using UnityEngine;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop
{
    /// <summary>Persistent payload carried by a lootable enemy weapon item.</summary>
    [Serializable]
    public sealed class LootWeaponData
    {
        [SerializeField] private string weaponDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private int level;
        [SerializeField] private int visualSeed;
        [SerializeField, TextArea] private string gridStateJson;

        public string WeaponDefinitionId => weaponDefinitionId;
        public string DisplayName => displayName;
        public int Level => level;
        public int VisualSeed => visualSeed;
        public string GridStateJson => gridStateJson;

        public static LootWeaponData Create(
            string weaponDefinitionId,
            int seed)
        {
            bool sword = weaponDefinitionId == ItemDefinitionIds.LootShortSword;
            int level = Mathf.Clamp(1 + Mathf.Abs(seed % 5), 1, 5);
            string displayName = sword ? "Raider Short Sword" : "Raider Hunting Bow";
            WeaponGridState grid = new WeaponGridState(
                Guid.NewGuid().ToString("N"),
                displayName,
                seed);
            int growth = Mathf.Max(0, level * 2 - 1);
            for (int index = 0; index < growth; index++)
            {
                grid.GrowOne();
            }

            return new LootWeaponData
            {
                weaponDefinitionId = weaponDefinitionId,
                displayName = displayName,
                level = level,
                visualSeed = seed,
                gridStateJson = JsonUtility.ToJson(grid)
            };
        }

        public static bool TryParse(string json, out LootWeaponData data)
        {
            data = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<LootWeaponData>(json);
            return data != null &&
                !string.IsNullOrWhiteSpace(data.weaponDefinitionId);
        }
    }
}
