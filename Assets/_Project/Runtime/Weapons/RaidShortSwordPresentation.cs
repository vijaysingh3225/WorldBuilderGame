using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Weapons
{
    /// <summary>
    /// Replaces a legacy short-sword visual while keeping its authored socket,
    /// combat timing, and attachment transform intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidShortSwordPresentation : MonoBehaviour
    {
        public const float LegacyAverageLength = 1.215f;
        public const float LegacyGripCenterHeight = 0.09f;

        [SerializeField] private int seed;
        [SerializeField] private float targetLength = LegacyAverageLength;
        [SerializeField] private ProceduralShortSwordGenerator generator;
        [SerializeField] private Transform bladeHitbox;
        [SerializeField] private float bladeLength;
        [SerializeField] private float bladeRadius;
        [SerializeField] private float gripCenterHeight;
        [SerializeField] private ShortSwordCombatProfile combatProfile =
            ShortSwordCombatProfile.Default;
        private float nextLightingSafetyCheckAt;
        private bool enforcingLightingSafety;

        public int Seed => seed;
        public ProceduralShortSwordGenerator Generator => generator;
        public Transform BladeHitbox => bladeHitbox;
        public float BladeLength => bladeLength;
        public float BladeRadius => bladeRadius;
        public float GripCenterHeight => gripCenterHeight;
        public ShortSwordCombatProfile CombatProfile =>
            combatProfile.IsValid
                ? combatProfile
                : ShortSwordCombatProfile.Default;

        public static RaidShortSwordPresentation Replace(
            Transform swordRoot,
            int newSeed,
            float desiredLength = LegacyAverageLength,
            Material bladeOverride = null,
            Material guardOverride = null,
            Material gripOverride = null)
        {
            if (swordRoot == null)
            {
                return null;
            }

            RaidShortSwordPresentation existing =
                swordRoot.GetComponent<RaidShortSwordPresentation>();
            if (existing != null)
            {
                bool missingGenerator = existing.generator == null;
                bool needsMaterialMigration =
                    !missingGenerator &&
                    !existing.HasControlledWorldMaterials();
                bool needsRegeneration = missingGenerator ||
                    needsMaterialMigration ||
                    existing.seed != newSeed ||
                    !existing.generator.HasGeneratedSword ||
                    Mathf.Abs(
                        existing.targetLength -
                        Mathf.Max(0.01f, desiredLength)) > 0.0001f;
                if (missingGenerator)
                {
                    Material rebuildBlade = bladeOverride ??
                        FindMaterial(swordRoot, "blade");
                    Material rebuildGuard = guardOverride ??
                        FindMaterial(swordRoot, "guard", "pommel");
                    Material rebuildGrip = gripOverride ??
                        FindMaterial(swordRoot, "grip", "handle");
                    existing.RebuildGenerator(
                        rebuildBlade,
                        rebuildGuard,
                        rebuildGrip);
                }
                else if (needsMaterialMigration)
                {
                    // Existing instances can survive a script reload or an
                    // actor-pool reuse. Recreate their private world
                    // materials instead of leaving the historical Simple Lit
                    // material attached to a still-valid presentation.
                    existing.generator.ConfigureMaterials(
                        null,
                        null,
                        null,
                        null,
                        useProceduralPalette: true);
                }
                if (needsRegeneration)
                {
                    existing.Regenerate(
                        newSeed,
                        desiredLength);
                }
                existing.EnforceLightingSafety();
                return existing;
            }

            Material blade = bladeOverride ??
                FindMaterial(swordRoot, "blade");
            Material guard = guardOverride ??
                FindMaterial(swordRoot, "guard", "pommel");
            Material grip = gripOverride ??
                FindMaterial(swordRoot, "grip", "handle");
            HideLegacyParts(swordRoot);

            RaidShortSwordPresentation presentation =
                swordRoot.gameObject.AddComponent<RaidShortSwordPresentation>();
            presentation.seed = newSeed;
            presentation.targetLength = Mathf.Max(0.01f, desiredLength);
            presentation.CreateGenerator(blade, guard, grip);
            presentation.Regenerate(newSeed, desiredLength);
            return presentation;
        }

        private void OnEnable()
        {
            EnforceLightingSafety();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextLightingSafetyCheckAt)
            {
                return;
            }

            nextLightingSafetyCheckAt = Time.unscaledTime + 0.1f;
            EnforceLightingSafety();
        }

        /// <summary>
        /// Maintains the raid-sword lighting invariant even if pooled content
        /// or another presentation system changes a renderer after spawn.
        /// </summary>
        public void EnforceLightingSafety()
        {
            if (enforcingLightingSafety)
            {
                return;
            }

            enforcingLightingSafety = true;
            try
            {
                DisableLocalEffects(transform);
                if (generator != null &&
                    generator.HasGeneratedSword &&
                    !HasControlledWorldMaterials())
                {
                    generator.ConfigureMaterials(
                        null,
                        null,
                        null,
                        null,
                        useProceduralPalette: true);
                    Regenerate(seed, targetLength);
                }
                SuppressWeaponEmission(transform);
            }
            finally
            {
                enforcingLightingSafety = false;
            }
        }

        private void RebuildGenerator(
            Material blade,
            Material guard,
            Material grip)
        {
            HideLegacyParts(transform);
            generator = null;
            bladeHitbox = null;
            CreateGenerator(blade, guard, grip);
        }

        private void CreateGenerator(
            Material blade,
            Material guard,
            Material grip)
        {
            GameObject generatedRoot =
                new GameObject("Generated Raid Short Sword");
            generatedRoot.layer = gameObject.layer;
            generatedRoot.transform.SetParent(transform, false);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generator =
                generatedRoot.AddComponent<ProceduralShortSwordGenerator>();
            generator.ConfigureMaterials(
                blade,
                guard,
                grip,
                guard,
                useProceduralPalette: true);
        }

        private void Regenerate(int newSeed, float desiredLength)
        {
            if (generator == null)
            {
                return;
            }

            seed = newSeed;
            targetLength = Mathf.Max(0.01f, desiredLength);
            ProceduralShortSwordDefinition definition =
                generator.GenerateUnrestricted(newSeed);
            combatProfile = definition.CombatProfile.IsValid
                ? definition.CombatProfile
                : ProceduralShortSwordGenerator.CalculateCombatProfile(
                    definition);
            float scale = targetLength /
                Mathf.Max(0.01f, definition.TotalLength);
            Transform generatedRoot = generator.transform;
            generatedRoot.localScale = Vector3.one * scale;
            float handleCenter = (
                ProceduralShortSwordGenerator.ResolveHandleSeatHeight(
                    definition) - definition.HandleLength) * 0.5f;
            float generatedOffset = LegacyGripCenterHeight -
                handleCenter * scale;
            generatedRoot.transform.localPosition =
                Vector3.up * generatedOffset;
            gripCenterHeight = generatedOffset +
                handleCenter * scale;
            if (bladeHitbox != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(bladeHitbox.gameObject);
                }
                else
                {
                    DestroyImmediate(bladeHitbox.gameObject);
                }
                bladeHitbox = null;
            }
            CreateBladeHitbox(
                definition,
                generatedOffset,
                scale);
            SuppressWeaponEmission(transform);
        }

        public void ConfigureMeleeWeapon(MeleeWeapon weapon)
        {
            if (weapon != null && bladeHitbox != null)
            {
                if (!combatProfile.IsValid &&
                    generator != null &&
                    generator.HasGeneratedSword)
                {
                    ProceduralShortSwordDefinition definition =
                        generator.CurrentDefinition;
                    combatProfile = definition.CombatProfile.IsValid
                        ? definition.CombatProfile
                        : ProceduralShortSwordGenerator.
                            CalculateCombatProfile(definition);
                }
                weapon.ConfigureBlade(bladeHitbox, bladeLength, bladeRadius);
                weapon.ConfigureGeneratedCombatProfile(CombatProfile);
                ShortSwordAttackPresenter attackPresenter =
                    weapon.GetComponentInChildren<
                        ShortSwordAttackPresenter>(true);
                attackPresenter?.ConfigureGeneratedCombatProfile(
                    CombatProfile);
            }
        }

        private void CreateBladeHitbox(
            ProceduralShortSwordDefinition definition,
            float generatedOffset,
            float generatedScale)
        {
            GameObject hitbox = new GameObject("Generated Blade Hitbox");
            hitbox.layer = gameObject.layer;
            bladeHitbox = hitbox.transform;
            bladeHitbox.SetParent(transform, false);
            bladeHitbox.localPosition = Vector3.up * (
                generatedOffset +
                ProceduralShortSwordGenerator.ResolveBladeSeatHeightAtX(
                    definition,
                    0f) * generatedScale);
            bladeHitbox.localRotation = Quaternion.identity;
            bladeHitbox.localScale = Vector3.one;
            bladeLength = Mathf.Max(
                0.1f,
                (definition.BladeLength -
                    ProceduralShortSwordGenerator.ResolveBladeSeatHeightAtX(
                        definition,
                        0f)) * generatedScale);
            bladeRadius = Mathf.Max(
                0.005f,
                definition.BladeWidth * generatedScale * 0.5f);
        }

        private static Material FindMaterial(
            Transform root,
            params string[] preferredNames)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                string name = renderer.name.ToLowerInvariant();
                for (int nameIndex = 0;
                     nameIndex < preferredNames.Length;
                     nameIndex++)
                {
                    if (name.Contains(preferredNames[nameIndex]) &&
                        renderer.sharedMaterial != null)
                    {
                        return renderer.sharedMaterial;
                    }
                }
            }
            return renderers.Length > 0
                ? renderers[0].sharedMaterial
                : null;
        }

        private bool HasControlledWorldMaterials()
        {
            if (generator == null || !generator.HasGeneratedSword)
            {
                return false;
            }

            Renderer[] renderers =
                generator.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (!IsControlledWorldMaterial(renderers[index].sharedMaterial))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsControlledWorldMaterial(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }
            return material.shader.name ==
                    ProceduralShortSwordGenerator.WorldShaderName &&
                material.name.StartsWith(
                    ProceduralShortSwordGenerator.WorldMaterialName,
                    System.StringComparison.Ordinal);
        }

        private static void HideLegacyParts(Transform root)
        {
            // A few legacy weapon prefabs put their renderer (and sometimes
            // a light) on the socket root rather than a child. Destroying
            // only children left those renderers alive underneath the new
            // procedural sword, where their authored emission could flood
            // the screen. Disable every legacy visual first.
            foreach (Renderer renderer in
                     root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            DisableLocalEffects(root);

            for (int index = root.childCount - 1; index >= 0; index--)
            {
                GameObject child = root.GetChild(index).gameObject;
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

        private static void DisableLocalEffects(Transform root)
        {
            foreach (Light light in
                     root.GetComponentsInChildren<Light>(true))
            {
                light.intensity = 0f;
                light.range = 0f;
                light.enabled = false;
            }
            foreach (ParticleSystem particles in
                     root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.gameObject.SetActive(false);
            }
        }

        private static void SuppressWeaponEmission(Transform root)
        {
            foreach (Renderer renderer in
                     root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_EmissionColor", Color.black);
                properties.SetTexture("_EmissionMap", Texture2D.blackTexture);
                properties.SetFloat("_EmissionIntensity", 0f);
                properties.SetFloat("_EmissiveIntensity", 0f);
                properties.SetFloat("_ClearCoatMask", 0f);
                properties.SetFloat("_ClearCoatSmoothness", 0f);
                renderer.SetPropertyBlock(properties);
                renderer.reflectionProbeUsage =
                    UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
                renderer.lightProbeUsage =
                    UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            }
        }
    }
}
