using UnityEngine;

namespace WorldBuilder.Gameplay.Input
{
    public readonly struct PlayerIntent
    {
        public PlayerIntent(Vector2 move, Vector2 look, bool sprintHeld, bool attackPressed)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Look = look;
            SprintHeld = sprintHeld;
            AttackPressed = attackPressed;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool SprintHeld { get; }
        public bool AttackPressed { get; }
    }

    public interface IPlayerIntentSource
    {
        PlayerIntent CurrentIntent { get; }
    }
}
