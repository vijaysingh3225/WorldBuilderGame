using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    /// <summary>
    /// Scene-facing API around the serializable grid model. Combat, profiles, and UI
    /// can subscribe without depending on one another.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponGridRuntime : MonoBehaviour
    {
        [SerializeField] private WeaponGridLoadoutState loadout =
            new WeaponGridLoadoutState();
        [SerializeField] private List<ArtifactDefinitionData> definitions =
            new List<ArtifactDefinitionData>();
        [SerializeField] private bool initializeSandboxDefaultsIfEmpty = true;

        private readonly Dictionary<string, ArtifactDefinitionData> catalog =
            new Dictionary<string, ArtifactDefinitionData>(StringComparer.Ordinal);

        public event Action<int, WeaponGridState> GridChanged;
        public event Action<int> ActiveWeaponChanged;
        public event Action<WeaponGridModifierSummary> ModifiersChanged;

        public WeaponGridLoadoutState Loadout => loadout;
        public IReadOnlyList<ArtifactDefinitionData> Definitions => definitions;
        public int ActiveWeaponIndex => loadout.ActiveWeaponIndex;
        public WeaponGridState ActiveGrid => loadout.Active;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            loadout ??= new WeaponGridLoadoutState();
            definitions ??= new List<ArtifactDefinitionData>();
            if (initializeSandboxDefaultsIfEmpty && definitions.Count == 0)
            {
                definitions.AddRange(CreateSandboxDefinitions());
            }

            loadout.EnsureInitialized();
            RebuildCatalog();
        }

        public void Configure(
            WeaponGridLoadoutState state,
            IEnumerable<ArtifactDefinitionData> artifactDefinitions)
        {
            loadout = state ?? new WeaponGridLoadoutState();
            definitions = artifactDefinitions != null
                ? new List<ArtifactDefinitionData>(artifactDefinitions)
                : new List<ArtifactDefinitionData>();
            EnsureInitialized();
            NotifyAllChanged();
        }

        public bool SelectWeapon(int weaponIndex)
        {
            EnsureInitialized();
            if (!loadout.SelectWeapon(weaponIndex))
            {
                return false;
            }

            ActiveWeaponChanged?.Invoke(loadout.ActiveWeaponIndex);
            ModifiersChanged?.Invoke(GetModifierSummary());
            return true;
        }

        public GridCoordinate GrowActive()
        {
            return GrowWeapon(loadout.ActiveWeaponIndex);
        }

        public GridCoordinate GrowWeapon(int weaponIndex)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            WeaponGridState state = loadout.GetWeapon(normalizedIndex);
            GridCoordinate cell = state.GrowOne();
            NotifyGridChanged(normalizedIndex);
            return cell;
        }

        public void GrowWeapon(int weaponIndex, int count)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            WeaponGridState state = loadout.GetWeapon(normalizedIndex);
            for (int index = 0; index < Mathf.Max(0, count); index++)
            {
                state.GrowOne();
            }

            NotifyGridChanged(normalizedIndex);
        }

        public void ResetActive(int seed)
        {
            ResetWeapon(loadout.ActiveWeaponIndex, seed);
        }

        public void ResetWeapon(int weaponIndex, int seed)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            loadout.GetWeapon(normalizedIndex).Reset(seed);
            NotifyGridChanged(normalizedIndex);
        }

        public void SetWeaponIdentity(
            int weaponIndex,
            string owningWeaponInstanceId,
            string owningDisplayName = null)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            loadout.GetWeapon(normalizedIndex).SetWeaponIdentity(
                owningWeaponInstanceId,
                owningDisplayName);
            NotifyGridChanged(normalizedIndex);
        }

        public ArtifactInstance CreateArtifact(string definitionId)
        {
            EnsureInitialized();
            return catalog.ContainsKey(definitionId)
                ? ArtifactInstance.Create(definitionId)
                : null;
        }

        public bool TryPlaceActive(
            string definitionId,
            GridCoordinate anchor,
            int rotation,
            out string reason)
        {
            ArtifactInstance artifact = CreateArtifact(definitionId);
            if (artifact == null)
            {
                reason = $"Unknown artifact definition '{definitionId}'.";
                return false;
            }

            return TryPlace(
                loadout.ActiveWeaponIndex,
                artifact,
                anchor,
                rotation,
                out reason);
        }

        public bool TryPlace(
            int weaponIndex,
            ArtifactInstance artifact,
            GridCoordinate anchor,
            int rotation,
            out string reason)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            if (artifact == null ||
                !catalog.TryGetValue(
                    artifact.DefinitionId ?? string.Empty,
                    out ArtifactDefinitionData definition))
            {
                reason = "Artifact definition is not present in the runtime catalog.";
                return false;
            }

            bool placed = loadout.GetWeapon(normalizedIndex).TryPlace(
                artifact,
                definition,
                anchor,
                rotation,
                catalog,
                out reason);
            if (placed)
            {
                NotifyGridChanged(normalizedIndex);
            }

            return placed;
        }

        public bool TryRemoveActiveAt(
            GridCoordinate coordinate,
            out ArtifactInstance removed)
        {
            return TryRemoveAt(
                loadout.ActiveWeaponIndex,
                coordinate,
                out removed);
        }

        public bool TryRemoveAt(
            int weaponIndex,
            GridCoordinate coordinate,
            out ArtifactInstance removed)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            bool didRemove = loadout.GetWeapon(normalizedIndex).TryRemoveAt(
                coordinate,
                catalog,
                out removed);
            if (didRemove)
            {
                NotifyGridChanged(normalizedIndex);
            }

            return didRemove;
        }

        public bool TryRemoveInstance(
            int weaponIndex,
            string instanceId,
            out ArtifactInstance removed)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            bool didRemove = loadout.GetWeapon(normalizedIndex)
                .TryRemoveInstance(instanceId, out removed);
            if (didRemove)
            {
                NotifyGridChanged(normalizedIndex);
            }

            return didRemove;
        }

        public bool TryRotateActiveAt(
            GridCoordinate coordinate,
            int quarterTurnDelta,
            out string reason)
        {
            return TryRotateAt(
                loadout.ActiveWeaponIndex,
                coordinate,
                quarterTurnDelta,
                out reason);
        }

        public bool TryRotateAt(
            int weaponIndex,
            GridCoordinate coordinate,
            int quarterTurnDelta,
            out string reason)
        {
            EnsureInitialized();
            int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
            bool didRotate = loadout.GetWeapon(normalizedIndex).TryRotateAt(
                coordinate,
                quarterTurnDelta,
                catalog,
                out reason);
            if (didRotate)
            {
                NotifyGridChanged(normalizedIndex);
            }

            return didRotate;
        }

        public ArtifactPlacement FindActivePlacementAt(GridCoordinate coordinate)
        {
            EnsureInitialized();
            return loadout.Active.FindPlacementAt(coordinate, catalog);
        }

        public bool TryGetDefinition(
            string definitionId,
            out ArtifactDefinitionData definition)
        {
            EnsureInitialized();
            return catalog.TryGetValue(definitionId ?? string.Empty, out definition);
        }

        public WeaponGridModifiers ResolveWeapon(int weaponIndex)
        {
            EnsureInitialized();
            return loadout.GetWeapon(NormalizeWeaponIndex(weaponIndex))
                .ResolveModifiers(catalog);
        }

        /// <summary>
        /// Damage comes from the active weapon grid. Character utility bonuses are
        /// combined from both equipped weapon grids.
        /// </summary>
        public WeaponGridModifiers ResolveEffective()
        {
            WeaponGridModifiers primary = ResolveWeapon(0);
            WeaponGridModifiers secondary = ResolveWeapon(1);
            WeaponGridModifiers active =
                loadout.ActiveWeaponIndex == 0 ? primary : secondary;
            return WeaponGridModifiers.Create(
                active.Damage,
                primary.MaxHealth + secondary.MaxHealth,
                primary.MoveSpeed + secondary.MoveSpeed);
        }

        public WeaponGridModifierSummary GetModifierSummary()
        {
            WeaponGridModifiers primary = ResolveWeapon(0);
            WeaponGridModifiers secondary = ResolveWeapon(1);
            WeaponGridModifiers active =
                loadout.ActiveWeaponIndex == 0 ? primary : secondary;
            WeaponGridModifiers effective = WeaponGridModifiers.Create(
                active.Damage,
                primary.MaxHealth + secondary.MaxHealth,
                primary.MoveSpeed + secondary.MoveSpeed);
            return new WeaponGridModifierSummary(
                loadout.ActiveWeaponIndex,
                primary,
                secondary,
                effective);
        }

        public string ExportLoadoutJson(bool prettyPrint = false)
        {
            EnsureInitialized();
            return JsonUtility.ToJson(loadout, prettyPrint);
        }

        public string ExportWeaponJson(
            int weaponIndex,
            bool prettyPrint = false)
        {
            EnsureInitialized();
            return JsonUtility.ToJson(
                loadout.GetWeapon(NormalizeWeaponIndex(weaponIndex)),
                prettyPrint);
        }

        public bool ImportLoadoutJson(string json, out string reason)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "Grid JSON is empty.";
                return false;
            }

            try
            {
                WeaponGridLoadoutState imported =
                    JsonUtility.FromJson<WeaponGridLoadoutState>(json);
                if (imported == null)
                {
                    reason = "Grid JSON did not contain a loadout.";
                    return false;
                }

                imported.EnsureInitialized();
                loadout = imported;
                NotifyAllChanged();
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        public bool ImportWeaponJson(
            int weaponIndex,
            string json,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "Weapon grid JSON is empty.";
                return false;
            }

            try
            {
                WeaponGridState imported =
                    JsonUtility.FromJson<WeaponGridState>(json);
                if (imported == null)
                {
                    reason = "Weapon grid JSON did not contain grid state.";
                    return false;
                }

                EnsureInitialized();
                int normalizedIndex = NormalizeWeaponIndex(weaponIndex);
                imported.EnsureInitialized(
                    normalizedIndex == 0 ? "Weapon 1" : "Weapon 2",
                    normalizedIndex == 0 ? 1337 : 7331);
                if (!imported.ValidatePlacements(catalog, out reason))
                {
                    return false;
                }

                loadout.ReplaceWeapon(normalizedIndex, imported);
                NotifyGridChanged(normalizedIndex);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        public void InitializeSandboxDefaults(
            int primarySeed = 1337,
            int secondarySeed = 7331,
            int startingCells = 13)
        {
            definitions = CreateSandboxDefinitions();
            loadout = new WeaponGridLoadoutState(
                new WeaponGridState(
                    Guid.NewGuid().ToString("N"),
                    "1  SHORT SWORD",
                    primarySeed),
                new WeaponGridState(
                    Guid.NewGuid().ToString("N"),
                    "2  BOW",
                    secondarySeed));
            EnsureInitialized();
            int growthCount = Mathf.Max(0, startingCells - 1);
            for (int index = 0; index < growthCount; index++)
            {
                loadout.Primary.GrowOne();
                loadout.Secondary.GrowOne();
            }

            NotifyAllChanged();
        }

        public static List<ArtifactDefinitionData> CreateSandboxDefinitions()
        {
            return new List<ArtifactDefinitionData>
            {
                new ArtifactDefinitionData(
                    "keen-shard",
                    "Keen Shard",
                    new Color(0.93f, 0.38f, 0.19f),
                    new[] { GridCoordinate.Root },
                    new[]
                    {
                        new ArtifactStatModifier(ArtifactStat.Damage, 1f)
                    }),
                new ArtifactDefinitionData(
                    "iron-bond",
                    "Iron Bond",
                    new Color(0.42f, 0.62f, 0.76f),
                    new[]
                    {
                        GridCoordinate.Root,
                        new GridCoordinate(1, 0)
                    },
                    new[]
                    {
                        new ArtifactStatModifier(ArtifactStat.MaxHealth, 10f)
                    }),
                new ArtifactDefinitionData(
                    "wind-step",
                    "Wind Step",
                    new Color(0.31f, 0.82f, 0.62f),
                    new[]
                    {
                        GridCoordinate.Root,
                        new GridCoordinate(1, 0),
                        new GridCoordinate(0, 1)
                    },
                    new[]
                    {
                        new ArtifactStatModifier(ArtifactStat.MoveSpeed, 0.25f)
                    }),
                new ArtifactDefinitionData(
                    "razor-line",
                    "Razor Line",
                    new Color(0.82f, 0.35f, 0.66f),
                    new[]
                    {
                        new GridCoordinate(-1, 0),
                        GridCoordinate.Root,
                        new GridCoordinate(1, 0)
                    },
                    new[]
                    {
                        new ArtifactStatModifier(ArtifactStat.Damage, 2f)
                    }),
                new ArtifactDefinitionData(
                    "wayfarer-knot",
                    "Wayfarer Knot",
                    new Color(0.88f, 0.72f, 0.24f),
                    new[]
                    {
                        GridCoordinate.Root,
                        new GridCoordinate(1, 0),
                        new GridCoordinate(1, 1),
                        new GridCoordinate(2, 1)
                    },
                    new[]
                    {
                        new ArtifactStatModifier(ArtifactStat.MaxHealth, 5f),
                        new ArtifactStatModifier(ArtifactStat.MoveSpeed, 0.15f)
                    })
            };
        }

        private void NotifyGridChanged(int weaponIndex)
        {
            GridChanged?.Invoke(weaponIndex, loadout.GetWeapon(weaponIndex));
            ModifiersChanged?.Invoke(GetModifierSummary());
        }

        private void NotifyAllChanged()
        {
            GridChanged?.Invoke(0, loadout.Primary);
            GridChanged?.Invoke(1, loadout.Secondary);
            ActiveWeaponChanged?.Invoke(loadout.ActiveWeaponIndex);
            ModifiersChanged?.Invoke(GetModifierSummary());
        }

        private void RebuildCatalog()
        {
            catalog.Clear();
            for (int index = definitions.Count - 1; index >= 0; index--)
            {
                ArtifactDefinitionData definition = definitions[index];
                if (definition == null)
                {
                    definitions.RemoveAt(index);
                    continue;
                }

                definition.EnsureValid();
                if (!catalog.ContainsKey(definition.DefinitionId))
                {
                    catalog.Add(definition.DefinitionId, definition);
                }
            }
        }

        private static int NormalizeWeaponIndex(int weaponIndex)
        {
            return weaponIndex == 1 ? 1 : 0;
        }
    }
}
