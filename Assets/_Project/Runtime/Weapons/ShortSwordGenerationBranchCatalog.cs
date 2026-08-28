using System;
using System.Collections.Generic;

namespace WorldBuilder.Gameplay.Weapons
{
    public enum ShortSwordFamily
    {
        Cruciform = 0,
        Leafblade = 1,
        Legionary = 2,
        Piercer = 3,
        Seax = 4,
        Falchion = 5,
        Kopis = 6,
        Hanger = 7
    }

    public enum ShortSwordBladeBaseStyle
    {
        Plain = 0,
        NarrowRicasso = 1,
        FlaredShoulders = 2,
        SteppedShoulders = 3,
        SmallChoil = 4,
        ReinforcedBase = 5
    }

    public enum ShortSwordBladeSectionStyle
    {
        DiamondRidge = 0,
        BroadMidrib = 1,
        FlatBevel = 2,
        ShallowFuller = 3,
        HexagonalRidge = 4
    }

    public enum ShortSwordGuardBindingStyle
    {
        None = 0,
        LeftLeather = 1,
        RightLeather = 2,
        BothArms = 3,
        LeftCord = 4,
        RightCord = 5
    }

    public enum ShortSwordHandleCrossSection
    {
        Hexagonal = 0,
        Octagonal = 1,
        Decagonal = 2,
        OvalFaceted = 3
    }

    public enum ShortSwordFacetTier
    {
        Coarse = 0,
        Standard = 1,
        Intricate = 2
    }

    public enum ShortSwordHeroZone
    {
        Blade = 0,
        Guard = 1,
        Grip = 2,
        Balanced = 3
    }

    public enum ShortSwordBranchUiCategory
    {
        Identity = 0,
        Blade = 1,
        Guard = 2,
        Grip = 3,
        Pommel = 4,
        Materials = 5,
        Ornament = 6,
        Faceting = 7
    }

    public sealed class ShortSwordGenerationBranchOption
    {
        public ShortSwordGenerationBranchOption(
            int value,
            string label,
            string tooltip)
        {
            Value = value;
            Label = label ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
        }

        public int Value { get; }
        public string Label { get; }
        public string Tooltip { get; }
    }

    public sealed class ShortSwordGenerationBranchGroup
    {
        public ShortSwordGenerationBranchGroup(
            ShortSwordGenerationDecision decision,
            ShortSwordBranchUiCategory category,
            string heading,
            string tooltip,
            IReadOnlyList<ShortSwordGenerationBranchOption> options)
        {
            Decision = decision;
            Category = category;
            Heading = heading ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            Options = options ?? Array.Empty<ShortSwordGenerationBranchOption>();
        }

        public ShortSwordGenerationDecision Decision { get; }
        public ShortSwordBranchUiCategory Category { get; }
        public string Heading { get; }
        public string Tooltip { get; }
        public IReadOnlyList<ShortSwordGenerationBranchOption> Options { get; }
    }

    /// <summary>
    /// One ordered source of truth for the generator's lockable branches and
    /// the family grammar that keeps those branches visually coherent.
    /// </summary>
    public static class ShortSwordGenerationBranchCatalog
    {
        private static readonly ShortSwordFamily[] AllFamilies =
        {
            ShortSwordFamily.Cruciform,
            ShortSwordFamily.Leafblade,
            ShortSwordFamily.Legionary,
            ShortSwordFamily.Piercer
        };

