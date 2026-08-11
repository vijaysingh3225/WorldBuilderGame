using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class HomePlacementGrid : MonoBehaviour
    {
        [SerializeField, Min(0.25f)] private float cellSize = 2.2f;

        private readonly Dictionary<Vector3Int, HomeGridOccupant>
            occupantsByCell =
                new Dictionary<Vector3Int, HomeGridOccupant>();

        public float CellSize => cellSize;

        public void Configure(float size)
        {
            cellSize = Mathf.Max(0.25f, size);
            ReapplyPlacements();
        }

        public Vector3 CellToWorldCenter(Vector3Int cell)
        {
            return transform.TransformPoint(
                ((Vector3)cell + Vector3.one * 0.5f) * cellSize);
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 local =
                transform.InverseTransformPoint(worldPosition) / cellSize;
            return new Vector3Int(
                Mathf.FloorToInt(local.x),
                Mathf.FloorToInt(local.y),
                Mathf.FloorToInt(local.z));
        }

        public Bounds GetWorldBounds(
            Vector3Int minimumCell,
            Vector3Int footprint)
        {
            Vector3Int safeFootprint = SanitizeFootprint(footprint);
            Vector3 localCenter =
                ((Vector3)minimumCell + (Vector3)safeFootprint * 0.5f) *
                cellSize;
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 worldSize = Vector3.Scale(
                (Vector3)safeFootprint * cellSize,
                Abs(transform.lossyScale));
            return new Bounds(worldCenter, worldSize);
        }

        public Vector3 GetFootprintBaseCenter(
            Vector3Int minimumCell,
            Vector3Int footprint)
        {
            Vector3Int safeFootprint = SanitizeFootprint(footprint);
            return transform.TransformPoint(
                new Vector3(
                    (minimumCell.x + safeFootprint.x * 0.5f) * cellSize,
                    minimumCell.y * cellSize,
                    (minimumCell.z + safeFootprint.z * 0.5f) * cellSize));
        }

        public bool CanOccupy(
            HomeGridOccupant occupant,
            Vector3Int minimumCell,
            Vector3Int footprint)
        {
            foreach (Vector3Int cell in EnumerateCells(
                         minimumCell,
                         footprint))
            {
                if (occupantsByCell.TryGetValue(
                        cell,
                        out HomeGridOccupant existing) &&
                    existing != null &&
                    existing != occupant)
                {
                    return false;
                }
            }
            return true;
        }

        internal bool TryOccupy(
            HomeGridOccupant occupant,
            Vector3Int minimumCell,
            Vector3Int footprint)
        {
            if (occupant == null ||
                !CanOccupy(occupant, minimumCell, footprint))
            {
                return false;
            }

            Release(occupant);
            foreach (Vector3Int cell in EnumerateCells(
                         minimumCell,
                         footprint))
            {
                occupantsByCell[cell] = occupant;
            }
            return true;
        }

        internal void Release(HomeGridOccupant occupant)
        {
            if (occupant == null || occupantsByCell.Count == 0)
            {
                return;
            }

            var releasedCells = new List<Vector3Int>();
            foreach (KeyValuePair<Vector3Int, HomeGridOccupant> entry in
                     occupantsByCell)
            {
                if (entry.Value == occupant || entry.Value == null)
                {
                    releasedCells.Add(entry.Key);
                }
            }
            for (int index = 0; index < releasedCells.Count; index++)
            {
                occupantsByCell.Remove(releasedCells[index]);
            }
        }

        public HomeGridOccupant GetOccupant(Vector3Int cell)
        {
            occupantsByCell.TryGetValue(cell, out HomeGridOccupant occupant);
            return occupant;
        }

        public static Vector3Int SanitizeFootprint(Vector3Int footprint)
        {
            return new Vector3Int(
                Mathf.Max(1, footprint.x),
                Mathf.Max(1, footprint.y),
                Mathf.Max(1, footprint.z));
        }

        public static IEnumerable<Vector3Int> EnumerateCells(
            Vector3Int minimumCell,
            Vector3Int footprint)
        {
            Vector3Int safeFootprint = SanitizeFootprint(footprint);
            for (int y = 0; y < safeFootprint.y; y++)
            {
                for (int z = 0; z < safeFootprint.z; z++)
                {
                    for (int x = 0; x < safeFootprint.x; x++)
                    {
                        yield return minimumCell +
                            new Vector3Int(x, y, z);
                    }
                }
            }
        }

        private void ReapplyPlacements()
        {
            HomeGridOccupant[] occupants =
                FindObjectsByType<HomeGridOccupant>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < occupants.Length; index++)
            {
                if (occupants[index].Grid == this)
                {
                    occupants[index].ApplyPlacement();
                }
            }
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }
    }
}
