using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class HomeGridOccupant :
        MonoBehaviour,
        ISerializationCallbackReceiver
    {
        private const int CurrentSerializationVersion = 1;

        [SerializeField] private HomePlacementGrid grid;
        [SerializeField] private Vector3Int cell;
        [SerializeField] private Vector3Int footprint = Vector3Int.one;
        [SerializeField, Range(0, 3)] private int yawQuarterTurns;
        [SerializeField, HideInInspector] private int serializationVersion;

        public HomePlacementGrid Grid => grid;
        public Vector3Int Cell => cell;
        public Vector3Int Footprint => footprint;
        public int YawQuarterTurns => yawQuarterTurns;

        public bool Configure(
            HomePlacementGrid placementGrid,
            Vector3Int minimumCell,
            Vector3Int occupiedFootprint,
            int quarterTurns = 0)
        {
            HomePlacementGrid previousGrid = grid;
            Vector3Int previousCell = cell;
            Vector3Int previousFootprint = footprint;
            int previousQuarterTurns = yawQuarterTurns;

            grid = placementGrid;
            cell = minimumCell;
            footprint = HomePlacementGrid.SanitizeFootprint(
                occupiedFootprint);
            yawQuarterTurns = NormalizeQuarterTurns(quarterTurns);
            serializationVersion = CurrentSerializationVersion;

            if (TryClaimPlacement())
            {
                if (previousGrid != null && previousGrid != grid)
                {
                    previousGrid.Release(this);
                }
                ApplyTransform();
                return true;
            }

            grid = previousGrid;
            cell = previousCell;
            footprint = previousFootprint;
            yawQuarterTurns = previousQuarterTurns;
            TryClaimPlacement();
            ApplyTransform();
            return false;
        }

        public bool TryPlace(Vector3Int minimumCell)
        {
            return Configure(
                grid,
                minimumCell,
                footprint,
                yawQuarterTurns);
        }

        public bool TryRotate(int quarterTurns)
        {
            return Configure(
                grid,
                cell,
                footprint,
                quarterTurns);
        }

        public IEnumerable<Vector3Int> OccupiedCells()
        {
            return HomePlacementGrid.EnumerateCells(
                cell,
                OrientedFootprint);
        }

        [ContextMenu("Snap To Home Grid")]
        public void ApplyPlacement()
        {
            if (grid == null)
            {
                return;
            }
            if (!TryClaimPlacement())
            {
                Debug.LogError(
                    $"Cannot place {name}: Home grid cells are occupied.",
                    this);
                return;
            }
            ApplyTransform();
        }

        private Vector3Int OrientedFootprint =>
            (yawQuarterTurns & 1) == 0
                ? footprint
                : new Vector3Int(
                    footprint.z,
                    footprint.y,
                    footprint.x);

        private bool TryClaimPlacement()
        {
            return grid != null &&
                grid.TryOccupy(this, cell, OrientedFootprint);
        }

        private void ApplyTransform()
        {
            if (grid == null)
            {
                return;
            }
            transform.position = grid.GetFootprintBaseCenter(
                cell,
                OrientedFootprint);
            transform.rotation = grid.transform.rotation *
                Quaternion.Euler(0f, yawQuarterTurns * 90f, 0f);
        }

        private void OnDisable()
        {
            grid?.Release(this);
        }

        private void OnEnable()
        {
            if (grid != null)
            {
                ApplyPlacement();
            }
        }

        private void OnDestroy()
        {
            grid?.Release(this);
        }

        private static int NormalizeQuarterTurns(int quarterTurns)
        {
            return ((quarterTurns % 4) + 4) % 4;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (serializationVersion >= CurrentSerializationVersion)
            {
                return;
            }

            int migratedZ = cell.z != 0 ? cell.z : cell.y;
            int migratedDepth = footprint.z > 0
                ? footprint.z
                : footprint.y;
            cell = new Vector3Int(cell.x, 0, migratedZ);
            footprint = HomePlacementGrid.SanitizeFootprint(
                new Vector3Int(
                    footprint.x,
                    1,
                    migratedDepth));
            serializationVersion = CurrentSerializationVersion;
        }
    }
}
