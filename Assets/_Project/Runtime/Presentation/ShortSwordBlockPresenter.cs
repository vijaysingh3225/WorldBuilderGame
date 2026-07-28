using UnityEngine;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ShortSwordBlockPresenter : MonoBehaviour
    {
        public const string BlockLayerName = "Short Sword Block";
        public const string BlockStateName = "Two Handed Defensive Guard";

        private static readonly int BlockStateHash =
            Animator.StringToHash(BlockStateName);

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private ShortSwordAttackPresenter attackPresenter;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Transform swordRoot;
        [SerializeField, Range(0f, 1f)] private float defensivePoseTime = 0.55f;
        [SerializeField, Min(0.01f)] private float blendInDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float blendOutDuration = 0.16f;
        [SerializeField] private Vector3 leftHandHiltOffset =
            new Vector3(0f, 0.025f, 0f);
        [SerializeField] private Vector3 authoredGuardSwordLocalPosition;
        [SerializeField] private Quaternion authoredGuardSwordLocalRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion authoredGuardLeftHandLocalRotation =
            Quaternion.identity;
        private int blockLayerIndex = -1;
        private float blockWeight;
        private bool poseRequestedLastFrame;
        private Transform leftMiddleKnuckle;
        private Transform leftHand;
        private Transform leftIndexKnuckle;
        private Transform leftLittleKnuckle;
        private Transform head;
        private Quaternion swordCarryLocalRotation;
        private Vector3 swordCarryLocalPosition;
        private bool hasSwordCarryTransform;

        public float BlockWeight => blockWeight;
        public bool IsBlocking => blockWeight > 0.01f;
        public float LeftHandHiltContactGap =>
            leftIndexKnuckle != null &&
            leftLittleKnuckle != null &&
            swordRoot != null
                ? Vector3.Distance(
                    GetPalmCenter(
                        animator.GetBoneTransform(HumanBodyBones.LeftHand),
                        leftIndexKnuckle,
                        leftLittleKnuckle),
                    swordRoot.TransformPoint(leftHandHiltOffset))
                : 99f;
        public float LeftGripAxisAlignmentAngle
        {
            get
            {
                if (leftIndexKnuckle == null ||
                    leftLittleKnuckle == null ||
                    swordRoot == null)
                {
                    return 180f;
                }

                Vector3 gripAxis =
                    (leftIndexKnuckle.position - leftLittleKnuckle.position).normalized;
                float angle = Vector3.Angle(gripAxis, swordRoot.up);
                return Mathf.Min(angle, 180f - angle);
            }
        }

        public float BladeHeadClearance
        {
            get
            {
                if (head == null || swordRoot == null)
                {
                    return 99f;
                }

                Vector3 bladeBase = swordRoot.TransformPoint(new Vector3(0f, 0.215f, 0f));
                Vector3 bladeTip = swordRoot.TransformPoint(new Vector3(0f, 0.995f, 0f));
                return DistanceToSegment(head.position, bladeBase, bladeTip);
            }
        }

        public float BladeHeadSilhouetteClearance
        {
            get
            {
                if (head == null || swordRoot == null || characterRoot == null)
                {
                    return 99f;
                }

                Vector3 headLocal =
                    characterRoot.InverseTransformPoint(head.position);
                Vector3 bladeBaseLocal = characterRoot.InverseTransformPoint(
                    swordRoot.TransformPoint(new Vector3(0f, 0.215f, 0f)));
                Vector3 bladeTipLocal = characterRoot.InverseTransformPoint(
                    swordRoot.TransformPoint(new Vector3(0f, 0.995f, 0f)));
                return DistanceToSegment(
                    new Vector3(headLocal.x, headLocal.y, 0f),
                    new Vector3(bladeBaseLocal.x, bladeBaseLocal.y, 0f),
                    new Vector3(bladeTipLocal.x, bladeTipLocal.y, 0f));
            }
        }

        public void Configure(
            Animator targetAnimator,
            PlayerInputSource intentSource,
            ShortSwordAttackPresenter swordAttackPresenter,
            Transform root,
            Transform equippedSwordRoot)
        {
            animator = targetAnimator;
            input = intentSource;
            attackPresenter = swordAttackPresenter;
            characterRoot = root;
            swordRoot = equippedSwordRoot;
            CaptureSwordCarryTransform();
            ResolveBones();
            ResolveLayer();
        }

        public void ConfigureAuthoredGuardSwordTransform(
            Vector3 localPosition,
            Quaternion localRotation,
            Quaternion leftHandLocalRotation)
        {
            authoredGuardSwordLocalPosition = localPosition;
            authoredGuardSwordLocalRotation = localRotation.normalized;
            authoredGuardLeftHandLocalRotation =
                leftHandLocalRotation.normalized;
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            input ??= GetComponentInParent<PlayerInputSource>();
            attackPresenter ??= GetComponent<ShortSwordAttackPresenter>();
            characterRoot ??= input != null ? input.transform : transform.root;
            swordRoot ??= transform.Find("Equipped Short Sword");
            CaptureSwordCarryTransform();
            ResolveBones();
            ResolveLayer();
        }

        private void OnDisable()
        {
            blockWeight = 0f;
            poseRequestedLastFrame = false;
            RestoreSwordCarryTransform();
            if (animator != null && blockLayerIndex >= 0)
            {
                animator.SetLayerWeight(blockLayerIndex, 0f);
            }
        }

        private void Update()
        {
            if (animator == null || input == null)
            {
                return;
            }

            if (blockLayerIndex < 0)
            {
                ResolveLayer();
            }

            if (blockLayerIndex < 0)
            {
                return;
            }

            bool poseRequested =
                input.CurrentIntent.BlockHeld &&
                (attackPresenter == null || !attackPresenter.IsAttacking);
            if (poseRequested && !poseRequestedLastFrame)
            {
                animator.Play(
                    BlockStateHash,
                    blockLayerIndex,
                    defensivePoseTime);
            }

            poseRequestedLastFrame = poseRequested;
            float targetWeight = poseRequested ? 1f : 0f;
            float duration = poseRequested ? blendInDuration : blendOutDuration;
            blockWeight = Mathf.MoveTowards(
                blockWeight,
                targetWeight,
                Time.deltaTime / duration);
            animator.SetLayerWeight(blockLayerIndex, blockWeight);
        }

        private void LateUpdate()
        {
            if (swordRoot == null)
            {
                return;
            }

            CaptureSwordCarryTransform();
            bool attackActive = attackPresenter != null && attackPresenter.IsAttacking;
            float weight = attackActive ? 0f : blockWeight;
            if (leftHand != null && weight > 0.001f)
            {
                leftHand.localRotation = Quaternion.Slerp(
                    leftHand.localRotation,
                    authoredGuardLeftHandLocalRotation,
                    weight);
            }

            swordRoot.localPosition = Vector3.Lerp(
                swordCarryLocalPosition,
                authoredGuardSwordLocalPosition,
                weight);
            swordRoot.localRotation = Quaternion.Slerp(
                swordCarryLocalRotation,
                authoredGuardSwordLocalRotation,
                weight);
        }

        private void CaptureSwordCarryTransform()
        {
            if (swordRoot == null || hasSwordCarryTransform)
            {
                return;
            }

            swordCarryLocalPosition = swordRoot.localPosition;
            swordCarryLocalRotation = swordRoot.localRotation;
            hasSwordCarryTransform = true;
        }

        private void RestoreSwordCarryTransform()
        {
            if (swordRoot == null || !hasSwordCarryTransform)
            {
                return;
            }

            swordRoot.localPosition = swordCarryLocalPosition;
            swordRoot.localRotation = swordCarryLocalRotation;
        }

        private void ResolveBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            leftMiddleKnuckle =
                animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            leftIndexKnuckle =
                animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            leftLittleKnuckle =
                animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private static Vector3 GetPalmCenter(
            Transform hand,
            Transform index,
            Transform little)
        {
            Vector3 knuckleCenter = (index.position + little.position) * 0.5f;
            return Vector3.Lerp(hand.position, knuckleCenter, 0.68f);
        }

        private static float DistanceToSegment(
            Vector3 point,
            Vector3 segmentStart,
            Vector3 segmentEnd)
        {
            Vector3 segment = segmentEnd - segmentStart;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return Vector3.Distance(point, segmentStart);
            }

            float t = Mathf.Clamp01(
                Vector3.Dot(point - segmentStart, segment) / lengthSquared);
            return Vector3.Distance(point, segmentStart + segment * t);
        }

        private void ResolveLayer()
        {
            if (animator == null)
            {
                return;
            }

            blockLayerIndex = animator.GetLayerIndex(BlockLayerName);
            if (blockLayerIndex >= 0)
            {
                blockWeight = 0f;
                animator.SetLayerWeight(blockLayerIndex, 0f);
            }
        }
    }
}
