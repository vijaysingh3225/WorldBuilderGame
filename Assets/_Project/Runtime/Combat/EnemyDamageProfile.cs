using UnityEngine;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Combat
{
    public enum EnemyCombatVariant
    {
        CombatLabDummy = 0,
        RaidEnemy = 1
    }

    public enum HumanoidHitRegion
    {
        Head = 0,
        Torso = 1,
        Limb = 2
    }

    [DefaultExecutionOrder(-45)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyDamageProfile :
        MonoBehaviour,
        IDamageable
    {
        public const float FullDrawHeadDamage = 105f;
        public const float FullDrawTorsoDamage = 58f;
        public const float FullDrawLimbDamage = 27.5f;

        [SerializeField]
        private EnemyCombatVariant variant =
            EnemyCombatVariant.CombatLabDummy;
        [SerializeField, Min(1f)]
        private float maximumHealth = 100f;
        [SerializeField, Min(1)]
        private int headHitsToKill = 1;
        [SerializeField, Min(1)]
        private int torsoHitsToKill = 2;
        [SerializeField, Min(1)]
        private int limbHitsToKill = 4;

        private Health health;
        private Animator animator;
        private FloatingDamageNumberPresenter damageNumbers;

        public EnemyCombatVariant Variant => variant;
        public int HeadHitsToKill => headHitsToKill;
        public int TorsoHitsToKill => torsoHitsToKill;
        public int LimbHitsToKill => limbHitsToKill;
        public float MaximumHealth => maximumHealth;
        public bool IsAlive =>
            health != null && health.IsAlive;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Configure(
            EnemyCombatVariant combatVariant,
            bool resetHealth = true)
        {
            variant = combatVariant;
            headHitsToKill = 1;
            torsoHitsToKill = 2;
            limbHitsToKill = 4;
            ResolveReferences();
            if (resetHealth && health != null)
            {
                // Enemy profiles must always be killable. Configure() preserves
                // an existing floor, so explicitly clear the legacy dummy floor.
                health.ConfigureWithFloor(maximumHealth, 0f);
            }
        }

        public void ReceiveDamage(
            in DamageRequest request)
        {
            ReceiveDamage(
                ResolveHitRegion(request.HitPoint),
                request);
        }

        public HumanoidHitRegion ResolveHitRegion(
            Vector3 hitPoint)
        {
            return ResolveRegion(hitPoint);
        }

        public Transform ResolveAttachmentTransform(
            Vector3 hitPoint)
        {
            ResolveReferences();
            if (animator == null || !animator.isHuman)
            {
                return transform;
            }

            Transform closest = null;
            float closestDistance =
                float.PositiveInfinity;
            HumanBodyBones[] bones =
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot
            };
            for (int index = 0;
                 index < bones.Length;
                 index++)
            {
                Transform bone =
                    animator.GetBoneTransform(
                        bones[index]);
                if (bone == null)
                {
                    continue;
                }
                float distance =
                    Vector3.SqrMagnitude(
                        hitPoint - bone.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = bone;
                }
            }
            return closest != null
                ? closest
                : transform;
        }

        public void ReceiveDamage(
            HumanoidHitRegion region,
            in DamageRequest request)
        {
            ResolveReferences();
            if (health == null ||
                !health.IsAlive ||
                request.Amount <= 0f)
            {
                return;
            }

            float amount = request.Amount;
            if (request.SourceId == "prototype-bow")
            {
                float regionMultiplier =
                    region == HumanoidHitRegion.Head
                        ? FullDrawHeadDamage / 100f
                        : region == HumanoidHitRegion.Torso
                            ? FullDrawTorsoDamage / 100f
                            : FullDrawLimbDamage / 100f;
                amount *= regionMultiplier;
            }
            else if (request.SourceId ==
                     "prototype-sword")
            {
                amount = health.Maximum;
            }

            health.ReceiveDamage(
                new DamageRequest(
                    request.Instigator,
                    amount,
                    request.HitPoint,
                    request.Direction,
                    request.SourceId));
        }

        public void ConfigureDormantTrainingDummy()
        {
            ResolveReferences();
            if (health != null)
            {
                health.ConfigureWithFloor(
                    maximumHealth,
                    maximumHealth);
            }
        }

        private void ResolveReferences()
        {
            health ??= GetComponent<Health>();
            animator ??=
                GetComponentInChildren<Animator>(true);
            damageNumbers ??=
                GetComponent<
                    FloatingDamageNumberPresenter>();
            if (damageNumbers == null)
            {
                damageNumbers = gameObject.AddComponent<
                    FloatingDamageNumberPresenter>();
            }
            damageNumbers.Configure(health, this);
        }

        private HumanoidHitRegion ResolveRegion(
            Vector3 hitPoint)
        {
            ResolveReferences();
            if (animator == null || !animator.isHuman)
            {
                float localHeight =
                    transform.InverseTransformPoint(
                        hitPoint).y;
                return localHeight > 1.48f
                    ? HumanoidHitRegion.Head
                    : localHeight > 0.62f
                        ? HumanoidHitRegion.Torso
                        : HumanoidHitRegion.Limb;
            }

            float headDistance =
                BoneDistance(
                    HumanBodyBones.Head,
                    hitPoint);
            float torsoDistance =
                MinimumBoneDistance(
                    hitPoint,
                    HumanBodyBones.Hips,
                    HumanBodyBones.Spine,
                    HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest,
                    HumanBodyBones.Neck);
            float limbDistance =
                MinimumBoneDistance(
                    hitPoint,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand,
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot,
                    HumanBodyBones.RightUpperLeg,
                    HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot);
            if (headDistance <= torsoDistance &&
                headDistance <= limbDistance)
            {
                return HumanoidHitRegion.Head;
            }
            return torsoDistance <= limbDistance
                ? HumanoidHitRegion.Torso
                : HumanoidHitRegion.Limb;
        }

        private float MinimumBoneDistance(
            Vector3 hitPoint,
            params HumanBodyBones[] bones)
        {
            float minimum = float.PositiveInfinity;
            for (int index = 0;
                 index < bones.Length;
                 index++)
            {
                minimum =
                    Mathf.Min(
                        minimum,
                        BoneDistance(
                            bones[index],
                            hitPoint));
            }
            return minimum;
        }

        private float BoneDistance(
            HumanBodyBones bone,
            Vector3 hitPoint)
        {
            Transform boneTransform =
                animator.GetBoneTransform(bone);
            return boneTransform != null
                ? Vector3.Distance(
                    boneTransform.position,
                    hitPoint)
                : float.PositiveInfinity;
        }
    }
}
