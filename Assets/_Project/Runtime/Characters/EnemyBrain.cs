using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public enum WeaponLoadout
        {
            Adaptive,
            BowOnly,
            SwordOnly
        }

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
        [SerializeField, Min(0.1f)] private float minimumBowHold = 1.22f;
        [SerializeField, Min(0.1f)] private float maximumBowHold = 1.38f;
        [SerializeField, Min(0.1f)] private float minimumBowRecovery = 0.45f;
        [SerializeField, Min(0.1f)] private float maximumBowRecovery = 0.75f;
        [Header("Perception")]
        [SerializeField, Min(1f)] private float passiveSightRange = 32f;
        [SerializeField, Min(1f)] private float alertedSightRange = 100f;
        [SerializeField, Range(10f, 360f)] private float passiveViewAngle = 100f;
        [SerializeField, Range(10f, 360f)] private float alertedViewAngle = 220f;
        [SerializeField, Min(0.1f)] private float immediateRecognitionRange = 6f;
        [SerializeField, Min(0.1f)] private float minimumRecognitionDuration = 0.55f;
        [SerializeField, Min(0.1f)] private float maximumRecognitionDuration = 1.65f;
        [SerializeField, Min(1f)] private float forestSightRange = 18f;
        [SerializeField, Min(1f)] private float forestRecognitionMultiplier = 2.25f;
        [SerializeField, Min(1f)] private float crouchedRecognitionMultiplier = 1.55f;
        [SerializeField, Min(0f)] private float passiveAwarenessDecay = 1.35f;
        [SerializeField, Min(0.1f)] private float runningHearingRange = 16f;
        [SerializeField, Min(0f)] private float runningHearingReactionDuration = 0.45f;
        [SerializeField, Min(0f)] private float forestTrailClearance = 5f;
        [SerializeField, Min(0.1f)] private float investigationDuration = 9f;
        [SerializeField, Min(0.1f)]
        private float confirmedEmptySearchDuration = 3.2f;
        [SerializeField, Min(0.1f)] private float investigationGuessDistance = 10f;
        [SerializeField, Min(0.1f)] private float investigationStopDistance = 1.25f;
        [SerializeField, Min(0f)] private float lostSightWaitDuration = 1.25f;
        [SerializeField, Min(0.1f)] private float investigationSwordDistance = 2.6f;
        [SerializeField, Min(0.1f)] private float searchAimInterval = 0.8f;
        [SerializeField, Min(0.1f)] private float nearMissRadius = 1.35f;
        [SerializeField, Min(0.1f)] private float impactHearingRadius = 10f;
        [SerializeField, Min(0f)] private float nearMissReactionDuration = 1.5f;
        [SerializeField, Min(0f)] private float impactReactionDuration = 0.9f;
        [SerializeField, Min(0f)] private float impactLookDuration = 0.3f;
        [SerializeField] private LayerMask sightMask = ~0;
        [Header("Patrol")]
        [SerializeField, Min(0.5f)] private float patrolRadius = 5f;
        [SerializeField, Min(0.1f)] private float patrolStopDistance = 0.65f;
        [SerializeField, Min(0f)] private float minimumPatrolWait = 3.2f;
        [SerializeField, Min(0f)] private float maximumPatrolWait = 6.4f;

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
        private Vector3 lastVisibleTargetPoint;
        private bool aimingForHeadshot;
        private Vector3 lastKnownPosition;
        private Vector3 lastKnownApproachDirection;
        private float investigationTimer;
        private bool reachedLastKnownPosition;
        private float lostSightWaitTimer;
        private float searchAimTimer;
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
        private WeaponLoadout weaponLoadout;
        private float alertReactionTimer;
        private float impactLookTimer;
        private Vector3 alertFocusPoint;
        private Vector3 alertSourceDirection;
        private float rangedStrafeDirection = 1f;
        private float rangedStrafeTimer;
        private Vector3 patrolLookDirection;
        private int lastHeardArrowId;
        private float passiveAwareness;
        private ThirdPersonMotor targetMotor;
        private ProceduralRaidGenerator raidEnvironment;
        private Vector3 lastSafeNavigationPosition;
        private bool hasSafeNavigationPosition;
        private readonly RaycastHit[] navigationProbeHits =
            new RaycastHit[32];
        private readonly Collider[] navigationOverlapColliders =
            new Collider[32];
        private Vector3 navigationDetourDirection;
        private Vector3 navigationProgressOrigin;
        private float navigationDetourTimer;
        private float navigationRecoveryTimer;
        private float navigationProgressTimer;
        private float navigationLastRequestTime = float.NegativeInfinity;
        private float navigationAvoidanceSide = 1f;
        private bool hasNavigationProgressOrigin;

        private const float MinimumCommittedBowCharge = 0.995f;
        private const float FullDrawSafetyDuration = 0.12f;
        private const float HeadshotChance = 0.075f;
        private const float NavigationLookAhead = 2.8f;
        private const float NavigationDetourDuration = 0.72f;
        private const float NavigationStuckSampleDuration = 0.55f;
        private const float NavigationMinimumProgress = 0.20f;
        private const float RangedNearbyBridgeEntryDistance = 4.5f;
        private const float RangedBridgeMinimumForwardDot = 0.25f;
        private static readonly float[] NavigationFanAngles =
        {
            30f,
            52f,
            74f,
            96f,
            122f
        };

        public event Action<EnemyState> StateChanged;

        public EnemyState CurrentState => state;
        public bool IsActivated => !trainingDummy;
        public bool IsAlerted => alerted;
        public bool HasVisualContact => hasVisualContact;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public WeaponLoadout ConfiguredWeaponLoadout =>
            weaponLoadout;

        public void Configure(Transform pursuitTarget)
        {
            target = pursuitTarget;
            targetMotor = null;
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
            if (weaponLoadout == WeaponLoadout.Adaptive)
            {
                weaponLoadout = WeaponLoadout.BowOnly;
            }
            ResolveReferences();
            ApplyConfiguredWeaponLoadout();
        }

        public void ConfigureCampGuardLoadout(
            WeaponLoadout loadout)
        {
            weaponLoadout = loadout == WeaponLoadout.SwordOnly
                ? WeaponLoadout.SwordOnly
                : WeaponLoadout.BowOnly;
            manualActivationOnly = false;
            patrolWhenIdle = true;
            ResolveReferences();
            ApplyConfiguredWeaponLoadout();
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
            Vector3 toInitialDestination =
                Vector3.ProjectOnPlane(
                    patrolDestination -
                        transform.position,
                    Vector3.up);
            if (toInitialDestination.magnitude <=
                patrolStopDistance * 1.5f)
            {
                AdvancePatrolRoute();
                patrolDestination =
                    patrolRoute[patrolRouteIndex];
            }
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
            passiveAwareness = 0f;
            drawingBow = false;
            investigationTimer = 0f;
            reachedLastKnownPosition = false;
            lostSightWaitTimer = 0f;
            patrolWhenIdle = false;
            ResolveReferences();
            if (requireManualActivation)
            {
                damageProfile?.ConfigureDormantTrainingDummy();
            }
            else if (damageProfile != null)
            {
                damageProfile.Configure(
                    damageProfile.Variant);
            }
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
            navigationProgressOrigin = transform.position;
            hasNavigationProgressOrigin = true;
            EnsureDamageProfile();
            health.Damaged += HandleDamaged;
            health.Died += HandleDeath;
            if (trainingDummy)
            {
                ConfigureAsTrainingDummy();
            }
        }

        private void OnEnable()
        {
            BowArrowProjectile.ArrowInFlight -= HandleArrowInFlight;
            BowArrowProjectile.ArrowInFlight += HandleArrowInFlight;
            BowArrowProjectile.ArrowImpacted -= HandleArrowImpacted;
            BowArrowProjectile.ArrowImpacted += HandleArrowImpacted;
        }

        private void OnDisable()
        {
            BowArrowProjectile.ArrowInFlight -= HandleArrowInFlight;
            BowArrowProjectile.ArrowImpacted -= HandleArrowImpacted;
        }

        private void OnDestroy()
        {
            BowArrowProjectile.ArrowInFlight -= HandleArrowInFlight;
            BowArrowProjectile.ArrowImpacted -= HandleArrowImpacted;
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

            if (alertReactionTimer > 0f)
            {
                UpdateAlertReaction();
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

            if (weaponLoadout == WeaponLoadout.BowOnly ||
                (weaponLoadout != WeaponLoadout.SwordOnly &&
                 distance > bowRange))
            {
                swordModeActive = false;
                UpdateRangedCombat(chestPoint);
            }
            else
            {
                if (!swordModeActive)
                {
                    EnterSwordEngagement();
                }
                UpdateSwordCombat(direction, distance, chestPoint);
            }
        }

        private void LateUpdate()
        {
            if (state == EnemyState.Dead)
            {
                return;
            }

            ResolveRaidEnvironment();
            if (raidEnvironment == null)
            {
                return;
            }

            controller ??= GetComponent<CharacterController>();
            float padding = controller != null
                ? controller.radius + 0.12f
                : 0.37f;
            if (raidEnvironment.IsEnemyNavigationPositionSafe(
                    transform.position,
                    padding))
            {
                lastSafeNavigationPosition = transform.position;
                hasSafeNavigationPosition = true;
                return;
            }

            if (!hasSafeNavigationPosition)
            {
                return;
            }

            bool controllerWasEnabled =
                controller != null && controller.enabled;
            if (controllerWasEnabled)
            {
                controller.enabled = false;
            }
            transform.position = lastSafeNavigationPosition;
            if (controllerWasEnabled)
            {
                controller.enabled = true;
            }
            motor?.StopMotion();
            SetIntent(Vector2.zero, false, false);
            GameplayEventLog.Publish(
                "enemy-river-entry-prevented",
                gameObject,
                "restored-last-dry-position");
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
            ApplyConfiguredWeaponLoadout();
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
            passiveAwareness = 0f;
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

            Vector2 strafeIntent = ResolveRangedStrafeIntent();
            if (weaponSlots.ActiveSlot !=
                TwoSlotWeaponPresenter.SecondarySlot)
            {
                weaponSlots.RequestSlot(
                    TwoSlotWeaponPresenter.SecondarySlot);
                SetAim(targetChest);
                SetIntent(strafeIntent, false, false);
                ChangeState(EnemyState.Pursuing);
                return;
            }

            SetAim(targetChest);
            SetIntent(strafeIntent, false, false);
            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f || weaponSlots.IsTransitioning)
            {
                ChangeState(EnemyState.Recovering);
                return;
            }

            aimingForHeadshot =
                UnityEngine.Random.value < HeadshotChance;
            heldAimPoint = ResolvePerfectAimPoint();
            SetAim(heldAimPoint);
            drawingBow = true;
            actionTimer = Mathf.Max(
                UnityEngine.Random.Range(
                    minimumBowHold,
                    maximumBowHold),
                bowWeapon.FullDrawDuration +
                    FullDrawSafetyDuration);
            SetIntent(strafeIntent, false, true);
            ChangeState(EnemyState.Windup);
        }

        private void UpdateBowDraw(bool targetVisible)
        {
            if (!targetVisible)
            {
                actionTimer -= Time.deltaTime;
                SetAim(heldAimPoint);
                SetIntent(
                    ResolveOccludedBowMovement(),
                    false,
                    true);
                ChangeState(EnemyState.Windup);
                return;
            }

            Vector2 strafeIntent = ResolveRangedStrafeIntent();
            heldAimPoint = ResolvePerfectAimPoint();
            SetAim(heldAimPoint);
            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f ||
                !IsCommittedBowShotReady())
            {
                SetIntent(strafeIntent, false, true);
                return;
            }

            if (!bowWeapon.CommitNpcFullDrawRelease())
            {
                SetIntent(strafeIntent, false, true);
                return;
            }

            SetIntent(strafeIntent, false, false);
            drawingBow = false;
            actionTimer = UnityEngine.Random.Range(
                minimumBowRecovery,
                maximumBowRecovery);
            ChangeState(EnemyState.Recovering);
        }

        private void UpdatePerception(Vector3 targetChest)
        {
            if (!alerted && TryHearRunningTarget())
            {
                BeginRunningAlert();
                return;
            }

            bool hadVisualContact = hasVisualContact;
            bool targetVisible = TryResolveVisibleTargetPoint(
                targetChest,
                out Vector3 visiblePoint);
            if (!alerted)
            {
                bool targetInForest = IsTargetInForest();
                float distance = Vector3.Distance(
                    transform.position,
                    target.position);
                bool withinContextRange =
                    distance <= (targetInForest
                        ? forestSightRange
                        : passiveSightRange);
                bool crouched = ResolveTargetMotor() != null &&
                    targetMotor.IsCrouched;
                bool recognized =
                    (distance <= immediateRecognitionRange &&
                     targetVisible) ||
                    AdvancePassiveAwareness(
                        Time.deltaTime,
                        targetVisible && withinContextRange,
                        distance,
                        targetInForest,
                        crouched);
                if (!recognized)
                {
                    hasVisualContact = false;
                    return;
                }

                passiveAwareness = 0f;
            }

            hasVisualContact = targetVisible;
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
            lastVisibleTargetPoint = visiblePoint;
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
            reachedLastKnownPosition = false;
            lostSightWaitTimer = lostSightWaitDuration;
            searchAimTimer = searchAimInterval;
        }

        private bool AdvancePassiveAwareness(
            float deltaTime,
            bool visible,
            float distance,
            bool targetInForest,
            bool crouched)
        {
            if (!visible)
            {
                passiveAwareness = Mathf.MoveTowards(
                    passiveAwareness,
                    0f,
                    passiveAwarenessDecay *
                        Mathf.Max(0f, deltaTime));
                return false;
            }

            float requiredDuration =
                CalculatePassiveRecognitionDuration(
                    distance,
                    targetInForest,
                    crouched);
            passiveAwareness = Mathf.Clamp01(
                passiveAwareness +
                Mathf.Max(0f, deltaTime) /
                    Mathf.Max(0.01f, requiredDuration));
            return passiveAwareness >= 1f;
        }

        private float CalculatePassiveRecognitionDuration(
            float distance,
            bool targetInForest,
            bool crouched)
        {
            float maximumRange = targetInForest
                ? forestSightRange
                : passiveSightRange;
            float distanceFactor = Mathf.InverseLerp(
                immediateRecognitionRange,
                Mathf.Max(
                    immediateRecognitionRange + 0.01f,
                    maximumRange),
                distance);
            float duration = Mathf.Lerp(
                minimumRecognitionDuration,
                maximumRecognitionDuration,
                distanceFactor);
            if (targetInForest)
            {
                duration *= forestRecognitionMultiplier;
            }
            if (crouched)
            {
                duration *= crouchedRecognitionMultiplier;
            }
            return duration;
        }

        private bool TryHearRunningTarget()
        {
            ThirdPersonMotor movement = ResolveTargetMotor();
            if (movement == null || target == null)
            {
                return false;
            }

            bool running =
                movement.HorizontalSpeed >
                    movement.WalkSpeed + 0.5f ||
                movement.TargetHorizontalSpeed >=
                    movement.SprintSpeed - 0.1f;
            return running &&
                Vector3.Distance(
                    transform.position,
                    target.position) <= runningHearingRange;
        }

        private void BeginRunningAlert()
        {
            Vector3 sourcePosition = target.position;
            alertFocusPoint = sourcePosition;
            Vector3 direction = Vector3.ProjectOnPlane(
                sourcePosition - transform.position,
                Vector3.up);
            alertSourceDirection =
                direction.sqrMagnitude > 0.001f
                    ? -direction.normalized
                    : -transform.forward;
            alertReactionTimer = runningHearingReactionDuration;
            impactLookTimer = runningHearingReactionDuration;
            AlertAt(sourcePosition, "player-running-heard");
        }

        private ThirdPersonMotor ResolveTargetMotor()
        {
            if (target == null)
            {
                targetMotor = null;
                return null;
            }

            if (targetMotor == null ||
                targetMotor.transform != target)
            {
                targetMotor =
                    target.GetComponent<ThirdPersonMotor>();
            }
            return targetMotor;
        }

        private bool IsTargetInForest()
        {
            if (target == null)
            {
                return false;
            }

            ResolveRaidEnvironment();
            return raidEnvironment != null &&
                raidEnvironment.DistanceToNearestTrail(
                    target.position) > forestTrailClearance;
        }

        private bool CanSeeTarget(Vector3 targetChest)
        {
            return TryResolveVisibleTargetPoint(
                targetChest,
                out _);
        }

        private bool TryResolveVisibleTargetPoint(
            Vector3 targetChest,
            out Vector3 visiblePoint)
        {
            visiblePoint = targetChest;
            if (target == null)
            {
                return false;
            }

            Vector3 origin = drawingBow &&
                bowWeapon != null &&
                bowWeapon.WeaponEquipped
                    ? bowWeapon.PresentedArrowTip
                    : ResolveSightOrigin();
            float maximumDistance =
                alerted ? alertedSightRange : passiveSightRange;
            float viewAngle =
                alerted ? alertedViewAngle : passiveViewAngle;
            Vector3 headPoint = ResolveTargetHeadPoint();
            Vector3 targetRight = target.right;
            Vector3 firstPoint = aimingForHeadshot
                ? headPoint
                : targetChest;
            Vector3 secondPoint = aimingForHeadshot
                ? targetChest
                : headPoint;
            if (HasDirectTargetRay(
                    origin,
                    firstPoint,
                    maximumDistance,
                    viewAngle))
            {
                visiblePoint = firstPoint;
                return true;
            }
            if (HasDirectTargetRay(
                    origin,
                    secondPoint,
                    maximumDistance,
                    viewAngle))
            {
                visiblePoint = secondPoint;
                return true;
            }
            Vector3 rightShoulder =
                targetChest + targetRight * 0.24f;
            if (HasDirectTargetRay(
                    origin,
                    rightShoulder,
                    maximumDistance,
                    viewAngle))
            {
                visiblePoint = rightShoulder;
                return true;
            }
            Vector3 leftShoulder =
                targetChest - targetRight * 0.24f;
            if (HasDirectTargetRay(
                    origin,
                    leftShoulder,
                    maximumDistance,
                    viewAngle))
            {
                visiblePoint = leftShoulder;
                return true;
            }
            Vector3 pelvisPoint =
                target.position + Vector3.up * 0.82f;
            if (HasDirectTargetRay(
                    origin,
                    pelvisPoint,
                    maximumDistance,
                    viewAngle))
            {
                visiblePoint = pelvisPoint;
                return true;
            }
            return false;
        }

        private bool HasDirectTargetRay(
            Vector3 origin,
            Vector3 targetPoint,
            float maximumDistance,
            float viewAngle)
        {
            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f ||
                distance > maximumDistance)
            {
                return false;
            }
            Vector3 planarDirection = Vector3.ProjectOnPlane(
                toTarget,
                Vector3.up);
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
                lastKnownPosition;
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
                int desiredSlot = weaponLoadout ==
                        WeaponLoadout.BowOnly
                    ? TwoSlotWeaponPresenter.SecondarySlot
                    : weaponLoadout == WeaponLoadout.SwordOnly
                        ? TwoSlotWeaponPresenter.PrimarySlot
                        : distanceToLastKnown >
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

            if (distance > investigationStopDistance)
            {
                reachedLastKnownPosition = false;
                Vector3 movement =
                    ResolveRiverAwareDirection(
                        direction,
                        investigationDestination);
                movement = ResolveObstacleAwareDirection(movement);
                SetIntent(
                    WorldDirectionToInput(movement),
                    false,
                    false);
                ChangeState(EnemyState.Investigating);
                return;
            }

            SetIntent(Vector2.zero, false, false);
            ChangeState(EnemyState.Alerted);
            if (!reachedLastKnownPosition)
            {
                reachedLastKnownPosition = true;
                investigationTimer =
                    confirmedEmptySearchDuration;
            }
            investigationTimer -= Time.deltaTime;
            if (investigationTimer > 0f)
            {
                return;
            }

            alerted = false;
            hasVisualContact = false;
            swordModeActive = false;
            reachedLastKnownPosition = false;
            aimSource?.ClearOverride();
            ChangeState(EnemyState.Idle);
        }

        private void UpdatePatrol()
        {
            if (!hasPatrolDestination)
            {
                patrolWaitTimer -= Time.deltaTime;
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Idle);
                if (patrolLookDirection.sqrMagnitude > 0.001f)
                {
                    SetAim(
                        transform.position +
                        patrolLookDirection * 6f +
                        Vector3.up * 0.65f);
                }
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
                patrolWaitTimer =
                    ResolvePatrolPauseDuration();
                float lookYaw = UnityEngine.Random.Range(-100f, 100f);
                patrolLookDirection =
                    Quaternion.AngleAxis(lookYaw, Vector3.up) *
                    transform.forward;
                SetIntent(Vector2.zero, false, false);
                ChangeState(EnemyState.Idle);
                return;
            }

            Vector3 direction =
                toDestination.sqrMagnitude > 0.001f
                    ? toDestination.normalized
                    : transform.forward;
            direction = ResolveRiverAwareDirection(
                direction,
                patrolDestination);
            direction = ResolveObstacleAwareDirection(direction);
            SetIntent(
                WorldDirectionToInput(direction),
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

        private float ResolvePatrolPauseDuration()
        {
            if (patrolRoute == null ||
                patrolRoute.Length == 0)
            {
                return UnityEngine.Random.Range(
                    minimumPatrolWait,
                    maximumPatrolWait);
            }

            float cadence = Mathf.Repeat(
                patrolRouteIndex * 0.6180339f +
                (patrolRouteDirection > 0
                    ? 0.17f
                    : 0.53f),
                1f);
            return Mathf.Lerp(
                minimumPatrolWait,
                maximumPatrolWait,
                cadence);
        }

        private Vector3 ResolveObstacleAwareDirection(
            Vector3 desiredDirection)
        {
            return ResolveObstacleAwareDirectionWithRiverPolicy(
                desiredDirection,
                allowRiverWaypoint: true);
        }

        private Vector3 ResolveObstacleAwareDirectionWithRiverPolicy(
            Vector3 desiredDirection,
            bool allowRiverWaypoint)
        {
            desiredDirection = Vector3.ProjectOnPlane(
                desiredDirection,
                Vector3.up);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }
            desiredDirection.Normalize();
            UpdateNavigationProgress();
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            navigationDetourTimer = Mathf.Max(
                0f,
                navigationDetourTimer - deltaTime);
            navigationRecoveryTimer = Mathf.Max(
                0f,
                navigationRecoveryTimer - deltaTime);

            if (navigationRecoveryTimer > 0f)
            {
                Vector3 recoveryDirection =
                    FindBestNavigationDetour(
                        desiredDirection,
                        true,
                        allowRiverWaypoint);
                if (recoveryDirection.sqrMagnitude > 0.0001f)
                {
                    RememberNavigationDetour(
                        recoveryDirection,
                        0.82f);
                    return recoveryDirection;
                }
            }

            if (navigationDetourTimer > 0f &&
                navigationDetourDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 continuedDetour = (
                    navigationDetourDirection * 0.84f +
                    desiredDirection * 0.16f).normalized;
                if (NavigationClearance(
                        continuedDetour,
                        1.55f) > 0.72f)
                {
                    return ConstrainImmediateRiverStep(
                        continuedDetour,
                        allowRiverWaypoint);
                }
                navigationDetourTimer = 0f;
            }

            float forwardClearance = NavigationClearance(
                desiredDirection,
                NavigationLookAhead);
            if (forwardClearance >= NavigationLookAhead - 0.04f)
            {
                return ConstrainImmediateRiverStep(
                    desiredDirection,
                    allowRiverWaypoint);
            }

            Vector3 detour = FindBestNavigationDetour(
                desiredDirection,
                false,
                allowRiverWaypoint);
            if (detour.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            RememberNavigationDetour(
                detour,
                NavigationDetourDuration);
            return detour;
        }

        private void UpdateNavigationProgress()
        {
            float now = Time.time;
            if (!hasNavigationProgressOrigin ||
                now - navigationLastRequestTime > 0.22f)
            {
                navigationProgressOrigin = transform.position;
                navigationProgressTimer = 0f;
                hasNavigationProgressOrigin = true;
            }
            navigationLastRequestTime = now;

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 progress = Vector3.ProjectOnPlane(
                transform.position - navigationProgressOrigin,
                Vector3.up);
            if (progress.magnitude >= NavigationMinimumProgress)
            {
                navigationProgressOrigin = transform.position;
                navigationProgressTimer = 0f;
                return;
            }

            navigationProgressTimer += deltaTime;
            if (navigationProgressTimer <
                NavigationStuckSampleDuration)
            {
                return;
            }

            navigationProgressOrigin = transform.position;
            navigationProgressTimer = 0f;
            navigationDetourTimer = 0f;
            navigationAvoidanceSide *= -1f;
            navigationRecoveryTimer = 0.9f;
            GameplayEventLog.Publish(
                "enemy-navigation-recovery",
                gameObject,
                "no-progress-wide-detour");
        }

        private Vector3 FindBestNavigationDetour(
            Vector3 desiredDirection,
            bool recovery,
            bool allowRiverWaypoint)
        {
            float maximumDistance = recovery ? 3.8f : 3.35f;
            float preferredSide = navigationAvoidanceSide >= 0f
                ? 1f
                : -1f;
            Vector3 bestDirection = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0
                    ? preferredSide
                    : -preferredSide;
                for (int angleIndex = 0;
                     angleIndex < NavigationFanAngles.Length;
                     angleIndex++)
                {
                    float angle = NavigationFanAngles[angleIndex];
                    Vector3 candidate = Quaternion.AngleAxis(
                        angle * side,
                        Vector3.up) * desiredDirection;
                    candidate = ConstrainImmediateRiverStep(
                        candidate,
                        allowRiverWaypoint);
                    if (candidate.sqrMagnitude <= 0.0001f)
                    {
                        continue;
                    }
                    candidate.Normalize();
                    float clearance = NavigationClearance(
                        candidate,
                        maximumDistance);
                    if (clearance < 0.48f)
                    {
                        continue;
                    }

                    float forwardProgress = Vector3.Dot(
                        candidate,
                        desiredDirection);
                    float score = clearance * 1.35f +
                        forwardProgress * (recovery ? 0.22f : 0.72f) -
                        angle * 0.0015f +
                        (side == preferredSide ? 0.12f : 0f);
                    if (score <= bestScore)
                    {
                        continue;
                    }
                    bestScore = score;
                    bestDirection = candidate;
                    navigationAvoidanceSide = side;
                }
            }
            return bestDirection;
        }

        private void RememberNavigationDetour(
            Vector3 direction,
            float duration)
        {
            navigationDetourDirection = Vector3.ProjectOnPlane(
                direction,
                Vector3.up).normalized;
            navigationDetourTimer = Mathf.Max(
                navigationDetourTimer,
                duration);
        }

        private float NavigationClearance(
            Vector3 direction,
            float maximumDistance)
        {
            direction = Vector3.ProjectOnPlane(
                direction,
                Vector3.up).normalized;
            ResolveNavigationCapsule(
                out Vector3 bottom,
                out Vector3 top,
                out float radius);
            float immediateStep = Mathf.Min(
                maximumDistance,
                radius + 0.16f);
            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                bottom + direction * immediateStep,
                top + direction * immediateStep,
                radius * 0.94f,
                navigationOverlapColliders,
                sightMask,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlapCount; index++)
            {
                if (!ShouldIgnoreNavigationCollider(
                        navigationOverlapColliders[index]))
                {
                    return 0f;
                }
            }

            int hitCount = Physics.CapsuleCastNonAlloc(
                bottom,
                top,
                radius,
                direction,
                navigationProbeHits,
                maximumDistance,
                sightMask,
                QueryTriggerInteraction.Ignore);
            float clearance = maximumDistance;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider =
                    navigationProbeHits[index].collider;
                if (ShouldIgnoreNavigationCollider(collider))
                {
                    continue;
                }
                clearance = Mathf.Min(
                    clearance,
                    navigationProbeHits[index].distance);
            }
            return clearance;
        }

        private void ResolveNavigationCapsule(
            out Vector3 bottom,
            out Vector3 top,
            out float radius)
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }
            if (controller == null)
            {
                radius = 0.27f;
                bottom = transform.position + Vector3.up * radius;
                top = transform.position + Vector3.up * 1.53f;
                return;
            }

            Vector3 scale = transform.lossyScale;
            float horizontalScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.z));
            radius = Mathf.Max(
                0.18f,
                controller.radius * horizontalScale * 0.92f);
            float height = Mathf.Max(
                radius * 2f,
                controller.height * Mathf.Abs(scale.y));
            Vector3 center = transform.TransformPoint(controller.center);
            float halfSegment = Mathf.Max(
                0f,
                height * 0.5f - radius);
            bottom = center - Vector3.up * halfSegment;
            top = center + Vector3.up * halfSegment;
        }

        private bool ShouldIgnoreNavigationCollider(
            Collider collider)
        {
            if (collider == null ||
                collider.transform.IsChildOf(transform) ||
                (target != null &&
                    (collider.transform == target ||
                     collider.transform.IsChildOf(target))))
            {
                return true;
            }

            EnemyBrain otherEnemy =
                collider.GetComponentInParent<EnemyBrain>();
            if (otherEnemy != null)
            {
                return otherEnemy.state != EnemyState.Dead;
            }

            for (Transform current = collider.transform;
                 current != null;
                 current = current.parent)
            {
                if (current.name.StartsWith(
                        "Road Bridge",
                        StringComparison.Ordinal) ||
                    current.name == "Terrain Disc")
                {
                    return true;
                }
            }
            return false;
        }

        private Vector3 ConstrainImmediateRiverStep(
            Vector3 direction,
            bool allowRiverWaypoint = true)
        {
            ResolveRaidEnvironment();
            if (raidEnvironment == null ||
                direction.sqrMagnitude <= 0.0001f)
            {
                return direction;
            }
            direction.Normalize();
            float padding = controller != null
                ? controller.radius + 0.12f
                : 0.37f;
            Vector3 step = transform.position +
                direction * 1.35f;
            if (raidEnvironment.IsEnemyNavigationPositionSafe(
                    step,
                    padding))
            {
                return direction;
            }

            if (!allowRiverWaypoint)
            {
                return Vector3.zero;
            }

            if (raidEnvironment.TryResolveEnemyRiverWaypoint(
                    transform.position,
                    step,
                    out Vector3 waypoint))
            {
                Vector3 towardWaypoint = Vector3.ProjectOnPlane(
                    waypoint - transform.position,
                    Vector3.up);
                if (towardWaypoint.sqrMagnitude > 0.001f)
                {
                    return towardWaypoint.normalized;
                }
            }
            return Vector3.zero;
        }

        private void ResolveRaidEnvironment()
        {
            damageProfile ??=
                GetComponent<EnemyDamageProfile>();
            if (damageProfile == null ||
                damageProfile.Variant !=
                    EnemyCombatVariant.RaidEnemy)
            {
                raidEnvironment = null;
                return;
            }
            if (raidEnvironment == null)
            {
                raidEnvironment =
                    FindFirstObjectByType<ProceduralRaidGenerator>();
            }
        }

        private Vector3 ResolveRiverAwareDirection(
            Vector3 proposedDirection,
            Vector3 strategicDestination)
        {
            ResolveRaidEnvironment();
            Vector3 flatDirection = Vector3.ProjectOnPlane(
                proposedDirection,
                Vector3.up);
            if (flatDirection.sqrMagnitude <= 0.0001f ||
                raidEnvironment == null)
            {
                return flatDirection;
            }
            flatDirection.Normalize();

            if (raidEnvironment.TryResolveEnemyRiverWaypoint(
                    transform.position,
                    strategicDestination,
                    out Vector3 waypoint))
            {
                Vector3 towardWaypoint = Vector3.ProjectOnPlane(
                    waypoint - transform.position,
                    Vector3.up);
                if (towardWaypoint.sqrMagnitude > 0.001f)
                {
                    return towardWaypoint.normalized;
                }
            }

            Vector3 step = transform.position +
                flatDirection * 1.15f;
            float padding = controller != null
                ? controller.radius + 0.12f
                : 0.37f;
            if (raidEnvironment.IsEnemyNavigationPositionSafe(
                    step,
                    padding))
            {
                return flatDirection;
            }

            if (raidEnvironment.TryResolveEnemyRiverWaypoint(
                    transform.position,
                    step,
                    out waypoint))
            {
                Vector3 towardWaypoint = Vector3.ProjectOnPlane(
                    waypoint - transform.position,
                    Vector3.up);
                if (towardWaypoint.sqrMagnitude > 0.001f)
                {
                    return towardWaypoint.normalized;
                }
            }
            return Vector3.zero;
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
            bool preserveCommittedDraw =
                alerted &&
                drawingBow &&
                target != null &&
                bowWeapon != null;
            if (!preserveCommittedDraw)
            {
                CancelBowAttackForInterruption();
            }
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
            reachedLastKnownPosition = false;
            lostSightWaitTimer = 0f;
            searchAimTimer = searchAimInterval;
            if (!preserveCommittedDraw)
            {
                actionTimer = 0f;
            }
            swordModeActive = false;
            if (preserveCommittedDraw)
            {
                ChangeState(EnemyState.Windup);
            }
            else
            {
                EnterGuardPhase();
                ChangeState(EnemyState.Alerted);
            }
            GameplayEventLog.Publish(
                "enemy-alerted",
                gameObject,
                reason);
        }

        private void CancelBowAttackForInterruption()
        {
            if (!drawingBow &&
                (bowWeapon == null || !bowWeapon.IsDrawing))
            {
                return;
            }

            drawingBow = false;
            bowWeapon?.AbortDraw();
            SetIntent(Vector2.zero, false, false);
            GameplayEventLog.Publish(
                "enemy-bow-interrupted",
                gameObject,
                "cancelled-without-release");
        }

        private void UpdateAlertReaction()
        {
            alertReactionTimer = Mathf.Max(
                0f,
                alertReactionTimer - Time.deltaTime);
            Vector3 focus = impactLookTimer > 0f
                ? alertFocusPoint
                : transform.position -
                    alertSourceDirection * alertedSightRange;
            impactLookTimer = Mathf.Max(
                0f,
                impactLookTimer - Time.deltaTime);
            SetAim(focus + Vector3.up * 0.65f);
            SetIntent(Vector2.zero, false, false);
            ChangeState(EnemyState.Alerted);
        }

        private Vector2 ResolveRangedStrafeIntent()
        {
            rangedStrafeTimer -= Time.deltaTime;
            if (rangedStrafeTimer <= 0f)
            {
                rangedStrafeDirection =
                    UnityEngine.Random.value < 0.5f ? -1f : 1f;
                rangedStrafeTimer = UnityEngine.Random.Range(1.6f, 2.8f);
            }

            Vector3 toTarget = target != null
                ? Vector3.ProjectOnPlane(
                    target.position - transform.position,
                    Vector3.up)
                : transform.forward;
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f
                ? toTarget / distance
                : transform.forward;
            Vector3 tangent =
                Vector3.Cross(Vector3.up, direction) *
                rangedStrafeDirection;
            Vector3 movement = tangent * 0.48f;
            if (distance < 4.5f)
            {
                movement -= direction * 0.34f;
            }
            float movementStrength = Mathf.Clamp01(
                movement.magnitude);

            movement = ResolveRangedRiverMovement(movement);
            movement = ResolveObstacleAwareDirectionWithRiverPolicy(
                    movement,
                    allowRiverWaypoint: false) *
                movementStrength;

            return WorldDirectionToInput(movement);
        }

        private Vector3 ResolveRangedRiverMovement(
            Vector3 proposedMovement)
        {
            ResolveRaidEnvironment();
            Vector3 movement = Vector3.ProjectOnPlane(
                proposedMovement,
                Vector3.up);
            if (movement.sqrMagnitude <= 0.0001f ||
                raidEnvironment == null)
            {
                return movement;
            }

            movement.Normalize();
            if (target != null &&
                raidEnvironment.TryResolveEnemyRiverWaypoint(
                    transform.position,
                    target.position,
                    out Vector3 bridgeWaypoint) &&
                ShouldTakeNearbyRangedBridge(
                    transform.position,
                    target.position,
                    bridgeWaypoint))
            {
                Vector3 towardBridge = Vector3.ProjectOnPlane(
                    bridgeWaypoint - transform.position,
                    Vector3.up);
                if (towardBridge.sqrMagnitude > 0.001f)
                {
                    return ConstrainImmediateRiverStep(
                        towardBridge.normalized,
                        allowRiverWaypoint: false);
                }
            }

            float padding = controller != null
                ? controller.radius + 0.12f
                : 0.37f;
            Vector3 step = transform.position + movement * 1.15f;
            if (raidEnvironment.IsEnemyNavigationPositionSafe(
                    step,
                    padding))
            {
                return movement;
            }

            Vector3 reversed = -movement;
            Vector3 reversedStep = transform.position +
                reversed * 1.15f;
            if (raidEnvironment.IsEnemyNavigationPositionSafe(
                    reversedStep,
                    padding))
            {
                rangedStrafeDirection *= -1f;
                return reversed;
            }
            return Vector3.zero;
        }

        public static bool ShouldTakeNearbyRangedBridge(
            Vector3 enemyPosition,
            Vector3 targetPosition,
            Vector3 bridgeWaypoint)
        {
            Vector3 towardTarget = Vector3.ProjectOnPlane(
                targetPosition - enemyPosition,
                Vector3.up);
            Vector3 towardBridge = Vector3.ProjectOnPlane(
                bridgeWaypoint - enemyPosition,
                Vector3.up);
            if (towardTarget.sqrMagnitude <= 0.001f ||
                towardBridge.sqrMagnitude <= 0.001f ||
                towardBridge.magnitude >
                    RangedNearbyBridgeEntryDistance)
            {
                return false;
            }

            return Vector3.Dot(
                    towardTarget.normalized,
                    towardBridge.normalized) >=
                RangedBridgeMinimumForwardDot;
        }

        private Vector2 ResolveOccludedBowMovement()
        {
            Vector3 towardLastKnown = Vector3.ProjectOnPlane(
                lastKnownPosition - transform.position,
                Vector3.up);
            float distance = towardLastKnown.magnitude;
            if (distance <= investigationStopDistance)
            {
                return ResolveRangedStrafeIntent();
            }

            Vector3 direction = ResolveRiverAwareDirection(
                towardLastKnown / distance,
                lastKnownPosition);
            Vector3 obstacleAware =
                ResolveObstacleAwareDirection(direction);
            return WorldDirectionToInput(
                obstacleAware * 0.78f);
        }

        private void HandleArrowInFlight(
            BowArrowProjectile.FlightSignal signal)
        {
            if (!CanHearArrow(signal.Owner) ||
                signal.Projectile == null ||
                signal.Projectile.GetInstanceID() == lastHeardArrowId)
            {
                return;
            }

            Vector3 sightOrigin = ResolveSightOrigin();
            Vector3 closest = ClosestPointOnSegment(
                signal.Start,
                signal.End,
                sightOrigin);
            if (Vector3.Distance(closest, sightOrigin) > nearMissRadius)
            {
                return;
            }

            lastHeardArrowId = signal.Projectile.GetInstanceID();
            Vector3 direction = signal.Direction.sqrMagnitude > 0.001f
                ? signal.Direction.normalized
                : (signal.End - signal.Start).normalized;
            BeginArrowAlert(
                closest,
                direction,
                nearMissReactionDuration,
                0f,
                "arrow-near-miss");
        }

        private void HandleArrowImpacted(
            BowArrowProjectile.ImpactSignal signal)
        {
            if (!CanHearArrow(signal.Owner) ||
                Vector3.Distance(transform.position, signal.Point) >
                    impactHearingRadius)
            {
                return;
            }

            int projectileId = signal.Projectile != null
                ? signal.Projectile.GetInstanceID()
                : 0;
            if (projectileId != 0 && projectileId == lastHeardArrowId)
            {
                return;
            }

            lastHeardArrowId = projectileId;
            BeginArrowAlert(
                signal.Point,
                signal.Direction,
                impactReactionDuration,
                impactLookDuration,
                "arrow-impact-heard");
        }

        private void BeginArrowAlert(
            Vector3 focusPoint,
            Vector3 flightDirection,
            float reactionDuration,
            float lookDuration,
            string reason)
        {
            if (trainingDummy)
            {
                if (manualActivationOnly)
                {
                    return;
                }

                ActivateCombat(
                    preserveCurrentHealth: true,
                    beginAlerted: false);
            }

            bool alreadyEngaged = alerted;
            ResolvePlayerTarget();
            Vector3 direction = Vector3.ProjectOnPlane(
                flightDirection,
                Vector3.up);
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
            }
            direction.Normalize();
            alertFocusPoint = focusPoint;
            alertSourceDirection = direction;
            alertReactionTimer = alreadyEngaged
                ? 0f
                : reactionDuration;
            impactLookTimer = alreadyEngaged
                ? 0f
                : lookDuration;
            AlertAt(
                transform.position -
                    direction * investigationGuessDistance,
                reason);
        }

        private bool CanHearArrow(GameObject arrowOwner)
        {
            return state != EnemyState.Dead &&
                arrowOwner != null &&
                arrowOwner != gameObject &&
                arrowOwner.GetComponent<EnemyBrain>() == null;
        }

        private static Vector3 ClosestPointOnSegment(
            Vector3 start,
            Vector3 end,
            Vector3 point)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return start;
            }

            float progress = Mathf.Clamp01(
                Vector3.Dot(point - start, segment) /
                lengthSquared);
            return start + segment * progress;
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

            bool aggressiveSwordAdvance =
                weaponLoadout == WeaponLoadout.SwordOnly &&
                (meleePhase == MeleePhase.Closing ||
                 meleePhase == MeleePhase.Attacking);
            if (aggressiveSwordAdvance)
            {
                // A facing override intentionally suppresses sprinting in the
                // player motor. While charging, travel direction already faces
                // the sword guard toward the target, so release the override.
                aimSource?.ClearOverride();
            }
            else
            {
                SetAim(targetChest);
            }
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

            movement = ResolveRiverAwareDirection(
                movement,
                transform.position + movement * 2f);
            movement = ResolveObstacleAwareDirection(movement);

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
                Vector3 movement = ResolveRiverAwareDirection(
                    direction,
                    target != null
                        ? target.position
                        : transform.position + direction * distance);
                movement = ResolveObstacleAwareDirection(movement);
                SetIntent(
                    WorldDirectionToInput(movement),
                    false,
                    false,
                    true);
                ChangeState(EnemyState.Pursuing);
                return;
            }

            meleePhase = MeleePhase.Attacking;
            comboPulsesRemaining = 3;
            nextAttackPulse = 0f;
            SetIntent(
                WorldDirectionToInput(direction),
                false,
                false,
                true);
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

            Vector3 movement = Vector3.zero;
            if (distance > 0.58f)
            {
                movement = ResolveRiverAwareDirection(
                    direction,
                    target != null
                        ? target.position
                        : transform.position +
                            direction * distance);
                movement = ResolveObstacleAwareDirection(movement);
            }
            SetIntent(
                WorldDirectionToInput(movement),
                attackPressed,
                false,
                true);
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
            movement = ResolveRiverAwareDirection(
                movement,
                transform.position + movement * 2f);
            movement = ResolveObstacleAwareDirection(movement);
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

        private void EnterSwordEngagement()
        {
            swordModeActive = true;
            if (weaponLoadout == WeaponLoadout.SwordOnly)
            {
                meleePhase = MeleePhase.Closing;
                meleePhaseTimer = 0f;
                comboPulsesRemaining = 0;
                ChangeState(EnemyState.Pursuing);
                return;
            }

            EnterGuardPhase();
        }

        private Vector3 ResolvePerfectAimPoint()
        {
            Vector3 targetPoint = hasVisualContact &&
                lastVisibleTargetPoint != Vector3.zero
                    ? lastVisibleTargetPoint
                    : aimingForHeadshot
                        ? ResolveTargetHeadPoint()
                        : ResolveTargetChestPoint();
            Vector3 velocity = Vector3.zero;
            ThirdPersonMotor targetMotor =
                target.GetComponent<ThirdPersonMotor>();
            if (targetMotor != null)
            {
                velocity = targetMotor.HorizontalVelocity;
            }

            Vector3 launchPoint =
                bowWeapon != null
                    ? bowWeapon.PresentedArrowTip
                    : transform.position +
                        Vector3.up * 1.35f;
            float shotSpeed =
                bowWeapon != null
                    ? bowWeapon.MaximumArrowSpeed
                    : 75f;
            float safeShotSpeed = Mathf.Max(1f, shotSpeed);
            float flightTime = Vector3.Distance(
                launchPoint,
                targetPoint) / safeShotSpeed;
            Vector3 direction =
                (targetPoint - launchPoint).normalized;
            for (int iteration = 0;
                 iteration < 5;
                 iteration++)
            {
                Vector3 futureTarget =
                    targetPoint +
                    velocity * flightTime;
                if (!TryResolveBallisticDirection(
                        launchPoint,
                        futureTarget,
                        safeShotSpeed,
                        out direction,
                        out flightTime))
                {
                    Vector3 compensatedAim =
                        futureTarget -
                        Physics.gravity *
                        (0.5f * flightTime * flightTime);
                    direction =
                        (compensatedAim - launchPoint).normalized;
                    flightTime = Vector3.Distance(
                        launchPoint,
                        compensatedAim) / safeShotSpeed;
                }
            }

            return launchPoint + direction * 150f;
        }

        private static bool TryResolveBallisticDirection(
            Vector3 launchPoint,
            Vector3 targetPoint,
            float shotSpeed,
            out Vector3 direction,
            out float flightTime)
        {
            Vector3 delta = targetPoint - launchPoint;
            Vector3 horizontal = Vector3.ProjectOnPlane(
                delta,
                Vector3.up);
            float horizontalDistance = horizontal.magnitude;
            float gravity = Mathf.Abs(Physics.gravity.y);
            float speedSquared = shotSpeed * shotSpeed;
            float discriminant =
                speedSquared * speedSquared -
                gravity *
                (gravity * horizontalDistance * horizontalDistance +
                 2f * delta.y * speedSquared);
            if (horizontalDistance <= 0.001f ||
                gravity <= 0.001f ||
                discriminant < 0f)
            {
                direction = delta.sqrMagnitude > 0.0001f
                    ? delta.normalized
                    : Vector3.forward;
                flightTime = delta.magnitude /
                    Mathf.Max(1f, shotSpeed);
                return false;
            }

            float tangent =
                (speedSquared - Mathf.Sqrt(discriminant)) /
                (gravity * horizontalDistance);
            float cosine = 1f / Mathf.Sqrt(1f + tangent * tangent);
            float sine = tangent * cosine;
            direction =
                horizontal.normalized * cosine +
                Vector3.up * sine;
            flightTime = horizontalDistance /
                Mathf.Max(0.001f, shotSpeed * cosine);
            return true;
        }

        private void SetAim(Vector3 worldPoint)
        {
            Vector3 origin;
            if (bowWeapon != null &&
                bowWeapon.WeaponEquipped)
            {
                origin = bowWeapon.PresentedArrowTip;
            }
            else
            {
                origin = animator != null &&
                    animator.GetBoneTransform(
                        HumanBodyBones.Head) != null
                        ? animator.GetBoneTransform(
                            HumanBodyBones.Head).position
                        : transform.position +
                            Vector3.up * 1.45f;
            }
            aimSource?.SetOverride(
                origin,
                worldPoint - origin);
        }

        private bool IsCommittedBowShotReady()
        {
            return bowWeapon != null &&
                bowWeapon.ArrowReady &&
                bowWeapon.IsDrawing &&
                bowWeapon.DrawNormalized >=
                    MinimumCommittedBowCharge;
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
                : target.position + Vector3.up * 1.20f;
        }

        private Vector3 ResolveTargetHeadPoint()
        {
            Animator targetAnimator =
                target.GetComponentInChildren<Animator>(true);
            Transform head = targetAnimator != null &&
                targetAnimator.isHuman
                    ? targetAnimator.GetBoneTransform(
                        HumanBodyBones.Head)
                    : null;
            return head != null
                ? head.position
                : target.position + Vector3.up * 1.70f;
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
            bool block,
            bool sprint = false)
        {
            input?.SetDiagnosticOverride(
                new PlayerIntent(
                    move,
                    Vector2.zero,
                    sprint,
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

        private void ApplyConfiguredWeaponLoadout()
        {
            if (weaponLoadout == WeaponLoadout.BowOnly)
            {
                weaponSlots?.ConfigureBowOnlyLoadout();
            }
            else if (weaponLoadout == WeaponLoadout.SwordOnly)
            {
                weaponSlots?.ConfigureSwordOnlyLoadout();
            }
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
