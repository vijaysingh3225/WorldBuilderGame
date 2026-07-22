using System;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Core;

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

        [SerializeField] private Transform target;
        [SerializeField] private bool trainingDummy = true;
        [SerializeField, Min(0f)] private float detectionRange = 13f;
        [SerializeField, Min(0f)] private float movementSpeed = 2.6f;
        [SerializeField, Min(0f)] private float turnSpeed = 420f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.65f;
        [SerializeField, Min(0.05f)] private float windupDuration = 0.62f;
        [SerializeField, Min(0.05f)] private float recoveryDuration = 0.78f;
        [SerializeField, Min(0f)] private float attackDamage = 14f;
        [SerializeField, Min(0f)] private float gravity = 28f;

        private CharacterController controller;
        private Health health;
        private EnemyState state;
        private float stateTimeRemaining;
        private float verticalVelocity;

        public event Action<EnemyState> StateChanged;

        public EnemyState CurrentState => state;

        public void Configure(Transform pursuitTarget)
        {
            target = pursuitTarget;
            trainingDummy = false;
        }

        public void ConfigureAsTrainingDummy()
        {
            target = null;
            trainingDummy = true;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            state = EnemyState.Idle;
        }

        private void OnEnable()
        {
            health.Died += HandleDeath;
        }

        private void OnDisable()
        {
            health.Died -= HandleDeath;
        }

        private void Update()
        {
            if (state == EnemyState.Dead)
            {
                return;
            }

            if (trainingDummy)
            {
                if (state != EnemyState.Idle)
                {
                    ChangeState(EnemyState.Idle);
                }

                ApplyMotion(Vector3.zero);
                return;
            }

            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                target = player != null ? player.transform : null;
                ApplyMotion(Vector3.zero);
                return;
            }

            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && !targetHealth.IsAlive)
            {
                ChangeState(EnemyState.Idle);
                ApplyMotion(Vector3.zero);
                return;
            }

            Vector3 toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.zero;

            switch (state)
            {
                case EnemyState.Idle:
                    ApplyMotion(Vector3.zero);
                    if (distance <= detectionRange)
                    {
                        ChangeState(EnemyState.Pursuing);
                    }
                    break;

                case EnemyState.Pursuing:
                    Face(direction);
                    if (distance <= attackRange)
                    {
                        ChangeState(EnemyState.Windup, windupDuration);
                        ApplyMotion(Vector3.zero);
                    }
                    else
                    {
                        ApplyMotion(direction * movementSpeed);
                    }
                    break;

                case EnemyState.Windup:
                    Face(direction);
                    ApplyMotion(Vector3.zero);
                    stateTimeRemaining -= Time.deltaTime;
                    if (stateTimeRemaining <= 0f)
                    {
                        ResolveAttack(distance);
                        ChangeState(EnemyState.Recovering, recoveryDuration);
                    }
                    break;

                case EnemyState.Recovering:
                    ApplyMotion(Vector3.zero);
                    stateTimeRemaining -= Time.deltaTime;
                    if (stateTimeRemaining <= 0f)
                    {
                        ChangeState(EnemyState.Pursuing);
                    }
                    break;
            }
        }

        private void ResolveAttack(float distance)
        {
            if (target == null || distance > attackRange + 0.35f)
            {
                GameplayEventLog.Publish("enemy-miss", gameObject, "basic-strike");
                return;
            }

            Vector3 direction = (target.position - transform.position).normalized;
            DamageRequest request = new DamageRequest(gameObject, attackDamage, target.position, direction, "enemy-basic-strike");
            DamageService.TryApply(target.gameObject, request);
        }

        private void ApplyMotion(Vector3 horizontalVelocity)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private void ChangeState(EnemyState nextState, float duration = 0f)
        {
            if (state == nextState && duration <= 0f)
            {
                return;
            }

            state = nextState;
            stateTimeRemaining = duration;
            StateChanged?.Invoke(state);
            GameplayEventLog.Publish("enemy-state", gameObject, state.ToString());
        }

        private void HandleDeath(DamageRequest request)
        {
            ChangeState(EnemyState.Dead);
            controller.enabled = false;
        }
    }
}
