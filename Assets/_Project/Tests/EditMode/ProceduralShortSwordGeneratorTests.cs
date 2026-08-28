using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests
{
    public sealed class ProceduralShortSwordGeneratorTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void SameSeedProducesTheSameCompleteDefinition()
        {
            ProceduralShortSwordDefinition first =
                ProceduralShortSwordGenerator.CreateDefinition(4021);
            ProceduralShortSwordDefinition second =
                ProceduralShortSwordGenerator.CreateDefinition(4021);

            Assert.That(
                JsonUtility.ToJson(second),
                Is.EqualTo(JsonUtility.ToJson(first)),
                "The full serialized definition must remain deterministic, " +
                "including every newly added branch field and proportion.");

            var constraints =
                new ProceduralShortSwordGenerationConstraints();
            constraints.Toggle(
                ShortSwordGenerationDecision.Family,
                (int)ShortSwordFamily.Cruciform);
            constraints.Toggle(
                ShortSwordGenerationDecision.HeroZone,
                (int)ShortSwordHeroZone.Blade);
            constraints.Toggle(
                ShortSwordGenerationDecision.BladeBaseStyle,
                (int)ShortSwordBladeBaseStyle.NarrowRicasso);
            ProceduralShortSwordDefinition lockedFirst =
                ProceduralShortSwordGenerator.CreateDefinition(
                    4021,
                    constraints);
            ProceduralShortSwordDefinition lockedSecond =
                ProceduralShortSwordGenerator.CreateDefinition(
                    4021,
                    constraints);
            Assert.That(
                JsonUtility.ToJson(lockedSecond),
                Is.EqualTo(JsonUtility.ToJson(lockedFirst)),
                "A complete locked branch path must also be deterministic.");
        }

        [Test]
        public void CombatProfileIsDeterministicAndLivesInTheSeededDefinition()
        {
            ProceduralShortSwordDefinition first =
                ProceduralShortSwordGenerator.CreateDefinition(91823);
            ProceduralShortSwordDefinition second =
                ProceduralShortSwordGenerator.CreateDefinition(91823);

            Assert.That(first.CombatProfile.IsValid, Is.True);
            Assert.That(
                JsonUtility.ToJson(second.CombatProfile),
                Is.EqualTo(JsonUtility.ToJson(first.CombatProfile)));
            Assert.That(
                JsonUtility.ToJson(
                    ProceduralShortSwordGenerator.CalculateCombatProfile(first)),
                Is.EqualTo(JsonUtility.ToJson(first.CombatProfile)));
        }

        [Test]
        public void PhysicalMassTradesAttackRateForImpactAndStagger()
        {
            ProceduralShortSwordDefinition light =
                ProceduralShortSwordGenerator.CreateDefinition(7712);
            light.Family = ShortSwordFamily.Piercer;
            light.BladeSectionStyle =
                ShortSwordBladeSectionStyle.FlatBevel;
            light.BladeLength = 0.94f;
            light.BladeWidth = 0.074f;
            light.BladeThickness = 0.026f;
            light.GuardSpan = 0.255f;
            light.GuardDepth = 0.037f;
            light.HiltRadius = 0.037f;
            light.HandleLength = 0.258f;
            ShortSwordCombatProfile lightFeel =
                ProceduralShortSwordGenerator.CalculateCombatProfile(light);

            ProceduralShortSwordDefinition heavy = light;
            heavy.Family = ShortSwordFamily.Legionary;
            heavy.BladeSectionStyle =
                ShortSwordBladeSectionStyle.BroadMidrib;
            heavy.BladeLength = 1.08f;
            heavy.BladeWidth = 0.112f;
            heavy.BladeThickness = 0.034f;
            heavy.GuardSpan = 0.362f;
            heavy.GuardDepth = 0.053f;
            heavy.HiltRadius = 0.053f;
            heavy.HandleLength = 0.205f;
            ShortSwordCombatProfile heavyFeel =
                ProceduralShortSwordGenerator.CalculateCombatProfile(heavy);

            Assert.That(heavyFeel.Heft, Is.GreaterThan(lightFeel.Heft));
            Assert.That(
                lightFeel.AttackSpeedMultiplier -
                    heavyFeel.AttackSpeedMultiplier,
                Is.GreaterThan(0.40f),
                "The fastest and slowest physical swords must have an " +
                "immediately obvious cadence difference.");
            Assert.That(
                heavyFeel.HitPauseDuration - lightFeel.HitPauseDuration,
                Is.GreaterThan(0.10f));
            Assert.That(
                heavyFeel.StaggerDuration - lightFeel.StaggerDuration,
                Is.GreaterThan(0.38f));
            Assert.That(
                heavyFeel.ImpactShakeMultiplier -
                    lightFeel.ImpactShakeMultiplier,
                Is.GreaterThan(1.40f));
            Assert.That(
                heavyFeel.DamageMultiplier - lightFeel.DamageMultiplier,
                Is.GreaterThan(0.50f));
            Assert.That(
                lightFeel.SwingPitchMultiplier -
                    heavyFeel.SwingPitchMultiplier,
                Is.GreaterThan(0.40f));
            Assert.That(
                heavyFeel.SwingVolumeMultiplier -
                    lightFeel.SwingVolumeMultiplier,
                Is.GreaterThan(0.45f));
            Assert.That(
                lightFeel.TrailPersistenceMultiplier -
                    heavyFeel.TrailPersistenceMultiplier,
                Is.GreaterThan(0.75f));
            Assert.That(
                lightFeel.TrailOpacityMultiplier -
                    heavyFeel.TrailOpacityMultiplier,
                Is.GreaterThan(0.40f));
        }

        [Test]
        public void GeneratedPopulationHasBroadCombatFeelSpread()
        {
            float minimumDamage = float.PositiveInfinity;
            float maximumDamage = float.NegativeInfinity;
            float minimumSpeed = float.PositiveInfinity;
            float maximumSpeed = float.NegativeInfinity;
            float minimumPause = float.PositiveInfinity;
            float maximumPause = float.NegativeInfinity;
            float minimumImpact = float.PositiveInfinity;
            float maximumImpact = float.NegativeInfinity;

            for (int seed = 1; seed <= 2000; seed++)
            {
                ShortSwordCombatProfile profile =
                    ProceduralShortSwordGenerator.CreateDefinition(seed).
                        CombatProfile;
                minimumDamage = Mathf.Min(
                    minimumDamage,
                    profile.DamageMultiplier);
                maximumDamage = Mathf.Max(
                    maximumDamage,
                    profile.DamageMultiplier);
                minimumSpeed = Mathf.Min(
                    minimumSpeed,
                    profile.AttackSpeedMultiplier);
                maximumSpeed = Mathf.Max(
                    maximumSpeed,
                    profile.AttackSpeedMultiplier);
                minimumPause = Mathf.Min(
                    minimumPause,
                    profile.HitPauseDuration);
                maximumPause = Mathf.Max(
                    maximumPause,
                    profile.HitPauseDuration);
                minimumImpact = Mathf.Min(
                    minimumImpact,
                    profile.ImpactShakeMultiplier);
                maximumImpact = Mathf.Max(
                    maximumImpact,
                    profile.ImpactShakeMultiplier);
            }

            Assert.That(maximumDamage - minimumDamage, Is.GreaterThan(0.30f));
            Assert.That(maximumSpeed - minimumSpeed, Is.GreaterThan(0.30f));
            Assert.That(maximumPause - minimumPause, Is.GreaterThan(0.06f));
            Assert.That(maximumImpact - minimumImpact, Is.GreaterThan(0.90f));
        }

        [Test]
        public void GemOrnamentRaisesHiddenQualityWithoutRemovingPhysicalIdentity()
        {
            ProceduralShortSwordDefinition plain = default;
            ShortSwordCombatProfile plainFeel = default;
            for (int seed = 1; seed <= 1000; seed++)
            {
                plain = ProceduralShortSwordGenerator.CreateDefinition(seed);
                plain.OrnamentStyle = ShortSwordOrnamentStyle.Plain;
                plainFeel =
                    ProceduralShortSwordGenerator.CalculateCombatProfile(plain);
                if (plainFeel.CraftQuality >= 0.55f &&
                    plainFeel.CraftQuality <= 0.80f)
                {
                    break;
                }
            }

            ProceduralShortSwordDefinition gemmed = plain;
            gemmed.OrnamentStyle = ShortSwordOrnamentStyle.GuardGem;
            ShortSwordCombatProfile gemmedFeel =
                ProceduralShortSwordGenerator.CalculateCombatProfile(gemmed);

            Assert.That(
                gemmedFeel.CraftQuality,
                Is.GreaterThan(plainFeel.CraftQuality));
            Assert.That(
                gemmedFeel.AttackSpeedMultiplier,
                Is.GreaterThan(plainFeel.AttackSpeedMultiplier));
            Assert.That(
                gemmedFeel.DamageMultiplier,
                Is.GreaterThan(plainFeel.DamageMultiplier));
            Assert.That(
                gemmedFeel.Heft,
                Is.EqualTo(plainFeel.Heft).Within(0.00001f),
                "Quality can refine a sword, but ornament must not rewrite " +
                "the mass implied by its physical construction.");
        }

        [Test]
        public void UnrestrictedWorldGenerationCanEmitEveryFamilyDespiteLabLocks()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.ToggleGenerationLock(
                ShortSwordGenerationDecision.Family,
                (int)ShortSwordFamily.Cruciform);

            var examples = new Dictionary<
                ShortSwordFamily,
                ProceduralShortSwordDefinition>();
            for (int seed = 1; seed <= 20000 && examples.Count < 8; seed++)
            {
                ProceduralShortSwordDefinition sword =
                    ProceduralShortSwordGenerator.CreateDefinition(seed);
                examples.TryAdd(sword.Family, sword);
            }
            ShortSwordFamily[] families =
                ShortSwordGenerationBranchCatalog.Families.ToArray();
            Assert.That(examples.Keys, Is.EquivalentTo(families));

            foreach (ShortSwordFamily family in families)
            {
                ProceduralShortSwordDefinition expected = examples[family];
                ProceduralShortSwordDefinition actual =
                    generator.GenerateUnrestricted(expected.Seed);
                Assert.That(actual.Family, Is.EqualTo(family));
                Assert.That(
                    JsonUtility.ToJson(actual),
                    Is.EqualTo(JsonUtility.ToJson(expected)),
                    $"World spawning narrowed or changed the {family} seed.");
                Assert.That(
                    generator.IsGenerationLocked(
                        ShortSwordGenerationDecision.Family,
                        (int)ShortSwordFamily.Cruciform),
                    Is.True,
                    "World generation must ignore lab locks without deleting " +
                    "the designer's lab setup.");
            }
        }

        [Test]
        public void ProceduralPaletteNeutralizesLegacyBrownGripAlbedo()
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Simple Lit");
            Assert.That(shader, Is.Not.Null);
            var legacyMaterial = new Material(shader);
            var brownTexture = new Texture2D(1, 1);
            brownTexture.SetPixel(0, 0, new Color(0.24f, 0.10f, 0.035f));
            brownTexture.Apply();
            try
            {
                legacyMaterial.SetTexture("_BaseMap", brownTexture);
                legacyMaterial.SetTexture("_MainTex", brownTexture);
                legacyMaterial.SetColor(
                    "_EmissionColor",
                    Color.white * 24f);
                legacyMaterial.SetTexture(
                    "_EmissionMap",
                    Texture2D.whiteTexture);
                legacyMaterial.SetColor("_SpecColor", Color.white);
                legacyMaterial.SetTexture(
                    "_SpecGlossMap",
                    Texture2D.whiteTexture);
                legacyMaterial.EnableKeyword("_SPECGLOSSMAP");
                legacyMaterial.EnableKeyword(
                    "_GLOSSINESS_FROM_BASE_ALPHA");
                ProceduralShortSwordGenerator generator = CreateGenerator();
                generator.ConfigureMaterials(
                    legacyMaterial,
                    legacyMaterial,
                    legacyMaterial,
                    legacyMaterial,
                    useProceduralPalette: true);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.GripColor,
                    (int)ShortSwordGripColor.Navy);
                ProceduralShortSwordDefinition sword = generator.Generate(8917);
                Renderer handle = generator.GeneratedParts.Single(part =>
                        part.name ==
                            ProceduralShortSwordGenerator.HandlePartName)
                    .GetComponent<Renderer>();
                Assert.That(
                    handle.sharedMaterial,
                    Is.Not.SameAs(legacyMaterial),
                    "World swords need a private sanitized material because " +
                    "shader keywords cannot be overridden per renderer.");
                Assert.That(
                    handle.sharedMaterial.IsKeywordEnabled(
                        "_SPECULARHIGHLIGHTS_OFF"),
                    Is.True,
                    "Hard-faced world swords must not re-enable the " +
                    "view-dependent highlight that produced raid flashes.");
                Assert.That(
                    handle.sharedMaterial.IsKeywordEnabled(
                        "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                    Is.False,
                    "A white base-map alpha must never override the bounded " +
                    "world-sword smoothness.");
                Assert.That(
                    handle.sharedMaterial.IsKeywordEnabled(
                        "_METALLICSPECGLOSSMAP"),
                    Is.False,
                    "World swords must not inherit texture-driven gloss from " +
                    "their source material.");
                Assert.That(
                    handle.sharedMaterial.IsKeywordEnabled(
                        "_ENVIRONMENTREFLECTIONS_OFF"),
                    Is.True,
                    "World swords retain Lit diffuse/probe lighting without " +
                    "sampling a bright reflection direction.");
                Assert.That(
                    handle.sharedMaterial.IsKeywordEnabled("_EMISSION"),
                    Is.False);
                Assert.That(
                    handle.sharedMaterial.IsKeywordEnabled(
                        "_SPECULAR_SETUP"),
                    Is.False);
                Assert.That(
                    handle.sharedMaterial.shader.name,
                    Is.EqualTo(
                        ProceduralShortSwordGenerator.WorldShaderName),
                    "World swords should use the same URP Lit path as other models.");
                Assert.That(
                    handle.sharedMaterial.renderQueue,
                    Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
                Assert.That(
                    handle.sharedMaterial.GetFloat("_Metallic"),
                    Is.EqualTo(
                        ProceduralShortSwordGenerator.WorldSwordMetallic));
                Assert.That(
                    handle.sharedMaterial.GetFloat("_Smoothness"),
                    Is.EqualTo(
                        ProceduralShortSwordGenerator.WorldSwordSmoothness));
                Assert.That(
                    handle.sharedMaterial.GetFloat("_WorkflowMode"),
                    Is.EqualTo(1f));
                if (handle.sharedMaterial.HasProperty("_BaseMap"))
                {
                    Assert.That(
                        handle.sharedMaterial.GetTexture("_BaseMap"),
                        Is.SameAs(Texture2D.whiteTexture));
                }
                if (handle.sharedMaterial.HasProperty("_MetallicGlossMap"))
                {
                    Assert.That(
                        handle.sharedMaterial.GetTexture("_MetallicGlossMap"),
                        Is.Null,
                        "No source metallic-gloss map may override the " +
                        "controlled world-sword material.");
                }
                if (handle.sharedMaterial.HasProperty("_SpecGlossMap"))
                {
                    Assert.That(
                        handle.sharedMaterial.GetTexture("_SpecGlossMap"),
                        Is.Null,
                        "The Simple Lit spec-gloss source must not reach the " +
                        "controlled URP Lit sword.");
                }
                Assert.That(
                    handle.reflectionProbeUsage,
                    Is.EqualTo(
                        UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes));
                Assert.That(
                    handle.shadowCastingMode,
                    Is.EqualTo(
                        UnityEngine.Rendering.ShadowCastingMode.On));
                Assert.That(handle.receiveShadows, Is.True);
                Assert.That(
                    handle.lightProbeUsage,
                    Is.EqualTo(
                        UnityEngine.Rendering.LightProbeUsage.BlendProbes));
                var properties = new MaterialPropertyBlock();
                handle.GetPropertyBlock(properties);

                Assert.That(
                    properties.GetTexture("_BaseMap"),
                    Is.SameAs(Texture2D.whiteTexture));
                Assert.That(
                    properties.GetTexture("_MainTex"),
                    Is.SameAs(Texture2D.whiteTexture));
                Color expectedGripColor =
                    ProceduralShortSwordGenerator.ResolveGripColor(
                        sword.GripColor);
                Color actualGripColor = properties.GetColor("_BaseColor");
                Assert.That(
                    actualGripColor.r,
                    Is.EqualTo(expectedGripColor.r).Within(0.0001f));
                Assert.That(
                    actualGripColor.g,
                    Is.EqualTo(expectedGripColor.g).Within(0.0001f));
                Assert.That(
                    actualGripColor.b,
                    Is.EqualTo(expectedGripColor.b).Within(0.0001f));
                Assert.That(
                    actualGripColor.a,
                    Is.EqualTo(expectedGripColor.a).Within(0.0001f));
                Assert.That(
                    properties.GetColor("_EmissionColor"),
                    Is.EqualTo(Color.black));
                Assert.That(
                    properties.GetTexture("_EmissionMap"),
                    Is.SameAs(Texture2D.blackTexture));
                Assert.That(
                    properties.GetTexture("_BumpMap"),
                    Is.SameAs(Texture2D.normalTexture));
                Assert.That(
                    properties.GetFloat("_ClearCoatMask"),
                    Is.Zero);
                Assert.That(
                    properties.GetFloat("_SpecularHighlights"),
                    Is.Zero);
                Assert.That(
                    properties.GetFloat("_Metallic"),
                    Is.LessThanOrEqualTo(
                        ProceduralShortSwordGenerator.WorldSwordMetallic));
                Assert.That(
                    properties.GetFloat("_Smoothness"),
                    Is.LessThanOrEqualTo(
                        ProceduralShortSwordGenerator.WorldSwordSmoothness));
                Assert.That(
                    properties.GetFloat("_EnvironmentReflections"),
                    Is.Zero);
                foreach (Renderer renderer in generator.GeneratedParts
                             .SelectMany(part =>
                                 part.GetComponentsInChildren<Renderer>(true)))
                {
                    var rendererProperties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(rendererProperties);
                    Assert.That(
                        rendererProperties.GetColor("_EmissionColor"),
                        Is.EqualTo(Color.black),
                        $"{renderer.name} must not inherit emissive source " +
                        "material data.");
                    Assert.That(
                        rendererProperties.GetFloat("_Metallic"),
                        Is.LessThanOrEqualTo(
                            ProceduralShortSwordGenerator.WorldSwordMetallic));
                    Assert.That(
                        rendererProperties.GetFloat("_Smoothness"),
                        Is.LessThanOrEqualTo(
                            ProceduralShortSwordGenerator.WorldSwordSmoothness));
                    Assert.That(
                        rendererProperties.GetFloat("_SpecularHighlights"),
                        Is.Zero);
                    Assert.That(
                        rendererProperties.GetFloat("_EnvironmentReflections"),
                        Is.Zero);
                }
                Assert.That(sword.GripColor, Is.EqualTo(ShortSwordGripColor.Navy));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(brownTexture);
                UnityEngine.Object.DestroyImmediate(legacyMaterial);
            }
        }

        [Test]
        public void CatalogContainsAndGuaranteesEveryDiscreteChoice()
        {
            ShortSwordGenerationBranchGroup[] groups =
                ShortSwordGenerationBranchCatalog.Groups.ToArray();
            ShortSwordGenerationDecision[] decisions =
                (ShortSwordGenerationDecision[])Enum.GetValues(
                    typeof(ShortSwordGenerationDecision));
            ShortSwordFamily[] families =
                ShortSwordGenerationBranchCatalog.Families.ToArray();

            Assert.That(groups, Has.Length.EqualTo(22));
            Assert.That(groups, Has.Length.EqualTo(decisions.Length));
            Assert.That(
                groups.Select(group => group.Decision),
                Is.EquivalentTo(decisions));
            Assert.That(
                groups.Select(group => group.Decision).Distinct().Count(),
                Is.EqualTo(groups.Length));
            Assert.That(families, Has.Length.EqualTo(8));
            Assert.That(
                groups.Single(group =>
                        group.Decision == ShortSwordGenerationDecision.Family)
                    .Options.Select(option => option.Value),
                Is.EquivalentTo(families.Select(family => (int)family)));

            int optionCount = 0;
            foreach (ShortSwordGenerationBranchGroup group in groups)
            {
                Assert.That(group.Options.Count, Is.GreaterThan(0));
                Assert.That(
                    group.Options.Select(option => option.Value)
                        .Distinct().Count(),
                    Is.EqualTo(group.Options.Count),
                    $"{group.Decision} contains a duplicate lock value.");
                foreach (ShortSwordGenerationBranchOption option in
                         group.Options)
                {
                    optionCount++;
                    var constraints =
                        new ProceduralShortSwordGenerationConstraints();
                    Assert.That(
                        constraints.Toggle(group.Decision, option.Value),
                        Is.True,
                        $"{group.Decision} / {option.Label} was rejected.");
                    Assert.That(constraints.ActiveLockCount, Is.EqualTo(1));

                    for (int sample = 0; sample < 6; sample++)
                    {
                        ProceduralShortSwordDefinition sword =
                            ProceduralShortSwordGenerator.CreateDefinition(
                                41000 + optionCount * 17 + sample,
                                constraints);
                        Assert.That(
                            ShortSwordGenerationBranchCatalog.TryReadValue(
                                sword,
                                group.Decision,
                                out int generatedValue),
                            Is.True);
                        Assert.That(
                            generatedValue,
                            Is.EqualTo(option.Value),
                            $"{group.Decision} / {option.Label} was not " +
                            $"guaranteed for seed {sword.Seed}.");
                    }
                }
            }

            Assert.That(optionCount, Is.GreaterThan(100));
        }

        [Test]
        public void EveryLockedFamilyOnlyEmitsCompatibleBranches()
        {
            ShortSwordFamily[] families =
                ShortSwordGenerationBranchCatalog.Families.ToArray();
            foreach (ShortSwordFamily family in families)
            {
                var constraints =
                    new ProceduralShortSwordGenerationConstraints();
                constraints.Toggle(
                    ShortSwordGenerationDecision.Family,
                    (int)family);
                for (int sample = 0; sample < 48; sample++)
                {
                    ProceduralShortSwordDefinition sword =
                        ProceduralShortSwordGenerator.CreateDefinition(
                            42000 + (int)family * 101 + sample,
                            constraints);
                    Assert.That(sword.Family, Is.EqualTo(family));
                    foreach (ShortSwordGenerationBranchGroup group in
                             ShortSwordGenerationBranchCatalog.Groups)
                    {
                        IReadOnlyList<int> candidates =
                            ShortSwordGenerationBranchCatalog.
                                GetCandidateValues(family, group.Decision);
                        if (candidates.Count == 0)
                        {
                            continue;
                        }
                        Assert.That(
                            ShortSwordGenerationBranchCatalog.TryReadValue(
                                sword,
                                group.Decision,
                                out int value),
                            Is.True);
                        Assert.That(
                            candidates,
                            Does.Contain(value),
                            $"{family} generated incompatible " +
                            $"{group.Decision} value {value} at seed " +
                            $"{sword.Seed}.");
                    }
                }
            }
        }

        [Test]
        public void MostRecentFamilyOrLeafLockWinsEveryFamilyConflict()
        {
            ShortSwordGenerationBranchGroup profiles =
                ShortSwordGenerationBranchCatalog.Groups.Single(group =>
                    group.Decision ==
                        ShortSwordGenerationDecision.BladeProfile);
            ShortSwordFamily[] families =
                ShortSwordGenerationBranchCatalog.Families.ToArray();
            foreach (ShortSwordFamily family in families)
            {
                ShortSwordGenerationBranchOption incompatible =
                    profiles.Options.First(option =>
                        !ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                            family,
                            profiles.Decision,
                            option.Value));

                var leafWins =
                    new ProceduralShortSwordGenerationConstraints();
                leafWins.Toggle(
                    ShortSwordGenerationDecision.Family,
                    (int)family);
                leafWins.Toggle(profiles.Decision, incompatible.Value);
                Assert.That(
                    leafWins.IsLocked(
                        ShortSwordGenerationDecision.Family,
                        (int)family),
                    Is.False);
                Assert.That(
                    leafWins.IsLocked(profiles.Decision, incompatible.Value),
                    Is.True);
                ProceduralShortSwordDefinition leafSword =
                    ProceduralShortSwordGenerator.CreateDefinition(
                        42500 + (int)family,
                        leafWins);
                Assert.That(
                    (int)leafSword.BladeProfile,
                    Is.EqualTo(incompatible.Value));
                Assert.That(
                    ShortSwordGenerationBranchCatalog.IsFamilyCompatible(
                        leafSword.Family,
                        profiles.Decision,
                        incompatible.Value),
                    Is.True);

                var familyWins =
                    new ProceduralShortSwordGenerationConstraints();
                familyWins.Toggle(profiles.Decision, incompatible.Value);
                familyWins.Toggle(
                    ShortSwordGenerationDecision.Family,
                    (int)family);
                Assert.That(
                    familyWins.IsLocked(
                        ShortSwordGenerationDecision.Family,
                        (int)family),
                    Is.True);
                Assert.That(
                    familyWins.IsLocked(profiles.Decision, incompatible.Value),
                    Is.False);
                Assert.That(
                    ProceduralShortSwordGenerator.CreateDefinition(
                        42600 + (int)family,
                        familyWins).Family,
                    Is.EqualTo(family));
            }
        }

        [Test]
        public void MostRecentFacetOrPairedGeometryLockWinsEveryConflict()
        {
            var incompatibleWithCoarse = new[]
            {
                new ShortSwordGenerationLock(
                    ShortSwordGenerationDecision.GuardCrossSectionSides,
                    12),
                new ShortSwordGenerationLock(
                    ShortSwordGenerationDecision.GuardCurveSegments,
                    14),
                new ShortSwordGenerationLock(
                    ShortSwordGenerationDecision.HandleCrossSection,
                    (int)ShortSwordHandleCrossSection.Decagonal)
            };

            for (int index = 0; index < incompatibleWithCoarse.Length; index++)
            {
                ShortSwordGenerationLock paired =
                    incompatibleWithCoarse[index];
                var pairedWins =
                    new ProceduralShortSwordGenerationConstraints();
                pairedWins.Toggle(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)ShortSwordFacetTier.Coarse);
                pairedWins.Toggle(paired.Decision, paired.Value);

                Assert.That(
                    pairedWins.IsLocked(paired.Decision, paired.Value),
                    Is.True);
                Assert.That(
                    pairedWins.TryGetValue(
                        ShortSwordGenerationDecision.FacetTier,
                        out _),
                    Is.False,
                    $"The newer {paired.Decision} lock must replace an " +
                    "incompatible facet tier.");
                ProceduralShortSwordDefinition pairedSword =
                    ProceduralShortSwordGenerator.CreateDefinition(
                        42700 + index,
                        pairedWins);
                Assert.That(
                    ShortSwordGenerationBranchCatalog.TryReadValue(
                        pairedSword,
                        paired.Decision,
                        out int pairedValue),
                    Is.True);
                Assert.That(pairedValue, Is.EqualTo(paired.Value));
                Assert.That(
                    ShortSwordGenerationBranchCatalog.IsFacetTierCompatible(
                        pairedSword.FacetTier,
                        paired.Decision,
                        pairedValue),
                    Is.True);

                var facetWins =
                    new ProceduralShortSwordGenerationConstraints();
                facetWins.Toggle(paired.Decision, paired.Value);
                facetWins.Toggle(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)ShortSwordFacetTier.Coarse);

                Assert.That(
                    facetWins.IsLocked(
                        ShortSwordGenerationDecision.FacetTier,
                        (int)ShortSwordFacetTier.Coarse),
                    Is.True);
                Assert.That(
                    facetWins.TryGetValue(paired.Decision, out _),
                    Is.False,
                    $"The newer coarse facet lock must replace incompatible " +
                    $"{paired.Decision} geometry.");
                ProceduralShortSwordDefinition facetSword =
                    ProceduralShortSwordGenerator.CreateDefinition(
                        42720 + index,
                        facetWins);
                Assert.That(
                    ShortSwordGenerationBranchCatalog.TryReadValue(
                        facetSword,
                        paired.Decision,
                        out int resolvedValue),
                    Is.True);
                Assert.That(
                    ShortSwordGenerationBranchCatalog.IsFacetTierCompatible(
                        facetSword.FacetTier,
                        paired.Decision,
                        resolvedValue),
                    Is.True);
            }

            var compatibleWithCoarse = new[]
            {
                new ShortSwordGenerationLock(
                    ShortSwordGenerationDecision.GuardCrossSectionSides,
                    6),
                new ShortSwordGenerationLock(
                    ShortSwordGenerationDecision.GuardCurveSegments,
                    8),
                new ShortSwordGenerationLock(
                    ShortSwordGenerationDecision.HandleCrossSection,
                    (int)ShortSwordHandleCrossSection.OvalFaceted)
            };
            foreach (ShortSwordGenerationLock paired in compatibleWithCoarse)
            {
                var constraints =
                    new ProceduralShortSwordGenerationConstraints();
                constraints.Toggle(
                    ShortSwordGenerationDecision.Family,
                    (int)ShortSwordFamily.Cruciform);
                constraints.Toggle(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)ShortSwordFacetTier.Coarse);
                constraints.Toggle(paired.Decision, paired.Value);

                Assert.That(
                    constraints.IsLocked(
                        ShortSwordGenerationDecision.FacetTier,
                        (int)ShortSwordFacetTier.Coarse),
                    Is.True);
                Assert.That(
                    constraints.IsLocked(paired.Decision, paired.Value),
                    Is.True,
                    $"A compatible {paired.Decision} pair must retain both " +
                    "locks.");
            }
        }

        [Test]
        public void LockedGuardGemAlwaysBuildsACompatibleVisibleSocket()
        {
            var constraints =
                new ProceduralShortSwordGenerationConstraints();
            constraints.Toggle(
                ShortSwordGenerationDecision.OrnamentStyle,
                (int)ShortSwordOrnamentStyle.GuardGem);
            constraints.Toggle(
                ShortSwordGenerationDecision.GemFamily,
                (int)ShortSwordGemFamily.Ruby);

            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(43000, 128)
                    .Select(seed =>
                        ProceduralShortSwordGenerator.CreateDefinition(
                            seed,
                            constraints))
                    .ToArray();

            Assert.That(
                swords.All(sword =>
                    sword.OrnamentStyle ==
                        ShortSwordOrnamentStyle.GuardGem &&
                    sword.GemFamily == ShortSwordGemFamily.Ruby &&
                    sword.GuardHeight >= 0.028f &&
                    sword.GuardSpan >= 0.300f &&
                    sword.GuardConstruction !=
                        ShortSwordGuardConstruction.DirectionalSweep &&
                    sword.GuardConstruction !=
                        ShortSwordGuardConstruction.OffsetLeaf &&
                    sword.GuardConstruction !=
                        ShortSwordGuardConstruction.MinimalBolster),
                Is.True);
        }

        [Test]
        public void ChildFeatureLocksForceAVisibleCompatibleParentBranch()
        {
            var constraints =
                new ProceduralShortSwordGenerationConstraints();
            constraints.Toggle(
                ShortSwordGenerationDecision.DirectionSide,
                (int)ShortSwordDirectionSide.Right);
            constraints.Toggle(
                ShortSwordGenerationDecision.GemCut,
                (int)ShortSwordGemCut.Emerald);

            Assert.That(
                Enumerable.Range(43500, 64)
                    .Select(seed =>
                        ProceduralShortSwordGenerator.CreateDefinition(
                            seed,
                            constraints))
                    .All(sword =>
                        sword.DirectionSign == 1 &&
                        ShortSwordGenerationBranchCatalog.
                            IsDirectionalBladeProfile(
                                sword.BladeProfile) &&
                        sword.OrnamentStyle ==
                            ShortSwordOrnamentStyle.PommelGem &&
                        sword.GemCut == ShortSwordGemCut.Emerald),
                Is.True);
        }

        [Test]
        public void ClickingAnActiveLockAgainReturnsThatDecisionToRandom()
        {
            var constraints =
                new ProceduralShortSwordGenerationConstraints();
            Assert.That(
                constraints.Toggle(
                    ShortSwordGenerationDecision.HiltProfile,
                    (int)ShortSwordHiltProfile.Hooked),
                Is.True);
            Assert.That(constraints.ActiveLockCount, Is.EqualTo(1));
            Assert.That(
                constraints.Toggle(
                    ShortSwordGenerationDecision.HiltProfile,
                    (int)ShortSwordHiltProfile.Hooked),
                Is.False);
            Assert.That(constraints.ActiveLockCount, Is.Zero);
            Assert.That(
                Enumerable.Range(44000, 64)
                    .Select(seed =>
                        ProceduralShortSwordGenerator.CreateDefinition(
                            seed,
                            constraints).HiltProfile)
                    .Distinct()
                    .Count(),
                Is.GreaterThan(1));
        }

        [Test]
        public void GeneratedSwordHasExactlyFourNamedMeshParts()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1701);

            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(
                generator.GeneratedParts.Select(part => part.name),
                Is.EqualTo(new[]
                {
                    ProceduralShortSwordGenerator.BladePartName,
                    ProceduralShortSwordGenerator.GuardPartName,
                    ProceduralShortSwordGenerator.HandlePartName,
                    ProceduralShortSwordGenerator.HiltPartName
                }));
            Assert.That(
                generator.GeneratedParts.All(part =>
                    part.GetComponent<MeshFilter>()?.sharedMesh != null &&
                    part.GetComponent<MeshRenderer>() != null),
                Is.True);
        }

        [Test]
        public void CrackPreviewPartitionsTheExactBladeSurfaceWithoutExternalPieces()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1701);
            GameObject blade = generator.GeneratedParts.Single(part =>
                part.name == ProceduralShortSwordGenerator.BladePartName);
            float intactArea = SurfaceArea(
                blade.GetComponent<MeshFilter>().sharedMesh);

            int created = generator.CrackBlade();
            Transform[] fracturePieces = blade.GetComponentsInChildren<Transform>()
                .Where(item => item != blade.transform)
                .ToArray();
            Transform[] sections = fracturePieces
                .Where(item => item.name.Contains("Section"))
                .ToArray();
            Transform[] branches = fracturePieces
                .Where(item => item.name.Contains("Branch"))
                .ToArray();

            Assert.That(generator.IsBladeCracked, Is.True);
            Assert.That(generator.FractureRevision, Is.EqualTo(1));
            Assert.That(blade.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(created, Is.EqualTo(fracturePieces.Length));
            Assert.That(sections.Length, Is.InRange(3, 4));
            Assert.That(generator.MainFractureCount, Is.InRange(2, 3));
            Assert.That(generator.MissingFracturePieceCount, Is.InRange(1, 2));
            Assert.That(generator.MinimumFractureSegmentRise, Is.GreaterThan(0.018f));
            Assert.That(
                branches.Length,
                Is.EqualTo(
                    generator.MainFractureCount -
                    generator.MissingFracturePieceCount));
            Assert.That(
                fracturePieces.Any(item => item.name.Contains("Shard")),
                Is.False);
            float remainingArea = fracturePieces.Sum(item =>
                SurfaceArea(item.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(remainingArea, Is.LessThan(intactArea * 0.995f));
            Assert.That(remainingArea, Is.GreaterThan(intactArea * 0.82f));
            Assert.That(
                branches.All(item =>
                    Mathf.Abs(item.localPosition.x) <= 0.0121f),
                Is.True);
            Assert.That(
                fracturePieces.All(item =>
                    Mathf.Abs(item.localPosition.z) <= 0.00001f),
                Is.True,
                "Fractured pieces must remain in the original blade plane.");
            float[] sectionOffsets = sections
                .Select(item => item.localPosition.y)
                .OrderBy(value => value)
                .ToArray();
            float[] sectionGaps = sectionOffsets
                .Skip(1)
                .Select((value, index) => value - sectionOffsets[index])
                .ToArray();
            Assert.That(
                sectionGaps.Max() - sectionGaps.Min(),
                Is.LessThan(0.00001f),
                "Major blade pieces must use even separation spacing.");
            Assert.That(
                fracturePieces.All(item =>
                    item.name.StartsWith(
                        ProceduralShortSwordGenerator.BladeFracturePrefix) &&
                    item.GetComponent<MeshFilter>()?.sharedMesh.vertexCount > 3),
                Is.True);
            Assert.That(
                sections.Select(item => item.localPosition.y).Distinct().Count(),
                Is.EqualTo(sections.Length));
        }

        [Test]
        public void CrackButtonRerollsAndNewSwordRestoresAnIntactBlade()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(4021);
            generator.CrackBlade();
            GameObject blade = generator.GeneratedParts.Single(part =>
                part.name == ProceduralShortSwordGenerator.BladePartName);
            Vector3[] firstPositions = blade.transform.Cast<Transform>()
                .Select(item => item.localPosition)
                .ToArray();

            generator.CrackBlade();
            Vector3[] secondPositions = blade.transform.Cast<Transform>()
                .Select(item => item.localPosition)
                .ToArray();
            Assert.That(generator.FractureRevision, Is.EqualTo(2));
            Assert.That(secondPositions, Is.Not.EqualTo(firstPositions));

            generator.RestoreBlade();
            Assert.That(generator.IsBladeCracked, Is.False);
            Assert.That(blade.transform.childCount, Is.EqualTo(0));
            Assert.That(blade.GetComponent<MeshRenderer>().enabled, Is.True);

            generator.CrackBlade();
            generator.GenerateNext();
            GameObject nextBlade = generator.GeneratedParts.Single(part =>
                part.name == ProceduralShortSwordGenerator.BladePartName);
            Assert.That(generator.IsBladeCracked, Is.False);
            Assert.That(generator.FractureRevision, Is.EqualTo(0));
            Assert.That(nextBlade.transform.childCount, Is.EqualTo(0));
            Assert.That(nextBlade.GetComponent<MeshRenderer>().enabled, Is.True);
        }

        [Test]
        public void FourPartsMeetWithoutVisibleAssemblyGaps()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(2334);

            Bounds blade = BoundsOf(generator, ProceduralShortSwordGenerator.BladePartName);
            Bounds guard = BoundsOf(generator, ProceduralShortSwordGenerator.GuardPartName);
            Bounds handle = BoundsOf(generator, ProceduralShortSwordGenerator.HandlePartName);
            Bounds hilt = BoundsOf(generator, ProceduralShortSwordGenerator.HiltPartName);

            Assert.That(blade.min.y, Is.LessThanOrEqualTo(guard.max.y));
            Assert.That(blade.min.y, Is.GreaterThanOrEqualTo(guard.min.y - 0.02f));
            Assert.That(handle.max.y, Is.GreaterThanOrEqualTo(guard.min.y));
            Assert.That(
                hilt.max.y,
                Is.GreaterThanOrEqualTo(handle.min.y - 0.00001f));
            Assert.That(blade.max.y, Is.GreaterThan(0.90f));
            Assert.That(hilt.min.y, Is.LessThan(-0.30f));
        }

        [Test]
        public void BladeAndHandleSeatInsideTheGuardWithoutCrossingIt()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            foreach (int seed in Enumerable.Range(2300, 512))
            {
                ProceduralShortSwordDefinition sword = generator.Generate(seed);
                Bounds blade = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.BladePartName);
                Bounds guard = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.GuardPartName);
                Bounds handle = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.HandlePartName);
                Bounds hilt = BoundsOf(
                    generator,
                    ProceduralShortSwordGenerator.HiltPartName);

                for (int sample = 0; sample < 9; sample++)
                {
                    float x = Mathf.Lerp(
                        -sword.BladeWidth * 0.55f,
                        sword.BladeWidth * 0.55f,
                        sample / 8f);
                    ProceduralShortSwordGenerator.ResolveGuardVerticalEnvelopeAtX(
                        sword,
                        x,
                        out float guardBottom,
                        out float guardTop);
                    float bladeSeat =
                        ProceduralShortSwordGenerator.ResolveBladeSeatHeightAtX(
                            sword,
                            x);
                    Assert.That(
                        bladeSeat,
                        Is.GreaterThan(guardBottom),
                        $"Seed {seed} blade passed through the guard bottom at x={x}.");
                    Assert.That(
                        bladeSeat,
                        Is.LessThan(guardTop),
                        $"Seed {seed} blade bottom remained visible above the guard at x={x}.");
                }
                float handleTopRadius =
                    ProceduralShortSwordGenerator.ResolveHandleEndRadius(
                        sword.HandleRadius,
                        sword.HandleProfile,
                        top: true);
                float expectedHandleTop = Enumerable.Range(0, 8)
                    .Select(side =>
                        ProceduralShortSwordGenerator.ResolveHandleSeatHeightAtX(
                            sword,
                            Mathf.Cos(side / 8f * Mathf.PI * 2f) *
                                handleTopRadius))
                    .Append(
                        ProceduralShortSwordGenerator.ResolveHandleSeatHeight(sword))
                    .Max();
                Assert.That(
                    handle.max.y,
                    Is.EqualTo(expectedHandleTop).Within(0.00001f),
                    $"Seed {seed} handle must rise into its fitted guard seat.");
                for (int sample = 0; sample < 9; sample++)
                {
                    float x = Mathf.Lerp(
                        -handleTopRadius,
                        handleTopRadius,
                        sample / 8f);
                    ProceduralShortSwordGenerator.ResolveGuardVerticalEnvelopeAtX(
                        sword,
                        x,
                        out float handleGuardBottom,
                        out float handleGuardTop);
                    float handleSeat = ProceduralShortSwordGenerator
                        .ResolveHandleSeatHeightAtX(sword, x);
                    Assert.That(handleSeat, Is.GreaterThan(handleGuardBottom));
                    Assert.That(handleSeat, Is.LessThan(handleGuardTop));
                }
                Assert.That(blade.min.y, Is.GreaterThan(guard.min.y));
                Assert.That(blade.min.y, Is.LessThan(guard.max.y));
                Assert.That(
                    handle.min.y,
                    Is.EqualTo(-sword.HandleLength).Within(0.00001f));
                Assert.That(
                    hilt.max.y,
                    Is.EqualTo(-sword.HandleLength).Within(0.00001f),
                    $"Seed {seed} pommel must begin at the handle's bottom face.");
            }
        }

        [Test]
        public void VariationStaysInsideShortSwordProportions()
        {
            ProceduralShortSwordDefinition[] definitions =
                Enumerable.Range(3000, 8192)
                .Select(ProceduralShortSwordGenerator.CreateDefinition)
                .ToArray();

            foreach (ProceduralShortSwordDefinition definition in definitions)
            {
                AssertShortSwordDefinitionBounds(
                    definition,
                    $"Seed {definition.Seed}");
            }

            foreach (ShortSwordGenerationBranchGroup group in
                     ShortSwordGenerationBranchCatalog.Groups)
            {
                int[] generatedValues = definitions
                    .Select(definition =>
                    {
                        Assert.That(
                            ShortSwordGenerationBranchCatalog.TryReadValue(
                                definition,
                                group.Decision,
                                out int value),
                            Is.True);
                        return value;
                    })
                    .Distinct()
                    .ToArray();
                Assert.That(
                    generatedValues,
                    Is.EquivalentTo(
                        group.Options.Select(option => option.Value)),
                    $"The unlocked seed corpus did not reach every " +
                    $"{group.Decision} branch.");
            }
        }

        [Test]
        public void GuardCrossSectionsSpanBothFlatOrientationsAndIntermediateAngles()
        {
            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(12000, 4096)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            float[] normalizedRotations = swords
                .Select(sword =>
                    sword.GuardCrossSectionRotation /
                    (Mathf.PI / sword.GuardCrossSectionSides))
                .ToArray();

            Assert.That(normalizedRotations.Min(), Is.LessThan(0.03f));
            Assert.That(normalizedRotations.Max(), Is.GreaterThan(0.97f));
            Assert.That(
                normalizedRotations.Count(value => value > 0.35f && value < 0.65f),
                Is.GreaterThan(800));
        }

        [Test]
        public void GuardAndHiltConnectionsAlwaysCoverTheHandleEnds()
        {
            foreach (ProceduralShortSwordDefinition sword in
                     Enumerable.Range(6200, 512)
                         .Select(ProceduralShortSwordGenerator.CreateDefinition))
            {
                float handleTopRadius =
                    ProceduralShortSwordGenerator.ResolveHandleEndRadius(
                        sword.HandleRadius,
                        sword.HandleProfile,
                        top: true);
                float handleBottomRadius =
                    ProceduralShortSwordGenerator.ResolveHandleEndRadius(
                        sword.HandleRadius,
                        sword.HandleProfile,
                        top: false);

                Assert.That(sword.GuardDepth * 0.5f, Is.GreaterThan(handleTopRadius));
                Assert.That(sword.GuardSpan, Is.GreaterThan(handleTopRadius * 2f));
                Assert.That(sword.HiltRadius, Is.GreaterThan(handleBottomRadius));
                Assert.That(
                    ProceduralShortSwordGenerator.ResolveHiltConnectionRadius(
                        sword),
                    Is.GreaterThan(handleBottomRadius));
            }
        }

        [Test]
        public void GuardMassAndStyleAdaptToTheBlade()
        {
            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(8000, 1024)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            ProceduralShortSwordDefinition[] narrow = swords
                .Where(sword => sword.BladeWidth < 0.083f)
                .ToArray();
            ProceduralShortSwordDefinition[] broad = swords
                .Where(sword => sword.BladeWidth > 0.103f)
                .ToArray();

            Assert.That(
                broad.Average(sword => sword.GuardHeight),
                Is.GreaterThan(narrow.Average(sword => sword.GuardHeight) + 0.004f));
            Assert.That(
                broad.Average(sword => sword.GuardSpan),
                Is.GreaterThan(narrow.Average(sword => sword.GuardSpan) + 0.035f));

            Assert.That(
                swords.Where(sword =>
                        !ShortSwordGenerationBranchCatalog.
                            IsDirectionalBladeProfile(sword.BladeProfile))
                    .All(sword =>
                        !RequiresDirectionalFamily(
                            sword.GuardConstruction)),
                Is.True);
            Assert.That(
                swords.Where(sword =>
                        sword.BladeBackStyle == ShortSwordBladeBackStyle.Sawback)
                    .All(sword =>
                        ShortSwordGenerationBranchCatalog.
                            IsDirectionalBladeProfile(
                                sword.BladeProfile)),
                Is.True);
            Assert.That(
                swords.Where(sword => RequiresDirectionalFamily(
                        sword.GuardConstruction))
                    .All(sword =>
                        ShortSwordGenerationBranchCatalog.
                            IsDirectionalBladeProfile(
                                sword.BladeProfile)),
                Is.True);
        }

        [Test]
        public void PreviewDollyReachesDetailViewInTwoHardwareIndependentSteps()
        {
            float oneStepIn =
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomDistance(4.45f, 0.02f, 0.225f, 0.18f, 8f);
            Assert.That(
                oneStepIn,
                Is.LessThan(3.56f),
                "One wheel event should move substantially toward the model.");
            float twoStepsIn =
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomDistance(
                        oneStepIn,
                        0.02f,
                        0.225f,
                        0.18f,
                        8f);
            Assert.That(
                twoStepsIn,
                Is.LessThan(2.84f),
                "Two wheel events should reach a close inspection view.");
            float minimumDistance =
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomDistance(
                        0.2f,
                        120f,
                        0.225f,
                        0.18f,
                        8f);
            Assert.That(
                minimumDistance,
                Is.EqualTo(0.18f));
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomDistance(
                        7.9f,
                        -120f,
                        0.225f,
                        0.18f,
                        8f),
                Is.EqualTo(8f));
            Assert.That(
                WorldBuilder.Gameplay.Loop.Scenes.ShortSwordGeneratorLabController
                    .CalculateZoomDistance(
                        4.45f,
                        120f,
                        0.225f,
                        0.18f,
                        8f),
                Is.EqualTo(oneStepIn).Within(0.0001f),
                "Wheel hardware magnitude must not change the per-step zoom.");
        }

        [Test]
        public void RegenerationReplacesInsteadOfAccumulatingParts()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            ProceduralShortSwordDefinition first = generator.Generate(90);
            ProceduralShortSwordDefinition second = generator.GenerateNext();

            Assert.That(second.Seed, Is.EqualTo(first.Seed + 1));
            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(root.transform.childCount, Is.EqualTo(4));
        }

        [Test]
        public void GenerationRemovesUntrackedOrphanSwordParts()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var orphanBlade = new GameObject(
                ProceduralShortSwordGenerator.BladePartName);
            orphanBlade.transform.SetParent(root.transform, false);
            orphanBlade.AddComponent<MeshFilter>();

            generator.Generate(2104);

            Assert.That(root.transform.childCount, Is.EqualTo(4));
            Assert.That(
                root.transform.Cast<Transform>().Count(child =>
                    child.name == ProceduralShortSwordGenerator.BladePartName),
                Is.EqualTo(1));
            Assert.That(orphanBlade == null, Is.True);
        }

        [Test]
        public void EveryCatalogLeafBuildsValidFiniteHardFacedMeshes()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            int leafIndex = 0;
            foreach (ShortSwordGenerationBranchGroup group in
                     ShortSwordGenerationBranchCatalog.Groups)
            {
                foreach (ShortSwordGenerationBranchOption option in
                         group.Options)
                {
                    generator.ClearGenerationLocks();
                    Assert.That(
                        generator.ToggleGenerationLock(
                            group.Decision,
                            option.Value),
                        Is.True);
                    int seed = 5100 + leafIndex * 37;
                    ProceduralShortSwordDefinition sword =
                        generator.Generate(seed);
                    Assert.That(
                        ShortSwordGenerationBranchCatalog.TryReadValue(
                            sword,
                            group.Decision,
                            out int generatedValue),
                        Is.True);
                    Assert.That(
                        generatedValue,
                        Is.EqualTo(option.Value),
                        $"Mesh coverage did not retain {group.Decision} / " +
                        $"{option.Label}.");
                    AssertShortSwordDefinitionBounds(
                        sword,
                        $"{group.Decision} / {option.Label}, seed {seed}");
                    foreach (MeshFilter filter in
                             root.GetComponentsInChildren<MeshFilter>())
                    {
                        AssertValidHardFacedMesh(
                            filter.sharedMesh,
                            $"{group.Decision} / {option.Label}, seed " +
                            $"{seed}, mesh {filter.name}");
                    }
                    AssertGeneratedSwordBounds(
                        $"{group.Decision} / {option.Label}, seed {seed}");
                    leafIndex++;
                }
            }

            Assert.That(leafIndex, Is.GreaterThan(100));
        }

        [Test]
        public void EveryTriangleOwnsHardFaceVerticesAndNormals()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            generator.Generate(1201);

            foreach (MeshFilter filter in
                     root.GetComponentsInChildren<MeshFilter>())
            {
                AssertValidHardFacedMesh(
                    filter.sharedMesh,
                    $"Seed 1201, mesh {filter.name}");
            }
        }

        [Test]
        public void EveryGuardBindingStaysFittedToItsSelectedGuardArm()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            ShortSwordGenerationBranchGroup bindings =
                ShortSwordGenerationBranchCatalog.Groups.Single(group =>
                    group.Decision ==
                        ShortSwordGenerationDecision.GuardBindingStyle);
            int optionIndex = 0;
            foreach (ShortSwordGenerationBranchOption option in
                     bindings.Options.Where(option =>
                         option.Value !=
                            (int)ShortSwordGuardBindingStyle.None))
            {
                generator.ClearGenerationLocks();
                Assert.That(
                    generator.ToggleGenerationLock(
                        ShortSwordGenerationDecision.GuardConstruction,
                        (int)ShortSwordGuardConstruction.BladeQuillons),
                    Is.True);
                Assert.That(
                    generator.ToggleGenerationLock(
                        ShortSwordGenerationDecision.Directionality,
                        (int)ShortSwordDirectionality.Conventional),
                    Is.True);
                Assert.That(
                    generator.ToggleGenerationLock(
                        bindings.Decision,
                        option.Value),
                    Is.True);
                ProceduralShortSwordDefinition sword = generator.Generate(
                    6900 + optionIndex * 53);
                var binding = (ShortSwordGuardBindingStyle)option.Value;
                Assert.That(sword.GuardBindingStyle, Is.EqualTo(binding));
                Assert.That(
                    sword.GuardConstruction,
                    Is.EqualTo(ShortSwordGuardConstruction.BladeQuillons));

                GameObject guard = generator.GeneratedParts.Single(part =>
                    part.name == ProceduralShortSwordGenerator.GuardPartName);
                Mesh guardMesh = guard.GetComponent<MeshFilter>().sharedMesh;
                MeshFilter[] wraps = guard.GetComponentsInChildren<MeshFilter>()
                    .Where(filter => IsGuardBindingName(filter.name))
                    .ToArray();
                Assert.That(
                    wraps,
                    Has.Length.EqualTo(
                        ExpectedGuardBindingDetailCount(binding)));
                Assert.That(
                    wraps.Count(filter => filter.name.StartsWith("Left")),
                    Is.EqualTo(ExpectedGuardBindingSideCount(binding, left: true)));
                Assert.That(
                    wraps.Count(filter => filter.name.StartsWith("Right")),
                    Is.EqualTo(ExpectedGuardBindingSideCount(binding, left: false)));
                foreach (MeshFilter wrap in wraps)
                {
                    AssertGuardBindingFitsEnvelope(
                        sword,
                        guardMesh,
                        wrap);
                }
                optionIndex++;
            }
        }

        [Test]
        public void CuratedDetailsAvoidJointCollarsAndGuardMatchesPommelMetal()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var seenOrnaments = new HashSet<ShortSwordOrnamentStyle>();
            for (int seed = 7000; seed < 7512; seed++)
            {
                ProceduralShortSwordDefinition sword = generator.Generate(seed);
                seenOrnaments.Add(sword.OrnamentStyle);
                Assert.That(
                    root.GetComponentsInChildren<Transform>()
                        .Any(item => item.name == "Guard Collar"),
                    Is.False);
                Assert.That(
                    root.GetComponentsInChildren<Transform>()
                        .Any(item => item.name == "Pommel Connection Ring"),
                    Is.False);
                if (sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem)
                {
                    string[] jewelNames = root.GetComponentsInChildren<Transform>()
                        .Select(item => item.name)
                        .Where(name => name.EndsWith("Guard Jewel"))
                        .ToArray();
                    Assert.That(
                        jewelNames,
                        Is.EquivalentTo(new[]
                        {
                            "Front Guard Jewel",
                            "Rear Guard Jewel"
                        }));
                    Mesh guardMesh = generator.GeneratedParts.Single(
                            part => part.name ==
                                ProceduralShortSwordGenerator.GuardPartName)
                        .GetComponent<MeshFilter>()
                        .sharedMesh;
                    Mesh frontJewel = root.GetComponentsInChildren<MeshFilter>()
                        .Single(filter => filter.name == "Front Guard Jewel")
                        .sharedMesh;
                    Assert.That(
                        frontJewel.bounds.size.y,
                        Is.LessThan(sword.GuardHeight * 0.50f));
                    Assert.That(
                        frontJewel.bounds.min.z,
                        Is.LessThanOrEqualTo(guardMesh.bounds.max.z));
                }

                GameObject guardObject = generator.GeneratedParts.Single(
                    part => part.name == ProceduralShortSwordGenerator.GuardPartName);
                Transform[] guardDetails = guardObject.transform
                    .Cast<Transform>()
                    .ToArray();
                Assert.That(
                    guardDetails.All(detail =>
                        detail.name.EndsWith("Guard Jewel") ||
                        IsGuardBindingName(detail.name)),
                    Is.True,
                    $"Seed {seed} added an uncurated floating guard detail.");
                int expectedGuardDetails =
                    ExpectedGuardBindingDetailCount(
                        sword.GuardBindingStyle) +
                    (sword.OrnamentStyle ==
                        ShortSwordOrnamentStyle.GuardGem
                            ? 2
                            : 0);
                Assert.That(
                    guardDetails,
                    Has.Length.EqualTo(expectedGuardDetails),
                    $"Seed {seed} must contain only fitted bindings and " +
                    "the selected jewel pair.");

                Renderer guard = generator.GeneratedParts.Single(
                    part => part.name == ProceduralShortSwordGenerator.GuardPartName)
                    .GetComponent<Renderer>();
                Renderer hilt = generator.GeneratedParts.Single(
                    part => part.name == ProceduralShortSwordGenerator.HiltPartName)
                    .GetComponent<Renderer>();
                var guardProperties = new MaterialPropertyBlock();
                var hiltProperties = new MaterialPropertyBlock();
                guard.GetPropertyBlock(guardProperties);
                hilt.GetPropertyBlock(hiltProperties);
                Assert.That(
                    hiltProperties.GetColor("_BaseColor"),
                    Is.EqualTo(guardProperties.GetColor("_BaseColor")));
            }
            Assert.That(seenOrnaments, Does.Contain(ShortSwordOrnamentStyle.Plain));
            Assert.That(seenOrnaments, Does.Contain(ShortSwordOrnamentStyle.PommelGem));

            ProceduralShortSwordDefinition guardJewelSword =
                Enumerable.Range(10000, 50000)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .First(sword =>
                        sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem);
            generator.Generate(guardJewelSword.Seed);
            GameObject jeweledGuard = generator.GeneratedParts.Single(
                part => part.name == ProceduralShortSwordGenerator.GuardPartName);
            Assert.That(
                jeweledGuard.transform.Cast<Transform>()
                    .Select(child => child.name)
                    .Where(name => name.EndsWith("Guard Jewel")),
                Is.EquivalentTo(new[]
                {
                    "Front Guard Jewel",
                    "Rear Guard Jewel"
                }));
        }

        [Test]
        public void JewelsAreRareAndGuardJewelsOnlyUseViableFaces()
        {
            ProceduralShortSwordDefinition[] swords =
                Enumerable.Range(10000, 32768)
                    .Select(ProceduralShortSwordGenerator.CreateDefinition)
                    .ToArray();
            ProceduralShortSwordDefinition[] jeweled = swords
                .Where(sword =>
                    sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem ||
                    sword.OrnamentStyle == ShortSwordOrnamentStyle.PommelGem)
                .ToArray();
            float jewelRate = jeweled.Length / (float)swords.Length;

            Assert.That(
                jewelRate,
                Is.InRange(0.15f, 0.45f),
                "Unlocked world swords should visibly exercise the authored " +
                "guard and pommel additions instead of suppressing almost all of them.");
            Assert.That(
                swords.Where(sword =>
                        sword.OrnamentStyle == ShortSwordOrnamentStyle.GuardGem)
                    .All(sword =>
                        sword.GuardHeight >= 0.028f &&
                        sword.GuardSpan >= 0.300f &&
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.DirectionalSweep &&
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.OffsetLeaf &&
                        sword.GuardConstruction !=
                            ShortSwordGuardConstruction.MinimalBolster),
                Is.True);
            Assert.That(
                jeweled.Select(sword => sword.GemCut).Distinct().Count(),
                Is.EqualTo(
                    ShortSwordGenerationBranchCatalog.Groups.Single(group =>
                            group.Decision ==
                                ShortSwordGenerationDecision.GemCut)
                        .Options.Count));
        }

        [Test]
        public void BladeFacetTierScalesAuthoredBandsWithinLowPolyBudgets()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var faceCounts = new Dictionary<ShortSwordFacetTier, int>();
            foreach (ShortSwordFacetTier tier in
                     (ShortSwordFacetTier[])Enum.GetValues(
                         typeof(ShortSwordFacetTier)))
            {
                generator.ClearGenerationLocks();
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.Family,
                    (int)ShortSwordFamily.Cruciform);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.BladeSectionStyle,
                    (int)ShortSwordBladeSectionStyle.ShallowFuller);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)tier);
                generator.Generate(1201);
                Mesh blade = generator.GeneratedParts
                    .Single(part =>
                        part.name ==
                            ProceduralShortSwordGenerator.BladePartName)
                    .GetComponent<MeshFilter>()
                    .sharedMesh;
                int faceCount = blade.triangles.Length / 3;
                Assert.That(
                    faceCount,
                    Is.InRange(80, 760),
                    $"{tier} left the authored low-poly face budget.");
                faceCounts[tier] = faceCount;
            }

            Assert.That(
                faceCounts[ShortSwordFacetTier.Standard],
                Is.GreaterThan(faceCounts[ShortSwordFacetTier.Coarse]));
            Assert.That(
                faceCounts[ShortSwordFacetTier.Intricate],
                Is.GreaterThan(faceCounts[ShortSwordFacetTier.Standard]));
            Assert.That(
                ProceduralShortSwordGenerator.TargetFacetLength,
                Is.InRange(0.045f, 0.060f));
        }

        [Test]
        public void FacetTierScalesRingPommelWithoutOpeningItsHandleJoint()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var faceCounts = new Dictionary<ShortSwordFacetTier, int>();
            foreach (ShortSwordFacetTier tier in new[]
                     {
                         ShortSwordFacetTier.Standard,
                         ShortSwordFacetTier.Intricate
                     })
            {
                generator.ClearGenerationLocks();
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.Family,
                    (int)ShortSwordFamily.Leafblade);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.HiltProfile,
                    (int)ShortSwordHiltProfile.Ring);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)tier);
                ProceduralShortSwordDefinition sword = generator.Generate(
                    12140 + (int)tier);
                Mesh hilt = MeshOf(
                    generator,
                    ProceduralShortSwordGenerator.HiltPartName);

                Assert.That(sword.Family, Is.EqualTo(ShortSwordFamily.Leafblade));
                Assert.That(sword.HiltProfile, Is.EqualTo(ShortSwordHiltProfile.Ring));
                Assert.That(sword.FacetTier, Is.EqualTo(tier));
                Assert.That(
                    hilt.bounds.max.y,
                    Is.EqualTo(-sword.HandleLength).Within(0.00001f),
                    $"{tier} ring pommel opened a gap below the handle.");
                AssertValidHardFacedMesh(hilt, $"{tier} ring pommel");
                AssertClosedOutwardMesh(hilt, $"{tier} ring pommel");
                faceCounts[tier] = hilt.triangles.Length / 3;
            }

            Assert.That(
                faceCounts[ShortSwordFacetTier.Intricate],
                Is.GreaterThan(faceCounts[ShortSwordFacetTier.Standard]));
        }

        [TestCase(
            ShortSwordFamily.Cruciform,
            ShortSwordHiltProfile.Faceted)]
        [TestCase(
            ShortSwordFamily.Leafblade,
            ShortSwordHiltProfile.Crowned)]
        public void FacetTierScalesSolidPommelSilhouettes(
            ShortSwordFamily family,
            ShortSwordHiltProfile hiltProfile)
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            var faceCounts = new Dictionary<ShortSwordFacetTier, int>();
            foreach (ShortSwordFacetTier tier in
                     (ShortSwordFacetTier[])Enum.GetValues(
                         typeof(ShortSwordFacetTier)))
            {
                generator.ClearGenerationLocks();
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.Family,
                    (int)family);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.HiltProfile,
                    (int)hiltProfile);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)tier);
                ProceduralShortSwordDefinition sword = generator.Generate(
                    12220 + (int)hiltProfile * 31);
                Mesh hilt = MeshOf(
                    generator,
                    ProceduralShortSwordGenerator.HiltPartName);

                Assert.That(sword.Family, Is.EqualTo(family));
                Assert.That(sword.HiltProfile, Is.EqualTo(hiltProfile));
                Assert.That(sword.FacetTier, Is.EqualTo(tier));
                Assert.That(
                    hilt.bounds.max.y,
                    Is.EqualTo(-sword.HandleLength).Within(0.00001f),
                    $"{tier} {hiltProfile} pommel opened a handle gap.");
                AssertValidHardFacedMesh(hilt, $"{tier} {hiltProfile} pommel");
                AssertClosedOutwardMesh(hilt, $"{tier} {hiltProfile} pommel");
                faceCounts[tier] = hilt.triangles.Length / 3;
            }

            Assert.That(
                faceCounts[ShortSwordFacetTier.Standard],
                Is.GreaterThan(faceCounts[ShortSwordFacetTier.Coarse]));
            Assert.That(
                faceCounts[ShortSwordFacetTier.Intricate],
                Is.GreaterThan(faceCounts[ShortSwordFacetTier.Standard]));
        }

        [Test]
        public void RaidPresentationReplacesLegacyVisualAtRequestedLength()
        {
            root = new GameObject("Legacy Raid Sword");
            MeshRenderer legacyRootRenderer =
                root.AddComponent<MeshRenderer>();
            Light legacyRootLight = root.AddComponent<Light>();
            legacyRootLight.intensity = 40f;
            GameObject legacyBlade = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            legacyBlade.name = "Pointed Blade";
            legacyBlade.transform.SetParent(root.transform, false);

            RaidShortSwordPresentation presentation =
                RaidShortSwordPresentation.Replace(
                    root.transform,
                    7719,
                    1.6f);

            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.Seed, Is.EqualTo(7719));
            Assert.That(legacyRootRenderer.enabled, Is.False);
            Assert.That(legacyRootLight.enabled, Is.False);
            Assert.That(
                root.GetComponentsInChildren<Transform>(true)
                    .Any(part => part.name == "Pointed Blade"),
                Is.False);
            Assert.That(presentation.Generator.HasGeneratedSword, Is.True);
            float displayedLength = presentation.Generator
                .CurrentDefinition.TotalLength *
                presentation.Generator.transform.localScale.y;
            Assert.That(displayedLength, Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(
                presentation.GripCenterHeight,
                Is.EqualTo(
                    RaidShortSwordPresentation.LegacyGripCenterHeight)
                    .Within(0.0001f));
            Assert.That(
                presentation.BladeLength,
                Is.GreaterThan(0.5f));
            Assert.That(presentation.BladeHitbox, Is.Not.Null);
            foreach (Renderer renderer in
                     presentation.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Assert.That(
                    properties.GetColor("_EmissionColor"),
                    Is.EqualTo(Color.black),
                    $"{renderer.name} must not emit light in a raid.");
                Assert.That(
                    renderer.sharedMaterial,
                    Is.Not.Null,
                    $"{renderer.name} must use the controlled world-sword " +
                    "material even when its legacy socket has no source material.");
                Assert.That(
                    renderer.sharedMaterial.shader.name,
                    Is.EqualTo(
                        ProceduralShortSwordGenerator.WorldShaderName));
                Assert.That(
                    renderer.sharedMaterial.IsKeywordEnabled(
                        "_SPECULARHIGHLIGHTS_OFF"),
                    Is.True);
                Assert.That(
                    renderer.sharedMaterial.IsKeywordEnabled(
                        "_ENVIRONMENTREFLECTIONS_OFF"),
                    Is.True);
                Assert.That(
                    properties.GetFloat("_Metallic"),
                    Is.LessThanOrEqualTo(
                        ProceduralShortSwordGenerator.WorldSwordMetallic));
                Assert.That(
                    properties.GetFloat("_Smoothness"),
                    Is.LessThanOrEqualTo(
                        ProceduralShortSwordGenerator.WorldSwordSmoothness));
                Assert.That(
                    properties.GetFloat("_SpecularHighlights"),
                    Is.Zero);
                Assert.That(
                    properties.GetFloat("_EnvironmentReflections"),
                    Is.Zero);
            }
            Mesh stableMesh = presentation.Generator
                .GetComponentInChildren<MeshFilter>(true).sharedMesh;
            RaidShortSwordPresentation unchanged =
                RaidShortSwordPresentation.Replace(root.transform, 7719, 1.6f);
            Assert.That(unchanged, Is.SameAs(presentation));
            Assert.That(
                unchanged.Generator
                    .GetComponentInChildren<MeshFilter>(true).sharedMesh,
                Is.SameAs(stableMesh),
                "The one-second actor refresh must not rebuild an unchanged " +
                "raid sword or invalidate inventory-preview mesh references.");
            RaidShortSwordPresentation refreshed =
                RaidShortSwordPresentation.Replace(root.transform, 77);
            Assert.That(refreshed, Is.SameAs(presentation));
            Assert.That(refreshed.Seed, Is.EqualTo(77));
            Assert.That(
                refreshed.Generator.CurrentDefinition.Seed,
                Is.EqualTo(77),
                "A reused player or pooled guard sword must not retain the " +
                "previous raid's furniture definition.");
            UnityEngine.Object.DestroyImmediate(refreshed.Generator.gameObject);
            RaidShortSwordPresentation recovered =
                RaidShortSwordPresentation.Replace(root.transform, 77, 1.6f);
            Assert.That(recovered, Is.SameAs(presentation));
            Assert.That(recovered.Generator, Is.Not.Null);
            Assert.That(recovered.Generator.HasGeneratedSword, Is.True);
                Assert.That(
                    recovered.Generator.GetComponentInChildren<Renderer>()
                        .sharedMaterial.shader.name,
                    Is.EqualTo(
                        ProceduralShortSwordGenerator.WorldShaderName));
        }

        [Test]
        public void RaidPresentationMigratesAnExistingSimpleLitSword()
        {
            root = new GameObject("Pooled Raid Sword");
            Shader simpleLit = Shader.Find(
                "Universal Render Pipeline/Simple Lit");
            Assert.That(simpleLit, Is.Not.Null);
            var legacyGlossMaterial = new Material(simpleLit);
            legacyGlossMaterial.SetColor("_SpecColor", Color.white);
            legacyGlossMaterial.SetTexture(
                "_SpecGlossMap",
                Texture2D.whiteTexture);
            legacyGlossMaterial.EnableKeyword("_SPECGLOSSMAP");
            legacyGlossMaterial.EnableKeyword(
                "_GLOSSINESS_FROM_BASE_ALPHA");
            try
            {
                RaidShortSwordPresentation presentation =
                    RaidShortSwordPresentation.Replace(
                        root.transform,
                        7719,
                        1.3f);
                ProceduralShortSwordGenerator originalGenerator =
                    presentation.Generator;

                // Simulate an actor held alive across the material-pipeline
                // change: it has valid geometry, but it still owns the old
                // Simple Lit shader that ignores our smoothness property.
                originalGenerator.ConfigureMaterials(
                    legacyGlossMaterial,
                    legacyGlossMaterial,
                    legacyGlossMaterial,
                    legacyGlossMaterial);
                originalGenerator.GenerateUnrestricted(7719);
                Assert.That(
                    originalGenerator.GetComponentInChildren<Renderer>()
                        .sharedMaterial.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Simple Lit"));

                RaidShortSwordPresentation migrated =
                    RaidShortSwordPresentation.Replace(
                        root.transform,
                        7719,
                        1.3f);
                Assert.That(migrated, Is.SameAs(presentation));
                Assert.That(migrated.Generator, Is.SameAs(originalGenerator));
                foreach (Renderer renderer in
                         migrated.Generator.GetComponentsInChildren<Renderer>(
                             true))
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null);
                    Assert.That(
                        renderer.sharedMaterial.shader.name,
                        Is.EqualTo(
                            ProceduralShortSwordGenerator.WorldShaderName));
                    Assert.That(
                        renderer.sharedMaterial.IsKeywordEnabled(
                            "_METALLICSPECGLOSSMAP"),
                        Is.False);
                    Assert.That(
                        renderer.sharedMaterial.IsKeywordEnabled(
                            "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                        Is.False);
                    Assert.That(
                        renderer.sharedMaterial.IsKeywordEnabled(
                            "_ENVIRONMENTREFLECTIONS_OFF"),
                        Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacyGlossMaterial);
            }
        }

        [Test]
        public void RaidPresentationRepairsLightingAddedAfterSpawn()
        {
            root = new GameObject("Mutated Raid Sword");
            RaidShortSwordPresentation presentation =
                RaidShortSwordPresentation.Replace(root.transform, 3317);
            Renderer renderer = presentation.Generator
                .GetComponentInChildren<Renderer>();
            Shader unsafeShader = Shader.Find(
                "Universal Render Pipeline/Lit");
            var unsafeMaterial = new Material(unsafeShader);
            unsafeMaterial.EnableKeyword("_EMISSION");
            unsafeMaterial.SetColor("_EmissionColor", Color.white * 40f);
            renderer.sharedMaterial = unsafeMaterial;
            Light injectedLight = root.AddComponent<Light>();
            injectedLight.intensity = 80f;
            injectedLight.range = 40f;
            try
            {
                presentation.EnforceLightingSafety();

                Assert.That(injectedLight.enabled, Is.False);
                Assert.That(injectedLight.intensity, Is.Zero);
                Assert.That(injectedLight.range, Is.Zero);
                foreach (Renderer repaired in presentation.Generator
                             .GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(
                        repaired.sharedMaterial.shader.name,
                        Is.EqualTo(
                            ProceduralShortSwordGenerator.
                                WorldShaderName));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unsafeMaterial);
            }
        }

        [Test]
        public void UnrestrictedRaidSeedsReachEveryFurnitureBranch()
        {
            ProceduralShortSwordDefinition[] swords = Enumerable.Range(0, 4096)
                .Select(seed =>
                    ProceduralShortSwordGenerator.CreateDefinition(
                        unchecked(seed * 486187739 + 7919)))
                .ToArray();

            Assert.That(
                swords.Select(sword => sword.GripStyle).Distinct(),
                Is.EquivalentTo(Enum.GetValues(typeof(ShortSwordGripStyle))));
            Assert.That(
                swords.Select(sword => sword.GripColor).Distinct(),
                Is.EquivalentTo(Enum.GetValues(typeof(ShortSwordGripColor))));
            Assert.That(
                swords.Select(sword => sword.HandleProfile).Distinct(),
                Is.EquivalentTo(Enum.GetValues(typeof(ShortSwordHandleProfile))));
            Assert.That(
                swords.Select(sword => sword.HiltProfile).Distinct(),
                Is.EquivalentTo(Enum.GetValues(typeof(ShortSwordHiltProfile))));

            var visiblyConstructedGrips = new HashSet<ShortSwordGripStyle>
            {
                ShortSwordGripStyle.CrossWrappedCord,
                ShortSwordGripStyle.RibbedWood,
                ShortSwordGripStyle.HerringboneCord,
                ShortSwordGripStyle.HalfWrappedWood,
                ShortSwordGripStyle.WireBoundLeather
            };
            float constructedGripRate = swords.Count(sword =>
                visiblyConstructedGrips.Contains(sword.GripStyle)) /
                (float)swords.Length;
            Assert.That(
                constructedGripRate,
                Is.GreaterThan(0.40f),
                "Raid generation must frequently show cord, wood, and wire " +
                "construction rather than reading as an all-leather pool.");
            Assert.That(
                swords.Count(sword => sword.GuardBindingStyle !=
                        ShortSwordGuardBindingStyle.None) /
                    (float)swords.Length,
                Is.GreaterThan(0.30f));
            Assert.That(
                swords.Count(sword => sword.OrnamentStyle !=
                        ShortSwordOrnamentStyle.Plain) /
                    (float)swords.Length,
                Is.GreaterThan(0.15f));
        }

        [Test]
        public void HandlesAreShorterAndGripDetailsFollowTheirSurface()
        {
            ProceduralShortSwordDefinition[] swords = Enumerable.Range(1, 80)
                .Select(ProceduralShortSwordGenerator.CreateDefinition)
                .ToArray();

            Assert.That(swords.Average(sword => sword.HandleLength),
                Is.LessThan(0.235f));
            foreach (ProceduralShortSwordDefinition sword in swords)
            {
                float middleRadius =
                    ProceduralShortSwordGenerator.ResolveHandleSurfaceRadius(
                        sword,
                        0.5f);
                Assert.That(middleRadius, Is.GreaterThan(0.020f));
                Assert.That(middleRadius, Is.LessThan(0.036f));
            }
        }

        [Test]
        public void GeneratedSwordNormalsRemainFiniteAndUnitLengthForUrpLit()
        {
            ProceduralShortSwordGenerator generator = CreateGenerator();
            int[] seeds = Enumerable.Range(0, 256)
                .Append(5248)
                .ToArray();
            foreach (int seed in seeds)
            {
                generator.GenerateUnrestricted(seed);
                AssertLightingSafeNormals(generator, $"seed {seed}");
            }

            generator.ClearGenerationLocks();
            generator.ToggleGenerationLock(
                ShortSwordGenerationDecision.GripStyle,
                (int)ShortSwordGripStyle.CrossWrappedCord);
            generator.ToggleGenerationLock(
                ShortSwordGenerationDecision.HandleCrossSection,
                (int)ShortSwordHandleCrossSection.OvalFaceted);
            generator.ToggleGenerationLock(
                ShortSwordGenerationDecision.FacetTier,
                (int)ShortSwordFacetTier.Intricate);
            generator.Generate(12520);
            AssertLightingSafeNormals(
                generator,
                "intricate oval cross-wrapped grip");
        }

        [Test]
        public void CounterWovenGripsUseTwoOpposingStrandsFittedToOvalHandles()
        {
            ShortSwordGripStyle[] styles =
            {
                ShortSwordGripStyle.CrossWrappedCord,
                ShortSwordGripStyle.HerringboneCord
            };
            string[][] expectedNames =
            {
                new[]
                {
                    "Cross Cord Clockwise",
                    "Cross Cord Counterclockwise"
                },
                new[]
                {
                    "Herringbone Cord Clockwise",
                    "Herringbone Cord Counterclockwise"
                }
            };
            float[] thicknessScales = { 0.82f, 0.72f };
            ProceduralShortSwordGenerator generator = CreateGenerator();

            for (int styleIndex = 0; styleIndex < styles.Length; styleIndex++)
            {
                generator.ClearGenerationLocks();
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.GripStyle,
                    (int)styles[styleIndex]);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.HandleCrossSection,
                    (int)ShortSwordHandleCrossSection.OvalFaceted);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)ShortSwordFacetTier.Intricate);
                ProceduralShortSwordDefinition sword = generator.Generate(
                    12520 + styleIndex * 47);
                Transform handle = generator.GeneratedParts.Single(part =>
                        part.name ==
                            ProceduralShortSwordGenerator.HandlePartName)
                    .transform;
                MeshFilter[] strands = handle.Cast<Transform>()
                    .Where(child => expectedNames[styleIndex].Contains(
                        child.name))
                    .Select(child => child.GetComponent<MeshFilter>())
                    .ToArray();

                Assert.That(sword.GripStyle, Is.EqualTo(styles[styleIndex]));
                Assert.That(
                    sword.HandleCrossSection,
                    Is.EqualTo(ShortSwordHandleCrossSection.OvalFaceted));
                Assert.That(
                    handle.Cast<Transform>().Select(child => child.name),
                    Is.EquivalentTo(expectedNames[styleIndex]));
                Assert.That(strands, Has.Length.EqualTo(2));

                float[] winding = strands
                    .Select(strand =>
                    {
                        AssertValidHardFacedMesh(
                            strand.sharedMesh,
                            $"{styles[styleIndex]} / {strand.name}");
                        Vector3[] centers = ExtractHelixCenters(
                            strand.sharedMesh,
                            sword.FacetTier);
                        AssertHelixFollowsHandleSurface(
                            sword,
                            centers,
                            strand.name,
                            thicknessScales[styleIndex]);
                        return ResolveHelixWinding(centers);
                    })
                    .ToArray();
                Assert.That(
                    winding.Any(value => value > Mathf.PI * 4f),
                    Is.True,
                    $"{styles[styleIndex]} lost its clockwise strand.");
                Assert.That(
                    winding.Any(value => value < -Mathf.PI * 4f),
                    Is.True,
                    $"{styles[styleIndex]} lost its counterclockwise strand.");

                Vector3[] firstCenters = ExtractHelixCenters(
                    strands[0].sharedMesh,
                    sword.FacetTier);
                Vector3[] secondCenters = ExtractHelixCenters(
                    strands[1].sharedMesh,
                    sword.FacetTier);
                Assert.That(firstCenters, Has.Length.EqualTo(secondCenters.Length));
                Assert.That(
                    Enumerable.Range(0, firstCenters.Length)
                        .Min(index => Vector3.Distance(
                            firstCenters[index],
                            secondCenters[index])),
                    Is.GreaterThan(0.001f),
                    $"{styles[styleIndex]} collapsed both strands onto the " +
                    "same path.");
                AssertAlternatingWeaveClearance(
                    sword,
                    firstCenters,
                    secondCenters,
                    thicknessScales[styleIndex],
                    styles[styleIndex].ToString());
            }
        }

        private static Mesh MeshOf(
            ProceduralShortSwordGenerator generator,
            string partName)
        {
            return generator.GeneratedParts.Single(part =>
                    part.name == partName)
                .GetComponent<MeshFilter>()
                .sharedMesh;
        }

        private static string[] QuantizedVertexKeys(
            Mesh mesh,
            bool mirrorX)
        {
            return mesh.vertices
                .Select(vertex => QuantizedVertexKey(vertex, mirrorX))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        private static string QuantizedVertexKey(
            Vector3 vertex,
            bool mirrorX = false,
            float precision = 100000f)
        {
            float x = mirrorX ? -vertex.x : vertex.x;
            return $"{Mathf.RoundToInt(x * precision)}:" +
                $"{Mathf.RoundToInt(vertex.y * precision)}:" +
                $"{Mathf.RoundToInt(vertex.z * precision)}";
        }

        private static Vector2[] ExtractBladeCenterline(Mesh blade)
        {
            return blade.vertices
                .Where(vertex =>
                    vertex.y > 0.075f &&
                    Mathf.Abs(vertex.z) > 0.00005f)
                .GroupBy(vertex => Mathf.RoundToInt(vertex.y * 100000f))
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    float maximumDepth = group.Max(vertex =>
                        Mathf.Abs(vertex.z));
                    Vector3[] ridge = group.Where(vertex =>
                            Mathf.Abs(
                                Mathf.Abs(vertex.z) - maximumDepth) <
                            0.00001f)
                        .ToArray();
                    return new Vector2(
                        ridge.Average(vertex => vertex.x),
                        ridge.Average(vertex => vertex.y));
                })
                .ToArray();
        }

        private static void AssertClosedOutwardMesh(
            Mesh mesh,
            string context)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var edgeCounts = new Dictionary<string, int>();
            var edgeDirections = new Dictionary<string, int>();
            float signedVolume = 0f;

            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                signedVolume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
                AddDirectedEdge(a, b, edgeCounts, edgeDirections);
                AddDirectedEdge(b, c, edgeCounts, edgeDirections);
                AddDirectedEdge(c, a, edgeCounts, edgeDirections);
            }

            Assert.That(
                edgeCounts.Values.All(count => count == 2),
                Is.True,
                $"{context}: the mesh contains an open or non-manifold edge.");
            Assert.That(
                edgeDirections.Values.All(balance => balance == 0),
                Is.True,
                $"{context}: adjacent faces disagree about outward winding.");
            Assert.That(
                signedVolume,
                Is.GreaterThan(0.000000001f),
                $"{context}: the closed surface is inverted or has no volume.");
        }

        private static void AddDirectedEdge(
            Vector3 from,
            Vector3 to,
            IDictionary<string, int> counts,
            IDictionary<string, int> directions)
        {
            string fromKey = QuantizedVertexKey(
                from,
                precision: 1000000f);
            string toKey = QuantizedVertexKey(
                to,
                precision: 1000000f);
            bool forward = string.CompareOrdinal(fromKey, toKey) < 0;
            string edgeKey = forward
                ? $"{fromKey}|{toKey}"
                : $"{toKey}|{fromKey}";
            counts[edgeKey] = counts.TryGetValue(edgeKey, out int count)
                ? count + 1
                : 1;
            directions[edgeKey] = directions.TryGetValue(
                    edgeKey,
                    out int balance)
                ? balance + (forward ? 1 : -1)
                : forward ? 1 : -1;
        }

        private static Vector3[] ExtractHelixCenters(
            Mesh mesh,
            ShortSwordFacetTier facetTier)
        {
            int sides = facetTier == ShortSwordFacetTier.Intricate ? 6 : 4;
            int verticesPerSegment = sides * 6;
            Assert.That(
                mesh.vertexCount % verticesPerSegment,
                Is.Zero,
                "A woven strand no longer has complete tube rings.");
            int sampleCount = mesh.vertexCount / verticesPerSegment;
            Assert.That(sampleCount, Is.GreaterThan(2));
            Vector3[] vertices = mesh.vertices;
            var centers = new Vector3[sampleCount];

            for (int segment = 0; segment < sampleCount - 1; segment++)
            {
                int segmentStart = segment * verticesPerSegment;
                Vector3 center = Vector3.zero;
                for (int side = 0; side < sides; side++)
                {
                    center += vertices[segmentStart + side * 6];
                }
                centers[segment] = center / sides;
            }

            int finalSegment = (sampleCount - 2) * verticesPerSegment;
            Vector3 finalCenter = Vector3.zero;
            for (int side = 0; side < sides; side++)
            {
                finalCenter += vertices[finalSegment + side * 6 + 5];
            }
            centers[sampleCount - 1] = finalCenter / sides;
            return centers;
        }

        private static void AssertHelixFollowsHandleSurface(
            ProceduralShortSwordDefinition sword,
            IReadOnlyList<Vector3> centers,
            string context,
            float thicknessScale)
        {
            float top = ProceduralShortSwordGenerator.ResolveHandleSeatHeight(
                    sword) -
                0.016f;
            float bottom = -sword.HandleLength + 0.020f;
            Assert.That(centers[0].y, Is.EqualTo(top).Within(0.00001f), context);
            Assert.That(
                centers[centers.Count - 1].y,
                Is.EqualTo(bottom).Within(0.00001f),
                context);
            for (int index = 0; index < centers.Count; index++)
            {
                Vector3 center = centers[index];
                float t = Mathf.InverseLerp(top, bottom, center.y);
                float angle = Mathf.Atan2(center.z, center.x);
                float surfaceRadius =
                    ProceduralShortSwordGenerator.ResolveHandleSurfaceRadius(
                        sword,
                        t);
                float expectedRadius = surfaceRadius;
                if (sword.HandleCrossSection ==
                    ShortSwordHandleCrossSection.OvalFaceted)
                {
                    const float depthScale = 0.76f;
                    float cosine = Mathf.Cos(angle);
                    float sine = Mathf.Sin(angle);
                    expectedRadius = surfaceRadius * depthScale /
                        Mathf.Sqrt(
                            depthScale * depthScale * cosine * cosine +
                            sine * sine);
                }

                Assert.That(
                    t,
                    Is.InRange(0f, 1f),
                    $"{context}: strand center left the grip's height range.");
                if (index > 0)
                {
                    Assert.That(
                        center.y,
                        Is.LessThan(centers[index - 1].y),
                        $"{context}: enriched crossing samples lost their " +
                        "top-to-bottom order.");
                }
                float cordRadius = ResolveExpectedWovenCordRadius(
                    sword,
                    t,
                    thicknessScale);
                float surfaceOffset =
                    new Vector2(center.x, center.z).magnitude -
                    expectedRadius;
                Assert.That(
                    surfaceOffset,
                    Is.InRange(
                        ProceduralShortSwordGenerator.WovenGripRadialOffset -
                            0.00001f,
                        ProceduralShortSwordGenerator.WovenGripRadialOffset +
                            cordRadius * 2f +
                            ProceduralShortSwordGenerator.WovenGripAirGap +
                            ProceduralShortSwordGenerator.
                                WovenGripLowPolyAllowance +
                            0.00001f),
                    $"{context}: strand floated away from or clipped through " +
                    "the handle surface.");
            }
        }

        private static void AssertAlternatingWeaveClearance(
            ProceduralShortSwordDefinition sword,
            IReadOnlyList<Vector3> firstCenters,
            IReadOnlyList<Vector3> secondCenters,
            float thicknessScale,
            string context)
        {
            Assert.That(firstCenters.Count, Is.EqualTo(secondCenters.Count));
            float top = ProceduralShortSwordGenerator.ResolveHandleSeatHeight(
                    sword) -
                0.016f;
            float bottom = -sword.HandleLength + 0.020f;
            var raisedAtCrossings = new List<int>();
            int locallyCheckedSamples = 0;

            for (int index = 0; index < firstCenters.Count; index++)
            {
                Vector3 first = firstCenters[index];
                Vector3 second = secondCenters[index];
                Assert.That(
                    second.y,
                    Is.EqualTo(first.y).Within(0.00001f),
                    $"{context}: paired strands no longer share sample heights.");
                float t = Mathf.InverseLerp(top, bottom, first.y);
                float firstAngle = Mathf.Atan2(first.z, first.x);
                float secondAngle = Mathf.Atan2(second.z, second.x);
                float firstSurface = ResolveExpectedHandleRadiusAtAngle(
                    sword,
                    t,
                    firstAngle);
                float secondSurface = ResolveExpectedHandleRadiusAtAngle(
                    sword,
                    t,
                    secondAngle);
                float firstLift = new Vector2(first.x, first.z).magnitude -
                    firstSurface -
                    ProceduralShortSwordGenerator.WovenGripRadialOffset;
                float secondLift = new Vector2(second.x, second.z).magnitude -
                    secondSurface -
                    ProceduralShortSwordGenerator.WovenGripRadialOffset;
                Assert.That(
                    Mathf.Min(firstLift, secondLift),
                    Is.EqualTo(0f).Within(0.00002f),
                    $"{context}: both strands lifted away from the handle at " +
                    $"sample {index}.");

                float cordRadius = ResolveExpectedWovenCordRadius(
                    sword,
                    t,
                    thicknessScale);
                Vector2 firstBase = new Vector2(
                    Mathf.Cos(firstAngle),
                    Mathf.Sin(firstAngle)) *
                    (firstSurface +
                     ProceduralShortSwordGenerator.WovenGripRadialOffset);
                Vector2 secondBase = new Vector2(
                    Mathf.Cos(secondAngle),
                    Mathf.Sin(secondAngle)) *
                    (secondSurface +
                     ProceduralShortSwordGenerator.WovenGripRadialOffset);
                float paddedClearance = cordRadius * 2f +
                    ProceduralShortSwordGenerator.WovenGripAirGap +
                    ProceduralShortSwordGenerator.WovenGripLowPolyAllowance;
                if (Vector2.Distance(firstBase, secondBase) <=
                    paddedClearance + 0.00001f)
                {
                    locallyCheckedSamples++;
                    Assert.That(
                        Vector3.Distance(first, second),
                        Is.GreaterThanOrEqualTo(
                            cordRadius * 2f +
                            ProceduralShortSwordGenerator.WovenGripAirGap -
                            0.00001f),
                        $"{context}: faceted cord tubes overlap near crossing " +
                        $"sample {index}.");
                }

                float angularSeparation = Mathf.Abs(Mathf.DeltaAngle(
                    firstAngle * Mathf.Rad2Deg,
                    secondAngle * Mathf.Rad2Deg));
                if (angularSeparation <= 0.001f)
                {
                    raisedAtCrossings.Add(firstLift > secondLift ? 0 : 1);
                }
            }

            Assert.That(
                locallyCheckedSamples,
                Is.GreaterThan(4),
                $"{context}: no local crossing-clearance samples were emitted.");
            Assert.That(
                raisedAtCrossings.Count,
                Is.GreaterThan(3),
                $"{context}: exact crossing samples were lost.");
            for (int index = 1; index < raisedAtCrossings.Count; index++)
            {
                Assert.That(
                    raisedAtCrossings[index],
                    Is.Not.EqualTo(raisedAtCrossings[index - 1]),
                    $"{context}: the same strand stayed over at consecutive " +
                    "crossings.");
            }
        }

        private static float ResolveExpectedWovenCordRadius(
            ProceduralShortSwordDefinition sword,
            float normalizedHeight,
            float thicknessScale)
        {
            return Mathf.Clamp(
                ProceduralShortSwordGenerator.ResolveHandleSurfaceRadius(
                    sword,
                    normalizedHeight) *
                0.105f * thicknessScale,
                0.0015f,
                0.0058f);
        }

        private static float ResolveExpectedHandleRadiusAtAngle(
            ProceduralShortSwordDefinition sword,
            float normalizedHeight,
            float angle)
        {
            float radius =
                ProceduralShortSwordGenerator.ResolveHandleSurfaceRadius(
                    sword,
                    normalizedHeight);
            if (sword.HandleCrossSection !=
                ShortSwordHandleCrossSection.OvalFaceted)
            {
                return radius;
            }
            const float depthScale = 0.76f;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            return radius * depthScale /
                Mathf.Sqrt(
                    depthScale * depthScale * cosine * cosine +
                    sine * sine);
        }

        private static float ResolveHelixWinding(
            IReadOnlyList<Vector3> centers)
        {
            float winding = 0f;
            for (int index = 1; index < centers.Count; index++)
            {
                float previous = Mathf.Atan2(
                    centers[index - 1].z,
                    centers[index - 1].x) * Mathf.Rad2Deg;
                float current = Mathf.Atan2(
                    centers[index].z,
                    centers[index].x) * Mathf.Rad2Deg;
                winding += Mathf.DeltaAngle(previous, current) * Mathf.Deg2Rad;
            }
            return winding;
        }

        private static void AssertShortSwordDefinitionBounds(
            ProceduralShortSwordDefinition sword,
            string context)
        {
            Assert.That(sword.BladeLength, Is.InRange(0.94f, 1.08f), context);
            Assert.That(sword.BladeWidth, Is.InRange(0.074f, 0.112f), context);
            Assert.That(sword.BladeThickness, Is.InRange(0.026f, 0.034f), context);
            Assert.That(sword.TipLength, Is.InRange(0.18f, 0.285f), context);
            Assert.That(sword.GuardSpan, Is.InRange(0.255f, 0.375f), context);
            Assert.That(sword.GuardHeight, Is.InRange(0.014f, 0.055f), context);
            Assert.That(sword.HandleLength, Is.InRange(0.205f, 0.260f), context);
            Assert.That(sword.HiltLength, Is.InRange(0.066f, 0.096f), context);
            Assert.That(sword.TotalLength, Is.InRange(1.20f, 1.45f), context);
            Assert.That(
                sword.BladeWidth / sword.BladeLength,
                Is.InRange(0.065f, 0.121f),
                $"{context}: blade proportions drifted toward a rapier or " +
                "an oversized chopping blade.");
            Assert.That(
                sword.BladeLength / sword.TotalLength,
                Is.InRange(0.70f, 0.85f),
                $"{context}: the assembly no longer reads as a short sword.");
            Assert.That(Mathf.Abs(sword.DirectionSign), Is.EqualTo(1), context);
            Assert.That(
                new[] { 4, 6, 8, 10, 12 },
                Does.Contain(sword.GuardCrossSectionSides),
                context);
            Assert.That(
                new[] { 6, 8, 10, 12, 14 },
                Does.Contain(sword.GuardCurveSegments),
                context);
        }

        private void AssertGeneratedSwordBounds(string context)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.That(renderers, Is.Not.Empty, context);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            Assert.That(bounds.size.y, Is.InRange(1.18f, 1.49f), context);
            Assert.That(
                bounds.size.x,
                Is.LessThanOrEqualTo(0.43f),
                $"{context}: lateral furniture exceeded the short-sword " +
                "silhouette envelope.");
            Assert.That(
                bounds.size.z,
                Is.LessThanOrEqualTo(0.12f),
                $"{context}: a detail bled too far out of the blade plane.");
        }

        private static void AssertValidHardFacedMesh(
            Mesh mesh,
            string context)
        {
            Assert.That(mesh, Is.Not.Null, context);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            Assert.That(vertices.Length, Is.GreaterThan(3), context);
            Assert.That(triangles.Length, Is.GreaterThan(3), context);
            Assert.That(triangles.Length % 3, Is.Zero, context);
            Assert.That(
                vertices.Length,
                Is.EqualTo(triangles.Length),
                $"{context}: polygon faces shared smoothed vertices.");
            Assert.That(normals, Has.Length.EqualTo(vertices.Length), context);
            Assert.That(vertices.All(IsFinite), Is.True, context);
            Assert.That(normals.All(IsFinite), Is.True, context);
            Assert.That(IsFinite(mesh.bounds.size), Is.True, context);
            Assert.That(
                mesh.bounds.size.sqrMagnitude,
                Is.GreaterThan(0.000001f),
                context);

            for (int index = 0; index < triangles.Length; index += 3)
            {
                int first = triangles[index];
                int second = triangles[index + 1];
                int third = triangles[index + 2];
                Assert.That(first, Is.InRange(0, vertices.Length - 1), context);
                Assert.That(second, Is.InRange(0, vertices.Length - 1), context);
                Assert.That(third, Is.InRange(0, vertices.Length - 1), context);
                Vector3 cross = Vector3.Cross(
                    vertices[second] - vertices[first],
                    vertices[third] - vertices[first]);
                Assert.That(
                    cross.sqrMagnitude,
                    Is.GreaterThan(0.000000000001f),
                    $"{context}: degenerate triangle {index / 3}.");
                Vector3 faceNormal = cross /
                    Mathf.Sqrt(cross.sqrMagnitude);
                Assert.That(normals[second], Is.EqualTo(normals[first]), context);
                Assert.That(normals[third], Is.EqualTo(normals[first]), context);
                Assert.That(
                    normals[first].sqrMagnitude,
                    Is.EqualTo(1f).Within(0.00001f),
                    $"{context}: triangle {index / 3} has a zero or " +
                    "non-unit normal that can destabilize URP lighting.");
                Assert.That(
                    Vector3.Dot(normals[first], faceNormal),
                    Is.GreaterThan(0.9999f),
                    $"{context}: triangle {index / 3} does not own its " +
                    "geometric hard-face normal.");
            }
        }

        private static void AssertLightingSafeNormals(
            ProceduralShortSwordGenerator generator,
            string context)
        {
            foreach (MeshFilter filter in
                     generator.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh, Is.Not.Null, $"{context} / {filter.name}");
                Vector3[] normals = mesh.normals;
                Assert.That(
                    normals,
                    Has.Length.EqualTo(mesh.vertexCount),
                    $"{context} / {filter.name}");
                for (int index = 0; index < normals.Length; index++)
                {
                    Vector3 normal = normals[index];
                    Assert.That(
                        IsFinite(normal),
                        Is.True,
                        $"{context} / {filter.name} normal {index} is not finite.");
                    Assert.That(
                        normal.sqrMagnitude,
                        Is.EqualTo(1f).Within(0.00001f),
                        $"{context} / {filter.name} normal {index} can " +
                        "produce invalid URP lighting.");
                }
            }
        }

        private static void AssertGuardBindingFitsEnvelope(
            ProceduralShortSwordDefinition sword,
            Mesh guardMesh,
            MeshFilter wrap)
        {
            bool left = wrap.name.StartsWith("Left", StringComparison.Ordinal);
            float sideSpan = left
                ? Mathf.Abs(guardMesh.bounds.min.x)
                : guardMesh.bounds.max.x;
            Assert.That(sideSpan, Is.GreaterThan(0.01f));
            bool intersectsCore = false;
            foreach (Vector3 vertex in wrap.sharedMesh.vertices)
            {
                Assert.That(
                    left ? vertex.x : -vertex.x,
                    Is.LessThan(0f),
                    $"{wrap.name} crossed onto the opposite guard arm.");
                float armT = Mathf.Abs(vertex.x) / sideSpan;
                Assert.That(
                    armT,
                    Is.InRange(0.47f, 0.74f),
                    $"{wrap.name} escaped its authored middle-arm band.");
                Assert.That(
                    Mathf.Abs(vertex.x),
                    Is.GreaterThan(sword.BladeWidth * 0.5f),
                    $"{wrap.name} bled into the blade/handle joint.");

                ProceduralShortSwordGenerator.ResolveGuardVerticalEnvelopeAtX(
                    sword,
                    vertex.x,
                    out float bottom,
                    out float top);
                Assert.That(
                    vertex.y,
                    Is.InRange(bottom - 0.0021f, top + 0.0021f),
                    $"{wrap.name} floated beyond the guard's vertical face.");
                float halfDepth = sword.GuardDepth * 0.5f *
                    Mathf.Lerp(1f, 0.72f, armT);
                Assert.That(
                    Mathf.Abs(vertex.z),
                    Is.LessThanOrEqualTo(halfDepth + 0.0021f),
                    $"{wrap.name} floated beyond the guard's front/rear face.");
                intersectsCore |= vertex.y >= bottom && vertex.y <= top &&
                    Mathf.Abs(vertex.z) <= halfDepth;
            }

            Assert.That(
                intersectsCore,
                Is.True,
                $"{wrap.name} must sleeve the guard rather than float above it.");
            Assert.That(
                wrap.sharedMesh.bounds.min.y,
                Is.GreaterThanOrEqualTo(guardMesh.bounds.min.y - 0.0021f));
            Assert.That(
                wrap.sharedMesh.bounds.max.y,
                Is.LessThanOrEqualTo(guardMesh.bounds.max.y + 0.0021f));
            Assert.That(
                wrap.sharedMesh.bounds.min.z,
                Is.GreaterThanOrEqualTo(guardMesh.bounds.min.z - 0.0021f));
            Assert.That(
                wrap.sharedMesh.bounds.max.z,
                Is.LessThanOrEqualTo(guardMesh.bounds.max.z + 0.0021f));
        }

        private static int ExpectedGuardBindingDetailCount(
            ShortSwordGuardBindingStyle style)
        {
            return style switch
            {
                ShortSwordGuardBindingStyle.LeftLeather => 3,
                ShortSwordGuardBindingStyle.RightLeather => 3,
                ShortSwordGuardBindingStyle.BothArms => 6,
                ShortSwordGuardBindingStyle.LeftCord => 4,
                ShortSwordGuardBindingStyle.RightCord => 4,
                _ => 0
            };
        }

        private static int ExpectedGuardBindingSideCount(
            ShortSwordGuardBindingStyle style,
            bool left)
        {
            return style switch
            {
                ShortSwordGuardBindingStyle.BothArms => 3,
                ShortSwordGuardBindingStyle.LeftLeather when left => 3,
                ShortSwordGuardBindingStyle.RightLeather when !left => 3,
                ShortSwordGuardBindingStyle.LeftCord when left => 4,
                ShortSwordGuardBindingStyle.RightCord when !left => 4,
                _ => 0
            };
        }

        private static bool IsGuardBindingName(string objectName)
        {
            return objectName.Contains("Guard Leather Wrap") ||
                objectName.Contains("Guard Cord Wrap");
        }

        private static bool RequiresDirectionalFamily(
            ShortSwordGuardConstruction construction)
        {
            IReadOnlyList<ShortSwordFamily> families =
                ShortSwordGenerationBranchCatalog.GetCompatibleFamilies(
                    ShortSwordGenerationDecision.GuardConstruction,
                    (int)construction);
            return families.Count > 0 && families.All(family =>
            {
                IReadOnlyList<int> directionality =
                    ShortSwordGenerationBranchCatalog.GetCandidateValues(
                        family,
                        ShortSwordGenerationDecision.Directionality);
                return directionality.Contains(
                        (int)ShortSwordDirectionality.Directional) &&
                    !directionality.Contains(
                        (int)ShortSwordDirectionality.Conventional);
            });
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private ProceduralShortSwordGenerator CreateGenerator()
        {
            root = new GameObject("Procedural Sword Test");
            return root.AddComponent<ProceduralShortSwordGenerator>();
        }

        private static Bounds BoundsOf(
            ProceduralShortSwordGenerator generator,
            string partName)
        {
            return generator.GeneratedParts
                .Single(part => part.name == partName)
                .GetComponent<MeshFilter>()
                .sharedMesh.bounds;
        }

        private static float SurfaceArea(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float area = 0f;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return area;
        }
    }
}
