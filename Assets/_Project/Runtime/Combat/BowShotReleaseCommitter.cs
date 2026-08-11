using UnityEngine;

namespace WorldBuilder.Gameplay.Combat
{
    /// <summary>
    /// Commits a queued bow release after camera systems have completed their
    /// LateUpdate. This makes the shot use the same camera pose that renders
    /// the centered crosshair for the frame.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class BowShotReleaseCommitter : MonoBehaviour
    {
        [SerializeField] private BowWeapon bowWeapon;

        public void Configure(BowWeapon weapon)
        {
            bowWeapon = weapon;
        }

        private void Awake()
        {
            bowWeapon ??= GetComponent<BowWeapon>();
        }

        private void LateUpdate()
        {
            bowWeapon?.CommitPendingReleaseAtRenderedCamera();
        }
    }
}
