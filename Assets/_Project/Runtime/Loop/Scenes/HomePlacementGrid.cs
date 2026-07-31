using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class HomePlacementGrid : MonoBehaviour
    {
        [SerializeField, Min(0.25f)] private float cellSize = 2.2f;

        public float CellSize => cellSize;

        public void Configure(float size)
        {
            cellSize = Mathf.Max(0.25f, size);
        }

        public Vector3 GetCellCenter(
            Vector2Int cell,
            Vector2Int footprint,
            float elevation)
        {
            Vector2Int safeFootprint =
                new Vector2Int(
                    Mathf.Max(1, footprint.x),
                    Mathf.Max(1, footprint.y));
            float centerX =
                (cell.x + (safeFootprint.x - 1) * 0.5f) *
                cellSize;
            float centerZ =
                (cell.y + (safeFootprint.y - 1) * 0.5f) *
                cellSize;
            return transform.TransformPoint(
                new Vector3(
                    centerX,
                    elevation,
                    centerZ));
        }
    }
}
