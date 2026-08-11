using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Weapons
{
    public enum ShortSwordBladeProfile
    {
        StraightPoint,
        LongTaper,
        RoundedShoulder,
        ForwardSwept,
        ClipPoint
    }

    public enum ShortSwordBladeBackStyle
    {
        Clean,
        Sawback,
        SteppedSpine
    }

    public enum ShortSwordGuardProfile
    {
        Straight,
        Downturned,
        Upswept,
        Bowed,
        HookedQuillons,
        Slanted,
        OffsetQuillons
    }

    public enum ShortSwordGuardConstruction
    {
        RazorBar,
        BladeQuillons,
        WingedW,
        Crescent,
        DirectionalSweep,
        OffsetLeaf
    }

    public enum ShortSwordHandleProfile
    {
        Straight,
        Tapered,
        Waisted
    }

    public enum ShortSwordHiltProfile
    {
        Disc,
        Faceted,
        ScentStopper,
        Crowned,
        Hooked
    }

    public enum ShortSwordMetalFamily
    {
        Iron,
        Bronze,
        Silver,
        BlackenedSteel
    }

    public enum ShortSwordGripStyle
    {
        LeatherBands,
        CrossWrappedCord,
        RibbedWood,
        StuddedLeather
    }

    public enum ShortSwordGripColor
    {
        DarkBrown,
        OxBlood,
        Charcoal,
        WornTan,
        ForestGreen
    }

    public enum ShortSwordOrnamentStyle
    {
        Plain,
        GuardGem,
        PommelGem
    }

    public enum ShortSwordGemFamily
    {
        Ruby,
        Emerald,
        Sapphire,
        Amber
    }

    public enum ShortSwordGemCut
    {
        Round,
        Oval,
        PrincessSquare,
        Emerald,
        Pear
    }

    [Serializable]
    public struct ProceduralShortSwordDefinition
    {
        public int Seed;
        public ShortSwordBladeProfile BladeProfile;
        public ShortSwordBladeBackStyle BladeBackStyle;
        public ShortSwordGuardProfile GuardProfile;
        public ShortSwordGuardConstruction GuardConstruction;
        public ShortSwordHandleProfile HandleProfile;
        public ShortSwordHiltProfile HiltProfile;
        public ShortSwordMetalFamily MetalFamily;
        public ShortSwordGripStyle GripStyle;
        public ShortSwordGripColor GripColor;
        public ShortSwordOrnamentStyle OrnamentStyle;
        public ShortSwordGemFamily GemFamily;
        public ShortSwordGemCut GemCut;
        public int DirectionSign;
        public float BladeLength;
        public float BladeWidth;
        public float BladeThickness;
        public float TipLength;
        public float GuardSpan;
        public float GuardHeight;
        public float GuardDepth;
        public int GuardCurveSegments;
        public int GuardCrossSectionSides;
        public float GuardCrossSectionRotation;
        public float HandleLength;
        public float HandleRadius;
        public float HiltLength;
        public float HiltRadius;

        public float TotalLength =>
            BladeLength + HandleLength + HiltLength;
    }

    [DisallowMultipleComponent]
    public sealed class ProceduralShortSwordGenerator : MonoBehaviour
    {
        public const string BladePartName = "Blade";
        public const string GuardPartName = "Guard";
        public const string HandlePartName = "Handle";
        public const string HiltPartName = "Hilt / Pommel";
        public const string BladeFracturePrefix = "Blade Fracture";
        public const float TargetFacetLength = 0.052f;

        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private int startingSeed = 1201;
        [SerializeField] private Material bladeMaterial;
        [SerializeField] private Material guardMaterial;
        [SerializeField] private Material handleMaterial;
        [SerializeField] private Material hiltMaterial;

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<GameObject> generatedParts =
            new List<GameObject>();
        private ProceduralShortSwordDefinition currentDefinition;
        private bool hasGeneratedSword;
        private bool isBladeCracked;
        private int fractureRevision;
        private int mainFractureCount;
        private int missingFracturePieceCount;
        private float minimumFractureSegmentRise;

        public ProceduralShortSwordDefinition CurrentDefinition =>
            currentDefinition;
        public bool HasGeneratedSword => hasGeneratedSword;
        public bool IsBladeCracked => isBladeCracked;
        public int FractureRevision => fractureRevision;
        public int MainFractureCount => mainFractureCount;
        public int MissingFracturePieceCount => missingFracturePieceCount;
        public float MinimumFractureSegmentRise => minimumFractureSegmentRise;
        public IReadOnlyList<GameObject> GeneratedParts => generatedParts;

        private void Start()
        {
            if (generateOnStart && !hasGeneratedSword)
            {
                Generate(startingSeed);
            }
        }

        private void OnDestroy()
        {
            ClearGeneratedSword();
        }

        public void ConfigureMaterials(
            Material blade,
            Material guard,
            Material handle,
            Material hilt)
        {
            bladeMaterial = blade;
            guardMaterial = guard;
            handleMaterial = handle;
            hiltMaterial = hilt;
        }

        public ProceduralShortSwordDefinition GenerateNext()
        {
            int seed = hasGeneratedSword
                ? unchecked(currentDefinition.Seed + 1)
                : startingSeed;
            return Generate(seed);
        }

        public ProceduralShortSwordDefinition Generate(int seed)
        {
            ClearGeneratedSword();
            currentDefinition = CreateDefinition(seed);

            CreatePart(
                BladePartName,
                BuildBladeMesh(currentDefinition),
                bladeMaterial,
                ResolveBladeColor(currentDefinition.MetalFamily),
                0.46f,
                0.30f);
            GameObject guard = CreatePart(
                GuardPartName,
                BuildGuardMesh(currentDefinition),
                guardMaterial,
                ResolveMetalColor(currentDefinition.MetalFamily),
                ResolveMetallic(currentDefinition.MetalFamily),
                0.34f);
            GameObject handle = CreatePart(
                HandlePartName,
                BuildHandleMesh(currentDefinition),
                handleMaterial,
                ResolveGripColor(currentDefinition.GripColor),
                0.02f,
                0.18f);
            GameObject hilt = CreatePart(
                HiltPartName,
                BuildHiltMesh(currentDefinition),
                hiltMaterial,
                ResolveMetalColor(currentDefinition.MetalFamily),
                ResolveMetallic(currentDefinition.MetalFamily),
                0.34f);

            CreateGuardDetails(guard, currentDefinition);
            CreateHandleDetails(handle, currentDefinition);
            CreateHiltDetails(hilt, currentDefinition);

            hasGeneratedSword = true;
            return currentDefinition;
        }

        public int CrackBlade()
        {
            if (!hasGeneratedSword)
            {
                return 0;
            }
            GameObject blade = generatedParts.Find(part =>
                part != null && part.name == BladePartName);
            MeshFilter bladeFilter = blade != null
                ? blade.GetComponent<MeshFilter>()
                : null;
            MeshRenderer bladeRenderer = blade != null
                ? blade.GetComponent<MeshRenderer>()
                : null;
            if (bladeFilter == null || bladeFilter.sharedMesh == null ||
                bladeRenderer == null)
            {
                return 0;
            }

            ClearBladeFracture(blade);
            fractureRevision++;
            var random = new System.Random(unchecked(
                currentDefinition.Seed * 486187739 +
                fractureRevision * 7919));
            int crackCount = random.Next(2, 4);
            const int crackNodeCount = 5;
            float[,] crackNodes = new float[crackCount, crackNodeCount];
            int[] branchSides = new int[crackCount];
            bool[] branchAbove = new bool[crackCount];
            float[] branchReach = new float[crackCount];
            bool[] missingBranches = new bool[crackCount];
            mainFractureCount = crackCount;
            minimumFractureSegmentRise = float.PositiveInfinity;
            float bladeBase = ResolveBladeSeatHeightAtX(
                currentDefinition,
                0f);
            for (int cut = 0; cut < crackCount; cut++)
            {
                float t = (cut + 1f) / (crackCount + 1f);
                t += Lerp(random, -0.055f, 0.055f);
                float baseHeight = Mathf.Lerp(
                    bladeBase + 0.08f,
                    currentDefinition.BladeLength - 0.10f,
                    t);
                int slopeSign = random.Next(0, 2) == 0 ? -1 : 1;
                float totalRise = Lerp(random, 0.105f, 0.190f);
                float[] rises = new float[crackNodeCount - 1];
                float accumulatedRise = 0f;
                for (int segment = 0; segment < rises.Length; segment++)
                {
                    rises[segment] = totalRise / rises.Length *
                        Lerp(random, 0.72f, 1.28f);
                    accumulatedRise += rises[segment];
                    minimumFractureSegmentRise = Mathf.Min(
                        minimumFractureSegmentRise,
                        rises[segment]);
                }
                float currentHeight = baseHeight -
                    slopeSign * accumulatedRise * 0.5f;
                crackNodes[cut, 0] = currentHeight;
                for (int node = 1; node < crackNodeCount; node++)
                {
                    currentHeight += slopeSign * rises[node - 1];
                    crackNodes[cut, node] = currentHeight;
                }
                branchSides[cut] = random.Next(0, 2) == 0 ? -1 : 1;
                branchAbove[cut] = random.Next(0, 2) == 0;
                branchReach[cut] = Lerp(random, 0.070f, 0.115f);
            }
            int firstMissingBranch = random.Next(0, crackCount);
            missingBranches[firstMissingBranch] = true;
            if (crackCount == 3 && random.NextDouble() < 0.28)
            {
                int secondMissingBranch = (firstMissingBranch +
                    random.Next(1, crackCount)) % crackCount;
                missingBranches[secondMissingBranch] = true;
            }
            missingFracturePieceCount = 0;
            for (int cut = 0; cut < crackCount; cut++)
            {
                if (missingBranches[cut])
                {
                    missingFracturePieceCount++;
                }
            }

            Mesh source = bladeFilter.sharedMesh;
            int mainFragmentCount = crackCount + 1;
            int groupCount = mainFragmentCount + crackCount;
            var fragmentVertices = new List<Vector3>[groupCount];
            var fragmentTriangles = new List<int>[groupCount];
            for (int fragment = 0; fragment < groupCount; fragment++)
            {
                fragmentVertices[fragment] = new List<Vector3>();
                fragmentTriangles[fragment] = new List<int>();
            }
            Vector3[] sourceVertices = source.vertices;
            int[] sourceTriangles = source.triangles;
            for (int triangle = 0;
                 triangle < sourceTriangles.Length;
                 triangle += 3)
            {
                var sourcePolygon = new List<Vector3>
                {
                    sourceVertices[sourceTriangles[triangle]],
                    sourceVertices[sourceTriangles[triangle + 1]],
                    sourceVertices[sourceTriangles[triangle + 2]]
                };
                for (int band = 0; band < mainFragmentCount; band++)
                {
                    List<Vector3> polygon = sourcePolygon;
                    if (band > 0)
                    {
                        polygon = ClipFracturePolygon(
                            polygon,
                            currentDefinition.BladeWidth,
                            crackNodes,
                            band - 1,
                            keepAbove: true);
                    }
                    if (band < crackCount && polygon.Count >= 3)
                    {
                        polygon = ClipFracturePolygon(
                            polygon,
                            currentDefinition.BladeWidth,
                            crackNodes,
                            band,
                            keepAbove: false);
                    }
                    if (polygon.Count < 3)
                    {
                        continue;
                    }
                    for (int vertex = 1; vertex < polygon.Count - 1; vertex++)
                    {
                        Vector3 a = polygon[0];
                        Vector3 b = polygon[vertex];
                        Vector3 c = polygon[vertex + 1];
                        Vector3 center = (a + b + c) / 3f;
                        int fragmentIndex = ResolveFractureBranchGroup(
                            center,
                            band,
                            mainFragmentCount,
                            currentDefinition.BladeWidth,
                            crackNodes,
                            branchSides,
                            branchAbove,
                            branchReach);
                        AddFractureTriangle(
                            fragmentVertices[fragmentIndex],
                            fragmentTriangles[fragmentIndex],
                            a,
                            b,
                            c);
                    }
                }
            }

            int created = 0;
            float separation = Lerp(random, 0.022f, 0.032f);
            for (int fragment = 0; fragment < groupCount; fragment++)
            {
                if (fragment >= mainFragmentCount &&
                    missingBranches[fragment - mainFragmentCount])
                {
                    continue;
                }
                if (fragmentTriangles[fragment].Count == 0)
                {
                    continue;
                }
                Mesh mesh = CreateMesh(
                    fragmentVertices[fragment],
                    fragmentTriangles[fragment]);
                GameObject piece = CreateDecoration(
                    blade.transform,
                    fragment < mainFragmentCount
                        ? $"{BladeFracturePrefix} Section {fragment + 1}"
                        : $"{BladeFracturePrefix} Branch " +
                            $"{fragment - mainFragmentCount + 1}",
                    mesh,
                    bladeMaterial,
                    ResolveBladeColor(currentDefinition.MetalFamily),
                    0.46f,
                    0.30f);
                if (fragment < mainFragmentCount)
                {
                    piece.transform.localPosition = new Vector3(
                        0f,
                        (fragment - crackCount * 0.5f) * separation,
                        0f);
                }
                else
                {
                    int cut = fragment - mainFragmentCount;
                    piece.transform.localPosition = new Vector3(
                        branchSides[cut] * Lerp(random, 0.006f, 0.012f),
                        (cut + 0.5f - crackCount * 0.5f) * separation +
                            (branchAbove[cut] ? 0.004f : -0.004f),
                        0f);
                }
                created++;
            }

            bladeRenderer.enabled = false;
            isBladeCracked = true;
            return created;
        }

        public void RestoreBlade()
        {
            GameObject blade = generatedParts.Find(part =>
                part != null && part.name == BladePartName);
            if (blade == null)
            {
                return;
            }
            ClearBladeFracture(blade);
            MeshRenderer renderer = blade.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
            isBladeCracked = false;
        }

        private static List<Vector3> ClipFracturePolygon(
            IReadOnlyList<Vector3> input,
            float bladeWidth,
            float[,] crackNodes,
            int cut,
            bool keepAbove)
        {
            var output = new List<Vector3>();
            if (input.Count == 0)
            {
                return output;
            }
            Vector3 previous = input[input.Count - 1];
            float previousDistance = FractureDistance(
                previous,
                bladeWidth,
                crackNodes,
                cut);
            bool previousInside = keepAbove
                ? previousDistance >= -0.000001f
                : previousDistance <= 0.000001f;
            for (int index = 0; index < input.Count; index++)
            {
                Vector3 current = input[index];
                float currentDistance = FractureDistance(
                    current,
                    bladeWidth,
                    crackNodes,
                    cut);
                bool currentInside = keepAbove
                    ? currentDistance >= -0.000001f
                    : currentDistance <= 0.000001f;
                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;
                    float t = Mathf.Abs(denominator) > 0.000001f
                        ? previousDistance / denominator
                        : 0.5f;
                    output.Add(Vector3.Lerp(previous, current, Mathf.Clamp01(t)));
                }
                if (currentInside)
                {
                    output.Add(current);
                }
                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }
            return output;
        }

        private static float FractureDistance(
            Vector3 point,
            float bladeWidth,
            float[,] crackNodes,
            int cut)
        {
            return point.y - EvaluateFractureBoundary(
                point.x,
                bladeWidth,
                crackNodes,
                cut);
        }

        private static float EvaluateFractureBoundary(
            float x,
            float bladeWidth,
            float[,] crackNodes,
            int cut)
        {
            int nodeCount = crackNodes.GetLength(1);
            float normalizedX = Mathf.Clamp(
                x / Mathf.Max(0.001f, bladeWidth * 0.58f),
                -1f,
                1f);
            float nodePosition = (normalizedX + 1f) * 0.5f *
                (nodeCount - 1);
            int lower = Mathf.Min(
                nodeCount - 2,
                Mathf.FloorToInt(nodePosition));
            return Mathf.Lerp(
                crackNodes[cut, lower],
                crackNodes[cut, lower + 1],
                nodePosition - lower);
        }

        private static int ResolveFractureBranchGroup(
            Vector3 center,
            int band,
            int mainFragmentCount,
            float bladeWidth,
            float[,] crackNodes,
            IReadOnlyList<int> branchSides,
            IReadOnlyList<bool> branchAbove,
            IReadOnlyList<float> branchReach)
        {
            for (int cut = 0; cut < branchSides.Count; cut++)
            {
                int branchBand = branchAbove[cut] ? cut + 1 : cut;
                if (band != branchBand)
                {
                    continue;
                }
                float sidePosition = center.x /
                    Mathf.Max(0.001f, bladeWidth * 0.5f) * branchSides[cut];
                if (sidePosition < -0.18f)
                {
                    continue;
                }
                float distance = Mathf.Abs(FractureDistance(
                    center,
                    bladeWidth,
                    crackNodes,
                    cut));
                float allowedDistance = branchReach[cut] *
                    Mathf.InverseLerp(-0.18f, 0.92f, sidePosition);
                if (distance <= allowedDistance)
                {
                    return mainFragmentCount + cut;
                }
            }
            return band;
        }

        private static void AddFractureTriangle(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        private void ClearBladeFracture(GameObject blade)
        {
            for (int index = blade.transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                GameObject child = blade.transform.GetChild(index).gameObject;
                if (!child.name.StartsWith(
                        BladeFracturePrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                Mesh mesh = child.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    generatedMeshes.Remove(mesh);
                    if (Application.isPlaying)
                    {
                        Destroy(mesh);
                    }
                    else
                    {
                        DestroyImmediate(mesh);
                    }
                }
                child.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        public static ProceduralShortSwordDefinition CreateDefinition(
            int seed)
        {
            var random = new System.Random(seed);
            float bladeLength = Lerp(random, 0.94f, 1.08f);
            float normalizedLength = Mathf.InverseLerp(
                0.94f,
                1.08f,
                bladeLength);
            float bladeWidth = Lerp(random, 0.074f, 0.112f);
            float handleLength = Lerp(
                random,
                0.205f + normalizedLength * 0.008f,
                0.250f + normalizedLength * 0.008f);
            ShortSwordBladeProfile bladeProfile =
                (ShortSwordBladeProfile)random.Next(0, 5);
            int directionSign = random.Next(0, 2) == 0 ? -1 : 1;
            bool directionalBlade = IsDirectionalBlade(bladeProfile);
            ShortSwordBladeBackStyle bladeBackStyle = directionalBlade
                ? (ShortSwordBladeBackStyle)random.Next(0, 3)
                : (random.Next(0, 2) == 0
                    ? ShortSwordBladeBackStyle.Clean
                    : ShortSwordBladeBackStyle.SteppedSpine);
            float normalizedBladeWidth = Mathf.InverseLerp(
                0.074f,
                0.112f,
                bladeWidth);
            ShortSwordGuardConstruction guardConstruction =
                SelectGuardConstruction(
                    random,
                    bladeProfile,
                    normalizedBladeWidth);
            ShortSwordGuardProfile guardProfile =
                ResolveGuardProfile(guardConstruction);
            ShortSwordHandleProfile handleProfile =
                (ShortSwordHandleProfile)random.Next(0, 3);
            ShortSwordHiltProfile hiltProfile =
                (ShortSwordHiltProfile)random.Next(0, 5);
            ShortSwordMetalFamily metalFamily =
                (ShortSwordMetalFamily)random.Next(0, 4);
            ShortSwordGripStyle gripStyle =
                (ShortSwordGripStyle)random.Next(0, 4);
            ShortSwordGripColor gripColor =
                (ShortSwordGripColor)random.Next(0, 5);
            ShortSwordOrnamentStyle ornamentStyle =
                SelectOrnamentStyle(random);
            ShortSwordGemFamily gemFamily =
                (ShortSwordGemFamily)random.Next(0, 4);
            ShortSwordGemCut gemCut =
                (ShortSwordGemCut)random.Next(0, 5);
            float handleRadius = Lerp(random, 0.027f, 0.032f);
            float handleTopRadius = ResolveHandleEndRadius(
                handleRadius,
                handleProfile,
                top: true);
            float handleBottomRadius = ResolveHandleEndRadius(
                handleRadius,
                handleProfile,
                top: false);
            float guardConnectionSize = handleTopRadius * 2f + 0.008f;
            float guardSpan = Mathf.Max(
                Lerp(random, 0.255f, 0.292f) +
                    normalizedBladeWidth * 0.070f,
                guardConnectionSize * 3.4f);
            float guardSectionBias = Lerp(random, -1f, 1f);
            int[] guardSideOptions = { 4, 6, 8, 10, 12 };
            int guardCrossSectionSides =
                guardSideOptions[random.Next(0, guardSideOptions.Length)];
            int[] guardCurveOptions = { 6, 8, 10, 12, 14 };
            int guardCurveSegments =
                guardCurveOptions[random.Next(0, guardCurveOptions.Length)];
            float guardCrossSectionRotation = Lerp(
                random,
                0f,
                Mathf.PI / guardCrossSectionSides);
            float guardHorizontalFactor = ResolveCrossSectionHorizontalFactor(
                guardCrossSectionSides,
                guardCrossSectionRotation);
            float guardHeight = Mathf.Clamp(
                ResolveGuardBaseHeight(
                    random,
                    guardConstruction,
                    normalizedBladeWidth) *
                    Mathf.Lerp(0.76f, 1.58f, (guardSectionBias + 1f) * 0.5f),
                0.014f,
                0.055f);
            float guardDepth = Mathf.Max(
                (Lerp(random, 0.050f, 0.069f) +
                    normalizedBladeWidth * 0.008f) *
                    Mathf.Lerp(1.20f, 0.86f, (guardSectionBias + 1f) * 0.5f),
                (handleTopRadius + 0.002f) * 2f / guardHorizontalFactor);
            if (ornamentStyle == ShortSwordOrnamentStyle.GuardGem)
            {
                bool hasJewelSocket = guardHeight >= 0.028f &&
                    guardSpan >= 0.300f &&
                    guardConstruction !=
                        ShortSwordGuardConstruction.DirectionalSweep &&
                    guardConstruction !=
                        ShortSwordGuardConstruction.OffsetLeaf;
                if (hasJewelSocket)
                {
                }
                else
                {
                    ornamentStyle = ShortSwordOrnamentStyle.Plain;
                }
            }

            return new ProceduralShortSwordDefinition
            {
                Seed = seed,
                BladeProfile = bladeProfile,
                BladeBackStyle = bladeBackStyle,
                GuardProfile = guardProfile,
                GuardConstruction = guardConstruction,
                HandleProfile = handleProfile,
                HiltProfile = hiltProfile,
                MetalFamily = metalFamily,
                GripStyle = gripStyle,
                GripColor = gripColor,
                OrnamentStyle = ornamentStyle,
                GemFamily = gemFamily,
                GemCut = gemCut,
                DirectionSign = directionSign,
                BladeLength = bladeLength,
                BladeWidth = bladeWidth,
                BladeThickness = Lerp(random, 0.026f, 0.034f),
                TipLength = Lerp(random, 0.18f, 0.285f),
                GuardSpan = guardSpan,
                GuardHeight = guardHeight,
                GuardDepth = guardDepth,
                GuardCurveSegments = guardCurveSegments,
                GuardCrossSectionSides = guardCrossSectionSides,
                GuardCrossSectionRotation = guardCrossSectionRotation,
                HandleLength = handleLength,
                HandleRadius = handleRadius,
                HiltLength = Lerp(random, 0.066f, 0.096f),
                HiltRadius = Lerp(
                    random,
                    handleBottomRadius + 0.010f,
                    handleBottomRadius + 0.021f)
            };
        }

        private static ShortSwordOrnamentStyle SelectOrnamentStyle(
            System.Random random)
        {
            int roll = random.Next(0, 100);
            if (roll < 91)
            {
                return ShortSwordOrnamentStyle.Plain;
            }
            if (roll < 95)
            {
                return ShortSwordOrnamentStyle.GuardGem;
            }
            return ShortSwordOrnamentStyle.PommelGem;
        }

        private static bool IsDirectionalBlade(
            ShortSwordBladeProfile profile)
        {
            return profile == ShortSwordBladeProfile.ForwardSwept ||
                profile == ShortSwordBladeProfile.ClipPoint;
        }

        private static ShortSwordGuardConstruction SelectGuardConstruction(
            System.Random random,
            ShortSwordBladeProfile bladeProfile,
            float normalizedBladeWidth)
        {
            ShortSwordGuardConstruction[] candidates;
            if (IsDirectionalBlade(bladeProfile))
            {
                candidates = new[]
                {
                    ShortSwordGuardConstruction.DirectionalSweep,
                    ShortSwordGuardConstruction.OffsetLeaf,
                    ShortSwordGuardConstruction.BladeQuillons,
                    ShortSwordGuardConstruction.WingedW,
                    ShortSwordGuardConstruction.Crescent
                };
            }
            else if (normalizedBladeWidth > 0.68f)
            {
                candidates = new[]
                {
                    ShortSwordGuardConstruction.WingedW,
                    ShortSwordGuardConstruction.Crescent,
                    ShortSwordGuardConstruction.RazorBar,
                    ShortSwordGuardConstruction.BladeQuillons
                };
            }
            else
            {
                candidates = new[]
                {
                    ShortSwordGuardConstruction.RazorBar,
                    ShortSwordGuardConstruction.BladeQuillons,
                    ShortSwordGuardConstruction.Crescent,
                    ShortSwordGuardConstruction.WingedW
                };
            }
            return candidates[random.Next(0, candidates.Length)];
        }

        private static ShortSwordGuardProfile ResolveGuardProfile(
            ShortSwordGuardConstruction construction)
        {
            return construction switch
            {
                ShortSwordGuardConstruction.BladeQuillons =>
                    ShortSwordGuardProfile.Upswept,
                ShortSwordGuardConstruction.WingedW =>
                    ShortSwordGuardProfile.HookedQuillons,
                ShortSwordGuardConstruction.Crescent =>
                    ShortSwordGuardProfile.Bowed,
                ShortSwordGuardConstruction.DirectionalSweep =>
                    ShortSwordGuardProfile.Slanted,
                ShortSwordGuardConstruction.OffsetLeaf =>
                    ShortSwordGuardProfile.OffsetQuillons,
                _ => ShortSwordGuardProfile.Straight
            };
        }

        private static float ResolveGuardBaseHeight(
            System.Random random,
            ShortSwordGuardConstruction construction,
            float bladeMass)
        {
            float baseHeight = construction switch
            {
                ShortSwordGuardConstruction.RazorBar =>
                    Lerp(random, 0.016f, 0.020f),
                ShortSwordGuardConstruction.BladeQuillons =>
                    Lerp(random, 0.017f, 0.022f),
                ShortSwordGuardConstruction.WingedW =>
                    Lerp(random, 0.018f, 0.023f),
                ShortSwordGuardConstruction.Crescent =>
                    Lerp(random, 0.020f, 0.026f),
                ShortSwordGuardConstruction.DirectionalSweep =>
                    Lerp(random, 0.016f, 0.021f),
                _ => Lerp(random, 0.017f, 0.022f)
            };
            float massScale = construction switch
            {
                ShortSwordGuardConstruction.WingedW => 0.010f,
                ShortSwordGuardConstruction.Crescent => 0.010f,
                ShortSwordGuardConstruction.RazorBar => 0.006f,
                _ => 0.007f
            };
            return baseHeight + bladeMass * massScale;
        }

        private GameObject CreatePart(
            string partName,
            Mesh mesh,
            Material material,
            Color color,
            float metallic,
            float smoothness)
        {
            mesh.name = $"Procedural {partName} {currentDefinition.Seed}";
            generatedMeshes.Add(mesh);

            GameObject part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            ApplyRendererProperties(
                renderer,
                color,
                metallic,
                smoothness);
            generatedParts.Add(part);
            return part;
        }

        private void CreateGuardDetails(
            GameObject guard,
            ProceduralShortSwordDefinition definition)
        {
            if (definition.OrnamentStyle ==
                ShortSwordOrnamentStyle.GuardGem)
            {
                CreateMirroredGuardGem(
                    guard,
                    definition,
                    ResolveGuardGemRadii(definition));
            }
        }

        private void CreateMirroredGuardGem(
            GameObject guard,
            ProceduralShortSwordDefinition definition,
            Vector3 radii)
        {
            float surfaceDepth = definition.GuardDepth * 0.5f *
                ResolveCrossSectionHorizontalFactor(
                    definition.GuardCrossSectionSides,
                    definition.GuardCrossSectionRotation);
            for (int face = -1; face <= 1; face += 2)
            {
                Vector3 center = new Vector3(
                    0f,
                    ResolveGuardCenterHeight(
                        definition,
                        0f),
                    face * (surfaceDepth + 0.0003f));
                CreateDecoration(
                    guard.transform,
                    $"{(face > 0 ? "Front" : "Rear")} Guard Jewel",
                    BuildGemMesh(
                        center,
                        radii,
                        definition.GemCut,
                        face),
                    guardMaterial,
                    ResolveGemColor(definition.GemFamily),
                    0.10f,
                    0.62f);
            }
        }

        private static Vector3 ResolveGuardGemRadii(
            ProceduralShortSwordDefinition definition)
        {
            float safeHeight = definition.GuardHeight * 0.22f;
            return definition.GemCut switch
            {
                ShortSwordGemCut.Emerald => new Vector3(
                    Mathf.Min(0.016f, safeHeight * 1.35f),
                    safeHeight * 0.64f,
                    0.003f),
                ShortSwordGemCut.Oval => new Vector3(
                    safeHeight * 0.72f,
                    safeHeight,
                    0.003f),
                ShortSwordGemCut.Pear => new Vector3(
                    safeHeight * 0.78f,
                    safeHeight,
                    0.003f),
                _ => new Vector3(
                    safeHeight * 0.82f,
                    safeHeight * 0.82f,
                    0.003f)
            };
        }

        private void CreateHandleDetails(
            GameObject handle,
            ProceduralShortSwordDefinition definition)
        {
            Color grip = ResolveGripColor(definition.GripColor);
            Color detail = definition.GripStyle ==
                    ShortSwordGripStyle.RibbedWood
                ? Color.Lerp(grip, new Color(0.055f, 0.035f, 0.022f), 0.42f)
                : Color.Lerp(grip, Color.black, 0.32f);
            float top = ResolveHandleSeatHeight(definition) - 0.018f;
            float bottom = -definition.HandleLength + 0.018f;

            switch (definition.GripStyle)
            {
                case ShortSwordGripStyle.CrossWrappedCord:
                    CreateDecoration(
                        handle.transform,
                        "Cord Wrap",
                        BuildHelixMesh(definition, clockwise: true),
                        handleMaterial,
                        Color.Lerp(grip, new Color(0.72f, 0.60f, 0.42f), 0.42f),
                        0f,
                        0.12f);
                    break;
                case ShortSwordGripStyle.StuddedLeather:
                    for (int index = 0; index < 6; index++)
                    {
                        float t = (index + 1f) / 7f;
                        float angle = index * Mathf.PI * 0.72f;
                        float surfaceRadius = ResolveHandleSurfaceRadius(
                            definition,
                            t);
                        Vector3 radial = new Vector3(
                            Mathf.Cos(angle),
                            0f,
                            Mathf.Sin(angle)) *
                            (surfaceRadius + 0.004f);
                        Vector3 center = radial;
                        center.y = Mathf.Lerp(top, bottom, t);
                        CreateDecoration(
                            handle.transform,
                            $"Grip Stud {index + 1}",
                            BuildOctahedron(
                                center,
                                new Vector3(0.0065f, 0.0065f, 0.0065f)),
                            guardMaterial,
                            ResolveMetalAccentColor(definition.MetalFamily),
                            ResolveMetallic(definition.MetalFamily),
                            0.24f);
                    }
                    break;
                default:
                    int bandCount = definition.GripStyle ==
                            ShortSwordGripStyle.LeatherBands
                        ? 7
                        : 5;
                    for (int index = 0; index < bandCount; index++)
                    {
                        float t = (index + 0.5f) / bandCount;
                        CreateDecoration(
                            handle.transform,
                            $"Grip Band {index + 1}",
                            BuildBandMesh(
                                Mathf.Lerp(top, bottom, t),
                                ResolveHandleSurfaceRadius(
                                    definition,
                                    t) + 0.003f,
                                definition.GripStyle ==
                                    ShortSwordGripStyle.LeatherBands
                                        ? 0.010f
                                        : 0.007f),
                            handleMaterial,
                            detail,
                            0f,
                            0.10f);
                    }
                    break;
            }
        }

        private void CreateHiltDetails(
            GameObject hilt,
            ProceduralShortSwordDefinition definition)
        {
            float top = -definition.HandleLength;
            if (definition.OrnamentStyle !=
                ShortSwordOrnamentStyle.PommelGem)
            {
                return;
            }
            Vector3 gemCenter = new Vector3(
                definition.HiltProfile == ShortSwordHiltProfile.Hooked
                    ? 0.060f
                    : 0f,
                top - definition.HiltLength - 0.008f,
                0f);
            CreateDecoration(
                hilt.transform,
                "Pommel Jewel",
                BuildGemMesh(
                    gemCenter,
                    new Vector3(0.017f, 0.021f, 0.012f),
                    definition.GemCut,
                    1f),
                hiltMaterial,
                ResolveGemColor(definition.GemFamily),
                0.10f,
                0.62f);
        }

        private GameObject CreateDecoration(
            Transform parent,
            string decorationName,
            Mesh mesh,
            Material material,
            Color color,
            float metallic,
            float smoothness)
        {
            mesh.name = $"Procedural {decorationName} {currentDefinition.Seed}";
            generatedMeshes.Add(mesh);
            GameObject decoration = new GameObject(decorationName);
            decoration.transform.SetParent(parent, false);
            MeshFilter filter = decoration.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = decoration.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            ApplyRendererProperties(
                renderer,
                color,
                metallic,
                smoothness);
            return decoration;
        }

        private static void ApplyRendererProperties(
            Renderer renderer,
            Color color,
            float metallic,
            float smoothness)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetFloat("_Metallic", metallic);
            properties.SetFloat("_Smoothness", smoothness);
            renderer.SetPropertyBlock(properties);
        }

        public static Color ResolveMetalColor(ShortSwordMetalFamily family)
        {
            return family switch
            {
                ShortSwordMetalFamily.Bronze => new Color(0.46f, 0.29f, 0.13f),
                ShortSwordMetalFamily.Silver => new Color(0.66f, 0.69f, 0.70f),
                ShortSwordMetalFamily.BlackenedSteel => new Color(0.13f, 0.15f, 0.16f),
                _ => new Color(0.34f, 0.36f, 0.36f)
            };
        }

        public static Color ResolveBladeColor(ShortSwordMetalFamily family)
        {
            return family switch
            {
                ShortSwordMetalFamily.Bronze =>
                    new Color(0.51f, 0.50f, 0.45f),
                ShortSwordMetalFamily.Silver =>
                    new Color(0.66f, 0.69f, 0.68f),
                ShortSwordMetalFamily.BlackenedSteel =>
                    new Color(0.43f, 0.45f, 0.45f),
                _ => new Color(0.56f, 0.58f, 0.57f)
            };
        }

        public static Color ResolveGripColor(ShortSwordGripColor color)
        {
            return color switch
            {
                ShortSwordGripColor.OxBlood => new Color(0.27f, 0.065f, 0.045f),
                ShortSwordGripColor.Charcoal => new Color(0.075f, 0.080f, 0.078f),
                ShortSwordGripColor.WornTan => new Color(0.42f, 0.27f, 0.13f),
                ShortSwordGripColor.ForestGreen => new Color(0.105f, 0.19f, 0.125f),
                _ => new Color(0.19f, 0.095f, 0.045f)
            };
        }

        public static Color ResolveGemColor(ShortSwordGemFamily family)
        {
            return family switch
            {
                ShortSwordGemFamily.Emerald => new Color(0.06f, 0.52f, 0.26f),
                ShortSwordGemFamily.Sapphire => new Color(0.055f, 0.23f, 0.62f),
                ShortSwordGemFamily.Amber => new Color(0.78f, 0.39f, 0.055f),
                _ => new Color(0.64f, 0.045f, 0.075f)
            };
        }

        private static Color ResolveMetalAccentColor(
            ShortSwordMetalFamily family)
        {
            return Color.Lerp(
                ResolveMetalColor(family),
                family == ShortSwordMetalFamily.Bronze
                    ? new Color(0.86f, 0.61f, 0.24f)
                    : Color.white,
                0.28f);
        }

        private static float ResolveMetallic(ShortSwordMetalFamily family)
        {
            return family == ShortSwordMetalFamily.BlackenedSteel
                ? 0.58f
                : 0.74f;
        }

        private void ClearGeneratedSword()
        {
            var partsToDestroy = new HashSet<GameObject>();
            foreach (GameObject part in generatedParts)
            {
                if (part != null)
                {
                    partsToDestroy.Add(part);
                }
            }

            // The runtime tracking lists are intentionally not serialized. If
            // a play-mode/domain transition leaves generated children behind,
            // find them by their stable top-level part names as well.
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                GameObject child = transform.GetChild(index).gameObject;
                if (IsGeneratedPartName(child.name))
                {
                    partsToDestroy.Add(child);
                }
            }

            foreach (GameObject part in partsToDestroy)
            {
                // Destroy is deferred in play mode. Hiding first prevents the
                // outgoing sword from z-fighting with its replacement frame.
                part.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(part);
                }
                else
                {
                    DestroyImmediate(part);
                }
            }
            generatedParts.Clear();

            for (int index = generatedMeshes.Count - 1;
                 index >= 0;
                 index--)
            {
                Mesh mesh = generatedMeshes[index];
                if (mesh == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(mesh);
                }
                else
                {
                    DestroyImmediate(mesh);
                }
            }
            generatedMeshes.Clear();
            hasGeneratedSword = false;
            isBladeCracked = false;
            fractureRevision = 0;
            mainFractureCount = 0;
            missingFracturePieceCount = 0;
            minimumFractureSegmentRise = 0f;
        }

        private static bool IsGeneratedPartName(string objectName)
        {
            return objectName == BladePartName ||
                objectName == GuardPartName ||
                objectName == HandlePartName ||
                objectName == HiltPartName;
        }

        private static Mesh BuildBladeMesh(
            ProceduralShortSwordDefinition definition)
        {
            float halfWidth = definition.BladeWidth * 0.5f;
            float baseHeight = ResolveBladeSeatHeightAtX(definition, 0f);
            float facetedLength = definition.BladeLength - baseHeight;
            int segments = Mathf.Max(
                8,
                Mathf.CeilToInt(facetedLength / TargetFacetLength));
            var vertices = new List<Vector3>(segments * 4 + 1);
            var triangles = new List<int>(segments * 24 + 12);

            for (int ring = 0; ring < segments; ring++)
            {
                float t = ring / (float)segments;
                float y = Mathf.Lerp(baseHeight, definition.BladeLength, t);
                float width = ResolveBladeHalfWidthAtHeight(
                    definition,
                    y,
                    halfWidth);
                ResolveBladeEdgeWidths(
                    definition,
                    y,
                    width,
                    out float leftWidth,
                    out float rightWidth);
                float ridgeDepth = definition.BladeThickness * 0.5f *
                    Mathf.Clamp01(width / Mathf.Max(0.0001f, halfWidth));
                float centerY = ring == 0
                    ? ResolveBladeSeatHeightAtX(definition, 0f)
                    : y;
                float rightY = ring == 0
                    ? ResolveBladeSeatHeightAtX(definition, rightWidth)
                    : y;
                float leftY = ring == 0
                    ? ResolveBladeSeatHeightAtX(definition, -leftWidth)
                    : y;
                vertices.Add(new Vector3(0f, centerY, ridgeDepth));
                vertices.Add(new Vector3(rightWidth, rightY, 0f));
                vertices.Add(new Vector3(0f, centerY, -ridgeDepth));
                vertices.Add(new Vector3(-leftWidth, leftY, 0f));
            }

            for (int ring = 0; ring < segments - 1; ring++)
            {
                int current = ring * 4;
                int nextRing = (ring + 1) * 4;
                for (int side = 0; side < 4; side++)
                {
                    int nextSide = (side + 1) % 4;
                    AddQuad(
                        triangles,
                        current + side,
                        current + nextSide,
                        nextRing + nextSide,
                        nextRing + side);
                }
            }

            int tip = vertices.Count;
            vertices.Add(new Vector3(
                ResolveBladeTipOffset(definition, halfWidth),
                definition.BladeLength,
                0f));
            int lastRing = (segments - 1) * 4;
            for (int side = 0; side < 4; side++)
            {
                int nextSide = (side + 1) % 4;
                triangles.Add(lastRing + side);
                triangles.Add(lastRing + nextSide);
                triangles.Add(tip);
            }

            int baseCenter = vertices.Count;
            vertices.Add(new Vector3(0f, baseHeight, 0f));
            for (int side = 0; side < 4; side++)
            {
                int nextSide = (side + 1) % 4;
                triangles.Add(baseCenter);
                triangles.Add(nextSide);
                triangles.Add(side);
            }
            return CreateMesh(vertices, triangles);
        }

        private static float ResolveBladeHalfWidthAtHeight(
            ProceduralShortSwordDefinition definition,
            float height,
            float halfWidth)
        {
            float taperMultiplier = definition.BladeProfile ==
                ShortSwordBladeProfile.LongTaper
                    ? 1.35f
                    : 1f;
            float taperStart = definition.BladeLength -
                definition.TipLength * taperMultiplier;
            if (height <= taperStart)
            {
                float baseBlend = Mathf.InverseLerp(
                    ResolveBladeSeatHeightAtX(definition, 0f),
                    0.075f,
                    height);
                return halfWidth * Mathf.Lerp(0.94f, 1f, baseBlend);
            }

            float taper = Mathf.InverseLerp(
                taperStart,
                definition.BladeLength,
                height);
            float remaining = definition.BladeProfile ==
                ShortSwordBladeProfile.RoundedShoulder
                    ? 1f - Mathf.SmoothStep(0f, 1f, taper)
                    : 1f - taper;
            return halfWidth * Mathf.Clamp01(remaining);
        }

        private static void ResolveBladeEdgeWidths(
            ProceduralShortSwordDefinition definition,
            float height,
            float baseWidth,
            out float leftWidth,
            out float rightWidth)
        {
            float t = Mathf.InverseLerp(0f, definition.BladeLength, height);
            leftWidth = baseWidth;
            rightWidth = baseWidth;
            switch (definition.BladeProfile)
            {
                case ShortSwordBladeProfile.ForwardSwept:
                    leftWidth *= Mathf.Lerp(0.94f, 0.68f, t);
                    rightWidth *= Mathf.Lerp(1.02f, 1.24f, t);
                    break;
                case ShortSwordBladeProfile.ClipPoint:
                    leftWidth *= t < 0.68f
                        ? 1.02f
                        : Mathf.Lerp(1.02f, 0.54f, Mathf.InverseLerp(0.68f, 1f, t));
                    rightWidth *= Mathf.Lerp(1f, 1.10f, t);
                    break;
            }

            if (definition.DirectionSign < 0 &&
                IsDirectionalBlade(definition.BladeProfile))
            {
                (leftWidth, rightWidth) = (rightWidth, leftWidth);
            }

            if (t < 0.18f || t > 0.70f)
            {
                return;
            }
            float backT = Mathf.InverseLerp(0.18f, 0.70f, t);
            switch (definition.BladeBackStyle)
            {
                case ShortSwordBladeBackStyle.Sawback:
                    int tooth = Mathf.FloorToInt(backT * 9f);
                    leftWidth *= tooth % 2 == 0 ? 0.72f : 1.02f;
                    break;
                case ShortSwordBladeBackStyle.SteppedSpine:
                    int step = Mathf.Min(3, Mathf.FloorToInt(backT * 4f));
                    leftWidth *= 1f - step * 0.055f;
                    break;
            }
        }

        private static float ResolveBladeTipOffset(
            ProceduralShortSwordDefinition definition,
            float halfWidth)
        {
            return definition.BladeProfile switch
            {
                ShortSwordBladeProfile.ForwardSwept =>
                    halfWidth * 0.44f * definition.DirectionSign,
                ShortSwordBladeProfile.ClipPoint =>
                    halfWidth * 0.18f * definition.DirectionSign,
                _ => 0f
            };
        }

        private static Mesh BuildGuardMesh(
            ProceduralShortSwordDefinition definition)
        {
            int segments = Mathf.Clamp(definition.GuardCurveSegments, 6, 14);
            int crossSectionVertices = Mathf.Clamp(
                definition.GuardCrossSectionSides,
                4,
                12);
            var vertices = new List<Vector3>(
                (segments + 1) * crossSectionVertices + 2);
            var triangles = new List<int>(
                segments * crossSectionVertices * 6 +
                crossSectionVertices * 6);
            float leftSpan = ResolveGuardSideSpan(definition, negative: true);
            float rightSpan = ResolveGuardSideSpan(definition, negative: false);
            float bladeMass = Mathf.InverseLerp(
                0.074f,
                0.112f,
                definition.BladeWidth);

            for (int index = 0; index <= segments; index++)
            {
                float x = index <= segments / 2
                    ? Mathf.Lerp(
                        -leftSpan,
                        0f,
                        index / (segments * 0.5f))
                    : Mathf.Lerp(
                        0f,
                        rightSpan,
                        (index - segments * 0.5f) / (segments * 0.5f));
                float normalizedX = x < 0f
                    ? x / leftSpan
                    : x / rightSpan;
                float edge = Mathf.Abs(normalizedX);
                float centerY = ResolveGuardCenterHeight(
                    definition,
                    normalizedX);
                float tipScale = ResolveGuardTipScale(
                    definition,
                    normalizedX,
                    bladeMass);
                float taper = Mathf.Lerp(
                    1f,
                    tipScale,
                    Mathf.Pow(
                        edge,
                        ResolveGuardTaperExponent(
                            definition.GuardConstruction)));
                float halfHeight = definition.GuardHeight * 0.5f * taper;
                float halfDepth = definition.GuardDepth * 0.5f *
                    Mathf.Lerp(1f, 0.72f, edge);
                for (int side = 0; side < crossSectionVertices; side++)
                {
                    float angle = definition.GuardCrossSectionRotation +
                        side / (float)crossSectionVertices * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        x,
                        centerY + Mathf.Cos(angle) * halfHeight,
                        Mathf.Sin(angle) * halfDepth));
                }
            }

            for (int index = 0; index < segments; index++)
            {
                int current = index * crossSectionVertices;
                int next = (index + 1) * crossSectionVertices;
                for (int face = 0; face < crossSectionVertices; face++)
                {
                    int nextFace = (face + 1) % crossSectionVertices;
                    AddQuad(
                        triangles,
                        current + face,
                        current + nextFace,
                        next + nextFace,
                        next + face);
                }
            }

            int leftCenter = vertices.Count;
            vertices.Add(new Vector3(
                -leftSpan,
                ResolveGuardCenterHeight(
                    definition,
                    -1f),
                0f));
            int rightCenter = vertices.Count;
            vertices.Add(new Vector3(
                rightSpan,
                ResolveGuardCenterHeight(
                    definition,
                    1f),
                0f));
            int rightStart = segments * crossSectionVertices;
            for (int face = 0; face < crossSectionVertices; face++)
            {
                int nextFace = (face + 1) % crossSectionVertices;
                triangles.Add(leftCenter);
                triangles.Add(nextFace);
                triangles.Add(face);
                triangles.Add(rightCenter);
                triangles.Add(rightStart + face);
                triangles.Add(rightStart + nextFace);
            }
            return CreateMesh(vertices, triangles);
        }

        private static float ResolveGuardSideSpan(
            ProceduralShortSwordDefinition definition,
            bool negative)
        {
            float halfSpan = definition.GuardSpan * 0.5f;
            bool directionSide = negative
                ? definition.DirectionSign < 0
                : definition.DirectionSign > 0;
            return definition.GuardConstruction switch
            {
                ShortSwordGuardConstruction.DirectionalSweep => halfSpan *
                    (directionSide ? 1.05f : 0.94f),
                ShortSwordGuardConstruction.OffsetLeaf => halfSpan *
                    (directionSide ? 1.08f : 0.88f),
                _ => halfSpan
            };
        }

        private static float ResolveGuardTipScale(
            ProceduralShortSwordDefinition definition,
            float normalizedX,
            float bladeMass)
        {
            float sideDirection = Mathf.Sign(normalizedX) *
                definition.DirectionSign;
            return definition.GuardConstruction switch
            {
                ShortSwordGuardConstruction.RazorBar =>
                    Mathf.Lerp(0.08f, 0.18f, bladeMass),
                ShortSwordGuardConstruction.BladeQuillons =>
                    Mathf.Lerp(0.05f, 0.16f, bladeMass),
                ShortSwordGuardConstruction.WingedW =>
                    Mathf.Lerp(0.10f, 0.20f, bladeMass),
                ShortSwordGuardConstruction.Crescent =>
                    Mathf.Lerp(0.22f, 0.39f, bladeMass),
                ShortSwordGuardConstruction.DirectionalSweep =>
                    sideDirection > 0f
                        ? Mathf.Lerp(0.06f, 0.16f, bladeMass)
                        : Mathf.Lerp(0.14f, 0.25f, bladeMass),
                ShortSwordGuardConstruction.OffsetLeaf =>
                    sideDirection > 0f
                        ? Mathf.Lerp(0.06f, 0.17f, bladeMass)
                        : Mathf.Lerp(0.20f, 0.32f, bladeMass),
                _ => 0.20f
            };
        }

        private static float ResolveGuardCenterHeight(
            ProceduralShortSwordDefinition definition,
            float normalizedX)
        {
            normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
            float edge = Mathf.Abs(normalizedX);
            return definition.GuardConstruction switch
            {
                ShortSwordGuardConstruction.BladeQuillons =>
                    0.018f * Mathf.Pow(edge, 1.45f),
                ShortSwordGuardConstruction.WingedW =>
                    edge < 0.58f
                        ? Mathf.Lerp(
                            -0.008f,
                            0.024f,
                            Mathf.SmoothStep(0f, 1f, edge / 0.58f))
                        : Mathf.Lerp(
                            0.024f,
                            0.005f,
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                Mathf.InverseLerp(0.58f, 1f, edge))),
                ShortSwordGuardConstruction.Crescent =>
                    0.010f * (1f - Mathf.Pow(edge, 1.3f)) -
                    0.013f * Mathf.Pow(edge, 1.65f),
                ShortSwordGuardConstruction.DirectionalSweep =>
                    -normalizedX * definition.DirectionSign * 0.018f +
                    ResolveDirectionalTipHook(
                        normalizedX,
                        definition.DirectionSign,
                        0.024f,
                        0.032f),
                ShortSwordGuardConstruction.OffsetLeaf =>
                    (normalizedX * definition.DirectionSign > 0f
                        ? -0.026f * Mathf.Pow(edge, 1.35f)
                        : 0.013f * Mathf.Pow(edge, 1.55f)) +
                    ResolveDirectionalTipHook(
                        normalizedX,
                        definition.DirectionSign,
                        0.027f,
                        0.036f),
                _ => 0f
            };
        }

        private static float ResolveDirectionalTipHook(
            float normalizedX,
            int directionSign,
            float directionalRise,
            float counterRise)
        {
            float edge = Mathf.Abs(normalizedX);
            float hook = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.58f, 1f, edge));
            bool directionSide = Mathf.Sign(normalizedX) * directionSign > 0f;
            return hook * (directionSide ? directionalRise : counterRise);
        }

        public static float ResolveGuardTopAtCenter(
            ProceduralShortSwordDefinition definition)
        {
            return ResolveGuardCenterHeight(definition, 0f) +
                definition.GuardHeight * 0.5f *
                ResolveCrossSectionVerticalFactor(definition, top: true);
        }

        public static float ResolveGuardBottomAtCenter(
            ProceduralShortSwordDefinition definition)
        {
            return ResolveGuardCenterHeight(definition, 0f) -
                definition.GuardHeight * 0.5f *
                ResolveCrossSectionVerticalFactor(definition, top: false);
        }

        public static float ResolveBladeSeatHeightAtX(
            ProceduralShortSwordDefinition definition,
            float x)
        {
            ResolveGuardVerticalEnvelopeAtX(
                definition,
                x,
                out float bottom,
                out float top);
            float clearance = Mathf.Min(0.0015f, (top - bottom) * 0.12f);
            return Mathf.Lerp(
                bottom + clearance,
                top - clearance,
                0.38f);
        }

        public static float ResolveHandleSeatHeight(
            ProceduralShortSwordDefinition definition)
        {
            return ResolveHandleSeatHeightAtX(definition, 0f);
        }

        public static float ResolveHandleSeatHeightAtX(
            ProceduralShortSwordDefinition definition,
            float x)
        {
            ResolveGuardVerticalEnvelopeAtX(
                definition,
                x,
                out float bottom,
                out float top);
            float clearance = Mathf.Min(0.0015f, (top - bottom) * 0.12f);
            return Mathf.Lerp(
                bottom + clearance,
                top - clearance,
                0.68f);
        }

        public static void ResolveGuardVerticalEnvelopeAtX(
            ProceduralShortSwordDefinition definition,
            float x,
            out float bottom,
            out float top)
        {
            float leftSpan = ResolveGuardSideSpan(definition, negative: true);
            float rightSpan = ResolveGuardSideSpan(definition, negative: false);
            float normalizedX = x < 0f
                ? Mathf.Clamp(x / leftSpan, -1f, 0f)
                : Mathf.Clamp(x / rightSpan, 0f, 1f);
            float edge = Mathf.Abs(normalizedX);
            float bladeMass = Mathf.InverseLerp(
                0.074f,
                0.112f,
                definition.BladeWidth);
            float tipScale = ResolveGuardTipScale(
                definition,
                normalizedX,
                bladeMass);
            float taper = Mathf.Lerp(
                1f,
                tipScale,
                Mathf.Pow(
                    edge,
                    ResolveGuardTaperExponent(
                        definition.GuardConstruction)));
            float halfHeight = definition.GuardHeight * 0.5f * taper;
            float center = ResolveGuardCenterHeight(definition, normalizedX);
            bottom = center - halfHeight *
                ResolveCrossSectionVerticalFactor(definition, top: false);
            top = center + halfHeight *
                ResolveCrossSectionVerticalFactor(definition, top: true);
        }

        private static float ResolveCrossSectionVerticalFactor(
            ProceduralShortSwordDefinition definition,
            bool top)
        {
            int sides = Mathf.Clamp(definition.GuardCrossSectionSides, 4, 12);
            float extreme = top ? float.NegativeInfinity : float.PositiveInfinity;
            for (int side = 0; side < sides; side++)
            {
                float angle = definition.GuardCrossSectionRotation +
                    side / (float)sides * Mathf.PI * 2f;
                float value = Mathf.Cos(angle);
                extreme = top
                    ? Mathf.Max(extreme, value)
                    : Mathf.Min(extreme, value);
            }
            return top ? extreme : -extreme;
        }

        private static float ResolveCrossSectionHorizontalFactor(
            int sideCount,
            float rotation)
        {
            int sides = Mathf.Clamp(sideCount, 4, 12);
            float extreme = 0f;
            for (int side = 0; side < sides; side++)
            {
                float angle = rotation +
                    side / (float)sides * Mathf.PI * 2f;
                extreme = Mathf.Max(extreme, Mathf.Abs(Mathf.Sin(angle)));
            }
            return Mathf.Max(0.01f, extreme);
        }

        private static float ResolveGuardTaperExponent(
            ShortSwordGuardConstruction construction)
        {
            return construction switch
            {
                ShortSwordGuardConstruction.RazorBar => 1.10f,
                ShortSwordGuardConstruction.BladeQuillons => 0.92f,
                ShortSwordGuardConstruction.WingedW => 1.35f,
                ShortSwordGuardConstruction.Crescent => 1.70f,
                ShortSwordGuardConstruction.DirectionalSweep => 1.15f,
                ShortSwordGuardConstruction.OffsetLeaf => 1.05f,
                _ => 1.4f
            };
        }

        private static Mesh BuildHandleMesh(
            ProceduralShortSwordDefinition definition)
        {
            float top = ResolveHandleSeatHeight(definition);
            float bottom = -definition.HandleLength;
            var rings = new List<Vector2>();
            const int ringCount = 7;
            for (int index = 0; index < ringCount; index++)
            {
                float t = index / (ringCount - 1f);
                rings.Add(new Vector2(
                    Mathf.Lerp(top, bottom, t),
                    ResolveHandleSurfaceRadius(definition, t, index)));
            }
            return BuildRevolvedMesh(rings, 8, definition);
        }

        private static Mesh BuildHiltMesh(
            ProceduralShortSwordDefinition definition)
        {
            float top = -definition.HandleLength;
            float bottom = top - definition.HiltLength;
            float radius = definition.HiltRadius;
            float connectionRadius = ResolveHiltConnectionRadius(
                definition);
            if (definition.HiltProfile == ShortSwordHiltProfile.Hooked)
            {
                var hookedRings = new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.20f), radius * 0.82f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.40f), radius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.60f), radius * 0.96f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.80f), radius * 0.76f),
                    new Vector2(bottom, radius * 0.48f)
                };
                var centers = new List<Vector2>
                {
                    Vector2.zero,
                    new Vector2(0.002f, 0f),
                    new Vector2(0.009f, 0f),
                    new Vector2(0.022f, 0f),
                    new Vector2(0.040f, 0f),
                    new Vector2(0.060f, 0f)
                };
                return BuildRevolvedMesh(hookedRings, centers, 7);
            }
            List<Vector2> rings = definition.HiltProfile switch
            {
                ShortSwordHiltProfile.Disc => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.22f), radius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.70f), radius),
                    new Vector2(bottom, radius * 0.70f)
                },
                ShortSwordHiltProfile.ScentStopper => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.34f), radius * 0.90f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.76f), radius),
                    new Vector2(bottom, radius * 0.34f)
                },
                ShortSwordHiltProfile.Crowned => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.20f), radius * 0.78f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.44f), radius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.68f), radius * 0.82f),
                    new Vector2(bottom, radius * 0.58f)
                },
                _ => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.28f), radius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.72f), radius * 0.88f),
                    new Vector2(bottom, radius * 0.48f)
                }
            };
            int sides = definition.HiltProfile == ShortSwordHiltProfile.Faceted
                ? 6
                : 8;
            return BuildRevolvedMesh(rings, sides);
        }

        public static float ResolveHandleEndRadius(
            float handleRadius,
            ShortSwordHandleProfile profile,
            bool top)
        {
            float multiplier = profile switch
            {
                ShortSwordHandleProfile.Tapered => top ? 1.06f : 0.88f,
                ShortSwordHandleProfile.Waisted => 1.03f,
                _ => 1f
            };
            return handleRadius * multiplier;
        }

        public static float ResolveHiltConnectionRadius(
            ProceduralShortSwordDefinition definition)
        {
            return ResolveHandleEndRadius(
                    definition.HandleRadius,
                    definition.HandleProfile,
                    top: false) +
                0.003f;
        }

        private static Mesh BuildBandMesh(
            float centerY,
            float radius,
            float height)
        {
            float halfHeight = height * 0.5f;
            var rings = new List<Vector2>
            {
                new Vector2(centerY + halfHeight, radius * 0.92f),
                new Vector2(centerY + halfHeight * 0.64f, radius),
                new Vector2(centerY - halfHeight * 0.64f, radius),
                new Vector2(centerY - halfHeight, radius * 0.92f)
            };
            return BuildRevolvedMesh(rings, 8);
        }

        private static Mesh BuildOctahedron(
            Vector3 center,
            Vector3 radii)
        {
            var vertices = new List<Vector3>
            {
                center + Vector3.right * radii.x,
                center - Vector3.right * radii.x,
                center + Vector3.up * radii.y,
                center - Vector3.up * radii.y,
                center + Vector3.forward * radii.z,
                center - Vector3.forward * radii.z
            };
            var triangles = new List<int>
            {
                2, 0, 4,
                2, 4, 1,
                2, 1, 5,
                2, 5, 0,
                3, 4, 0,
                3, 1, 4,
                3, 5, 1,
                3, 0, 5
            };
            return CreateMesh(vertices, triangles);
        }

        private static Mesh BuildGemMesh(
            Vector3 center,
            Vector3 radii,
            ShortSwordGemCut cut,
            float facing)
        {
            Vector2[] outline = cut switch
            {
                ShortSwordGemCut.Emerald => new[]
                {
                    new Vector2(-0.55f, 1f),
                    new Vector2(0.55f, 1f),
                    new Vector2(1f, 0.55f),
                    new Vector2(1f, -0.55f),
                    new Vector2(0.55f, -1f),
                    new Vector2(-0.55f, -1f),
                    new Vector2(-1f, -0.55f),
                    new Vector2(-1f, 0.55f)
                },
                ShortSwordGemCut.PrincessSquare => new[]
                {
                    new Vector2(-1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, -1f),
                    new Vector2(-1f, -1f)
                },
                ShortSwordGemCut.Pear => new[]
                {
                    new Vector2(0f, 1f),
                    new Vector2(0.58f, 0.46f),
                    new Vector2(0.86f, -0.12f),
                    new Vector2(0.58f, -0.72f),
                    new Vector2(0f, -1f),
                    new Vector2(-0.58f, -0.72f),
                    new Vector2(-0.86f, -0.12f),
                    new Vector2(-0.58f, 0.46f)
                },
                _ => new[]
                {
                    new Vector2(0f, 1f),
                    new Vector2(0.707f, 0.707f),
                    new Vector2(1f, 0f),
                    new Vector2(0.707f, -0.707f),
                    new Vector2(0f, -1f),
                    new Vector2(-0.707f, -0.707f),
                    new Vector2(-1f, 0f),
                    new Vector2(-0.707f, 0.707f)
                }
            };

            int count = outline.Length;
            var vertices = new List<Vector3>(count * 2 + 2);
            var triangles = new List<int>(count * 12);
            for (int index = 0; index < count; index++)
            {
                Vector2 point = outline[index];
                vertices.Add(center + new Vector3(
                    point.x * radii.x,
                    point.y * radii.y,
                    0f));
            }
            for (int index = 0; index < count; index++)
            {
                Vector2 point = outline[index] * 0.56f;
                vertices.Add(center + new Vector3(
                    point.x * radii.x,
                    point.y * radii.y,
                    facing * radii.z * 0.68f));
            }
            int backCenter = vertices.Count;
            vertices.Add(center - Vector3.forward * (facing * radii.z * 0.42f));
            int tableCenter = vertices.Count;
            vertices.Add(center + Vector3.forward * (facing * radii.z * 0.68f));

            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                AddQuad(
                    triangles,
                    index,
                    next,
                    count + next,
                    count + index);
                triangles.Add(tableCenter);
                triangles.Add(count + index);
                triangles.Add(count + next);
                triangles.Add(backCenter);
                triangles.Add(next);
                triangles.Add(index);
            }
            return CreateMesh(vertices, triangles);
        }

        private static Mesh BuildHelixMesh(
            ProceduralShortSwordDefinition definition,
            bool clockwise)
        {
            const int sampleCount = 27;
            const int sides = 4;
            const float turns = 2.35f;
            float top = ResolveHandleSeatHeight(definition) - 0.016f;
            float bottom = -definition.HandleLength + 0.020f;
            float direction = clockwise ? 1f : -1f;
            float phase = clockwise ? 0f : Mathf.PI;
            var centers = new List<Vector3>(sampleCount);
            for (int index = 0; index < sampleCount; index++)
            {
                float t = index / (sampleCount - 1f);
                float angle = phase + direction * t * turns * Mathf.PI * 2f;
                float pathRadius = ResolveHandleSurfaceRadius(
                    definition,
                    t) + 0.0035f;
                centers.Add(new Vector3(
                    Mathf.Cos(angle) * pathRadius,
                    Mathf.Lerp(top, bottom, t),
                    Mathf.Sin(angle) * pathRadius));
            }

            var vertices = new List<Vector3>(sampleCount * sides + 2);
            var triangles = new List<int>((sampleCount - 1) * sides * 6 + 24);
            for (int index = 0; index < sampleCount; index++)
            {
                Vector3 tangent = index == 0
                    ? centers[1] - centers[0]
                    : index == sampleCount - 1
                        ? centers[index] - centers[index - 1]
                        : centers[index + 1] - centers[index - 1];
                tangent.Normalize();
                Vector3 radial = new Vector3(
                    centers[index].x,
                    0f,
                    centers[index].z).normalized;
                Vector3 across = Vector3.Cross(tangent, radial).normalized;
                float t = index / (sampleCount - 1f);
                float cordRadius = Mathf.Clamp(
                    ResolveHandleSurfaceRadius(definition, t) * 0.105f,
                    0.0027f,
                    0.0040f);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side / (float)sides * Mathf.PI * 2f;
                    vertices.Add(
                        centers[index] +
                        radial * (Mathf.Cos(angle) * cordRadius) +
                        across * (Mathf.Sin(angle) * cordRadius));
                }
            }
            for (int index = 0; index < sampleCount - 1; index++)
            {
                int current = index * sides;
                int next = (index + 1) * sides;
                for (int side = 0; side < sides; side++)
                {
                    int nextSide = (side + 1) % sides;
                    AddQuad(
                        triangles,
                        current + side,
                        current + nextSide,
                        next + nextSide,
                        next + side);
                }
            }

            int topCenter = vertices.Count;
            vertices.Add(centers[0]);
            int bottomCenter = vertices.Count;
            vertices.Add(centers[sampleCount - 1]);
            int bottomStart = (sampleCount - 1) * sides;
            for (int side = 0; side < sides; side++)
            {
                int nextSide = (side + 1) % sides;
                triangles.Add(topCenter);
                triangles.Add(side);
                triangles.Add(nextSide);
                triangles.Add(bottomCenter);
                triangles.Add(bottomStart + nextSide);
                triangles.Add(bottomStart + side);
            }
            return CreateMesh(vertices, triangles);
        }

        public static float ResolveHandleSurfaceRadius(
            ProceduralShortSwordDefinition definition,
            float normalizedHeight,
            int ringIndex = -1)
        {
            float t = Mathf.Clamp01(normalizedHeight);
            float profile = definition.HandleProfile switch
            {
                ShortSwordHandleProfile.Tapered => Mathf.Lerp(1.06f, 0.88f, t),
                ShortSwordHandleProfile.Waisted => 0.88f +
                    Mathf.Abs(t - 0.5f) * 0.30f,
                _ => 1f
            };
            float wrapRelief = ringIndex > 0 && ringIndex < 6
                ? (ringIndex % 2 == 0 ? 1.035f : 0.985f)
                : 1f;
            return definition.HandleRadius * profile * wrapRelief;
        }

        private static Mesh BuildExtrudedPolygon(
            IReadOnlyList<Vector2> outline,
            float depth)
        {
            int count = outline.Count;
            float halfDepth = depth * 0.5f;
            var vertices = new List<Vector3>(count * 2);
            var triangles = new List<int>((count - 2) * 6 + count * 6);
            for (int index = 0; index < count; index++)
            {
                Vector2 point = outline[index];
                vertices.Add(new Vector3(point.x, point.y, halfDepth));
            }
            for (int index = 0; index < count; index++)
            {
                Vector2 point = outline[index];
                vertices.Add(new Vector3(point.x, point.y, -halfDepth));
            }
            for (int index = 1; index < count - 1; index++)
            {
                triangles.Add(0);
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(count);
                triangles.Add(count + index + 1);
                triangles.Add(count + index);
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                AddQuad(
                    triangles,
                    index,
                    count + index,
                    count + next,
                    next);
            }
            return CreateMesh(vertices, triangles);
        }

        private static Mesh BuildRevolvedMesh(
            IReadOnlyList<Vector2> rings,
            int sides)
        {
            var centers = new Vector2[rings.Count];
            return BuildRevolvedMesh(rings, centers, sides);
        }

        private static Mesh BuildRevolvedMesh(
            IReadOnlyList<Vector2> rings,
            int sides,
            ProceduralShortSwordDefinition topSeatDefinition)
        {
            var centers = new Vector2[rings.Count];
            return BuildRevolvedMesh(
                rings,
                centers,
                sides,
                topSeatDefinition);
        }

        private static Mesh BuildRevolvedMesh(
            IReadOnlyList<Vector2> rings,
            IReadOnlyList<Vector2> centers,
            int sides,
            ProceduralShortSwordDefinition? topSeatDefinition = null)
        {
            var vertices = new List<Vector3>(rings.Count * sides + 2);
            var triangles = new List<int>((rings.Count - 1) * sides * 6 + sides * 6);
            for (int ring = 0; ring < rings.Count; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side / (float)sides * Mathf.PI * 2f;
                    float x = centers[ring].x +
                        Mathf.Cos(angle) * rings[ring].y;
                    float y = ring == 0 && topSeatDefinition.HasValue
                        ? ResolveHandleSeatHeightAtX(
                            topSeatDefinition.Value,
                            x)
                        : rings[ring].x;
                    vertices.Add(new Vector3(
                        x,
                        y,
                        centers[ring].y +
                            Mathf.Sin(angle) * rings[ring].y));
                }
            }
            for (int ring = 0; ring < rings.Count - 1; ring++)
            {
                int current = ring * sides;
                int nextRing = (ring + 1) * sides;
                for (int side = 0; side < sides; side++)
                {
                    int nextSide = (side + 1) % sides;
                    AddQuad(
                        triangles,
                        current + side,
                        current + nextSide,
                        nextRing + nextSide,
                        nextRing + side);
                }
            }

            int topCenter = vertices.Count;
            vertices.Add(new Vector3(
                centers[0].x,
                topSeatDefinition.HasValue
                    ? ResolveHandleSeatHeightAtX(
                        topSeatDefinition.Value,
                        centers[0].x)
                    : rings[0].x,
                centers[0].y));
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(
                centers[rings.Count - 1].x,
                rings[rings.Count - 1].x,
                centers[rings.Count - 1].y));
            int bottomStart = (rings.Count - 1) * sides;
            for (int side = 0; side < sides; side++)
            {
                int nextSide = (side + 1) % sides;
                triangles.Add(topCenter);
                triangles.Add(nextSide);
                triangles.Add(side);
                triangles.Add(bottomCenter);
                triangles.Add(bottomStart + side);
                triangles.Add(bottomStart + nextSide);
            }
            return CreateMesh(vertices, triangles);
        }

        private static Mesh CreateMesh(
            List<Vector3> vertices,
            List<int> triangles)
        {
            var flatVertices = new List<Vector3>(triangles.Count);
            var flatNormals = new List<Vector3>(triangles.Count);
            var flatTriangles = new List<int>(triangles.Count);
            for (int index = 0; index < triangles.Count; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude <= 0.000000000001f)
                {
                    continue;
                }
                Vector3 normal = cross.normalized;
                int first = flatVertices.Count;
                flatVertices.Add(a);
                flatVertices.Add(b);
                flatVertices.Add(c);
                flatNormals.Add(normal);
                flatNormals.Add(normal);
                flatNormals.Add(normal);
                flatTriangles.Add(first);
                flatTriangles.Add(first + 1);
                flatTriangles.Add(first + 2);
            }

            var mesh = new Mesh();
            mesh.SetVertices(flatVertices);
            mesh.SetNormals(flatNormals);
            mesh.SetTriangles(flatTriangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            List<int> triangles,
            int a,
            int b,
            int c,
            int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private static float Lerp(
            System.Random random,
            float minimum,
            float maximum)
        {
            return Mathf.Lerp(
                minimum,
                maximum,
                (float)random.NextDouble());
        }
    }
}
