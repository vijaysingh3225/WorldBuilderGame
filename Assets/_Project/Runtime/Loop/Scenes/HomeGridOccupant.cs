using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class HomeGridOccupant : MonoBehaviour
    {
        [SerializeField] private HomePlacementGrid grid;
        [SerializeField] private Vector2Int cell;
        [SerializeField] private Vector2Int footprint =
            Vector2Int.one;
        [SerializeField] private float elevation;
        [SerializeField, Range(0, 3)] private int yawQuarterTurns;

        public HomePlacementGrid Grid => grid;
        public Vector2Int Cell => cell;
        public Vector2Int Footprint => footprint;

        public void Configure(
            HomePlacementGrid placementGrid,
            Vector2Int minimumCell,
            Vector2Int occupiedFootprint,
            float height = 0f,
            int quarterTurns = 0)
        {
            grid = placementGrid;
            cell = minimumCell;
            footprint =
                new Vector2Int(
                    Mathf.Max(1, occupiedFootprint.x),
                    Mathf.Max(1, occupiedFootprint.y));
            elevation = height;
            yawQuarterTurns =
                ((quarterTurns % 4) + 4) % 4;
            ApplyPlacement();
        }

        public IEnumerable<Vector2Int> OccupiedCells()
        {
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    yield return cell + new Vector2Int(x, y);
                }
            }
        }

        [ContextMenu("Snap To Home Grid")]
        public void ApplyPlacement()
        {
            if (grid == null)
            {
                return;
            }

            transform.position =
                grid.GetCellCenter(
                    cell,
                    footprint,
                    elevation);
            transform.rotation =
                grid.transform.rotation *
                Quaternion.Euler(
                    0f,
                    yawQuarterTurns * 90f,
                    0f);
        }
    }
}
