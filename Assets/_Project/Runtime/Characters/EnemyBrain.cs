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
            Patrolling,
            Alerted,
            Investigating,
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
        [SerializeField, Min(0.1f)] private float minimumBowRecovery = 1.35f;
        [SerializeField, Min(0.1f)] private float maximumBowRecovery = 2.25f;
        [Header("Perception")]
        [SerializeField, Min(1f)] private float passiveSightRange = 24f;
        [SerializeField, Min(1f)] private float alertedSightRange = 46f;
        [SerializeField, Range(10f, 360f)] private float passiveViewAngle = 120f;
        [SerializeField, Range(10f, 360f)] private float alertedViewAngle = 220f;
        [SerializeField, Min(0.1f)] private float investigationDuration = 9f;
        [SerializeField, Min(0.1f)] private float investigationGuessDistance = 10f;
        [SerializeField, Min(0.1f)] private float investigationStopDistance = 1.25f;
        [SerializeField, Min(0f)] private float lostSightWaitDuration = 1.25f;
        [SerializeField, Min(0.1f)] private float investigationSwordDistance = 2.6f;
        [SerializeField, Min(0f)] private float cornerProbeDistance = 1.4f;
        [SerializeField, Min(0.1f)] private float bowOcclusionHoldDuration = 2.4f;
        [SerializeField, Min(0.1f)] private float searchAimInterval = 0.8f;
        [SerializeField] private LayerMask sightMask = ~0;
        [Header("Patrol")]
        [SerializeField, Min(0.5f)] private float patrolRadius = 5f;
        [SerializeField, Min(0.1f)] private float patrolStopDistance = 0.65f;
        [SerializeField, Min(0f)] private float minimumPatrolWait = 0.8f;
        [SerializeField, Min(0f)] private float maximumPatrolWait = 2.2f;

        private CharacterController controller;
        private Health health;
        private EnemyDamageProfile damageProfile;
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
        private bool alerted;
        private bool hasVisualContact;
        private bool loggedFirstArrow;
        private bool loggedFirstArrowHit;
        private bool loggedFirstSwordHit;
        private Vector3 heldAimPoint;
        private Vector3 lastKnownPosition;
        private Vector3 lastKnownApproachDirection;
        private float investigationTimer;
        private float lostSightWaitTimer;
        private float searchAimTimer;
        private float bowOcclusionTimer;
        private float searchSide = 1f;
        private bool patrolWhenIdle;
        private bool manualActivationOnly = true;
        private Vector3 patrolOrigin;
        private Vector3 patrolDestination;
        private float patrolWaitTimer;
        private bool hasPatrolDestination;
        private Vector3[] patrolRoute;
        private int patrolRouteIndex;
        private int patrolRouteDirection = 1;

        public event Action<EnemyState> StateChanged;

        public EnemyState CurrentState => state;
        public bool IsActivated => !trainingDummy;
        public bool IsAlerted => alerted;
        public bool HasVisualContact => hasVisualContact;
        public Vector3 LastKnownPosition => lastKnownPosition;

        public void Configure(Transform pursuitTarget)
        {
            target = pursuitTarget;
            patrolWhenIdle = true;
            ActivateCombat(
                preserveCurrentHealth: false,
                beginAlerted: false);
        }

        public void ActivateForDiagnostics()
        {
            patrolWhenIdle = false;
            ResolvePlayerTarget();
            ActivateCombat(
                preserveCurrentHealth: false,
                beginAlerted: true);
        }

        public void ConfigureForArenaDormancy()
        {
            manualActivationOnly = false;
            patrolWhenIdle = true;
        }

        public void ConfigurePatrolRoute(
            Vector3[] worldPoints,
            int startIndex)
        {
            patrolRoute =
                worldPoints != null
                    ? (Vector3[])worldPoints.Clone()
                    : null;
            if (patrolRoute == null ||
                patrolRoute.Length == 0)
            {
                patrolRouteIndex = 0;
                patrolRouteDirection = 1;
                hasPatrolDestination = false;
                return;
            }

            patrolRouteIndex = Mathf.Clamp(
                startIndex,
                0,
                patrolRoute.Length - 1);
            patrolRouteDirection =
                patrolRouteIndex >= patrolRoute.Length - 1
                    ? -1
                    : 1;
            patrolDestination =
                patrolRoute[patrolRouteIndex];
            hasPatrolDestination = true;
            patrolWhenIdle = true;
        }

        public void ConfigureAsTrainingDummy(
            bool requireManualActivation = true)
        {
            target = null;
            trainingDummy = true;
            manualActivationOnly = requireManualActivation;
            alerted = false;
            hasVisualContact = false;
            drawingBow = false;
            investigationTimer = 0f;
            lostSightWaitTimer = 0f;
            patrolWhenIdle = false;
            ResolveReferences();
            damageProfile?.ConfigureDormantTrainingDummy();
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
            EnsureDamageProfile();
            health.Damaged += HandleDamaged;
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
                health.Damaged -= HandleDamaged;
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
                    ActivateCombat(
                        preserveCurrentHealth: false,
                        beginAlerted: true);
                }
                return;
            }

            if (target == null)
            {
                ResolvePlayerTarget();
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
            UpdatePerception(chestPoint);
            if (!alerted)
            {
                aimSource?.ClearOverride();
                if (patrolWhenIdle)
                {
                    UpdatePatrol();
                }
                else
                {
                    SetIntent(Vector2.zero, false, false);
                    ChangeState(EnemyState.Idle);
                }
                return;
            }

            if (drawingBow)
            {
                UpdateBowDraw(hasVisualContact);
                return;
            }

            if (!hasVisualContact)
            {
                UpdateInvestigation();
                return;
            }

            Vector3 toTarget = Vector3.ProjectOnPlane(
                target.position - transform.position,
                Vector3.up);
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f
                ? toTarget / distance
                : transform.forward;

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

        private void ResolvePlayerTarget()
        {
            if (target != null)
            {
                return;
            }

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            target = player != null
                ? player.transform
                : null;
        }

        private void ActivateCombat(
            bool preserveCurrentHealth,
            bool beginAlerted)
        {
            if (!trainingDummy)
            {
                return;
            }

            ResolveReferences();
            trainingDummy = false;
            patrolOrigin = transform.position;
            patrolWaitTimer = 0f;
            if (patrolRoute != null &&
                patrolRoute.Length > 0)
            {
                patrolRouteIndex = Mathf.Clamp(
                    patrolRouteIndex,
                    0,
                    patrolRoute.Length - 1);
                patrolDestination =
                    patrolRoute[patrolRouteIndex];
                hasPatrolDestination = true;
            }
            else
            {
                patrolDestination = patrolOrigin;
                hasPatrolDestination = false;
            }
            if (!preserveCurrentHealth)
            {
                if (damageProfile != null)
                {
                    damageProfile.Configure(
                        damageProfile.Variant);
                }
                else
                {
                    health.ConfigureWithFloor(88f, 0f);
                }
            }
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
            if (beginAlerted)
            {
                ResolvePlayerTarget();
            }

            if (beginAlerted && target != null)
            {
                AlertAt(target.position, "activation");
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
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
            bowOcclusionTimer = bowOcclusionHoldDuration;
            actionTimer = UnityEngine.Random.Range(
                minimumBowHold,
                maximumBowHold);
            SetIntent(Vector2.zero, false, true);
            ChangeState(EnemyState.Windup);
        }

        private void UpdateBowDraw(bool targetVisible)
        {
            if (!targetVisible)
            {
                bowOcclusionTimer -= Time.deltaTime;
                SetAim(heldAimPoint);
                if (bowOcclusionTimer > 0f)
                {
                    SetIntent(Vector2.zero, false, true);
                    ChangeState(EnemyState.Windup);
                    return;
                }

                bowWeapon?.AbortDraw();
                SetIntent(Vector2.zero, false, false);
                drawingBow = false;
                lostSightWaitTimer = 0f;
                actionTimer = UnityEngine.Random.Range(
                    minimumBowRecovery,
                    maximumBowRecovery);
                ChangeState(EnemyState.Investigating);
                return;
            }

            bowOcclusionTimer = bowOcclusionHoldDuration;
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
            actionTimer = UnityEngine.Random.Range(
                minimumBowRecovery,
                maximumBowRecovery);
            ChangeState(EnemyState.Recovering);
        }

        private void UpdatePerception(Vector3 targetChest)
        {
            bool hadVisualContact = hasVisualContact;
            hasVisualContact = CanSeeTarget(targetChest);
            if (!hasVisualContact)
            {
                if (hadVisualContact)
                {
                    lostSightWaitTimer =
                        lostSightWaitDuration;
                }
                return;
            }

            alerted = true;
            lastKnownPosition = target.position;
            Vector3 approachDirection =
                Vector3.ProjectOnPlane(
                    target.position - transform.position,
                    Vector3.up);
            if (approachDirection.sqrMagnitude > 0.001f)
            {
                lastKnownApproachDirection =
                    approachDirection.normalized;
            }
            investigationTimer = investigationDuration;
            lostSightWaitTimer = lostSightWaitDuration;
            searchAimTimer = searchAimInterval;
        }

        private bool CanSeeTarget(Vector3 targetChest)
        {
            if (target == null)
            {
                return false;
            }

            Vector3 origin = ResolveSightOrigin();
            Vector3 toTarget = targetChest - origin;
            float distance = toTarget.magnitude;
            float maximumDistance =
                alerted ? alertedSightRange : passiveSightRange;
            if (distance <= 0.001f ||
                distance > maximumDistance)
            {
                return false;
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(
                toTarget,
                Vector3.up);
            float viewAngle =
                alerted ? alertedViewAngle : passiveViewAngle;
            if (planarDirection.sqrMagnitude > 0.001f &&
                Vector3.Angle(
                    transform.forward,
                    planarDirection) >
                viewAngle * 0.5f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                toTarget / distance,
                distance + 0.15f,
                sightMask,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            Transform closestTransform = null;
            for (int index = 0; index < hits.Length; index++)
            {
                Transform hitTransform =
                    hits[index].collider != null
                        ? hits[index].collider.transform
                        : null;
                if (hitTransform == null ||
                    hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hits[index].distance < closestDistance)
                {
                    closestDistance = hits[index].distance;
                    closestTransform = hitTransform;
                }
            }

            return closestTransform != null &&
                (closestTransform == target ||
                 closestTransform.IsChildOf(target));
        }

        private void UpdateInvestigation()
        {
            investigationTimer -= Time.deltaTime;
            if (lostSightWaitTimer > 0f)
            {
                lostSightWaitTimer -= Time.deltaTime;
                SetAim(
                    lastKnownPosition +
                    Vector3.up * 0.65f);
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Alerted);
                return;
            }

            Vector3 investigationDestination =
                lastKnownPosition +
                lastKnownApproachDirection *
                cornerProbeDistance;
            Vector3 toLastKnown = Vector3.ProjectOnPlane(
                investigationDestination - transform.position,
                Vector3.up);
            float distance = toLastKnown.magnitude;
            float distanceToLastKnown = Vector3.Distance(
                Vector3.ProjectOnPlane(
                    lastKnownPosition,
                    Vector3.up),
                Vector3.ProjectOnPlane(
                    transform.position,
                    Vector3.up));
            Vector3 direction = distance > 0.001f
                ? toLastKnown / distance
                : transform.forward;
            if (weaponSlots != null)
            {
                int desiredSlot =
                    distanceToLastKnown >
                        investigationSwordDistance
                        ? TwoSlotWeaponPresenter.SecondarySlot
                        : TwoSlotWeaponPresenter.PrimarySlot;
                if (weaponSlots.ActiveSlot != desiredSlot &&
                    !weaponSlots.IsTransitioning)
                {
                    weaponSlots.RequestSlot(desiredSlot);
                }
            }

            searchAimTimer -= Time.deltaTime;
            if (searchAimTimer <= 0f)
            {
                searchSide *= -1f;
                searchAimTimer = searchAimInterval;
            }

            Vector3 searchRight = Vector3.Cross(
                Vector3.up,
                direction);
            Vector3 searchPoint =
                lastKnownPosition +
                Vector3.up * 0.65f +
                searchRight * (searchSide * 1.4f);
            SetAim(searchPoint);

            if (investigationTimer <= 0f)
            {
                SetIntent(Vector2.zero, false, false);
                alerted = false;
                hasVisualContact = false;
                aimSource?.ClearOverride();
                ChangeState(EnemyState.Idle);
                return;
            }

            if (distance > investigationStopDistance)
            {
                Vector3 movement =
                    ResolveObstacleAwareDirection(direction);
                SetIntent(
                    WorldDirectionToInput(movement),
                    false,
                    false);
                ChangeState(EnemyState.Investigating);
                return;
            }

            SetIntent(Vector2.zero, false, false);
            ChangeState(EnemyState.Alerted);
        }

        private void UpdatePatrol()
        {
            if (!hasPatrolDestination)
            {
                patrolWaitTimer -= Time.deltaTime;
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Idle);
                if (patrolWaitTimer > 0f)
                {
                    return;
                }

                if (patrolRoute != null &&
                    patrolRoute.Length > 0)
                {
                    AdvancePatrolRoute();
                    patrolDestination =
                        patrolRoute[patrolRouteIndex];
                }
                else
                {
                    Vector2 offset =
                        UnityEngine.Random.insideUnitCircle *
                        patrolRadius;
                    patrolDestination =
                        patrolOrigin +
                        new Vector3(offset.x, 0f, offset.y);
                }
                hasPatrolDestination = true;
            }

            Vector3 toDestination = Vector3.ProjectOnPlane(
                patrolDestination - transform.position,
                Vector3.up);
            if (toDestination.magnitude <= patrolStopDistance)
            {
                hasPatrolDestination = false;
                patrolWaitTimer = UnityEngine.Random.Range(
                    minimumPatrolWait,
                    maximumPatrolWait);
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Idle);
                return;
            }

            Vector3 direction =
                toDestination.sqrMagnitude > 0.001f
                    ? toDestination.normalized
                    : transform.forward;
            direction = ResolveObstacleAwareDirection(direction);
            SetIntent(
                WorldDirectionToInput(direction * 0.55f),
                false,
                false);
            ChangeState(EnemyState.Patrolling);
        }

        private void AdvancePatrolRoute()
        {
            if (patrolRoute == null ||
                patrolRoute.Length <= 1)
            {
                patrolRouteIndex = 0;
                return;
            }

            int next =
                patrolRouteIndex +
                patrolRouteDirection;
            if (next < 0 ||
                next >= patrolRoute.Length)
            {
                patrolRouteDirection *= -1;
                next =
                    patrolRouteIndex +
                    patrolRouteDirection;
            }

            patrolRouteIndex = Mathf.Clamp(
                next,
                0,
                patrolRoute.Length - 1);
        }

        private Vector3 ResolveObstacleAwareDirection(
            Vector3 desiredDirection)
        {
            Vector3 origin =
                transform.position +
                Vector3.up * 0.65f +
                desiredDirection * 0.38f;
            if (!Physics.SphereCast(
                    origin,
                    0.20f,
                    desiredDirection,
                    out RaycastHit hit,
                    1.1f,
                    sightMask,
                    QueryTriggerInteraction.Ignore) ||
                hit.collider == null ||
                hit.collider.transform.IsChildOf(transform) ||
                (target != null &&
                    (hit.collider.transform == target ||
                     hit.collider.transform.IsChildOf(target))))
            {
                return desiredDirection;
            }

            Vector3 tangent = Vector3.Cross(
                Vector3.up,
                desiredDirection) * orbitDirection;
            return (
                tangent * 0.85f +
                desiredDirection * 0.35f).normalized;
        }

        private Vector3 ResolveSightOrigin()
        {
            Transform head = animator != null &&
                animator.isHuman
                    ? animator.GetBoneTransform(
                        HumanBodyBones.Head)
                    : null;
            return head != null
                ? head.position
                : transform.position + Vector3.up * 0.75f;
        }

        private void AlertAt(
            Vector3 believedSourcePosition,
            string reason)
        {
            alerted = true;
            hasVisualContact = false;
            lastKnownPosition = believedSourcePosition;
            Vector3 approachDirection =
                Vector3.ProjectOnPlane(
                    believedSourcePosition - transform.position,
                    Vector3.up);
            lastKnownApproachDirection =
                approachDirection.sqrMagnitude > 0.001f
                    ? approachDirection.normalized
                    : transform.forward;
            investigationTimer = investigationDuration;
            lostSightWaitTimer = 0f;
            searchAimTimer = searchAimInterval;
            actionTimer = 0f;
            swordModeActive = false;
            EnterGuardPhase();
            ChangeState(EnemyState.Alerted);
            GameplayEventLog.Publish(
                "enemy-alerted",
                gameObject,
                reason);
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
            damageProfile ??=
                GetComponent<EnemyDamageProfile>();
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

        private void EnsureDamageProfile()
        {
            if (damageProfile != null)
            {
                return;
            }

            damageProfile =
                gameObject.AddComponent<EnemyDamageProfile>();
            bool isRaidEnemy =
                gameObject.scene.name.IndexOf(
                    "Raid",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            damageProfile.Configure(
                isRaidEnemy
                    ? EnemyCombatVariant.RaidEnemy
                    : EnemyCombatVariant.CombatLabDummy);
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

        private void HandleDamaged(DamageRequest request)
        {
            if (state == EnemyState.Dead ||
                request.Instigator == gameObject)
            {
                return;
            }

            if (trainingDummy)
            {
                if (manualActivationOnly)
                {
                    return;
                }

                patrolWhenIdle = true;
                target = request.Instigator != null
                    ? request.Instigator.transform
                    : target;
                enabled = true;
                ActivateCombat(
                    preserveCurrentHealth: true,
                    beginAlerted: false);
            }

            if (request.Instigator != null)
            {
                target = request.Instigator.transform;
            }

            if (!enabled)
            {
                enabled = true;
            }

            Vector3 incomingDirection = Vector3.ProjectOnPlane(
                request.Direction,
                Vector3.up);
            Vector3 believedSource;
            if (incomingDirection.sqrMagnitude > 0.001f)
            {
                believedSource =
                    request.HitPoint -
                    incomingDirection.normalized *
                    investigationGuessDistance;
            }
            else if (request.Instigator != null)
            {
                Vector3 towardInstigator =
                    request.Instigator.transform.position -
                    transform.position;
                believedSource =
                    transform.position +
                    Vector3.ClampMagnitude(
                        Vector3.ProjectOnPlane(
                            towardInstigator,
                            Vector3.up),
                        investigationGuessDistance);
            }
            else
            {
                believedSource =
                    transform.position -
                    transform.forward *
                    investigationGuessDistance;
            }

            believedSource.y = transform.position.y;
            AlertAt(
                believedSource,
                string.IsNullOrWhiteSpace(request.SourceId)
                    ? "damage"
                    : request.SourceId);
        }

        private void HandleDeath(DamageRequest request)
        {
            alerted = false;
            hasVisualContact = false;
            drawingBow = false;
            bowWeapon?.AbortDraw();
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
