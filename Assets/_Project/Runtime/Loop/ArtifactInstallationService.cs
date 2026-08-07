using System;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop
{
    public static class ArtifactInstallationService
    {
        public static bool TryInstall(
            PlayerProfile profile,
            WeaponGridRuntime runtime,
            int weaponIndex,
            StorageEntry entry,
            string adjacentChestId,
            GridCoordinate anchor,
            int rotation,
            out string reason)
        {
            if (profile == null || runtime == null || entry == null)
            {
                reason = "The anvil is missing its profile, weapon grid, or item.";
                return false;
            }
            if (!ItemDefinitionCatalog.IsArtifact(entry.DefinitionId))
            {
                reason = $"{ItemDefinitionCatalog.DisplayName(entry.DefinitionId)} is not an artifact.";
                return false;
            }
            bool inInventory = profile.IsInInventory(entry.EntryId);
            bool inAdjacentChest = !string.IsNullOrWhiteSpace(adjacentChestId) &&
                Contains(profile.GetChestEntryIds(adjacentChestId), entry.EntryId);
            if (!inInventory && !inAdjacentChest)
            {
                reason = "Artifacts can only be installed from your backpack or an adjacent chest.";
                return false;
            }
            if (entry.Quantity != 1)
            {
                reason = "Artifact stacks must be split before installation.";
                return false;
            }

            var artifact = new ArtifactInstance(entry.EntryId, entry.DefinitionId);
            if (!runtime.TryPlace(weaponIndex, artifact, anchor, rotation, out reason))
            {
                return false;
            }
            if (profile.RemoveStorageEntry(entry.EntryId))
            {
                reason = string.Empty;
                return true;
            }

            runtime.TryRemoveInstance(weaponIndex, artifact.InstanceId, out _);
            reason = "The item changed before it could be installed.";
            return false;
        }

        public static bool TryReturnToStorage(
            PlayerProfile profile,
            WeaponGridRuntime runtime,
            int weaponIndex,
            ArtifactPlacement placement,
            string adjacentChestId,
            out string reason)
        {
            if (profile == null || runtime == null || placement?.Artifact == null)
            {
                reason = "The installed artifact could not be resolved.";
                return false;
            }
            ArtifactInstance artifact = placement.Artifact;
            if (!runtime.TryRemoveInstance(weaponIndex, artifact.InstanceId, out _))
            {
                reason = "That artifact is no longer installed.";
                return false;
            }

            StorageEntry restored = StorageEntry.CreateWithId(
                artifact.InstanceId,
                artifact.DefinitionId);
            profile.AddToStorage(restored);
            bool stored = profile.TryMoveToInventory(restored.EntryId);
            if (!stored && !string.IsNullOrWhiteSpace(adjacentChestId))
            {
                stored = profile.MoveToChest(restored.EntryId, adjacentChestId);
            }
            if (stored)
            {
                reason = string.Empty;
                return true;
            }

            profile.RemoveStorageEntry(restored.EntryId);
            runtime.TryPlace(
                weaponIndex,
                artifact,
                placement.Anchor,
                placement.Rotation,
                out _);
            reason = "Your backpack and adjacent chest are both full.";
            return false;
        }

        private static bool Contains(
            System.Collections.Generic.IReadOnlyList<string> ids,
            string wanted)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (string.Equals(ids[index], wanted, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
