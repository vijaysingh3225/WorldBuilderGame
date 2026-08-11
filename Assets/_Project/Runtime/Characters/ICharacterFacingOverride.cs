using UnityEngine;

namespace WorldBuilder.Gameplay.Characters
{
    public interface ICharacterFacingOverride
    {
        bool TryGetFacingDirection(out Vector3 worldDirection);
    }
}
