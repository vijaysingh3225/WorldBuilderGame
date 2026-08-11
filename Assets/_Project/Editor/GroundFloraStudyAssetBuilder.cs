using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldBuilder.Editor
{
    public static class GroundFloraStudyAssetBuilder
    {
        public const string GalleryRootName =
            "05 - Ground Flora Studies";
        public const int StudyCount = 12;

        private const string RootFolder =
            "Assets/_Project/Art/Environment/GroundFloraStudies";
        private const string MeshFolder = RootFolder + "/Meshes";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string MaterialPath =
            MaterialFolder + "/GroundFloraStudies.mat";

        private enum FloraShape
        {
            Grass,
            SeededGrass,
            Fern,
            Mixed
        }

        private sealed class StudySpec
        {
            public string Name;
            public FloraShape Shape;
            public int Count;
            public float Height;
            public float Width;
            public float Spread;
            public float Bend;
            public Color BaseColor;
            public Color TipColor;
            public int Seed;
        }

        private static readonly StudySpec[] Studies =
        {
            Grass(
                "Short Soft Meadow Tuft", 24, 0.48f, 0.045f,
                0.34f, 0.12f,
                new Color(0.16f, 0.31f, 0.10f),
                new Color(0.34f, 0.52f, 0.17f), 1103),
            Grass(
                "Fine Wispy Hairgrass", 34, 0.82f, 0.018f,
                0.30f, 0.25f,
                new Color(0.20f, 0.34f, 0.11f),
                new Color(0.52f, 0.58f, 0.22f), 2207),
            Grass(
                "Broad Woodland Blades", 15, 0.68f, 0.095f,
                0.38f, 0.18f,
                new Color(0.08f, 0.23f, 0.10f),
                new Color(0.22f, 0.41f, 0.15f), 3301),
            Grass(
                "Tall Arching Forest Grass", 20, 1.18f, 0.042f,
                0.42f, 0.42f,
                new Color(0.12f, 0.27f, 0.08f),
                new Color(0.37f, 0.49f, 0.15f), 4409),
            Seeded(
                "Pale Seedhead Grass", 19, 1.05f, 0.024f,
                0.40f, 0.28f,
                new Color(0.22f, 0.34f, 0.10f),
                new Color(0.66f, 0.61f, 0.25f), 5519),
            Seeded(
                "Sparse Dry Straw Tuft", 13, 0.78f, 0.020f,
                0.38f, 0.18f,
                new Color(0.34f, 0.29f, 0.10f),
                new Color(0.70f, 0.56f, 0.25f), 6607),
            Grass(
                "Deep Green Woodland Sedge", 28, 0.42f, 0.034f,
                0.42f, 0.08f,
                new Color(0.06f, 0.20f, 0.09f),
                new Color(0.16f, 0.36f, 0.13f), 7717),
            Grass(
                "Irregular Patch Edge Mix", 27, 0.72f, 0.050f,
                0.58f, 0.24f,
                new Color(0.12f, 0.25f, 0.08f),
                new Color(0.46f, 0.48f, 0.16f), 8803),
            Fern(
                "Bracken Fern Cluster", 8, 0.92f, 0.055f,
                0.56f, 0.34f,
                new Color(0.07f, 0.22f, 0.08f),
                new Color(0.26f, 0.46f, 0.15f), 9913),
            Fern(
                "Young Fern Rosette", 7, 0.58f, 0.046f,
                0.46f, 0.30f,
                new Color(0.10f, 0.28f, 0.10f),
                new Color(0.36f, 0.56f, 0.18f), 10103),
            Fern(
                "Low Woodland Fern", 6, 0.38f, 0.040f,
                0.52f, 0.36f,
                new Color(0.05f, 0.18f, 0.08f),
                new Color(0.20f, 0.38f, 0.13f), 11117),
            Mixed(
                "Grass And Fern Mosaic", 19, 0.78f, 0.036f,
                0.62f, 0.24f,
                new Color(0.09f, 0.23f, 0.08f),
                new Color(0.38f, 0.50f, 0.17f), 12109)
        };

        public static GameObject[] BuildOrLoadStudies()
        {
            EnsureFolders();
            Material material = GetOrCreateMaterial();
            var prefabs = new GameObject[Studies.Length];
            for (int index = 0; index < Studies.Length; index++)
            {
                StudySpec spec = Studies[index];
                Mesh mesh = BuildMesh(spec);
                string safeName = spec.Name.Replace(" ", string.Empty);
                string meshPath =
                    $"{MeshFolder}/{safeName}.asset";
                Mesh meshAsset =
                    AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (meshAsset == null)
                {
                    mesh.name = spec.Name;
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    meshAsset = mesh;
                }
                else
                {
                    mesh.name = spec.Name;
                    EditorUtility.CopySerialized(mesh, meshAsset);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    EditorUtility.SetDirty(meshAsset);
                }

                var root = new GameObject(spec.Name);
                MeshFilter filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = meshAsset;
                MeshRenderer renderer =
                    root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode =
                    ShadowCastingMode.TwoSided;
                renderer.receiveShadows = true;
                string prefabPath =
                    $"{PrefabFolder}/{safeName}.prefab";
                prefabs[index] = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            return prefabs;
        }

        public static string StudyDisplayName(int index)
        {
            return Studies[index].Name;
        }

        private static StudySpec Grass(
            string name,
            int count,
            float height,
            float width,
            float spread,
            float bend,
            Color baseColor,
            Color tipColor,
            int seed)
        {
            return Spec(
                name, FloraShape.Grass, count, height, width,
                spread, bend, baseColor, tipColor, seed);
        }

        private static StudySpec Seeded(
            string name,
            int count,
            float height,
            float width,
            float spread,
            float bend,
            Color baseColor,
            Color tipColor,
            int seed)
        {
            return Spec(
                name, FloraShape.SeededGrass, count, height, width,
                spread, bend, baseColor, tipColor, seed);
        }

        private static StudySpec Fern(
            string name,
            int count,
            float height,
            float width,
            float spread,
            float bend,
            Color baseColor,
            Color tipColor,
            int seed)
        {
            return Spec(
                name, FloraShape.Fern, count, height, width,
                spread, bend, baseColor, tipColor, seed);
        }

        private static StudySpec Mixed(
            string name,
            int count,
            float height,
            float width,
            float spread,
            float bend,
            Color baseColor,
            Color tipColor,
            int seed)
        {
            return Spec(
                name, FloraShape.Mixed, count, height, width,
                spread, bend, baseColor, tipColor, seed);
        }

        private static StudySpec Spec(
            string name,
            FloraShape shape,
            int count,
            float height,
            float width,
            float spread,
            float bend,
            Color baseColor,
            Color tipColor,
            int seed)
        {
            return new StudySpec
            {
                Name = name,
                Shape = shape,
                Count = count,
                Height = height,
                Width = width,
                Spread = spread,
                Bend = bend,
                BaseColor = baseColor,
                TipColor = tipColor,
                Seed = seed
            };
        }

        private static Mesh BuildMesh(StudySpec spec)
        {
            var vertices = new List<Vector3>(1600);
            var normals = new List<Vector3>(1600);
            var colors = new List<Color>(1600);
            var triangles = new List<int>(2400);
            var random = new System.Random(spec.Seed);
            int grassCount = spec.Shape == FloraShape.Mixed
                ? spec.Count
                : spec.Shape == FloraShape.Grass ||
                  spec.Shape == FloraShape.SeededGrass
                    ? spec.Count
                    : 0;
            for (int index = 0; index < grassCount; index++)
            {
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                float radius = spec.Spread *
                    Mathf.Sqrt(RandomRange(random, 0f, 1f));
                Vector3 basePoint = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
                Vector3 direction = new Vector3(
                    Mathf.Cos(angle + RandomRange(random, -1.1f, 1.1f)),
                    0f,
                    Mathf.Sin(angle + RandomRange(random, -1.1f, 1.1f)));
                float height = spec.Height *
                    RandomRange(random, 0.68f, 1.12f);
                float width = spec.Width *
                    RandomRange(random, 0.72f, 1.24f);
                Vector3 tip = AddBlade(
                    vertices,
                    normals,
                    colors,
                    triangles,
                    basePoint,
                    direction,
                    height,
                    width,
                    spec.Bend * RandomRange(random, 0.55f, 1.15f),
                    spec.BaseColor,
                    spec.TipColor,
                    RandomRange(random, -0.07f, 0.07f));
                if (spec.Shape == FloraShape.SeededGrass &&
                    index % 2 == 0)
                {
                    AddSeedHead(
                        vertices,
                        normals,
                        colors,
                        triangles,
                        tip,
                        direction,
                        height * 0.13f,
                        width * 2.8f,
                        spec.TipColor);
                }
            }

            int fernCount = spec.Shape == FloraShape.Mixed
                ? 4
                : spec.Shape == FloraShape.Fern
                    ? spec.Count
                    : 0;
            for (int index = 0; index < fernCount; index++)
            {
                float angle = Mathf.PI * 2f * index /
                    Mathf.Max(1, fernCount) +
                    RandomRange(random, -0.22f, 0.22f);
                float radius = spec.Shape == FloraShape.Mixed
                    ? spec.Spread * RandomRange(random, 0.18f, 0.55f)
                    : spec.Spread * RandomRange(random, 0f, 0.22f);
                AddFernFrond(
                    vertices,
                    normals,
                    colors,
                    triangles,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius),
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)),
                    spec.Height * RandomRange(random, 0.74f, 1.08f),
                    spec.Spread * RandomRange(random, 0.74f, 1.08f),
                    spec.BaseColor,
                    spec.TipColor,
                    random);
            }

            var mesh = new Mesh
            {
                name = spec.Name,
                indexFormat = vertices.Count > 65000
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 AddBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            Vector3 basePoint,
            Vector3 direction,
            float height,
            float width,
            float bend,
            Color baseColor,
            Color tipColor,
            float sidewaysWobble)
        {
            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            Vector3 normal = Vector3.Cross(
                side,
                Vector3.up + direction * bend).normalized;
            const int segments = 4;
            int first = vertices.Count;
            Vector3 tip = basePoint;
            for (int segment = 0; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float taper = Mathf.Lerp(1f, 0.06f, t);
                Vector3 center =
                    basePoint +
                    Vector3.up * height * t +
                    direction * bend * t * t +
                    side * sidewaysWobble * Mathf.Sin(t * Mathf.PI);
                float halfWidth = width * taper * 0.5f;
                vertices.Add(center - side * halfWidth);
                vertices.Add(center + side * halfWidth);
                normals.Add(normal);
                normals.Add(normal);
                Color color = Color.Lerp(baseColor, tipColor, t);
                colors.Add(color);
                colors.Add(color);
                tip = center;
                if (segment == 0)
                {
                    continue;
                }
                int current = first + segment * 2;
                int previous = current - 2;
                triangles.Add(previous);
                triangles.Add(current);
                triangles.Add(previous + 1);
                triangles.Add(previous + 1);
                triangles.Add(current);
                triangles.Add(current + 1);
            }
            return tip;
        }

        private static void AddSeedHead(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            Vector3 center,
            Vector3 direction,
            float height,
            float width,
            Color color)
        {
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            Vector3 normal = Vector3.Cross(side, Vector3.up).normalized;
            int first = vertices.Count;
            vertices.Add(center - side * width * 0.18f);
            vertices.Add(center + Vector3.up * height * 0.52f - side * width * 0.5f);
            vertices.Add(center + Vector3.up * height);
            vertices.Add(center + Vector3.up * height * 0.52f + side * width * 0.5f);
            for (int index = 0; index < 4; index++)
            {
                normals.Add(normal);
                colors.Add(color);
            }
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private static void AddFernFrond(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            Vector3 basePoint,
            Vector3 direction,
            float height,
            float reach,
            Color baseColor,
            Color tipColor,
            System.Random random)
        {
            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            Vector3 tip = AddBlade(
                vertices,
                normals,
                colors,
                triangles,
                basePoint,
                direction,
                height,
                0.025f,
                reach * 0.72f,
                baseColor,
                tipColor,
                0f);
            const int leafletPairs = 7;
            for (int pair = 1; pair <= leafletPairs; pair++)
            {
                float t = pair / (leafletPairs + 1f);
                Vector3 center =
                    Vector3.Lerp(basePoint, tip, t) +
                    direction * reach * 0.13f * t;
                float leafletLength =
                    reach * Mathf.Sin(t * Mathf.PI) *
                    RandomRange(random, 0.28f, 0.42f);
                float leafletWidth =
                    Mathf.Max(0.025f, leafletLength * 0.28f);
                Color color = Color.Lerp(baseColor, tipColor, t);
                AddLeaflet(
                    vertices,
                    normals,
                    colors,
                    triangles,
                    center,
                    side,
                    direction,
                    leafletLength,
                    leafletWidth,
                    color);
                AddLeaflet(
                    vertices,
                    normals,
                    colors,
                    triangles,
                    center,
                    -side,
                    direction,
                    leafletLength,
                    leafletWidth,
                    color);
            }
        }

        private static void AddLeaflet(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles,
            Vector3 basePoint,
            Vector3 side,
            Vector3 forward,
            float length,
            float width,
            Color color)
        {
            Vector3 tip =
                basePoint + side * length + forward * length * 0.18f;
            Vector3 along = (tip - basePoint).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, along);
            Vector3 middle = Vector3.Lerp(basePoint, tip, 0.48f);
            int first = vertices.Count;
            vertices.Add(basePoint);
            vertices.Add(middle + across * width);
            vertices.Add(tip);
            vertices.Add(middle - across * width);
            for (int index = 0; index < 4; index++)
            {
                normals.Add(Vector3.up);
                colors.Add(color);
            }
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private static float RandomRange(
            System.Random random,
            float minimum,
            float maximum)
        {
            return Mathf.Lerp(
                minimum,
                maximum,
                (float)random.NextDouble());
        }

        private static Material GetOrCreateMaterial()
        {
            Shader shader = Shader.Find(
                "WorldBuilder/Ground Flora Study Lit");
            if (shader == null)
            {
                shader = Shader.Find(
                    "Universal Render Pipeline/Lit");
            }
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "GroundFloraStudies"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    new Color(0.82f, 0.86f, 0.76f, 1f));
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Art/Environment", "GroundFloraStudies");
            EnsureFolder(RootFolder, "Meshes");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
