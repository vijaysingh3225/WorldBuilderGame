using UnityEngine;

namespace WorldBuilder.Gameplay.Input
{
    public readonly struct PlayerIntent
    {
        public PlayerIntent(
            Vector2 move,
            Vector2 look,
            bool sprintHeld,
            bool jumpPressed,
            bool jumpHeld,
            bool crouchHeld,
            bool attackPressed,
            bool blockHeld = false,
            bool attackHeld = false)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Look = look;
            SprintHeld = sprintHeld;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            CrouchHeld = crouchHeld;
            AttackPressed = attackPressed;
            BlockHeld = blockHeld;
            AttackHeld = attackHeld;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool SprintHeld { get; }
        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
        public bool CrouchHeld { get; }
        public bool AttackPressed { get; }
        public bool BlockHeld { get; }
        public bool AttackHeld { get; }
    }

    public interface IPlayerIntentSource
    {
        PlayerIntent CurrentIntent { get; }
    }
}
