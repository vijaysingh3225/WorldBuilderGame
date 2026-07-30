using System;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    public enum GameLaunchMode
    {
        FreshGame = 0,
        Continue = 1,
        HomeSandbox = 2,
        RaidSandbox = 3,
        CombatLab = 4,
    }

    [Serializable]
    public sealed class GameLaunchContext
    {
        public const string DefaultProfileSlot = "profile-1";
        public const string DefaultRaidDefinition = "raid-prototype";

        [SerializeField] private GameLaunchMode mode = GameLaunchMode.CombatLab;
        [SerializeField] private string profileSlotId = DefaultProfileSlot;
        [SerializeField] private string homePresetId = "home-default";
        [SerializeField] private string raidDefinitionId = DefaultRaidDefinition;
        [SerializeField] private string raidPresetId = "raid-default";
        [SerializeField] private bool hasFixedRaidSeed;
        [SerializeField] private int raidSeed;
        [SerializeField] private bool persistenceEnabled;

        public GameLaunchMode Mode => mode;
        public string ProfileSlotId => profileSlotId;
        public string HomePresetId => homePresetId;
        public string RaidDefinitionId => raidDefinitionId;
        public string RaidPresetId => raidPresetId;
        public bool HasFixedRaidSeed => hasFixedRaidSeed;
        public int RaidSeed => raidSeed;
        public bool PersistenceEnabled => persistenceEnabled;
        public bool IsSandbox =>
            mode == GameLaunchMode.HomeSandbox ||
            mode == GameLaunchMode.RaidSandbox ||
            mode == GameLaunchMode.CombatLab;

        public static GameLaunchContext CreateFreshGame(string profileSlotId = DefaultProfileSlot)
        {
            return Create(
                GameLaunchMode.FreshGame,
                profileSlotId,
                persistenceEnabled: true);
        }

        public static GameLaunchContext CreateContinue(string profileSlotId = DefaultProfileSlot)
        {
            return Create(
                GameLaunchMode.Continue,
                profileSlotId,
                persistenceEnabled: true);
        }

        public static GameLaunchContext CreateHomeSandbox(string presetId = "home-default")
        {
            GameLaunchContext context = Create(
                GameLaunchMode.HomeSandbox,
                "home-sandbox",
                persistenceEnabled: false);
            context.homePresetId = NormalizeId(presetId, "home-default");
            return context;
        }

        public static GameLaunchContext CreateRaidSandbox(
            string presetId = "raid-default",
            int? fixedSeed = null,
            string raidDefinitionId = DefaultRaidDefinition)
        {
            GameLaunchContext context = Create(
                GameLaunchMode.RaidSandbox,
                "raid-sandbox",
                persistenceEnabled: false);
            context.raidDefinitionId = NormalizeId(raidDefinitionId, DefaultRaidDefinition);
            context.raidPresetId = NormalizeId(presetId, "raid-default");
            context.hasFixedRaidSeed = fixedSeed.HasValue;
            context.raidSeed = fixedSeed.GetValueOrDefault();
            return context;
        }

        public static GameLaunchContext CreateCombatLab()
        {
            return Create(
                GameLaunchMode.CombatLab,
                "combat-lab",
                persistenceEnabled: false);
        }

        public GameLaunchContext Clone()
        {
            return new GameLaunchContext
            {
                mode = mode,
                profileSlotId = profileSlotId,
                homePresetId = homePresetId,
                raidDefinitionId = raidDefinitionId,
                raidPresetId = raidPresetId,
                hasFixedRaidSeed = hasFixedRaidSeed,
                raidSeed = raidSeed,
                persistenceEnabled = persistenceEnabled,
            };
        }

        public void Normalize()
        {
            profileSlotId = NormalizeId(
                profileSlotId,
                IsSandbox ? mode.ToString().ToLowerInvariant() : DefaultProfileSlot);
            homePresetId = NormalizeId(homePresetId, "home-default");
            raidDefinitionId = NormalizeId(raidDefinitionId, DefaultRaidDefinition);
            raidPresetId = NormalizeId(raidPresetId, "raid-default");

            if (IsSandbox)
            {
                persistenceEnabled = false;
            }
        }

        private static GameLaunchContext Create(
            GameLaunchMode mode,
            string profileSlotId,
            bool persistenceEnabled)
        {
            GameLaunchContext context = new GameLaunchContext
            {
                mode = mode,
                profileSlotId = NormalizeId(profileSlotId, DefaultProfileSlot),
                persistenceEnabled = persistenceEnabled,
            };
            context.Normalize();
            return context;
        }

        private static string NormalizeId(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
