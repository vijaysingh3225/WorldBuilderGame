using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Weapons
{
    public enum ShortSwordBladeProfile
    {
        StraightPoint = 0,
        LongTaper = 1,
        RoundedShoulder = 2,
        ForwardSwept = 3,
        ClipPoint = 4,
        LeafBlade = 5,
        Gladius = 6,
        PiercingDiamond = 7,
        Seax = 8,
        Falchion = 9,
        Kopis = 10,
        Hanger = 11
    }

    public enum ShortSwordBladeBackStyle
    {
        Clean = 0,
        Sawback = 1,
        SteppedSpine = 2,
        ReinforcedSpine = 3,
        ScallopedSpine = 4,
        BrokenBack = 5
    }

    public enum ShortSwordGuardProfile
    {
        Straight = 0,
        Downturned = 1,
        Upswept = 2,
        Bowed = 3,
        HookedQuillons = 4,
        Slanted = 5,
        OffsetQuillons = 6
    }

    public enum ShortSwordGuardConstruction
    {
        RazorBar = 0,
        BladeQuillons = 1,
        WingedW = 2,
        Crescent = 3,
        DirectionalSweep = 4,
        OffsetLeaf = 5,
        MinimalBolster = 6,
        DownturnedHooks = 7,
        GreekWings = 8,
        SQuillons = 9,
        LobedCross = 10
    }

    public enum ShortSwordHandleProfile
    {
        Straight = 0,
        Tapered = 1,
        Waisted = 2,
        PalmSwell = 3,
        FlaredEnds = 4
    }

    public enum ShortSwordHiltProfile
    {
        Disc = 0,
        Faceted = 1,
        ScentStopper = 2,
        Crowned = 3,
        Hooked = 4,
        Acorn = 5,
        BrazilNut = 6,
        Mushroom = 7,
        Fishtail = 8,
        Ring = 9,
        Beaked = 10
    }

    public enum ShortSwordMetalFamily
    {
        Iron = 0,
        Bronze = 1,
        Silver = 2,
        BlackenedSteel = 3,
        AgedSteel = 4,
        BlueSteel = 5,
        CopperAlloy = 6
    }

    public enum ShortSwordGripStyle
    {
        LeatherBands = 0,
        CrossWrappedCord = 1,
        RibbedWood = 2,
        StuddedLeather = 3,
        SpiralLeather = 4,
        HerringboneCord = 5,
        HalfWrappedWood = 6,
        FacetedLeather = 7,
        WireBoundLeather = 8
    }

    public enum ShortSwordGripColor
    {
        DarkBrown = 0,
        OxBlood = 1,
        Charcoal = 2,
        WornTan = 3,
        ForestGreen = 4,
        Navy = 5,
        Bone = 6,
        Ochre = 7
    }

    public enum ShortSwordOrnamentStyle
    {
        Plain = 0,
        GuardGem = 1,
        PommelGem = 2
    }

    public enum ShortSwordGemFamily
    {
        Ruby = 0,
        Emerald = 1,
        Sapphire = 2,
        Amber = 3
    }

    public enum ShortSwordGemCut
    {
        Round = 0,
        Oval = 1,
        PrincessSquare = 2,
        Emerald = 3,
        Pear = 4
    }

    public enum ShortSwordDirectionality
    {
        Conventional = 0,
        Directional = 1
    }

    public enum ShortSwordDirectionSide
    {
        Left = -1,
        Right = 1
    }

    public enum ShortSwordGenerationDecision
    {
        Directionality = 0,
        BladeProfile = 1,
        BladeBackStyle = 2,
        DirectionSide = 3,
        GuardConstruction = 4,
        HandleProfile = 5,
        HiltProfile = 6,
        MetalFamily = 7,
        GripStyle = 8,
        GripColor = 9,
        OrnamentStyle = 10,
        GemFamily = 11,
        GemCut = 12,
        GuardCrossSectionSides = 13,
        GuardCurveSegments = 14,
        Family = 15,
        BladeBaseStyle = 16,
        BladeSectionStyle = 17,
        GuardBindingStyle = 18,
        HandleCrossSection = 19,
        FacetTier = 20,
        HeroZone = 21
    }

    [Serializable]
    public struct ShortSwordGenerationLock
    {
        [SerializeField] private ShortSwordGenerationDecision decision;
        [SerializeField] private int value;

        public ShortSwordGenerationLock(
            ShortSwordGenerationDecision decision,
            int value)
        {
            this.decision = decision;
            this.value = value;
        }

        public ShortSwordGenerationDecision Decision => decision;
        public int Value => value;
    }

    [Serializable]
    public sealed class ProceduralShortSwordGenerationConstraints
    {
        [SerializeField] private List<ShortSwordGenerationLock> locks =
            new List<ShortSwordGenerationLock>();

        public int ActiveLockCount => locks?.Count ?? 0;
        public IReadOnlyList<ShortSwordGenerationLock> Locks
        {
            get
            {
                EnsureLocks();
                return locks;
            }
        }

        public bool IsLocked(
            ShortSwordGenerationDecision decision,
            int value)
        {
            return TryGetValue(decision, out int lockedValue) &&
                lockedValue == value;
        }

        public bool TryGetValue(
            ShortSwordGenerationDecision decision,
            out int value)
        {
            EnsureLocks();
            for (int index = 0; index < locks.Count; index++)
            {
                if (locks[index].Decision != decision)
                {
                    continue;
                }

                value = locks[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool Toggle(
            ShortSwordGenerationDecision decision,
            int value)
        {
            if (!IsCatalogValue(decision, value))
            {
                return false;
            }
            EnsureLocks();
            for (int index = 0; index < locks.Count; index++)
            {
                if (locks[index].Decision != decision)
                {
                    continue;
                }
                if (locks[index].Value == value)
                {
                    locks.RemoveAt(index);
                    return false;
                }

                locks[index] = new ShortSwordGenerationLock(
                    decision,
                    value);
                ResolveConflicts(decision, value);
                return true;
            }

            locks.Add(new ShortSwordGenerationLock(decision, value));
            ResolveConflicts(decision, value);
            return true;
        }

        public void Clear()
        {
            EnsureLocks();
            locks.Clear();
        }

        private void ResolveConflicts(
            ShortSwordGenerationDecision changedDecision,
            int changedValue)
        {
            switch (changedDecision)
            {
                case ShortSwordGenerationDecision.Directionality:
                    ResolveDirectionalityConflicts(
                        (ShortSwordDirectionality)changedValue);
                    break;
                case ShortSwordGenerationDecision.BladeProfile:
                    ResolveBladeProfileConflicts(
                        (ShortSwordBladeProfile)changedValue);
                    break;
                case ShortSwordGenerationDecision.BladeBackStyle:
                    if ((ShortSwordBladeBackStyle)changedValue ==
                        ShortSwordBladeBackStyle.Sawback)
                    {
                        RemoveConventionalBladeConstraints();
                    }
                    break;
                case ShortSwordGenerationDecision.DirectionSide:
                    RemoveConventionalBladeConstraints();
                    break;
                case ShortSwordGenerationDecision.GuardConstruction:
                    ResolveGuardConflicts(
                        (ShortSwordGuardConstruction)changedValue);
                    break;
                case ShortSwordGenerationDecision.GuardBindingStyle:
                    ResolveGuardBindingConflicts(
                        (ShortSwordGuardBindingStyle)changedValue);
                    break;
                case ShortSwordGenerationDecision.HiltProfile:
                    ResolveHiltConflicts(
                        (ShortSwordHiltProfile)changedValue);
                    break;
                case ShortSwordGenerationDecision.FacetTier:
                    ResolveFacetTierConflicts(
                        (ShortSwordFacetTier)changedValue);
                    break;
                case ShortSwordGenerationDecision.GuardCrossSectionSides:
                case ShortSwordGenerationDecision.GuardCurveSegments:
                case ShortSwordGenerationDecision.HandleCrossSection:
                    RemoveFacetTierIfIncompatible(
                        changedDecision,
                        changedValue);
                    break;
                case ShortSwordGenerationDecision.OrnamentStyle:
                    ResolveOrnamentConflicts(
                        (ShortSwordOrnamentStyle)changedValue);
                    break;
                case ShortSwordGenerationDecision.GemFamily:
                case ShortSwordGenerationDecision.GemCut:
                    RemoveIncompatibleGemHilt();
                    if (IsLocked(
                            ShortSwordGenerationDecision.OrnamentStyle,
                            (int)ShortSwordOrnamentStyle.Plain))
                    {
                        Remove(ShortSwordGenerationDecision.OrnamentStyle);
                    }
                    break;
            }

            ResolveFamilyGrammar(changedDecision, changedValue);
        }

        private void ResolveFamilyGrammar(
            ShortSwordGenerationDecision changedDecision,
            int changedValue)
        {
            if (changedDecision == ShortSwordGenerationDecision.Family)
            {
                var family = (ShortSwordFamily)changedValue;
                for (int index = locks.Count - 1; index >= 0; index--)
                {
                    ShortSwordGenerationLock generationLock = locks[index];
                    if (generationLock.Decision ==
                            ShortSwordGenerationDecision.Family)
                    {
                        continue;
                    }
                    if (!ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                            family,
                            generationLock.Decision,
                            generationLock.Value))
                    {
                        locks.RemoveAt(index);
                    }
                }
                if (!ShortSwordGenerationBranchCatalog.
                    IsFamilyCompatibleWithLocks(family, locks))
                {
                    Remove(ShortSwordGenerationDecision.FacetTier);
                    Remove(
                        ShortSwordGenerationDecision.
                            GuardCrossSectionSides);
                    Remove(
                        ShortSwordGenerationDecision.GuardCurveSegments);
                    Remove(
                        ShortSwordGenerationDecision.HandleCrossSection);
                }
                return;
            }

            if (TryGetValue(
                    ShortSwordGenerationDecision.Family,
                    out int familyValue) &&
                !ShortSwordGenerationBranchCatalog.
                    IsFamilyCompatibleWithLocks(
                        (ShortSwordFamily)familyValue,
                        locks))
            {
                Remove(ShortSwordGenerationDecision.Family);
            }

            IReadOnlyList<ShortSwordFamily> changedFamilies =
                ShortSwordGenerationBranchCatalog.GetCompatibleFamilies(
                    changedDecision,
                    changedValue);
            for (int index = locks.Count - 1; index >= 0; index--)
            {
                ShortSwordGenerationLock generationLock = locks[index];
                if (generationLock.Decision == changedDecision ||
                    generationLock.Decision ==
                        ShortSwordGenerationDecision.Family)
                {
                    continue;
                }
                IReadOnlyList<ShortSwordFamily> otherFamilies =
                    ShortSwordGenerationBranchCatalog.GetCompatibleFamilies(
                        generationLock.Decision,
                        generationLock.Value);
                if (!SharesFamily(changedFamilies, otherFamilies))
                {
                    locks.RemoveAt(index);
                }
            }

            while (!HasCompatibleFamilyForCurrentLocks())
            {
                int removableIndex = -1;
                for (int index = 0; index < locks.Count; index++)
                {
                    if (locks[index].Decision != changedDecision &&
                        locks[index].Decision !=
                            ShortSwordGenerationDecision.Family)
                    {
                        removableIndex = index;
                        break;
                    }
                }
                if (removableIndex < 0)
                {
                    break;
                }
                locks.RemoveAt(removableIndex);
            }
        }

        private bool HasCompatibleFamilyForCurrentLocks()
        {
            IReadOnlyList<ShortSwordFamily> families =
                ShortSwordGenerationBranchCatalog.Families;
            for (int index = 0; index < families.Count; index++)
            {
                if (ShortSwordGenerationBranchCatalog.
                    IsFamilyCompatibleWithLocks(
                        families[index],
                        locks))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SharesFamily(
            IReadOnlyList<ShortSwordFamily> left,
            IReadOnlyList<ShortSwordFamily> right)
        {
            for (int leftIndex = 0; leftIndex < left.Count; leftIndex++)
            {
                for (int rightIndex = 0;
                     rightIndex < right.Count;
                     rightIndex++)
                {
                    if (left[leftIndex] == right[rightIndex])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsCatalogValue(
            ShortSwordGenerationDecision decision,
            int value)
        {
            if (!ShortSwordGenerationBranchCatalog.TryGetGroup(
                    decision,
                    out ShortSwordGenerationBranchGroup group))
            {
                return false;
            }
            for (int index = 0; index < group.Options.Count; index++)
            {
                if (group.Options[index].Value == value)
                {
                    return true;
                }
            }
            return false;
        }

        private void ResolveDirectionalityConflicts(
            ShortSwordDirectionality directionality)
        {
            if (directionality == ShortSwordDirectionality.Directional)
            {
                RemoveBladeProfileIf(directional: false);
                RemoveGuardIf(
                    ShortSwordGuardConstruction.RazorBar);
                return;
            }

            RemoveBladeProfileIf(directional: true);
            Remove(ShortSwordGenerationDecision.DirectionSide);
            RemoveBladeBackIf(ShortSwordBladeBackStyle.Sawback);
            RemoveDirectionalGuard();
        }

        private void ResolveBladeProfileConflicts(
            ShortSwordBladeProfile profile)
        {
            bool directional = IsDirectionalBladeProfile(profile);
            if (TryGetValue(
                    ShortSwordGenerationDecision.Directionality,
                    out int directionality) &&
                ((ShortSwordDirectionality)directionality ==
                    ShortSwordDirectionality.Directional) != directional)
            {
                Remove(ShortSwordGenerationDecision.Directionality);
            }

            if (directional)
            {
                RemoveGuardIf(ShortSwordGuardConstruction.RazorBar);
                return;
            }

            Remove(ShortSwordGenerationDecision.DirectionSide);
            RemoveBladeBackIf(ShortSwordBladeBackStyle.Sawback);
            RemoveDirectionalGuard();
        }

        private void ResolveGuardConflicts(
            ShortSwordGuardConstruction construction)
        {
            if (IsDirectionalGuard(construction))
            {
                RemoveConventionalBladeConstraints();
                RemoveOrnamentIf(ShortSwordOrnamentStyle.GuardGem);
                return;
            }
            if (construction == ShortSwordGuardConstruction.MinimalBolster)
            {
                RemoveOrnamentIf(ShortSwordOrnamentStyle.GuardGem);
            }
            if (!SupportsGuardBindingConstruction(construction))
            {
                Remove(ShortSwordGenerationDecision.GuardBindingStyle);
            }
            if (construction != ShortSwordGuardConstruction.RazorBar)
            {
                return;
            }

            RemoveDirectionalBladeConstraints();
        }

        private void ResolveGuardBindingConflicts(
            ShortSwordGuardBindingStyle binding)
        {
            if (binding == ShortSwordGuardBindingStyle.None)
            {
                return;
            }
            if (TryGetValue(
                    ShortSwordGenerationDecision.GuardConstruction,
                    out int value) &&
                !SupportsGuardBindingConstruction(
                    (ShortSwordGuardConstruction)value))
            {
                Remove(ShortSwordGenerationDecision.GuardConstruction);
            }
        }

        private void ResolveOrnamentConflicts(
            ShortSwordOrnamentStyle ornament)
        {
            if (ornament == ShortSwordOrnamentStyle.Plain)
            {
                Remove(ShortSwordGenerationDecision.GemFamily);
                Remove(ShortSwordGenerationDecision.GemCut);
                return;
            }
            if (ornament == ShortSwordOrnamentStyle.GuardGem)
            {
                RemoveDirectionalGuard();
                RemoveGuardIf(ShortSwordGuardConstruction.MinimalBolster);
            }
            else if (ornament == ShortSwordOrnamentStyle.PommelGem)
            {
                RemoveIncompatibleGemHilt();
            }
        }

        private void ResolveHiltConflicts(ShortSwordHiltProfile profile)
        {
            if (SupportsPommelGemProfile(profile))
            {
                return;
            }
            RemoveOrnamentIf(ShortSwordOrnamentStyle.PommelGem);
            Remove(ShortSwordGenerationDecision.GemFamily);
            Remove(ShortSwordGenerationDecision.GemCut);
        }

        private void RemoveIncompatibleGemHilt()
        {
            if (TryGetValue(
                    ShortSwordGenerationDecision.HiltProfile,
                    out int value) &&
                !SupportsPommelGemProfile((ShortSwordHiltProfile)value))
            {
                Remove(ShortSwordGenerationDecision.HiltProfile);
            }
        }

        private void ResolveFacetTierConflicts(ShortSwordFacetTier tier)
        {
            RemoveIfFacetTierIncompatible(
                tier,
                ShortSwordGenerationDecision.GuardCrossSectionSides);
            RemoveIfFacetTierIncompatible(
                tier,
                ShortSwordGenerationDecision.GuardCurveSegments);
            RemoveIfFacetTierIncompatible(
                tier,
                ShortSwordGenerationDecision.HandleCrossSection);
        }

        private void RemoveIfFacetTierIncompatible(
            ShortSwordFacetTier tier,
            ShortSwordGenerationDecision decision)
        {
            if (TryGetValue(decision, out int value) &&
                !ShortSwordGenerationBranchCatalog.IsFacetTierCompatible(
                    tier,
                    decision,
                    value))
            {
                Remove(decision);
            }
        }

        private void RemoveFacetTierIfIncompatible(
            ShortSwordGenerationDecision decision,
            int value)
        {
            if (TryGetValue(
                    ShortSwordGenerationDecision.FacetTier,
                    out int tierValue) &&
                !ShortSwordGenerationBranchCatalog.IsFacetTierCompatible(
                    (ShortSwordFacetTier)tierValue,
                    decision,
                    value))
            {
                Remove(ShortSwordGenerationDecision.FacetTier);
            }
        }

        private void RemoveConventionalBladeConstraints()
        {
            RemoveDirectionalityIf(ShortSwordDirectionality.Conventional);
            RemoveBladeProfileIf(directional: false);
            RemoveGuardIf(ShortSwordGuardConstruction.RazorBar);
        }

        private void RemoveDirectionalBladeConstraints()
        {
            RemoveDirectionalityIf(ShortSwordDirectionality.Directional);
            RemoveBladeProfileIf(directional: true);
            Remove(ShortSwordGenerationDecision.DirectionSide);
            RemoveBladeBackIf(ShortSwordBladeBackStyle.Sawback);
        }

        private void RemoveDirectionalityIf(
            ShortSwordDirectionality directionality)
        {
            if (IsLocked(
                    ShortSwordGenerationDecision.Directionality,
                    (int)directionality))
            {
                Remove(ShortSwordGenerationDecision.Directionality);
            }
        }

        private void RemoveBladeProfileIf(bool directional)
        {
            if (TryGetValue(
                    ShortSwordGenerationDecision.BladeProfile,
                    out int value) &&
                IsDirectionalBladeProfile(
                    (ShortSwordBladeProfile)value) == directional)
            {
                Remove(ShortSwordGenerationDecision.BladeProfile);
            }
        }

        private void RemoveBladeBackIf(ShortSwordBladeBackStyle style)
        {
            if (IsLocked(
                    ShortSwordGenerationDecision.BladeBackStyle,
                    (int)style))
            {
                Remove(ShortSwordGenerationDecision.BladeBackStyle);
            }
        }

        private void RemoveDirectionalGuard()
        {
            if (TryGetValue(
                    ShortSwordGenerationDecision.GuardConstruction,
                    out int value) &&
                IsDirectionalGuard((ShortSwordGuardConstruction)value))
            {
                Remove(ShortSwordGenerationDecision.GuardConstruction);
            }
        }

        private void RemoveGuardIf(ShortSwordGuardConstruction construction)
        {
            if (IsLocked(
                    ShortSwordGenerationDecision.GuardConstruction,
                    (int)construction))
            {
                Remove(ShortSwordGenerationDecision.GuardConstruction);
            }
        }

        private void RemoveOrnamentIf(ShortSwordOrnamentStyle ornament)
        {
            if (IsLocked(
                    ShortSwordGenerationDecision.OrnamentStyle,
                    (int)ornament))
            {
                Remove(ShortSwordGenerationDecision.OrnamentStyle);
            }
        }

        private void Remove(ShortSwordGenerationDecision decision)
        {
            EnsureLocks();
            for (int index = locks.Count - 1; index >= 0; index--)
            {
                if (locks[index].Decision == decision)
                {
                    locks.RemoveAt(index);
                }
            }
        }

        private void EnsureLocks()
        {
            locks ??= new List<ShortSwordGenerationLock>();
        }

        private static bool IsDirectionalBladeProfile(
            ShortSwordBladeProfile profile)
        {
            return ShortSwordGenerationBranchCatalog.
                IsDirectionalBladeProfile(profile);
        }

        private static bool IsDirectionalGuard(
            ShortSwordGuardConstruction construction)
        {
            return construction ==
                    ShortSwordGuardConstruction.DirectionalSweep ||
                construction == ShortSwordGuardConstruction.OffsetLeaf;
        }

        private static bool SupportsPommelGemProfile(
            ShortSwordHiltProfile profile)
        {
            return profile is
                ShortSwordHiltProfile.Disc or
                ShortSwordHiltProfile.Faceted or
                ShortSwordHiltProfile.ScentStopper or
                ShortSwordHiltProfile.Crowned or
                ShortSwordHiltProfile.Acorn or
                ShortSwordHiltProfile.BrazilNut or
                ShortSwordHiltProfile.Mushroom;
        }

        private static bool SupportsGuardBindingConstruction(
            ShortSwordGuardConstruction construction)
        {
            return construction != ShortSwordGuardConstruction.RazorBar &&
                construction != ShortSwordGuardConstruction.MinimalBolster;
        }
    }

    [Serializable]
    public struct ShortSwordCombatProfile
    {
        public float CraftQuality;
        public float Heft;
        public float Handling;
        public float DamageMultiplier;
        public float AttackSpeedMultiplier;
        public float HitPauseDuration;
        public float StaggerDuration;
        public float ImpactShakeMultiplier;
        public float SwingPitchMultiplier;
        public float SwingVolumeMultiplier;
        public float TrailPersistenceMultiplier;
        public float TrailOpacityMultiplier;

        public bool IsValid =>
            DamageMultiplier > 0f &&
            AttackSpeedMultiplier > 0f;

        public static ShortSwordCombatProfile Default =>
            new ShortSwordCombatProfile
            {
                CraftQuality = 0.5f,
                Heft = 0.5f,
                Handling = 0.5f,
                DamageMultiplier = 1f,
                AttackSpeedMultiplier = 1f,
                HitPauseDuration = 0.04f,
                StaggerDuration = 0.25f,
                ImpactShakeMultiplier = 1f,
                SwingPitchMultiplier = 1f,
                SwingVolumeMultiplier = 1f,
                TrailPersistenceMultiplier = 1f,
                TrailOpacityMultiplier = 1f
            };
    }

    [Serializable]
    public struct ProceduralShortSwordDefinition
    {
        public int Seed;
        public ShortSwordFamily Family;
        public ShortSwordHeroZone HeroZone;
        public ShortSwordFacetTier FacetTier;
        public ShortSwordBladeProfile BladeProfile;
        public ShortSwordBladeBackStyle BladeBackStyle;
        public ShortSwordBladeBaseStyle BladeBaseStyle;
        public ShortSwordBladeSectionStyle BladeSectionStyle;
        public ShortSwordGuardProfile GuardProfile;
        public ShortSwordGuardConstruction GuardConstruction;
        public ShortSwordGuardBindingStyle GuardBindingStyle;
        public ShortSwordHandleProfile HandleProfile;
        public ShortSwordHandleCrossSection HandleCrossSection;
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
        public ShortSwordCombatProfile CombatProfile;

        public float TotalLength =>
            BladeLength + HandleLength + HiltLength;
    }

    [DisallowMultipleComponent]
    public sealed class ProceduralShortSwordGenerator : MonoBehaviour
    {
        public const string WorldShaderName =
            "Universal Render Pipeline/Lit";
        public const string WorldMaterialName =
            "Generated Sword Standard Lit";
        public const string BladePartName = "Blade";
        public const string GuardPartName = "Guard";
        public const string HandlePartName = "Handle";
        public const string HiltPartName = "Hilt / Pommel";
        public const string BladeFracturePrefix = "Blade Fracture";
        public const float TargetFacetLength = 0.052f;
        public const float WovenGripRadialOffset = 0.0035f;
        public const float WovenGripAirGap = 0.0008f;
        // The controlled sword stays on URP/Lit for diffuse light, probes, and
        // shadows. Metallic must remain zero when its view-dependent specular
        // paths are disabled; otherwise the metallic workflow removes diffuse
        // energy and recreates the dark-sword workaround this material replaces.
        public const float WorldSwordMetallic = 0f;
        public const float WorldSwordSmoothness = 0.08f;
        public const float WovenGripLowPolyAllowance = 0.0006f;

        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private int startingSeed = 1201;
        [SerializeField] private Material bladeMaterial;
        [SerializeField] private Material guardMaterial;
        [SerializeField] private Material handleMaterial;
        [SerializeField] private Material hiltMaterial;
        [SerializeField] private bool neutralizeBaseTextures;
        [SerializeField] private bool useColumnFurnitureStandard = true;
        [SerializeField] private ProceduralShortSwordGenerationConstraints
            generationConstraints =
                new ProceduralShortSwordGenerationConstraints();

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> ownedRuntimeMaterials =
            new List<Material>();
        private Material missingSourceMaterial;
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
        public ProceduralShortSwordGenerationConstraints GenerationConstraints =>
            generationConstraints ??=
                new ProceduralShortSwordGenerationConstraints();
        public int ActiveGenerationLockCount =>
            GenerationConstraints.ActiveLockCount;

        public void SetGenerateOnStart(bool value)
        {
            generateOnStart = value;
        }

        public void SetUseColumnFurnitureStandard(bool value)
        {
            useColumnFurnitureStandard = value;
        }

        public bool IsGenerationLocked(
            ShortSwordGenerationDecision decision,
            int value)
        {
            return GenerationConstraints.IsLocked(decision, value);
        }

        public bool ToggleGenerationLock(
            ShortSwordGenerationDecision decision,
            int value)
        {
            return GenerationConstraints.Toggle(decision, value);
        }

        public void ClearGenerationLocks()
        {
            GenerationConstraints.Clear();
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
            ReleaseRuntimeMaterials();
        }

        public void ConfigureMaterials(
            Material blade,
            Material guard,
            Material handle,
            Material hilt,
            bool useProceduralPalette = false)
        {
            neutralizeBaseTextures = useProceduralPalette;
            ReleaseRuntimeMaterials();
            if (!useProceduralPalette)
            {
                bladeMaterial = blade;
                guardMaterial = guard;
                handleMaterial = handle;
                hiltMaterial = hilt;
                return;
            }

            var sanitized = new Dictionary<Material, Material>();
            bladeMaterial = CreateSanitizedWorldMaterial(blade, sanitized);
            guardMaterial = CreateSanitizedWorldMaterial(guard, sanitized);
            handleMaterial = CreateSanitizedWorldMaterial(handle, sanitized);
            hiltMaterial = CreateSanitizedWorldMaterial(hilt, sanitized);
        }

        private Material CreateSanitizedWorldMaterial(
            Material source,
            Dictionary<Material, Material> sanitized)
        {
            if (source == null && missingSourceMaterial != null)
            {
                return missingSourceMaterial;
            }
            if (source != null &&
                sanitized.TryGetValue(source, out Material existing))
            {
                return existing;
            }

            // Use the project's ordinary URP lighting path. The previous
            // diffuse-only safety shader removed the flash, but it also made
            // swords flat and under-lit in the world and inventory previews.
            // Keep Lit diffuse illumination, probes, and real shadows while
            // compiling out only the hard-faced sword's unstable view-dependent
            // highlight and environment-reflection lobes.
            Shader worldShader =
                Shader.Find(WorldShaderName) ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(worldShader)
            {
                name = WorldMaterialName,
                globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry,
                enableInstancing = false
            };
            material.shaderKeywords = Array.Empty<string>();
            material.DisableKeyword("_EMISSION");
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_PARALLAXMAP");
            material.DisableKeyword("_DETAIL_MULX2");
            material.DisableKeyword("_DETAIL_SCALED");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_CLEARCOAT");
            material.DisableKeyword("_CLEARCOATMAP");
            material.DisableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            material.DisableKeyword("_SPECULAR_SETUP");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            SetMaterialColorIfPresent(material, "_BaseColor", Color.white);
            SetMaterialColorIfPresent(material, "_Color", Color.white);
            SetMaterialColorIfPresent(material, "_EmissionColor", Color.black);
            SetMaterialColorIfPresent(
                material,
                "_SpecColor",
                new Color(0.20f, 0.21f, 0.22f, 1f));
            SetMaterialFloatIfPresent(material, "_Surface", 0f);
            SetMaterialFloatIfPresent(material, "_Blend", 0f);
            SetMaterialFloatIfPresent(material, "_AlphaClip", 0f);
            SetMaterialFloatIfPresent(material, "_ZWrite", 1f);
            SetMaterialFloatIfPresent(material, "_Cull", 2f);
            SetMaterialFloatIfPresent(material, "_WorkflowMode", 1f);
            SetMaterialFloatIfPresent(
                material,
                "_SmoothnessTextureChannel",
                0f);
            SetMaterialFloatIfPresent(material, "_Metallic", WorldSwordMetallic);
            SetMaterialFloatIfPresent(
                material,
                "_Smoothness",
                WorldSwordSmoothness);
            SetMaterialFloatIfPresent(material, "_ClearCoatMask", 0f);
            SetMaterialFloatIfPresent(material, "_ClearCoatSmoothness", 0f);
            SetMaterialFloatIfPresent(material, "_SpecularHighlights", 0f);
            SetMaterialFloatIfPresent(material, "_EnvironmentReflections", 0f);
            SetMaterialTextureIfPresent(
                material,
                "_EmissionMap",
                Texture2D.blackTexture);
            SetMaterialTextureIfPresent(
                material,
                "_BaseMap",
                Texture2D.whiteTexture);
            SetMaterialTextureIfPresent(
                material,
                "_MainTex",
                Texture2D.whiteTexture);
            SetMaterialTextureIfPresent(
                material,
                "_SpecGlossMap",
                null);
            SetMaterialTextureIfPresent(
                material,
                "_MetallicGlossMap",
                null);
            SetMaterialTextureIfPresent(material, "_BumpMap", null);
            SetMaterialTextureIfPresent(material, "_DetailNormalMap", null);
            SetMaterialTextureIfPresent(material, "_DetailAlbedoMap", null);
            SetMaterialTextureIfPresent(material, "_ParallaxMap", null);
            SetMaterialTextureIfPresent(material, "_OcclusionMap", null);
            SetMaterialTextureIfPresent(
                material,
                "_ClearCoatMap",
                Texture2D.blackTexture);
            if (source != null)
            {
                sanitized[source] = material;
            }
            else
            {
                // A legacy socket can lack a guard/grip material entirely.
                // Do not fall back to Unity's uncontrolled default material:
                // reuse this controlled Lit fallback for every missing slot.
                missingSourceMaterial = material;
            }
            ownedRuntimeMaterials.Add(material);
            return material;
        }

        private static void SetMaterialColorIfPresent(
            Material material,
            string property,
            Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetMaterialFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetMaterialTextureIfPresent(
            Material material,
            string property,
            Texture value)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }

        private void ReleaseRuntimeMaterials()
        {
            for (int index = 0; index < ownedRuntimeMaterials.Count; index++)
            {
                Material material = ownedRuntimeMaterials[index];
                if (material == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
            ownedRuntimeMaterials.Clear();
            missingSourceMaterial = null;
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
            return GenerateDefinition(
                CreateDefinition(
                    seed,
                    GenerationConstraints,
                    useColumnFurnitureStandard));
        }

        public ProceduralShortSwordDefinition GenerateForFamily(
            int seed,
            ShortSwordFamily family)
        {
            if (!ShortSwordGenerationBranchCatalog.IsActiveFamily(family))
            {
                return Generate(seed);
            }

            var constraints = new ProceduralShortSwordGenerationConstraints();
            IReadOnlyList<ShortSwordGenerationLock> sourceLocks =
                GenerationConstraints.Locks;
            for (int index = 0; index < sourceLocks.Count; index++)
            {
                ShortSwordGenerationLock generationLock = sourceLocks[index];
                constraints.Toggle(
                    generationLock.Decision,
                    generationLock.Value);
            }
            constraints.Toggle(
                ShortSwordGenerationDecision.Family,
                (int)family);
            return GenerateDefinition(CreateDefinition(
                seed,
                constraints,
                useColumnFurnitureStandard));
        }

        /// <summary>
        /// Generates from the complete authored short-sword pool, ignoring any
        /// lab locks stored on this component. Runtime actors, camp props, and
        /// loot presentations use this path so no spawn context narrows the
        /// available families or child branches.
        /// </summary>
        public ProceduralShortSwordDefinition GenerateUnrestricted(int seed)
        {
            return GenerateDefinition(CreateDefinition(
                seed,
                null,
                useColumnFurnitureStandard));
        }

        private ProceduralShortSwordDefinition GenerateDefinition(
            ProceduralShortSwordDefinition definition)
        {
            ClearGeneratedSword();
            currentDefinition = definition;

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
            return CreateDefinition(seed, null, true);
        }

        public static ProceduralShortSwordDefinition CreateDefinition(
            int seed,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            return CreateDefinition(seed, constraints, true);
        }

        public static ProceduralShortSwordDefinition CreateDefinition(
            int seed,
            ProceduralShortSwordGenerationConstraints constraints,
            bool useColumnFurnitureStandard)
        {
            ShortSwordFamily family = SelectFamily(seed, constraints);
            ShortSwordHeroZone heroZone =
                (ShortSwordHeroZone)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.HeroZone,
                    constraints);
            ShortSwordFacetTier facetTier =
                SelectFacetTier(seed, family, constraints);

            var proportionRandom = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.Family,
                17);
            ResolveFamilyBladeLengthRange(
                family,
                out float minimumBladeLength,
                out float maximumBladeLength);
            ResolveFamilyBladeWidthRange(
                family,
                out float minimumBladeWidth,
                out float maximumBladeWidth);
            float bladeLength = Lerp(
                proportionRandom,
                minimumBladeLength,
                maximumBladeLength);
            float normalizedLength = Mathf.InverseLerp(
                0.94f,
                1.08f,
                bladeLength);
            float bladeWidth = Lerp(
                proportionRandom,
                minimumBladeWidth,
                maximumBladeWidth);
            float handleLength = Lerp(
                proportionRandom,
                0.205f + normalizedLength * 0.008f,
                0.250f + normalizedLength * 0.008f);
            ShortSwordBladeProfile bladeProfile =
                (ShortSwordBladeProfile)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.BladeProfile,
                    constraints);
            ShortSwordBladeBackStyle bladeBackStyle =
                (ShortSwordBladeBackStyle)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.BladeBackStyle,
                    constraints);
            ShortSwordBladeBaseStyle bladeBaseStyle =
                (ShortSwordBladeBaseStyle)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.BladeBaseStyle,
                    constraints);
            ShortSwordBladeSectionStyle bladeSectionStyle =
                (ShortSwordBladeSectionStyle)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.BladeSectionStyle,
                    constraints);

            var directionRandom = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.DirectionSide);
            int directionSign = directionRandom.Next(0, 2) == 0 ? -1 : 1;
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.DirectionSide,
                    out int lockedDirectionSign))
            {
                directionSign = lockedDirectionSign < 0 ? -1 : 1;
            }

            ApplyHeroZoneToBlade(
                seed,
                family,
                heroZone,
                constraints,
                ref bladeBackStyle,
                ref bladeBaseStyle,
                ref bladeSectionStyle);
            float normalizedBladeWidth = Mathf.InverseLerp(
                0.074f,
                0.112f,
                bladeWidth);
            ShortSwordGuardConstruction guardConstruction =
                (ShortSwordGuardConstruction)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.GuardConstruction,
                    constraints,
                    salt: Mathf.RoundToInt(normalizedBladeWidth * 100f));
            bool forcesGuardBinding = TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.GuardBindingStyle,
                    out int lockedGuardBinding) &&
                (ShortSwordGuardBindingStyle)lockedGuardBinding !=
                    ShortSwordGuardBindingStyle.None;
            if (forcesGuardBinding &&
                !SupportsGuardBinding(guardConstruction))
            {
                guardConstruction = SelectGuardSupportingBinding(
                    seed,
                    family,
                    guardConstruction);
            }
            bool forcesGuardGem = IsLocked(
                constraints,
                ShortSwordGenerationDecision.OrnamentStyle,
                (int)ShortSwordOrnamentStyle.GuardGem);
            if ((forcesGuardBinding &&
                 !SupportsGuardBinding(guardConstruction)) ||
                (forcesGuardGem &&
                 !SupportsGuardGem(guardConstruction)))
            {
                guardConstruction = SelectGuardSupportingRequirements(
                    seed,
                    family,
                    guardConstruction,
                    forcesGuardBinding,
                    forcesGuardGem);
            }
            ShortSwordGuardProfile guardProfile =
                ResolveGuardProfile(guardConstruction);
            ShortSwordHandleProfile handleProfile =
                (ShortSwordHandleProfile)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.HandleProfile,
                    constraints);
            ShortSwordHandleCrossSection handleCrossSection =
                SelectHandleCrossSection(
                    seed,
                    family,
                    facetTier,
                    constraints);
            ShortSwordHiltProfile hiltProfile =
                (ShortSwordHiltProfile)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.HiltProfile,
                    constraints);
            ShortSwordMetalFamily metalFamily =
                (ShortSwordMetalFamily)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.MetalFamily,
                    constraints);
            ShortSwordGripStyle gripStyle = SelectGripStyle(
                seed,
                family,
                heroZone,
                constraints);
            ShortSwordGripColor gripColor =
                (ShortSwordGripColor)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.GripColor,
                    constraints);
            ShortSwordGuardBindingStyle guardBindingStyle =
                SelectGuardBindingStyle(
                    seed,
                    family,
                    heroZone,
                    guardConstruction,
                    constraints);
            ShortSwordOrnamentStyle ornamentStyle = SelectOrnamentStyle(
                CreateGenerationRandom(
                    seed,
                    ShortSwordGenerationDecision.OrnamentStyle),
                heroZone);
            ornamentStyle = (ShortSwordOrnamentStyle)LockedOrRolled(
                constraints,
                ShortSwordGenerationDecision.OrnamentStyle,
                (int)ornamentStyle);
            if (!TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.OrnamentStyle,
                    out _) &&
                (HasLock(
                     constraints,
                     ShortSwordGenerationDecision.GemFamily) ||
                 HasLock(
                     constraints,
                     ShortSwordGenerationDecision.GemCut)))
            {
                ornamentStyle = ShortSwordOrnamentStyle.PommelGem;
            }
            bool requiresPommelGem = ornamentStyle ==
                    ShortSwordOrnamentStyle.PommelGem &&
                (IsLocked(
                     constraints,
                     ShortSwordGenerationDecision.OrnamentStyle,
                     (int)ShortSwordOrnamentStyle.PommelGem) ||
                 HasLock(
                     constraints,
                     ShortSwordGenerationDecision.GemFamily) ||
                 HasLock(
                     constraints,
                     ShortSwordGenerationDecision.GemCut));
            if (ornamentStyle == ShortSwordOrnamentStyle.PommelGem &&
                !SupportsPommelGem(hiltProfile))
            {
                if (requiresPommelGem)
                {
                    hiltProfile = SelectHiltSupportingGem(
                        seed,
                        family,
                        hiltProfile);
                }
                else
                {
                    ornamentStyle = ShortSwordOrnamentStyle.Plain;
                }
            }
            ShortSwordGemFamily gemFamily =
                (ShortSwordGemFamily)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.GemFamily,
                    constraints);
            ShortSwordGemCut gemCut =
                (ShortSwordGemCut)SelectFamilyBranch(
                    seed,
                    family,
                    ShortSwordGenerationDecision.GemCut,
                    constraints);
            float handleRadius = Lerp(proportionRandom, 0.027f, 0.032f);
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
                Lerp(proportionRandom, 0.255f, 0.292f) +
                    normalizedBladeWidth * 0.070f,
                guardConnectionSize * 3.4f);
            float guardSectionBias = Lerp(proportionRandom, -1f, 1f);
            int guardCrossSectionSides = SelectGuardCrossSectionSides(
                seed,
                facetTier,
                constraints);
            int guardCurveSegments = SelectGuardCurveSegments(
                seed,
                facetTier,
                constraints);
            float guardCrossSectionRotation = Lerp(
                proportionRandom,
                0f,
                Mathf.PI / guardCrossSectionSides);
            float guardHorizontalFactor = ResolveCrossSectionHorizontalFactor(
                guardCrossSectionSides,
                guardCrossSectionRotation);
            float guardHeight = Mathf.Clamp(
                ResolveGuardBaseHeight(
                    proportionRandom,
                    guardConstruction,
                    normalizedBladeWidth) *
                    Mathf.Lerp(0.76f, 1.58f, (guardSectionBias + 1f) * 0.5f),
                0.014f,
                0.055f);
            float guardDepth = Mathf.Max(
                (Lerp(proportionRandom, 0.050f, 0.069f) +
                    normalizedBladeWidth * 0.008f) *
                    Mathf.Lerp(1.20f, 0.86f, (guardSectionBias + 1f) * 0.5f),
                (handleTopRadius + 0.002f) * 2f / guardHorizontalFactor);
            if (ornamentStyle == ShortSwordOrnamentStyle.GuardGem)
            {
                if (forcesGuardGem)
                {
                    guardHeight = Mathf.Max(guardHeight, 0.028f);
                    guardSpan = Mathf.Max(guardSpan, 0.300f);
                }
                bool hasJewelSocket = guardHeight >= 0.028f &&
                    guardSpan >= 0.300f &&
                    SupportsGuardGem(guardConstruction);
                if (hasJewelSocket)
                {
                }
                else
                {
                    ornamentStyle = ShortSwordOrnamentStyle.Plain;
                }
            }

            float bladeThickness = Lerp(
                proportionRandom,
                0.026f,
                0.034f);
            float tipLength = Lerp(
                proportionRandom,
                0.18f,
                0.285f);
            float hiltLength = Lerp(
                proportionRandom,
                0.066f,
                0.096f);
            float hiltRadius = Lerp(
                proportionRandom,
                handleBottomRadius + 0.010f,
                handleBottomRadius + 0.021f);
            if (useColumnFurnitureStandard)
            {
                float furnitureScale =
                    ProceduralColumnBladeGenerator
                        .ResolveFurnitureRadialScale(seed);
                handleRadius *= furnitureScale;
                hiltRadius *= furnitureScale;
            }
            var definition = new ProceduralShortSwordDefinition
            {
                Seed = seed,
                Family = family,
                HeroZone = heroZone,
                FacetTier = facetTier,
                BladeProfile = bladeProfile,
                BladeBackStyle = bladeBackStyle,
                BladeBaseStyle = bladeBaseStyle,
                BladeSectionStyle = bladeSectionStyle,
                GuardProfile = guardProfile,
                GuardConstruction = guardConstruction,
                GuardBindingStyle = guardBindingStyle,
                HandleProfile = handleProfile,
                HandleCrossSection = handleCrossSection,
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
                BladeThickness = bladeThickness,
                TipLength = tipLength,
                GuardSpan = guardSpan,
                GuardHeight = guardHeight,
                GuardDepth = guardDepth,
                GuardCurveSegments = guardCurveSegments,
                GuardCrossSectionSides = guardCrossSectionSides,
                GuardCrossSectionRotation = guardCrossSectionRotation,
                HandleLength = handleLength,
                HandleRadius = handleRadius,
                HiltLength = hiltLength,
                HiltRadius = hiltRadius
            };
            definition.CombatProfile = CalculateCombatProfile(definition);
            return definition;
        }

        public static ShortSwordCombatProfile CalculateCombatProfile(
            ProceduralShortSwordDefinition definition)
        {
            float width = Mathf.InverseLerp(
                0.074f,
                0.112f,
                definition.BladeWidth);
            float thickness = Mathf.InverseLerp(
                0.026f,
                0.034f,
                definition.BladeThickness);
            float length = Mathf.InverseLerp(
                0.94f,
                1.08f,
                definition.BladeLength);
            float furniture = Mathf.Clamp01(
                Mathf.InverseLerp(
                    0.255f,
                    0.362f,
                    definition.GuardSpan) * 0.45f +
                Mathf.InverseLerp(
                    0.037f,
                    0.053f,
                    definition.HiltRadius) * 0.55f);
            float familyHeft = definition.Family switch
            {
                ShortSwordFamily.Falchion => 0.10f,
                ShortSwordFamily.Kopis => 0.12f,
                ShortSwordFamily.Hanger => 0.07f,
                ShortSwordFamily.Legionary => 0.04f,
                ShortSwordFamily.Piercer => -0.10f,
                ShortSwordFamily.Seax => -0.04f,
                _ => 0f
            };
            float heft = Mathf.Clamp01(
                width * 0.34f +
                thickness * 0.31f +
                length * 0.20f +
                furniture * 0.15f +
                familyHeft);

            float edgeBias = Mathf.Clamp01(
                (1f - thickness) * 0.46f +
                (1f - width) * 0.24f +
                ResolveSectionEdgeBias(
                    definition.BladeSectionStyle) * 0.30f);
            float counterBalance = Mathf.Clamp01(
                Mathf.InverseLerp(
                    0.037f,
                    0.053f,
                    definition.HiltRadius) * 0.55f +
                Mathf.InverseLerp(
                    0.205f,
                    0.258f,
                    definition.HandleLength) * 0.25f +
                (1f - length) * 0.20f);

            var qualityRandom = new System.Random(unchecked(
                definition.Seed * 1103515245 + 0x5A17C9D));
            float quality = (float)qualityRandom.NextDouble();
            quality += definition.OrnamentStyle switch
            {
                ShortSwordOrnamentStyle.GuardGem => 0.14f,
                ShortSwordOrnamentStyle.PommelGem => 0.14f,
                _ => 0f
            };
            quality += definition.FacetTier ==
                    ShortSwordFacetTier.Intricate
                ? 0.045f
                : 0f;
            quality += definition.GripStyle is
                    ShortSwordGripStyle.WireBoundLeather or
                    ShortSwordGripStyle.HerringboneCord or
                    ShortSwordGripStyle.SpiralLeather
                ? 0.025f
                : 0f;
            quality += definition.MetalFamily is
                    ShortSwordMetalFamily.Silver or
                    ShortSwordMetalFamily.BlueSteel or
                    ShortSwordMetalFamily.BlackenedSteel
                ? 0.025f
                : 0f;
            quality = Mathf.Clamp01(quality);
            float exceptionalQuality = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.62f, 1f, quality));

            float handling = Mathf.Clamp01(
                0.84f - heft * 0.76f +
                counterBalance * 0.23f +
                edgeBias * 0.12f +
                exceptionalQuality * 0.16f);
            float physicalAttackSpeed = Mathf.Lerp(
                0.68f,
                1.44f,
                handling);
            float attackSpeed = physicalAttackSpeed +
                exceptionalQuality *
                (0.035f +
                 Mathf.Max(0f, 1f - physicalAttackSpeed) * 0.30f);
            attackSpeed = Mathf.Clamp(attackSpeed, 0.68f, 1.52f);

            float damageMultiplier = Mathf.Clamp(
                0.66f + heft * 0.68f + edgeBias * 0.08f +
                exceptionalQuality * 0.17f,
                0.68f,
                1.55f);

            return new ShortSwordCombatProfile
            {
                CraftQuality = quality,
                Heft = heft,
                Handling = handling,
                DamageMultiplier = damageMultiplier,
                AttackSpeedMultiplier = attackSpeed,
                HitPauseDuration = Mathf.Lerp(
                    0.006f,
                    0.140f,
                    Mathf.Pow(heft, 1.15f)) +
                    exceptionalQuality * 0.012f,
                StaggerDuration = Mathf.Lerp(
                    0.07f,
                    0.58f,
                    Mathf.Pow(heft, 1.10f)) +
                    exceptionalQuality * 0.04f,
                ImpactShakeMultiplier = Mathf.Lerp(
                    0.32f,
                    2.15f,
                    Mathf.Pow(heft, 1.05f)) +
                    exceptionalQuality * 0.15f,
                SwingPitchMultiplier = Mathf.Clamp(
                    1.25f - heft * 0.49f +
                    exceptionalQuality * 0.025f,
                    0.76f,
                    1.28f),
                SwingVolumeMultiplier = Mathf.Clamp(
                    Mathf.Lerp(0.78f, 1.35f, heft) +
                    exceptionalQuality * 0.08f,
                    0.78f,
                    1.43f),
                TrailPersistenceMultiplier = Mathf.Clamp(
                    Mathf.Lerp(1.75f, 0.70f, heft) *
                    Mathf.Lerp(0.94f, 1.08f, edgeBias),
                    0.65f,
                    1.85f),
                TrailOpacityMultiplier = Mathf.Clamp(
                    Mathf.Lerp(1.40f, 0.82f, heft) *
                    Mathf.Lerp(0.95f, 1.05f, edgeBias),
                    0.75f,
                    1.48f)
            };
        }

        private static float ResolveSectionEdgeBias(
            ShortSwordBladeSectionStyle section)
        {
            return section switch
            {
                ShortSwordBladeSectionStyle.FlatBevel => 1f,
                ShortSwordBladeSectionStyle.ShallowFuller => 0.82f,
                ShortSwordBladeSectionStyle.DiamondRidge => 0.70f,
                ShortSwordBladeSectionStyle.HexagonalRidge => 0.55f,
                ShortSwordBladeSectionStyle.BroadMidrib => 0.35f,
                _ => 0.60f
            };
        }

        private static ShortSwordFamily SelectFamily(
            int seed,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.Family,
                    out int lockedFamily) &&
                Enum.IsDefined(typeof(ShortSwordFamily), lockedFamily) &&
                ShortSwordGenerationBranchCatalog.IsActiveFamily(
                    (ShortSwordFamily)lockedFamily))
            {
                return (ShortSwordFamily)lockedFamily;
            }

            var candidates = new List<ShortSwordFamily>();
            IReadOnlyList<ShortSwordFamily> values =
                ShortSwordGenerationBranchCatalog.Families;
            for (int index = 0; index < values.Count; index++)
            {
                ShortSwordFamily family = values[index];
                if (ShortSwordGenerationBranchCatalog.IsFamilyCompatibleWithLocks(
                        family,
                        constraints?.Locks))
                {
                    candidates.Add(family);
                }
            }
            if (candidates.Count == 0)
            {
                for (int index = 0; index < values.Count; index++)
                {
                    candidates.Add(values[index]);
                }
            }

            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.Family);
            return candidates[random.Next(0, candidates.Count)];
        }

        private static int SelectFamilyBranch(
            int seed,
            ShortSwordFamily family,
            ShortSwordGenerationDecision decision,
            ProceduralShortSwordGenerationConstraints constraints,
            int salt = 0)
        {
            if (TryGetLockedValue(constraints, decision, out int lockedValue) &&
                ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                    family,
                    decision,
                    lockedValue))
            {
                return lockedValue;
            }

            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    decision);
            if (candidates.Count == 0 &&
                ShortSwordGenerationBranchCatalog.TryGetGroup(
                    decision,
                    out ShortSwordGenerationBranchGroup group) &&
                group.Options.Count > 0)
            {
                var fallback = CreateGenerationRandom(seed, decision, salt);
                return group.Options[fallback.Next(0, group.Options.Count)].Value;
            }
            if (candidates.Count == 0)
            {
                return 0;
            }

            var random = CreateGenerationRandom(seed, decision, salt);
            if (decision == ShortSwordGenerationDecision.BladeProfile &&
                random.NextDouble() < 0.72)
            {
                int signature = (int)ResolveSignatureBladeProfile(family);
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (candidates[index] == signature)
                    {
                        return signature;
                    }
                }
            }
            if (decision == ShortSwordGenerationDecision.GuardConstruction &&
                random.NextDouble() < 0.55)
            {
                int signature = (int)ResolveSignatureGuardConstruction(family);
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (candidates[index] == signature)
                    {
                        return signature;
                    }
                }
            }
            return candidates[random.Next(0, candidates.Count)];
        }

        private static ShortSwordBladeProfile ResolveSignatureBladeProfile(
            ShortSwordFamily family)
        {
            return family switch
            {
                ShortSwordFamily.Leafblade => ShortSwordBladeProfile.LeafBlade,
                ShortSwordFamily.Legionary => ShortSwordBladeProfile.Gladius,
                ShortSwordFamily.Piercer =>
                    ShortSwordBladeProfile.PiercingDiamond,
                ShortSwordFamily.Seax => ShortSwordBladeProfile.Seax,
                ShortSwordFamily.Falchion => ShortSwordBladeProfile.Falchion,
                ShortSwordFamily.Kopis => ShortSwordBladeProfile.Kopis,
                ShortSwordFamily.Hanger => ShortSwordBladeProfile.Hanger,
                _ => ShortSwordBladeProfile.StraightPoint
            };
        }

        private static ShortSwordGuardConstruction
            ResolveSignatureGuardConstruction(ShortSwordFamily family)
        {
            return family switch
            {
                ShortSwordFamily.Leafblade =>
                    ShortSwordGuardConstruction.GreekWings,
                ShortSwordFamily.Legionary =>
                    ShortSwordGuardConstruction.MinimalBolster,
                ShortSwordFamily.Piercer =>
                    ShortSwordGuardConstruction.RazorBar,
                ShortSwordFamily.Seax =>
                    ShortSwordGuardConstruction.DownturnedHooks,
                ShortSwordFamily.Falchion =>
                    ShortSwordGuardConstruction.DirectionalSweep,
                ShortSwordFamily.Kopis =>
                    ShortSwordGuardConstruction.Crescent,
                ShortSwordFamily.Hanger =>
                    ShortSwordGuardConstruction.SQuillons,
                _ => ShortSwordGuardConstruction.BladeQuillons
            };
        }

        private static ShortSwordFacetTier SelectFacetTier(
            int seed,
            ShortSwordFamily family,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.FacetTier,
                    out int lockedTier))
            {
                return (ShortSwordFacetTier)lockedTier;
            }
            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.FacetTier);
            var compatible = new List<ShortSwordFacetTier>();
            for (int index = 0; index < candidates.Count; index++)
            {
                var tier = (ShortSwordFacetTier)candidates[index];
                if (IsFacetTierCompatibleWithLocks(tier, constraints))
                {
                    compatible.Add(tier);
                }
            }
            if (compatible.Count == 0)
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    compatible.Add((ShortSwordFacetTier)candidates[index]);
                }
            }
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.FacetTier);
            return compatible[random.Next(0, compatible.Count)];
        }

        private static bool IsFacetTierCompatibleWithLocks(
            ShortSwordFacetTier tier,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (constraints == null)
            {
                return true;
            }
            IReadOnlyList<ShortSwordGenerationLock> locks = constraints.Locks;
            for (int index = 0; index < locks.Count; index++)
            {
                if (!ShortSwordGenerationBranchCatalog.IsFacetTierCompatible(
                        tier,
                        locks[index].Decision,
                        locks[index].Value))
                {
                    return false;
                }
            }
            return true;
        }

        private static ShortSwordHandleCrossSection SelectHandleCrossSection(
            int seed,
            ShortSwordFamily family,
            ShortSwordFacetTier facetTier,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.HandleCrossSection,
                    out int lockedSection))
            {
                return (ShortSwordHandleCrossSection)lockedSection;
            }
            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.HandleCrossSection);
            var compatible = new List<ShortSwordHandleCrossSection>();
            for (int index = 0; index < candidates.Count; index++)
            {
                if (ShortSwordGenerationBranchCatalog.IsFacetTierCompatible(
                        facetTier,
                        ShortSwordGenerationDecision.HandleCrossSection,
                        candidates[index]))
                {
                    compatible.Add(
                        (ShortSwordHandleCrossSection)candidates[index]);
                }
            }
            if (compatible.Count == 0)
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    compatible.Add(
                        (ShortSwordHandleCrossSection)candidates[index]);
                }
            }
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.HandleCrossSection);
            return compatible[random.Next(0, compatible.Count)];
        }

        private static System.Random CreateGenerationRandom(
            int seed,
            ShortSwordGenerationDecision decision,
            int salt = 0)
        {
            int mixed = unchecked(
                seed * 486187739 +
                ((int)decision + 1) * 16777619 +
                (salt + 1) * 374761393);
            mixed ^= mixed >> 13;
            mixed = unchecked(mixed * 1274126177);
            mixed ^= mixed >> 16;
            return new System.Random(mixed);
        }

        private static void ResolveFamilyBladeLengthRange(
            ShortSwordFamily family,
            out float minimum,
            out float maximum)
        {
            switch (family)
            {
                case ShortSwordFamily.Legionary:
                    minimum = 0.94f;
                    maximum = 1.01f;
                    break;
                case ShortSwordFamily.Piercer:
                    minimum = 1.00f;
                    maximum = 1.08f;
                    break;
                case ShortSwordFamily.Kopis:
                case ShortSwordFamily.Falchion:
                    minimum = 0.96f;
                    maximum = 1.04f;
                    break;
                default:
                    minimum = 0.94f;
                    maximum = 1.08f;
                    break;
            }
        }

        private static void ResolveFamilyBladeWidthRange(
            ShortSwordFamily family,
            out float minimum,
            out float maximum)
        {
            switch (family)
            {
                case ShortSwordFamily.Piercer:
                    minimum = 0.074f;
                    maximum = 0.088f;
                    break;
                case ShortSwordFamily.Leafblade:
                case ShortSwordFamily.Legionary:
                    minimum = 0.090f;
                    maximum = 0.112f;
                    break;
                case ShortSwordFamily.Falchion:
                case ShortSwordFamily.Kopis:
                    minimum = 0.094f;
                    maximum = 0.112f;
                    break;
                default:
                    minimum = 0.080f;
                    maximum = 0.108f;
                    break;
            }
        }

        private static void ApplyHeroZoneToBlade(
            int seed,
            ShortSwordFamily family,
            ShortSwordHeroZone heroZone,
            ProceduralShortSwordGenerationConstraints constraints,
            ref ShortSwordBladeBackStyle backStyle,
            ref ShortSwordBladeBaseStyle baseStyle,
            ref ShortSwordBladeSectionStyle sectionStyle)
        {
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.HeroZone,
                31);
            if (heroZone == ShortSwordHeroZone.Blade)
            {
                return;
            }
            if (!HasLock(
                    constraints,
                    ShortSwordGenerationDecision.BladeBackStyle) &&
                random.NextDouble() < 0.68)
            {
                backStyle = ShortSwordBladeBackStyle.Clean;
            }
            if (heroZone == ShortSwordHeroZone.Grip &&
                !HasLock(
                    constraints,
                    ShortSwordGenerationDecision.BladeBaseStyle) &&
                ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                    family,
                    ShortSwordGenerationDecision.BladeBaseStyle,
                    (int)ShortSwordBladeBaseStyle.Plain) &&
                random.NextDouble() < 0.55)
            {
                baseStyle = ShortSwordBladeBaseStyle.Plain;
            }
            if (heroZone == ShortSwordHeroZone.Guard &&
                !HasLock(
                    constraints,
                    ShortSwordGenerationDecision.BladeSectionStyle) &&
                ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                    family,
                    ShortSwordGenerationDecision.BladeSectionStyle,
                    (int)ShortSwordBladeSectionStyle.FlatBevel) &&
                random.NextDouble() < 0.52)
            {
                sectionStyle = ShortSwordBladeSectionStyle.FlatBevel;
            }
        }

        private static ShortSwordGripStyle SelectGripStyle(
            int seed,
            ShortSwordFamily family,
            ShortSwordHeroZone heroZone,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.GripStyle,
                    out int lockedGrip))
            {
                return (ShortSwordGripStyle)lockedGrip;
            }

            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.GripStyle);
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.GripStyle);
            // Every authored grip construction is already curated by family.
            // Do not apply a second "quiet" roll here: that made most world
            // swords converge on simple leather bands even though their blade
            // definitions were visibly diverse.
            return (ShortSwordGripStyle)candidates[
                random.Next(0, candidates.Count)];
        }

        private static ShortSwordGuardConstruction
            SelectGuardSupportingRequirements(
                int seed,
                ShortSwordFamily family,
                ShortSwordGuardConstruction fallback,
                bool requiresBinding,
                bool requiresGem)
        {
            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.GuardConstruction);
            var compatible = new List<ShortSwordGuardConstruction>();
            for (int index = 0; index < candidates.Count; index++)
            {
                var construction =
                    (ShortSwordGuardConstruction)candidates[index];
                if ((!requiresBinding || SupportsGuardBinding(construction)) &&
                    (!requiresGem || SupportsGuardGem(construction)))
                {
                    compatible.Add(construction);
                }
            }
            if (compatible.Count == 0)
            {
                return fallback;
            }
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.GuardConstruction,
                97);
            return compatible[random.Next(0, compatible.Count)];
        }

        private static bool SupportsGuardGem(
            ShortSwordGuardConstruction construction)
        {
            return !IsDirectionalGuardConstruction(construction) &&
                construction != ShortSwordGuardConstruction.MinimalBolster;
        }

        private static ShortSwordGuardConstruction
            SelectGuardSupportingBinding(
                int seed,
                ShortSwordFamily family,
                ShortSwordGuardConstruction fallback)
        {
            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.GuardConstruction);
            var compatible = new List<ShortSwordGuardConstruction>();
            for (int index = 0; index < candidates.Count; index++)
            {
                var construction =
                    (ShortSwordGuardConstruction)candidates[index];
                if (SupportsGuardBinding(construction))
                {
                    compatible.Add(construction);
                }
            }
            if (compatible.Count == 0)
            {
                return fallback;
            }
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.GuardBindingStyle,
                61);
            return compatible[random.Next(0, compatible.Count)];
        }

        private static bool SupportsGuardBinding(
            ShortSwordGuardConstruction construction)
        {
            return construction != ShortSwordGuardConstruction.RazorBar &&
                construction != ShortSwordGuardConstruction.MinimalBolster;
        }

        private static ShortSwordHiltProfile SelectHiltSupportingGem(
            int seed,
            ShortSwordFamily family,
            ShortSwordHiltProfile fallback)
        {
            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.HiltProfile);
            var compatible = new List<ShortSwordHiltProfile>();
            for (int index = 0; index < candidates.Count; index++)
            {
                var profile = (ShortSwordHiltProfile)candidates[index];
                if (SupportsPommelGem(profile))
                {
                    compatible.Add(profile);
                }
            }
            if (compatible.Count == 0)
            {
                return fallback;
            }
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.OrnamentStyle,
                83);
            return compatible[random.Next(0, compatible.Count)];
        }

        private static bool SupportsPommelGem(
            ShortSwordHiltProfile profile)
        {
            return profile is
                ShortSwordHiltProfile.Disc or
                ShortSwordHiltProfile.Faceted or
                ShortSwordHiltProfile.ScentStopper or
                ShortSwordHiltProfile.Crowned or
                ShortSwordHiltProfile.Acorn or
                ShortSwordHiltProfile.BrazilNut or
                ShortSwordHiltProfile.Mushroom;
        }

        private static ShortSwordGuardBindingStyle SelectGuardBindingStyle(
            int seed,
            ShortSwordFamily family,
            ShortSwordHeroZone heroZone,
            ShortSwordGuardConstruction construction,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.GuardBindingStyle,
                    out int lockedBinding))
            {
                var binding = (ShortSwordGuardBindingStyle)lockedBinding;
                return binding == ShortSwordGuardBindingStyle.None ||
                    SupportsGuardBinding(construction)
                        ? binding
                        : ShortSwordGuardBindingStyle.None;
            }

            bool supportsBinding = SupportsGuardBinding(construction);
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.GuardBindingStyle);
            float unboundChance = heroZone == ShortSwordHeroZone.Guard
                ? 0.18f
                : 0.45f;
            if (!supportsBinding || random.NextDouble() < unboundChance)
            {
                return ShortSwordGuardBindingStyle.None;
            }

            IReadOnlyList<int> candidates =
                ShortSwordGenerationBranchCatalog.GetCandidateValues(
                    family,
                    ShortSwordGenerationDecision.GuardBindingStyle);
            int firstBoundOption = candidates.Count > 1 ? 1 : 0;
            int optionIndex = random.Next(firstBoundOption, candidates.Count);
            return (ShortSwordGuardBindingStyle)candidates[optionIndex];
        }

        private static int SelectGuardCrossSectionSides(
            int seed,
            ShortSwordFacetTier facetTier,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.GuardCrossSectionSides,
                    out int lockedValue))
            {
                return lockedValue;
            }
            int[] candidates = facetTier switch
            {
                ShortSwordFacetTier.Coarse => new[] { 4, 6 },
                ShortSwordFacetTier.Intricate => new[] { 8, 10, 12 },
                _ => new[] { 6, 8, 10 }
            };
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.GuardCrossSectionSides);
            return candidates[random.Next(0, candidates.Length)];
        }

        private static int SelectGuardCurveSegments(
            int seed,
            ShortSwordFacetTier facetTier,
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.GuardCurveSegments,
                    out int lockedValue))
            {
                return lockedValue;
            }
            int[] candidates = facetTier switch
            {
                ShortSwordFacetTier.Coarse => new[] { 6, 8 },
                ShortSwordFacetTier.Intricate => new[] { 10, 12, 14 },
                _ => new[] { 8, 10, 12 }
            };
            var random = CreateGenerationRandom(
                seed,
                ShortSwordGenerationDecision.GuardCurveSegments);
            return candidates[random.Next(0, candidates.Length)];
        }

        private static int LockedOrRolled(
            ProceduralShortSwordGenerationConstraints constraints,
            ShortSwordGenerationDecision decision,
            int rolledValue)
        {
            return TryGetLockedValue(
                constraints,
                decision,
                out int lockedValue)
                    ? lockedValue
                    : rolledValue;
        }

        private static bool TryGetLockedValue(
            ProceduralShortSwordGenerationConstraints constraints,
            ShortSwordGenerationDecision decision,
            out int value)
        {
            if (constraints != null &&
                constraints.TryGetValue(decision, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static bool HasLock(
            ProceduralShortSwordGenerationConstraints constraints,
            ShortSwordGenerationDecision decision)
        {
            return TryGetLockedValue(constraints, decision, out _);
        }

        private static bool IsLocked(
            ProceduralShortSwordGenerationConstraints constraints,
            ShortSwordGenerationDecision decision,
            int value)
        {
            return TryGetLockedValue(
                    constraints,
                    decision,
                    out int lockedValue) &&
                lockedValue == value;
        }

        private static bool RequiresDirectionalBlade(
            ProceduralShortSwordGenerationConstraints constraints)
        {
            if (IsLocked(
                    constraints,
                    ShortSwordGenerationDecision.Directionality,
                    (int)ShortSwordDirectionality.Directional) ||
                HasLock(
                    constraints,
                    ShortSwordGenerationDecision.DirectionSide) ||
                IsLocked(
                    constraints,
                    ShortSwordGenerationDecision.BladeBackStyle,
                    (int)ShortSwordBladeBackStyle.Sawback))
            {
                return true;
            }

            return TryGetLockedValue(
                    constraints,
                    ShortSwordGenerationDecision.GuardConstruction,
                    out int guard) &&
                IsDirectionalGuardConstruction(
                    (ShortSwordGuardConstruction)guard);
        }

        private static bool RequiresConventionalBlade(
            ProceduralShortSwordGenerationConstraints constraints)
        {
            return IsLocked(
                    constraints,
                    ShortSwordGenerationDecision.Directionality,
                    (int)ShortSwordDirectionality.Conventional) ||
                IsLocked(
                    constraints,
                    ShortSwordGenerationDecision.GuardConstruction,
                    (int)ShortSwordGuardConstruction.RazorBar);
        }

        private static bool IsDirectionalGuardConstruction(
            ShortSwordGuardConstruction construction)
        {
            return construction ==
                    ShortSwordGuardConstruction.DirectionalSweep ||
                construction == ShortSwordGuardConstruction.OffsetLeaf;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }

        private static ShortSwordOrnamentStyle SelectOrnamentStyle(
            System.Random random,
            ShortSwordHeroZone heroZone)
        {
            int roll = random.Next(0, 100);
            int plainThreshold = heroZone switch
            {
                ShortSwordHeroZone.Blade => 62,
                ShortSwordHeroZone.Guard => 48,
                ShortSwordHeroZone.Grip => 54,
                _ => 56
            };
            int guardThreshold = heroZone switch
            {
                ShortSwordHeroZone.Guard => 78,
                ShortSwordHeroZone.Grip => 72,
                _ => 70
            };
            if (roll < plainThreshold)
            {
                return ShortSwordOrnamentStyle.Plain;
            }
            if (roll < guardThreshold)
            {
                return ShortSwordOrnamentStyle.GuardGem;
            }
            return ShortSwordOrnamentStyle.PommelGem;
        }

        private static bool IsDirectionalBlade(
            ShortSwordBladeProfile profile)
        {
            return ShortSwordGenerationBranchCatalog.
                IsDirectionalBladeProfile(profile);
        }

        private static bool IsSingleEdgedBlade(
            ShortSwordBladeProfile profile)
        {
            return profile is
                ShortSwordBladeProfile.ForwardSwept or
                ShortSwordBladeProfile.ClipPoint or
                ShortSwordBladeProfile.Seax or
                ShortSwordBladeProfile.Falchion or
                ShortSwordBladeProfile.Kopis or
                ShortSwordBladeProfile.Hanger;
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
                ShortSwordGuardConstruction.DownturnedHooks =>
                    ShortSwordGuardProfile.Downturned,
                ShortSwordGuardConstruction.GreekWings =>
                    ShortSwordGuardProfile.Upswept,
                ShortSwordGuardConstruction.SQuillons =>
                    ShortSwordGuardProfile.Slanted,
                ShortSwordGuardConstruction.LobedCross =>
                    ShortSwordGuardProfile.Bowed,
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
                ShortSwordGuardConstruction.MinimalBolster =>
                    Lerp(random, 0.019f, 0.026f),
                ShortSwordGuardConstruction.DownturnedHooks =>
                    Lerp(random, 0.018f, 0.024f),
                ShortSwordGuardConstruction.GreekWings =>
                    Lerp(random, 0.020f, 0.027f),
                ShortSwordGuardConstruction.SQuillons =>
                    Lerp(random, 0.017f, 0.023f),
                ShortSwordGuardConstruction.LobedCross =>
                    Lerp(random, 0.021f, 0.028f),
                _ => Lerp(random, 0.017f, 0.022f)
            };
            float massScale = construction switch
            {
                ShortSwordGuardConstruction.WingedW => 0.010f,
                ShortSwordGuardConstruction.Crescent => 0.010f,
                ShortSwordGuardConstruction.RazorBar => 0.006f,
                ShortSwordGuardConstruction.MinimalBolster => 0.009f,
                ShortSwordGuardConstruction.LobedCross => 0.011f,
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
            renderer.reflectionProbeUsage =
                UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
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
            CreateGuardBindings(guard, definition);
            if (definition.OrnamentStyle ==
                ShortSwordOrnamentStyle.GuardGem)
            {
                CreateMirroredGuardGem(
                    guard,
                    definition,
                    ResolveGuardGemRadii(definition));
            }
        }

        private void CreateGuardBindings(
            GameObject guard,
            ProceduralShortSwordDefinition definition)
        {
            ShortSwordGuardBindingStyle binding =
                definition.GuardBindingStyle;
            if (binding == ShortSwordGuardBindingStyle.None ||
                !SupportsGuardBinding(definition.GuardConstruction))
            {
                return;
            }
            bool bindLeft = binding is
                ShortSwordGuardBindingStyle.LeftLeather or
                ShortSwordGuardBindingStyle.BothArms or
                ShortSwordGuardBindingStyle.LeftCord;
            bool bindRight = binding is
                ShortSwordGuardBindingStyle.RightLeather or
                ShortSwordGuardBindingStyle.BothArms or
                ShortSwordGuardBindingStyle.RightCord;
            if (definition.DirectionSign < 0 &&
                IsDirectionalBlade(definition.BladeProfile))
            {
                (bindLeft, bindRight) = (bindRight, bindLeft);
            }
            if (!bindLeft && !bindRight)
            {
                return;
            }

            bool cord = binding is
                ShortSwordGuardBindingStyle.LeftCord or
                ShortSwordGuardBindingStyle.RightCord;
            int count = cord ? 4 : 3;
            Color bindingColor = Color.Lerp(
                ResolveGripColor(definition.GripColor),
                cord ? new Color(0.68f, 0.54f, 0.34f) : Color.black,
                cord ? 0.34f : 0.18f);
            if (bindLeft)
            {
                CreateGuardBindingSide(
                    guard,
                    definition,
                    negative: true,
                    count,
                    cord,
                    bindingColor);
            }
            if (bindRight)
            {
                CreateGuardBindingSide(
                    guard,
                    definition,
                    negative: false,
                    count,
                    cord,
                    bindingColor);
            }
        }

        private void CreateGuardBindingSide(
            GameObject guard,
            ProceduralShortSwordDefinition definition,
            bool negative,
            int count,
            bool cord,
            Color color)
        {
            float sideSpan = ResolveGuardSideSpan(definition, negative);
            float sideSign = negative ? -1f : 1f;
            float bindingWidth = sideSpan * (cord ? 0.032f : 0.055f);
            for (int index = 0; index < count; index++)
            {
                float t = Mathf.Lerp(0.48f, 0.72f, (index + 0.5f) / count);
                float x = sideSign * sideSpan * t;
                CreateDecoration(
                    guard.transform,
                    $"{(negative ? "Left" : "Right")} Guard " +
                    $"{(cord ? "Cord" : "Leather")} Wrap {index + 1}",
                    BuildGuardBindingMesh(
                        definition,
                        x,
                        bindingWidth,
                        cord ? 0.0012f : 0.0018f),
                    handleMaterial,
                    color,
                    0f,
                    cord ? 0.08f : 0.12f);
            }
        }

        private static Mesh BuildGuardBindingMesh(
            ProceduralShortSwordDefinition definition,
            float centerX,
            float width,
            float relief)
        {
            int sides = Mathf.Clamp(
                definition.GuardCrossSectionSides,
                4,
                12);
            var vertices = new List<Vector3>(sides * 2 + 2);
            var triangles = new List<int>(sides * 12);
            float firstCenterY = 0f;
            float secondCenterY = 0f;
            for (int end = 0; end < 2; end++)
            {
                float x = centerX + (end == 0 ? -width : width) * 0.5f;
                ResolveGuardSectionAtX(
                    definition,
                    x,
                    relief,
                    out float centerY,
                    out float halfHeight,
                    out float halfDepth);
                if (end == 0)
                {
                    firstCenterY = centerY;
                }
                else
                {
                    secondCenterY = centerY;
                }
                for (int side = 0; side < sides; side++)
                {
                    float angle = definition.GuardCrossSectionRotation +
                        side / (float)sides * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        x,
                        centerY + Mathf.Cos(angle) * halfHeight,
                        Mathf.Sin(angle) * halfDepth));
                }
            }
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                AddQuad(
                    triangles,
                    side,
                    next,
                    sides + next,
                    sides + side);
            }

            int firstCenter = vertices.Count;
            vertices.Add(new Vector3(
                centerX - width * 0.5f,
                firstCenterY,
                0f));
            int secondCenter = vertices.Count;
            vertices.Add(new Vector3(
                centerX + width * 0.5f,
                secondCenterY,
                0f));
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles.Add(firstCenter);
                triangles.Add(next);
                triangles.Add(side);
                triangles.Add(secondCenter);
                triangles.Add(sides + side);
                triangles.Add(sides + next);
            }
            return CreateMesh(vertices, triangles);
        }

        private static void ResolveGuardSectionAtX(
            ProceduralShortSwordDefinition definition,
            float x,
            float relief,
            out float centerY,
            out float halfHeight,
            out float halfDepth)
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
            float taper = Mathf.Lerp(
                1f,
                ResolveGuardTipScale(definition, normalizedX, bladeMass),
                Mathf.Pow(
                    edge,
                    ResolveGuardTaperExponent(
                        definition.GuardConstruction)));
            centerY = ResolveGuardCenterHeight(definition, normalizedX);
            halfHeight = definition.GuardHeight * 0.5f * taper + relief;
            halfDepth = definition.GuardDepth * 0.5f *
                Mathf.Lerp(1f, 0.72f, edge) + relief;
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
                    for (int strand = 0; strand < 2; strand++)
                    {
                        CreateDecoration(
                            handle.transform,
                            strand == 0
                                ? "Cross Cord Clockwise"
                                : "Cross Cord Counterclockwise",
                            BuildHelixMesh(
                                definition,
                                clockwise: strand == 0,
                                turns: 2.35f,
                                thicknessScale: 0.82f,
                                radialOffset: WovenGripRadialOffset,
                                phaseOffset: strand * Mathf.PI * 0.20f,
                                alternatingWeave: true,
                                weaveStrand: strand,
                                weavePairPhaseOffset: Mathf.PI * 0.20f),
                            handleMaterial,
                            Color.Lerp(
                                grip,
                                new Color(0.72f, 0.60f, 0.42f),
                                0.42f),
                            0f,
                            0.12f);
                    }
                    break;
                case ShortSwordGripStyle.SpiralLeather:
                    CreateDecoration(
                        handle.transform,
                        "Spiral Leather Wrap",
                        BuildHelixMesh(
                            definition,
                            clockwise: definition.DirectionSign > 0,
                            turns: 3.15f,
                            thicknessScale: 1.45f),
                        handleMaterial,
                        Color.Lerp(grip, Color.black, 0.16f),
                        0f,
                        0.12f);
                    break;
                case ShortSwordGripStyle.HerringboneCord:
                    for (int strand = 0; strand < 2; strand++)
                    {
                        CreateDecoration(
                            handle.transform,
                            strand == 0
                                ? "Herringbone Cord Clockwise"
                                : "Herringbone Cord Counterclockwise",
                            BuildHelixMesh(
                                definition,
                                clockwise: strand == 0,
                                turns: 3.4f,
                                thicknessScale: 0.72f,
                                radialOffset: WovenGripRadialOffset,
                                phaseOffset: strand * Mathf.PI * 0.25f,
                                alternatingWeave: true,
                                weaveStrand: strand,
                                weavePairPhaseOffset: Mathf.PI * 0.25f),
                            handleMaterial,
                            Color.Lerp(
                                grip,
                                new Color(0.72f, 0.60f, 0.42f),
                                0.34f),
                            0f,
                            0.10f);
                    }
                    break;
                case ShortSwordGripStyle.WireBoundLeather:
                    for (int strand = 0; strand < 2; strand++)
                    {
                        CreateDecoration(
                            handle.transform,
                            $"Wire Grip Strand {strand + 1}",
                            BuildHelixMesh(
                                definition,
                                clockwise: true,
                                turns: 5.2f,
                                thicknessScale: 0.48f,
                                radialOffset: 0.0028f,
                                phaseOffset: strand * Mathf.PI),
                            guardMaterial,
                            ResolveMetalAccentColor(definition.MetalFamily),
                            ResolveMetallic(definition.MetalFamily),
                            0.22f);
                    }
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
                            Mathf.Sin(angle));
                        radial *= ResolveHandleDecorationRadius(
                            definition,
                            t,
                            angle,
                            surfaceRadius) + 0.004f;
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
                case ShortSwordGripStyle.HalfWrappedWood:
                    for (int index = 0; index < 4; index++)
                    {
                        float t = Mathf.Lerp(0.48f, 0.88f, index / 3f);
                        CreateDecoration(
                            handle.transform,
                            $"Half Grip Wrap {index + 1}",
                            BuildBandMesh(
                                Mathf.Lerp(top, bottom, t),
                                ResolveHandleSurfaceRadius(definition, t) +
                                    0.003f,
                                0.012f,
                                ResolveHandleDepthScale(definition),
                                ResolveHandleCrossSectionSides(definition)),
                            handleMaterial,
                            Color.Lerp(grip, Color.black, 0.18f),
                            0f,
                            0.10f);
                    }
                    break;
                case ShortSwordGripStyle.FacetedLeather:
                    for (int index = 0; index < 3; index++)
                    {
                        float t = index / 2f;
                        CreateDecoration(
                            handle.transform,
                            $"Faceted Grip Seam {index + 1}",
                            BuildBandMesh(
                                Mathf.Lerp(top, bottom, t),
                                ResolveHandleSurfaceRadius(definition, t) +
                                    0.0024f,
                                0.008f,
                                ResolveHandleDepthScale(definition),
                                ResolveHandleCrossSectionSides(definition)),
                            handleMaterial,
                            detail,
                            0f,
                            0.08f);
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
                                        : 0.007f,
                                ResolveHandleDepthScale(definition),
                                ResolveHandleCrossSectionSides(definition)),
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
                    ShortSwordOrnamentStyle.PommelGem ||
                !SupportsPommelGem(definition.HiltProfile))
            {
                return;
            }
            float normalizedHeight = 0.56f;
            float radiusScale = definition.HiltProfile switch
            {
                ShortSwordHiltProfile.Disc => 1f,
                ShortSwordHiltProfile.BrazilNut => 0.99f,
                ShortSwordHiltProfile.Mushroom => 1.06f,
                ShortSwordHiltProfile.Acorn => 0.94f,
                _ => 0.92f
            };
            float surfaceDepth = definition.HiltRadius * radiusScale *
                ResolveCrossSectionHorizontalFactor(
                    ResolveHiltSides(definition),
                    0f);
            Vector3 radii = new Vector3(
                Mathf.Min(0.013f, definition.HiltRadius * 0.31f),
                Mathf.Min(0.016f, definition.HiltLength * 0.20f),
                0.003f);
            for (int face = -1; face <= 1; face += 2)
            {
                Vector3 gemCenter = new Vector3(
                    0f,
                    top - definition.HiltLength * normalizedHeight,
                    face * (surfaceDepth + 0.0003f));
                CreateDecoration(
                    hilt.transform,
                    $"{(face > 0 ? "Front" : "Rear")} Pommel Jewel",
                    BuildGemMesh(
                        gemCenter,
                        radii,
                        definition.GemCut,
                        face),
                    hiltMaterial,
                    ResolveGemColor(definition.GemFamily),
                    0.10f,
                    0.62f);
            }
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
            renderer.reflectionProbeUsage =
                UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            ApplyRendererProperties(
                renderer,
                color,
                metallic,
                smoothness);
            return decoration;
        }

        private void ApplyRendererProperties(
            Renderer renderer,
            Color color,
            float metallic,
            float smoothness)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            float boundedMetallic = neutralizeBaseTextures
                ? Mathf.Min(metallic, WorldSwordMetallic)
                : Mathf.Min(metallic, 0.58f);
            float boundedSmoothness = neutralizeBaseTextures
                ? Mathf.Min(smoothness, WorldSwordSmoothness)
                : Mathf.Min(smoothness, 0.38f);
            properties.SetFloat("_Metallic", boundedMetallic);
            properties.SetFloat("_Smoothness", boundedSmoothness);
            properties.SetFloat("_Glossiness", boundedSmoothness);

            // Emission stays explicitly black on every renderer so legacy
            // source materials cannot turn a normally-lit blade into a light
            // source. The shared Lit material also compiles out specular and
            // environment reflections; these matching values prevent tooling
            // and material migration from silently opting a renderer back in.
            properties.SetColor("_EmissionColor", Color.black);
            properties.SetTexture("_EmissionMap", Texture2D.blackTexture);
            properties.SetFloat("_ClearCoatMask", 0f);
            properties.SetFloat("_ClearCoatSmoothness", 0f);
            properties.SetTexture("_ClearCoatMap", Texture2D.blackTexture);
            if (neutralizeBaseTextures)
            {
                // Raid prefabs historically supply a brown leather albedo.
                // Replacing that sampled color with white lets the generated
                // grip/metal palette pass through unchanged while retaining
                // the controlled world-sword lighting response.
                properties.SetTexture("_BaseMap", Texture2D.whiteTexture);
                properties.SetTexture("_MainTex", Texture2D.whiteTexture);
                properties.SetTexture(
                    "_DetailAlbedoMap",
                    Texture2D.blackTexture);
                properties.SetTexture(
                    "_SpecGlossMap",
                    Texture2D.blackTexture);
                properties.SetTexture(
                    "_MetallicGlossMap",
                    Texture2D.blackTexture);
                properties.SetTexture(
                    "_BumpMap",
                    Texture2D.normalTexture);
                properties.SetTexture(
                    "_DetailNormalMap",
                    Texture2D.normalTexture);
                properties.SetTexture(
                    "_ParallaxMap",
                    Texture2D.blackTexture);
                properties.SetColor(
                    "_SpecColor",
                    new Color(0.20f, 0.21f, 0.22f, 1f));
                properties.SetFloat("_SpecularHighlights", 0f);
                properties.SetFloat("_EnvironmentReflections", 0f);
            }
            renderer.SetPropertyBlock(properties);
        }

        public static Color ResolveMetalColor(ShortSwordMetalFamily family)
        {
            return family switch
            {
                ShortSwordMetalFamily.Bronze => new Color(0.46f, 0.29f, 0.13f),
                ShortSwordMetalFamily.Silver => new Color(0.66f, 0.69f, 0.70f),
                ShortSwordMetalFamily.BlackenedSteel => new Color(0.13f, 0.15f, 0.16f),
                ShortSwordMetalFamily.AgedSteel => new Color(0.31f, 0.32f, 0.29f),
                ShortSwordMetalFamily.BlueSteel => new Color(0.24f, 0.31f, 0.38f),
                ShortSwordMetalFamily.CopperAlloy => new Color(0.48f, 0.25f, 0.15f),
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
                ShortSwordMetalFamily.AgedSteel =>
                    new Color(0.48f, 0.49f, 0.45f),
                ShortSwordMetalFamily.BlueSteel =>
                    new Color(0.47f, 0.53f, 0.59f),
                ShortSwordMetalFamily.CopperAlloy =>
                    new Color(0.53f, 0.50f, 0.44f),
                _ => new Color(0.56f, 0.58f, 0.57f)
            };
        }

        public static Color ResolveGripColor(ShortSwordGripColor color)
        {
            return color switch
            {
                ShortSwordGripColor.OxBlood => new Color(0.38f, 0.08f, 0.055f),
                ShortSwordGripColor.Charcoal => new Color(0.13f, 0.14f, 0.135f),
                ShortSwordGripColor.WornTan => new Color(0.52f, 0.33f, 0.15f),
                ShortSwordGripColor.ForestGreen => new Color(0.12f, 0.30f, 0.16f),
                ShortSwordGripColor.Navy => new Color(0.07f, 0.15f, 0.33f),
                ShortSwordGripColor.Bone => new Color(0.70f, 0.61f, 0.44f),
                ShortSwordGripColor.Ochre => new Color(0.59f, 0.34f, 0.06f),
                _ => new Color(0.30f, 0.13f, 0.055f)
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
            float targetFacetLength = definition.FacetTier switch
            {
                ShortSwordFacetTier.Coarse => 0.074f,
                ShortSwordFacetTier.Intricate => 0.038f,
                _ => TargetFacetLength
            };
            int segments = Mathf.Max(
                8,
                Mathf.CeilToInt(facetedLength / targetFacetLength));
            List<float> ringHeights = BuildBladeRingHeights(
                definition,
                baseHeight,
                segments);
            int ringCount = ringHeights.Count;
            int ringVertexCount = ResolveBladeRingVertexCount(
                definition.BladeSectionStyle);
            var vertices = new List<Vector3>(
                ringCount * ringVertexCount + 2);
            var triangles = new List<int>(
                ringCount * ringVertexCount * 6);

            for (int ring = 0; ring < ringCount; ring++)
            {
                float y = ringHeights[ring];
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
                AddBladeRing(
                    vertices,
                    definition,
                    ring == 0,
                    y,
                    leftWidth,
                    rightWidth,
                    ridgeDepth);
            }

            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                int current = ring * ringVertexCount;
                int nextRing = (ring + 1) * ringVertexCount;
                for (int side = 0; side < ringVertexCount; side++)
                {
                    int nextSide = (side + 1) % ringVertexCount;
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
            int lastRing = (ringCount - 1) * ringVertexCount;
            for (int side = 0; side < ringVertexCount; side++)
            {
                int nextSide = (side + 1) % ringVertexCount;
                triangles.Add(lastRing + side);
                triangles.Add(lastRing + nextSide);
                triangles.Add(tip);
            }

            int baseCenter = vertices.Count;
            vertices.Add(new Vector3(0f, baseHeight, 0f));
            for (int side = 0; side < ringVertexCount; side++)
            {
                int nextSide = (side + 1) % ringVertexCount;
                triangles.Add(baseCenter);
                triangles.Add(nextSide);
                triangles.Add(side);
            }
            return CreateMesh(vertices, triangles);
        }

        private static List<float> BuildBladeRingHeights(
            ProceduralShortSwordDefinition definition,
            float baseHeight,
            int uniformSegments)
        {
            var heights = new List<float>(uniformSegments + 32);
            for (int ring = 0; ring < uniformSegments; ring++)
            {
                heights.Add(Mathf.Lerp(
                    baseHeight,
                    definition.BladeLength,
                    ring / (float)uniformSegments));
            }

            switch (definition.BladeBaseStyle)
            {
                case ShortSwordBladeBaseStyle.NarrowRicasso:
                    AddBladeBreakpoint(
                        heights,
                        Mathf.Lerp(baseHeight, definition.BladeLength, 0.13f),
                        definition,
                        sharp: true);
                    AddBladeBreakpoint(
                        heights,
                        Mathf.Lerp(baseHeight, definition.BladeLength, 0.22f),
                        definition);
                    break;
                case ShortSwordBladeBaseStyle.FlaredShoulders:
                    AddBladeBreakpoint(
                        heights,
                        Mathf.Lerp(baseHeight, definition.BladeLength, 0.20f),
                        definition);
                    break;
                case ShortSwordBladeBaseStyle.SteppedShoulders:
                    AddBladeBreakpoint(
                        heights,
                        Mathf.Lerp(baseHeight, definition.BladeLength, 0.08f),
                        definition,
                        sharp: true);
                    AddBladeBreakpoint(
                        heights,
                        Mathf.Lerp(baseHeight, definition.BladeLength, 0.17f),
                        definition,
                        sharp: true);
                    break;
                case ShortSwordBladeBaseStyle.SmallChoil:
                    AddBladeBreakpoint(
                        heights,
                        definition.BladeLength * 0.02f,
                        definition);
                    AddBladeBreakpoint(
                        heights,
                        definition.BladeLength * 0.15f,
                        definition);
                    break;
                case ShortSwordBladeBaseStyle.ReinforcedBase:
                    AddBladeBreakpoint(
                        heights,
                        Mathf.Lerp(baseHeight, definition.BladeLength, 0.24f),
                        definition);
                    break;
            }

            int spineBreaks = definition.BladeBackStyle switch
            {
                ShortSwordBladeBackStyle.Sawback => 9,
                ShortSwordBladeBackStyle.SteppedSpine => 4,
                ShortSwordBladeBackStyle.ScallopedSpine => 6,
                _ => 0
            };
            for (int breakpoint = 0;
                 breakpoint <= spineBreaks && spineBreaks > 0;
                 breakpoint++)
            {
                float t = Mathf.Lerp(
                    0.18f,
                    0.72f,
                    breakpoint / (float)spineBreaks);
                AddBladeBreakpoint(
                    heights,
                    definition.BladeLength * t,
                    definition,
                    sharp: true);
            }
            if (definition.BladeBackStyle ==
                ShortSwordBladeBackStyle.BrokenBack)
            {
                AddBladeBreakpoint(
                    heights,
                    definition.BladeLength * 0.18f,
                    definition,
                    sharp: true);
                AddBladeBreakpoint(
                    heights,
                    definition.BladeLength *
                        Mathf.Lerp(0.18f, 0.72f, 0.58f),
                    definition,
                    sharp: true);
                AddBladeBreakpoint(
                    heights,
                    definition.BladeLength * 0.72f,
                    definition,
                    sharp: true);
            }

            float taperStart = definition.BladeLength -
                definition.TipLength * ResolveBladeTaperMultiplier(
                    definition.BladeProfile);
            AddBladeBreakpoint(heights, taperStart, definition);
            heights.Sort();
            for (int index = heights.Count - 1; index > 0; index--)
            {
                if (heights[index] - heights[index - 1] < 0.00045f)
                {
                    heights.RemoveAt(index);
                }
            }
            return heights;
        }

        private static void AddBladeBreakpoint(
            List<float> heights,
            float height,
            ProceduralShortSwordDefinition definition,
            bool sharp = false)
        {
            float minimum = ResolveBladeSeatHeightAtX(definition, 0f) +
                0.0005f;
            float maximum = definition.BladeLength - 0.001f;
            if (sharp)
            {
                heights.Add(Mathf.Clamp(height - 0.0012f, minimum, maximum));
                heights.Add(Mathf.Clamp(height + 0.0012f, minimum, maximum));
                return;
            }
            heights.Add(Mathf.Clamp(height, minimum, maximum));
        }

        private static int ResolveBladeRingVertexCount(
            ShortSwordBladeSectionStyle section)
        {
            return section == ShortSwordBladeSectionStyle.DiamondRidge
                ? 4
                : 8;
        }

        private static void AddBladeRing(
            List<Vector3> vertices,
            ProceduralShortSwordDefinition definition,
            bool fittedBase,
            float y,
            float leftWidth,
            float rightWidth,
            float ridgeDepth)
        {
            float depthScale = definition.BladeSectionStyle switch
            {
                ShortSwordBladeSectionStyle.BroadMidrib => 1.18f,
                ShortSwordBladeSectionStyle.FlatBevel => 0.58f,
                ShortSwordBladeSectionStyle.ShallowFuller => 0.82f,
                ShortSwordBladeSectionStyle.HexagonalRidge => 0.94f,
                _ => 1f
            };
            float depth = ridgeDepth * depthScale;
            if (definition.BladeSectionStyle ==
                ShortSwordBladeSectionStyle.DiamondRidge)
            {
                AddBladeRingPoint(vertices, definition, fittedBase, y, 0f, depth);
                AddBladeRingPoint(
                    vertices,
                    definition,
                    fittedBase,
                    y,
                    rightWidth,
                    0f);
                AddBladeRingPoint(vertices, definition, fittedBase, y, 0f, -depth);
                AddBladeRingPoint(
                    vertices,
                    definition,
                    fittedBase,
                    y,
                    -leftWidth,
                    0f);
                return;
            }

            float centerDepth = definition.BladeSectionStyle ==
                    ShortSwordBladeSectionStyle.ShallowFuller
                ? depth * 0.38f
                : depth;
            float shoulderDepth = definition.BladeSectionStyle switch
            {
                ShortSwordBladeSectionStyle.BroadMidrib => depth * 0.72f,
                ShortSwordBladeSectionStyle.FlatBevel => depth * 0.54f,
                ShortSwordBladeSectionStyle.ShallowFuller => depth * 0.78f,
                _ => depth * 0.62f
            };
            float shoulderScale = definition.BladeSectionStyle ==
                    ShortSwordBladeSectionStyle.BroadMidrib
                ? 0.34f
                : 0.68f;
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                0f,
                centerDepth);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                rightWidth * shoulderScale,
                shoulderDepth);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                rightWidth,
                0f);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                rightWidth * shoulderScale,
                -shoulderDepth);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                0f,
                -centerDepth);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                -leftWidth * shoulderScale,
                -shoulderDepth);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                -leftWidth,
                0f);
            AddBladeRingPoint(
                vertices,
                definition,
                fittedBase,
                y,
                -leftWidth * shoulderScale,
                shoulderDepth);
        }

        private static void AddBladeRingPoint(
            List<Vector3> vertices,
            ProceduralShortSwordDefinition definition,
            bool fittedBase,
            float y,
            float x,
            float z)
        {
            float centeredX = x + ResolveBladeCenterOffset(definition, y);
            vertices.Add(new Vector3(
                centeredX,
                fittedBase
                    ? ResolveBladeSeatHeightAtX(definition, centeredX)
                    : y,
                z));
        }

        private static float ResolveBladeHalfWidthAtHeight(
            ProceduralShortSwordDefinition definition,
            float height,
            float halfWidth)
        {
            float baseHeight = ResolveBladeSeatHeightAtX(definition, 0f);
            float bladeT = Mathf.InverseLerp(
                baseHeight,
                definition.BladeLength,
                height);
            float silhouetteScale = definition.BladeProfile switch
            {
                ShortSwordBladeProfile.LeafBlade =>
                    Mathf.Lerp(0.80f, 1f, Mathf.SmoothStep(0f, 1f, bladeT / 0.24f)) *
                    (1f + Mathf.Sin(bladeT * Mathf.PI) * 0.22f),
                ShortSwordBladeProfile.Gladius =>
                    bladeT < 0.70f
                        ? Mathf.Lerp(1.08f, 0.98f, bladeT / 0.70f)
                        : 1f,
                ShortSwordBladeProfile.PiercingDiamond =>
                    Mathf.Lerp(0.72f, 0.60f, bladeT),
                ShortSwordBladeProfile.Seax =>
                    Mathf.Lerp(1.04f, 0.94f, bladeT),
                ShortSwordBladeProfile.Falchion =>
                    Mathf.Lerp(0.88f, 1.24f, Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.08f, 0.66f, bladeT))),
                ShortSwordBladeProfile.Kopis =>
                    bladeT < 0.36f
                        ? Mathf.Lerp(0.94f, 0.76f, bladeT / 0.36f)
                        : Mathf.Lerp(
                            0.76f,
                            1.28f,
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                Mathf.InverseLerp(0.36f, 0.76f, bladeT))),
                ShortSwordBladeProfile.Hanger =>
                    Mathf.Lerp(0.92f, 1.08f, bladeT),
                _ => 1f
            };
            float baseScale = ResolveBladeBaseScale(
                definition.BladeBaseStyle,
                bladeT);
            float familyScale = ResolveFamilyBladeSilhouetteScale(
                definition.Family,
                bladeT);
            halfWidth *= silhouetteScale * familyScale * baseScale;

            float taperMultiplier = ResolveBladeTaperMultiplier(
                definition.BladeProfile);
            float taperStart = definition.BladeLength -
                definition.TipLength * taperMultiplier;
            if (height <= taperStart)
            {
                float baseBlend = Mathf.InverseLerp(
                    baseHeight,
                    0.075f,
                    height);
                return halfWidth * Mathf.Lerp(0.94f, 1f, baseBlend);
            }

            float taper = Mathf.InverseLerp(
                taperStart,
                definition.BladeLength,
                height);
            float remaining = definition.BladeProfile switch
            {
                ShortSwordBladeProfile.RoundedShoulder =>
                    1f - Mathf.SmoothStep(0f, 1f, taper),
                ShortSwordBladeProfile.Gladius =>
                    1f - Mathf.Pow(taper, 1.35f),
                ShortSwordBladeProfile.Seax =>
                    1f - Mathf.Pow(taper, 0.72f),
                ShortSwordBladeProfile.Falchion or
                ShortSwordBladeProfile.Kopis or
                ShortSwordBladeProfile.Hanger =>
                    1f - Mathf.Pow(taper, 0.78f),
                _ => 1f - taper
            };
            return halfWidth * Mathf.Clamp01(remaining);
        }

        private static float ResolveFamilyBladeSilhouetteScale(
            ShortSwordFamily family,
            float bladeT)
        {
            float t = Mathf.Clamp01(bladeT);
            float middle = Mathf.Sin(t * Mathf.PI);
            return family switch
            {
                // Family grammar is deliberately lower-amplitude than the
                // selected profile. It remains present when a family rolls a
                // secondary profile, while the profile still supplies most of
                // the individual sword's variation.
                ShortSwordFamily.Cruciform =>
                    1.02f - middle * middle * 0.055f,
                ShortSwordFamily.Leafblade =>
                    0.94f + middle * 0.14f,
                ShortSwordFamily.Legionary =>
                    t < 0.62f
                        ? Mathf.Lerp(1.08f, 0.97f, t / 0.62f)
                        : Mathf.Lerp(0.97f, 1.01f, Mathf.InverseLerp(
                            0.62f,
                            0.84f,
                            t)),
                ShortSwordFamily.Piercer =>
                    Mathf.Lerp(0.90f, 0.72f, t),
                ShortSwordFamily.Seax =>
                    Mathf.Lerp(1.05f, 0.94f, t),
                ShortSwordFamily.Falchion =>
                    Mathf.Lerp(
                        0.94f,
                        1.15f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(0.16f, 0.72f, t))),
                ShortSwordFamily.Kopis => t < 0.38f
                    ? Mathf.Lerp(1.00f, 0.84f, t / 0.38f)
                    : Mathf.Lerp(
                        0.84f,
                        1.18f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(0.38f, 0.78f, t))),
                ShortSwordFamily.Hanger =>
                    Mathf.Lerp(
                        0.96f,
                        1.10f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(0.12f, 0.82f, t))),
                _ => 1f
            };
        }

        private static float ResolveBladeTaperMultiplier(
            ShortSwordBladeProfile profile)
        {
            return profile switch
            {
                ShortSwordBladeProfile.LongTaper => 1.35f,
                ShortSwordBladeProfile.PiercingDiamond => 1.70f,
                ShortSwordBladeProfile.LeafBlade => 0.78f,
                ShortSwordBladeProfile.Gladius => 0.82f,
                ShortSwordBladeProfile.Seax => 0.72f,
                ShortSwordBladeProfile.Falchion => 0.70f,
                ShortSwordBladeProfile.Kopis => 0.64f,
                ShortSwordBladeProfile.Hanger => 0.72f,
                _ => 1f
            };
        }

        private static float ResolveBladeBaseScale(
            ShortSwordBladeBaseStyle style,
            float bladeT)
        {
            if (bladeT >= 0.24f)
            {
                return 1f;
            }

            return style switch
            {
                ShortSwordBladeBaseStyle.NarrowRicasso =>
                    bladeT < 0.13f
                        ? 0.70f
                        : Mathf.Lerp(0.70f, 1f, Mathf.InverseLerp(
                            0.13f,
                            0.22f,
                            bladeT)),
                ShortSwordBladeBaseStyle.FlaredShoulders =>
                    Mathf.Lerp(1.20f, 1f, Mathf.SmoothStep(
                        0f,
                        1f,
                        bladeT / 0.20f)),
                ShortSwordBladeBaseStyle.SteppedShoulders =>
                    bladeT < 0.08f ? 0.80f : bladeT < 0.17f ? 1.12f : 1f,
                ShortSwordBladeBaseStyle.ReinforcedBase =>
                    Mathf.Lerp(1.14f, 1f, bladeT / 0.24f),
                _ => 1f
            };
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
                case ShortSwordBladeProfile.Seax:
                    leftWidth *= t < 0.68f
                        ? 1.04f
                        : Mathf.Lerp(
                            1.04f,
                            0.28f,
                            Mathf.InverseLerp(0.68f, 1f, t));
                    rightWidth *= Mathf.Lerp(0.96f, 1.12f, t);
                    break;
                case ShortSwordBladeProfile.Falchion:
                    leftWidth *= Mathf.Lerp(0.92f, 0.72f, t);
                    rightWidth *= Mathf.Lerp(1.04f, 1.20f, t);
                    break;
                case ShortSwordBladeProfile.Kopis:
                    leftWidth *= Mathf.Lerp(0.96f, 0.68f, t);
                    rightWidth *= Mathf.Lerp(1.02f, 1.30f, t);
                    break;
                case ShortSwordBladeProfile.Hanger:
                    leftWidth *= Mathf.Lerp(0.98f, 0.78f, t);
                    rightWidth *= Mathf.Lerp(1.02f, 1.16f, t);
                    break;
            }

            if (definition.BladeBaseStyle ==
                    ShortSwordBladeBaseStyle.SmallChoil &&
                t < 0.15f &&
                IsSingleEdgedBlade(definition.BladeProfile))
            {
                rightWidth *= Mathf.Lerp(
                    0.68f,
                    1f,
                    Mathf.InverseLerp(0.02f, 0.15f, t));
            }

            if (t >= 0.18f && t <= 0.72f)
            {
                float backT = Mathf.InverseLerp(0.18f, 0.72f, t);
                switch (definition.BladeBackStyle)
                {
                    case ShortSwordBladeBackStyle.Sawback:
                        int tooth = Mathf.FloorToInt(backT * 9f);
                        leftWidth *= tooth % 2 == 0 ? 0.72f : 1.02f;
                        break;
                    case ShortSwordBladeBackStyle.SteppedSpine:
                        int step = Mathf.Min(
                            3,
                            Mathf.FloorToInt(backT * 4f));
                        float stepScale = 1f - step * 0.055f;
                        leftWidth *= stepScale;
                        if (!IsSingleEdgedBlade(definition.BladeProfile))
                        {
                            rightWidth *= stepScale;
                        }
                        break;
                    case ShortSwordBladeBackStyle.ReinforcedSpine:
                        leftWidth *= 1.08f;
                        if (!IsSingleEdgedBlade(definition.BladeProfile))
                        {
                            rightWidth *= 1.08f;
                        }
                        break;
                    case ShortSwordBladeBackStyle.ScallopedSpine:
                        int scallop = Mathf.FloorToInt(backT * 6f);
                        float scallopScale = scallop % 2 == 0
                            ? 0.86f
                            : 1.02f;
                        leftWidth *= scallopScale;
                        if (!IsSingleEdgedBlade(definition.BladeProfile))
                        {
                            rightWidth *= scallopScale;
                        }
                        break;
                    case ShortSwordBladeBackStyle.BrokenBack:
                        leftWidth *= backT < 0.58f ? 1.04f : 0.72f;
                        break;
                }
            }

            if (definition.DirectionSign < 0 &&
                IsDirectionalBlade(definition.BladeProfile))
            {
                (leftWidth, rightWidth) = (rightWidth, leftWidth);
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
                ShortSwordBladeProfile.Seax =>
                    halfWidth * 0.22f * definition.DirectionSign,
                ShortSwordBladeProfile.Falchion =>
                    halfWidth * 0.34f * definition.DirectionSign,
                ShortSwordBladeProfile.Kopis =>
                    halfWidth * 0.50f * definition.DirectionSign,
                ShortSwordBladeProfile.Hanger =>
                    halfWidth * 0.30f * definition.DirectionSign,
                _ => 0f
            };
        }

        private static float ResolveBladeCenterOffset(
            ProceduralShortSwordDefinition definition,
            float height)
        {
            float baseHeight = ResolveBladeSeatHeightAtX(definition, 0f);
            float t = Mathf.InverseLerp(
                baseHeight,
                definition.BladeLength,
                height);
            float halfWidth = definition.BladeWidth * 0.5f;
            float signedWidth = halfWidth * definition.DirectionSign;
            return definition.BladeProfile switch
            {
                ShortSwordBladeProfile.ForwardSwept =>
                    signedWidth * 0.36f * Mathf.Pow(t, 1.35f),
                ShortSwordBladeProfile.ClipPoint =>
                    signedWidth * 0.12f * Mathf.SmoothStep(0f, 1f, t),
                ShortSwordBladeProfile.Seax =>
                    signedWidth * 0.14f * Mathf.Pow(t, 1.45f),
                ShortSwordBladeProfile.Falchion =>
                    signedWidth * 0.32f * Mathf.Pow(t, 1.35f),
                ShortSwordBladeProfile.Kopis =>
                    signedWidth * (
                        -0.12f * Mathf.Sin(t * Mathf.PI) +
                        0.50f * Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(0.30f, 1f, t))),
                ShortSwordBladeProfile.Hanger =>
                    signedWidth * 0.26f * Mathf.SmoothStep(0f, 1f, t),
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
                ShortSwordGuardConstruction.MinimalBolster =>
                    halfSpan * 0.68f,
                ShortSwordGuardConstruction.SQuillons => halfSpan *
                    (directionSide ? 1.04f : 0.98f),
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
                ShortSwordGuardConstruction.MinimalBolster =>
                    Mathf.Lerp(0.58f, 0.72f, bladeMass),
                ShortSwordGuardConstruction.DownturnedHooks =>
                    Mathf.Lerp(0.10f, 0.20f, bladeMass),
                ShortSwordGuardConstruction.GreekWings =>
                    Mathf.Lerp(0.12f, 0.22f, bladeMass),
                ShortSwordGuardConstruction.SQuillons =>
                    sideDirection > 0f
                        ? Mathf.Lerp(0.08f, 0.18f, bladeMass)
                        : Mathf.Lerp(0.12f, 0.23f, bladeMass),
                ShortSwordGuardConstruction.LobedCross =>
                    Mathf.Lerp(0.24f, 0.38f, bladeMass),
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
                ShortSwordGuardConstruction.MinimalBolster =>
                    -0.004f * Mathf.Pow(edge, 1.4f),
                ShortSwordGuardConstruction.DownturnedHooks =>
                    -0.023f * Mathf.Pow(edge, 1.32f) +
                    0.010f * Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.78f, 1f, edge)),
                ShortSwordGuardConstruction.GreekWings =>
                    edge < 0.68f
                        ? 0.021f * Mathf.SmoothStep(
                            0f,
                            1f,
                            edge / 0.68f)
                        : Mathf.Lerp(
                            0.021f,
                            -0.005f,
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                Mathf.InverseLerp(0.68f, 1f, edge))),
                ShortSwordGuardConstruction.SQuillons =>
                    normalizedX * definition.DirectionSign * 0.021f -
                    Mathf.Sign(normalizedX) * definition.DirectionSign *
                    0.009f * Mathf.Sin(edge * Mathf.PI),
                ShortSwordGuardConstruction.LobedCross =>
                    0.010f * Mathf.Sin(edge * Mathf.PI * 2f) -
                    0.006f * Mathf.Pow(edge, 1.8f),
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
                ShortSwordGuardConstruction.MinimalBolster => 1.75f,
                ShortSwordGuardConstruction.DownturnedHooks => 1.20f,
                ShortSwordGuardConstruction.GreekWings => 1.32f,
                ShortSwordGuardConstruction.SQuillons => 1.05f,
                ShortSwordGuardConstruction.LobedCross => 1.55f,
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
            int sides = ResolveHandleCrossSectionSides(definition);
            float depthScale = ResolveHandleDepthScale(definition);
            return BuildRevolvedMesh(
                rings,
                sides,
                definition,
                depthScale);
        }

        private static Mesh BuildHiltMesh(
            ProceduralShortSwordDefinition definition)
        {
            float top = -definition.HandleLength;
            float bottom = top - definition.HiltLength;
            float radius = definition.HiltRadius;
            float connectionRadius = ResolveHiltConnectionRadius(
                definition);
            if (definition.HiltProfile == ShortSwordHiltProfile.Fishtail)
            {
                return BuildFishtailPommelMesh(
                    top,
                    bottom,
                    radius,
                    connectionRadius,
                    definition.FacetTier);
            }
            if (definition.HiltProfile == ShortSwordHiltProfile.Ring)
            {
                return BuildRingPommelMesh(
                    top,
                    bottom,
                    radius,
                    connectionRadius,
                    definition.FacetTier);
            }
            if (definition.HiltProfile is
                ShortSwordHiltProfile.Hooked or
                ShortSwordHiltProfile.Beaked)
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
                    new Vector2(0.002f * definition.DirectionSign, 0f),
                    new Vector2(0.009f * definition.DirectionSign, 0f),
                    new Vector2(0.022f * definition.DirectionSign, 0f),
                    new Vector2(
                        (definition.HiltProfile == ShortSwordHiltProfile.Beaked
                            ? 0.050f
                            : 0.040f) * definition.DirectionSign,
                        0f),
                    new Vector2(
                        (definition.HiltProfile == ShortSwordHiltProfile.Beaked
                            ? 0.078f
                            : 0.060f) * definition.DirectionSign,
                        0f)
                };
                return BuildRevolvedMesh(
                    hookedRings,
                    centers,
                    ResolveHiltSides(definition));
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
                ShortSwordHiltProfile.Acorn => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.18f), radius * 0.72f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.48f), radius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.78f), radius * 0.78f),
                    new Vector2(bottom, radius * 0.18f)
                },
                ShortSwordHiltProfile.BrazilNut => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.22f), radius * 0.82f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.58f), radius),
                    new Vector2(bottom, radius * 0.54f)
                },
                ShortSwordHiltProfile.Mushroom => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.30f), radius * 0.64f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.58f), radius * 1.08f),
                    new Vector2(Mathf.Lerp(top, bottom, 0.80f), radius),
                    new Vector2(bottom, radius * 0.72f)
                },
                _ => new List<Vector2>
                {
                    new Vector2(top, connectionRadius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.28f), radius),
                    new Vector2(Mathf.Lerp(top, bottom, 0.72f), radius * 0.88f),
                    new Vector2(bottom, radius * 0.48f)
                }
            };
            return BuildRevolvedMesh(rings, ResolveHiltSides(definition));
        }

        private static int ResolveHiltSides(
            ProceduralShortSwordDefinition definition)
        {
            return definition.FacetTier switch
            {
                ShortSwordFacetTier.Coarse => 6,
                ShortSwordFacetTier.Intricate => 10,
                _ => 8
            };
        }

        private static Mesh BuildFishtailPommelMesh(
            float top,
            float bottom,
            float radius,
            float connectionRadius,
            ShortSwordFacetTier facetTier)
        {
            float upper = Mathf.Lerp(top, bottom, 0.24f);
            float lower = Mathf.Lerp(top, bottom, 0.78f);
            float notch = Mathf.Lerp(lower, bottom, 0.36f);
            List<Vector2> outline;
            if (facetTier == ShortSwordFacetTier.Coarse)
            {
                outline = new List<Vector2>
                {
                    new Vector2(-connectionRadius, top),
                    new Vector2(connectionRadius, top),
                    new Vector2(radius * 1.12f, lower),
                    new Vector2(radius * 0.58f, bottom),
                    new Vector2(0f, notch),
                    new Vector2(-radius * 0.58f, bottom),
                    new Vector2(-radius * 1.12f, lower)
                };
            }
            else if (facetTier == ShortSwordFacetTier.Intricate)
            {
                float shoulder = Mathf.Lerp(top, upper, 0.48f);
                float flare = Mathf.Lerp(upper, lower, 0.52f);
                outline = new List<Vector2>
                {
                    new Vector2(-connectionRadius, top),
                    new Vector2(connectionRadius, top),
                    new Vector2(radius * 0.48f, shoulder),
                    new Vector2(radius * 0.72f, upper),
                    new Vector2(radius * 0.98f, flare),
                    new Vector2(radius * 1.16f, lower),
                    new Vector2(radius * 0.62f, bottom),
                    new Vector2(0f, notch),
                    new Vector2(-radius * 0.62f, bottom),
                    new Vector2(-radius * 1.16f, lower),
                    new Vector2(-radius * 0.98f, flare),
                    new Vector2(-radius * 0.72f, upper),
                    new Vector2(-radius * 0.48f, shoulder)
                };
            }
            else
            {
                outline = new List<Vector2>
                {
                    new Vector2(-connectionRadius, top),
                    new Vector2(connectionRadius, top),
                    new Vector2(radius * 0.72f, upper),
                    new Vector2(radius * 1.16f, lower),
                    new Vector2(radius * 0.62f, bottom),
                    new Vector2(0f, notch),
                    new Vector2(-radius * 0.62f, bottom),
                    new Vector2(-radius * 1.16f, lower),
                    new Vector2(-radius * 0.72f, upper)
                };
            }
            return BuildExtrudedPolygon(outline, radius * 1.18f);
        }

        private static Mesh BuildRingPommelMesh(
            float top,
            float bottom,
            float radius,
            float connectionRadius,
            ShortSwordFacetTier facetTier)
        {
            int ringSegments = facetTier switch
            {
                ShortSwordFacetTier.Coarse => 8,
                ShortSwordFacetTier.Intricate => 12,
                _ => 10
            };
            int tubeSides = facetTier == ShortSwordFacetTier.Intricate
                ? 6
                : 4;
            float centerY = Mathf.Lerp(top, bottom, 0.68f);
            float ringRadius = Mathf.Min(
                radius * 0.72f,
                Mathf.Abs(centerY - bottom) * 0.82f);
            float tubeRadius = Mathf.Max(0.006f, radius * 0.20f);
            var vertices = new List<Vector3>(
                ringSegments * tubeSides + 8);
            var triangles = new List<int>(
                ringSegments * tubeSides * 6 + 36);
            for (int segment = 0; segment < ringSegments; segment++)
            {
                float angle = segment / (float)ringSegments * Mathf.PI * 2f;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f);
                Vector3 center = new Vector3(
                    radial.x * ringRadius,
                    centerY + radial.y * ringRadius,
                    0f);
                for (int side = 0; side < tubeSides; side++)
                {
                    float tubeAngle = side / (float)tubeSides * Mathf.PI * 2f;
                    vertices.Add(
                        center +
                        radial * (Mathf.Cos(tubeAngle) * tubeRadius) +
                        Vector3.forward *
                            (Mathf.Sin(tubeAngle) * tubeRadius));
                }
            }
            for (int segment = 0; segment < ringSegments; segment++)
            {
                int nextSegment = (segment + 1) % ringSegments;
                for (int side = 0; side < tubeSides; side++)
                {
                    int nextSide = (side + 1) % tubeSides;
                    AddQuad(
                        triangles,
                        segment * tubeSides + side,
                        nextSegment * tubeSides + side,
                        nextSegment * tubeSides + nextSide,
                        segment * tubeSides + nextSide);
                }
            }

            int connectorStart = vertices.Count;
            float connectorBottom = centerY + ringRadius * 0.78f;
            vertices.Add(new Vector3(-connectionRadius, top, tubeRadius));
            vertices.Add(new Vector3(connectionRadius, top, tubeRadius));
            vertices.Add(new Vector3(connectionRadius, connectorBottom, tubeRadius));
            vertices.Add(new Vector3(-connectionRadius, connectorBottom, tubeRadius));
            vertices.Add(new Vector3(-connectionRadius, top, -tubeRadius));
            vertices.Add(new Vector3(connectionRadius, top, -tubeRadius));
            vertices.Add(new Vector3(connectionRadius, connectorBottom, -tubeRadius));
            vertices.Add(new Vector3(-connectionRadius, connectorBottom, -tubeRadius));
            AddBoxTriangles(triangles, connectorStart);
            return CreateMesh(vertices, triangles);
        }

        private static void AddBoxTriangles(List<int> triangles, int start)
        {
            AddQuad(triangles, start, start + 3, start + 2, start + 1);
            AddQuad(triangles, start + 4, start + 5, start + 6, start + 7);
            AddQuad(triangles, start, start + 4, start + 7, start + 3);
            AddQuad(triangles, start + 1, start + 2, start + 6, start + 5);
            AddQuad(triangles, start, start + 1, start + 5, start + 4);
            AddQuad(triangles, start + 3, start + 7, start + 6, start + 2);
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
                ShortSwordHandleProfile.PalmSwell => 0.94f,
                ShortSwordHandleProfile.FlaredEnds => 1.10f,
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
            float height,
            float depthScale = 1f,
            int sides = 8)
        {
            float halfHeight = height * 0.5f;
            var rings = new List<Vector2>
            {
                new Vector2(centerY + halfHeight, radius * 0.92f),
                new Vector2(centerY + halfHeight * 0.64f, radius),
                new Vector2(centerY - halfHeight * 0.64f, radius),
                new Vector2(centerY - halfHeight, radius * 0.92f)
            };
            var centers = new Vector2[rings.Count];
            return BuildRevolvedMesh(
                rings,
                centers,
                Mathf.Clamp(sides, 6, 10),
                null,
                depthScale);
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
                if (facing > 0f)
                {
                    AddQuad(
                        triangles,
                        index,
                        count + index,
                        count + next,
                        next);
                    triangles.Add(tableCenter);
                    triangles.Add(count + next);
                    triangles.Add(count + index);
                    triangles.Add(backCenter);
                    triangles.Add(index);
                    triangles.Add(next);
                }
                else
                {
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
            }
            return CreateMesh(vertices, triangles);
        }

        private static Mesh BuildHelixMesh(
            ProceduralShortSwordDefinition definition,
            bool clockwise,
            float turns,
            float thicknessScale,
            float radialOffset = 0.0035f,
            float phaseOffset = 0f,
            bool alternatingWeave = false,
            int weaveStrand = 0,
            float weavePairPhaseOffset = 0f)
        {
            int sampleCount = definition.FacetTier switch
            {
                ShortSwordFacetTier.Coarse => 23,
                ShortSwordFacetTier.Intricate => 35,
                _ => 27
            };
            int sides = definition.FacetTier == ShortSwordFacetTier.Intricate
                ? 6
                : 4;
            float top = ResolveHandleSeatHeight(definition) - 0.016f;
            float bottom = -definition.HandleLength + 0.020f;
            float direction = clockwise ? 1f : -1f;
            float phase = (clockwise ? 0f : Mathf.PI) + phaseOffset;
            List<float> sampleParameters = BuildHelixSampleParameters(
                definition,
                sampleCount,
                turns,
                thicknessScale,
                radialOffset,
                alternatingWeave,
                weavePairPhaseOffset);
            sampleCount = sampleParameters.Count;
            var centers = new List<Vector3>(sampleCount);
            for (int index = 0; index < sampleCount; index++)
            {
                float t = sampleParameters[index];
                float angle = phase + direction * t * turns * Mathf.PI * 2f;
                float surfaceRadius = ResolveHandleSurfaceRadius(
                    definition,
                    t);
                float cordRadius = ResolveHelixCordRadius(
                    definition,
                    t,
                    thicknessScale);
                float basePathRadius = ResolveHandleDecorationRadius(
                        definition,
                        t,
                        angle,
                        surfaceRadius) +
                    radialOffset;
                float pairedAngle = clockwise
                    ? Mathf.PI + weavePairPhaseOffset -
                        t * turns * Mathf.PI * 2f
                    : t * turns * Mathf.PI * 2f;
                float pairedBasePathRadius = ResolveHandleDecorationRadius(
                        definition,
                        t,
                        pairedAngle,
                        surfaceRadius) +
                    radialOffset;
                float weaveLift = alternatingWeave
                    ? ResolveAlternatingWeaveLift(
                        t,
                        turns,
                        weaveStrand,
                        weavePairPhaseOffset,
                        cordRadius,
                        basePathRadius,
                        pairedBasePathRadius)
                    : 0f;
                float pathRadius = basePathRadius + weaveLift;
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
                float t = sampleParameters[index];
                float cordRadius = ResolveHelixCordRadius(
                    definition,
                    t,
                    thicknessScale);
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

        private static List<float> BuildHelixSampleParameters(
            ProceduralShortSwordDefinition definition,
            int baseSampleCount,
            float turns,
            float thicknessScale,
            float radialOffset,
            bool alternatingWeave,
            float pairPhaseOffset)
        {
            var parameters = new List<float>(baseSampleCount + 32);
            for (int index = 0; index < baseSampleCount; index++)
            {
                parameters.Add(index / (baseSampleCount - 1f));
            }
            if (!alternatingWeave || turns <= 0f)
            {
                return parameters;
            }

            float startDifference = -(Mathf.PI + pairPhaseOffset);
            float relativeTravel = turns * Mathf.PI * 4f;
            int firstCrossing = Mathf.CeilToInt(
                startDifference / (Mathf.PI * 2f));
            int lastCrossing = Mathf.FloorToInt(
                (startDifference + relativeTravel) / (Mathf.PI * 2f));
            for (int crossing = firstCrossing;
                 crossing <= lastCrossing;
                 crossing++)
            {
                float crossingT =
                    (crossing * Mathf.PI * 2f - startDifference) /
                    relativeTravel;
                if (crossingT < 0f || crossingT > 1f)
                {
                    continue;
                }

                float angle = crossingT * turns * Mathf.PI * 2f;
                float surfaceRadius = ResolveHandleSurfaceRadius(
                    definition,
                    crossingT);
                float basePathRadius = ResolveHandleDecorationRadius(
                        definition,
                        crossingT,
                        angle,
                        surfaceRadius) +
                    radialOffset;
                float cordRadius = ResolveHelixCordRadius(
                    definition,
                    crossingT,
                    thicknessScale);
                float targetClearance = ResolveWovenGripTargetClearance(
                    cordRadius);
                float boundaryAngle = 2f * Mathf.Asin(Mathf.Clamp01(
                    targetClearance /
                    Mathf.Max(0.0001f, basePathRadius * 2f)));
                float boundaryT = boundaryAngle / relativeTravel;

                AddUniqueSample(parameters, crossingT - boundaryT);
                AddUniqueSample(parameters, crossingT - boundaryT * 0.5f);
                AddUniqueSample(parameters, crossingT);
                AddUniqueSample(parameters, crossingT + boundaryT * 0.5f);
                AddUniqueSample(parameters, crossingT + boundaryT);
            }
            parameters.Sort();
            return parameters;
        }

        private static void AddUniqueSample(
            List<float> parameters,
            float parameter)
        {
            if (parameter < 0f || parameter > 1f)
            {
                return;
            }
            for (int index = 0; index < parameters.Count; index++)
            {
                if (Mathf.Abs(parameters[index] - parameter) < 0.000001f)
                {
                    return;
                }
            }
            parameters.Add(parameter);
        }

        private static float ResolveHelixCordRadius(
            ProceduralShortSwordDefinition definition,
            float normalizedHeight,
            float thicknessScale)
        {
            return Mathf.Clamp(
                ResolveHandleSurfaceRadius(definition, normalizedHeight) *
                    0.105f * thicknessScale,
                0.0015f,
                0.0058f);
        }

        private static float ResolveAlternatingWeaveLift(
            float normalizedHeight,
            float turns,
            int weaveStrand,
            float pairPhaseOffset,
            float cordRadius,
            float basePathRadius,
            float pairedBasePathRadius)
        {
            // The paired helices cross whenever their unwrapped angular
            // difference reaches a multiple of two PI. Only that crossing's
            // over-strand is raised; every other point stays seated unless its
            // angular chord is too short to clear both faceted cord tubes.
            float unwrappedDifference =
                -(Mathf.PI + pairPhaseOffset) +
                normalizedHeight * turns * Mathf.PI * 4f;
            int crossingIndex = Mathf.RoundToInt(
                unwrappedDifference / (Mathf.PI * 2f));
            int raisedStrand = Mathf.Abs(crossingIndex % 2);
            if (weaveStrand != raisedStrand)
            {
                return 0f;
            }

            float wrappedDifference = unwrappedDifference -
                crossingIndex * Mathf.PI * 2f;
            float radius = Mathf.Max(0.0001f, basePathRadius);
            float pairedRadius = Mathf.Max(0.0001f, pairedBasePathRadius);
            float targetClearance = ResolveWovenGripTargetClearance(cordRadius);
            float cosine = Mathf.Cos(wrappedDifference);
            float sine = Mathf.Sin(wrappedDifference);
            float seatedDistanceSquared =
                radius * radius +
                pairedRadius * pairedRadius -
                2f * radius * pairedRadius * cosine;
            if (seatedDistanceSquared >= targetClearance * targetClearance)
            {
                return 0f;
            }

            // Intersect the raised strand's radial ray with a clearance circle
            // around the seated strand. The farther intersection is the
            // smallest outward-only displacement that reaches the target.
            float radicand = targetClearance * targetClearance -
                pairedRadius * pairedRadius * sine * sine;
            float requiredRadius = pairedRadius * cosine +
                Mathf.Sqrt(Mathf.Max(0f, radicand));
            return Mathf.Max(
                0f,
                requiredRadius - radius);
        }

        private static float ResolveWovenGripTargetClearance(float cordRadius)
        {
            return cordRadius * 2f +
                WovenGripAirGap +
                WovenGripLowPolyAllowance;
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
                ShortSwordHandleProfile.PalmSwell =>
                    0.94f + Mathf.Sin(t * Mathf.PI) * 0.16f,
                ShortSwordHandleProfile.FlaredEnds =>
                    0.90f + Mathf.Abs(t - 0.5f) * 0.40f,
                _ => 1f
            };
            bool ringRelief = definition.GripStyle is
                ShortSwordGripStyle.LeatherBands or
                ShortSwordGripStyle.RibbedWood or
                ShortSwordGripStyle.FacetedLeather;
            float wrapRelief = ringRelief && ringIndex > 0 && ringIndex < 6
                ? (ringIndex % 2 == 0 ? 1.035f : 0.985f)
                : 1f;
            return definition.HandleRadius * profile * wrapRelief;
        }

        private static float ResolveHandleDepthScale(
            ProceduralShortSwordDefinition definition)
        {
            return definition.HandleCrossSection ==
                    ShortSwordHandleCrossSection.OvalFaceted
                ? 0.76f
                : 1f;
        }

        private static int ResolveHandleCrossSectionSides(
            ProceduralShortSwordDefinition definition)
        {
            return definition.HandleCrossSection switch
            {
                ShortSwordHandleCrossSection.Hexagonal => 6,
                ShortSwordHandleCrossSection.Decagonal => 10,
                ShortSwordHandleCrossSection.OvalFaceted => 8,
                _ => 8
            };
        }

        private static float ResolveHandleDecorationRadius(
            ProceduralShortSwordDefinition definition,
            float normalizedHeight,
            float angle,
            float surfaceRadius = -1f)
        {
            float radius = surfaceRadius > 0f
                ? surfaceRadius
                : ResolveHandleSurfaceRadius(definition, normalizedHeight);
            float depthScale = ResolveHandleDepthScale(definition);
            if (Mathf.Approximately(depthScale, 1f))
            {
                return radius;
            }
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            float denominator = Mathf.Sqrt(
                depthScale * depthScale * cosine * cosine +
                sine * sine);
            return radius * depthScale / Mathf.Max(0.0001f, denominator);
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
            var faceTriangles = new List<int>((count - 2) * 3);
            TriangulatePolygon(outline, faceTriangles);
            bool outlineClockwise = SignedPolygonArea(outline) < 0f;
            for (int index = 0; index < faceTriangles.Count; index += 3)
            {
                int a = faceTriangles[index];
                int b = faceTriangles[index + 1];
                int c = faceTriangles[index + 2];
                triangles.Add(a);
                triangles.Add(outlineClockwise ? c : b);
                triangles.Add(outlineClockwise ? b : c);
                triangles.Add(count + a);
                triangles.Add(count + (outlineClockwise ? b : c));
                triangles.Add(count + (outlineClockwise ? c : b));
            }
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                if (outlineClockwise)
                {
                    AddQuad(
                        triangles,
                        index,
                        next,
                        count + next,
                        count + index);
                }
                else
                {
                    AddQuad(
                        triangles,
                        index,
                        count + index,
                        count + next,
                        next);
                }
            }
            return CreateMesh(vertices, triangles);
        }

        private static void TriangulatePolygon(
            IReadOnlyList<Vector2> outline,
            List<int> triangles)
        {
            var remaining = new List<int>(outline.Count);
            for (int index = 0; index < outline.Count; index++)
            {
                remaining.Add(index);
            }
            bool clockwise = SignedPolygonArea(outline) < 0f;
            int safety = outline.Count * outline.Count;
            while (remaining.Count > 3 && safety-- > 0)
            {
                bool clipped = false;
                for (int index = 0; index < remaining.Count; index++)
                {
                    int previous = remaining[
                        (index - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[index];
                    int next = remaining[(index + 1) % remaining.Count];
                    float corner = Cross2D(
                        outline[current] - outline[previous],
                        outline[next] - outline[current]);
                    if (clockwise ? corner >= -0.0000001f : corner <= 0.0000001f)
                    {
                        continue;
                    }

                    bool containsPoint = false;
                    for (int candidateIndex = 0;
                         candidateIndex < remaining.Count;
                         candidateIndex++)
                    {
                        int candidate = remaining[candidateIndex];
                        if (candidate == previous ||
                            candidate == current ||
                            candidate == next)
                        {
                            continue;
                        }
                        if (PointInsideTriangle(
                                outline[candidate],
                                outline[previous],
                                outline[current],
                                outline[next]))
                        {
                            containsPoint = true;
                            break;
                        }
                    }
                    if (containsPoint)
                    {
                        continue;
                    }

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    remaining.RemoveAt(index);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    break;
                }
            }
            if (remaining.Count == 3)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                triangles.Add(remaining[2]);
            }
        }

        private static float SignedPolygonArea(
            IReadOnlyList<Vector2> outline)
        {
            float twiceArea = 0f;
            for (int index = 0; index < outline.Count; index++)
            {
                Vector2 current = outline[index];
                Vector2 next = outline[(index + 1) % outline.Count];
                twiceArea += current.x * next.y - next.x * current.y;
            }
            return twiceArea * 0.5f;
        }

        private static float Cross2D(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static bool PointInsideTriangle(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            float first = Cross2D(b - a, point - a);
            float second = Cross2D(c - b, point - b);
            float third = Cross2D(a - c, point - c);
            bool hasNegative = first < -0.0000001f ||
                second < -0.0000001f ||
                third < -0.0000001f;
            bool hasPositive = first > 0.0000001f ||
                second > 0.0000001f ||
                third > 0.0000001f;
            return !(hasNegative && hasPositive);
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
            ProceduralShortSwordDefinition topSeatDefinition,
            float depthScale = 1f)
        {
            var centers = new Vector2[rings.Count];
            return BuildRevolvedMesh(
                rings,
                centers,
                sides,
                topSeatDefinition,
                depthScale);
        }

        private static Mesh BuildRevolvedMesh(
            IReadOnlyList<Vector2> rings,
            IReadOnlyList<Vector2> centers,
            int sides,
            ProceduralShortSwordDefinition? topSeatDefinition = null,
            float depthScale = 1f)
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
                            Mathf.Sin(angle) * rings[ring].y * depthScale));
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
                float squaredMagnitude = cross.sqrMagnitude;
                if (squaredMagnitude <= 0.000000000001f ||
                    float.IsNaN(squaredMagnitude) ||
                    float.IsInfinity(squaredMagnitude))
                {
                    continue;
                }
                // Vector3.normalized returns Vector3.zero below Unity's
                // 1e-5 magnitude cutoff. Several legitimate fine grip/cord
                // faces are smaller than that but larger than our degeneracy
                // threshold, so they previously reached URP Lit with a zero
                // normal. GPU normalization of that value can yield NaN/Inf
                // lighting and an intermittent HDR flash. Normalize the
                // already-validated cross product explicitly instead.
                Vector3 normal = cross / Mathf.Sqrt(squaredMagnitude);
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
