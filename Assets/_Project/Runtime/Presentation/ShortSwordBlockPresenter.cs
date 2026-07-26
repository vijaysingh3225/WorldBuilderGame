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
        [SerializeField, Min(0.01f)] private float blendOutDuration = 0.14f;
        [SerializeField] private Vector3 leftHandHiltOffset =
            new Vector3(0f, 0.15f, 0f);
        [SerializeField, Range(0f, 0.3f)] private float guardRetraction = 0.22f;

        private int blockLayerIndex = -1;
        private float blockWeight;
        private bool poseRequestedLastFrame;
        private Transform leftHand;
        private Transform rightHand;
        private Transform leftMiddleKnuckle;
        private Transform leftShoulder;
        private Vector3 leftWristGripCorrection;
        private Transform leftIndexKnuckle;
        private Transform leftLittleKnuckle;
        private Transform head;

        public float BlockWeight => blockWeight;
        public bool IsBlocking => blockWeight > 0.01f;
        public float LeftHandHiltContactGap =>
            leftMiddleKnuckle != null && swordRoot != null
                ? Vector3.Distance(
                    leftMiddleKnuckle.position,
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
            ResolveBones();
            ResolveLayer();
        }

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            input ??= GetComponentInParent<PlayerInputSource>();
            attackPresenter ??= GetComponent<ShortSwordAttackPresenter>();
            characterRoot ??= input != null ? input.transform : transform.root;
            swordRoot ??= transform.Find("Equipped Short Sword");
            ResolveBones();
            ResolveLayer();
        }

        private void OnDisable()
        {
            blockWeight = 0f;
            poseRequestedLastFrame = false;
            leftWristGripCorrection = Vector3.zero;
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
                leftWristGripCorrection = Vector3.zero;
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
            if (!poseRequestedLastFrame ||
                blockWeight < 0.5f ||
                leftMiddleKnuckle == null ||
                swordRoot == null)
            {
                if (!poseRequestedLastFrame)
                {
                    leftWristGripCorrection = Vector3.zero;
                }

                return;
            }

            Vector3 hiltContact = swordRoot.TransformPoint(leftHandHiltOffset);
            Vector3 contactError = hiltContact - leftMiddleKnuckle.position;
            leftWristGripCorrection += Vector3.ClampMagnitude(contactError, 0.015f);
            leftWristGripCorrection = Vector3.ClampMagnitude(
                leftWristGripCorrection,
                0.2f);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != blockLayerIndex ||
                animator == null ||
                swordRoot == null ||
                characterRoot == null ||
                leftHand == null ||
                rightHand == null ||
                leftMiddleKnuckle == null ||
                leftShoulder == null)
            {
                return;
            }

            float ikWeight = poseRequestedLastFrame ? blockWeight : 0f;
            if (ikWeight <= 0.001f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                return;
            }

            Vector3 guardShift = -characterRoot.forward * guardRetraction;
            Vector3 hiltContact =
                swordRoot.TransformPoint(leftHandHiltOffset) + guardShift;
            Vector3 wristTarget = hiltContact + leftWristGripCorrection;
            Vector3 elbowHint =
                leftShoulder.position -
                characterRoot.right * 0.28f +
                characterRoot.forward * 0.16f -
                characterRoot.up * 0.12f;

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, ikWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, ikWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, ikWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, wristTarget);
            animator.SetIKPosition(
                AvatarIKGoal.RightHand,
                rightHand.position + guardShift);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, elbowHint);
        }

        private void ResolveBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftMiddleKnuckle =
                animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            leftShoulder =
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftIndexKnuckle =
                animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            leftLittleKnuckle =
                animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
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
