using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.WeaponGrid
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class WeaponGridCombatBridge : MonoBehaviour
    {
        [SerializeField] private WeaponGridRuntime gridRuntime;
        [SerializeField] private GameObject characterRoot;
        [SerializeField] private MeleeWeapon meleeWeapon;
        [SerializeField] private BowWeapon bowWeapon;
        [SerializeField] private Health health;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private TwoSlotWeaponPresenter weaponPresenter;

        private bool subscribed;

        public WeaponGridRuntime GridRuntime => gridRuntime;

        public void Configure(
            WeaponGridRuntime runtime,
            GameObject targetCharacter)
        {
            Unsubscribe();
            gridRuntime = runtime;
            characterRoot = targetCharacter;
            ResolveReferences();
            Subscribe();
            ApplyResolvedModifiers();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            ApplyResolvedModifiers();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveReferences()
        {
            if (gridRuntime == null)
            {
                gridRuntime =
                    GetComponent<WeaponGridRuntime>() ??
                    FindFirstObjectByType<WeaponGridRuntime>();
            }

            if (characterRoot == null)
            {
                GameObject taggedPlayer =
                    GameObject.FindGameObjectWithTag("Player");
                characterRoot = taggedPlayer;
            }

            if (characterRoot == null)
            {
                return;
            }

            meleeWeapon ??=
                characterRoot.GetComponent<MeleeWeapon>();
            bowWeapon ??=
                characterRoot.GetComponentInChildren<BowWeapon>(true);
            health ??=
                characterRoot.GetComponent<Health>();
            motor ??=
                characterRoot.GetComponent<ThirdPersonMotor>();
            weaponPresenter ??=
                characterRoot.GetComponentInChildren<TwoSlotWeaponPresenter>(
                    true);
        }

        private void Subscribe()
        {
            if (subscribed || gridRuntime == null)
            {
                return;
            }

            gridRuntime.ModifiersChanged += HandleModifiersChanged;
            if (weaponPresenter != null)
            {
                weaponPresenter.ActiveSlotChanged +=
                    HandleActiveSlotChanged;
                gridRuntime.SelectWeapon(weaponPresenter.ActiveSlot);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (gridRuntime != null)
            {
                gridRuntime.ModifiersChanged -= HandleModifiersChanged;
            }

            if (weaponPresenter != null)
            {
                weaponPresenter.ActiveSlotChanged -=
                    HandleActiveSlotChanged;
            }

            subscribed = false;
        }

        private void HandleModifiersChanged(
            WeaponGridModifierSummary summary)
        {
            ApplyResolvedModifiers(summary);
        }

        private void HandleActiveSlotChanged(int slot)
        {
            gridRuntime?.SelectWeapon(slot);
        }

        private void ApplyResolvedModifiers()
        {
            if (gridRuntime == null)
            {
                return;
            }

            ApplyResolvedModifiers(gridRuntime.GetModifierSummary());
        }

        private void ApplyResolvedModifiers(
            WeaponGridModifierSummary summary)
        {
            meleeWeapon?.SetRuntimeDamageBonus(
                summary.Primary.Damage);
            bowWeapon?.SetRuntimeDamageBonus(
                summary.Secondary.Damage);
            health?.SetRuntimeMaximumBonus(
                summary.Effective.MaxHealth);
            motor?.SetRuntimeSpeedBonus(
                summary.Effective.MoveSpeed);
        }
    }
}