        private static readonly ShortSwordGenerationBranchGroup[] OrderedGroups =
        {
            Group(
                ShortSwordGenerationDecision.Family,
                ShortSwordBranchUiCategory.Identity,
                "Sword Family",
                "The authored silhouette grammar that constrains all later branches.",
                EnumOptions(AllFamilies)),
            Group(
                ShortSwordGenerationDecision.HeroZone,
                ShortSwordBranchUiCategory.Identity,
                "Hero Zone",
                "Chooses which assembly region receives the strongest visual emphasis.",
                EnumOptions(
                    ShortSwordHeroZone.Blade,
                    ShortSwordHeroZone.Guard,
                    ShortSwordHeroZone.Grip,
                    ShortSwordHeroZone.Balanced)),
            Group(
                ShortSwordGenerationDecision.Directionality,
                ShortSwordBranchUiCategory.Identity,
                "Directionality",
                "Conventional swords remain centered; directional swords share one handed side across blade and furniture.",
                EnumOptions(
                    ShortSwordDirectionality.Conventional,
                    ShortSwordDirectionality.Directional)),
            Group(
                ShortSwordGenerationDecision.DirectionSide,
                ShortSwordBranchUiCategory.Identity,
                "Directional Side",
                "Mirrors every handed blade, guard, binding, and pommel decision together.",
                EnumOptions(
                    ShortSwordDirectionSide.Left,
                    ShortSwordDirectionSide.Right)),

            Group(
                ShortSwordGenerationDecision.BladeProfile,
                ShortSwordBranchUiCategory.Blade,
                "Blade Profile",
                "The primary outline of the blade from shoulder to point.",
                EnumOptions(
                    ShortSwordBladeProfile.StraightPoint,
                    ShortSwordBladeProfile.LongTaper,
                    ShortSwordBladeProfile.RoundedShoulder,
                    ShortSwordBladeProfile.ForwardSwept,
                    ShortSwordBladeProfile.ClipPoint,
                    ShortSwordBladeProfile.LeafBlade,
                    ShortSwordBladeProfile.Gladius,
                    ShortSwordBladeProfile.PiercingDiamond)),
            Group(
                ShortSwordGenerationDecision.BladeBackStyle,
                ShortSwordBranchUiCategory.Blade,
                "Spine Treatment",
                "A restrained physical treatment applied to the blade's authored spine.",
                EnumOptions(
                    ShortSwordBladeBackStyle.Clean,
                    ShortSwordBladeBackStyle.Sawback,
                    ShortSwordBladeBackStyle.SteppedSpine,
                    ShortSwordBladeBackStyle.ReinforcedSpine,
                    ShortSwordBladeBackStyle.ScallopedSpine,
                    ShortSwordBladeBackStyle.BrokenBack)),
            Group(
                ShortSwordGenerationDecision.BladeBaseStyle,
                ShortSwordBranchUiCategory.Blade,
                "Blade Base",
                "Controls the transition between the cutting profile and its fitted guard seat.",
                EnumOptions(
                    ShortSwordBladeBaseStyle.Plain,
                    ShortSwordBladeBaseStyle.NarrowRicasso,
                    ShortSwordBladeBaseStyle.FlaredShoulders,
                    ShortSwordBladeBaseStyle.SteppedShoulders,
                    ShortSwordBladeBaseStyle.SmallChoil,
                    ShortSwordBladeBaseStyle.ReinforcedBase)),
            Group(
                ShortSwordGenerationDecision.BladeSectionStyle,
                ShortSwordBranchUiCategory.Blade,
                "Blade Section",
                "Selects an integrated low-poly ridge, bevel, or fuller cross-section.",
                EnumOptions(
                    ShortSwordBladeSectionStyle.DiamondRidge,
                    ShortSwordBladeSectionStyle.BroadMidrib,
                    ShortSwordBladeSectionStyle.FlatBevel,
                    ShortSwordBladeSectionStyle.ShallowFuller,
                    ShortSwordBladeSectionStyle.HexagonalRidge)),

            Group(
                ShortSwordGenerationDecision.GuardConstruction,
                ShortSwordBranchUiCategory.Guard,
                "Guard Construction",
                "The authored quillon or bolster structure fitted to the selected blade family.",
                EnumOptions(
                    ShortSwordGuardConstruction.RazorBar,
                    ShortSwordGuardConstruction.BladeQuillons,
                    ShortSwordGuardConstruction.WingedW,
                    ShortSwordGuardConstruction.Crescent,
                    ShortSwordGuardConstruction.DirectionalSweep,
                    ShortSwordGuardConstruction.OffsetLeaf,
                    ShortSwordGuardConstruction.MinimalBolster,
                    ShortSwordGuardConstruction.DownturnedHooks,
                    ShortSwordGuardConstruction.GreekWings,
                    ShortSwordGuardConstruction.SQuillons,
                    ShortSwordGuardConstruction.LobedCross)),
            Group(
                ShortSwordGenerationDecision.GuardBindingStyle,
                ShortSwordBranchUiCategory.Guard,
                "Guard Binding",
                "Adds a fitted leather or cord sleeve to a viable guard arm without covering its joint.",
                EnumOptions(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord)),

            Group(
                ShortSwordGenerationDecision.HandleProfile,
                ShortSwordBranchUiCategory.Grip,
                "Handle Profile",
                "The continuous fitted silhouette beneath the guard.",
                EnumOptions(
                    ShortSwordHandleProfile.Straight,
                    ShortSwordHandleProfile.Tapered,
                    ShortSwordHandleProfile.Waisted,
                    ShortSwordHandleProfile.PalmSwell,
                    ShortSwordHandleProfile.FlaredEnds)),
            Group(
                ShortSwordGenerationDecision.HandleCrossSection,
                ShortSwordBranchUiCategory.Grip,
                "Handle Facets",
                "Sets the hard-sided handle cross-section used by its fitted grip geometry.",
                EnumOptions(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.Decagonal,
                    ShortSwordHandleCrossSection.OvalFaceted)),
            Group(
                ShortSwordGenerationDecision.GripStyle,
                ShortSwordBranchUiCategory.Grip,
                "Grip Construction",
                "A complete authored grip treatment rather than independently layered decoration.",
                EnumOptions(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.RibbedWood,
                    ShortSwordGripStyle.StuddedLeather,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HerringboneCord,
                    ShortSwordGripStyle.HalfWrappedWood,
                    ShortSwordGripStyle.FacetedLeather,
                    ShortSwordGripStyle.WireBoundLeather)),
            Group(
                ShortSwordGenerationDecision.GripColor,
                ShortSwordBranchUiCategory.Grip,
                "Grip Color",
                "The bounded leather, cord, or wood color family.",
                EnumOptions(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.ForestGreen,
                    ShortSwordGripColor.Navy,
                    ShortSwordGripColor.Bone,
                    ShortSwordGripColor.Ochre)),

            Group(
                ShortSwordGenerationDecision.HiltProfile,
                ShortSwordBranchUiCategory.Pommel,
                "Pommel Profile",
                "The counterweight silhouette joined directly to the handle's lower envelope.",
                EnumOptions(
                    ShortSwordHiltProfile.Disc,
                    ShortSwordHiltProfile.Faceted,
                    ShortSwordHiltProfile.ScentStopper,
                    ShortSwordHiltProfile.Crowned,
                    ShortSwordHiltProfile.Hooked,
                    ShortSwordHiltProfile.Acorn,
                    ShortSwordHiltProfile.BrazilNut,
                    ShortSwordHiltProfile.Mushroom,
                    ShortSwordHiltProfile.Fishtail,
                    ShortSwordHiltProfile.Ring,
                    ShortSwordHiltProfile.Beaked)),

            Group(
                ShortSwordGenerationDecision.MetalFamily,
                ShortSwordBranchUiCategory.Materials,
                "Metal Family",
                "Coordinates blade furniture and pommel while retaining a readable steel blade.",
                EnumOptions(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Bronze,
                    ShortSwordMetalFamily.Silver,
                    ShortSwordMetalFamily.BlackenedSteel,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel,
                    ShortSwordMetalFamily.CopperAlloy)),

            Group(
                ShortSwordGenerationDecision.OrnamentStyle,
                ShortSwordBranchUiCategory.Ornament,
                "Ornament",
                "Keeps embellishment plain or assigns one viable fitted jewel socket.",
                EnumOptions(
                    ShortSwordOrnamentStyle.Plain,
                    ShortSwordOrnamentStyle.GuardGem,
                    ShortSwordOrnamentStyle.PommelGem)),
            Group(
                ShortSwordGenerationDecision.GemFamily,
                ShortSwordBranchUiCategory.Ornament,
                "Gem Family",
                "The restrained color family used by a fitted jewel.",
                EnumOptions(
                    ShortSwordGemFamily.Ruby,
                    ShortSwordGemFamily.Emerald,
                    ShortSwordGemFamily.Sapphire,
                    ShortSwordGemFamily.Amber)),
            Group(
                ShortSwordGenerationDecision.GemCut,
                ShortSwordBranchUiCategory.Ornament,
                "Gem Cut",
                "The familiar low-poly outline of a fitted jewel.",
                EnumOptions(
                    ShortSwordGemCut.Round,
                    ShortSwordGemCut.Oval,
                    ShortSwordGemCut.PrincessSquare,
                    ShortSwordGemCut.Emerald,
                    ShortSwordGemCut.Pear)),

            Group(
                ShortSwordGenerationDecision.FacetTier,
                ShortSwordBranchUiCategory.Faceting,
                "Facet Tier",
                "Coordinates polygon density across the whole sword instead of mixing unrelated resolutions.",
                EnumOptions(
                    ShortSwordFacetTier.Coarse,
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate)),
            Group(
                ShortSwordGenerationDecision.GuardCrossSectionSides,
                ShortSwordBranchUiCategory.Faceting,
                "Guard Section Sides",
                "Overrides the hard-sided cross-section of the guard arms.",
                NumericOptions(4, 6, 8, 10, 12)),
            Group(
                ShortSwordGenerationDecision.GuardCurveSegments,
                ShortSwordBranchUiCategory.Faceting,
                "Guard Curve Segments",
                "Overrides the even longitudinal segment count while retaining an exact center joint.",
                NumericOptions(6, 8, 10, 12, 14))
        };

