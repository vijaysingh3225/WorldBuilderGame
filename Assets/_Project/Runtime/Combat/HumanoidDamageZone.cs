using UnityEngine;

namespace WorldBuilder.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class HumanoidDamageZone :
        MonoBehaviour,
        IDamageable
    {
        [SerializeField]
        private HumanoidHitRegion region =
            HumanoidHitRegion.Torso;
        [SerializeField]
        private Transform primaryAttachment;
        [SerializeField]
        private Transform secondaryAttachment;
        [SerializeField]
        private bool chooseClosestAttachment;
        private EnemyDamageProfile profile;

        public HumanoidHitRegion Region => region;
        public bool IsAlive
        {
            get
            {
                ResolveProfile();
                return profile != null &&
                    profile.IsAlive;
            }
        }

        public void Configure(
            HumanoidHitRegion hitRegion,
            Transform primary = null,
            Transform secondary = null,
            bool chooseClosest = false)
        {
            region = hitRegion;
            primaryAttachment = primary;
            secondaryAttachment = secondary;
            chooseClosestAttachment =
                chooseClosest;
            ResolveProfile();
        }

        public Transform ResolveAttachmentTransform(
            Vector3 hitPoint)
        {
            if (primaryAttachment == null)
            {
                ResolveProfile();
                return profile != null
                    ? profile.ResolveAttachmentTransform(
                        hitPoint)
                    : transform;
            }
            if (secondaryAttachment == null ||
                !chooseClosestAttachment)
            {
                return primaryAttachment;
            }

            return Vector3.SqrMagnitude(
                       hitPoint -
                       primaryAttachment.position) <=
                   Vector3.SqrMagnitude(
                       hitPoint -
                       secondaryAttachment.position)
                ? primaryAttachment
                : secondaryAttachment;
        }

        public void ReceiveDamage(
            in DamageRequest request)
        {
            ResolveProfile();
            profile?.ReceiveDamage(
                region,
                request);
        }

        private void ResolveProfile()
        {
            profile ??=
                GetComponentInParent<
                    EnemyDamageProfile>(true);
        }
    }
}
