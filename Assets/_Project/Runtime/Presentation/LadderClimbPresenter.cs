using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(1050)]
    [DisallowMultipleComponent]
    public sealed class LadderClimbPresenter : MonoBehaviour
    {
        public const string LayerName = "Ladder Climb";
        public const string StateName = "Climb Ladder";
        public const string ClipName = "Armature|ClimbUp_1m";

        private static readonly int StateHash =
            Animator.StringToHash(StateName);

        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Animator animator;

        private int layerIndex = -1;
        private bool presentationActive;
        private ShortSwordAttackPresenter swordAttack;
        private ShortSwordBlockPresenter swordBlock;
        private BowWeapon bowWeapon;
        private TwoSlotWeaponPresenter weaponPresenter;
        private UpperBodyAimPresenter upperBodyAim;
        private bool swordAttackWasEnabled;
        private bool swordBlockWasEnabled;
        private bool bowWasEnabled;
        private bool weaponPresenterWasEnabled;
        private bool upperBodyAimWasEnabled;
        private bool swordWasVisible;
        private bool bowWasVisible;

        public bool IsPresenting => presentationActive;

        public void Configure(ThirdPersonMotor targetMotor)
        {
            Unsubscribe();
            motor = targetMotor;
            ResolveReferences();
            Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            if (motor != null && motor.IsClimbingLadder)
            {
                BeginPresentation();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            EndPresentation();
        }

        private void Update()
        {
            if (motor != null && motor.IsClimbingLadder)
            {
                if (!presentationActive)
                {
                    BeginPresentation();
                }
                if (animator != null && layerIndex >= 0)
                {
                    animator.SetLayerWeight(layerIndex, 1f);
                }
            }
            else if (presentationActive)
            {
                EndPresentation();
            }
        }

        private void BeginPresentation()
        {
            ResolveReferences();
            if (animator == null ||
                !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
            {
                return;
            }

            layerIndex = animator.GetLayerIndex(LayerName);
            if (layerIndex < 0)
            {
                return;
            }

            if (!presentationActive)
            {
                swordAttackWasEnabled =
                    swordAttack != null && swordAttack.enabled;
                swordBlockWasEnabled =
                    swordBlock != null && swordBlock.enabled;
                bowWasEnabled = bowWeapon != null && bowWeapon.enabled;
                weaponPresenterWasEnabled =
                    weaponPresenter != null && weaponPresenter.enabled;
                upperBodyAimWasEnabled =
                    upperBodyAim != null && upperBodyAim.enabled;
                swordWasVisible =
                    weaponPresenter != null &&
                    weaponPresenter.PrimaryWeaponRoot != null &&
                    weaponPresenter.PrimaryWeaponRoot.gameObject.activeSelf;
                bowWasVisible =
                    weaponPresenter != null &&
                    weaponPresenter.SecondaryWeaponRoot != null &&
                    weaponPresenter.SecondaryWeaponRoot.gameObject.activeSelf;
            }

            swordAttack?.InterruptForHitStagger();
            if (swordAttack != null)
            {
                swordAttack.enabled = false;
            }
            if (swordBlock != null)
            {
                swordBlock.enabled = false;
            }
            if (bowWeapon != null)
            {
                bowWeapon.AbortDraw();
                bowWeapon.enabled = false;
            }
            if (weaponPresenter != null)
            {
                weaponPresenter.enabled = false;
                if (weaponPresenter.PrimaryWeaponRoot != null)
                {
                    weaponPresenter.PrimaryWeaponRoot.gameObject.SetActive(false);
                }
                if (weaponPresenter.SecondaryWeaponRoot != null)
                {
                    weaponPresenter.SecondaryWeaponRoot.gameObject.SetActive(false);
                }
            }
            if (upperBodyAim != null)
            {
                upperBodyAim.enabled = false;
            }

            presentationActive = true;
            animator.SetLayerWeight(layerIndex, 1f);
            animator.Play(StateHash, layerIndex, 0f);
        }

        private void EndPresentation()
        {
            if (!presentationActive)
            {
                return;
            }

            presentationActive = false;
            if (animator != null && layerIndex >= 0)
            {
                animator.SetLayerWeight(layerIndex, 0f);
            }
            if (weaponPresenter != null)
            {
                if (weaponPresenter.PrimaryWeaponRoot != null)
                {
                    weaponPresenter.PrimaryWeaponRoot.gameObject.SetActive(
                        swordWasVisible);
                }
                if (weaponPresenter.SecondaryWeaponRoot != null)
                {
                    weaponPresenter.SecondaryWeaponRoot.gameObject.SetActive(
                        bowWasVisible);
                }
                weaponPresenter.enabled = weaponPresenterWasEnabled;
            }
            if (swordAttack != null)
            {
                swordAttack.enabled = swordAttackWasEnabled;
            }
            if (swordBlock != null)
            {
                swordBlock.enabled = swordBlockWasEnabled;
            }
            if (bowWeapon != null)
            {
                bowWeapon.enabled = bowWasEnabled;
            }
            if (upperBodyAim != null)
            {
                upperBodyAim.enabled = upperBodyAimWasEnabled;
            }
        }

        private void ResolveReferences()
        {
            motor ??= GetComponent<ThirdPersonMotor>();
            animator ??= GetComponentInChildren<Animator>(true);
            swordAttack ??=
                GetComponentInChildren<ShortSwordAttackPresenter>(true);
            swordBlock ??=
                GetComponentInChildren<ShortSwordBlockPresenter>(true);
            bowWeapon ??= GetComponentInChildren<BowWeapon>(true);
            weaponPresenter ??=
                GetComponentInChildren<TwoSlotWeaponPresenter>(true);
            upperBodyAim ??=
                GetComponentInChildren<UpperBodyAimPresenter>(true);
            layerIndex = animator != null
                ? animator.GetLayerIndex(LayerName)
                : -1;
        }

        private void Subscribe()
        {
            if (motor == null)
            {
                return;
            }
            motor.LadderClimbStarted -= BeginPresentation;
            motor.LadderClimbEnded -= EndPresentation;
            motor.LadderClimbStarted += BeginPresentation;
            motor.LadderClimbEnded += EndPresentation;
        }

        private void Unsubscribe()
        {
            if (motor == null)
            {
                return;
            }
            motor.LadderClimbStarted -= BeginPresentation;
            motor.LadderClimbEnded -= EndPresentation;
        }
    }
}
