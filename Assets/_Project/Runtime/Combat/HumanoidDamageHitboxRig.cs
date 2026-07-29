using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilder.Gameplay.Combat
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class HumanoidDamageHitboxRig : MonoBehaviour
    {
        private sealed class TrackedCapsule
        {
            public Transform Start;
            public Transform End;
            public Transform Hitbox;
            public CapsuleCollider Collider;
            public float Radius;
        }

        private sealed class TrackedSphere
        {
            public Transform Bone;
            public Transform Hitbox;
            public SphereCollider Collider;
            public Vector3 BoneLocalOffset;
            public float Radius;
        }

        private readonly List<TrackedCapsule> capsules = new();
        private readonly List<TrackedSphere> spheres = new();
        private Transform hitboxRoot;
        private Animator animator;

        public int HitboxCount => capsules.Count + spheres.Count;

        public void SetHitboxesEnabled(bool value)
        {
            if (hitboxRoot != null)
            {
                hitboxRoot.gameObject.SetActive(value);
            }
        }

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
            Build();
        }

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>(true);
            Build();
        }

        private void LateUpdate()
        {
            UpdateHitboxes();
        }

        private void Build()
        {
            if (animator == null || hitboxRoot != null)
            {
                return;
            }

            GameObject rootObject =
                new GameObject("Precise Humanoid Damage Hitboxes");
            rootObject.layer = gameObject.layer;
            hitboxRoot = rootObject.transform;
            hitboxRoot.SetParent(transform, false);

            Transform hips = Bone(HumanBodyBones.Hips);
            Transform spine = Bone(HumanBodyBones.Spine);
            Transform upperChest =
                Bone(HumanBodyBones.UpperChest) ??
                Bone(HumanBodyBones.Chest);
            Transform neck = Bone(HumanBodyBones.Neck);
            Transform head = Bone(HumanBodyBones.Head);
            Transform leftUpperArm =
                Bone(HumanBodyBones.LeftUpperArm);
            Transform leftLowerArm =
                Bone(HumanBodyBones.LeftLowerArm);
            Transform leftHand = Bone(HumanBodyBones.LeftHand);
            Transform rightUpperArm =
                Bone(HumanBodyBones.RightUpperArm);
            Transform rightLowerArm =
                Bone(HumanBodyBones.RightLowerArm);
            Transform rightHand = Bone(HumanBodyBones.RightHand);
            Transform leftUpperLeg =
                Bone(HumanBodyBones.LeftUpperLeg);
            Transform leftLowerLeg =
                Bone(HumanBodyBones.LeftLowerLeg);
            Transform leftFoot = Bone(HumanBodyBones.LeftFoot);
            Transform rightUpperLeg =
                Bone(HumanBodyBones.RightUpperLeg);
            Transform rightLowerLeg =
                Bone(HumanBodyBones.RightLowerLeg);
            Transform rightFoot = Bone(HumanBodyBones.RightFoot);

            AddCapsule("Abdomen", hips, spine, 0.20f);
            AddCapsule("Chest", spine, upperChest, 0.235f);
            AddCapsule(
                "Shoulders",
                leftUpperArm,
                rightUpperArm,
                0.13f);
            AddCapsule("Neck And Head", neck, head, 0.135f);
            AddSphere(
                "Skull",
                head,
                ResolveHeadCenterOffset(head, neck),
                0.145f);
            AddCapsule(
                "Pelvis",
                leftUpperLeg,
                rightUpperLeg,
                0.135f);

            AddCapsule(
                "Left Upper Arm",
                leftUpperArm,
                leftLowerArm,
                0.085f);
            AddCapsule(
                "Left Forearm",
                leftLowerArm,
                leftHand,
                0.070f);
            AddSphere(
                "Left Hand",
                leftHand,
                Vector3.zero,
                0.075f);
            AddCapsule(
                "Right Upper Arm",
                rightUpperArm,
                rightLowerArm,
                0.085f);
            AddCapsule(
                "Right Forearm",
                rightLowerArm,
                rightHand,
                0.070f);
            AddSphere(
                "Right Hand",
                rightHand,
                Vector3.zero,
                0.075f);

            AddCapsule(
                "Left Thigh",
                leftUpperLeg,
                leftLowerLeg,
                0.115f);
            AddCapsule(
                "Left Calf",
                leftLowerLeg,
                leftFoot,
                0.085f);
            AddSphere(
                "Left Foot",
                leftFoot,
                Vector3.zero,
                0.105f);
            AddCapsule(
                "Right Thigh",
                rightUpperLeg,
                rightLowerLeg,
                0.115f);
            AddCapsule(
                "Right Calf",
                rightLowerLeg,
                rightFoot,
                0.085f);
            AddSphere(
                "Right Foot",
                rightFoot,
                Vector3.zero,
                0.105f);

            IgnoreOwnerControllerCollisions();
            UpdateHitboxes();
        }

        private void IgnoreOwnerControllerCollisions()
        {
            CharacterController ownerController =
                GetComponent<CharacterController>();
            if (ownerController == null ||
                hitboxRoot == null)
            {
                return;
            }

            Collider[] hitboxColliders =
                hitboxRoot.GetComponentsInChildren<Collider>(true);
            for (int index = 0;
                 index < hitboxColliders.Length;
                 index++)
            {
                Physics.IgnoreCollision(
                    ownerController,
                    hitboxColliders[index],
                    true);
            }
        }

        private Transform Bone(HumanBodyBones bone)
        {
            return animator != null
                ? animator.GetBoneTransform(bone)
                : null;
        }

        private static Vector3 ResolveHeadCenterOffset(
            Transform head,
            Transform neck)
        {
            if (head == null || neck == null)
            {
                return Vector3.zero;
            }

            Vector3 neckToHead = head.position - neck.position;
            if (neckToHead.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            return head.InverseTransformVector(
                neckToHead.normalized * 0.10f);
        }

        private void AddCapsule(
            string name,
            Transform start,
            Transform end,
            float radius)
        {
            if (start == null || end == null)
            {
                return;
            }

            GameObject hitboxObject =
                new GameObject($"Damage Hitbox - {name}");
            hitboxObject.layer = gameObject.layer;
            hitboxObject.transform.SetParent(hitboxRoot, false);
            CapsuleCollider collider =
                hitboxObject.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.center = Vector3.zero;
            capsules.Add(new TrackedCapsule
            {
                Start = start,
                End = end,
                Hitbox = hitboxObject.transform,
                Collider = collider,
                Radius = radius
            });
        }

        private void AddSphere(
            string name,
            Transform bone,
            Vector3 boneLocalOffset,
            float radius)
        {
            if (bone == null)
            {
                return;
            }

            GameObject hitboxObject =
                new GameObject($"Damage Hitbox - {name}");
            hitboxObject.layer = gameObject.layer;
            hitboxObject.transform.SetParent(hitboxRoot, false);
            SphereCollider collider =
                hitboxObject.AddComponent<SphereCollider>();
            collider.center = Vector3.zero;
            spheres.Add(new TrackedSphere
            {
                Bone = bone,
                Hitbox = hitboxObject.transform,
                Collider = collider,
                BoneLocalOffset = boneLocalOffset,
                Radius = radius
            });
        }

        private void UpdateHitboxes()
        {
            for (int index = 0; index < capsules.Count; index++)
            {
                TrackedCapsule tracked = capsules[index];
                if (tracked.Start == null ||
                    tracked.End == null ||
                    tracked.Hitbox == null)
                {
                    continue;
                }

                Vector3 start = tracked.Start.position;
                Vector3 end = tracked.End.position;
                Vector3 direction = end - start;
                float length = direction.magnitude;
                tracked.Hitbox.SetPositionAndRotation(
                    Vector3.Lerp(start, end, 0.5f),
                    length > 0.0001f
                        ? Quaternion.FromToRotation(
                            Vector3.up,
                            direction / length)
                        : Quaternion.identity);
                tracked.Hitbox.localScale = Vector3.one;
                tracked.Collider.radius = tracked.Radius;
                tracked.Collider.height =
                    Mathf.Max(
                        tracked.Radius * 2f,
                        length + tracked.Radius * 2f);
            }

            for (int index = 0; index < spheres.Count; index++)
            {
                TrackedSphere tracked = spheres[index];
                if (tracked.Bone == null ||
                    tracked.Hitbox == null)
                {
                    continue;
                }

                tracked.Hitbox.SetPositionAndRotation(
                    tracked.Bone.TransformPoint(
                        tracked.BoneLocalOffset),
                    tracked.Bone.rotation);
                tracked.Hitbox.localScale = Vector3.one;
                tracked.Collider.radius = tracked.Radius;
            }
        }
    }
}
