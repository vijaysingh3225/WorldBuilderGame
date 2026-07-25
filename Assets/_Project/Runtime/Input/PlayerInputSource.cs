using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldBuilder.Gameplay.Input
{
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerInputSource : MonoBehaviour, IPlayerIntentSource
    {
        [SerializeField, Min(0.001f)] private float lookScale = 0.08f;

        private bool diagnosticOverrideActive;
        private PlayerIntent diagnosticIntent;
        private bool crouchToggled;

        public PlayerIntent CurrentIntent { get; private set; }
        public bool DiagnosticOverrideActive => diagnosticOverrideActive;

        public void SetDiagnosticOverride(in PlayerIntent intent)
        {
            diagnosticIntent = intent;
            diagnosticOverrideActive = true;
            CurrentIntent = intent;
        }

        public void ClearDiagnosticOverride()
        {
            diagnosticOverrideActive = false;
            diagnosticIntent = default;
            CurrentIntent = default;
        }

        private void OnEnable()
        {
            LockCursor();
        }

        private void OnDisable()
        {
            diagnosticOverrideActive = false;
            diagnosticIntent = default;
            crouchToggled = false;
            CurrentIntent = default;
        }

        private void Update()
        {
            if (diagnosticOverrideActive)
            {
                CurrentIntent = diagnosticIntent;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool primaryClickPressed =
                mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (primaryClickPressed && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                move.x = ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
                move.y = ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            }

            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            Vector2 look = cursorLocked && mouse != null ? mouse.delta.ReadValue() * lookScale : Vector2.zero;
            bool attackPressed = primaryClickPressed;
            bool sprintHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            bool jumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            bool jumpHeld = keyboard != null && keyboard.spaceKey.isPressed;
            bool crouchPressed = keyboard != null &&
                (keyboard.leftCtrlKey.wasPressedThisFrame ||
                 keyboard.rightCtrlKey.wasPressedThisFrame ||
                 keyboard.cKey.wasPressedThisFrame);
            if (crouchPressed)
            {
                crouchToggled = !crouchToggled;
            }

            CurrentIntent = new PlayerIntent(
                move,
                look,
                sprintHeld,
                jumpPressed,
                jumpHeld,
                crouchToggled,
                attackPressed);
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
