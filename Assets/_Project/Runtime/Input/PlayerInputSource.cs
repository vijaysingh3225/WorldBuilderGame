using System;
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
        private bool userInterfaceCaptureActive;
        private bool gameplayCursorCaptureRequested;
        private int shoulderSide = 1;

        public PlayerIntent CurrentIntent { get; private set; }
        public bool DiagnosticOverrideActive => diagnosticOverrideActive;
        public bool UserInterfaceCaptureActive =>
            userInterfaceCaptureActive;
        public bool GameplayCursorCaptureRequested =>
            gameplayCursorCaptureRequested;
        public bool CameraOrbitHeld { get; private set; }
        public int ShoulderSide => shoulderSide;
        public event Action<int> WeaponSlotRequested;

        public void SetUserInterfaceCapture(bool captured)
        {
            userInterfaceCaptureActive = captured;
            if (captured)
            {
                CurrentIntent = default;
                CameraOrbitHeld = false;
            }
        }

        public void RequestGameplayCursorCapture()
        {
            gameplayCursorCaptureRequested = true;
            LockCursor();
        }

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

        public void SetShoulderSideDiagnostic(int side)
        {
            shoulderSide = side < 0 ? -1 : 1;
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
            userInterfaceCaptureActive = false;
            gameplayCursorCaptureRequested = false;
            CameraOrbitHeld = false;
            shoulderSide = 1;
            CurrentIntent = default;
        }

        private void Update()
        {
            if (userInterfaceCaptureActive)
            {
                CurrentIntent = default;
                CameraOrbitHeld = false;
                return;
            }

            if (gameplayCursorCaptureRequested)
            {
                gameplayCursorCaptureRequested = false;
                LockCursor();
                CurrentIntent = default;
                CameraOrbitHeld = false;
                return;
            }

            if (diagnosticOverrideActive)
            {
                CurrentIntent = diagnosticIntent;
                CameraOrbitHeld = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool primaryClickPressed =
                mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool secondaryClickPressed =
                mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool inspectionClickPressed =
                mouse != null && mouse.middleButton.wasPressedThisFrame;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if ((primaryClickPressed ||
                    secondaryClickPressed ||
                    inspectionClickPressed) &&
                Cursor.lockState != CursorLockMode.Locked)
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
            CameraOrbitHeld =
                cursorLocked &&
                mouse != null &&
                mouse.middleButton.isPressed;
            Vector2 look = cursorLocked && mouse != null ? mouse.delta.ReadValue() * lookScale : Vector2.zero;
            bool attackPressed = primaryClickPressed;
            bool blockHeld =
                cursorLocked &&
                mouse != null &&
                mouse.rightButton.isPressed;
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

            if (cursorLocked &&
                keyboard != null &&
                keyboard.xKey.wasPressedThisFrame)
            {
                shoulderSide = -shoulderSide;
            }

            ReadWeaponSlotRequest(keyboard, mouse);
            CurrentIntent = new PlayerIntent(
                move,
                look,
                sprintHeld,
                jumpPressed,
                jumpHeld,
                crouchToggled,
                attackPressed,
                blockHeld);
        }

        private void ReadWeaponSlotRequest(Keyboard keyboard, Mouse mouse)
        {
            int requestedSlot = -1;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame ||
                    keyboard.numpad1Key.wasPressedThisFrame)
                {
                    requestedSlot = 0;
                }
                else if (keyboard.digit2Key.wasPressedThisFrame ||
                    keyboard.numpad2Key.wasPressedThisFrame)
                {
                    requestedSlot = 1;
                }
            }

            if (requestedSlot < 0 && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    requestedSlot = scroll > 0f ? 0 : 1;
                }
            }

            if (requestedSlot >= 0)
            {
                WeaponSlotRequested?.Invoke(requestedSlot);
            }
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
