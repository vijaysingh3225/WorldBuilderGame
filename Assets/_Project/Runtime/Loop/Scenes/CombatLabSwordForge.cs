using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    /// <summary>
    /// Gives the Combat Lab a fresh generated sword on entry and turns the
    /// wall-mounted sword silhouette into an unrestricted reroll station.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatLabSwordForge : MonoBehaviour
    {
        public const string StationName = "Sword Silhouette Forge";
        public const string PromptText = "Generate New Column Blade";
        public const float DefaultInteractionDistance = 2.25f;

        [SerializeField, Min(0.5f)] private float interactionDistance =
            DefaultInteractionDistance;
        [SerializeField] private Transform player;
        [SerializeField] private PlayerInputSource playerInput;
        [SerializeField] private TwoSlotWeaponPresenter weaponPresenter;
        [SerializeField] private MeleeWeapon meleeWeapon;
        [SerializeField] private Material columnStoneMaterial;
        [SerializeField] private Material columnWoodMaterial;
        [SerializeField] private Material columnObsidianMaterial;
        [SerializeField] private Material columnFurnitureMaterial;
        [SerializeField] private Material columnAccentMaterial;
        [SerializeField] private bool generateOnStart = true;

        public int CurrentSeed { get; private set; }
        public int GenerationCount { get; private set; }
        public bool HasGeneratedSword { get; private set; }
        public bool GeneratesOnStart => generateOnStart;
        public bool CanInteract => ResolveCanInteract();

        public void Configure(
            Transform playerTransform,
            PlayerInputSource input,
            TwoSlotWeaponPresenter presenter,
            MeleeWeapon weapon,
            bool generateWhenPlayStarts = true)
        {
            player = playerTransform;
            playerInput = input;
            weaponPresenter = presenter;
            meleeWeapon = weapon;
            generateOnStart = generateWhenPlayStarts;
        }

        public void ConfigureColumnBladeMaterials(
            Material stone,
            Material wood,
            Material obsidian,
            Material furniture,
            Material accent)
        {
            columnStoneMaterial = stone;
            columnWoodMaterial = wood;
            columnObsidianMaterial = obsidian;
            columnFurnitureMaterial = furniture;
            columnAccentMaterial = accent;
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateRandomSword();
            }
        }

        private void Update()
        {
            if (!ResolveCanInteract())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                PlayerControlBindings.WasPressedThisFrame(
                    keyboard,
                    PlayerControl.Interact))
            {
                GenerateRandomSword();
            }
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint &&
                ResolveCanInteract())
            {
                LootInteractionPresentation.DrawPrompt(PromptText);
            }
        }

        public bool GenerateRandomSword()
        {
            int seed;
            do
            {
                seed = Guid.NewGuid().GetHashCode();
            }
            while (HasGeneratedSword && seed == CurrentSeed);

            return GenerateSword(seed);
        }

        public bool GenerateSword(int seed)
        {
            ResolvePlayerReferences();
            Transform swordRoot = weaponPresenter != null
                ? weaponPresenter.PrimaryWeaponRoot
                : null;
            if (swordRoot == null || meleeWeapon == null)
            {
                return false;
            }

            ShortSwordAttackPresenter attackPresenter =
                meleeWeapon.GetComponentInChildren<
                    ShortSwordAttackPresenter>(true);
            attackPresenter?.InterruptForWeaponReplacement();
            meleeWeapon.EndSwing();

            CombatLabColumnBladePresentation presentation =
                CombatLabColumnBladePresentation.Replace(
                    swordRoot,
                    seed,
                    columnStoneMaterial,
                    columnWoodMaterial,
                    columnObsidianMaterial,
                    columnFurnitureMaterial,
                    columnAccentMaterial);
            if (presentation == null)
            {
                return false;
            }

            presentation.ConfigureMeleeWeapon(meleeWeapon);
            CurrentSeed = seed;
            GenerationCount++;
            HasGeneratedSword = true;
            return true;
        }

        private bool ResolveCanInteract()
        {
            ResolvePlayerReferences();
            return player != null &&
                (playerInput == null ||
                 !playerInput.UserInterfaceCaptureActive) &&
                LootInteractionPresentation.IsFocused(
                    player,
                    transform,
                    interactionDistance,
                    allowRendererBoundsFallback: false);
        }

        private void ResolvePlayerReferences()
        {
            if (player == null)
            {
                GameObject playerObject =
                    GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null
                    ? playerObject.transform
                    : null;
            }

            if (player == null)
            {
                return;
            }

            playerInput ??=
                player.GetComponent<PlayerInputSource>();
            weaponPresenter ??=
                player.GetComponentInChildren<
                    TwoSlotWeaponPresenter>(true);
            meleeWeapon ??= player.GetComponent<MeleeWeapon>();
        }
    }
}
