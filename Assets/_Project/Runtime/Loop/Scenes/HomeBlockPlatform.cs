using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(MeshFilter),
        typeof(MeshRenderer),
        typeof(BoxCollider))]
    public sealed class HomeBlockPlatform : MonoBehaviour
    {
        [SerializeField] private HomePlacementGrid grid;
        [SerializeField, Min(1)] private int columns = 12;
        [SerializeField, Min(1)] private int rows = 10;
        [SerializeField] private Material material;

        private Mesh generatedMesh;

        public int Columns => columns;
        public int Rows => rows;
        public int BlockCount => columns * rows;

        public void Configure(
            HomePlacementGrid placementGrid,
            int platformColumns,
            int platformRows,
            Material blockMaterial)
        {
            grid = placementGrid;
            columns = Mathf.Max(1, platformColumns);
            rows = Mathf.Max(1, platformRows);
            material = blockMaterial;
            Rebuild();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ReleaseMesh();
        }

        private void OnDestroy()
        {
            ReleaseMesh();
        }

        [ContextMenu("Rebuild Block Platform")]
        public void Rebuild()
        {
            float cellSize = grid != null ? grid.CellSize : 2.5f;
            float visibleSize = cellSize;
            var vertices = new List<Vector3>(BlockCount * 24);
            var normals = new List<Vector3>(BlockCount * 24);
            var uv = new List<Vector2>(BlockCount * 24);
            var triangles = new List<int>(BlockCount * 36);
            float firstX = -(columns - 1) * cellSize * 0.5f;
            float firstZ = -(rows - 1) * cellSize * 0.5f;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    AppendCube(
                        vertices,
                        normals,
                        uv,
                        triangles,
                        new Vector3(
                            firstX + x * cellSize,
                            cellSize - visibleSize * 0.5f,
                            firstZ + z * cellSize),
                        visibleSize);
                }
            }

            ReleaseMesh();
            generatedMesh = new Mesh
            {
                name = "Home Block Platform Mesh",
                hideFlags = HideFlags.DontSave
            };
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetNormals(normals);
            generatedMesh.SetUVs(0, uv);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            GetComponent<MeshRenderer>().sharedMaterial = material;

            BoxCollider collider = GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, cellSize * 0.5f, 0f);
            collider.size = new Vector3(
                columns * cellSize,
                cellSize,
                rows * cellSize);
            collider.enabled = true;
            collider.isTrigger = false;
        }

        private void ReleaseMesh()
        {
            if (generatedMesh == null)
            {
                return;
            }
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == generatedMesh)
            {
                filter.sharedMesh = null;
            }
            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }
            generatedMesh = null;
        }

        private static void AppendCube(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 center,
            float size)
        {
            float half = size * 0.5f;
            AddFace(vertices, normals, uv, triangles, center,
                Vector3.up, Vector3.right, Vector3.forward, half);
            AddFace(vertices, normals, uv, triangles, center,
                Vector3.down, Vector3.right, Vector3.back, half);
            AddFace(vertices, normals, uv, triangles, center,
                Vector3.right, Vector3.forward, Vector3.up, half);
            AddFace(vertices, normals, uv, triangles, center,
                Vector3.left, Vector3.back, Vector3.up, half);
            AddFace(vertices, normals, uv, triangles, center,
                Vector3.forward, Vector3.left, Vector3.up, half);
            AddFace(vertices, normals, uv, triangles, center,
                Vector3.back, Vector3.right, Vector3.up, half);
        }

        private static void AddFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 center,
            Vector3 normal,
            Vector3 axisU,
            Vector3 axisV,
            float half)
        {
            int start = vertices.Count;
            Vector3 faceCenter = center + normal * half;
            vertices.Add(faceCenter - axisU * half - axisV * half);
            vertices.Add(faceCenter + axisU * half - axisV * half);
            vertices.Add(faceCenter + axisU * half + axisV * half);
            vertices.Add(faceCenter - axisU * half + axisV * half);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(1f, 1f));
            uv.Add(new Vector2(0f, 1f));
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }
    }
}