        private static readonly Dictionary<
            ShortSwordFamily,
            Dictionary<ShortSwordGenerationDecision, int[]>> FamilyCandidates =
                BuildFamilyCandidates();
        private static readonly Dictionary<ShortSwordGenerationDecision, int[]>
            AllCandidateValues = BuildAllCandidateValues();

        public static IReadOnlyList<ShortSwordGenerationBranchGroup> Groups =>
            OrderedGroups;
        public static IReadOnlyList<ShortSwordFamily> Families =>
            AllFamilies;

        public static bool IsActiveFamily(ShortSwordFamily family)
        {
            for (int index = 0; index < AllFamilies.Length; index++)
            {
                if (AllFamilies[index] == family)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TryGetGroup(
            ShortSwordGenerationDecision decision,
            out ShortSwordGenerationBranchGroup group)
        {
            for (int index = 0; index < OrderedGroups.Length; index++)
            {
                if (OrderedGroups[index].Decision == decision)
                {
                    group = OrderedGroups[index];
                    return true;
                }
            }

            group = null;
            return false;
        }

        public static float CalculateContentHeight(
            int columns,
            float optionHeight = 28f,
            float optionGap = 6f,
            float groupHeadingHeight = 24f,
            float groupBottomSpacing = 12f,
            float categoryHeadingHeight = 32f,
            float categorySpacing = 6f)
        {
            int safeColumns = Math.Max(1, columns);
            float height = 0f;
            ShortSwordBranchUiCategory? previousCategory = null;
            for (int index = 0; index < OrderedGroups.Length; index++)
            {
                ShortSwordGenerationBranchGroup group = OrderedGroups[index];
                if (!previousCategory.HasValue ||
                    previousCategory.Value != group.Category)
                {
                    height += categoryHeadingHeight;
                    if (previousCategory.HasValue)
                    {
                        height += categorySpacing;
                    }
                    previousCategory = group.Category;
                }

                int rows = (group.Options.Count + safeColumns - 1) /
                    safeColumns;
                height += groupHeadingHeight +
                    rows * (optionHeight + optionGap) +
                    groupBottomSpacing;
            }

            return Math.Max(1f, height);
        }

        public static bool TryReadValue(
            ProceduralShortSwordDefinition definition,
            ShortSwordGenerationDecision decision,
            out int value)
        {
            switch (decision)
            {
                case ShortSwordGenerationDecision.Family:
                    value = (int)definition.Family;
                    return true;
                case ShortSwordGenerationDecision.HeroZone:
                    value = (int)definition.HeroZone;
                    return true;
                case ShortSwordGenerationDecision.Directionality:
                    value = (int)(IsDirectionalBladeProfile(
                            definition.BladeProfile)
                        ? ShortSwordDirectionality.Directional
                        : ShortSwordDirectionality.Conventional);
                    return true;
                case ShortSwordGenerationDecision.DirectionSide:
                    value = definition.DirectionSign < 0
                        ? (int)ShortSwordDirectionSide.Left
                        : (int)ShortSwordDirectionSide.Right;
                    return true;
                case ShortSwordGenerationDecision.BladeProfile:
                    value = (int)definition.BladeProfile;
                    return true;
                case ShortSwordGenerationDecision.BladeBackStyle:
                    value = (int)definition.BladeBackStyle;
                    return true;
                case ShortSwordGenerationDecision.BladeBaseStyle:
                    value = (int)definition.BladeBaseStyle;
                    return true;
                case ShortSwordGenerationDecision.BladeSectionStyle:
                    value = (int)definition.BladeSectionStyle;
                    return true;
                case ShortSwordGenerationDecision.GuardConstruction:
                    value = (int)definition.GuardConstruction;
                    return true;
                case ShortSwordGenerationDecision.GuardBindingStyle:
                    value = (int)definition.GuardBindingStyle;
                    return true;
                case ShortSwordGenerationDecision.HandleProfile:
                    value = (int)definition.HandleProfile;
                    return true;
                case ShortSwordGenerationDecision.HandleCrossSection:
                    value = (int)definition.HandleCrossSection;
                    return true;
                case ShortSwordGenerationDecision.GripStyle:
                    value = (int)definition.GripStyle;
                    return true;
                case ShortSwordGenerationDecision.GripColor:
                    value = (int)definition.GripColor;
                    return true;
                case ShortSwordGenerationDecision.HiltProfile:
                    value = (int)definition.HiltProfile;
                    return true;
                case ShortSwordGenerationDecision.MetalFamily:
                    value = (int)definition.MetalFamily;
                    return true;
                case ShortSwordGenerationDecision.OrnamentStyle:
                    value = (int)definition.OrnamentStyle;
                    return true;
                case ShortSwordGenerationDecision.GemFamily:
                    value = (int)definition.GemFamily;
                    return true;
                case ShortSwordGenerationDecision.GemCut:
                    value = (int)definition.GemCut;
                    return true;
                case ShortSwordGenerationDecision.FacetTier:
                    value = (int)definition.FacetTier;
                    return true;
                case ShortSwordGenerationDecision.GuardCrossSectionSides:
                    value = definition.GuardCrossSectionSides;
                    return true;
                case ShortSwordGenerationDecision.GuardCurveSegments:
                    value = definition.GuardCurveSegments;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        public static IReadOnlyList<int> GetCandidateValues(
            ShortSwordFamily family,
            ShortSwordGenerationDecision decision)
        {
            if (decision == ShortSwordGenerationDecision.Family)
            {
                return IsActiveFamily(family)
                    ? new[] { (int)family }
                    : Array.Empty<int>();
            }
            if (FamilyCandidates.TryGetValue(family, out var familyRules) &&
                familyRules.TryGetValue(decision, out int[] candidates))
            {
                return candidates;
            }
            if (!TryGetGroup(decision, out ShortSwordGenerationBranchGroup group))
            {
                return Array.Empty<int>();
            }

            return AllCandidateValues.TryGetValue(decision, out int[] allValues)
                ? allValues
                : Array.Empty<int>();
        }

        public static bool IsFamilyCompatible(
            ShortSwordFamily family,
            ShortSwordGenerationDecision decision,
            int value)
        {
            if (!IsActiveFamily(family))
            {
                return false;
            }
            IReadOnlyList<int> candidates = GetCandidateValues(
                family,
                decision);
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index] == value)
                {
                    return true;
                }
            }
            return false;
        }

        public static IReadOnlyList<ShortSwordFamily> GetCompatibleFamilies(
            ShortSwordGenerationDecision decision,
            int value)
        {
            var compatible = new List<ShortSwordFamily>(AllFamilies.Length);
            var singleLock = new[]
            {
                new ShortSwordGenerationLock(decision, value)
            };
            for (int index = 0; index < AllFamilies.Length; index++)
            {
                if (IsFamilyCompatibleWithLocks(
                        AllFamilies[index],
                        singleLock))
                {
                    compatible.Add(AllFamilies[index]);
                }
            }
            return compatible;
        }

        public static bool IsFamilyCompatibleWithLocks(
            ShortSwordFamily family,
            IReadOnlyList<ShortSwordGenerationLock> locks)
        {
            if (!IsActiveFamily(family))
            {
                return false;
            }
            if (locks == null)
            {
                return true;
            }
            for (int index = 0; index < locks.Count; index++)
            {
                ShortSwordGenerationLock generationLock = locks[index];
                if (!IsFamilyCompatible(
                        family,
                        generationLock.Decision,
                        generationLock.Value))
                {
                    return false;
                }
            }

            IReadOnlyList<int> facetCandidates = GetCandidateValues(
                family,
                ShortSwordGenerationDecision.FacetTier);
            for (int tierIndex = 0;
                 tierIndex < facetCandidates.Count;
                 tierIndex++)
            {
                var tier = (ShortSwordFacetTier)facetCandidates[tierIndex];
                bool compatible = true;
                for (int lockIndex = 0;
                     lockIndex < locks.Count;
                     lockIndex++)
                {
                    ShortSwordGenerationLock generationLock = locks[lockIndex];
                    if (generationLock.Decision ==
                            ShortSwordGenerationDecision.FacetTier &&
                        generationLock.Value != (int)tier)
                    {
                        compatible = false;
                        break;
                    }
                    if (!IsFacetTierCompatible(
                            tier,
                            generationLock.Decision,
                            generationLock.Value))
                    {
                        compatible = false;
                        break;
                    }
                }
                if (compatible)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsDirectionalBladeProfile(
            ShortSwordBladeProfile profile)
        {
            return profile == ShortSwordBladeProfile.ForwardSwept ||
                profile == ShortSwordBladeProfile.ClipPoint ||
                profile == ShortSwordBladeProfile.Seax ||
                profile == ShortSwordBladeProfile.Falchion ||
                profile == ShortSwordBladeProfile.Kopis ||
                profile == ShortSwordBladeProfile.Hanger;
        }

        public static bool IsFacetTierCompatible(
            ShortSwordFacetTier tier,
            ShortSwordGenerationDecision decision,
            int value)
        {
            switch (decision)
            {
                case ShortSwordGenerationDecision.GuardCrossSectionSides:
                    return tier switch
                    {
                        ShortSwordFacetTier.Coarse => value == 4 || value == 6,
                        ShortSwordFacetTier.Intricate =>
                            value == 8 || value == 10 || value == 12,
                        _ => value == 6 || value == 8 || value == 10
                    };
                case ShortSwordGenerationDecision.GuardCurveSegments:
                    return tier switch
                    {
                        ShortSwordFacetTier.Coarse => value == 6 || value == 8,
                        ShortSwordFacetTier.Intricate =>
                            value == 10 || value == 12 || value == 14,
                        _ => value == 8 || value == 10 || value == 12
                    };
                case ShortSwordGenerationDecision.HandleCrossSection:
                    var section = (ShortSwordHandleCrossSection)value;
                    return tier switch
                    {
                        ShortSwordFacetTier.Coarse => section is
                            ShortSwordHandleCrossSection.Hexagonal or
                            ShortSwordHandleCrossSection.Octagonal or
                            ShortSwordHandleCrossSection.OvalFaceted,
                        ShortSwordFacetTier.Intricate => section is
                            ShortSwordHandleCrossSection.Octagonal or
                            ShortSwordHandleCrossSection.Decagonal or
                            ShortSwordHandleCrossSection.OvalFaceted,
                        _ => true
                    };
                default:
                    return true;
            }
        }

        private static Dictionary<
            ShortSwordFamily,
            Dictionary<ShortSwordGenerationDecision, int[]>>
            BuildFamilyCandidates()
        {
            var candidates = new Dictionary<
                ShortSwordFamily,
                Dictionary<ShortSwordGenerationDecision, int[]>>
            {
                [ShortSwordFamily.Cruciform] = Rules(
                    conventional: true,
                    profiles: Values(
                        ShortSwordBladeProfile.StraightPoint,
                        ShortSwordBladeProfile.LongTaper,
                        ShortSwordBladeProfile.RoundedShoulder,
                        ShortSwordBladeProfile.PiercingDiamond),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.SteppedSpine,
                        ShortSwordBladeBackStyle.ReinforcedSpine),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.NarrowRicasso,
                        ShortSwordBladeBaseStyle.SteppedShoulders,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.DiamondRidge,
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.ShallowFuller,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.RazorBar,
                        ShortSwordGuardConstruction.BladeQuillons,
                        ShortSwordGuardConstruction.Crescent,
                        ShortSwordGuardConstruction.DownturnedHooks,
                        ShortSwordGuardConstruction.LobedCross),
                    handles: Values(
                        ShortSwordHandleProfile.Straight,
                        ShortSwordHandleProfile.Tapered,
                        ShortSwordHandleProfile.Waisted,
                        ShortSwordHandleProfile.PalmSwell),
                    hilts: Values(
                        ShortSwordHiltProfile.Disc,
                        ShortSwordHiltProfile.Faceted,
                        ShortSwordHiltProfile.ScentStopper,
                        ShortSwordHiltProfile.Crowned,
                        ShortSwordHiltProfile.Acorn,
                        ShortSwordHiltProfile.BrazilNut,
                        ShortSwordHiltProfile.Mushroom)),

                [ShortSwordFamily.Leafblade] = Rules(
                    conventional: true,
                    profiles: Values(
                        ShortSwordBladeProfile.LeafBlade,
                        ShortSwordBladeProfile.RoundedShoulder,
                        ShortSwordBladeProfile.LongTaper),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.ReinforcedSpine,
                        ShortSwordBladeBackStyle.ScallopedSpine),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.FlaredShoulders,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.BroadMidrib,
                        ShortSwordBladeSectionStyle.DiamondRidge,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.WingedW,
                        ShortSwordGuardConstruction.Crescent,
                        ShortSwordGuardConstruction.GreekWings,
                        ShortSwordGuardConstruction.LobedCross),
                    handles: Values(
                        ShortSwordHandleProfile.Waisted,
                        ShortSwordHandleProfile.PalmSwell,
                        ShortSwordHandleProfile.FlaredEnds),
                    hilts: Values(
                        ShortSwordHiltProfile.Faceted,
                        ShortSwordHiltProfile.Crowned,
                        ShortSwordHiltProfile.Acorn,
                        ShortSwordHiltProfile.Mushroom,
                        ShortSwordHiltProfile.Ring)),

                [ShortSwordFamily.Legionary] = Rules(
                    conventional: true,
                    profiles: Values(
                        ShortSwordBladeProfile.Gladius,
                        ShortSwordBladeProfile.StraightPoint,
                        ShortSwordBladeProfile.LongTaper),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.ReinforcedSpine,
                        ShortSwordBladeBackStyle.SteppedSpine),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.FlaredShoulders,
                        ShortSwordBladeBaseStyle.SteppedShoulders,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.DiamondRidge,
                        ShortSwordBladeSectionStyle.BroadMidrib,
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.MinimalBolster,
                        ShortSwordGuardConstruction.RazorBar,
                        ShortSwordGuardConstruction.GreekWings,
                        ShortSwordGuardConstruction.LobedCross),
                    handles: Values(
                        ShortSwordHandleProfile.Straight,
                        ShortSwordHandleProfile.Waisted,
                        ShortSwordHandleProfile.PalmSwell,
                        ShortSwordHandleProfile.FlaredEnds),
                    hilts: Values(
                        ShortSwordHiltProfile.Disc,
                        ShortSwordHiltProfile.Faceted,
                        ShortSwordHiltProfile.Acorn,
                        ShortSwordHiltProfile.BrazilNut,
                        ShortSwordHiltProfile.Mushroom)),

