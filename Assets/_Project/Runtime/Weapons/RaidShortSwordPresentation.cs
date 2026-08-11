using UnityEngine;
using WorldBuilder.Gameplay.Combat;

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

        public int Seed => seed;
        public ProceduralShortSwordGenerator Generator => generator;
        public Transform BladeHitbox => bladeHitbox;
        public float BladeLength => bladeLength;
        public float BladeRadius => bladeRadius;
        public float GripCenterHeight => gripCenterHeight;

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

            GameObject generatedRoot =
                new GameObject("Generated Raid Short Sword");
            generatedRoot.layer = swordRoot.gameObject.layer;
            generatedRoot.transform.SetParent(swordRoot, false);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            presentation.generator =
                generatedRoot.AddComponent<ProceduralShortSwordGenerator>();
            presentation.generator.ConfigureMaterials(blade, guard, grip, guard);
            ProceduralShortSwordDefinition definition =
                presentation.generator.Generate(newSeed);
            float scale = presentation.targetLength /
                Mathf.Max(0.01f, definition.TotalLength);
            generatedRoot.transform.localScale = Vector3.one * scale;
            float handleCenter = (
                ProceduralShortSwordGenerator.ResolveHandleSeatHeight(
                    definition) - definition.HandleLength) * 0.5f;
            float generatedOffset = LegacyGripCenterHeight -
                handleCenter * scale;
            generatedRoot.transform.localPosition =
                Vector3.up * generatedOffset;
            presentation.gripCenterHeight = generatedOffset +
                handleCenter * scale;
            presentation.CreateBladeHitbox(
                definition,
                generatedOffset,
                scale);
            return presentation;
        }

        public void ConfigureMeleeWeapon(MeleeWeapon weapon)
        {
            if (weapon != null && bladeHitbox != null)
            {
                weapon.ConfigureBlade(bladeHitbox, bladeLength, bladeRadius);
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

        private static void HideLegacyParts(Transform root)
        {
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
    }
}
