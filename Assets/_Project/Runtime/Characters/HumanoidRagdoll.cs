using System.Collections.Generic;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class HumanoidRagdoll : MonoBehaviour
    {
        private readonly Dictionary<HumanBodyBones, Rigidbody> bodies =
            new Dictionary<HumanBodyBones, Rigidbody>();

        [SerializeField] private Animator animator;
        private Health health;
        private bool activated;

        private static readonly HumanBodyBones[] PhysicsBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg
        };

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

        public Transform ResolveAttachmentTransform(
            Vector3 hitPoint)
        {
            animator ??= GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                return transform;
            }

            Transform closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int index = 0;
                 index < PhysicsBones.Length;
                 index++)
            {
                Transform bone = animator.GetBoneTransform(
                    PhysicsBones[index]);
                if (bone == null)
                {
                    continue;
                }
                float distance = Vector3.SqrMagnitude(
                    hitPoint - bone.position);
                if (distance >= closestDistance)
                {
                    continue;
                }
                closestDistance = distance;
                closest = bone;
            }
            return closest != null ? closest : transform;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            animator ??= GetComponentInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            health ??= GetComponent<Health>();
            health.Died += Activate;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= Activate;
            }
        }

        private void Activate(DamageRequest finalHit)
        {
            if (activated || animator == null || !animator.isHuman)
            {
                return;
            }

            activated = true;
            DisableCharacterCollision();
            BuildBodies();
            BuildJoints();
            IgnorePlayerCollision();
            animator.enabled = false;
            DisableCharacterControllers();

            if (bodies.TryGetValue(HumanBodyBones.Chest, out Rigidbody chest))
            {
                Vector3 impulse = finalHit.Direction * 2.3f + Vector3.up * 0.35f;
                chest.AddForceAtPosition(
                    impulse,
                    finalHit.HitPoint == Vector3.zero
                        ? chest.worldCenterOfMass
                        : finalHit.HitPoint,
                    ForceMode.Impulse);
            }
        }

        private void DisableCharacterCollision()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }
        }

        private void BuildBodies()
        {
            bodies.Clear();
            for (int index = 0; index < PhysicsBones.Length; index++)
            {
                HumanBodyBones boneId = PhysicsBones[index];
                Transform bone = animator.GetBoneTransform(boneId);
                if (bone == null)
                {
                    continue;
                }

                Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = GetMass(boneId);
                body.linearDamping = 0.08f;
                body.angularDamping = 0.12f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                bodies.Add(boneId, body);
                AddCollider(boneId, bone);
            }
        }

        private void BuildJoints()
        {
            AddJoint(HumanBodyBones.Spine, HumanBodyBones.Hips, 20f, 25f);
            AddJoint(HumanBodyBones.Chest, HumanBodyBones.Spine, 24f, 28f);
            AddJoint(HumanBodyBones.Head, HumanBodyBones.Chest, 30f, 32f);
            AddJoint(HumanBodyBones.LeftUpperArm, HumanBodyBones.Chest, 48f, 65f);
            AddJoint(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, 8f, 10f);
            AddJoint(HumanBodyBones.RightUpperArm, HumanBodyBones.Chest, 48f, 65f);
            AddJoint(HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, 8f, 10f);
            AddJoint(HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips, 34f, 42f);
            AddJoint(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg, 7f, 10f);
            AddJoint(HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips, 34f, 42f);
            AddJoint(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg, 7f, 10f);
        }

        private void IgnorePlayerCollision()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            CharacterController playerController = player != null
                ? player.GetComponent<CharacterController>()
                : null;
            if (playerController == null ||
                playerController.transform.IsChildOf(transform) ||
                transform.IsChildOf(playerController.transform))
            {
                return;
            }

            IgnoreControllerCollision(
                playerController,
                GetComponentsInChildren<Collider>(true));
        }

        public static void IgnoreControllerCollision(
            CharacterController controller,
            IEnumerable<Collider> corpseColliders)
        {
            if (controller == null || corpseColliders == null)
            {
                return;
            }

            foreach (Collider corpseCollider in corpseColliders)
            {
                if (corpseCollider == null ||
                    ReferenceEquals(corpseCollider, controller))
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    controller,
                    corpseCollider,
                    true);
            }
        }

        private void AddJoint(
            HumanBodyBones bone,
            HumanBodyBones parent,
            float lowTwist,
            float swing)
        {
            if (!bodies.TryGetValue(bone, out Rigidbody body) ||
                !bodies.TryGetValue(parent, out Rigidbody parentBody))
            {
                return;
            }

            CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.enablePreprocessing = false;
            joint.lowTwistLimit = new SoftJointLimit { limit = -lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
        }

        private static void AddCollider(HumanBodyBones boneId, Transform bone)
        {
            if (boneId == HumanBodyBones.Head)
            {
                SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.13f;
                sphere.center = new Vector3(0f, 0.06f, 0f);
                return;
            }

            CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = IsTorso(boneId) ? 0.15f : 0.075f;
            capsule.height = IsTorso(boneId) ? 0.34f : 0.31f;
            capsule.center = Vector3.up * capsule.height * 0.24f;
        }

        private void DisableCharacterControllers()
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null ||
                    ReferenceEquals(behaviour, this) ||
                    behaviour is Health ||
                    behaviour is RaidLootContainer)
                {
                    continue;
                }

                behaviour.enabled = false;
            }
        }

        private static bool IsTorso(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.Hips ||
                bone == HumanBodyBones.Spine ||
                bone == HumanBodyBones.Chest;
        }

        private static float GetMass(HumanBodyBones bone)
        {
            if (bone == HumanBodyBones.Hips)
            {
                return 8f;
            }

            if (bone == HumanBodyBones.Spine || bone == HumanBodyBones.Chest)
            {
                return 5f;
            }

            if (bone == HumanBodyBones.Head)
            {
                return 2.2f;
            }

            return 1.7f;
        }
    }
}
