using System;
using UnityEngine;

namespace WorldBuilder.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class StableId : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string value;

        public string Value => value;

        public void EnsureAssigned()
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                value = Guid.NewGuid().ToString("N");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureAssigned();
        }
#endif
    }
}
