using System;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    /// <summary>
    /// Gives a non-player Combat Lab actor a random Column Blade when play
    /// begins. The player's blade remains owned by CombatLabSwordForge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatLabColumnBladeLoadout : MonoBehaviour
    {
        [SerializeField] private Transform swordRoot;
        [SerializeField] private MeleeWeapon meleeWeapon;
        [SerializeField] private Material stoneMaterial;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Material obsidianMaterial;
        [SerializeField] private Material furnitureMaterial;
        [SerializeField] private Material accentMaterial;

        public CombatLabColumnBladePresentation Presentation { get; private set; }

        public void Configure(
            Transform weaponRoot,
            MeleeWeapon weapon,
            Material stone,
            Material wood,
            Material obsidian,
            Material furniture,
            Material accent)
        {
            swordRoot = weaponRoot;
            meleeWeapon = weapon;
            stoneMaterial = stone;
            woodMaterial = wood;
            obsidianMaterial = obsidian;
            furnitureMaterial = furniture;
            accentMaterial = accent;
        }

        private void Start()
        {
            Generate(Guid.NewGuid().GetHashCode());
        }

        public bool Generate(int seed)
        {
            if (swordRoot == null)
            {
                return false;
            }

            Presentation = CombatLabColumnBladePresentation.Replace(
                swordRoot,
                seed,
                stoneMaterial,
                woodMaterial,
                obsidianMaterial,
                furnitureMaterial,
                accentMaterial);
            if (Presentation == null)
            {
                return false;
            }
            Presentation.ConfigureMeleeWeapon(meleeWeapon);
            return true;
        }
    }
}
