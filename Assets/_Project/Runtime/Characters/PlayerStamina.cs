using System;
using UnityEngine;

namespace WorldBuilder.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximum = 100f;
        [SerializeField] private float current = 100f;

        public event Action<float, float> Changed;

        public float Current => current;
        public float Maximum => maximum;
        public float Normalized => maximum > 0f
            ? Mathf.Clamp01(current / maximum)
            : 0f;

        public void Configure(float maximumStamina)
        {
            maximum = Mathf.Max(1f, maximumStamina);
            current = maximum;
            Changed?.Invoke(current, maximum);
        }

        public bool TrySpend(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount > current)
            {
                return false;
            }

            current -= amount;
            Changed?.Invoke(current, maximum);
            return true;
        }

        public void Restore(float amount)
        {
            float restored = Mathf.Clamp(
                current + Mathf.Max(0f, amount),
                0f,
                maximum);
            if (Mathf.Approximately(restored, current))
            {
                return;
            }

            current = restored;
            Changed?.Invoke(current, maximum);
        }
    }
}
