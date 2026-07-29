using UnityEngine;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatGuard : MonoBehaviour
    {
        [SerializeField] private ShortSwordBlockPresenter blockPresenter;
        [SerializeField, Range(0f, 1f)] private float swordDamageReceived = 0.25f;
        [SerializeField, Range(0f, 1f)] private float arrowDamageReceived = 0.60f;

        public bool IsGuarding =>
            blockPresenter != null &&
            blockPresenter.WeaponEquipped &&
            blockPresenter.IsBlocking;

        public void Configure(ShortSwordBlockPresenter presenter)
        {
            blockPresenter = presenter;
        }

        public float GetDamageMultiplier(string sourceId)
        {
            if (!IsGuarding)
            {
                return 1f;
            }

            if (sourceId == "prototype-sword")
            {
                return swordDamageReceived;
            }

            if (sourceId == "prototype-bow")
            {
                return arrowDamageReceived;
            }

            return 1f;
        }
    }
}
