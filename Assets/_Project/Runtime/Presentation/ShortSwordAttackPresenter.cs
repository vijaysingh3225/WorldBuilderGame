using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ShortSwordAttackPresenter : MonoBehaviour
    {
        public const string AttackLayerName = "Short Sword Attack V4";
        public const string AttackStateName = "Diagonal Sword Slash V4";
        public const float AttackDuration = 0.72f;

        private static readonly int AttackStateHash = Animator.StringToHash(AttackStateName);
        private const float ForearmTwistShare = 0.72f;

        [SerializeField] private Animator animator;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private Transform swordRoot;

        private Transform rightHand;
        private Transform rightLowerArm;
        private Transform rightIndexProximal;
        private Transform rightMiddleProximal;
        private Transform rightLittleProximal;
        private int attackLayerIndex = -1;
        private float attackStartTime;
        private bool attackActive;
        private bool subscribed;
        private Vector3 attackStartHandLocal;
        private Vector3 desiredGripDirection;
        private Vector3 desiredBladePlaneNormal;
        private float desiredBladePlaneWeight;

        public bool IsAttacking => attackActive;
        public Vector3 SwordDirection => swordRoot != null ? swordRoot.up : Vector3.zero;
        public Vector3 BladePlaneNormal => swordRoot != null ? swordRoot.forward : Vector3.zero;
        public float BladePlaneAlignmentError
        {
            get
            {
                if (swordRoot == null || desiredBladePlaneWeight < 0.99f ||
                    desiredBladePlaneNormal.sqrMagnitude < 0.9f)
                {
                    return 0f;
                }

                return Vector3.Angle(swordRoot.forward, desiredBladePlaneNormal);
            }
        }

        public void Configure(
            Animator targetAnimator,
            Transform root,
            ThirdPersonMotor movementMotor,
            MeleeWeapon meleeWeapon,
            Transform equippedSwordRoot)
        {
            Unsubscribe();
            animator = targetAnimator;
            playerRoot = root;
            motor = movementMotor;
            weapon = meleeWeapon;
            swordRoot = equippedSwordRoot;
            ResolveAnimatorState();
            Subscribe();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            weapon ??= GetComponentInParent<MeleeWeapon>();
            motor ??= GetComponentInParent<ThirdPersonMotor>();
            playerRoot ??= weapon != null ? weapon.transform : transform.root;
            ResolveAnimatorState();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (animator != null && attackLayerIndex >= 0)
            {
                animator.SetLayerWeight(attackLayerIndex, 0f);
            }
        }

        private void Update()
        {
            if (animator == null || attackLayerIndex < 0 || !attackActive)
            {
                return;
            }

            float elapsed = Time.time - attackStartTime;
            animator.SetLayerWeight(attackLayerIndex, AttackBlendWeight(elapsed));
            if (elapsed < AttackDuration)
            {
                return;
            }

            animator.SetLayerWeight(attackLayerIndex, 0f);
            attackActive = false;
        }

        private void LateUpdate()
        {
            if (swordRoot == null || rightHand == null || rightLowerArm == null ||
                rightIndexProximal == null || rightLittleProximal == null ||
                playerRoot == null)
            {
                return;
            }

            Vector3 forearmDirection =
                (rightHand.position - rightLowerArm.position).normalized;
            if (!attackActive)
            {
                desiredGripDirection = LockedCarryDirection(forearmDirection);
                desiredBladePlaneWeight = 0f;
            }

            Vector3 currentGripAxis = GetGripAxis();
            if (attackActive && desiredBladePlaneWeight > 0f)
            {
                ApplyForearmTwist(
                    forearmDirection,
                    currentGripAxis,
                    desiredBladePlaneNormal,
                    desiredBladePlaneWeight);
                forearmDirection =
                    (rightHand.position - rightLowerArm.position).normalized;
                currentGripAxis = GetGripAxis();
            }

            if (!TryBuildSwordRotation(
                    currentGripAxis,
                    forearmDirection,
                    out Quaternion currentSwordRotation) ||
                !TryBuildSwordRotation(
                    desiredGripDirection,
                    forearmDirection,
                    out Quaternion naturalTargetRotation))
            {
                return;
            }

            Vector3 targetPlaneNormal = Vector3.ProjectOnPlane(
                desiredBladePlaneNormal,
                desiredGripDirection).normalized;
            Vector3 targetSwordForward = naturalTargetRotation * Vector3.forward;
            if (desiredBladePlaneWeight > 0f &&
                targetPlaneNormal.sqrMagnitude >= 0.9f)
            {
                targetSwordForward = Vector3.Slerp(
                    targetSwordForward,
                    targetPlaneNormal,
                    desiredBladePlaneWeight).normalized;
            }

            Quaternion targetSwordRotation = Quaternion.LookRotation(
                targetSwordForward,
                desiredGripDirection);
            rightHand.rotation =
                targetSwordRotation *
                Quaternion.Inverse(currentSwordRotation) *
                rightHand.rotation;

            Vector3 swordDirection = targetSwordRotation * Vector3.up;
            Vector3 knuckleCenter = rightMiddleProximal != null
                ? rightMiddleProximal.position
                : (rightIndexProximal.position + rightLittleProximal.position) * 0.5f;
            Vector3 palmCenter = Vector3.Lerp(rightHand.position, knuckleCenter, 0.68f);
            const float gripCenterFromPommel = 0.09f;
            swordRoot.SetPositionAndRotation(
                palmCenter - swordDirection * gripCenterFromPommel,
                targetSwordRotation);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || playerRoot == null || layerIndex != 0)
            {
                return;
            }

            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            if (!attackActive)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
                return;
            }

            float elapsed = Time.time - attackStartTime;
            float normalized = Mathf.Clamp01(elapsed / AttackDuration);
            Vector3 currentLocomotionHand = rightHand != null
                ? playerRoot.InverseTransformPoint(rightHand.position)
                : attackStartHandLocal;
            Vector3 restingHand = currentLocomotionHand;
            Vector3 restingElbow = rightLowerArm != null
                ? playerRoot.InverseTransformPoint(rightLowerArm.position)
                : new Vector3(0.50f, 0.18f, 0.08f);
            Vector3 windup = new Vector3(0.64f, 0.73f, 0.04f);
            Vector3 contact = new Vector3(-0.36f, -0.02f, 0.58f);
            Vector3 followThrough = new Vector3(-0.46f, -0.22f, 0.40f);
            Vector3 restingForearmDirection =
                (rightHand.position - rightLowerArm.position).normalized;
            Vector3 restingSwordDirection =
                LockedCarryDirection(restingForearmDirection);
            Vector3 slashPlaneNormal =
                (playerRoot.forward + playerRoot.right * 0.22f).normalized;
            Vector3 windupSwordDirection = ConstrainToPlane(
                playerRoot.up + playerRoot.right * 0.18f,
                slashPlaneNormal);
            Vector3 contactSwordDirection = ConstrainToPlane(
                -playerRoot.right * 0.82f - playerRoot.up * 0.28f,
                slashPlaneNormal);
            Vector3 followThroughSwordDirection = ConstrainToPlane(
                -playerRoot.right * 0.50f - playerRoot.up * 0.66f,
                slashPlaneNormal);

            Vector3 targetLocal;
            Vector3 elbowHintLocal;
            Vector3 targetSwordDirection;
            float bladePlaneWeight;
            if (normalized < 0.27f)
            {
                float phase = Smooth01(normalized / 0.27f);
                targetLocal = Vector3.Lerp(attackStartHandLocal, windup, phase);
                targetSwordDirection =
                    Vector3.Slerp(restingSwordDirection, windupSwordDirection, phase);
                bladePlaneWeight = phase;
                elbowHintLocal = Vector3.Lerp(
                    restingElbow,
                    new Vector3(0.74f, 0.38f, -0.04f),
                    phase);
            }
            else if (normalized < 0.58f)
            {
                float phase = Smooth01((normalized - 0.27f) / 0.31f);
                targetLocal = Vector3.Lerp(windup, contact, phase);
                targetSwordDirection =
                    Vector3.Slerp(windupSwordDirection, contactSwordDirection, phase);
                bladePlaneWeight = 1f;
                elbowHintLocal = Vector3.Lerp(
                    new Vector3(0.74f, 0.38f, -0.04f),
                    new Vector3(0.18f, 0.18f, 0.32f),
                    phase);
            }
            else if (normalized < 0.73f)
            {
                float phase = Smooth01((normalized - 0.58f) / 0.15f);
                targetLocal = Vector3.Lerp(contact, followThrough, phase);
                targetSwordDirection =
                    Vector3.Slerp(contactSwordDirection, followThroughSwordDirection, phase);
                bladePlaneWeight = 1f;
                elbowHintLocal = Vector3.Lerp(
                    new Vector3(0.18f, 0.18f, 0.32f),
                    new Vector3(0.06f, 0.12f, 0.24f),
                    phase);
            }
            else
            {
                float phase = Smooth01((normalized - 0.73f) / 0.27f);
                targetLocal = Vector3.Lerp(followThrough, restingHand, phase);
                targetSwordDirection =
                    Vector3.Slerp(followThroughSwordDirection, restingSwordDirection, phase);
                bladePlaneWeight = 1f - phase;
                elbowHintLocal = Vector3.Lerp(
                    new Vector3(0.06f, 0.12f, 0.24f),
                    restingElbow,
                    phase);
            }

            float weight = AttackBlendWeight(elapsed);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPosition(
                AvatarIKGoal.RightHand,
                playerRoot.TransformPoint(targetLocal));
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
            animator.SetIKHintPosition(
                AvatarIKHint.RightElbow,
                playerRoot.TransformPoint(elbowHintLocal));
            ApplyWristRotation(
                targetSwordDirection,
                slashPlaneNormal,
                bladePlaneWeight);
        }

        private void OnAttackStarted()
        {
            if (animator == null || playerRoot == null)
            {
                return;
            }

            if (attackLayerIndex < 0)
            {
                ResolveAnimatorState();
            }

            if (attackLayerIndex < 0)
            {
                return;
            }

            attackStartHandLocal = rightHand != null
                ? playerRoot.InverseTransformPoint(rightHand.position)
                : new Vector3(0.38f, 0.02f, 0.12f);
            attackStartTime = Time.time;
            attackActive = true;
            animator.SetLayerWeight(attackLayerIndex, 0f);
            animator.Play(AttackStateHash, attackLayerIndex, 0f);
        }

        private void ResolveAnimatorState()
        {
            if (animator == null)
            {
                return;
            }

            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightIndexProximal = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            rightMiddleProximal = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            rightLittleProximal = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            desiredGripDirection = playerRoot != null
                ? playerRoot.forward
                : transform.forward;
            attackLayerIndex = animator.GetLayerIndex(AttackLayerName);
            if (attackLayerIndex >= 0)
            {
                animator.SetLayerWeight(attackLayerIndex, 0f);
            }
        }

        private void Subscribe()
        {
            if (subscribed || weapon == null || !isActiveAndEnabled)
            {
                return;
            }

            weapon.AttackStarted += OnAttackStarted;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || weapon == null)
            {
                return;
            }

            weapon.AttackStarted -= OnAttackStarted;
            subscribed = false;
        }

        private static float AttackBlendWeight(float elapsed)
        {
            return Mathf.Min(
                Mathf.Clamp01(elapsed / 0.06f),
                Mathf.Clamp01((AttackDuration - elapsed) / 0.10f));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void ApplyWristRotation(
            Vector3 targetSwordDirection,
            Vector3 bladePlaneNormal,
            float bladePlaneWeight)
        {
            if (targetSwordDirection.sqrMagnitude < 0.9f)
            {
                return;
            }

            desiredGripDirection = targetSwordDirection.normalized;
            desiredBladePlaneNormal = bladePlaneNormal.normalized;
            desiredBladePlaneWeight = Mathf.Clamp01(bladePlaneWeight);
        }

        private Vector3 LockedCarryDirection(Vector3 forearmDirection)
        {
            if (playerRoot == null || forearmDirection.sqrMagnitude < 0.9f)
            {
                return transform.forward;
            }

            Vector3 lockedDirection = Vector3.ProjectOnPlane(
                playerRoot.forward,
                forearmDirection).normalized;
            if (lockedDirection.sqrMagnitude < 0.9f)
            {
                lockedDirection = Vector3.ProjectOnPlane(
                    playerRoot.up,
                    forearmDirection).normalized;
            }

            return lockedDirection.sqrMagnitude >= 0.9f
                ? lockedDirection
                : transform.forward;
        }

        private bool TryBuildSwordRotation(
            Vector3 gripDirection,
            Vector3 forearmDirection,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (gripDirection.sqrMagnitude < 0.9f ||
                forearmDirection.sqrMagnitude < 0.9f)
            {
                return false;
            }

            gripDirection.Normalize();
            Vector3 swordRight =
                Vector3.ProjectOnPlane(forearmDirection, gripDirection).normalized;
            if (swordRight.sqrMagnitude < 0.9f)
            {
                swordRight = Vector3.ProjectOnPlane(
                    playerRoot.right,
                    gripDirection).normalized;
            }

            if (swordRight.sqrMagnitude < 0.9f)
            {
                return false;
            }

            Vector3 bladePlaneNormal =
                Vector3.Cross(swordRight, gripDirection).normalized;
            rotation = Quaternion.LookRotation(
                bladePlaneNormal,
                gripDirection);
            return true;
        }

        private void ApplyForearmTwist(
            Vector3 forearmDirection,
            Vector3 currentGripDirection,
            Vector3 targetBladePlaneNormal,
            float weight)
        {
            if (rightLowerArm == null ||
                !TryBuildSwordRotation(
                    currentGripDirection,
                    forearmDirection,
                    out Quaternion currentRotation))
            {
                return;
            }

            Vector3 currentNormal = Vector3.ProjectOnPlane(
                currentRotation * Vector3.forward,
                forearmDirection).normalized;
            Vector3 targetNormal = Vector3.ProjectOnPlane(
                targetBladePlaneNormal,
                forearmDirection).normalized;
            if (currentNormal.sqrMagnitude < 0.9f ||
                targetNormal.sqrMagnitude < 0.9f)
            {
                return;
            }

            float twistAngle = Mathf.Clamp(
                Vector3.SignedAngle(
                    currentNormal,
                    targetNormal,
                    forearmDirection),
                -75f,
                75f);
            rightLowerArm.rotation =
                Quaternion.AngleAxis(
                    twistAngle * Mathf.Clamp01(weight) * ForearmTwistShare,
                    forearmDirection) *
                rightLowerArm.rotation;
        }

        private static Vector3 ConstrainToPlane(
            Vector3 direction,
            Vector3 planeNormal)
        {
            Vector3 constrained = Vector3.ProjectOnPlane(direction, planeNormal);
            return constrained.sqrMagnitude >= 0.0001f
                ? constrained.normalized
                : direction.normalized;
        }

        private Vector3 GetGripAxis()
        {
            if (rightIndexProximal == null || rightLittleProximal == null)
            {
                return Vector3.zero;
            }

            return (rightIndexProximal.position - rightLittleProximal.position).normalized;
        }
    }
}
