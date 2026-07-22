using UnityEngine;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    public sealed class WeaponSwingPresenter : MonoBehaviour
    {
        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private Transform weaponVisual;
        [SerializeField, Min(0.05f)] private float duration = 0.24f;
        [SerializeField] private float arc = 105f;

        private Quaternion restRotation;
        private float swingStartedAt = float.NegativeInfinity;

        public void Configure(MeleeWeapon source, Transform visual)
        {
            if (isActiveAndEnabled && weapon != null)
            {
                weapon.AttackStarted -= BeginSwing;
            }

            weapon = source;
            weaponVisual = visual;
            restRotation = visual != null ? visual.localRotation : Quaternion.identity;

            if (isActiveAndEnabled && weapon != null)
            {
                weapon.AttackStarted += BeginSwing;
            }
        }

        private void Awake()
        {
            if (weaponVisual != null)
            {
                restRotation = weaponVisual.localRotation;
            }
        }

        private void OnEnable()
        {
            if (weapon != null)
            {
                weapon.AttackStarted += BeginSwing;
            }
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.AttackStarted -= BeginSwing;
            }
        }

        private void LateUpdate()
        {
            if (weaponVisual == null)
            {
                return;
            }

            float progress = (Time.time - swingStartedAt) / duration;
            if (progress < 0f || progress > 1f)
            {
                weaponVisual.localRotation = restRotation;
                return;
            }

            float eased = Mathf.Sin(progress * Mathf.PI);
            weaponVisual.localRotation = restRotation * Quaternion.Euler(0f, -arc * eased, 0f);
        }

        private void BeginSwing()
        {
            swingStartedAt = Time.time;
        }
    }
}
