using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldBuilder.Editor
{
    public static class FallenTreeStudyAssetBuilder
    {
        public const string GalleryRootName = "05B - Fallen Tree Studies";
        public const int StudyCount = 4;

        private const string RootFolder =
            "Assets/_Project/Art/Environment/FallenTreeStudies";
        private const string MeshFolder = RootFolder + "/Meshes";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string CutMaterialPath =
            MaterialFolder + "/FallenTreeCutWood.mat";
        private const string RingMaterialPath =
            MaterialFolder + "/FallenTreeGrowthRings.mat";
        private const string BarkMaterialPath =
            "Assets/_Project/Art/Prototype/Materials/StylizedForestBark.mat";
        private const string BirchMaterialPath =
            "Assets/_Project/Art/Prototype/Materials/StylizedForestBirchBark.mat";

        private sealed class StudySpec
        {
            public string Name;
            public float Length;
            public float ButtRadius;
            public float TipRadius;
            public bool Birch;
            public Vector2[] Curve;
            public BranchSpec[] Branches;
        }

        private readonly struct BranchSpec
        {
            public BranchSpec(
                float along,
                float yaw,
                float rise,
                float length,
                float radius)
            {
                Along = along;
                Yaw = yaw;
                Rise = rise;
                Length = length;
                Radius = radius;
            }

            public float Along { get; }
            public float Yaw { get; }
            public float Rise { get; }
            public float Length { get; }
            public float Radius { get; }
        }

        private static readonly StudySpec[] Studies =
        {
            new StudySpec
            {
                Name = "Heavy Oak River Span",
                Length = 14.8f,
                ButtRadius = 0.72f,
                TipRadius = 0.43f,
                Curve = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0.28f, 0.10f),
                    new Vector2(0.63f, -0.08f),
                    new Vector2(1f, 0.04f)
                },
                Branches = new[]
                {
                    new BranchSpec(0.34f, -58f, 0.42f, 2.2f, 0.22f),
                    new BranchSpec(0.58f, 72f, 0.25f, 1.35f, 0.16f),
                    new BranchSpec(0.77f, -35f, 0.52f, 1.05f, 0.13f)
                }
            },
            new StudySpec
            {
                Name = "Pale Birch Crossing",
                Length = 12.6f,
                ButtRadius = 0.50f,
                TipRadius = 0.27f,
                Birch = true,
                Curve = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0.23f, -0.13f),
                    new Vector2(0.50f, 0.18f),
                    new Vector2(0.76f, -0.10f),
                    new Vector2(1f, 0.03f)
                },
                Branches = new[]
                {
                    new BranchSpec(0.27f, 54f, 0.58f, 1.55f, 0.13f),
                    new BranchSpec(0.49f, -70f, 0.38f, 1.15f, 0.10f),
                    new BranchSpec(0.70f, 42f, 0.66f, 1.35f, 0.10f),
                    new BranchSpec(0.84f, -48f, 0.28f, 0.72f, 0.07f)
                }
            },
            new StudySpec
            {
                Name = "Crooked Weathered Deadfall",
                Length = 11.7f,
                ButtRadius = 0.61f,
                TipRadius = 0.31f,
                Curve = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0.19f, 0.26f),
                    new Vector2(0.43f, -0.23f),
                    new Vector2(0.67f, 0.22f),
                    new Vector2(0.84f, -0.15f),
                    new Vector2(1f, 0.08f)
                },
                Branches = new[]
                {
                    new BranchSpec(0.21f, -76f, 0.20f, 1.1f, 0.18f),
                    new BranchSpec(0.47f, 64f, 0.34f, 1.7f, 0.15f),
                    new BranchSpec(0.74f, -42f, 0.72f, 1.25f, 0.11f)
                }
            },
            new StudySpec
            {
                Name = "Forked Woodland Trunk",
                Length = 13.8f,
                ButtRadius = 0.66f,
                TipRadius = 0.36f,
                Curve = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0.31f, -0.08f),
                    new Vector2(0.62f, 0.13f),
                    new Vector2(1f, -0.04f)
                },
                Branches = new[]
                {
                    new BranchSpec(0.46f, 58f, 0.46f, 2.8f, 0.25f),
                    new BranchSpec(0.50f, -51f, 0.61f, 2.45f, 0.23f),
                    new BranchSpec(0.76f, 73f, 0.30f, 1.05f, 0.12f)
                }
            }
        };

        [MenuItem("WorldBuilder/Build/Fallen Tree Studies")]
        public static void BuildFromMenu()
        {
            BuildOrLoadStudies();
            Debug.Log("Built four river-scale fallen tree studies.");
        }

        [MenuItem("WorldBuilder/Build/Fallen Tree Review Gallery %#f")]
        public static void BuildReviewGalleryFromMenu()
        {
            GameplayLoopSceneBuilder.BuildEnvironmentAssetGalleryOnly();
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void ScheduleFirstGalleryBuild()
        {
            EditorApplication.delayCall += BuildGalleryIfMissing;
        }

        private static void BuildGalleryIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BuildGalleryIfMissing;
                return;
            }

            const string galleryPath =
                "Assets/_Project/Scenes/EnvironmentAssetGallery.unity";
            if (File.Exists(galleryPath) &&
                File.ReadAllText(galleryPath).Contains(GalleryRootName))
            {
                return;
            }

            GameplayLoopSceneBuilder.
                BuildEnvironmentAssetGalleryFromCommandLine();
            Debug.Log(
                "Built and opened the fallen-tree Environment Asset Gallery review.");
        }

        public static GameObject[] BuildOrLoadStudies()
        {
            EnsureFolders();
            Material bark = AssetDatabase.LoadAssetAtPath<Material>(
                BarkMaterialPath);
            Material birch = AssetDatabase.LoadAssetAtPath<Material>(
                BirchMaterialPath);
            Material cut = GetOrCreateMaterial(
                CutMaterialPath,
                "Fallen Tree Cut Wood",
                new Color(0.48f, 0.31f, 0.16f),
                0.08f);
            Material rings = GetOrCreateMaterial(
                RingMaterialPath,
                "Fallen Tree Growth Rings",
                new Color(0.20f, 0.105f, 0.045f),
                0.04f);
            var prefabs = new GameObject[Studies.Length];
            for (int index = 0; index < Studies.Length; index++)
            {
                prefabs[index] = BuildStudy(
                    Studies[index],
                    Studies[index].Birch ? birch : bark,
                    cut,
                    rings);
            }
            AssetDatabase.SaveAssets();
            return prefabs;
        }

        public static string StudyDisplayName(int index)
        {
            return Studies[index].Name;
        }

        public static float StudyLength(int index)
        {
            return Studies[index].Length;
        }

        private static GameObject BuildStudy(
            StudySpec spec,
            Material bark,
            Material cut,
            Material rings)
        {
            string safeName = spec.Name.Replace(" ", string.Empty);
            Vector3[] centers = BuildCenters(spec);
            float[] radii = BuildRadii(spec, centers.Length);
            Mesh trunkMesh = BuildTaperedTube(
                centers,
                radii,
                10,
                capStart: true,
                capEnd: true,
                jaggedEnd: true);
            Mesh trunkAsset = SaveMesh(
                trunkMesh,
                $"{MeshFolder}/{safeName}Trunk.asset",
                spec.Name + " Trunk");

            var root = new GameObject(spec.Name);
            MeshFilter trunkFilter = root.AddComponent<MeshFilter>();
            trunkFilter.sharedMesh = trunkAsset;
            MeshRenderer trunkRenderer = root.AddComponent<MeshRenderer>();
            trunkRenderer.sharedMaterials = new[] { bark, cut };
            trunkRenderer.shadowCastingMode = ShadowCastingMode.On;
            trunkRenderer.receiveShadows = true;
            root.AddComponent<MeshCollider>().sharedMesh = trunkAsset;

            AddGrowthRings(
                root.transform,
                centers[0],
                (centers[0] - centers[1]).normalized,
                radii[0],
                rings,
                safeName);

            for (int index = 0; index < spec.Branches.Length; index++)
            {
                BranchSpec branch = spec.Branches[index];
                Vector3 basePoint = SampleCenter(centers, branch.Along);
                Vector3 trunkDirection = SampleDirection(centers, branch.Along);
                Vector3 side = Quaternion.AngleAxis(
                        branch.Yaw,
                        Vector3.up) *
                    Vector3.Cross(Vector3.up, trunkDirection).normalized;
                Vector3 direction = (
                    side +
                    Vector3.up * branch.Rise +
                    trunkDirection * 0.14f).normalized;
                float baseRadius = Mathf.Lerp(
                    spec.ButtRadius,
                    spec.TipRadius,
                    branch.Along);
                Vector3 start = basePoint +
                    direction * baseRadius * 0.45f;
                Vector3 middle = start +
                    direction * branch.Length * 0.58f +
                    Vector3.up * branch.Length * 0.08f;
                Vector3 end = start +
                    direction * branch.Length;
                Mesh branchMesh = BuildTaperedTube(
                    new[] { start, middle, end },
                    new[]
                    {
                        branch.Radius,
                        branch.Radius * 0.72f,
                        branch.Radius * 0.48f
                    },
                    7,
                    capStart: false,
                    capEnd: true,
                    jaggedEnd: true);
                Mesh branchAsset = SaveMesh(
                    branchMesh,
                    $"{MeshFolder}/{safeName}Branch{index + 1}.asset",
                    $"{spec.Name} Branch {index + 1}");
                GameObject branchObject = new GameObject(
                    $"Broken Branch {index + 1}");
                branchObject.transform.SetParent(root.transform, false);
                branchObject.AddComponent<MeshFilter>().sharedMesh =
                    branchAsset;
                MeshRenderer branchRenderer =
                    branchObject.AddComponent<MeshRenderer>();
                branchRenderer.sharedMaterials = new[] { bark, cut };
                branchRenderer.shadowCastingMode = ShadowCastingMode.On;
                branchRenderer.receiveShadows = true;
            }

            string prefabPath = $"{PrefabFolder}/{safeName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Vector3[] BuildCenters(StudySpec spec)
        {
            var centers = new Vector3[spec.Curve.Length];
            for (int index = 0; index < centers.Length; index++)
            {
                float t = spec.Curve[index].x;
                float radius = Mathf.Lerp(
                    spec.ButtRadius,
                    spec.TipRadius,
                    t);
                centers[index] = new Vector3(
                    (t - 0.5f) * spec.Length,
                    radius + Mathf.Sin(t * Mathf.PI) * 0.10f,
                    spec.Curve[index].y);
            }
            return centers;
        }

        private static float[] BuildRadii(StudySpec spec, int count)
        {
            var radii = new float[count];
            for (int index = 0; index < count; index++)
            {
                float t = index / (float)(count - 1);
                radii[index] = Mathf.Lerp(
                        spec.ButtRadius,
                        spec.TipRadius,
                        t) *
                    (1f + Mathf.Sin(index * 2.17f) * 0.035f);
            }
            return radii;
        }

        private static Mesh BuildTaperedTube(
            IReadOnlyList<Vector3> centers,
            IReadOnlyList<float> radii,
            int sides,
            bool capStart,
            bool capEnd,
            bool jaggedEnd)
        {
            var vertices = new List<Vector3>(centers.Count * sides + 2);
            var normals = new List<Vector3>(centers.Count * sides + 2);
            var barkTriangles = new List<int>();
            var capTriangles = new List<int>();
            for (int ring = 0; ring < centers.Count; ring++)
            {
                Vector3 tangent = ring == 0
                    ? centers[1] - centers[0]
                    : ring == centers.Count - 1
                        ? centers[ring] - centers[ring - 1]
                        : centers[ring + 1] - centers[ring - 1];
                tangent.Normalize();
                Vector3 frameUp = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) >
                    0.92f ? Vector3.forward : Vector3.up;
                Vector3 side = Vector3.Cross(tangent, frameUp).normalized;
                Vector3 up = Vector3.Cross(side, tangent).normalized;
                for (int sideIndex = 0; sideIndex < sides; sideIndex++)
                {
                    float angle = sideIndex / (float)sides * Mathf.PI * 2f;
                    float facet = 1f +
                        Mathf.Sin(sideIndex * 2.41f + ring * 1.37f) * 0.045f;
                    float endOffset = jaggedEnd &&
                        ring == centers.Count - 1
                            ? (sideIndex % 3 - 1) * radii[ring] * 0.16f
                            : 0f;
                    Vector3 radial =
                        side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    vertices.Add(
                        centers[ring] +
                        radial * radii[ring] * facet +
                        tangent * endOffset);
                    normals.Add(radial);
                }
            }
            for (int ring = 0; ring < centers.Count - 1; ring++)
            {
                int next = ring + 1;
                for (int sideIndex = 0; sideIndex < sides; sideIndex++)
                {
                    int following = (sideIndex + 1) % sides;
                    int a = ring * sides + sideIndex;
                    int b = ring * sides + following;
                    int c = next * sides + sideIndex;
                    int d = next * sides + following;
                    barkTriangles.Add(a);
                    barkTriangles.Add(c);
                    barkTriangles.Add(b);
                    barkTriangles.Add(b);
                    barkTriangles.Add(c);
                    barkTriangles.Add(d);
                }
            }
            if (capStart)
            {
                AddCap(vertices, normals, capTriangles, centers[0], sides, 0, false);
            }
            if (capEnd)
            {
                AddCap(
                    vertices,
                    normals,
                    capTriangles,
                    centers[centers.Count - 1],
                    sides,
                    (centers.Count - 1) * sides,
                    true);
            }

            var mesh = new Mesh { subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(barkTriangles, 0);
            mesh.SetTriangles(capTriangles, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 center,
            int sides,
            int ringStart,
            bool forward)
        {
            Vector3 tangent = forward
                ? (vertices[ringStart] - center).sqrMagnitude > 0f
                    ? Vector3.right
                    : Vector3.right
                : Vector3.left;
            int centerIndex = vertices.Count;
            vertices.Add(center);
            normals.Add(tangent);
            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                triangles.Add(centerIndex);
                triangles.Add(
                    ringStart + (forward ? index : next));
                triangles.Add(
                    ringStart + (forward ? next : index));
            }
        }

        private static void AddGrowthRings(
            Transform root,
            Vector3 center,
            Vector3 normal,
            float radius,
            Material material,
            string safeName)
        {
            for (int ring = 0; ring < 2; ring++)
            {
                float outer = radius * (0.33f + ring * 0.27f);
                float inner = outer - radius * 0.035f;
                Mesh mesh = BuildAnnulus(
                    center + normal * 0.012f,
                    normal,
                    inner,
                    outer,
                    18);
                Mesh asset = SaveMesh(
                    mesh,
                    $"{MeshFolder}/{safeName}GrowthRing{ring + 1}.asset",
                    $"{safeName} Growth Ring {ring + 1}");
                GameObject ringObject = new GameObject(
                    $"Growth Ring {ring + 1}");
                ringObject.transform.SetParent(root, false);
                ringObject.AddComponent<MeshFilter>().sharedMesh = asset;
                ringObject.AddComponent<MeshRenderer>().sharedMaterial =
                    material;
            }
        }

        private static Mesh BuildAnnulus(
            Vector3 center,
            Vector3 normal,
            float inner,
            float outer,
            int sides)
        {
            Vector3 side = Vector3.Cross(normal, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.right;
            }
            side.Normalize();
            Vector3 up = Vector3.Cross(side, normal).normalized;
            var vertices = new Vector3[sides * 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[sides * 6];
            for (int index = 0; index < sides; index++)
            {
                float angle = index / (float)sides * Mathf.PI * 2f;
                Vector3 radial =
                    side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                vertices[index * 2] = center + radial * inner;
                vertices[index * 2 + 1] = center + radial * outer;
                normals[index * 2] = normal;
                normals[index * 2 + 1] = normal;
                int next = (index + 1) % sides;
                int offset = index * 6;
                triangles[offset] = index * 2;
                triangles[offset + 1] = next * 2 + 1;
                triangles[offset + 2] = index * 2 + 1;
                triangles[offset + 3] = index * 2;
                triangles[offset + 4] = next * 2;
                triangles[offset + 5] = next * 2 + 1;
            }
            var mesh = new Mesh
            {
                vertices = vertices,
                normals = normals,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 SampleCenter(
            IReadOnlyList<Vector3> centers,
            float t)
        {
            float scaled = Mathf.Clamp01(t) * (centers.Count - 1);
            int index = Mathf.Min(
                Mathf.FloorToInt(scaled),
                centers.Count - 2);
            return Vector3.Lerp(
                centers[index],
                centers[index + 1],
                scaled - index);
        }

        private static Vector3 SampleDirection(
            IReadOnlyList<Vector3> centers,
            float t)
        {
            float scaled = Mathf.Clamp01(t) * (centers.Count - 1);
            int index = Mathf.Min(
                Mathf.FloorToInt(scaled),
                centers.Count - 2);
            return (centers[index + 1] - centers[index]).normalized;
        }

        private static Mesh SaveMesh(Mesh mesh, string path, string name)
        {
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (asset == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, asset);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material GetOrCreateMaterial(
            string path,
            string name,
            Color color,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Art/Environment", "FallenTreeStudies");
            EnsureFolder(RootFolder, "Meshes");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