                [ShortSwordFamily.Piercer] = Rules(
                    conventional: true,
                    profiles: Values(
                        ShortSwordBladeProfile.PiercingDiamond,
                        ShortSwordBladeProfile.LongTaper,
                        ShortSwordBladeProfile.StraightPoint),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.ReinforcedSpine),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.NarrowRicasso,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.DiamondRidge,
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.RazorBar,
                        ShortSwordGuardConstruction.BladeQuillons,
                        ShortSwordGuardConstruction.MinimalBolster,
                        ShortSwordGuardConstruction.GreekWings),
                    handles: Values(
                        ShortSwordHandleProfile.Straight,
                        ShortSwordHandleProfile.Tapered,
                        ShortSwordHandleProfile.PalmSwell),
                    hilts: Values(
                        ShortSwordHiltProfile.Disc,
                        ShortSwordHiltProfile.Faceted,
                        ShortSwordHiltProfile.ScentStopper,
                        ShortSwordHiltProfile.Acorn,
                        ShortSwordHiltProfile.Beaked)),

                [ShortSwordFamily.Seax] = Rules(
                    conventional: false,
                    profiles: Values(
                        ShortSwordBladeProfile.Seax,
                        ShortSwordBladeProfile.ClipPoint,
                        ShortSwordBladeProfile.Hanger),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.Sawback,
                        ShortSwordBladeBackStyle.SteppedSpine,
                        ShortSwordBladeBackStyle.ReinforcedSpine,
                        ShortSwordBladeBackStyle.ScallopedSpine,
                        ShortSwordBladeBackStyle.BrokenBack),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.NarrowRicasso,
                        ShortSwordBladeBaseStyle.SmallChoil,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.DiamondRidge,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.MinimalBolster,
                        ShortSwordGuardConstruction.OffsetLeaf,
                        ShortSwordGuardConstruction.BladeQuillons,
                        ShortSwordGuardConstruction.DownturnedHooks),
                    handles: Values(
                        ShortSwordHandleProfile.Straight,
                        ShortSwordHandleProfile.Tapered,
                        ShortSwordHandleProfile.PalmSwell,
                        ShortSwordHandleProfile.FlaredEnds),
                    hilts: Values(
                        ShortSwordHiltProfile.Faceted,
                        ShortSwordHiltProfile.Hooked,
                        ShortSwordHiltProfile.BrazilNut,
                        ShortSwordHiltProfile.Fishtail,
                        ShortSwordHiltProfile.Beaked)),

                [ShortSwordFamily.Falchion] = Rules(
                    conventional: false,
                    profiles: Values(
                        ShortSwordBladeProfile.Falchion,
                        ShortSwordBladeProfile.ForwardSwept,
                        ShortSwordBladeProfile.ClipPoint,
                        ShortSwordBladeProfile.Hanger),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.Sawback,
                        ShortSwordBladeBackStyle.SteppedSpine,
                        ShortSwordBladeBackStyle.ReinforcedSpine,
                        ShortSwordBladeBackStyle.ScallopedSpine),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.NarrowRicasso,
                        ShortSwordBladeBaseStyle.SmallChoil,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.ShallowFuller,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.DirectionalSweep,
                        ShortSwordGuardConstruction.OffsetLeaf,
                        ShortSwordGuardConstruction.Crescent,
                        ShortSwordGuardConstruction.DownturnedHooks,
                        ShortSwordGuardConstruction.SQuillons),
                    handles: Values(
                        ShortSwordHandleProfile.Tapered,
                        ShortSwordHandleProfile.Waisted,
                        ShortSwordHandleProfile.PalmSwell,
                        ShortSwordHandleProfile.FlaredEnds),
                    hilts: Values(
                        ShortSwordHiltProfile.Disc,
                        ShortSwordHiltProfile.Hooked,
                        ShortSwordHiltProfile.Fishtail,
                        ShortSwordHiltProfile.Beaked,
                        ShortSwordHiltProfile.Ring)),

                [ShortSwordFamily.Kopis] = Rules(
                    conventional: false,
                    profiles: Values(
                        ShortSwordBladeProfile.Kopis,
                        ShortSwordBladeProfile.ForwardSwept),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.ReinforcedSpine,
                        ShortSwordBladeBackStyle.ScallopedSpine),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.FlaredShoulders,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.BroadMidrib,
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.DiamondRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.GreekWings,
                        ShortSwordGuardConstruction.Crescent,
                        ShortSwordGuardConstruction.DirectionalSweep,
                        ShortSwordGuardConstruction.OffsetLeaf,
                        ShortSwordGuardConstruction.DownturnedHooks),
                    handles: Values(
                        ShortSwordHandleProfile.Waisted,
                        ShortSwordHandleProfile.PalmSwell,
                        ShortSwordHandleProfile.FlaredEnds),
                    hilts: Values(
                        ShortSwordHiltProfile.Hooked,
                        ShortSwordHiltProfile.Acorn,
                        ShortSwordHiltProfile.Fishtail,
                        ShortSwordHiltProfile.Beaked,
                        ShortSwordHiltProfile.Ring)),

                [ShortSwordFamily.Hanger] = Rules(
                    conventional: false,
                    profiles: Values(
                        ShortSwordBladeProfile.Hanger,
                        ShortSwordBladeProfile.ClipPoint,
                        ShortSwordBladeProfile.Falchion,
                        ShortSwordBladeProfile.Seax),
                    backs: Values(
                        ShortSwordBladeBackStyle.Clean,
                        ShortSwordBladeBackStyle.SteppedSpine,
                        ShortSwordBladeBackStyle.ReinforcedSpine,
                        ShortSwordBladeBackStyle.BrokenBack),
                    bases: Values(
                        ShortSwordBladeBaseStyle.Plain,
                        ShortSwordBladeBaseStyle.NarrowRicasso,
                        ShortSwordBladeBaseStyle.SmallChoil,
                        ShortSwordBladeBaseStyle.ReinforcedBase),
                    sections: Values(
                        ShortSwordBladeSectionStyle.FlatBevel,
                        ShortSwordBladeSectionStyle.ShallowFuller,
                        ShortSwordBladeSectionStyle.HexagonalRidge),
                    guards: Values(
                        ShortSwordGuardConstruction.SQuillons,
                        ShortSwordGuardConstruction.DownturnedHooks,
                        ShortSwordGuardConstruction.OffsetLeaf,
                        ShortSwordGuardConstruction.MinimalBolster,
                        ShortSwordGuardConstruction.Crescent),
                    handles: Values(
                        ShortSwordHandleProfile.Straight,
                        ShortSwordHandleProfile.Tapered,
                        ShortSwordHandleProfile.PalmSwell,
                        ShortSwordHandleProfile.FlaredEnds),
                    hilts: Values(
                        ShortSwordHiltProfile.Disc,
                        ShortSwordHiltProfile.Hooked,
                        ShortSwordHiltProfile.Fishtail,
                        ShortSwordHiltProfile.Beaked,
                        ShortSwordHiltProfile.Ring))
            };
            AddFurnitureCandidates(candidates);
            return candidates;
        }

        private static void AddFurnitureCandidates(
            Dictionary<ShortSwordFamily, Dictionary<
                ShortSwordGenerationDecision, int[]>> candidates)
        {
            Furniture(
                candidates[ShortSwordFamily.Cruciform],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.Decagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.StuddedLeather,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HerringboneCord,
                    ShortSwordGripStyle.FacetedLeather,
                    ShortSwordGripStyle.WireBoundLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.ForestGreen,
                    ShortSwordGripColor.Navy,
                    ShortSwordGripColor.Bone),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Silver,
                    ShortSwordMetalFamily.BlackenedSteel,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel),
                facets: Values(
                    ShortSwordFacetTier.Coarse,
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate));

            Furniture(
                candidates[ShortSwordFamily.Leafblade],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.Decagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.RibbedWood,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HerringboneCord,
                    ShortSwordGripStyle.HalfWrappedWood,
                    ShortSwordGripStyle.FacetedLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.ForestGreen,
                    ShortSwordGripColor.Bone,
                    ShortSwordGripColor.Ochre),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Bronze,
                    ShortSwordMetalFamily.Silver,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel,
                    ShortSwordMetalFamily.CopperAlloy),
                facets: Values(
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate));

            Furniture(
                candidates[ShortSwordFamily.Legionary],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.BothArms),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.RibbedWood,
                    ShortSwordGripStyle.StuddedLeather,
                    ShortSwordGripStyle.HalfWrappedWood,
                    ShortSwordGripStyle.FacetedLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.Bone,
                    ShortSwordGripColor.Ochre),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Bronze,
                    ShortSwordMetalFamily.Silver,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.CopperAlloy),
                facets: Values(
                    ShortSwordFacetTier.Coarse,
                    ShortSwordFacetTier.Standard));

            Furniture(
                candidates[ShortSwordFamily.Piercer],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.Decagonal),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HerringboneCord,
                    ShortSwordGripStyle.FacetedLeather,
                    ShortSwordGripStyle.WireBoundLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.Navy,
                    ShortSwordGripColor.Bone),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Silver,
                    ShortSwordMetalFamily.BlackenedSteel,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel),
                facets: Values(
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate));

            Furniture(
                candidates[ShortSwordFamily.Seax],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.RibbedWood,
                    ShortSwordGripStyle.StuddedLeather,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HalfWrappedWood,
                    ShortSwordGripStyle.WireBoundLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.ForestGreen,
                    ShortSwordGripColor.Ochre),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.BlackenedSteel,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel),
                facets: Values(
                    ShortSwordFacetTier.Coarse,
                    ShortSwordFacetTier.Standard));

            Furniture(
                candidates[ShortSwordFamily.Falchion],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.Decagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.StuddedLeather,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HerringboneCord,
                    ShortSwordGripStyle.HalfWrappedWood,
                    ShortSwordGripStyle.FacetedLeather,
                    ShortSwordGripStyle.WireBoundLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.Navy,
                    ShortSwordGripColor.Ochre),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Bronze,
                    ShortSwordMetalFamily.BlackenedSteel,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel,
                    ShortSwordMetalFamily.CopperAlloy),
                facets: Values(
                    ShortSwordFacetTier.Coarse,
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate));

            Furniture(
                candidates[ShortSwordFamily.Kopis],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.RibbedWood,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HerringboneCord,
                    ShortSwordGripStyle.HalfWrappedWood),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.ForestGreen,
                    ShortSwordGripColor.Bone,
                    ShortSwordGripColor.Ochre),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Bronze,
                    ShortSwordMetalFamily.Silver,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.CopperAlloy),
                facets: Values(
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate));

            Furniture(
                candidates[ShortSwordFamily.Hanger],
                bindings: Values(
                    ShortSwordGuardBindingStyle.None,
                    ShortSwordGuardBindingStyle.LeftLeather,
                    ShortSwordGuardBindingStyle.RightLeather,
                    ShortSwordGuardBindingStyle.BothArms,
                    ShortSwordGuardBindingStyle.LeftCord,
                    ShortSwordGuardBindingStyle.RightCord),
                handleSections: Values(
                    ShortSwordHandleCrossSection.Hexagonal,
                    ShortSwordHandleCrossSection.Octagonal,
                    ShortSwordHandleCrossSection.Decagonal,
                    ShortSwordHandleCrossSection.OvalFaceted),
                grips: Values(
                    ShortSwordGripStyle.LeatherBands,
                    ShortSwordGripStyle.CrossWrappedCord,
                    ShortSwordGripStyle.RibbedWood,
                    ShortSwordGripStyle.StuddedLeather,
                    ShortSwordGripStyle.SpiralLeather,
                    ShortSwordGripStyle.HalfWrappedWood,
                    ShortSwordGripStyle.WireBoundLeather),
                colors: Values(
                    ShortSwordGripColor.DarkBrown,
                    ShortSwordGripColor.OxBlood,
                    ShortSwordGripColor.Charcoal,
                    ShortSwordGripColor.WornTan,
                    ShortSwordGripColor.Navy,
                    ShortSwordGripColor.Ochre),
                metals: Values(
                    ShortSwordMetalFamily.Iron,
                    ShortSwordMetalFamily.Bronze,
                    ShortSwordMetalFamily.BlackenedSteel,
                    ShortSwordMetalFamily.AgedSteel,
                    ShortSwordMetalFamily.BlueSteel,
                    ShortSwordMetalFamily.CopperAlloy),
                facets: Values(
                    ShortSwordFacetTier.Coarse,
                    ShortSwordFacetTier.Standard,
                    ShortSwordFacetTier.Intricate));
        }

        private static void Furniture(
            Dictionary<ShortSwordGenerationDecision, int[]> rules,
            int[] bindings,
            int[] handleSections,
            int[] grips,
            int[] colors,
            int[] metals,
            int[] facets)
        {
            rules[ShortSwordGenerationDecision.GuardBindingStyle] = bindings;
            rules[ShortSwordGenerationDecision.HandleCrossSection] =
                handleSections;
            rules[ShortSwordGenerationDecision.GripStyle] = grips;
            rules[ShortSwordGenerationDecision.GripColor] = colors;
            rules[ShortSwordGenerationDecision.MetalFamily] = metals;
            rules[ShortSwordGenerationDecision.FacetTier] = facets;
            rules[ShortSwordGenerationDecision.GuardCrossSectionSides] =
                FacetCompatibleValues(
                    facets,
                    ShortSwordGenerationDecision.GuardCrossSectionSides);
            rules[ShortSwordGenerationDecision.GuardCurveSegments] =
                FacetCompatibleValues(
                    facets,
                    ShortSwordGenerationDecision.GuardCurveSegments);
        }

        private static int[] FacetCompatibleValues(
            IReadOnlyList<int> facets,
            ShortSwordGenerationDecision decision)
        {
            if (!TryGetGroup(
                    decision,
                    out ShortSwordGenerationBranchGroup group))
            {
                return Array.Empty<int>();
            }

            var compatible = new List<int>(group.Options.Count);
            for (int optionIndex = 0;
                 optionIndex < group.Options.Count;
                 optionIndex++)
            {
                int value = group.Options[optionIndex].Value;
                for (int facetIndex = 0;
                     facetIndex < facets.Count;
                     facetIndex++)
                {
                    if (!IsFacetTierCompatible(
                            (ShortSwordFacetTier)facets[facetIndex],
                            decision,
                            value))
                    {
                        continue;
                    }

                    compatible.Add(value);
                    break;
                }
            }
            return compatible.ToArray();
        }

        private static Dictionary<ShortSwordGenerationDecision, int[]>
            BuildAllCandidateValues()
        {
            var values = new Dictionary<ShortSwordGenerationDecision, int[]>();
            for (int groupIndex = 0;
                 groupIndex < OrderedGroups.Length;
                 groupIndex++)
            {
                ShortSwordGenerationBranchGroup group =
                    OrderedGroups[groupIndex];
                var options = new int[group.Options.Count];
                for (int optionIndex = 0;
                     optionIndex < group.Options.Count;
                     optionIndex++)
                {
                    options[optionIndex] = group.Options[optionIndex].Value;
                }
                values[group.Decision] = options;
            }
            return values;
        }

        private static Dictionary<ShortSwordGenerationDecision, int[]> Rules(
            bool conventional,
            int[] profiles,
            int[] backs,
            int[] bases,
            int[] sections,
            int[] guards,
            int[] handles,
            int[] hilts)
        {
            return new Dictionary<ShortSwordGenerationDecision, int[]>
            {
                [ShortSwordGenerationDecision.Directionality] = conventional
                    ? Values(ShortSwordDirectionality.Conventional)
                    : Values(ShortSwordDirectionality.Directional),
                [ShortSwordGenerationDecision.DirectionSide] = conventional
                    ? Array.Empty<int>()
                    : Values(
                        ShortSwordDirectionSide.Left,
                        ShortSwordDirectionSide.Right),
                [ShortSwordGenerationDecision.BladeProfile] = profiles,
                [ShortSwordGenerationDecision.BladeBackStyle] = backs,
                [ShortSwordGenerationDecision.BladeBaseStyle] = bases,
                [ShortSwordGenerationDecision.BladeSectionStyle] = sections,
                [ShortSwordGenerationDecision.GuardConstruction] = guards,
                [ShortSwordGenerationDecision.HandleProfile] = handles,
                [ShortSwordGenerationDecision.HiltProfile] = hilts
            };
        }

        private static ShortSwordGenerationBranchGroup Group(
            ShortSwordGenerationDecision decision,
            ShortSwordBranchUiCategory category,
            string heading,
            string tooltip,
            IReadOnlyList<ShortSwordGenerationBranchOption> options)
        {
            return new ShortSwordGenerationBranchGroup(
                decision,
                category,
                heading,
                tooltip,
                options);
        }

        private static ShortSwordGenerationBranchOption[] NumericOptions(
            params int[] values)
        {
            var options = new ShortSwordGenerationBranchOption[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                string label = values[index].ToString();
                options[index] = new ShortSwordGenerationBranchOption(
                    values[index],
                    label,
                    $"Guarantee {label} for this branch.");
            }
            return options;
        }

        private static ShortSwordGenerationBranchOption[] EnumOptions<T>(
            params T[] values)
            where T : Enum
        {
            var options = new ShortSwordGenerationBranchOption[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                string label = Nicify(values[index].ToString());
                options[index] = new ShortSwordGenerationBranchOption(
                    Convert.ToInt32(values[index]),
                    label,
                    $"Guarantee {label} for this branch.");
            }
            return options;
        }

        private static int[] Values<T>(params T[] values)
            where T : Enum
        {
            var result = new int[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = Convert.ToInt32(values[index]);
            }
            return result;
        }

        private static string Nicify(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return string.Empty;
            }

            var characters = new List<char>(identifier.Length + 6)
            {
                identifier[0]
            };
            for (int index = 1; index < identifier.Length; index++)
            {
                char current = identifier[index];
                char previous = identifier[index - 1];
                bool split = char.IsUpper(current) &&
                    (char.IsLower(previous) ||
                     (index + 1 < identifier.Length &&
                      char.IsLower(identifier[index + 1])));
                if (split)
                {
                    characters.Add(' ');
                }
                characters.Add(current);
            }
            return new string(characters.ToArray());
        }
    }
}
