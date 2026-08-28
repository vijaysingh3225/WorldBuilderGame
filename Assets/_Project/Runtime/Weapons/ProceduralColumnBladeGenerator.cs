using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Weapons
{
    public enum ColumnBladeMaterial
    {
        Stone = 0,
        Wood = 1,
        Obsidian = 2
    }

    public enum ColumnBladeAccentPalette
    {
        Sage = 0,
        DustyBlue = 1,
        ClayRose = 2,
        SoftOchre = 3
    }

    public enum ColumnBladeSectionProfile
    {
        FlatSlab = 0,
        BalancedBlock = 1
    }

    public enum ColumnBladeShapeCategory
    {
        SquareBlock = 0,
        FlatThin = 1,
        WideFlat = 2
    }

    public enum ColumnBladeEdgeStyle
    {
        Plain = 0,
        TwinSideEdges = 1
    }

    public enum ColumnBladeGuardProfile
    {
        WideBar = 0,
        CompactBlock = 1,
        Octagonal = 2,
        Ring = 3
    }

    public enum ColumnBladeTopProfile
    {
        Flat = 0,
        SlightSlant = 1,
        SteepSlant = 2
    }

    public enum ColumnBladeEngravingStyle
    {
        None = 0,
        StraightLine = 1,
        SilhouetteInset = 2
    }

    public enum ColumnBladeSilhouetteWallProfile
    {
        Straight = 0,
        Slanted = 1,
        DramaticSlant = 2
    }

    public enum ColumnBladeEngravingTermination
    {
        Half = 0,
        ShortOfTop = 1,
        Full = 2,
        Circle = 3
    }

    public enum ColumnBladeEngravingPath
    {
        Single = 0,
        Forked = 1
    }

    public enum ColumnBladeEngravingFill
    {
        MutedGold = 0
    }

    [Serializable]
    public readonly struct ColumnBladeTextureTransform
    {
        public readonly Vector2 Scale;
        public readonly Vector2 Offset;

        public ColumnBladeTextureTransform(Vector2 scale, Vector2 offset)
        {
            Scale = scale;
            Offset = offset;
        }

        public Vector4 AsShaderVector =>
            new Vector4(Scale.x, Scale.y, Offset.x, Offset.y);
    }

    [Serializable]
    public struct ProceduralColumnBladeDefinition
    {
        public int Seed;
        public ColumnBladeMaterial BladeMaterial;
        public ColumnBladeAccentPalette AccentPalette;
        public ColumnBladeShapeCategory ShapeCategory;
        public ColumnBladeSectionProfile SectionProfile;
        public ColumnBladeEdgeStyle EdgeStyle;
        public ColumnBladeGuardProfile GuardProfile;
        public int GuardColorVariant;
        public ColumnBladeTopProfile TopProfile;
        public ColumnBladeEngravingStyle PrimaryEngraving;
        public ColumnBladeSilhouetteWallProfile SilhouetteWallProfile;
        public ColumnBladeEngravingTermination EngravingTermination;
        public ColumnBladeEngravingFill EngravingFill;
        public float EngravingEndFraction;
        public float EngravingWidthScale;
        public bool EngravingAllFourSides;
        public ColumnBladeEngravingPath EngravingPath;
        public float EngravingForkFraction;
        public float EngravingForkHalfSpacing;
        public int TopSlantDirection;
        public float TopSlantRise;
        public float BladeLength;
        public float BladeCoreWidth;
        public float BladeWidth;
        public float BladeThickness;
        public float BladeEdgeWidth;
        public int StoneChipCount;
        public float StoneChipDepth;
        public float GuardWidth;
        public float GuardHeight;
        public float GuardDepth;
        public float HandleLength;
        public float HandleWidth;
        public float HandleDepth;
        public float FurnitureRadialScale;
        public ShortSwordHandleProfile HandleProfile;
        public ShortSwordHandleCrossSection HandleCrossSection;
        public ShortSwordGripStyle GripStyle;
        public ShortSwordHiltProfile PommelProfile;
        public ShortSwordFacetTier FurnitureFacetTier;
        public int GripBandCount;
        public int GripWrapTurns;
        public float GripWrapPitch;
        public float GripWrapWidth;
        public float GripWrapThickness;
        public float PommelLength;
        public float PommelWidth;
        public float PommelDepth;

        public float TotalLength =>
            BladeLength + HandleLength + PommelLength;

        public float AssembledLength =>
            TotalLength + GuardHeight *
                (GuardProfile == ColumnBladeGuardProfile.Ring
                    ? 0.76f
                    : 0.12f);
    }

    /// <summary>
    /// A deliberately small second sword phenotype. Column blades use a
    /// flat-ended slab or balanced block instead of a conventional point and
    /// keep every major part on a straight, hard-edged construction language.
    /// This generator remains independent from the mature short-sword branch
    /// catalog so its future avenues can be added one at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralColumnBladeGenerator : MonoBehaviour
    {
        public const string BladePartName = "Column Blade";
        public const string BladeRingJointPartName = "Column Blade Ring Joint";
        public const string GuardPartName = "Column Guard";
        public const string HandlePartName = "Short Sword Handle";
        public const string PommelPartName = "Short Sword Pommel";
        public const string SilhouetteInsetPartName =
            "Silhouette Inset Floor";
        public const int EngravingCircleSegments = 256;

        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private int startingSeed = 2401;
        [SerializeField] private ColumnBladeMaterial selectedBladeMaterial =
            ColumnBladeMaterial.Stone;
        [SerializeField] private bool bladeMaterialLocked;
        [SerializeField] private Material bladeMaterial;
        [SerializeField] private Material woodBladeMaterial;
        [SerializeField] private Material obsidianBladeMaterial;
        [SerializeField] private Material furnitureMaterial;
        [SerializeField] private Material accentMaterial;

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<GameObject> generatedParts =
            new List<GameObject>();
        private ProceduralColumnBladeDefinition currentDefinition;
        private bool hasGeneratedSword;
        private GameObject furnitureSource;
        private ColumnBladeShapeCategory? lockedShapeCategory;
        private ColumnBladeEdgeStyle? lockedEdgeStyle;
        private ColumnBladeGuardProfile? lockedGuardProfile;
        private ColumnBladeTopProfile? lockedTopProfile;
        private ColumnBladeEngravingStyle? lockedEngravingStyle;
        private ColumnBladeEngravingPath? lockedEngravingPath;
        private ColumnBladeSilhouetteWallProfile?
            lockedSilhouetteWallProfile;

        public ProceduralColumnBladeDefinition CurrentDefinition =>
            currentDefinition;
        public ColumnBladeMaterial? SelectedBladeMaterial =>
            bladeMaterialLocked ? selectedBladeMaterial : null;
        public bool IsBladeMaterialLocked => bladeMaterialLocked;
        public bool HasGeneratedSword => hasGeneratedSword;
        public ColumnBladeShapeCategory? LockedShapeCategory =>
            lockedShapeCategory;
        public ColumnBladeEdgeStyle? LockedEdgeStyle => lockedEdgeStyle;
        public ColumnBladeGuardProfile? LockedGuardProfile =>
            lockedGuardProfile;
        public ColumnBladeTopProfile? LockedTopProfile => lockedTopProfile;
        public ColumnBladeEngravingStyle? LockedEngravingStyle =>
            lockedEngravingStyle;
        public ColumnBladeEngravingPath? LockedEngravingPath =>
            lockedEngravingPath;
        public ColumnBladeSilhouetteWallProfile?
            LockedSilhouetteWallProfile => lockedSilhouetteWallProfile;

        public void SetGenerateOnStart(bool value)
        {
            generateOnStart = value;
        }
        public IReadOnlyList<GameObject> GeneratedParts => generatedParts;

        public void ConfigureMaterials(
            Material blade,
            Material furniture,
            Material accent)
        {
            bladeMaterial = blade;
            woodBladeMaterial = blade;
            obsidianBladeMaterial = blade;
            furnitureMaterial = furniture;
            accentMaterial = accent;
        }

        public void ConfigureMaterials(
            Material stoneBlade,
            Material woodBlade,
            Material obsidianBlade,
            Material furniture,
            Material accent)
        {
            bladeMaterial = stoneBlade;
            woodBladeMaterial = woodBlade;
            obsidianBladeMaterial = obsidianBlade;
            furnitureMaterial = furniture;
            accentMaterial = accent;
        }

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

        public ProceduralColumnBladeDefinition GenerateNext()
        {
            int seed = hasGeneratedSword
                ? unchecked(currentDefinition.Seed + 1)
                : startingSeed;
            return Generate(seed);
        }

        public ProceduralColumnBladeDefinition Generate(int seed)
        {
            ClearGeneratedSword();
            ColumnBladeMaterial generatedMaterial = bladeMaterialLocked
                ? selectedBladeMaterial
                : ResolveBladeMaterial(seed);
            currentDefinition = CreateDefinition(
                seed,
                generatedMaterial,
                lockedShapeCategory,
                lockedTopProfile,
                lockedEngravingStyle,
                lockedEngravingPath,
                lockedEdgeStyle,
                lockedSilhouetteWallProfile,
                lockedGuardProfile);

            Mesh bladeMesh = BuildBladeMesh(currentDefinition);
            if (currentDefinition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.StraightLine &&
                currentDefinition.EngravingPath ==
                    ColumnBladeEngravingPath.Forked)
            {
                Mesh uncarvedBlade = bladeMesh;
                bladeMesh = CarveForkEngravingIntoBladeMesh(
                    uncarvedBlade,
                    currentDefinition);
                DestroyGeneratedMeshImmediatelyOrDeferred(uncarvedBlade);
            }
            if (currentDefinition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Circle &&
                currentDefinition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.StraightLine)
            {
                Mesh uncarvedBlade = bladeMesh;
                bladeMesh = CarveCircleEngravingIntoBladeMesh(
                    uncarvedBlade,
                    currentDefinition);
                DestroyGeneratedMeshImmediatelyOrDeferred(uncarvedBlade);
            }
            ColumnBladeTextureTransform bladeTextureTransform =
                ResolveTextureTransform(
                    currentDefinition.Seed,
                    CalculateUvBounds(bladeMesh));
            GameObject bladePart = CreatePart(
                BladePartName,
                bladeMesh,
                ResolveConfiguredBladeMaterial(
                    currentDefinition.BladeMaterial),
                Color.white,
                ResolveBladeMetallic(currentDefinition.BladeMaterial),
                ResolveBladeSmoothness(currentDefinition.BladeMaterial),
                bladeTextureTransform);
            CreateBladeEngravings(bladePart, currentDefinition);

            if (currentDefinition.GuardProfile ==
                ColumnBladeGuardProfile.Ring)
            {
                CreatePart(
                    BladeRingJointPartName,
                    BuildRingBladeJointMesh(currentDefinition),
                    ResolveConfiguredBladeMaterial(
                        currentDefinition.BladeMaterial),
                    Color.white,
                    ResolveBladeMetallic(currentDefinition.BladeMaterial),
                    ResolveBladeSmoothness(currentDefinition.BladeMaterial),
                    bladeTextureTransform);
            }

            CreatePart(
                GuardPartName,
                BuildGuardMesh(currentDefinition),
                furnitureMaterial,
                currentDefinition.GuardProfile == ColumnBladeGuardProfile.Ring
                    ? ResolveRingGuardColor(
                        currentDefinition.BladeMaterial,
                        currentDefinition.GuardColorVariant)
                    : ResolveFurnitureColor(
                        currentDefinition.BladeMaterial),
                0.18f,
                0.18f);

            CreateShortSwordFurniture(currentDefinition);

            hasGeneratedSword = true;
            return currentDefinition;
        }

        public ProceduralColumnBladeDefinition GenerateForShapeCategory(
            int seed,
            ColumnBladeShapeCategory category)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeShapeCategory), category))
            {
                return Generate(seed);
            }

            ColumnBladeShapeCategory? previous = lockedShapeCategory;
            lockedShapeCategory = category;
            try
            {
                return Generate(seed);
            }
            finally
            {
                lockedShapeCategory = previous;
            }
        }

        public void SetBladeMaterial(
            ColumnBladeMaterial material,
            bool regenerateCurrent = true)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeMaterial), material))
            {
                return;
            }

            selectedBladeMaterial = material;
            bladeMaterialLocked = true;
            if (!regenerateCurrent || !hasGeneratedSword)
            {
                return;
            }

            int seed = currentDefinition.Seed;
            Generate(seed);
        }

        public void ToggleBladeMaterialLock(
            ColumnBladeMaterial material,
            bool regenerateCurrent = true)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeMaterial), material))
            {
                return;
            }

            if (bladeMaterialLocked && selectedBladeMaterial == material)
            {
                bladeMaterialLocked = false;
            }
            else
            {
                selectedBladeMaterial = material;
                bladeMaterialLocked = true;
            }
            if (regenerateCurrent && hasGeneratedSword)
            {
                Generate(currentDefinition.Seed);
            }
        }

        public void ClearBladeMaterialLock(bool regenerateCurrent = true)
        {
            bladeMaterialLocked = false;
            if (regenerateCurrent && hasGeneratedSword)
            {
                Generate(currentDefinition.Seed);
            }
        }

        public static ColumnBladeMaterial ResolveBladeMaterial(int seed)
        {
            var random = new System.Random(
                unchecked(seed * 104729 + 7919));
            return (ColumnBladeMaterial)random.Next(0, 3);
        }

        public bool IsShapeCategoryLocked(
            ColumnBladeShapeCategory category)
        {
            return lockedShapeCategory == category;
        }

        public void ToggleShapeCategoryLock(
            ColumnBladeShapeCategory category)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeShapeCategory), category))
            {
                return;
            }
            lockedShapeCategory = lockedShapeCategory == category
                ? null
                : category;
            if (lockedShapeCategory == ColumnBladeShapeCategory.SquareBlock &&
                lockedEdgeStyle == ColumnBladeEdgeStyle.TwinSideEdges)
            {
                lockedEdgeStyle = null;
            }
            if (lockedShapeCategory == ColumnBladeShapeCategory.SquareBlock &&
                lockedGuardProfile == ColumnBladeGuardProfile.Ring)
            {
                lockedGuardProfile = null;
            }
        }

        public void ClearShapeCategoryLock()
        {
            lockedShapeCategory = null;
        }

        public bool IsEdgeStyleLocked(ColumnBladeEdgeStyle style)
        {
            return lockedEdgeStyle == style;
        }

        public void ToggleEdgeStyleLock(ColumnBladeEdgeStyle style)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeEdgeStyle), style) ||
                (style == ColumnBladeEdgeStyle.TwinSideEdges &&
                 (lockedShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock ||
                  (!lockedShapeCategory.HasValue && hasGeneratedSword &&
                   currentDefinition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock))))
            {
                return;
            }
            lockedEdgeStyle = lockedEdgeStyle == style ? null : style;
        }

        public void ClearEdgeStyleLock()
        {
            lockedEdgeStyle = null;
        }

        public bool IsGuardProfileLocked(ColumnBladeGuardProfile profile)
        {
            return lockedGuardProfile == profile;
        }

        public void ToggleGuardProfileLock(ColumnBladeGuardProfile profile)
        {
            bool squareBlade = lockedShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock ||
                (!lockedShapeCategory.HasValue && hasGeneratedSword &&
                 currentDefinition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock);
            if (!Enum.IsDefined(typeof(ColumnBladeGuardProfile), profile) ||
                (profile == ColumnBladeGuardProfile.Ring && squareBlade))
            {
                return;
            }
            lockedGuardProfile = lockedGuardProfile == profile
                ? null
                : profile;
        }

        public void ClearGuardProfileLock()
        {
            lockedGuardProfile = null;
        }

        public bool IsTopProfileLocked(ColumnBladeTopProfile profile)
        {
            return lockedTopProfile == profile;
        }

        public void ToggleTopProfileLock(ColumnBladeTopProfile profile)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeTopProfile), profile))
            {
                return;
            }
            lockedTopProfile = lockedTopProfile == profile
                ? null
                : profile;
        }

        public void ClearTopProfileLock()
        {
            lockedTopProfile = null;
        }

        public bool IsEngravingStyleLocked(
            ColumnBladeEngravingStyle style)
        {
            return lockedEngravingStyle == style;
        }

        public void ToggleEngravingStyleLock(
            ColumnBladeEngravingStyle style)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeEngravingStyle), style))
            {
                return;
            }
            lockedEngravingStyle = lockedEngravingStyle == style
                ? null
                : style;
        }

        public bool IsEngravingPathLocked(ColumnBladeEngravingPath path)
        {
            return lockedEngravingPath == path;
        }

        public void ToggleEngravingPathLock(ColumnBladeEngravingPath path)
        {
            if (!Enum.IsDefined(typeof(ColumnBladeEngravingPath), path))
            {
                return;
            }
            lockedEngravingPath = lockedEngravingPath == path
                ? null
                : path;
        }

        public bool IsSilhouetteWallProfileLocked(
            ColumnBladeSilhouetteWallProfile profile)
        {
            return lockedSilhouetteWallProfile == profile;
        }

        public void ToggleSilhouetteWallProfileLock(
            ColumnBladeSilhouetteWallProfile profile)
        {
            if (!Enum.IsDefined(
                    typeof(ColumnBladeSilhouetteWallProfile),
                    profile))
            {
                return;
            }
            lockedSilhouetteWallProfile =
                lockedSilhouetteWallProfile == profile ? null : profile;
        }

        public void ClearSilhouetteWallProfileLock()
        {
            lockedSilhouetteWallProfile = null;
        }

        public static ProceduralColumnBladeDefinition CreateDefinition(
            int seed,
            ColumnBladeMaterial material = ColumnBladeMaterial.Stone,
            ColumnBladeShapeCategory? requiredShapeCategory = null,
            ColumnBladeTopProfile? requiredTopProfile = null,
            ColumnBladeEngravingStyle? requiredEngravingStyle = null,
            ColumnBladeEngravingPath? requiredEngravingPath = null,
            ColumnBladeEdgeStyle? requiredEdgeStyle = null,
            ColumnBladeSilhouetteWallProfile?
                requiredSilhouetteWallProfile = null,
            ColumnBladeGuardProfile? requiredGuardProfile = null)
        {
            var random = new System.Random(unchecked(seed * 486187739 + 2401));
            ColumnBladeShapeCategory shapeCategory =
                (ColumnBladeShapeCategory)random.Next(0, 3);
            if (requiredShapeCategory.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeShapeCategory),
                    requiredShapeCategory.Value))
            {
                shapeCategory = requiredShapeCategory.Value;
            }
            var sectionProfile = shapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock
                ? ColumnBladeSectionProfile.BalancedBlock
                : ColumnBladeSectionProfile.FlatSlab;
            var edgeStyle = (ColumnBladeEdgeStyle)random.Next(0, 2);
            if (requiredEdgeStyle.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeEdgeStyle),
                    requiredEdgeStyle.Value))
            {
                edgeStyle = requiredEdgeStyle.Value;
            }
            if (shapeCategory == ColumnBladeShapeCategory.SquareBlock)
            {
                edgeStyle = ColumnBladeEdgeStyle.Plain;
            }
            var guardProfile = (ColumnBladeGuardProfile)random.Next(
                0,
                shapeCategory == ColumnBladeShapeCategory.SquareBlock
                    ? 3
                    : 4);
            if (requiredGuardProfile.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeGuardProfile),
                    requiredGuardProfile.Value))
            {
                guardProfile = requiredGuardProfile.Value;
            }
            if (shapeCategory == ColumnBladeShapeCategory.SquareBlock &&
                guardProfile == ColumnBladeGuardProfile.Ring)
            {
                guardProfile = ColumnBladeGuardProfile.CompactBlock;
            }
            ColumnBladeTopProfile topProfile =
                (ColumnBladeTopProfile)random.Next(0, 3);
            if (requiredTopProfile.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeTopProfile),
                    requiredTopProfile.Value))
            {
                topProfile = requiredTopProfile.Value;
            }
            // Preserve the existing seeded random stream so this orientation
            // correction does not reroll any later blade attributes.
            random.Next(0, 2);
            // Combat presentation always holds this family with local left as
            // the rear of the swipe. Keeping that corner higher makes every
            // slanted top face forward instead of randomly generating a sword
            // that reads as though it is being held backwards.
            const int topSlantDirection = -1;
            float topSlantRise = topProfile switch
            {
                ColumnBladeTopProfile.SlightSlant =>
                    Lerp(random, 0.018f, 0.040f),
                ColumnBladeTopProfile.SteepSlant =>
                    Lerp(random, 0.055f, 0.105f),
                _ => 0f
            };
            ProceduralShortSwordDefinition furniture =
                ProceduralShortSwordGenerator.CreateDefinition(
                    seed,
                    null,
                    useColumnFurnitureStandard: false);
            float furnitureRadialScale = ResolveFurnitureRadialScale(seed);
            float widthPosition = (float)random.NextDouble();
            float bladeCoreWidth;
            float edgeWidthRatio;
            switch (shapeCategory)
            {
                case ColumnBladeShapeCategory.SquareBlock:
                    bladeCoreWidth = Mathf.Lerp(
                        0.072f,
                        0.098f,
                        widthPosition);
                    edgeWidthRatio = Lerp(random, 0.03f, 0.06f);
                    break;
                case ColumnBladeShapeCategory.WideFlat:
                    bladeCoreWidth = Mathf.Lerp(
                        0.112f,
                        0.142f,
                        widthPosition);
                    edgeWidthRatio = Lerp(random, 0.04f, 0.08f);
                    break;
                default:
                    bladeCoreWidth = Mathf.Lerp(
                        0.078f,
                        0.108f,
                        widthPosition);
                    edgeWidthRatio = Lerp(random, 0.08f, 0.14f);
                    break;
            }
            float bladeEdgeWidth = edgeStyle ==
                    ColumnBladeEdgeStyle.TwinSideEdges
                ? bladeCoreWidth * edgeWidthRatio
                : 0f;
            float bladeWidth = bladeCoreWidth + bladeEdgeWidth * 2f;
            float bladeThickness = shapeCategory switch
            {
                ColumnBladeShapeCategory.SquareBlock => Mathf.Min(
                    0.085f,
                    bladeWidth * Lerp(random, 0.76f, 0.94f)),
                ColumnBladeShapeCategory.WideFlat =>
                    bladeCoreWidth *
                    Mathf.Lerp(0.25f, 0.12f, widthPosition) *
                    Lerp(random, 0.82f, 1f),
                _ => bladeCoreWidth * Lerp(random, 0.16f, 0.28f)
            };
            ResolveEngravingDefinition(
                seed,
                requiredEngravingStyle,
                out ColumnBladeEngravingStyle primaryEngraving,
                out ColumnBladeEngravingTermination engravingTermination,
                out float engravingEndFraction);
            ColumnBladeSilhouetteWallProfile silhouetteWallProfile =
                ResolveSilhouetteWallProfile(
                    seed,
                    requiredSilhouetteWallProfile);
            ResolveEngravingPathDefinition(
                seed,
                primaryEngraving,
                requiredEngravingPath,
                bladeCoreWidth,
                out ColumnBladeEngravingPath engravingPath,
                out float engravingForkFraction,
                out float engravingForkHalfSpacing);
            float engravingWidthScale = ResolveEngravingWidthScale(
                seed,
                primaryEngraving,
                engravingTermination,
                engravingPath);
            bool engravingAllFourSides = ResolveEngravingAllFourSides(
                seed,
                shapeCategory,
                edgeStyle,
                primaryEngraving,
                engravingTermination,
                engravingPath);
            float handleDiameter = furniture.HandleRadius * 2.20f *
                furnitureRadialScale;
            ResolveGuardDimensions(
                random,
                guardProfile,
                bladeWidth,
                bladeThickness,
                handleDiameter,
                handleDiameter,
                out float guardWidth,
                out float guardHeight,
                out float guardDepth);
            float bladeLength = Lerp(random, 0.76f, 0.94f);
            float furnitureScale = bladeLength / furniture.BladeLength;
            float shortHandleTop =
                ProceduralShortSwordGenerator.ResolveHandleSeatHeight(
                    furniture);
            float handleLength =
                (furniture.HandleLength + shortHandleTop) * furnitureScale;
            float pommelLength = furniture.HiltLength * furnitureScale;
            return new ProceduralColumnBladeDefinition
            {
                Seed = seed,
                BladeMaterial = Enum.IsDefined(
                    typeof(ColumnBladeMaterial),
                    material)
                        ? material
                        : ColumnBladeMaterial.Stone,
                AccentPalette = (ColumnBladeAccentPalette)random.Next(0, 4),
                ShapeCategory = shapeCategory,
                SectionProfile = sectionProfile,
                EdgeStyle = edgeStyle,
                GuardProfile = guardProfile,
                GuardColorVariant = ResolveRingGuardColorVariant(seed),
                TopProfile = topProfile,
                PrimaryEngraving = primaryEngraving,
                SilhouetteWallProfile = silhouetteWallProfile,
                EngravingTermination = engravingTermination,
                EngravingFill = ResolveEngravingFill(seed),
                EngravingEndFraction = engravingEndFraction,
                EngravingWidthScale = engravingWidthScale,
                EngravingAllFourSides = engravingAllFourSides,
                EngravingPath = engravingPath,
                EngravingForkFraction = engravingForkFraction,
                EngravingForkHalfSpacing = engravingForkHalfSpacing,
                TopSlantDirection = topSlantDirection,
                TopSlantRise = topSlantRise,
                BladeLength = bladeLength,
                BladeCoreWidth = bladeCoreWidth,
                BladeWidth = bladeWidth,
                BladeThickness = bladeThickness,
                BladeEdgeWidth = bladeEdgeWidth,
                StoneChipCount = ResolveStoneChipCount(seed),
                StoneChipDepth = ResolveStoneChipDepth(
                    seed,
                    bladeWidth),
                GuardWidth = guardWidth,
                GuardHeight = guardHeight,
                GuardDepth = guardDepth,
                HandleLength = handleLength,
                HandleWidth = furniture.HandleRadius * 2f *
                    furnitureRadialScale,
                HandleDepth = furniture.HandleRadius * 2f *
                    (furniture.HandleCrossSection ==
                        ShortSwordHandleCrossSection.OvalFaceted
                            ? 0.76f
                            : 1f) * furnitureRadialScale,
                FurnitureRadialScale = furnitureRadialScale,
                HandleProfile = furniture.HandleProfile,
                HandleCrossSection = furniture.HandleCrossSection,
                GripStyle = furniture.GripStyle,
                PommelProfile = furniture.HiltProfile,
                FurnitureFacetTier = furniture.FacetTier,
                GripBandCount = 0,
                GripWrapTurns = 0,
                GripWrapPitch = 0f,
                GripWrapWidth = 0f,
                GripWrapThickness = 0f,
                PommelLength = pommelLength,
                PommelWidth = furniture.HiltRadius * 2f *
                    furnitureRadialScale,
                PommelDepth = furniture.HiltRadius * 2f *
                    furnitureRadialScale
            };
        }

        public static void ResolveEngravingDefinition(
            int seed,
            ColumnBladeEngravingStyle? requiredStyle,
            out ColumnBladeEngravingStyle primary,
            out ColumnBladeEngravingTermination termination,
            out float endFraction)
        {
            var random = new System.Random(
                unchecked(seed * 262147 + 1879));
            double styleRoll = random.NextDouble();
            primary = styleRoll < 0.32
                ? ColumnBladeEngravingStyle.None
                : styleRoll < 0.68
                    ? ColumnBladeEngravingStyle.StraightLine
                    : ColumnBladeEngravingStyle.SilhouetteInset;
            if (requiredStyle.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeEngravingStyle),
                    requiredStyle.Value))
            {
                primary = requiredStyle.Value;
            }

            int terminationRoll = random.Next(0, 4);
            termination = primary == ColumnBladeEngravingStyle.StraightLine &&
                    terminationRoll == 3
                ? ColumnBladeEngravingTermination.Circle
                : ColumnBladeEngravingTermination.Full;
            endFraction = termination ==
                    ColumnBladeEngravingTermination.Circle
                ? Lerp(random, 0.62f, 0.84f)
                : 1f;
        }

        public static ColumnBladeEngravingFill ResolveEngravingFill(int seed)
        {
            return ColumnBladeEngravingFill.MutedGold;
        }

        public static ColumnBladeSilhouetteWallProfile
            ResolveSilhouetteWallProfile(
                int seed,
                ColumnBladeSilhouetteWallProfile? requiredProfile = null)
        {
            if (requiredProfile.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeSilhouetteWallProfile),
                    requiredProfile.Value))
            {
                return requiredProfile.Value;
            }
            var random = new System.Random(
                unchecked(seed * 786433 + 4721));
            return (ColumnBladeSilhouetteWallProfile)random.Next(0, 3);
        }

        public static void ResolveEngravingPathDefinition(
            int seed,
            ColumnBladeEngravingStyle style,
            ColumnBladeEngravingPath? requiredPath,
            float bladeCoreWidth,
            out ColumnBladeEngravingPath path,
            out float forkFraction,
            out float forkHalfSpacing)
        {
            var random = new System.Random(
                unchecked(seed * 524287 + 3469));
            path = random.NextDouble() < 0.38
                ? ColumnBladeEngravingPath.Forked
                : ColumnBladeEngravingPath.Single;
            if (requiredPath.HasValue && Enum.IsDefined(
                    typeof(ColumnBladeEngravingPath),
                    requiredPath.Value))
            {
                path = requiredPath.Value;
            }
            if (style != ColumnBladeEngravingStyle.StraightLine)
            {
                path = ColumnBladeEngravingPath.Single;
            }

            forkFraction = Lerp(random, 0.22f, 0.44f);
            forkHalfSpacing = Mathf.Clamp(
                bladeCoreWidth * Lerp(random, 0.20f, 0.27f),
                0.012f,
                bladeCoreWidth * 0.29f);
        }

        public static float ResolveEngravingWidthScale(
            int seed,
            ColumnBladeEngravingStyle style,
            ColumnBladeEngravingTermination termination,
            ColumnBladeEngravingPath path)
        {
            if (style != ColumnBladeEngravingStyle.StraightLine ||
                termination != ColumnBladeEngravingTermination.Full ||
                path != ColumnBladeEngravingPath.Single)
            {
                return 1f;
            }

            var random = new System.Random(
                unchecked(seed * 1048573 + 6197));
            return Lerp(random, 1f, 2.25f);
        }

        public static bool ResolveEngravingAllFourSides(
            int seed,
            ColumnBladeShapeCategory shapeCategory,
            ColumnBladeEdgeStyle edgeStyle,
            ColumnBladeEngravingStyle style,
            ColumnBladeEngravingTermination termination,
            ColumnBladeEngravingPath path)
        {
            if (shapeCategory != ColumnBladeShapeCategory.SquareBlock ||
                edgeStyle != ColumnBladeEdgeStyle.Plain ||
                style != ColumnBladeEngravingStyle.StraightLine ||
                termination != ColumnBladeEngravingTermination.Full ||
                path != ColumnBladeEngravingPath.Single)
            {
                return false;
            }

            var random = new System.Random(
                unchecked(seed * 2097143 + 7411));
            return random.NextDouble() < 0.38;
        }

        private static void DestroyGeneratedMeshImmediatelyOrDeferred(
            Mesh mesh)
        {
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }

        public static Color ResolveEngravingFillColor(
            ColumnBladeEngravingFill fill)
        {
            return new Color(0.58f, 0.44f, 0.22f, 1f);
        }

        public static Color ResolveSilhouetteInsetColor(
            ColumnBladeMaterial material)
        {
            return material switch
            {
                ColumnBladeMaterial.Wood =>
                    ResolveEngravingFillColor(
                        ColumnBladeEngravingFill.MutedGold),
                ColumnBladeMaterial.Obsidian =>
                    new Color(0.17f, 0.09f, 0.24f, 1f),
                _ => new Color(0.47f, 0.49f, 0.50f, 1f)
            };
        }

        public static float ResolveFurnitureRadialScale(int seed)
        {
            var random = new System.Random(
                unchecked(seed * 131071 + 3253));
            return Lerp(random, 0.58f, 0.66f);
        }

        private static void ResolveGuardDimensions(
            System.Random random,
            ColumnBladeGuardProfile profile,
            float bladeWidth,
            float bladeDepth,
            float handleWidth,
            float handleDepth,
            out float width,
            out float height,
            out float depth)
        {
            float clearance = profile switch
            {
                ColumnBladeGuardProfile.Ring =>
                    Lerp(random, 0.014f, 0.022f),
                ColumnBladeGuardProfile.CompactBlock =>
                    Lerp(random, 0.008f, 0.014f),
                ColumnBladeGuardProfile.Octagonal =>
                    Lerp(random, 0.012f, 0.020f),
                _ => Lerp(random, 0.018f, 0.0254f)
            };
            clearance = Mathf.Max(
                clearance,
                Mathf.Max(
                    (handleWidth - bladeWidth) * 0.5f + 0.004f,
                    (handleDepth - bladeDepth) * 0.5f + 0.004f));
            // Keep every guard profile within one real-world inch per side.
            clearance = Mathf.Min(clearance, 0.0254f);
            if (profile == ColumnBladeGuardProfile.Ring)
            {
                width = Mathf.Max(
                    bladeWidth + clearance * 2f,
                    handleWidth + 0.052f);
                height = width * Lerp(random, 0.82f, 0.94f);
                depth = Mathf.Max(
                    bladeDepth + 0.008f,
                    Lerp(random, 0.026f, 0.038f));
                return;
            }
            width = bladeWidth + clearance * 2f;
            depth = bladeDepth + clearance * 2f;
            switch (profile)
            {
                case ColumnBladeGuardProfile.CompactBlock:
                    height = Lerp(random, 0.044f, 0.060f);
                    break;
                case ColumnBladeGuardProfile.Octagonal:
                    height = Lerp(random, 0.048f, 0.068f);
                    break;
                default:
                    height = Lerp(random, 0.046f, 0.066f);
                    break;
            }
        }

        public static Color ResolveBladeColor(ColumnBladeMaterial material)
        {
            return material switch
            {
                ColumnBladeMaterial.Wood =>
                    new Color(0.33f, 0.255f, 0.18f),
                ColumnBladeMaterial.Obsidian =>
                    new Color(0.085f, 0.075f, 0.105f),
                _ => new Color(0.59f, 0.60f, 0.54f)
            };
        }

        public static float ResolveBladeChamferWidth(
            ProceduralColumnBladeDefinition definition)
        {
            return Mathf.Min(
                0.0015f,
                Mathf.Min(
                    definition.BladeCoreWidth,
                    definition.BladeThickness) * 0.03f);
        }

        public static float ResolveGuardChamferWidth(
            ProceduralColumnBladeDefinition definition)
        {
            return Mathf.Min(
                0.001125f,
                Mathf.Min(
                    definition.GuardHeight * 0.03f,
                    Mathf.Min(
                        definition.GuardWidth,
                        definition.GuardDepth) * 0.015f));
        }

        public static int ResolveStoneChipCount(int seed)
        {
            var random = new System.Random(
                unchecked(seed * 65537 + 4421));
            return random.Next(7, 12);
        }

        public static float ResolveStoneChipDepth(
            int seed,
            float bladeWidth)
        {
            var random = new System.Random(
                unchecked(seed * 8191 + 2377));
            return Mathf.Min(
                bladeWidth * 0.17f,
                Lerp(random, 0.0025f, 0.021f));
        }

        public static float ResolveStoneChipDepthFactor(
            float normalizedLongitudinalSize)
        {
            float size = Mathf.Clamp01(normalizedLongitudinalSize);
            return Mathf.Lerp(
                0.10f,
                1f,
                Mathf.Pow(size, 1.15f));
        }

        public static ColumnBladeTextureTransform ResolveTextureTransform(
            int seed)
        {
            return ResolveTextureTransform(
                seed,
                new Rect(0f, 0f, 1f, 1f));
        }

        public static ColumnBladeTextureTransform ResolveTextureTransform(
            int seed,
            Rect uvBounds)
        {
            // This random stream is deliberately independent from form and
            // material selection. A seed keeps one bounded atlas window when
            // the user switches between stone, wood, and obsidian.
            var random = new System.Random(
                unchecked(seed * 1103515245 + 7919));
            float windowScale = Lerp(random, 0.18f, 0.32f);
            Vector2 scale = Vector2.one * windowScale;
            float minimumOffsetX = -uvBounds.xMin * windowScale;
            float maximumOffsetX = 1f - uvBounds.xMax * windowScale;
            float minimumOffsetY = -uvBounds.yMin * windowScale;
            float maximumOffsetY = 1f - uvBounds.yMax * windowScale;
            Vector2 offset = new Vector2(
                Lerp(random, minimumOffsetX, maximumOffsetX),
                Lerp(random, minimumOffsetY, maximumOffsetY));
            return new ColumnBladeTextureTransform(scale, offset);
        }

        private static Rect CalculateUvBounds(Mesh mesh)
        {
            Vector2[] uvs = mesh.uv;
            if (uvs.Length == 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            Vector2 minimum = uvs[0];
            Vector2 maximum = uvs[0];
            for (int index = 1; index < uvs.Length; index++)
            {
                minimum = Vector2.Min(minimum, uvs[index]);
                maximum = Vector2.Max(maximum, uvs[index]);
            }
            return Rect.MinMaxRect(
                minimum.x,
                minimum.y,
                maximum.x,
                maximum.y);
        }

        public static Color ResolveAccentColor(
            ColumnBladeAccentPalette palette)
        {
            return palette switch
            {
                ColumnBladeAccentPalette.DustyBlue =>
                    new Color(0.39f, 0.53f, 0.58f),
                ColumnBladeAccentPalette.ClayRose =>
                    new Color(0.62f, 0.43f, 0.40f),
                ColumnBladeAccentPalette.SoftOchre =>
                    new Color(0.65f, 0.55f, 0.34f),
                _ => new Color(0.44f, 0.56f, 0.43f)
            };
        }

        public static Color ResolveFurnitureColor(
            ColumnBladeMaterial material)
        {
            return material switch
            {
                ColumnBladeMaterial.Wood =>
                    new Color(0.36f, 0.32f, 0.27f),
                ColumnBladeMaterial.Obsidian =>
                    new Color(0.25f, 0.24f, 0.27f),
                _ => new Color(0.38f, 0.37f, 0.33f)
            };
        }

        public static int ResolveRingGuardColorVariant(int seed)
        {
            var random = new System.Random(
                unchecked(seed * 2097169 + 7013));
            return random.Next(0, 3);
        }

        public static Color ResolveRingGuardColor(
            ColumnBladeMaterial material,
            int variant)
        {
            int safeVariant = Mathf.Clamp(variant, 0, 2);
            return material switch
            {
                ColumnBladeMaterial.Wood => safeVariant switch
                {
                    0 => new Color(0.43f, 0.32f, 0.19f),
                    1 => new Color(0.27f, 0.26f, 0.24f),
                    _ => new Color(0.52f, 0.42f, 0.24f)
                },
                ColumnBladeMaterial.Obsidian => safeVariant switch
                {
                    0 => new Color(0.25f, 0.18f, 0.30f),
                    1 => new Color(0.30f, 0.31f, 0.35f),
                    _ => new Color(0.34f, 0.26f, 0.20f)
                },
                _ => safeVariant switch
                {
                    0 => new Color(0.31f, 0.32f, 0.30f),
                    1 => new Color(0.42f, 0.34f, 0.22f),
                    _ => new Color(0.31f, 0.35f, 0.25f)
                }
            };
        }

        public static float ResolveRingGuardRimThickness(
            ProceduralColumnBladeDefinition definition)
        {
            return Mathf.Clamp(
                Mathf.Min(
                    definition.GuardWidth,
                    definition.GuardHeight) * 0.16f,
                0.014f,
                0.024f);
        }

        public static float ResolveRingGuardBladeSeatWidth(
            ProceduralColumnBladeDefinition definition)
        {
            return definition.GuardWidth * Mathf.Tan(Mathf.PI / 12f);
        }

        public static float ResolveRingGuardHandleSeatWidth(
            ProceduralColumnBladeDefinition definition)
        {
            return ResolveRingGuardBladeSeatWidth(definition);
        }

        public static float ResolveRingGuardSeatY(
            ProceduralColumnBladeDefinition definition)
        {
            return definition.GuardHeight * 0.5f;
        }

        public static float ResolveBladeBottomY(
            ProceduralColumnBladeDefinition definition)
        {
            if (definition.GuardProfile != ColumnBladeGuardProfile.Ring)
            {
                return -definition.GuardHeight * 0.16f;
            }
            return ResolveRingGuardSeatY(definition);
        }

        public static float ResolveHandleTopY(
            ProceduralColumnBladeDefinition definition)
        {
            if (definition.GuardProfile != ColumnBladeGuardProfile.Ring)
            {
                return -definition.GuardHeight * 0.28f;
            }
            return -ResolveRingGuardSeatY(definition);
        }

        private static float ResolveBladeMetallic(
            ColumnBladeMaterial material)
        {
            return material == ColumnBladeMaterial.Obsidian ? 0.06f : 0f;
        }

        private static float ResolveBladeSmoothness(
            ColumnBladeMaterial material)
        {
            return material switch
            {
                ColumnBladeMaterial.Obsidian => 0.42f,
                ColumnBladeMaterial.Wood => 0.10f,
                _ => 0.05f
            };
        }

        private GameObject CreatePart(
            string partName,
            Mesh mesh,
            Material material,
            Color color,
            float metallic,
            float smoothness,
            ColumnBladeTextureTransform? textureTransform = null)
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
                smoothness,
                textureTransform);
            generatedParts.Add(part);
            return part;
        }

        private static void ApplyRendererProperties(
            Renderer renderer,
            Color color,
            float metallic,
            float smoothness,
            ColumnBladeTextureTransform? textureTransform = null)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetFloat("_Metallic", metallic);
            properties.SetFloat("_Smoothness", smoothness);
            properties.SetFloat("_Glossiness", smoothness);
            properties.SetColor("_EmissionColor", Color.black);
            if (textureTransform.HasValue)
            {
                Vector4 shaderTransform =
                    textureTransform.Value.AsShaderVector;
                properties.SetVector("_BaseMap_ST", shaderTransform);
                properties.SetVector("_MainTex_ST", shaderTransform);
            }
            renderer.SetPropertyBlock(properties);
        }

        private void CreateBladeEngravings(
            GameObject bladePart,
            ProceduralColumnBladeDefinition definition)
        {
            if (bladePart == null ||
                definition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.None)
            {
                return;
            }

            Material blade = ResolveConfiguredBladeMaterial(
                definition.BladeMaterial);
            if (definition.PrimaryEngraving ==
                ColumnBladeEngravingStyle.SilhouetteInset)
            {
                CreateEngravingChild(
                    bladePart,
                    SilhouetteInsetPartName,
                    BuildSilhouetteInsetFloorMesh(definition),
                    accentMaterial != null
                        ? accentMaterial
                        : furnitureMaterial != null
                            ? furnitureMaterial
                            : blade,
                    ResolveSilhouetteInsetColor(definition.BladeMaterial),
                    definition.BladeMaterial ==
                        ColumnBladeMaterial.Obsidian ? 0.12f : 0.18f,
                    definition.BladeMaterial ==
                        ColumnBladeMaterial.Obsidian ? 0.34f : 0.18f,
                    null);
                return;
            }
            Mesh floorMesh = definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Circle
                ? BuildContinuousLineAndCircleFloorMesh(definition)
                : BuildPrimaryEngravingFloorMesh(definition);
            CreateEngravingChild(
                bladePart,
                "Engraving Floor Inlay",
                floorMesh,
                accentMaterial != null
                    ? accentMaterial
                    : furnitureMaterial != null
                        ? furnitureMaterial
                        : blade,
                ResolveEngravingFillColor(definition.EngravingFill),
                0.55f,
                0.24f,
                null);

        }

        public static float ResolveSilhouetteInsetMargin(
            ProceduralColumnBladeDefinition definition)
        {
            float limitingSpan = definition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock
                ? Mathf.Min(
                    definition.BladeCoreWidth,
                    definition.BladeThickness)
                : definition.BladeCoreWidth;
            return Mathf.Min(0.0254f, limitingSpan * 0.28f);
        }

        public static float ResolveSilhouetteWallRun(
            ProceduralColumnBladeDefinition definition)
        {
            if (definition.SilhouetteWallProfile ==
                ColumnBladeSilhouetteWallProfile.Straight)
            {
                return 0f;
            }
            float limitingSpan = definition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock
                ? Mathf.Min(
                    definition.BladeCoreWidth,
                    definition.BladeThickness)
                : definition.BladeCoreWidth;
            float margin = ResolveSilhouetteInsetMargin(definition);
            float available = Mathf.Max(0f, limitingSpan - margin * 2f);
            var random = new System.Random(
                unchecked(definition.Seed * 1048573 + 6151));
            float ratio = definition.SilhouetteWallProfile ==
                    ColumnBladeSilhouetteWallProfile.DramaticSlant
                ? Lerp(random, 0.26f, 0.32f)
                : Lerp(random, 0.12f, 0.18f);
            return Mathf.Min(
                available * ratio,
                ResolveEngravingDepth(definition) *
                    (definition.SilhouetteWallProfile ==
                        ColumnBladeSilhouetteWallProfile.DramaticSlant
                            ? 1.35f
                            : 0.72f));
        }

        private static Mesh BuildSilhouetteInsetFloorMesh(
            ProceduralColumnBladeDefinition definition)
        {
            IReadOnlyList<Vector2> crossSection =
                BuildBladeCrossSection(definition);
            float margin = ResolveSilhouetteInsetMargin(definition);
            float wallRun = ResolveSilhouetteWallRun(definition);
            float floorInset = margin + wallRun;
            float visualDepth = Mathf.Max(
                0f,
                ResolveEngravingDepth(definition) - 0.00015f);
            float bottom = ResolveBladeBottomY(definition);
            float panelBottom = definition.GuardProfile ==
                    ColumnBladeGuardProfile.Ring
                ? bottom
                : bottom + floorInset;
            var vertices = new List<Vector3>(48);
            var triangles = new List<int>(72);

            for (int index = 0; index < crossSection.Count; index++)
            {
                Vector2 first = crossSection[index];
                Vector2 second = crossSection[
                    (index + 1) % crossSection.Count];
                if (!IsSilhouetteInsetEdge(
                        definition,
                        first,
                        second,
                        margin))
                {
                    continue;
                }

                Vector2 direction = (second - first).normalized;
                Vector2 inward = new Vector2(-direction.y, direction.x);
                Vector2 lower = first + direction * floorInset +
                    inward * visualDepth;
                Vector2 upper = second - direction * floorInset +
                    inward * visualDepth;
                float lowerTop = ResolveBladeTopY(
                    definition,
                    lower.x,
                    crossSection) - floorInset;
                float upperTop = ResolveBladeTopY(
                    definition,
                    upper.x,
                    crossSection) - floorInset;
                int start = vertices.Count;
                vertices.Add(new Vector3(lower.x, panelBottom, lower.y));
                vertices.Add(new Vector3(lower.x, lowerTop, lower.y));
                vertices.Add(new Vector3(upper.x, upperTop, upper.y));
                vertices.Add(new Vector3(upper.x, panelBottom, upper.y));
                AddQuad(
                    triangles,
                    start,
                    start + 1,
                    start + 2,
                    start + 3);
            }

            if (definition.ShapeCategory ==
                ColumnBladeShapeCategory.SquareBlock)
            {
                Vector2[] capOuter = InsetCrossSection(
                    crossSection,
                    ResolveBladeTopTransition(definition));
                Vector2[] capFloor = InsetCrossSection(
                    capOuter,
                    floorInset);
                int first = vertices.Count;
                for (int index = 0; index < capFloor.Length; index++)
                {
                    Vector2 point = capFloor[index];
                    vertices.Add(new Vector3(
                        point.x,
                        ResolveBladeTopY(
                            definition,
                            point.x,
                            capOuter) - visualDepth,
                        point.y));
                }
                AppendConvexTopCapFan(
                    vertices,
                    triangles,
                    first,
                    capFloor.Length);
            }

            return CreateFlatMesh(vertices, triangles);
        }

        private static Mesh BuildContinuousLineAndCircleFloorMesh(
            ProceduralColumnBladeDefinition definition)
        {
            Mesh line = BuildPrimaryEngravingFloorMesh(definition);
            Mesh loop = BuildCircleEngravingFloorMesh(definition);
            var vertices = new List<Vector3>(
                line.vertexCount + loop.vertexCount);
            var triangles = new List<int>(
                line.triangles.Length + loop.triangles.Length);
            AppendMeshGeometry(vertices, triangles, line);
            AppendMeshGeometry(vertices, triangles, loop);
            if (Application.isPlaying)
            {
                Destroy(line);
                Destroy(loop);
            }
            else
            {
                DestroyImmediate(line);
                DestroyImmediate(loop);
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static Mesh BuildPrimaryEngravingFloorMesh(
            ProceduralColumnBladeDefinition definition)
        {
            return definition.EngravingPath ==
                    ColumnBladeEngravingPath.Forked
                ? BuildForkedEngravingFloorMesh(definition)
                : BuildStraightEngravingFloorMesh(definition);
        }

        private static Mesh BuildForkedEngravingFloorMesh(
            ProceduralColumnBladeDefinition definition)
        {
            ResolveForkEngravingGeometry(
                definition,
                out Vector2[] leftBranch,
                out Vector2[] rightBranch,
                out Vector2[] upperStem);
            float floorDepth = definition.BladeThickness * 0.5f -
                ResolveEngravingDepth(definition) + 0.000025f;
            float halfWidth = ResolveEngravingWidth(definition) * 0.5f;
            float spacing = Mathf.Abs(leftBranch[0].x);
            float bottom = leftBranch[0].y;
            float knee = leftBranch[1].y;
            float fork = leftBranch[2].y;
            float merge = Mathf.Lerp(
                fork,
                knee,
                halfWidth / Mathf.Max(spacing, halfWidth));
            float end = upperStem[1].y;
            float leftEnd = end + ResolveEngravingTerminationYOffset(
                definition,
                -halfWidth);
            float rightEnd = end + ResolveEngravingTerminationYOffset(
                definition,
                halfWidth);
            var vertices = new List<Vector3>(128);
            var triangles = new List<int>(192);
            for (int face = -1; face <= 1; face += 2)
            {
                float z = floorDepth * face;
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-spacing - halfWidth, bottom),
                    new Vector2(-spacing + halfWidth, bottom),
                    new Vector2(-spacing + halfWidth, knee),
                    new Vector2(-spacing - halfWidth, knee), z, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(spacing - halfWidth, bottom),
                    new Vector2(spacing + halfWidth, bottom),
                    new Vector2(spacing + halfWidth, knee),
                    new Vector2(spacing - halfWidth, knee), z, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-spacing - halfWidth, knee),
                    new Vector2(-spacing + halfWidth, knee),
                    new Vector2(0f, merge),
                    new Vector2(-halfWidth * 2f, merge), z, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(spacing - halfWidth, knee),
                    new Vector2(spacing + halfWidth, knee),
                    new Vector2(halfWidth * 2f, merge),
                    new Vector2(0f, merge), z, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-halfWidth * 2f, merge),
                    new Vector2(halfWidth * 2f, merge),
                    new Vector2(halfWidth, fork),
                    new Vector2(-halfWidth, fork), z, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-halfWidth, fork),
                    new Vector2(halfWidth, fork),
                    new Vector2(halfWidth, rightEnd),
                    new Vector2(-halfWidth, leftEnd), z, face);
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static void AppendPlanarQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            float z,
            int face)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(first.x, first.y, z));
            vertices.Add(new Vector3(second.x, second.y, z));
            vertices.Add(new Vector3(third.x, third.y, z));
            vertices.Add(new Vector3(fourth.x, fourth.y, z));
            if (face > 0)
            {
                AddQuad(triangles, start, start + 1, start + 2, start + 3);
            }
            else
            {
                AddQuad(triangles, start + 3, start + 2, start + 1, start);
            }
        }

        private static void AppendMeshGeometry(
            List<Vector3> vertices,
            List<int> triangles,
            Mesh mesh)
        {
            int first = vertices.Count;
            vertices.AddRange(mesh.vertices);
            int[] sourceTriangles = mesh.triangles;
            for (int index = 0; index < sourceTriangles.Length; index++)
            {
                triangles.Add(first + sourceTriangles[index]);
            }
        }

        private static Mesh BuildStraightEngravingFloorMesh(
            ProceduralColumnBladeDefinition definition)
        {
            float bottom = ResolveBladeBottomY(definition);
            float topTransition = ResolveBladeTopTransition(definition);
            float lowestShoulder = bottom + definition.BladeLength -
                definition.TopSlantRise - topTransition;
            float floorEnd = definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Circle
                ? bottom + definition.BladeLength *
                    definition.EngravingEndFraction -
                    ResolveEngravingCircleRadius(definition)
                : ResolveEngravingEndY(definition, bottom);
            float end = definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Full
                ? floorEnd
                : Mathf.Min(
                    floorEnd,
                    lowestShoulder - 0.001f -
                    ResolveMaximumEngravingTerminationOffset(definition));
            float halfWidth = ResolveEngravingWidth(definition) * 0.5f /
                Mathf.Cos(Mathf.PI / EngravingCircleSegments);
            float leftEnd = end + ResolveEngravingTerminationYOffset(
                definition,
                -halfWidth);
            float rightEnd = end + ResolveEngravingTerminationYOffset(
                definition,
                halfWidth);
            float floorDepth = definition.BladeThickness * 0.5f -
                ResolveEngravingDepth(definition) + 0.000025f;
            var vertices = new List<Vector3>(
                definition.EngravingAllFourSides ? 16 : 8);
            var triangles = new List<int>(
                definition.EngravingAllFourSides ? 24 : 12);
            for (int face = -1; face <= 1; face += 2)
            {
                int first = vertices.Count;
                float z = floorDepth * face;
                vertices.Add(new Vector3(-halfWidth, bottom, z));
                vertices.Add(new Vector3(-halfWidth, leftEnd, z));
                vertices.Add(new Vector3(halfWidth, rightEnd, z));
                vertices.Add(new Vector3(halfWidth, bottom, z));
                if (face > 0)
                {
                    AddQuad(triangles, first + 3, first + 2, first + 1, first);
                }
                else
                {
                    AddQuad(triangles, first, first + 1, first + 2, first + 3);
                }
            }
            if (definition.EngravingAllFourSides)
            {
                float sideSurface = definition.BladeWidth * 0.5f;
                float sideFloor = sideSurface -
                    ResolveEngravingDepth(definition) + 0.000025f;
                for (int face = -1; face <= 1; face += 2)
                {
                    int first = vertices.Count;
                    float x = sideFloor * face;
                    float sideEnd = end + ResolveUnclampedTopCutOffset(
                        definition,
                        sideSurface * face);
                    vertices.Add(new Vector3(x, bottom, -halfWidth));
                    vertices.Add(new Vector3(x, sideEnd, -halfWidth));
                    vertices.Add(new Vector3(x, sideEnd, halfWidth));
                    vertices.Add(new Vector3(x, bottom, halfWidth));
                    if (face > 0)
                    {
                        AddQuad(
                            triangles,
                            first,
                            first + 1,
                            first + 2,
                            first + 3);
                    }
                    else
                    {
                        AddQuad(
                            triangles,
                            first + 3,
                            first + 2,
                            first + 1,
                            first);
                    }
                }
            }
            return CreateFlatMesh(vertices, triangles);
        }

        // Circle and line floors use the same standard inlay renderer; the
        // surrounding depth comes from the blade mesh, never a visual mask.
        private static Mesh BuildCircleEngravingFloorMesh(
            ProceduralColumnBladeDefinition definition)
        {
            float bottom = ResolveBladeBottomY(definition);
            float centerY = bottom + definition.BladeLength *
                definition.EngravingEndFraction;
            float radius = ResolveEngravingCircleRadius(definition);
            float floor = definition.BladeThickness * 0.5f -
                ResolveEngravingDepth(definition) + 0.000025f;
            const int circleSegments = EngravingCircleSegments;
            var path = new Vector2[circleSegments];
            for (int index = 0; index < circleSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / circleSegments;
                path[index] = new Vector2(
                    Mathf.Cos(angle) * radius,
                    centerY + Mathf.Sin(angle) * radius);
            }

            var vertices = new List<Vector3>(circleSegments * 8);
            var triangles = new List<int>(circleSegments * 12);
            for (int face = -1; face <= 1; face += 2)
            {
                AppendEngravingBrushFloor(
                    vertices,
                    triangles,
                    path,
                    closed: true,
                    ResolveEngravingWidth(definition),
                    floor * face,
                    face);
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static Mesh CarveCircleEngravingIntoBladeMesh(
            Mesh source,
            ProceduralColumnBladeDefinition definition)
        {
            Vector3[] sourceVertices = source.vertices;
            int[] sourceTriangles = source.triangles;
            float surface = definition.BladeThickness * 0.5f;
            float halfWidth = ResolveEngravingWidth(definition) * 0.5f;
            float wallWidth = Mathf.Max(
                ResolveEngravingWidth(definition) * 0.12f,
                0.00045f);
            float targetEdgeLength = Mathf.Max(wallWidth * 0.55f, 0.00025f);
            float bottom = ResolveBladeBottomY(definition);
            var center = new Vector2(
                0f,
                bottom + definition.BladeLength *
                    definition.EngravingEndFraction);
            float radius = ResolveEngravingCircleRadius(definition);
            float outerRadius = radius + halfWidth + wallWidth;
            var vertices = new List<Vector3>(sourceVertices.Length * 4);
            var triangles = new List<int>(sourceTriangles.Length * 4);

            for (int index = 0; index < sourceTriangles.Length; index += 3)
            {
                Vector3 a = sourceVertices[sourceTriangles[index]];
                Vector3 b = sourceVertices[sourceTriangles[index + 1]];
                Vector3 c = sourceVertices[sourceTriangles[index + 2]];
                if (!TriangleMayTouchCircleStroke(
                        a,
                        b,
                        c,
                        center,
                        radius,
                        outerRadius))
                {
                    AppendTriangle(vertices, triangles, a, b, c);
                    continue;
                }

                AppendCarvedCircleTriangle(
                    vertices,
                    triangles,
                    a,
                    b,
                    c,
                    center,
                    radius,
                    halfWidth,
                    wallWidth,
                    ResolveEngravingDepth(definition),
                    surface,
                    targetEdgeLength,
                    0);
            }

            AppendExactCircleTrenchFinish(
                vertices,
                triangles,
                definition,
                center,
                radius,
                halfWidth,
                wallWidth,
                surface);

            Mesh carved = CreateFlatMesh(vertices, triangles);
            carved.name = source.name;
            return carved;
        }

        private static Mesh CarveForkEngravingIntoBladeMesh(
            Mesh source,
            ProceduralColumnBladeDefinition definition)
        {
            ResolveForkEngravingGeometry(
                definition,
                out Vector2[] leftBranch,
                out Vector2[] rightBranch,
                out Vector2[] upperStem);
            Vector2[][] paths = { leftBranch, rightBranch, upperStem };
            Vector3[] sourceVertices = source.vertices;
            int[] sourceTriangles = source.triangles;
            float surface = definition.BladeThickness * 0.5f;
            float engravingWidth = ResolveEngravingWidth(definition);
            float visibleHalfWidth = engravingWidth * 0.5f;
            // Cut a deliberately oversized hidden void. The exact floor,
            // walls, and blade-material cover define the visible channel;
            // this clearance margin prevents any sampled support triangle
            // from poking back through their perfectly straight edges.
            float clearanceHalfWidth = visibleHalfWidth +
                engravingWidth * 0.80f;
            float clearanceWallWidth = engravingWidth * 0.45f;
            float reach = clearanceHalfWidth + clearanceWallWidth;
            float targetEdgeLength = Mathf.Max(
                engravingWidth * 0.34f,
                0.002f);
            var vertices = new List<Vector3>(sourceVertices.Length * 5);
            var triangles = new List<int>(sourceTriangles.Length * 5);

            for (int index = 0; index < sourceTriangles.Length; index += 3)
            {
                AppendCarvedPathTriangle(
                    vertices,
                    triangles,
                    sourceVertices[sourceTriangles[index]],
                    sourceVertices[sourceTriangles[index + 1]],
                    sourceVertices[sourceTriangles[index + 2]],
                    paths,
                    reach,
                    clearanceHalfWidth,
                    clearanceWallWidth,
                    ResolveEngravingDepth(definition),
                    surface,
                    targetEdgeLength,
                    0);
            }

            AppendExactForkTrenchFinish(
                vertices,
                triangles,
                definition,
                leftBranch,
                upperStem,
                surface);

            Mesh carved = CreateFlatMesh(vertices, triangles);
            carved.name = source.name;
            return carved;
        }

        private static void AppendExactForkTrenchFinish(
            List<Vector3> vertices,
            List<int> triangles,
            ProceduralColumnBladeDefinition definition,
            IReadOnlyList<Vector2> leftBranch,
            IReadOnlyList<Vector2> upperStem,
            float surface)
        {
            float halfWidth = ResolveEngravingWidth(definition) * 0.5f;
            float spacing = Mathf.Abs(leftBranch[0].x);
            float bottom = leftBranch[0].y;
            float knee = leftBranch[1].y;
            float fork = leftBranch[2].y;
            float merge = Mathf.Lerp(
                fork,
                knee,
                halfWidth / Mathf.Max(spacing, halfWidth));
            float end = upperStem[1].y;
            float leftEnd = end + ResolveEngravingTerminationYOffset(
                definition,
                -halfWidth);
            float rightEnd = end + ResolveEngravingTerminationYOffset(
                definition,
                halfWidth);
            // End the replacement surface at the blade's natural face edge,
            // never in the middle of the otherwise flat face.
            float patchHalfWidth = definition.BladeCoreWidth * 0.5f;
            float leftPatchEnd = definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Full
                ? end + ResolveUnclampedTopCutOffset(
                    definition,
                    -patchHalfWidth)
                : leftEnd;
            float rightPatchEnd = definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Full
                ? end + ResolveUnclampedTopCutOffset(
                    definition,
                    patchHalfWidth)
                : rightEnd;
            float floor = surface - ResolveEngravingDepth(definition);
            float faceOffset = 0.000004f;

            for (int face = -1; face <= 1; face += 2)
            {
                float surfaceZ = (surface + faceOffset) * face;
                float floorZ = floor * face;

                // Exact blade-material patches conceal the coarse clearance
                // cut. Their boundaries are the actual visible groove edges.
                AppendForkSurfaceBand(
                    vertices, triangles, patchHalfWidth,
                    -spacing - halfWidth, -spacing + halfWidth,
                    spacing - halfWidth, spacing + halfWidth,
                    bottom, knee, surfaceZ, face);

                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-patchHalfWidth, knee),
                    new Vector2(-spacing - halfWidth, knee),
                    new Vector2(-halfWidth * 2f, merge),
                    new Vector2(-patchHalfWidth, merge), surfaceZ, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-spacing + halfWidth, knee),
                    new Vector2(spacing - halfWidth, knee),
                    new Vector2(0f, merge),
                    new Vector2(0f, merge), surfaceZ, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(spacing + halfWidth, knee),
                    new Vector2(patchHalfWidth, knee),
                    new Vector2(patchHalfWidth, merge),
                    new Vector2(halfWidth * 2f, merge), surfaceZ, face);

                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-patchHalfWidth, merge),
                    new Vector2(-halfWidth * 2f, merge),
                    new Vector2(-halfWidth, fork),
                    new Vector2(-patchHalfWidth, fork), surfaceZ, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(halfWidth * 2f, merge),
                    new Vector2(patchHalfWidth, merge),
                    new Vector2(patchHalfWidth, fork),
                    new Vector2(halfWidth, fork), surfaceZ, face);

                AppendPlanarQuad(vertices, triangles,
                    new Vector2(-patchHalfWidth, fork),
                    new Vector2(-halfWidth, fork),
                    new Vector2(-halfWidth, leftEnd),
                    new Vector2(-patchHalfWidth, leftPatchEnd), surfaceZ, face);
                AppendPlanarQuad(vertices, triangles,
                    new Vector2(halfWidth, fork),
                    new Vector2(patchHalfWidth, fork),
                    new Vector2(patchHalfWidth, rightPatchEnd),
                    new Vector2(halfWidth, rightEnd), surfaceZ, face);

                var leftOuter = new[]
                {
                    new Vector2(-spacing - halfWidth, bottom),
                    new Vector2(-spacing - halfWidth, knee),
                    new Vector2(-halfWidth * 2f, merge),
                    new Vector2(-halfWidth, fork),
                    new Vector2(-halfWidth, leftEnd)
                };
                var rightOuter = new[]
                {
                    new Vector2(spacing + halfWidth, bottom),
                    new Vector2(spacing + halfWidth, knee),
                    new Vector2(halfWidth * 2f, merge),
                    new Vector2(halfWidth, fork),
                    new Vector2(halfWidth, rightEnd)
                };
                var leftInner = new[]
                {
                    new Vector2(-spacing + halfWidth, bottom),
                    new Vector2(-spacing + halfWidth, knee),
                    new Vector2(0f, merge)
                };
                var rightInner = new[]
                {
                    new Vector2(spacing - halfWidth, bottom),
                    new Vector2(spacing - halfWidth, knee),
                    new Vector2(0f, merge)
                };
                AppendForkWallPath(
                    vertices, triangles, leftOuter, surfaceZ, floorZ);
                AppendForkWallPath(
                    vertices, triangles, rightOuter, surfaceZ, floorZ);
                AppendForkWallPath(
                    vertices, triangles, leftInner, surfaceZ, floorZ);
                AppendForkWallPath(
                    vertices, triangles, rightInner, surfaceZ, floorZ);

                if (definition.EngravingTermination !=
                        ColumnBladeEngravingTermination.Circle &&
                    definition.EngravingTermination !=
                        ColumnBladeEngravingTermination.Full)
                {
                    AppendDoubleSidedWallQuad(
                        vertices,
                        triangles,
                        new Vector2(-halfWidth, leftEnd),
                        new Vector2(halfWidth, rightEnd),
                        surfaceZ,
                        floorZ);
                    float shoulder = bottom + definition.BladeLength -
                        definition.TopSlantRise -
                        ResolveBladeTopTransition(definition);
                    float capTop = Mathf.Min(
                        shoulder - 0.0005f,
                        Mathf.Max(leftEnd, rightEnd) +
                        // The hidden clearance cut reaches 1.75 widths past
                        // an open path endpoint. Cover substantially beyond
                        // that radius so none of its sampled triangles can
                        // appear above or beside the exact square end wall.
                        ResolveEngravingWidth(definition) * 3f);
                    if (capTop > Mathf.Max(leftEnd, rightEnd) + 0.000001f)
                    {
                        AppendPlanarQuad(vertices, triangles,
                            new Vector2(-patchHalfWidth, leftEnd),
                            new Vector2(patchHalfWidth, rightEnd),
                            new Vector2(patchHalfWidth, capTop),
                            new Vector2(-patchHalfWidth, capTop),
                            surfaceZ,
                            face);
                    }
                }
            }
        }

        private static void AppendForkSurfaceBand(
            List<Vector3> vertices,
            List<int> triangles,
            float patchHalfWidth,
            float leftOuter,
            float leftInner,
            float rightInner,
            float rightOuter,
            float bottom,
            float top,
            float z,
            int face)
        {
            if (top <= bottom + 0.000001f)
            {
                return;
            }
            AppendPlanarQuad(vertices, triangles,
                new Vector2(-patchHalfWidth, bottom),
                new Vector2(leftOuter, bottom),
                new Vector2(leftOuter, top),
                new Vector2(-patchHalfWidth, top), z, face);
            AppendPlanarQuad(vertices, triangles,
                new Vector2(leftInner, bottom),
                new Vector2(rightInner, bottom),
                new Vector2(rightInner, top),
                new Vector2(leftInner, top), z, face);
            AppendPlanarQuad(vertices, triangles,
                new Vector2(rightOuter, bottom),
                new Vector2(patchHalfWidth, bottom),
                new Vector2(patchHalfWidth, top),
                new Vector2(rightOuter, top), z, face);
        }

        private static void AppendForkWallPath(
            List<Vector3> vertices,
            List<int> triangles,
            IReadOnlyList<Vector2> path,
            float surfaceZ,
            float floorZ)
        {
            for (int index = 0; index < path.Count - 1; index++)
            {
                AppendDoubleSidedWallQuad(
                    vertices,
                    triangles,
                    path[index],
                    path[index + 1],
                    surfaceZ,
                    floorZ);
            }
        }

        private static void AppendCarvedPathTriangle(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            IReadOnlyList<Vector2[]> paths,
            float reach,
            float halfWidth,
            float wallWidth,
            float depth,
            float surface,
            float targetEdgeLength,
            int subdivisionDepth)
        {
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            if (Mathf.Abs(faceNormal.z) <
                faceNormal.magnitude * 0.90f)
            {
                // Clearance belongs only on the broad blade faces. Deforming
                // the top cap was the source of the broken top silhouette.
                AppendTriangle(vertices, triangles, a, b, c);
                return;
            }
            if (!TriangleMayTouchPaths(a, b, c, paths, reach))
            {
                AppendTriangle(vertices, triangles, a, b, c);
                return;
            }

            float ab = PlanarSquaredDistance(a, b);
            float bc = PlanarSquaredDistance(b, c);
            float ca = PlanarSquaredDistance(c, a);
            float targetSquared = targetEdgeLength * targetEdgeLength;
            if (subdivisionDepth < 26 &&
                Mathf.Max(ab, Mathf.Max(bc, ca)) > targetSquared)
            {
                if (ab >= bc && ab >= ca)
                {
                    Vector3 midpoint = (a + b) * 0.5f;
                    AppendCarvedPathTriangle(vertices, triangles, a, midpoint, c,
                        paths, reach, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                    AppendCarvedPathTriangle(vertices, triangles, midpoint, b, c,
                        paths, reach, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                }
                else if (bc >= ca)
                {
                    Vector3 midpoint = (b + c) * 0.5f;
                    AppendCarvedPathTriangle(vertices, triangles, a, b, midpoint,
                        paths, reach, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                    AppendCarvedPathTriangle(vertices, triangles, a, midpoint, c,
                        paths, reach, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                }
                else
                {
                    Vector3 midpoint = (c + a) * 0.5f;
                    AppendCarvedPathTriangle(vertices, triangles, a, b, midpoint,
                        paths, reach, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                    AppendCarvedPathTriangle(vertices, triangles, midpoint, b, c,
                        paths, reach, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                }
                return;
            }

            AppendTriangle(
                vertices,
                triangles,
                RecessPathVertex(a, paths, halfWidth, wallWidth, depth, surface),
                RecessPathVertex(b, paths, halfWidth, wallWidth, depth, surface),
                RecessPathVertex(c, paths, halfWidth, wallWidth, depth, surface));
        }

        private static Vector3 RecessPathVertex(
            Vector3 point,
            IReadOnlyList<Vector2[]> paths,
            float halfWidth,
            float wallWidth,
            float depth,
            float surface)
        {
            float pathDistance = DistanceToEngravingPaths(
                new Vector2(point.x, point.y),
                paths);
            float recessedDepth = pathDistance <= halfWidth
                ? depth
                : depth * Mathf.Clamp01(
                    1f - (pathDistance - halfWidth) / wallWidth);
            if (recessedDepth <= 0f)
            {
                return point;
            }
            float targetSurface = surface - recessedDepth;
            if (Mathf.Abs(point.z) > targetSurface)
            {
                point.z = Mathf.Sign(point.z) * targetSurface;
            }
            return point;
        }

        private static bool TriangleMayTouchPaths(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            IReadOnlyList<Vector2[]> paths,
            float reach)
        {
            float minimumX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - reach;
            float maximumX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + reach;
            float minimumY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - reach;
            float maximumY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + reach;
            foreach (Vector2[] path in paths)
            {
                for (int index = 0; index < path.Length - 1; index++)
                {
                    Vector2 first = path[index];
                    Vector2 second = path[index + 1];
                    if (Mathf.Min(first.x, second.x) <= maximumX &&
                        Mathf.Max(first.x, second.x) >= minimumX &&
                        Mathf.Min(first.y, second.y) <= maximumY &&
                        Mathf.Max(first.y, second.y) >= minimumY)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float DistanceToEngravingPaths(
            Vector2 point,
            IReadOnlyList<Vector2[]> paths)
        {
            float closest = float.PositiveInfinity;
            foreach (Vector2[] path in paths)
            {
                for (int index = 0; index < path.Length - 1; index++)
                {
                    Vector2 start = path[index];
                    Vector2 delta = path[index + 1] - start;
                    float lengthSquared = delta.sqrMagnitude;
                    float t = lengthSquared > 0.00000001f
                        ? Mathf.Clamp01(Vector2.Dot(point - start, delta) /
                            lengthSquared)
                        : 0f;
                    closest = Mathf.Min(
                        closest,
                        Vector2.Distance(point, start + delta * t));
                }
            }
            return closest;
        }

        private static void ResolveForkEngravingGeometry(
            ProceduralColumnBladeDefinition definition,
            out Vector2[] leftBranch,
            out Vector2[] rightBranch,
            out Vector2[] upperStem)
        {
            float bottom = ResolveBladeBottomY(definition);
            float forkY = bottom + definition.BladeLength *
                definition.EngravingForkFraction;
            float diagonalHeight = Mathf.Clamp(
                definition.BladeLength * 0.065f,
                0.042f,
                0.060f);
            float kneeY = Mathf.Max(bottom, forkY - diagonalHeight);
            float end = definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Circle
                ? bottom + definition.BladeLength *
                    definition.EngravingEndFraction -
                    ResolveEngravingCircleRadius(definition)
                : ResolveEngravingEndY(definition, bottom);
            float shoulder = bottom + definition.BladeLength -
                definition.TopSlantRise - ResolveBladeTopTransition(definition);
            if (definition.EngravingTermination !=
                ColumnBladeEngravingTermination.Full)
            {
                end = Mathf.Min(
                    end,
                    shoulder - 0.001f -
                    ResolveMaximumEngravingTerminationOffset(definition));
            }
            forkY = Mathf.Min(forkY, end - diagonalHeight * 0.5f);
            kneeY = Mathf.Min(kneeY, forkY);
            float spacing = definition.EngravingForkHalfSpacing;
            leftBranch = new[]
            {
                new Vector2(-spacing, bottom),
                new Vector2(-spacing, kneeY),
                new Vector2(0f, forkY)
            };
            rightBranch = new[]
            {
                new Vector2(spacing, bottom),
                new Vector2(spacing, kneeY),
                new Vector2(0f, forkY)
            };
            upperStem = new[]
            {
                new Vector2(0f, forkY),
                new Vector2(0f, end)
            };
        }

        private static void AppendExactCircleTrenchFinish(
            List<Vector3> vertices,
            List<int> triangles,
            ProceduralColumnBladeDefinition definition,
            Vector2 center,
            float radius,
            float brushOffset,
            float wallWidth,
            float surface)
        {
            float innerRadius = radius - brushOffset;
            float outerRadius = radius + brushOffset;
            // The adaptive cut is intentionally hidden beneath this ordinary
            // blade-material surface. Its generous reach covers the long
            // support triangles that can extend beyond the exact outer wall;
            // it does not change the visible groove radius or gold floor.
            float collarRadius = outerRadius +
                ResolveEngravingWidth(definition) * 1.25f;
            float floor = surface - ResolveEngravingDepth(definition);
            float faceOffset = 0.000004f;
            float halfLineWidth = ResolveEngravingWidth(definition) * 0.5f;

            for (int face = -1; face <= 1; face += 2)
            {
                float surfaceZ = (surface + faceOffset) * face;
                float floorZ = floor * face;

                // The untouched center disk supplies an exact circular inner
                // edge instead of exposing a sampled height-field contour.
                int diskCenter = vertices.Count;
                vertices.Add(new Vector3(center.x, center.y, surfaceZ));
                int diskRing = vertices.Count;
                for (int index = 0;
                     index < EngravingCircleSegments;
                     index++)
                {
                    float angle = index * Mathf.PI * 2f /
                        EngravingCircleSegments;
                    vertices.Add(new Vector3(
                        center.x + Mathf.Cos(angle) * innerRadius,
                        center.y + Mathf.Sin(angle) * innerRadius,
                        surfaceZ));
                }
                for (int index = 0;
                     index < EngravingCircleSegments;
                     index++)
                {
                    int next = (index + 1) % EngravingCircleSegments;
                    if (face > 0)
                    {
                        triangles.Add(diskCenter);
                        triangles.Add(diskRing + index);
                        triangles.Add(diskRing + next);
                    }
                    else
                    {
                        triangles.Add(diskCenter);
                        triangles.Add(diskRing + next);
                        triangles.Add(diskRing + index);
                    }
                }

                for (int index = 0;
                     index < EngravingCircleSegments;
                     index++)
                {
                    int next = (index + 1) % EngravingCircleSegments;
                    float angle = index * Mathf.PI * 2f /
                        EngravingCircleSegments;
                    float nextAngle = next * Mathf.PI * 2f /
                        EngravingCircleSegments;
                    float middleAngle = (index + 0.5f) * Mathf.PI * 2f /
                        EngravingCircleSegments;
                    Vector2 innerFirst = center + new Vector2(
                        Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius;
                    Vector2 innerSecond = center + new Vector2(
                        Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)) *
                        innerRadius;
                    Vector2 outerFirst = center + new Vector2(
                        Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
                    Vector2 outerSecond = center + new Vector2(
                        Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)) *
                        outerRadius;
                    Vector2 collarFirst = center + new Vector2(
                        Mathf.Cos(angle), Mathf.Sin(angle)) * collarRadius;
                    Vector2 collarSecond = center + new Vector2(
                        Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)) *
                        collarRadius;

                    AppendDoubleSidedWallQuad(
                        vertices,
                        triangles,
                        innerFirst,
                        innerSecond,
                        surfaceZ,
                        floorZ);

                    Vector2 middle = center + new Vector2(
                        Mathf.Cos(middleAngle),
                        Mathf.Sin(middleAngle)) * outerRadius;
                    bool lineOpening = middle.y < center.y &&
                        Mathf.Abs(middle.x - center.x) <=
                            halfLineWidth * 1.08f;
                    if (lineOpening)
                    {
                        continue;
                    }

                    AppendDoubleSidedWallQuad(
                        vertices,
                        triangles,
                        outerFirst,
                        outerSecond,
                        surfaceZ,
                        floorZ);
                    int collarStart = vertices.Count;
                    vertices.Add(new Vector3(
                        outerFirst.x, outerFirst.y, surfaceZ));
                    vertices.Add(new Vector3(
                        outerSecond.x, outerSecond.y, surfaceZ));
                    vertices.Add(new Vector3(
                        collarSecond.x, collarSecond.y, surfaceZ));
                    vertices.Add(new Vector3(
                        collarFirst.x, collarFirst.y, surfaceZ));
                    if (face > 0)
                    {
                        AddQuad(
                            triangles,
                            collarStart + 3,
                            collarStart + 2,
                            collarStart + 1,
                            collarStart);
                    }
                    else
                    {
                        AddQuad(
                            triangles,
                            collarStart,
                            collarStart + 1,
                            collarStart + 2,
                            collarStart + 3);
                    }
                }

                AppendCircleLineJunctionSurface(
                    vertices,
                    triangles,
                    center,
                    outerRadius,
                    halfLineWidth,
                    ResolveEngravingWidth(definition),
                    surfaceZ);
            }
        }

        private static void AppendCircleLineJunctionSurface(
            List<Vector3> vertices,
            List<int> triangles,
            Vector2 center,
            float outerRadius,
            float halfLineWidth,
            float lineWidth,
            float surfaceZ)
        {
            const int joinSegments = 16;
            float maximumX = Mathf.Min(
                halfLineWidth + lineWidth * 2f,
                outerRadius * 0.82f);
            float bottomY = center.y - outerRadius - lineWidth * 1.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < joinSegments; index++)
                {
                    float firstDistance = Mathf.Lerp(
                        halfLineWidth,
                        maximumX,
                        index / (float)joinSegments);
                    float secondDistance = Mathf.Lerp(
                        halfLineWidth,
                        maximumX,
                        (index + 1f) / joinSegments);
                    float firstX = center.x + firstDistance * side;
                    float secondX = center.x + secondDistance * side;
                    float firstArcY = center.y - Mathf.Sqrt(Mathf.Max(
                        0f,
                        outerRadius * outerRadius -
                        firstDistance * firstDistance));
                    float secondArcY = center.y - Mathf.Sqrt(Mathf.Max(
                        0f,
                        outerRadius * outerRadius -
                        secondDistance * secondDistance));
                    int start = vertices.Count;
                    vertices.Add(new Vector3(firstX, bottomY, surfaceZ));
                    vertices.Add(new Vector3(secondX, bottomY, surfaceZ));
                    vertices.Add(new Vector3(secondX, secondArcY, surfaceZ));
                    vertices.Add(new Vector3(firstX, firstArcY, surfaceZ));
                    AddQuad(triangles, start, start + 1, start + 2, start + 3);
                    AddQuad(triangles, start + 3, start + 2, start + 1, start);
                }
            }
        }

        private static void AppendDoubleSidedWallQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector2 first,
            Vector2 second,
            float surfaceZ,
            float floorZ)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(first.x, first.y, surfaceZ));
            vertices.Add(new Vector3(second.x, second.y, surfaceZ));
            vertices.Add(new Vector3(second.x, second.y, floorZ));
            vertices.Add(new Vector3(first.x, first.y, floorZ));
            AddQuad(triangles, start, start + 1, start + 2, start + 3);
            AddQuad(triangles, start + 3, start + 2, start + 1, start);
        }

        private static void AppendCarvedCircleTriangle(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector2 center,
            float radius,
            float halfWidth,
            float wallWidth,
            float depth,
            float surface,
            float targetEdgeLength,
            int subdivisionDepth)
        {
            float outerRadius = radius + halfWidth + wallWidth;
            if (!TriangleMayTouchCircleStroke(
                    a,
                    b,
                    c,
                    center,
                    radius,
                    outerRadius))
            {
                AppendTriangle(vertices, triangles, a, b, c);
                return;
            }

            float ab = PlanarSquaredDistance(a, b);
            float bc = PlanarSquaredDistance(b, c);
            float ca = PlanarSquaredDistance(c, a);
            float targetSquared = targetEdgeLength * targetEdgeLength;
            if (subdivisionDepth < 26 &&
                Mathf.Max(ab, Mathf.Max(bc, ca)) > targetSquared)
            {
                if (ab >= bc && ab >= ca)
                {
                    Vector3 midpoint = (a + b) * 0.5f;
                    AppendCarvedCircleTriangle(vertices, triangles, a, midpoint, c,
                        center, radius, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                    AppendCarvedCircleTriangle(vertices, triangles, midpoint, b, c,
                        center, radius, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                }
                else if (bc >= ca)
                {
                    Vector3 midpoint = (b + c) * 0.5f;
                    AppendCarvedCircleTriangle(vertices, triangles, a, b, midpoint,
                        center, radius, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                    AppendCarvedCircleTriangle(vertices, triangles, a, midpoint, c,
                        center, radius, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                }
                else
                {
                    Vector3 midpoint = (c + a) * 0.5f;
                    AppendCarvedCircleTriangle(vertices, triangles, a, b, midpoint,
                        center, radius, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                    AppendCarvedCircleTriangle(vertices, triangles, midpoint, b, c,
                        center, radius, halfWidth, wallWidth, depth, surface,
                        targetEdgeLength, subdivisionDepth + 1);
                }
                return;
            }

            AppendTriangle(
                vertices,
                triangles,
                RecessCircleVertex(a, center, radius, halfWidth, wallWidth,
                    depth, surface),
                RecessCircleVertex(b, center, radius, halfWidth, wallWidth,
                    depth, surface),
                RecessCircleVertex(c, center, radius, halfWidth, wallWidth,
                    depth, surface));
        }

        private static Vector3 RecessCircleVertex(
            Vector3 point,
            Vector2 center,
            float radius,
            float halfWidth,
            float wallWidth,
            float depth,
            float surface)
        {
            float pathDistance = Mathf.Abs(
                Vector2.Distance(new Vector2(point.x, point.y), center) - radius);
            float recessedDepth = pathDistance <= halfWidth
                ? depth
                : depth * Mathf.Clamp01(
                    1f - (pathDistance - halfWidth) / wallWidth);
            if (recessedDepth <= 0f)
            {
                return point;
            }
            float targetSurface = surface - recessedDepth;
            if (Mathf.Abs(point.z) > targetSurface)
            {
                point.z = Mathf.Sign(point.z) * targetSurface;
            }
            return point;
        }

        private static bool TriangleMayTouchCircleStroke(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector2 center,
            float radius,
            float outerRadius)
        {
            float minimumX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            float maximumX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minimumY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float maximumY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            float closestX = Mathf.Clamp(center.x, minimumX, maximumX);
            float closestY = Mathf.Clamp(center.y, minimumY, maximumY);
            float minimumRadius = Vector2.Distance(
                center,
                new Vector2(closestX, closestY));
            float maximumRadius = Mathf.Max(
                Vector2.Distance(center, new Vector2(minimumX, minimumY)),
                Mathf.Max(
                    Vector2.Distance(center, new Vector2(minimumX, maximumY)),
                    Mathf.Max(
                        Vector2.Distance(center, new Vector2(maximumX, minimumY)),
                        Vector2.Distance(center, new Vector2(maximumX, maximumY)))));
            float innerRadius = Mathf.Max(0f, radius - (outerRadius - radius));
            return minimumRadius <= outerRadius && maximumRadius >= innerRadius;
        }

        private static float PlanarSquaredDistance(Vector3 first, Vector3 second)
        {
            float x = first.x - second.x;
            float y = first.y - second.y;
            return x * x + y * y;
        }

        private static void AppendTriangle(
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

        private void CreateEngravingChild(
            GameObject bladePart,
            string name,
            Mesh mesh,
            Material material,
            Color color,
            float metallic,
            float smoothness,
            ColumnBladeTextureTransform? textureTransform)
        {
            mesh.name = $"Procedural {name} {currentDefinition.Seed}";
            generatedMeshes.Add(mesh);
            var child = new GameObject(name);
            child.transform.SetParent(bladePart.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            ApplyRendererProperties(
                renderer,
                color,
                metallic,
                smoothness,
                textureTransform);
        }

        private static void AppendEngravingBrushFloor(
            List<Vector3> vertices,
            List<int> triangles,
            IReadOnlyList<Vector2> path,
            bool closed,
            float width,
            float z,
            int face)
        {
            Vector2[] offsets = ResolveEngravingBrushOffsets(
                path,
                closed,
                width * 0.5f);
            int segmentCount = closed ? path.Count : path.Count - 1;
            for (int index = 0; index < segmentCount; index++)
            {
                int next = (index + 1) % path.Count;
                int first = vertices.Count;
                vertices.Add(new Vector3(
                    path[index].x + offsets[index].x,
                    path[index].y + offsets[index].y,
                    z));
                vertices.Add(new Vector3(
                    path[next].x + offsets[next].x,
                    path[next].y + offsets[next].y,
                    z));
                vertices.Add(new Vector3(
                    path[next].x - offsets[next].x,
                    path[next].y - offsets[next].y,
                    z));
                vertices.Add(new Vector3(
                    path[index].x - offsets[index].x,
                    path[index].y - offsets[index].y,
                    z));
                if (face > 0)
                {
                    AddQuad(
                        triangles,
                        first + 3,
                        first + 2,
                        first + 1,
                        first);
                }
                else
                {
                    AddQuad(
                        triangles,
                        first,
                        first + 1,
                        first + 2,
                        first + 3);
                }
            }
        }

        private static Vector2[] ResolveEngravingBrushOffsets(
            IReadOnlyList<Vector2> path,
            bool closed,
            float halfWidth)
        {
            var offsets = new Vector2[path.Count];
            for (int index = 0; index < path.Count; index++)
            {
                int previous = index > 0
                    ? index - 1
                    : closed ? path.Count - 1 : index;
                int next = index < path.Count - 1
                    ? index + 1
                    : closed ? 0 : index;
                Vector2 incoming = index == previous
                    ? (path[next] - path[index]).normalized
                    : (path[index] - path[previous]).normalized;
                Vector2 outgoing = index == next
                    ? incoming
                    : (path[next] - path[index]).normalized;
                Vector2 firstNormal = new Vector2(-incoming.y, incoming.x);
                Vector2 secondNormal = new Vector2(-outgoing.y, outgoing.x);
                Vector2 miter = firstNormal + secondNormal;
                if (miter.sqrMagnitude <= 0.000001f)
                {
                    miter = secondNormal;
                }
                miter.Normalize();
                float denominator = Mathf.Max(
                    0.35f,
                    Mathf.Abs(Vector2.Dot(miter, secondNormal)));
                offsets[index] = miter * (halfWidth / denominator);
            }
            return offsets;
        }

        private Material ResolveConfiguredBladeMaterial(
            ColumnBladeMaterial material)
        {
            return material switch
            {
                ColumnBladeMaterial.Wood =>
                    woodBladeMaterial != null
                        ? woodBladeMaterial
                        : bladeMaterial,
                ColumnBladeMaterial.Obsidian =>
                    obsidianBladeMaterial != null
                        ? obsidianBladeMaterial
                        : bladeMaterial,
                _ => bladeMaterial
            };
        }

        private void CreateShortSwordFurniture(
            ProceduralColumnBladeDefinition definition)
        {
            furnitureSource = new GameObject("Column Blade Furniture Source");
            furnitureSource.transform.SetParent(transform, false);
            var sourceGenerator =
                furnitureSource.AddComponent<ProceduralShortSwordGenerator>();
            sourceGenerator.SetUseColumnFurnitureStandard(false);
            sourceGenerator.ConfigureMaterials(
                furnitureMaterial,
                furnitureMaterial,
                accentMaterial,
                furnitureMaterial);
            ProceduralShortSwordDefinition source =
                sourceGenerator.Generate(definition.Seed);

            GameObject handle = null;
            GameObject pommel = null;
            foreach (GameObject part in sourceGenerator.GeneratedParts)
            {
                if (part.name == ProceduralShortSwordGenerator.BladePartName ||
                    part.name == ProceduralShortSwordGenerator.GuardPartName)
                {
                    part.SetActive(false);
                    continue;
                }
                if (part.name == ProceduralShortSwordGenerator.HandlePartName)
                {
                    handle = part;
                }
                else if (part.name == ProceduralShortSwordGenerator.HiltPartName)
                {
                    pommel = part;
                }
            }

            float scale = definition.BladeLength / source.BladeLength;
            float sourceHandleTop =
                ProceduralShortSwordGenerator.ResolveHandleSeatHeight(source);
            float targetHandleTop = ResolveHandleTopY(definition);
            furnitureSource.transform.localScale = new Vector3(
                definition.FurnitureRadialScale,
                scale,
                definition.FurnitureRadialScale);
            furnitureSource.transform.localPosition = new Vector3(
                0f,
                targetHandleTop - sourceHandleTop * scale,
                0f);

            if (handle != null)
            {
                handle.name = HandlePartName;
                generatedParts.Add(handle);
            }
            if (pommel != null)
            {
                pommel.name = PommelPartName;
                generatedParts.Add(pommel);
            }
        }

        private static Mesh BuildBladeMesh(
            ProceduralColumnBladeDefinition definition)
        {
            float bottom = ResolveBladeBottomY(definition);
            IReadOnlyList<Vector2> crossSection =
                BuildBladeCrossSection(definition);
            float topTransition = ResolveBladeTopTransition(definition);
            if (definition.BladeMaterial == ColumnBladeMaterial.Stone &&
                definition.PrimaryEngraving !=
                    ColumnBladeEngravingStyle.SilhouetteInset)
            {
                crossSection = AddStoneChipSupportPoints(
                    crossSection,
                    definition);
            }
            IReadOnlyList<Vector2> normalCrossSection = crossSection;
            IReadOnlyList<Vector2> engravedCrossSection = crossSection;
            if (definition.PrimaryEngraving ==
                ColumnBladeEngravingStyle.SilhouetteInset)
            {
                normalCrossSection = AddSilhouetteInsetSupportPoints(
                    crossSection,
                    definition,
                    out int[] recessedIndices);
                engravedCrossSection = RecessSilhouetteInsetFloor(
                    normalCrossSection,
                    recessedIndices,
                    ResolveEngravingDepth(definition));
            }
            else if (definition.PrimaryEngraving ==
                    ColumnBladeEngravingStyle.StraightLine &&
                (definition.EngravingPath ==
                     ColumnBladeEngravingPath.Single ||
                 definition.EngravingTermination ==
                     ColumnBladeEngravingTermination.Full))
            {
                normalCrossSection = AddEngravingSupportPoints(
                    crossSection,
                    definition,
                    out int[] broadFaceRecessedIndices,
                    out int[] sideFaceRecessedIndices);
                engravedCrossSection = RecessEngravingFloor(
                    normalCrossSection,
                    broadFaceRecessedIndices,
                    sideFaceRecessedIndices,
                    ResolveEngravingDepth(definition));
            }
            if (definition.BladeMaterial == ColumnBladeMaterial.Stone)
            {
                return BuildFlatStoneBladeMesh(
                    definition,
                    normalCrossSection,
                    engravedCrossSection,
                    bottom,
                    topTransition);
            }
            return BuildSlantedBladePrism(
                definition,
                normalCrossSection,
                engravedCrossSection,
                bottom,
                bottom + definition.BladeLength,
                topTransition);
        }

        public static float ResolveEngravingDepth(
            ProceduralColumnBladeDefinition definition)
        {
            float safeMaximum = Mathf.Min(
                0.012f,
                definition.BladeThickness * 0.38f);
            return Mathf.Min(
                safeMaximum,
                Mathf.Max(
                    0.004f,
                    definition.BladeThickness * 0.28f));
        }

        public static float ResolveEngravingWidth(
            ProceduralColumnBladeDefinition definition)
        {
            float minimumWidth = Mathf.Clamp(
                definition.BladeWidth * 0.073125f,
                0.00585f,
                0.00975f);
            float scale = Mathf.Max(1f, definition.EngravingWidthScale);
            return Mathf.Min(
                minimumWidth * scale,
                definition.BladeCoreWidth * 0.22f);
        }

        public static float ResolveEngravingCircleRadius(
            ProceduralColumnBladeDefinition definition)
        {
            return Mathf.Clamp(
                definition.BladeWidth * 0.25f,
                0.016f,
                0.030f);
        }

        private static bool IsSilhouetteInsetEdge(
            ProceduralColumnBladeDefinition definition,
            Vector2 first,
            Vector2 second,
            float margin)
        {
            Vector2 delta = second - first;
            if (delta.magnitude <= margin * 2f + 0.0001f)
            {
                return false;
            }
            return definition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock ||
                Mathf.Abs(delta.y) < 0.00001f;
        }

        private static IReadOnlyList<Vector2>
            AddSilhouetteInsetSupportPoints(
                IReadOnlyList<Vector2> outline,
                ProceduralColumnBladeDefinition definition,
                out int[] recessedIndices)
        {
            float margin = ResolveSilhouetteInsetMargin(definition);
            float wallRun = ResolveSilhouetteWallRun(definition);
            var result = new List<Vector2>(outline.Count * 5);
            var floors = new List<int>(outline.Count * 2);
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 current = outline[index];
                Vector2 next = outline[(index + 1) % outline.Count];
                result.Add(current);
                if (!IsSilhouetteInsetEdge(
                        definition,
                        current,
                        next,
                        margin))
                {
                    continue;
                }

                Vector2 direction = (next - current).normalized;
                Vector2 lipFirst = current + direction * margin;
                Vector2 floorFirst = lipFirst + direction * wallRun;
                Vector2 floorSecond = next - direction *
                    (margin + wallRun);
                Vector2 lipSecond = next - direction * margin;
                result.Add(lipFirst);
                floors.Add(result.Count);
                result.Add(floorFirst);
                floors.Add(result.Count);
                result.Add(floorSecond);
                result.Add(lipSecond);
            }
            recessedIndices = floors.ToArray();
            return result;
        }

        private static IReadOnlyList<Vector2> RecessSilhouetteInsetFloor(
            IReadOnlyList<Vector2> outline,
            IReadOnlyList<int> recessedIndices,
            float depth)
        {
            var result = new Vector2[outline.Count];
            for (int index = 0; index < outline.Count; index++)
            {
                result[index] = outline[index];
            }
            for (int pair = 0; pair + 1 < recessedIndices.Count; pair += 2)
            {
                int firstIndex = recessedIndices[pair];
                int secondIndex = recessedIndices[pair + 1];
                Vector2 direction = (outline[secondIndex] -
                    outline[firstIndex]).normalized;
                Vector2 inward = new Vector2(-direction.y, direction.x);
                result[firstIndex] += inward * depth;
                result[secondIndex] += inward * depth;
            }
            return result;
        }

        private static IReadOnlyList<Vector2> AddEngravingSupportPoints(
            IReadOnlyList<Vector2> outline,
            ProceduralColumnBladeDefinition definition,
            out int[] broadFaceRecessedIndices,
            out int[] sideFaceRecessedIndices)
        {
            float halfChannel = ResolveEngravingWidth(definition) * 0.5f;
            var result = new List<Vector2>(
                outline.Count +
                (definition.EngravingAllFourSides ? 16 : 8));
            var broadFaceFloors = new List<int>(4);
            var sideFaceFloors = new List<int>(4);
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 current = outline[index];
                Vector2 next = outline[(index + 1) % outline.Count];
                result.Add(current);
                bool broadFace = Mathf.Abs(current.y - next.y) < 0.00001f &&
                    Mathf.Min(current.x, next.x) < -halfChannel &&
                    Mathf.Max(current.x, next.x) > halfChannel;
                if (broadFace)
                {
                    bool increasing = next.x > current.x;
                    float first = increasing ? -halfChannel : halfChannel;
                    float second = -first;
                    result.Add(new Vector2(first, current.y));
                    broadFaceFloors.Add(result.Count);
                    result.Add(new Vector2(first, current.y));
                    broadFaceFloors.Add(result.Count);
                    result.Add(new Vector2(second, current.y));
                    result.Add(new Vector2(second, current.y));
                    continue;
                }

                bool sideFace = definition.EngravingAllFourSides &&
                    Mathf.Abs(current.x - next.x) < 0.00001f &&
                    Mathf.Min(current.y, next.y) < -halfChannel &&
                    Mathf.Max(current.y, next.y) > halfChannel;
                if (!sideFace)
                {
                    continue;
                }

                bool increasingDepth = next.y > current.y;
                float firstDepth = increasingDepth
                    ? -halfChannel
                    : halfChannel;
                float secondDepth = -firstDepth;
                result.Add(new Vector2(current.x, firstDepth));
                sideFaceFloors.Add(result.Count);
                result.Add(new Vector2(current.x, firstDepth));
                sideFaceFloors.Add(result.Count);
                result.Add(new Vector2(current.x, secondDepth));
                result.Add(new Vector2(current.x, secondDepth));
            }
            broadFaceRecessedIndices = broadFaceFloors.ToArray();
            sideFaceRecessedIndices = sideFaceFloors.ToArray();
            return result;
        }

        private static IReadOnlyList<Vector2> RecessEngravingFloor(
            IReadOnlyList<Vector2> outline,
            IReadOnlyList<int> broadFaceRecessedIndices,
            IReadOnlyList<int> sideFaceRecessedIndices,
            float depth)
        {
            var result = new Vector2[outline.Count];
            for (int index = 0; index < outline.Count; index++)
            {
                result[index] = outline[index];
            }
            foreach (int index in broadFaceRecessedIndices)
            {
                Vector2 point = result[index];
                point.y -= Mathf.Sign(point.y) * depth;
                result[index] = point;
            }
            foreach (int index in sideFaceRecessedIndices)
            {
                Vector2 point = result[index];
                point.x -= Mathf.Sign(point.x) * depth;
                result[index] = point;
            }
            return result;
        }

        private static float ResolveEngravingEndY(
            ProceduralColumnBladeDefinition definition,
            float bottomY)
        {
            float fraction = definition.EngravingEndFraction;
            if (definition.EngravingTermination ==
                ColumnBladeEngravingTermination.Full)
            {
                // Full is an open-ended channel through the actual top cut.
                // The x-specific termination offset then follows a slanted
                // top edge on the left and right sides of the floor.
                fraction = 1f - definition.TopSlantRise * 0.5f /
                    definition.BladeLength;
            }
            else if (definition.EngravingTermination ==
                     ColumnBladeEngravingTermination.Circle)
            {
                float halfLine = ResolveEngravingWidth(definition) * 0.5f;
                float brushOffset = halfLine /
                    Mathf.Cos(Mathf.PI / EngravingCircleSegments);
                float outerRadius = ResolveEngravingCircleRadius(definition) +
                    brushOffset;
                float outerJoinDistance = Mathf.Sqrt(Mathf.Max(
                    0f,
                    outerRadius * outerRadius - halfLine * halfLine));
                // The physical line walls stop where they meet the loop's
                // outer wall. The gold floor separately continues to the
                // loop centerline, so no internal wall remains at the join.
                fraction -= outerJoinDistance / definition.BladeLength;
            }
            return bottomY + definition.BladeLength *
                Mathf.Clamp01(fraction);
        }

        public static float ResolveEngravingTerminationYOffset(
            ProceduralColumnBladeDefinition definition,
            float x)
        {
            if (definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Circle ||
                definition.TopProfile == ColumnBladeTopProfile.Flat ||
                definition.TopSlantRise <= 0.000001f)
            {
                return 0f;
            }

            float halfChannel = ResolveEngravingWidth(definition) * 0.5f;
            float boundedX = Mathf.Clamp(x, -halfChannel, halfChannel);
            return definition.TopSlantDirection *
                definition.TopSlantRise * boundedX /
                Mathf.Max(definition.BladeWidth, 0.000001f);
        }

        private static float ResolveUnclampedTopCutOffset(
            ProceduralColumnBladeDefinition definition,
            float x)
        {
            if (definition.TopProfile == ColumnBladeTopProfile.Flat ||
                definition.TopSlantRise <= 0.000001f)
            {
                return 0f;
            }
            return definition.TopSlantDirection *
                definition.TopSlantRise * x /
                Mathf.Max(definition.BladeWidth, 0.000001f);
        }

        private static float ResolveMaximumEngravingTerminationOffset(
            ProceduralColumnBladeDefinition definition)
        {
            return Mathf.Abs(ResolveEngravingTerminationYOffset(
                definition,
                ResolveEngravingWidth(definition) * 0.5f));
        }

        private static IReadOnlyList<Vector2> BuildBladeCrossSection(
            ProceduralColumnBladeDefinition definition)
        {
            float halfCore = definition.BladeCoreWidth * 0.5f;
            float halfWidth = definition.BladeWidth * 0.5f;
            float halfDepth = definition.BladeThickness * 0.5f;
            IReadOnlyList<Vector2> baseCrossSection =
                definition.EdgeStyle == ColumnBladeEdgeStyle.TwinSideEdges
                    ? new[]
                    {
                        new Vector2(-halfWidth, 0f),
                        new Vector2(-halfCore, -halfDepth),
                        new Vector2(halfCore, -halfDepth),
                        new Vector2(halfWidth, 0f),
                        new Vector2(halfCore, halfDepth),
                        new Vector2(-halfCore, halfDepth)
                    }
                    : new[]
                    {
                        new Vector2(-halfWidth, -halfDepth),
                        new Vector2(halfWidth, -halfDepth),
                        new Vector2(halfWidth, halfDepth),
                        new Vector2(-halfWidth, halfDepth)
                    };
            float transitionWidth = ResolveBladeChamferWidth(definition);
            IReadOnlyList<Vector2> crossSection = ChamferOutline(
                baseCrossSection,
                transitionWidth,
                definition.EdgeStyle == ColumnBladeEdgeStyle.TwinSideEdges
                    ? new[] { 0, 3 }
                    : Array.Empty<int>());
            return crossSection;
        }

        private static float ResolveBladeTopTransition(
            ProceduralColumnBladeDefinition definition)
        {
            return Mathf.Min(
                0.002f,
                Mathf.Min(
                    definition.BladeCoreWidth * 0.02f,
                    definition.BladeThickness * 0.04f));
        }

        // Stone keeps one continuous textured surface. Its notches are spread
        // around independent perimeter edges instead of mirroring one side.
        private static Mesh BuildFlatStoneBladeMesh(
            ProceduralColumnBladeDefinition definition,
            IReadOnlyList<Vector2> normalCrossSection,
            IReadOnlyList<Vector2> engravedCrossSection,
            float bottomY,
            float topTransition)
        {
            float topY = bottomY + definition.BladeLength;
            float shoulderY = topY - topTransition -
                definition.TopSlantRise;
            var random = new System.Random(
                unchecked(definition.Seed * 104729 + 811));
            var chips = new List<StoneChipEvent>(definition.StoneChipCount);
            float usableStart = bottomY + definition.BladeLength * 0.16f;
            float usableEnd = shoulderY - definition.BladeLength * 0.10f;
            float slot = (usableEnd - usableStart) /
                definition.StoneChipCount;
            List<int> availableEdges = BuildStoneChipCornerOrder(
                normalCrossSection,
                random);
            List<int> lengthRanks = BuildShuffledRanks(
                definition.StoneChipCount,
                random);
            for (int index = 0;
                 index < definition.StoneChipCount;
                 index++)
            {
                float center = usableStart +
                    slot * (index + Lerp(random, 0.47f, 0.53f));
                float denominator = Mathf.Max(
                    1f,
                    definition.StoneChipCount - 1f);
                float length = lengthRanks[index] / denominator;
                // Depth follows the shuffled longitudinal size: narrow chips
                // are always shallow, while a deep break must also occupy a
                // wider stretch of edge. This avoids stone looking sliced.
                float halfHeight = slot *
                    Mathf.Lerp(0.06f, 0.44f, length);
                float depth = definition.StoneChipDepth *
                    ResolveStoneChipDepthFactor(length);
                chips.Add(new StoneChipEvent(
                    center,
                    halfHeight,
                    depth,
                    availableEdges[index % availableEdges.Count]));
            }

            if (definition.PrimaryEngraving ==
                ColumnBladeEngravingStyle.SilhouetteInset)
            {
                return BuildSilhouetteInsetStoneBladeMesh(
                    definition,
                    normalCrossSection,
                    engravedCrossSection,
                    chips,
                    bottomY,
                    topY,
                    shoulderY,
                    topTransition);
            }

            int count = normalCrossSection.Count;
            bool engravingActive = definition.PrimaryEngraving ==
                ColumnBladeEngravingStyle.StraightLine;
            bool engravingRunsThroughTop = engravingActive &&
                definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Full;
            float maximumEndOffset =
                ResolveMaximumEngravingTerminationOffset(definition);
            float engravingEndY = engravingRunsThroughTop
                ? ResolveEngravingEndY(definition, bottomY)
                : Mathf.Min(
                    ResolveEngravingEndY(definition, bottomY),
                    shoulderY - 0.001f - maximumEndOffset);
            bool engravingEnded = !engravingActive;
            var rings = new List<IReadOnlyList<Vector2>>(
                4 + chips.Count * 3);
            var ringHeights = new List<float>(rings.Capacity);
            var angledTerminationRings = new List<bool>(rings.Capacity);
            rings.Add(engravingActive
                ? engravedCrossSection
                : normalCrossSection);
            ringHeights.Add(bottomY);
            angledTerminationRings.Add(false);

            void EndEngravingBefore(float nextHeight)
            {
                if (engravingEnded ||
                    engravingEndY + maximumEndOffset > nextHeight)
                {
                    return;
                }

                // Two outlines at one height form the flat horizontal end wall
                // of the trench. The perimeter links between them form its
                // vertical side walls for the entire engraved run.
                rings.Add(engravedCrossSection);
                ringHeights.Add(engravingEndY);
                angledTerminationRings.Add(true);
                rings.Add(normalCrossSection);
                ringHeights.Add(engravingEndY);
                angledTerminationRings.Add(true);
                engravingEnded = true;
            }

            foreach (StoneChipEvent chip in chips)
            {
                float chipStart = chip.CenterY - chip.HalfHeight;
                EndEngravingBefore(chipStart);
                IReadOnlyList<Vector2> activeCrossSection = engravingEnded
                    ? normalCrossSection
                    : engravedCrossSection;
                rings.Add(activeCrossSection);
                ringHeights.Add(chipStart);
                angledTerminationRings.Add(false);

                EndEngravingBefore(chip.CenterY);
                activeCrossSection = engravingEnded
                    ? normalCrossSection
                    : engravedCrossSection;
                rings.Add(InsetStoneCorner(
                    activeCrossSection,
                    chip.Depth,
                    chip.VertexIndex));
                ringHeights.Add(chip.CenterY);
                angledTerminationRings.Add(false);

                float chipEnd = chip.CenterY + chip.HalfHeight;
                EndEngravingBefore(chipEnd);
                rings.Add(engravingEnded
                    ? normalCrossSection
                    : engravedCrossSection);
                ringHeights.Add(chipEnd);
                angledTerminationRings.Add(false);
            }
            EndEngravingBefore(shoulderY);
            IReadOnlyList<Vector2> topCrossSection = engravingRunsThroughTop
                ? engravedCrossSection
                : normalCrossSection;
            rings.Add(topCrossSection);
            ringHeights.Add(shoulderY);
            angledTerminationRings.Add(false);
            rings.Add(InsetCrossSection(
                topCrossSection,
                topTransition));
            ringHeights.Add(topY);
            angledTerminationRings.Add(false);

            var vertices = new List<Vector3>(count * rings.Count);
            var triangles = new List<int>(count * rings.Count * 6);
            for (int ring = 0; ring < rings.Count; ring++)
            {
                for (int index = 0; index < count; index++)
                {
                    Vector2 point = rings[ring][index];
                    float y = ringHeights[ring];
                    if (angledTerminationRings[ring])
                    {
                        y += ResolveEngravingTerminationYOffset(
                            definition,
                            point.x);
                    }
                    else if (ring == rings.Count - 2)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                rings[ring]) -
                            topTransition;
                    }
                    else if (ring == rings.Count - 1)
                    {
                        y = ResolveBladeTopY(
                            definition,
                            point.x,
                            rings[ring]) +
                            ResolveTopEdgeWrapYOffset(
                                definition,
                                point.x);
                    }
                    vertices.Add(new Vector3(
                        point.x,
                        y,
                        point.y));
                }
            }

            int topRing = (rings.Count - 1) * count;
            if (definition.GuardProfile != ColumnBladeGuardProfile.Ring)
            {
                AppendTriangulatedBladeCap(
                    vertices, triangles, 0, count, upward: false);
            }
            AppendTriangulatedBladeCap(
                vertices, triangles, topRing, count, upward: true);
            for (int ring = 0; ring < rings.Count - 1; ring++)
            {
                int lower = ring * count;
                int upper = (ring + 1) * count;
                for (int index = 0; index < count; index++)
                {
                    int next = (index + 1) % count;
                    AddQuad(
                        triangles,
                        lower + index,
                        upper + index,
                        upper + next,
                        lower + next);
                }
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static Mesh BuildSilhouetteInsetStoneBladeMesh(
            ProceduralColumnBladeDefinition definition,
            IReadOnlyList<Vector2> normalCrossSection,
            IReadOnlyList<Vector2> engravedCrossSection,
            IReadOnlyList<StoneChipEvent> chips,
            float bottomY,
            float topY,
            float shoulderY,
            float topTransition)
        {
            int count = normalCrossSection.Count;
            float margin = ResolveSilhouetteInsetMargin(definition);
            float floorInset = margin +
                ResolveSilhouetteWallRun(definition);
            var rings = new List<IReadOnlyList<Vector2>>(
                8 + chips.Count * 3);
            var ringHeights = new List<float>(rings.Capacity);
            var topMarginOffsets = new List<float>(rings.Capacity);
            void AddRing(
                IReadOnlyList<Vector2> ring,
                float height,
                float topMarginOffset = -1f)
            {
                rings.Add(ring);
                ringHeights.Add(height);
                topMarginOffsets.Add(topMarginOffset);
            }

            if (definition.GuardProfile == ColumnBladeGuardProfile.Ring)
            {
                AddRing(engravedCrossSection, bottomY);
            }
            else
            {
                AddRing(normalCrossSection, bottomY);
                AddRing(normalCrossSection, bottomY + margin);
                AddRing(engravedCrossSection, bottomY + floorInset);
            }
            foreach (StoneChipEvent chip in chips)
            {
                AddRing(
                    engravedCrossSection,
                    chip.CenterY - chip.HalfHeight);
                AddRing(
                    InsetStoneCorner(
                        engravedCrossSection,
                        chip.Depth,
                        chip.VertexIndex),
                    chip.CenterY);
                AddRing(
                    engravedCrossSection,
                    chip.CenterY + chip.HalfHeight);
            }
            AddRing(engravedCrossSection, shoulderY, floorInset);
            AddRing(normalCrossSection, shoulderY, margin);
            AddRing(normalCrossSection, shoulderY);
            IReadOnlyList<Vector2> topInset = InsetCrossSection(
                normalCrossSection,
                topTransition);
            AddRing(topInset, topY);

            var vertices = new List<Vector3>(count * rings.Count);
            var triangles = new List<int>(count * rings.Count * 6);
            for (int ring = 0; ring < rings.Count; ring++)
            {
                for (int index = 0; index < count; index++)
                {
                    Vector2 point = rings[ring][index];
                    float y = ringHeights[ring];
                    if (topMarginOffsets[ring] >= 0f)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                normalCrossSection) -
                            topMarginOffsets[ring];
                    }
                    else if (ring == rings.Count - 2)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                rings[ring]) - topTransition;
                    }
                    else if (ring == rings.Count - 1)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                rings[ring]) +
                            ResolveTopEdgeWrapYOffset(
                                definition,
                                point.x);
                    }
                    vertices.Add(new Vector3(point.x, y, point.y));
                }
            }

            int top = (rings.Count - 1) * count;
            if (definition.GuardProfile != ColumnBladeGuardProfile.Ring)
            {
                AppendTriangulatedBladeCap(
                    vertices, triangles, 0, count, upward: false);
            }
            if (definition.ShapeCategory ==
                ColumnBladeShapeCategory.SquareBlock)
            {
                AppendSilhouetteInsetTopCap(
                    definition,
                    vertices,
                    triangles,
                    top,
                    count,
                    topInset);
            }
            else
            {
                AppendTriangulatedBladeCap(
                    vertices, triangles, top, count, upward: true);
            }
            for (int ring = 0; ring < rings.Count - 1; ring++)
            {
                int lower = ring * count;
                int upper = (ring + 1) * count;
                for (int index = 0; index < count; index++)
                {
                    int next = (index + 1) % count;
                    AddQuad(
                        triangles,
                        lower + index,
                        upper + index,
                        upper + next,
                        lower + next);
                }
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static IReadOnlyList<Vector2> AddStoneChipSupportPoints(
            IReadOnlyList<Vector2> outline,
            ProceduralColumnBladeDefinition definition)
        {
            float supportReach = Mathf.Clamp(
                Mathf.Max(
                    ResolveBladeChamferWidth(definition) * 1.5f,
                    definition.StoneChipDepth * 2f),
                0.005f,
                0.036f);
            var result = new List<Vector2>(outline.Count * 3);
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 current = outline[index];
                Vector2 next = outline[(index + 1) % outline.Count];
                result.Add(current);
                float length = Vector2.Distance(current, next);
                if (length <= supportReach * 2.4f)
                {
                    continue;
                }
                Vector2 direction = (next - current) / length;
                result.Add(current + direction * supportReach);
                result.Add(next - direction * supportReach);
            }
            return result;
        }

        private static List<int> BuildStoneChipCornerOrder(
            IReadOnlyList<Vector2> outline,
            System.Random random)
        {
            List<int> corners = FindStoneChipCorners(outline);
            var cornerPoints = new Vector2[corners.Count];
            for (int index = 0; index < corners.Count; index++)
            {
                cornerPoints[index] = outline[corners[index]];
            }

            var ordered = new List<int>(corners.Count);
            AddDistinct(ordered, corners[IndexOfExtreme(
                cornerPoints, point => point.x, false)]);
            AddDistinct(ordered, corners[IndexOfExtreme(
                cornerPoints, point => point.x, true)]);
            AddDistinct(ordered, corners[IndexOfExtreme(
                cornerPoints, point => point.y, false)]);
            AddDistinct(ordered, corners[IndexOfExtreme(
                cornerPoints, point => point.y, true)]);

            var remaining = new List<int>();
            for (int index = 0; index < corners.Count; index++)
            {
                if (!ordered.Contains(corners[index]))
                {
                    remaining.Add(corners[index]);
                }
            }
            for (int index = remaining.Count - 1; index > 0; index--)
            {
                int swap = random.Next(0, index + 1);
                (remaining[index], remaining[swap]) =
                    (remaining[swap], remaining[index]);
            }
            ordered.AddRange(remaining);
            return ordered;
        }

        private static List<int> BuildShuffledRanks(
            int count,
            System.Random random)
        {
            var ranks = new List<int>(count);
            for (int index = 0; index < count; index++)
            {
                ranks.Add(index);
            }
            for (int index = ranks.Count - 1; index > 0; index--)
            {
                int swap = random.Next(0, index + 1);
                (ranks[index], ranks[swap]) =
                    (ranks[swap], ranks[index]);
            }
            return ranks;
        }

        private static List<int> FindStoneChipCorners(
            IReadOnlyList<Vector2> outline)
        {
            var result = new List<int>();
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 previous = outline[
                    (index - 1 + outline.Count) % outline.Count];
                Vector2 current = outline[index];
                Vector2 next = outline[(index + 1) % outline.Count];
                Vector2 incoming = (current - previous).normalized;
                Vector2 outgoing = (next - current).normalized;
                if (Mathf.Abs(
                        incoming.x * outgoing.y -
                        incoming.y * outgoing.x) > 0.025f)
                {
                    result.Add(index);
                }
            }
            return result;
        }

        private static int IndexOfExtreme(
            IReadOnlyList<Vector2> values,
            Func<Vector2, float> selector,
            bool maximum)
        {
            int result = 0;
            float extreme = selector(values[0]);
            for (int index = 1; index < values.Count; index++)
            {
                float candidate = selector(values[index]);
                if ((maximum && candidate > extreme) ||
                    (!maximum && candidate < extreme))
                {
                    extreme = candidate;
                    result = index;
                }
            }
            return result;
        }

        private static void AddDistinct(List<int> values, int value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static Vector2[] InsetStoneCorner(
            IReadOnlyList<Vector2> outline,
            float requestedDepth,
            int vertexIndex)
        {
            Vector2 center = Vector2.zero;
            for (int index = 0; index < outline.Count; index++)
            {
                center += outline[index];
            }
            center /= outline.Count;
            var result = new Vector2[outline.Count];
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 point = outline[index];
                if (index == vertexIndex)
                {
                    point += (center - point).normalized * requestedDepth;
                }
                result[index] = point;
            }
            return result;
        }

        private readonly struct StoneChipEvent
        {
            public readonly float CenterY;
            public readonly float HalfHeight;
            public readonly float Depth;
            public readonly int VertexIndex;

            public StoneChipEvent(
                float centerY,
                float halfHeight,
                float depth,
                int vertexIndex)
            {
                CenterY = centerY;
                HalfHeight = halfHeight;
                Depth = depth;
                VertexIndex = vertexIndex;
            }
        }

        private static Mesh BuildGuardMesh(
            ProceduralColumnBladeDefinition definition)
        {
            if (definition.GuardProfile == ColumnBladeGuardProfile.Ring)
            {
                return BuildRingGuardMesh(definition);
            }
            float cornerCut = definition.GuardProfile switch
            {
                ColumnBladeGuardProfile.Octagonal =>
                    Mathf.Min(
                        definition.GuardWidth,
                        definition.GuardDepth) * 0.29f,
                ColumnBladeGuardProfile.CompactBlock =>
                    Mathf.Min(
                        definition.GuardWidth,
                        definition.GuardDepth) * 0.10f,
                _ => Mathf.Min(
                    definition.GuardWidth,
                    definition.GuardDepth) * 0.08f
            };
            float halfWidth = definition.GuardWidth * 0.5f;
            float halfDepth = definition.GuardDepth * 0.5f;
            float cut = Mathf.Clamp(
                cornerCut,
                0f,
                Mathf.Min(halfWidth, halfDepth) * 0.48f);
            IReadOnlyList<Vector2> topDownOutline = new[]
            {
                new Vector2(-halfWidth + cut, -halfDepth),
                new Vector2(halfWidth - cut, -halfDepth),
                new Vector2(halfWidth, -halfDepth + cut),
                new Vector2(halfWidth, halfDepth - cut),
                new Vector2(halfWidth - cut, halfDepth),
                new Vector2(-halfWidth + cut, halfDepth),
                new Vector2(-halfWidth, halfDepth - cut),
                new Vector2(-halfWidth, -halfDepth + cut)
            };
            float guardChamfer = ResolveGuardChamferWidth(definition);
            return BuildVerticalPrism(
                topDownOutline,
                -definition.GuardHeight * 0.5f,
                definition.GuardHeight * 0.5f,
                guardChamfer,
                guardChamfer);
        }

        private static Mesh BuildRingGuardMesh(
            ProceduralColumnBladeDefinition definition)
        {
            const int segments = 12;
            float outerRadiusX = definition.GuardWidth * 0.5f;
            float outerRadiusY = definition.GuardHeight * 0.5f;
            float rim = ResolveRingGuardRimThickness(definition);
            float innerRadiusX = Mathf.Max(0.008f, outerRadiusX - rim);
            float innerRadiusY = Mathf.Max(0.008f, outerRadiusY - rim);
            float halfDepth = definition.GuardDepth * 0.5f;
            float faceFacetInset = Mathf.Min(
                0.0012f,
                definition.GuardDepth * 0.035f);
            Vector2[] outerOutline = BuildRingGuardOutline(
                outerRadiusX,
                outerRadiusY);
            Vector2[] innerOutline = BuildRingGuardOutline(
                innerRadiusX,
                innerRadiusY);
            var vertices = new List<Vector3>(segments * 4);
            for (int index = 0; index < segments; index++)
            {
                float inset = (index & 1) == 0 ? 0f : faceFacetInset;
                float frontZ = -halfDepth + inset;
                float backZ = halfDepth - inset;
                vertices.Add(new Vector3(
                    outerOutline[index].x,
                    outerOutline[index].y,
                    frontZ));
                vertices.Add(new Vector3(
                    outerOutline[index].x,
                    outerOutline[index].y,
                    backZ));
                vertices.Add(new Vector3(
                    innerOutline[index].x,
                    innerOutline[index].y,
                    frontZ));
                vertices.Add(new Vector3(
                    innerOutline[index].x,
                    innerOutline[index].y,
                    backZ));
            }

            var triangles = new List<int>(segments * 24);
            for (int index = 0; index < segments; index++)
            {
                int next = (index + 1) % segments;
                int outerFront = index * 4;
                int outerBack = outerFront + 1;
                int innerFront = outerFront + 2;
                int innerBack = outerFront + 3;
                int nextOuterFront = next * 4;
                int nextOuterBack = nextOuterFront + 1;
                int nextInnerFront = nextOuterFront + 2;
                int nextInnerBack = nextOuterFront + 3;

                AddQuad(
                    triangles,
                    outerFront,
                    nextOuterFront,
                    nextInnerFront,
                    innerFront);
                AddQuad(
                    triangles,
                    outerBack,
                    innerBack,
                    nextInnerBack,
                    nextOuterBack);
                AddQuad(
                    triangles,
                    outerFront,
                    outerBack,
                    nextOuterBack,
                    nextOuterFront);
                AddQuad(
                    triangles,
                    innerFront,
                    nextInnerFront,
                    nextInnerBack,
                    innerBack);
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static Vector2[] BuildRingGuardOutline(
            float radiusX,
            float radiusY)
        {
            const int segments = 12;
            float halfStepScale = 1f / Mathf.Cos(Mathf.PI / segments);
            var outline = new Vector2[segments];
            for (int index = 0; index < segments; index++)
            {
                float angle = Mathf.PI * 2f * (index + 0.5f) / segments;
                outline[index] = new Vector2(
                    Mathf.Sin(angle) * radiusX * halfStepScale,
                    Mathf.Cos(angle) * radiusY * halfStepScale);
            }
            return outline;
        }

        private static Mesh BuildRingBladeJointMesh(
            ProceduralColumnBladeDefinition definition)
        {
            float radiusX = definition.GuardWidth * 0.5f;
            float radiusY = definition.GuardHeight * 0.5f;
            float seatHalf = ResolveRingGuardBladeSeatWidth(definition) * 0.5f;
            float bladeHalf = Mathf.Min(
                definition.BladeWidth * 0.5f,
                radiusX * 0.94f);
            float halfDepth = Mathf.Min(
                definition.BladeThickness,
                definition.GuardDepth * 0.84f) * 0.5f;
            Vector2[] ring = BuildRingGuardOutline(radiusX, radiusY);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AppendRingBladeJointHalf(
                vertices,
                triangles,
                ring,
                seatHalf,
                bladeHalf,
                halfDepth,
                rightSide: true);
            AppendRingBladeJointHalf(
                vertices,
                triangles,
                ring,
                seatHalf,
                bladeHalf,
                halfDepth,
                rightSide: false);
            return CreateFlatMesh(vertices, triangles);
        }

        private static void AppendRingBladeJointHalf(
            List<Vector3> vertices,
            List<int> triangles,
            IReadOnlyList<Vector2> ring,
            float seatHalf,
            float bladeHalf,
            float halfDepth,
            bool rightSide)
        {
            if (bladeHalf <= seatHalf + 0.00001f)
            {
                return;
            }

            var curve = new List<Vector2> { ring[0] };
            for (int index = 0; index < 2; index++)
            {
                Vector2 first = ring[index];
                Vector2 second = ring[index + 1];
                if (bladeHalf <= second.x)
                {
                    float t = Mathf.InverseLerp(
                        first.x,
                        second.x,
                        bladeHalf);
                    curve.Add(Vector2.Lerp(first, second, t));
                    break;
                }
                curve.Add(second);
            }

            var polygon = new List<Vector2>
            {
                new Vector2(seatHalf, ring[0].y),
                new Vector2(bladeHalf, ring[0].y),
                curve[curve.Count - 1]
            };
            for (int index = curve.Count - 2; index > 0; index--)
            {
                polygon.Add(curve[index]);
            }
            if (!rightSide)
            {
                for (int index = 0; index < polygon.Count; index++)
                {
                    polygon[index] = new Vector2(
                        -polygon[index].x,
                        polygon[index].y);
                }
                polygon.Reverse();
            }
            AppendExtrudedPolygon(
                vertices,
                triangles,
                polygon,
                halfDepth);
        }

        private static void AppendExtrudedPolygon(
            List<Vector3> vertices,
            List<int> triangles,
            IReadOnlyList<Vector2> polygon,
            float halfDepth)
        {
            int count = polygon.Count;
            if (count < 3)
            {
                return;
            }
            int first = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                vertices.Add(new Vector3(
                    polygon[index].x,
                    polygon[index].y,
                    -halfDepth));
                vertices.Add(new Vector3(
                    polygon[index].x,
                    polygon[index].y,
                    halfDepth));
            }
            for (int index = 1; index < count - 1; index++)
            {
                triangles.Add(first);
                triangles.Add(first + index * 2);
                triangles.Add(first + (index + 1) * 2);
                triangles.Add(first + 1);
                triangles.Add(first + (index + 1) * 2 + 1);
                triangles.Add(first + index * 2 + 1);
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                AddQuad(
                    triangles,
                    first + index * 2,
                    first + index * 2 + 1,
                    first + next * 2 + 1,
                    first + next * 2);
            }
        }


        private static IReadOnlyList<Vector2> ChamferOutline(
            IReadOnlyList<Vector2> outline,
            float transitionWidth,
            IReadOnlyList<int> preservedSharpCorners)
        {
            var result = new List<Vector2>(outline.Count * 2);
            for (int index = 0; index < outline.Count; index++)
            {
                bool preserve = false;
                for (int sharpIndex = 0;
                     sharpIndex < preservedSharpCorners.Count;
                     sharpIndex++)
                {
                    if (preservedSharpCorners[sharpIndex] == index)
                    {
                        preserve = true;
                        break;
                    }
                }

                Vector2 current = outline[index];
                if (preserve || transitionWidth <= 0.000001f)
                {
                    result.Add(current);
                    continue;
                }

                Vector2 previous =
                    outline[(index - 1 + outline.Count) % outline.Count];
                Vector2 next = outline[(index + 1) % outline.Count];
                float cut = Mathf.Min(
                    transitionWidth,
                    Mathf.Min(
                        Vector2.Distance(current, previous),
                        Vector2.Distance(current, next)) * 0.28f);
                result.Add(
                    current + (previous - current).normalized * cut);
                result.Add(current + (next - current).normalized * cut);
            }
            return result;
        }

        private static Mesh BuildExtrudedPolygon(
            IReadOnlyList<Vector2> outline,
            float frontZ,
            float rearZ)
        {
            int count = outline.Count;
            var vertices = new List<Vector3>(count * 2);
            var triangles = new List<int>(count * 12);
            for (int index = 0; index < count; index++)
            {
                vertices.Add(new Vector3(
                    outline[index].x,
                    outline[index].y,
                    frontZ));
            }
            for (int index = 0; index < count; index++)
            {
                vertices.Add(new Vector3(
                    outline[index].x,
                    outline[index].y,
                    rearZ));
            }

            for (int index = 1; index < count - 1; index++)
            {
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add(index);

                triangles.Add(count);
                triangles.Add(count + index);
                triangles.Add(count + index + 1);
            }

            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                AddQuad(
                    triangles,
                    index,
                    next,
                    count + next,
                    count + index);
            }

            return CreateFlatMesh(vertices, triangles);
        }

        private static Mesh BuildSlantedBladePrism(
            ProceduralColumnBladeDefinition definition,
            IReadOnlyList<Vector2> normalCrossSection,
            IReadOnlyList<Vector2> engravedCrossSection,
            float bottomY,
            float topY,
            float topTransition)
        {
            int count = normalCrossSection.Count;
            bool lineEngravingActive = definition.PrimaryEngraving ==
                ColumnBladeEngravingStyle.StraightLine;
            bool silhouetteInsetActive = definition.PrimaryEngraving ==
                ColumnBladeEngravingStyle.SilhouetteInset;
            bool engravingRunsThroughTop = lineEngravingActive &&
                definition.EngravingTermination ==
                    ColumnBladeEngravingTermination.Full;
            IReadOnlyList<Vector2> topCrossSection = engravingRunsThroughTop
                ? engravedCrossSection
                : normalCrossSection;
            Vector2[] topInset = InsetCrossSection(
                topCrossSection,
                topTransition);
            float lowestShoulder = topY - definition.TopSlantRise -
                topTransition;
            float maximumEndOffset =
                ResolveMaximumEngravingTerminationOffset(definition);
            float engravingEndY = engravingRunsThroughTop
                ? ResolveEngravingEndY(definition, bottomY)
                : Mathf.Min(
                    ResolveEngravingEndY(definition, bottomY),
                    lowestShoulder - 0.001f - maximumEndOffset);
            var rings = new List<IReadOnlyList<Vector2>>(8);
            var ringHeights = new List<float>(8);
            var topMarginOffsets = new List<float>(8);
            void AddRing(
                IReadOnlyList<Vector2> ring,
                float height,
                float topMarginOffset = -1f)
            {
                rings.Add(ring);
                ringHeights.Add(height);
                topMarginOffsets.Add(topMarginOffset);
            }

            if (silhouetteInsetActive)
            {
                float margin = ResolveSilhouetteInsetMargin(definition);
                float floorInset = margin +
                    ResolveSilhouetteWallRun(definition);
                if (definition.GuardProfile == ColumnBladeGuardProfile.Ring)
                {
                    AddRing(engravedCrossSection, bottomY);
                }
                else
                {
                    AddRing(normalCrossSection, bottomY);
                    AddRing(normalCrossSection, bottomY + margin);
                    AddRing(engravedCrossSection, bottomY + floorInset);
                }
                AddRing(
                    engravedCrossSection,
                    lowestShoulder,
                    floorInset);
                AddRing(normalCrossSection, lowestShoulder, margin);
            }
            else
            {
                AddRing(
                    lineEngravingActive
                        ? engravedCrossSection
                        : normalCrossSection,
                    bottomY);
            }
            if (lineEngravingActive && !engravingRunsThroughTop)
            {
                // Coincident-height rings create a crisp, square termination
                // instead of feathering the engraving back into the surface.
                AddRing(engravedCrossSection, engravingEndY);
                AddRing(normalCrossSection, engravingEndY);
            }
            AddRing(topCrossSection, lowestShoulder);
            AddRing(topInset, topY);

            var vertices = new List<Vector3>(count * rings.Count);
            for (int ring = 0; ring < rings.Count; ring++)
            {
                for (int index = 0; index < count; index++)
                {
                    Vector2 point = rings[ring][index];
                    float y = ringHeights[ring];
                    bool engravingEndRing = lineEngravingActive &&
                        !engravingRunsThroughTop &&
                        (ring == 1 || ring == 2);
                    if (topMarginOffsets[ring] >= 0f)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                normalCrossSection) -
                            topMarginOffsets[ring];
                    }
                    else if (engravingEndRing)
                    {
                        y += ResolveEngravingTerminationYOffset(
                            definition,
                            point.x);
                    }
                    else if (ring == rings.Count - 2)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                rings[ring]) -
                            topTransition;
                    }
                    else if (ring == rings.Count - 1)
                    {
                        y = ResolveBladeTopY(
                                definition,
                                point.x,
                                rings[ring]) +
                            ResolveTopEdgeWrapYOffset(
                                definition,
                                point.x);
                    }
                    vertices.Add(new Vector3(point.x, y, point.y));
                }
            }

            var triangles = new List<int>(count * rings.Count * 6);
            int top = (rings.Count - 1) * count;
            if (definition.GuardProfile != ColumnBladeGuardProfile.Ring)
            {
                AppendTriangulatedBladeCap(
                    vertices, triangles, 0, count, upward: false);
            }
            if (silhouetteInsetActive &&
                definition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock)
            {
                AppendSilhouetteInsetTopCap(
                    definition,
                    vertices,
                    triangles,
                    top,
                    count,
                    topInset);
            }
            else
            {
                AppendTriangulatedBladeCap(
                    vertices, triangles, top, count, upward: true);
            }
            for (int ring = 0; ring < rings.Count - 1; ring++)
            {
                int lower = ring * count;
                int upper = (ring + 1) * count;
                for (int index = 0; index < count; index++)
                {
                    int next = (index + 1) % count;
                    AddQuad(
                        triangles,
                        lower + index,
                        upper + index,
                        upper + next,
                        lower + next);
                }
            }
            return CreateFlatMesh(vertices, triangles);
        }

        private static void AppendSilhouetteInsetTopCap(
            ProceduralColumnBladeDefinition definition,
            List<Vector3> vertices,
            List<int> triangles,
            int outerFirst,
            int count,
            IReadOnlyList<Vector2> outerOutline)
        {
            Vector2[] lipOutline = InsetCrossSection(
                outerOutline,
                ResolveSilhouetteInsetMargin(definition));
            Vector2[] floorOutline = InsetCrossSection(
                outerOutline,
                ResolveSilhouetteInsetMargin(definition) +
                    ResolveSilhouetteWallRun(definition));
            int lipFirst = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                Vector2 point = lipOutline[index];
                vertices.Add(new Vector3(
                    point.x,
                    ResolveBladeTopY(
                        definition,
                        point.x,
                        outerOutline),
                    point.y));
            }
            int floorFirst = vertices.Count;
            float depth = ResolveEngravingDepth(definition);
            for (int index = 0; index < count; index++)
            {
                Vector2 point = floorOutline[index];
                vertices.Add(new Vector3(
                    point.x,
                    ResolveBladeTopY(
                        definition,
                        point.x,
                        outerOutline) - depth,
                    point.y));
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                AddQuad(
                    triangles,
                    outerFirst + index,
                    lipFirst + index,
                    lipFirst + next,
                    outerFirst + next);
                AddQuad(
                    triangles,
                    lipFirst + index,
                    lipFirst + next,
                    floorFirst + next,
                    floorFirst + index);
            }
            AppendConvexTopCapFan(
                vertices,
                triangles,
                floorFirst,
                count);
        }

        private static void AppendConvexTopCapFan(
            List<Vector3> vertices,
            List<int> triangles,
            int first,
            int count)
        {
            Vector3 center = Vector3.zero;
            for (int index = 0; index < count; index++)
            {
                center += vertices[first + index];
            }
            center /= Mathf.Max(1, count);
            int centerIndex = vertices.Count;
            vertices.Add(center);
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                triangles.Add(first + index);
                triangles.Add(centerIndex);
                triangles.Add(first + next);
            }
        }

        private static void AppendTriangulatedBladeCap(
            IReadOnlyList<Vector3> vertices,
            List<int> triangles,
            int first,
            int count,
            bool upward)
        {
            var polygon = new List<int>(count);
            for (int offset = 0; offset < count; offset++)
            {
                int index = first + offset;
                Vector3 point = vertices[index];
                if (polygon.Count > 0)
                {
                    Vector3 previous = vertices[polygon[polygon.Count - 1]];
                    if (Mathf.Abs(previous.x - point.x) < 0.0000001f &&
                        Mathf.Abs(previous.z - point.z) < 0.0000001f)
                    {
                        continue;
                    }
                }
                polygon.Add(index);
            }

            bool removed;
            do
            {
                removed = false;
                for (int index = 0; index < polygon.Count &&
                     polygon.Count > 3; index++)
                {
                    Vector3 previous = vertices[polygon[
                        (index - 1 + polygon.Count) % polygon.Count]];
                    Vector3 current = vertices[polygon[index]];
                    Vector3 next = vertices[polygon[
                        (index + 1) % polygon.Count]];
                    float cross = CrossPlanar(previous, current, next);
                    if (Mathf.Abs(cross) > 0.0000000001f)
                    {
                        continue;
                    }
                    polygon.RemoveAt(index);
                    removed = true;
                    break;
                }
            } while (removed);

            float signedArea = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector3 current = vertices[polygon[index]];
                Vector3 next = vertices[polygon[
                    (index + 1) % polygon.Count]];
                signedArea += current.x * next.z - next.x * current.z;
            }
            if (signedArea < 0f)
            {
                polygon.Reverse();
            }

            int safety = polygon.Count * polygon.Count;
            while (polygon.Count > 3 && safety-- > 0)
            {
                bool clipped = false;
                for (int index = 0; index < polygon.Count; index++)
                {
                    int previousIndex = polygon[
                        (index - 1 + polygon.Count) % polygon.Count];
                    int currentIndex = polygon[index];
                    int nextIndex = polygon[(index + 1) % polygon.Count];
                    Vector3 previous = vertices[previousIndex];
                    Vector3 current = vertices[currentIndex];
                    Vector3 next = vertices[nextIndex];
                    if (CrossPlanar(previous, current, next) <=
                        0.0000000001f)
                    {
                        continue;
                    }

                    bool containsPoint = false;
                    for (int other = 0; other < polygon.Count; other++)
                    {
                        int otherIndex = polygon[other];
                        if (otherIndex == previousIndex ||
                            otherIndex == currentIndex ||
                            otherIndex == nextIndex)
                        {
                            continue;
                        }
                        if (PointStrictlyInsidePlanarTriangle(
                                vertices[otherIndex],
                                previous,
                                current,
                                next))
                        {
                            containsPoint = true;
                            break;
                        }
                    }
                    if (containsPoint)
                    {
                        continue;
                    }

                    AppendCapTriangle(
                        triangles,
                        previousIndex,
                        currentIndex,
                        nextIndex,
                        upward);
                    polygon.RemoveAt(index);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    break;
                }
            }
            if (polygon.Count == 3)
            {
                AppendCapTriangle(
                    triangles,
                    polygon[0],
                    polygon[1],
                    polygon[2],
                    upward);
            }
        }

        private static void AppendCapTriangle(
            List<int> triangles,
            int first,
            int second,
            int third,
            bool upward)
        {
            triangles.Add(first);
            triangles.Add(upward ? third : second);
            triangles.Add(upward ? second : third);
        }

        private static float CrossPlanar(
            Vector3 first,
            Vector3 second,
            Vector3 third)
        {
            return (second.x - first.x) * (third.z - first.z) -
                (second.z - first.z) * (third.x - first.x);
        }

        private static bool PointStrictlyInsidePlanarTriangle(
            Vector3 point,
            Vector3 first,
            Vector3 second,
            Vector3 third)
        {
            const float epsilon = 0.0000000001f;
            float firstCross = CrossPlanar(first, second, point);
            float secondCross = CrossPlanar(second, third, point);
            float thirdCross = CrossPlanar(third, first, point);
            return firstCross > epsilon &&
                secondCross > epsilon &&
                thirdCross > epsilon;
        }

        private static float ResolveBladeTopY(
            ProceduralColumnBladeDefinition definition,
            float x,
            IReadOnlyList<Vector2> outline)
        {
            float maximumY = ResolveBladeBottomY(definition) +
                definition.BladeLength;
            if (definition.TopProfile == ColumnBladeTopProfile.Flat ||
                definition.TopSlantRise <= 0.000001f)
            {
                return maximumY;
            }
            float halfWidth = definition.BladeWidth * 0.5f;
            float normalized = Mathf.InverseLerp(-halfWidth, halfWidth, x);
            if (definition.TopSlantDirection < 0)
            {
                normalized = 1f - normalized;
            }
            return maximumY - definition.TopSlantRise * (1f - normalized);
        }

        public static float ResolveTopEdgeWrapDrop(
            ProceduralColumnBladeDefinition definition)
        {
            if (definition.ShapeCategory ==
                    ColumnBladeShapeCategory.SquareBlock ||
                definition.EdgeStyle != ColumnBladeEdgeStyle.TwinSideEdges)
            {
                return 0f;
            }
            return Mathf.Min(
                0.008f,
                Mathf.Min(
                    definition.BladeThickness * 0.32f,
                    definition.BladeEdgeWidth * 0.72f));
        }

        private static float ResolveTopEdgeWrapYOffset(
            ProceduralColumnBladeDefinition definition,
            float x)
        {
            float drop = ResolveTopEdgeWrapDrop(definition);
            if (drop <= 0.000001f)
            {
                return 0f;
            }
            float halfCore = definition.BladeCoreWidth * 0.5f;
            float halfWidth = definition.BladeWidth * 0.5f;
            float edgeFraction = Mathf.InverseLerp(
                halfCore,
                halfWidth,
                Mathf.Abs(x));
            return -drop * edgeFraction;
        }

        private static Mesh BuildVerticalPrism(
            IReadOnlyList<Vector2> crossSection,
            float bottomY,
            float topY,
            float topTransition,
            float bottomTransition = 0f)
        {
            int count = crossSection.Count;
            float height = Mathf.Max(0f, topY - bottomY);
            topTransition = Mathf.Clamp(topTransition, 0f, height * 0.25f);
            bottomTransition = Mathf.Clamp(
                bottomTransition,
                0f,
                height * 0.25f);
            bool wrapTop = topTransition > 0.000001f;
            bool wrapBottom = bottomTransition > 0.000001f;
            Vector2[] topInset = wrapTop
                ? InsetCrossSection(crossSection, topTransition)
                : null;
            Vector2[] bottomInset = wrapBottom
                ? InsetCrossSection(crossSection, bottomTransition)
                : null;
            var rings = new List<IReadOnlyList<Vector2>>(4);
            var ringHeights = new List<float>(4);
            rings.Add(wrapBottom ? bottomInset : crossSection);
            ringHeights.Add(bottomY);
            if (wrapBottom)
            {
                rings.Add(crossSection);
                ringHeights.Add(bottomY + bottomTransition);
            }
            rings.Add(crossSection);
            ringHeights.Add(wrapTop ? topY - topTransition : topY);
            if (wrapTop)
            {
                rings.Add(topInset);
                ringHeights.Add(topY);
            }

            var vertices = new List<Vector3>(count * rings.Count);
            var triangles = new List<int>(count * rings.Count * 6);
            for (int ring = 0; ring < rings.Count; ring++)
            {
                for (int index = 0; index < count; index++)
                {
                    vertices.Add(new Vector3(
                        rings[ring][index].x,
                        ringHeights[ring],
                        rings[ring][index].y));
                }
            }
            int topRing = (rings.Count - 1) * count;

            for (int index = 1; index < count - 1; index++)
            {
                triangles.Add(0);
                triangles.Add(index);
                triangles.Add(index + 1);

                triangles.Add(topRing);
                triangles.Add(topRing + index + 1);
                triangles.Add(topRing + index);
            }

            for (int ring = 0; ring < rings.Count - 1; ring++)
            {
                int lowerRing = ring * count;
                int upperRing = (ring + 1) * count;
                for (int index = 0; index < count; index++)
                {
                    int next = (index + 1) % count;
                    AddQuad(
                        triangles,
                        lowerRing + index,
                        upperRing + index,
                        upperRing + next,
                        lowerRing + next);
                }
            }

            return CreateFlatMesh(vertices, triangles);
        }

        private static Vector2[] InsetCrossSection(
            IReadOnlyList<Vector2> crossSection,
            float inset)
        {
            Vector2 minimum = crossSection[0];
            Vector2 maximum = crossSection[0];
            for (int index = 1; index < crossSection.Count; index++)
            {
                minimum = Vector2.Min(minimum, crossSection[index]);
                maximum = Vector2.Max(maximum, crossSection[index]);
            }
            Vector2 center = (minimum + maximum) * 0.5f;
            Vector2 halfSize = (maximum - minimum) * 0.5f;
            float scaleX = halfSize.x > 0.000001f
                ? Mathf.Max(0.1f, (halfSize.x - inset) / halfSize.x)
                : 1f;
            float scaleZ = halfSize.y > 0.000001f
                ? Mathf.Max(0.1f, (halfSize.y - inset) / halfSize.y)
                : 1f;
            var result = new Vector2[crossSection.Count];
            for (int index = 0; index < crossSection.Count; index++)
            {
                Vector2 relative = crossSection[index] - center;
                result[index] = center + new Vector2(
                    relative.x * scaleX,
                    relative.y * scaleZ);
            }
            return result;
        }

        private static Mesh CreateFlatMesh(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles)
        {
            var flatVertices = new List<Vector3>(triangles.Count);
            var flatNormals = new List<Vector3>(triangles.Count);
            var flatUvs = new List<Vector2>(triangles.Count);
            var flatTriangles = new List<int>(triangles.Count);
            Vector3 minimum = vertices[0];
            Vector3 maximum = vertices[0];
            for (int index = 1; index < vertices.Count; index++)
            {
                minimum = Vector3.Min(minimum, vertices[index]);
                maximum = Vector3.Max(maximum, vertices[index]);
            }

            for (int index = 0; index < triangles.Count; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float squaredMagnitude = cross.sqrMagnitude;
                if (squaredMagnitude <= 0.000000000001f)
                {
                    continue;
                }

                Vector3 normal = cross / Mathf.Sqrt(squaredMagnitude);
                int first = flatVertices.Count;
                flatVertices.Add(a);
                flatVertices.Add(b);
                flatVertices.Add(c);
                flatNormals.Add(normal);
                flatNormals.Add(normal);
                flatNormals.Add(normal);
                flatUvs.Add(ProjectUv(
                    a, normal, a, b, c, minimum, maximum));
                flatUvs.Add(ProjectUv(
                    b, normal, a, b, c, minimum, maximum));
                flatUvs.Add(ProjectUv(
                    c, normal, a, b, c, minimum, maximum));
                flatTriangles.Add(first);
                flatTriangles.Add(first + 1);
                flatTriangles.Add(first + 2);
            }

            var mesh = new Mesh();
            if (flatVertices.Count > 65535)
            {
                mesh.indexFormat =
                    UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(flatVertices);
            mesh.SetNormals(flatNormals);
            mesh.SetUVs(0, flatUvs);
            mesh.SetTriangles(flatTriangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 ProjectUv(
            Vector3 vertex,
            Vector3 normal,
            Vector3 triangleA,
            Vector3 triangleB,
            Vector3 triangleC,
            Vector3 minimum,
            Vector3 maximum)
        {
            if (Mathf.Abs(normal.y) >=
                Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.z)))
            {
                float width = maximum.x - minimum.x;
                float depth = maximum.z - minimum.z;
                float largest = Mathf.Max(
                    Mathf.Max(width, depth),
                    0.000001f);
                float normalizedX =
                    NormalizeCoordinate(vertex.x, minimum.x, maximum.x);
                float normalizedZ =
                    NormalizeCoordinate(vertex.z, minimum.z, maximum.z);
                return new Vector2(
                    0.5f + (normalizedX - 0.5f) * width / largest,
                    0.5f + (normalizedZ - 0.5f) * depth / largest);
            }

            // V always follows the blade. U is the horizontal face tangent,
            // centered and narrowed by the physical face aspect ratio. This
            // preserves texel shape without encoding any lengthwise tiling.
            Vector2 tangent = new Vector2(normal.z, -normal.x).normalized;
            float projectionA =
                triangleA.x * tangent.x + triangleA.z * tangent.y;
            float projectionB =
                triangleB.x * tangent.x + triangleB.z * tangent.y;
            float projectionC =
                triangleC.x * tangent.x + triangleC.z * tangent.y;
            float projectionMinimum = Mathf.Min(
                projectionA,
                Mathf.Min(projectionB, projectionC));
            float projectionMaximum = Mathf.Max(
                projectionA,
                Mathf.Max(projectionB, projectionC));
            float projection =
                vertex.x * tangent.x + vertex.z * tangent.y;
            float horizontal = NormalizeCoordinate(
                projection,
                projectionMinimum,
                projectionMaximum);
            float faceWidth = projectionMaximum - projectionMinimum;
            float faceHeight = Mathf.Max(
                maximum.y - minimum.y,
                0.000001f);
            float aspect = Mathf.Clamp(faceWidth / faceHeight, 0.001f, 1f);
            return new Vector2(
                0.5f + (horizontal - 0.5f) * aspect,
                NormalizeCoordinate(vertex.y, minimum.y, maximum.y));
        }

        private static float NormalizeCoordinate(
            float value,
            float minimum,
            float maximum)
        {
            float range = maximum - minimum;
            return range > 0.000001f
                ? (value - minimum) / range
                : 0.5f;
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

        private void ClearGeneratedSword()
        {
            var partsToDestroy = new HashSet<GameObject>();
            foreach (GameObject part in generatedParts)
            {
                if (part != null &&
                    (furnitureSource == null ||
                     !part.transform.IsChildOf(furnitureSource.transform)))
                {
                    partsToDestroy.Add(part);
                }
            }

            if (furnitureSource != null)
            {
                furnitureSource.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(furnitureSource);
                }
                else
                {
                    DestroyImmediate(furnitureSource);
                }
                furnitureSource = null;
            }

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

            for (int index = generatedMeshes.Count - 1; index >= 0; index--)
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
        }

        private static bool IsGeneratedPartName(string objectName)
        {
            return objectName == BladePartName ||
                objectName == BladeRingJointPartName ||
                objectName == GuardPartName ||
                objectName == HandlePartName ||
                objectName == PommelPartName;
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
