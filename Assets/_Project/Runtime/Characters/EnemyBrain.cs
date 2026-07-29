using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public enum EnemyState
        {
            Idle,
            Pursuing,
            Windup,
            Recovering,
            Dead
        }

        private enum MeleePhase
        {
            EstablishGuard,
            Closing,
            Attacking,
            Disengaging
        }

        [SerializeField] private Transform target;
        [SerializeField] private bool trainingDummy = true;
        [SerializeField, Min(2f)] private float bowRange = 7f;
        [SerializeField, Min(0.5f)] private float swordRange = 0.85f;
        [SerializeField, Min(0.5f)] private float guardDistance = 1.65f;
        [SerializeField, Min(0.5f)] private float minimumGuardDistance = 1.25f;
        [SerializeField, Min(0.1f)] private float minimumBowHold = 1.14f;
        [SerializeField, Min(0.1f)] private float maximumBowHold = 1.24f;

        private CharacterController controller;
        private Health health;
        private Health observedTargetHealth;
        private HumanoidDamageHitboxRig damageHitboxes;
        private PlayerInputSource input;
        private ThirdPersonMotor motor;
        private CharacterAimSource aimSource;
        private TwoSlotWeaponPresenter weaponSlots;
        private BowWeapon bowWeapon;
        private MeleeWeapon meleeWeapon;
        private ShortSwordAttackPresenter swordAttack;
        private HumanoidAnimatorPresenter locomotionPresenter;
        private UpperBodyAimPresenter upperBodyAimPresenter;
        private AimStanceLocomotionPresenter stancePresenter;
        private Animator animator;
        private EnemyState state;
        private float actionTimer;
        private float nextAttackPulse;
        private float meleePhaseTimer;
        private float orbitDirectionTimer;
        private float orbitDirection = 1f;
        private int comboPulsesRemaining;
        private MeleePhase meleePhase;
        private bool swordModeActive;
        private bool drawingBow;
        private bool loggedFirstArrow;
        private bool loggedFirstArrowHit;
        private bool loggedFirstSwordHit;
        private Vector3 heldAimPoint;

        public event Action<EnemyState> StateChanged;

        public EnemyState CurrentState => state;
        public bool IsActivated => !trainingDummy;

        public void Configure(Transform pursuitTarget)
        {
            target = pursuitTarget;
            ActivateCombat();
        }

        public void ActivateForDiagnostics()
        {
            ActivateCombat();
        }

        public void ConfigureAsTrainingDummy()
        {
            target = null;
            trainingDummy = true;
            ResolveReferences();
            if (input != null)
            {
                input.SetDiagnosticOverride(default);
            }

            if (motor != null)
            {
                motor.enabled = false;
            }

            aimSource?.ClearOverride();
            SetDormantPresenterState(true);
            FreezeDormantPose();
            ConfigureTrainingDummyCollision();
            ChangeState(EnemyState.Idle);
        }

        private void Awake()
        {
            ResolveReferences();
            health.Died += HandleDeath;
            if (trainingDummy)
            {
                ConfigureAsTrainingDummy();
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= HandleDeath;
            }

            if (bowWeapon != null)
            {
                bowWeapon.ArrowFired -= HandleArrowFired;
            }

            if (meleeWeapon != null)
            {
                meleeWeapon.AttackResolved -= HandleSwordAttackResolved;
            }

            if (observedTargetHealth != null)
            {
                observedTargetHealth.Damaged -= HandleTargetDamaged;
            }
        }

        private void Update()
        {
            if (state == EnemyState.Dead)
            {
                return;
            }

            if (trainingDummy)
            {
                FreezeDormantPose();
                if (Keyboard.current != null &&
                    Keyboard.current.tKey.wasPressedThisFrame)
                {
                    ActivateCombat();
                }
                return;
            }

            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                target = player != null ? player.transform : null;
            }

            Health targetHealth =
                target != null ? target.GetComponent<Health>() : null;
            ObserveTargetHealth(targetHealth);
            if (target == null ||
                targetHealth == null ||
                !targetHealth.IsAlive)
            {
                aimSource?.ClearOverride();
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Idle);
                return;
            }

            Vector3 chestPoint = ResolveTargetChestPoint();
            Vector3 toTarget = Vector3.ProjectOnPlane(
                target.position - transform.position,
                Vector3.up);
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f
                ? toTarget / distance
                : transform.forward;

            if (drawingBow)
            {
                UpdateBowDraw();
                return;
            }

            if (distance > bowRange)
            {
                swordModeActive = false;
                UpdateRangedCombat(chestPoint);
            }
            else
            {
                if (!swordModeActive)
                {
                    swordModeActive = true;
                    EnterGuardPhase();
                }
                UpdateSwordCombat(direction, distance, chestPoint);
            }
        }

        private void ActivateCombat()
        {
            if (!trainingDummy)
            {
                return;
            }

            ResolveReferences();
            trainingDummy = false;
            health.ConfigureWithFloor(88f, 0f);
            if (animator != null)
            {
                animator.speed = 1f;
                animator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;
                animator.SetFloat(
                    HumanoidAnimatorPresenter.GaitPlaybackParameter,
                    1f);
            }
            SetDormantPresenterState(false);
            if (controller != null)
            {
                controller.enabled = true;
            }

            if (damageHitboxes != null)
            {
                damageHitboxes.enabled = true;
                damageHitboxes.SetHitboxesEnabled(true);
            }

            if (motor != null)
            {
                motor.enabled = true;
                motor.ResetForDiagnostics(
                    transform.position,
                    transform.rotation);
            }

            input?.SetDiagnosticOverride(default);
            actionTimer = 0.35f;
            ChangeState(EnemyState.Pursuing);
            GameplayEventLog.Publish(
                "enemy-activated",
                gameObject,
                "T");
            Debug.Log(
                "Training dummy combat AI activated.",
                this);
        }

        private void UpdateRangedCombat(Vector3 targetChest)
        {
            if (weaponSlots == null || bowWeapon == null)
            {
                SetIntent(Vector2.zero, false, false);
                return;
            }

            if (weaponSlots.ActiveSlot !=
                TwoSlotWeaponPresenter.SecondarySlot)
            {
                weaponSlots.RequestSlot(
                    TwoSlotWeaponPresenter.SecondarySlot);
                SetAim(targetChest);
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Pursuing);
                return;
            }

            SetAim(targetChest);
            SetIntent(Vector2.zero, false, false);
            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f || weaponSlots.IsTransitioning)
            {
                ChangeState(EnemyState.Recovering);
                return;
            }

            heldAimPoint = ResolvePerfectAimPoint();
            SetAim(heldAimPoint);
            drawingBow = true;
            actionTimer = UnityEngine.Random.Range(
                minimumBowHold,
                maximumBowHold);
            SetIntent(Vector2.zero, false, true);
            ChangeState(EnemyState.Windup);
        }

        private void UpdateBowDraw()
        {
            heldAimPoint = ResolvePerfectAimPoint();
            SetAim(heldAimPoint);
            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
            {
                SetIntent(Vector2.zero, false, true);
                return;
            }

            SetIntent(Vector2.zero, false, false);
            drawingBow = false;
            actionTimer = UnityEngine.Random.Range(0.85f, 1.35f);
            ChangeState(EnemyState.Recovering);
        }

        private void UpdateSwordCombat(
            Vector3 direction,
            float distance,
            Vector3 targetChest)
        {
            if (weaponSlots == null)
            {
                SetIntent(Vector2.zero, false, false);
                return;
            }

            if (weaponSlots.ActiveSlot !=
                TwoSlotWeaponPresenter.PrimarySlot)
            {
                weaponSlots.RequestSlot(
                    TwoSlotWeaponPresenter.PrimarySlot);
                SetAim(targetChest);
                SetIntent(Vector2.zero, false, false);
                return;
            }

            SetAim(targetChest);
            switch (meleePhase)
            {
                case MeleePhase.EstablishGuard:
                    UpdateGuardOrbit(direction, distance);
                    return;

                case MeleePhase.Closing:
                    UpdateMeleeClosing(direction, distance);
                    return;

                case MeleePhase.Attacking:
                    UpdateMeleeAttack(direction, distance);
                    return;

                case MeleePhase.Disengaging:
                    UpdateMeleeDisengage(direction, distance);
                    return;
            }
        }

        private void UpdateGuardOrbit(
            Vector3 direction,
            float distance)
        {
            meleePhaseTimer -= Time.deltaTime;
            orbitDirectionTimer -= Time.deltaTime;
            if (orbitDirectionTimer <= 0f)
            {
                orbitDirection =
                    UnityEngine.Random.value < 0.5f
                        ? -1f
                        : 1f;
                orbitDirectionTimer =
                    UnityEngine.Random.Range(0.8f, 1.6f);
            }

            Vector3 tangent =
                Vector3.Cross(Vector3.up, direction) *
                orbitDirection;
            float radialError =
                distance - guardDistance;
            Vector3 movement;
            if (distance < minimumGuardDistance)
            {
                movement =
                    -direction +
                    tangent * 0.25f;
            }
            else if (distance > guardDistance + 0.35f)
            {
                movement =
                    direction * 0.75f +
                    tangent * 0.30f;
            }
            else
            {
                movement =
                    tangent * 0.46f +
                    direction *
                    Mathf.Clamp(radialError, -0.25f, 0.25f);
            }

            SetIntent(
                WorldDirectionToInput(movement),
                false,
                true);
            ChangeState(EnemyState.Recovering);

            if (meleePhaseTimer <= 0f)
            {
                meleePhase =
                    MeleePhase.Closing;
                ChangeState(EnemyState.Pursuing);
            }
        }

        private void UpdateMeleeClosing(
            Vector3 direction,
            float distance)
        {
            if (distance > swordRange + 0.20f)
            {
                SetIntent(
                    WorldDirectionToInput(direction),
                    false,
                    true);
                ChangeState(EnemyState.Pursuing);
                return;
            }

            meleePhase = MeleePhase.Attacking;
            comboPulsesRemaining =
                UnityEngine.Random.Range(1, 4);
            nextAttackPulse = 0f;
            SetIntent(Vector2.zero, false, false);
        }

        private void UpdateMeleeAttack(
            Vector3 direction,
            float distance)
        {
            bool attackPressed = false;
            if (comboPulsesRemaining > 0)
            {
                nextAttackPulse -= Time.deltaTime;
                if (nextAttackPulse <= 0f)
                {
                    attackPressed = true;
                    comboPulsesRemaining--;
                    nextAttackPulse = 0.43f;
                }
            }

            SetIntent(
                distance > 0.72f
                    ? WorldDirectionToInput(
                        direction * 0.42f)
                    : Vector2.zero,
                attackPressed,
                false);
            ChangeState(
                attackPressed ||
                (swordAttack != null && swordAttack.IsAttacking)
                    ? EnemyState.Windup
                    : EnemyState.Recovering);

            if (comboPulsesRemaining <= 0 &&
                (swordAttack == null ||
                 !swordAttack.IsAttacking))
            {
                meleePhase =
                    MeleePhase.Disengaging;
                meleePhaseTimer =
                    UnityEngine.Random.Range(0.35f, 0.60f);
            }
        }

        private void UpdateMeleeDisengage(
            Vector3 direction,
            float distance)
        {
            meleePhaseTimer -= Time.deltaTime;
            Vector3 tangent =
                Vector3.Cross(Vector3.up, direction) *
                orbitDirection;
            bool needsSpace =
                distance < guardDistance - 0.10f;
            Vector3 movement = needsSpace
                ? -direction * 0.82f + tangent * 0.22f
                : tangent * 0.30f;
            SetIntent(
                WorldDirectionToInput(movement),
                false,
                true);
            ChangeState(EnemyState.Recovering);
            if (!needsSpace &&
                meleePhaseTimer <= 0f)
            {
                EnterGuardPhase();
            }
        }

        private void EnterGuardPhase()
        {
            meleePhase =
                MeleePhase.EstablishGuard;
            meleePhaseTimer =
                UnityEngine.Random.Range(0.90f, 1.65f);
            orbitDirection =
                UnityEngine.Random.value < 0.5f
                    ? -1f
                    : 1f;
            orbitDirectionTimer =
                UnityEngine.Random.Range(0.8f, 1.6f);
            comboPulsesRemaining = 0;
        }

        private Vector3 ResolvePerfectAimPoint()
        {
            Vector3 targetChest = ResolveTargetChestPoint();
            Vector3 velocity = Vector3.zero;
            ThirdPersonMotor targetMotor =
                target.GetComponent<ThirdPersonMotor>();
            if (targetMotor != null)
            {
                velocity = targetMotor.HorizontalVelocity;
            }

            float distance = Vector3.Distance(
                transform.position,
                targetChest);
            float shotSpeed =
                bowWeapon != null
                    ? bowWeapon.MaximumArrowSpeed
                    : 75f;
            float flightTime = distance /
                Mathf.Max(1f, shotSpeed);
            Vector3 lead = velocity * flightTime;
            float gravityCompensation =
                0.5f *
                Mathf.Abs(Physics.gravity.y) *
                flightTime *
                flightTime;
            return targetChest +
                lead +
                Vector3.up * gravityCompensation;
        }

        private void SetAim(Vector3 worldPoint)
        {
            Vector3 origin = animator != null &&
                animator.GetBoneTransform(HumanBodyBones.Head) != null
                    ? animator.GetBoneTransform(HumanBodyBones.Head).position
                    : transform.position + Vector3.up * 1.45f;
            aimSource?.SetOverride(
                origin,
                worldPoint - origin);
        }

        private Vector3 ResolveTargetChestPoint()
        {
            Animator targetAnimator =
                target.GetComponentInChildren<Animator>(true);
            Transform chest = targetAnimator != null &&
                targetAnimator.isHuman
                    ? targetAnimator.GetBoneTransform(
                        HumanBodyBones.Chest)
                    : null;
            return chest != null
                ? chest.position
                : target.position + Vector3.up * 0.55f;
        }

        private Vector2 WorldDirectionToInput(Vector3 direction)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return new Vector2(direction.x, direction.z);
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                camera.transform.forward,
                Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(
                camera.transform.right,
                Vector3.up).normalized;
            return Vector2.ClampMagnitude(
                new Vector2(
                    Vector3.Dot(direction, right),
                    Vector3.Dot(direction, forward)),
                1f);
        }

        private void SetIntent(
            Vector2 move,
            bool attack,
            bool block)
        {
            input?.SetDiagnosticOverride(
                new PlayerIntent(
                    move,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    attack,
                    block));
        }

        private void ResolveReferences()
        {
            controller ??= GetComponent<CharacterController>();
            health ??= GetComponent<Health>();
            input ??= GetComponent<PlayerInputSource>();
            motor ??= GetComponent<ThirdPersonMotor>();
            aimSource ??= GetComponent<CharacterAimSource>();
            aimSource?.SetCameraFallbackAllowed(false);
            damageHitboxes ??=
                GetComponent<HumanoidDamageHitboxRig>();
            weaponSlots ??=
                GetComponentInChildren<TwoSlotWeaponPresenter>(true);
            bowWeapon ??=
                GetComponentInChildren<BowWeapon>(true);
            if (bowWeapon != null)
            {
                bowWeapon.ArrowFired -= HandleArrowFired;
                bowWeapon.ArrowFired += HandleArrowFired;
            }
            meleeWeapon ??= GetComponent<MeleeWeapon>();
            if (meleeWeapon != null)
            {
                meleeWeapon.AttackResolved -= HandleSwordAttackResolved;
                meleeWeapon.AttackResolved += HandleSwordAttackResolved;
            }
            swordAttack ??=
                GetComponentInChildren<ShortSwordAttackPresenter>(true);
            locomotionPresenter ??=
                GetComponent<HumanoidAnimatorPresenter>();
            upperBodyAimPresenter ??=
                GetComponentInChildren<UpperBodyAimPresenter>(true);
            stancePresenter ??=
                GetComponentInChildren<AimStanceLocomotionPresenter>(true);
            animator ??= GetComponentInChildren<Animator>(true);
        }

        private void SetDormantPresenterState(bool dormant)
        {
            if (locomotionPresenter != null)
            {
                locomotionPresenter.enabled = !dormant;
            }

            if (upperBodyAimPresenter != null)
            {
                upperBodyAimPresenter.enabled = !dormant;
            }

            if (stancePresenter != null)
            {
                stancePresenter.enabled = !dormant;
            }
        }

        private void FreezeDormantPose()
        {
            if (animator == null)
            {
                return;
            }

            animator.speed = 0f;
            animator.SetFloat(
                HumanoidAnimatorPresenter.SpeedParameter,
                0f);
            animator.SetFloat(
                HumanoidAnimatorPresenter.MoveXParameter,
                0f);
            animator.SetFloat(
                HumanoidAnimatorPresenter.MoveZParameter,
                0f);
            animator.SetFloat(
                HumanoidAnimatorPresenter.GaitPlaybackParameter,
                1f);
            animator.SetFloat(
                HumanoidAnimatorPresenter.VerticalSpeedParameter,
                0f);
            animator.SetBool(
                HumanoidAnimatorPresenter.GroundedParameter,
                true);
            animator.SetBool(
                HumanoidAnimatorPresenter.CrouchedParameter,
                false);
            int attackLayer = animator.GetLayerIndex(
                ShortSwordAttackPresenter.AttackLayerName);
            if (attackLayer >= 0)
            {
                animator.SetLayerWeight(attackLayer, 0f);
            }

            int blockLayer = animator.GetLayerIndex(
                ShortSwordBlockPresenter.BlockLayerName);
            if (blockLayer >= 0)
            {
                animator.SetLayerWeight(blockLayer, 0f);
            }

            int readyLayer = animator.GetLayerIndex(
                TwoSlotWeaponPresenter.SwordReadyLayerName);
            if (readyLayer >= 0)
            {
                animator.SetLayerWeight(readyLayer, 1f);
            }
        }

        private void ConfigureTrainingDummyCollision()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            damageHitboxes ??=
                GetComponent<HumanoidDamageHitboxRig>();
            if (damageHitboxes == null)
            {
                damageHitboxes =
                    gameObject.AddComponent<HumanoidDamageHitboxRig>();
            }

            damageHitboxes.enabled = true;
            damageHitboxes.SetHitboxesEnabled(true);
            damageHitboxes.Configure(animator);
        }

        private void ChangeState(EnemyState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            state = nextState;
            StateChanged?.Invoke(state);
            GameplayEventLog.Publish(
                "enemy-state",
                gameObject,
                state.ToString());
        }

        private void HandleDeath(DamageRequest request)
        {
            SetIntent(Vector2.zero, false, false);
            ChangeState(EnemyState.Dead);
        }

        private void HandleArrowFired(float charge)
        {
            if (loggedFirstArrow)
            {
                return;
            }

            loggedFirstArrow = true;
            Debug.Log(
                $"Combat AI fired its first arrow at {charge:0.00} draw.",
                this);
        }

        private void HandleSwordAttackResolved(
            MeleeAttackReport report)
        {
            if (loggedFirstSwordHit ||
                report.DamagedTargets <= 0)
            {
                return;
            }

            loggedFirstSwordHit = true;
            Debug.Log(
                "Combat AI landed its first sword hit.",
                this);
        }

        private void ObserveTargetHealth(Health targetHealth)
        {
            if (ReferenceEquals(
                observedTargetHealth,
                targetHealth))
            {
                return;
            }

            if (observedTargetHealth != null)
            {
                observedTargetHealth.Damaged -=
                    HandleTargetDamaged;
            }

            observedTargetHealth = targetHealth;
            if (observedTargetHealth != null)
            {
                observedTargetHealth.Damaged +=
                    HandleTargetDamaged;
            }
        }

        private void HandleTargetDamaged(DamageRequest request)
        {
            if (loggedFirstArrowHit ||
                request.SourceId != "prototype-bow" ||
                request.Instigator != gameObject)
            {
                return;
            }

            loggedFirstArrowHit = true;
            Debug.Log(
                "Combat AI landed its first arrow hit.",
                this);
        }

    }
}
