using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Weapons
{
    /// <summary>
    /// Fits a generated Column Blade into the Combat Lab's existing sword
    /// socket and combat contracts without changing the authored attack,
    /// block, sheathe, or two-slot presentation systems. Combat Lab owns this
    /// adapter; raid and inventory sword presentation remain unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatLabColumnBladePresentation : MonoBehaviour
    {
        public const float LegacyAverageLength = 1.215f;
        public const float LegacyGripCenterHeight = 0.09f;

        [SerializeField] private int seed;
        [SerializeField] private float targetLength = LegacyAverageLength;
        [SerializeField] private ColumnBladeMaterial bladeMaterial;
        [SerializeField] private ProceduralColumnBladeGenerator generator;
        [SerializeField] private Transform bladeHitbox;
        [SerializeField] private float bladeLength;
        [SerializeField] private float bladeRadius;
        [SerializeField] private float gripCenterHeight;
        [SerializeField] private ShortSwordCombatProfile combatProfile =
            ShortSwordCombatProfile.Default;

        [SerializeField] private Material stoneMaterial;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Material obsidianMaterial;
        [SerializeField] private Material furnitureMaterial;
        [SerializeField] private Material accentMaterial;

        public int Seed => seed;
        public ColumnBladeMaterial BladeMaterial => bladeMaterial;
        public ProceduralColumnBladeGenerator Generator => generator;
        public Transform BladeSource => bladeHitbox;
        public Transform BladeHitbox => bladeHitbox;
        public float BladeLength => bladeLength;
        public float BladeRadius => bladeRadius;
        public float GripCenterHeight => gripCenterHeight;
        public ShortSwordCombatProfile CombatProfile =>
            combatProfile.IsValid
                ? combatProfile
                : ShortSwordCombatProfile.Default;

        public static CombatLabColumnBladePresentation Replace(
            Transform swordRoot,
            int newSeed,
            Material stone,
            Material wood,
            Material obsidian,
            Material furniture,
            Material accent,
            float desiredLength = LegacyAverageLength)
        {
            if (swordRoot == null)
            {
                return null;
            }

            CombatLabColumnBladePresentation existing =
                swordRoot.GetComponent<CombatLabColumnBladePresentation>();
            if (existing != null)
            {
                existing.ConfigureMaterials(
                    stone,
                    wood,
                    obsidian,
                    furniture,
                    accent);
                existing.Regenerate(newSeed, desiredLength);
                return existing;
            }

            Material fallback = FindMaterial(swordRoot);
            RaidShortSwordPresentation oldPresentation =
                swordRoot.GetComponent<RaidShortSwordPresentation>();
            if (oldPresentation != null)
            {
                oldPresentation.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(oldPresentation);
                }
                else
                {
                    DestroyImmediate(oldPresentation);
                }
            }
            HideLegacyParts(swordRoot);

            var presentation = swordRoot.gameObject.AddComponent<
                CombatLabColumnBladePresentation>();
            presentation.ConfigureMaterials(
                stone != null ? stone : fallback,
                wood != null ? wood : fallback,
                obsidian != null ? obsidian : fallback,
                furniture != null ? furniture : fallback,
                accent != null ? accent : furniture ?? fallback);
            presentation.CreateGenerator();
            presentation.Regenerate(newSeed, desiredLength);
            return presentation;
        }

        public static ColumnBladeMaterial ResolveBladeMaterial(int seed)
        {
            uint mixed = unchecked((uint)seed * 2654435761u + 2246822519u);
            return (ColumnBladeMaterial)(mixed % 3u);
        }

        public static ShortSwordCombatProfile CalculateCombatProfile(
            ProceduralColumnBladeDefinition definition)
        {
            float width = Mathf.InverseLerp(
                0.072f,
                0.166f,
                definition.BladeWidth);
            float thickness = Mathf.InverseLerp(
                0.011f,
                0.085f,
                definition.BladeThickness);
            float length = Mathf.InverseLerp(
                0.76f,
                0.94f,
                definition.BladeLength);
            float materialHeft = definition.BladeMaterial switch
            {
                ColumnBladeMaterial.Stone => 0.10f,
                ColumnBladeMaterial.Wood => -0.10f,
                _ => 0.03f
            };
            float heft = Mathf.Clamp01(
                width * 0.32f +
                thickness * 0.43f +
                length * 0.15f +
                0.10f +
                materialHeft);
            var qualityRandom = new System.Random(unchecked(
                definition.Seed * 1103515245 + 0x31B7D1));
            float quality = Mathf.Lerp(
                0.28f,
                0.88f,
                (float)qualityRandom.NextDouble());
            float handling = Mathf.Clamp01(
                0.91f - heft * 0.66f + quality * 0.13f);
            return new ShortSwordCombatProfile
            {
                CraftQuality = quality,
                Heft = heft,
                Handling = handling,
                DamageMultiplier = Mathf.Lerp(0.78f, 1.43f, heft),
                AttackSpeedMultiplier = Mathf.Lerp(0.76f, 1.30f, handling),
                HitPauseDuration = Mathf.Lerp(0.018f, 0.115f, heft),
                StaggerDuration = Mathf.Lerp(0.12f, 0.49f, heft),
                ImpactShakeMultiplier = Mathf.Lerp(0.62f, 1.92f, heft),
                SwingPitchMultiplier = Mathf.Lerp(1.18f, 0.82f, heft),
                SwingVolumeMultiplier = Mathf.Lerp(0.86f, 1.31f, heft),
                TrailPersistenceMultiplier = Mathf.Lerp(1.42f, 0.78f, heft),
                TrailOpacityMultiplier = Mathf.Lerp(0.88f, 1.18f, heft)
            };
        }

        public void ConfigureMeleeWeapon(MeleeWeapon weapon)
        {
            if (weapon == null || bladeHitbox == null)
            {
                return;
            }

            ProceduralColumnBladeDefinition definition =
                generator.CurrentDefinition;
            float bladeBottom = -definition.GuardHeight * 0.16f;
            float bladeTopCenter = bladeBottom + definition.BladeLength -
                definition.TopSlantRise * 0.5f;
            weapon.ConfigureBladeSegment(
                bladeHitbox,
                Vector3.up * bladeBottom,
                Vector3.up * bladeTopCenter,
                bladeRadius);
            weapon.ConfigureGeneratedCombatProfile(CombatProfile);
            ShortSwordAttackPresenter attackPresenter =
                weapon.GetComponentInChildren<ShortSwordAttackPresenter>(true);
            attackPresenter?.ConfigureGeneratedCombatProfile(CombatProfile);
        }

        private void ConfigureMaterials(
            Material stone,
            Material wood,
            Material obsidian,
            Material furniture,
            Material accent)
        {
            stoneMaterial = stone;
            woodMaterial = wood;
            obsidianMaterial = obsidian;
            furnitureMaterial = furniture;
            accentMaterial = accent;
            generator?.ConfigureMaterials(
                stoneMaterial,
                woodMaterial,
                obsidianMaterial,
                furnitureMaterial,
                accentMaterial);
        }

        private void CreateGenerator()
        {
            GameObject generatedRoot = new GameObject(
                "Generated Combat Lab Column Blade");
            generatedRoot.layer = gameObject.layer;
            generatedRoot.transform.SetParent(transform, false);
            generator = generatedRoot.AddComponent<
                ProceduralColumnBladeGenerator>();
            generator.ConfigureMaterials(
                stoneMaterial,
                woodMaterial,
                obsidianMaterial,
                furnitureMaterial,
                accentMaterial);
        }

        private void Regenerate(int newSeed, float desiredLength)
        {
            if (generator == null)
            {
                CreateGenerator();
            }

            seed = newSeed;
            targetLength = Mathf.Max(0.01f, desiredLength);
            bladeMaterial = ResolveBladeMaterial(newSeed);
            generator.SetBladeMaterial(bladeMaterial, false);
            ProceduralColumnBladeDefinition definition =
                generator.Generate(newSeed);
            combatProfile = CalculateCombatProfile(definition);

            float scale = targetLength /
                Mathf.Max(0.01f, definition.AssembledLength);
            float handleTop = -definition.GuardHeight * 0.28f;
            float handleCenter = handleTop - definition.HandleLength * 0.5f;
            float generatedOffset = LegacyGripCenterHeight -
                handleCenter * scale;
            generator.transform.localScale = Vector3.one * scale;
            generator.transform.localPosition = Vector3.up * generatedOffset;
            generator.transform.localRotation = Quaternion.identity;
            gripCenterHeight = generatedOffset + handleCenter * scale;

            ResolveBladeSource(definition, scale);
            SetLayerRecursively(generator.transform, gameObject.layer);
            SuppressWeaponEmission(transform);
        }

        private void ResolveBladeSource(
            ProceduralColumnBladeDefinition definition,
            float generatedScale)
        {
            GameObject visibleBlade = null;
            foreach (GameObject part in generator.GeneratedParts)
            {
                if (part != null &&
                    part.name == ProceduralColumnBladeGenerator.BladePartName)
                {
                    visibleBlade = part;
                    break;
                }
            }

            float bladeBottom = -definition.GuardHeight * 0.16f;
            float bladeTopCenter = bladeBottom + definition.BladeLength -
                definition.TopSlantRise * 0.5f;
            bladeHitbox = visibleBlade != null
                ? visibleBlade.transform
                : null;
            bladeLength = Mathf.Max(
                0.1f,
                (bladeTopCenter - bladeBottom) * generatedScale);
            bladeRadius = Mathf.Max(
                0.005f,
                Mathf.Max(
                    definition.BladeWidth,
                    definition.BladeThickness) *
                generatedScale * 0.5f);
        }

        private static Material FindMaterial(Transform root)
        {
            Renderer renderer = root.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static void HideLegacyParts(Transform root)
        {
            foreach (Renderer renderer in
                     root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
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

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int index = 0; index < root.childCount; index++)
            {
                SetLayerRecursively(root.GetChild(index), layer);
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
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
