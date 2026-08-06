using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldBuilder.Gameplay.Input
{
    public enum PlayerControl
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Sprint,
        Jump,
        Crouch,
        Interact,
        Inventory,
        RotateInventoryItem,
        SwapShoulder,
        WeaponSlotOne,
        WeaponSlotTwo,
        Pause
    }

    public static class PlayerControlBindings
    {
        private const string PreferencePrefix =
            "WorldBuilder.Control.";

        public static readonly PlayerControl[] AllControls =
        {
            PlayerControl.MoveForward,
            PlayerControl.MoveBackward,
            PlayerControl.MoveLeft,
            PlayerControl.MoveRight,
            PlayerControl.Sprint,
            PlayerControl.Jump,
            PlayerControl.Crouch,
            PlayerControl.Interact,
            PlayerControl.Inventory,
            PlayerControl.RotateInventoryItem,
            PlayerControl.SwapShoulder,
            PlayerControl.WeaponSlotOne,
            PlayerControl.WeaponSlotTwo,
            PlayerControl.Pause
        };

        private static readonly Key[] CurrentKeys =
            new Key[AllControls.Length];
        private static bool loaded;

        public static Key GetKey(PlayerControl control)
        {
            EnsureLoaded();
            return CurrentKeys[(int)control];
        }

        public static bool IsPressed(
            Keyboard keyboard,
            PlayerControl control)
        {
            Key key = GetKey(control);
            return keyboard != null &&
                key != Key.None &&
                keyboard[key].isPressed;
        }

        public static bool WasPressedThisFrame(
            Keyboard keyboard,
            PlayerControl control)
        {
            Key key = GetKey(control);
            return keyboard != null &&
                key != Key.None &&
                keyboard[key].wasPressedThisFrame;
        }

        public static void Rebind(
            PlayerControl control,
            Key newKey)
        {
            if (newKey == Key.None)
            {
                return;
            }

            EnsureLoaded();
            int swappedIndex = ApplyRebind(
                CurrentKeys,
                control,
                newKey);
            if (swappedIndex >= 0)
            {
                Save((PlayerControl)swappedIndex);
            }
            Save(control);
            PlayerPrefs.Save();
        }

        public static int ApplyRebind(
            Key[] keys,
            PlayerControl control,
            Key newKey)
        {
            if (keys == null ||
                keys.Length < AllControls.Length ||
                newKey == Key.None)
            {
                return -1;
            }

            int targetIndex = (int)control;
            Key previousKey = keys[targetIndex];
            int swappedIndex = -1;
            for (int index = 0; index < AllControls.Length; index++)
            {
                if (index == targetIndex || keys[index] != newKey)
                {
                    continue;
                }

                keys[index] = previousKey;
                swappedIndex = index;
                break;
            }
            keys[targetIndex] = newKey;
            return swappedIndex;
        }

        public static void ResetToDefaults()
        {
            for (int index = 0;
                 index < AllControls.Length;
                 index++)
            {
                PlayerControl control = AllControls[index];
                CurrentKeys[index] = GetDefaultKey(control);
                PlayerPrefs.DeleteKey(PreferenceKey(control));
            }
            loaded = true;
            PlayerPrefs.Save();
        }

        public static string ActionName(PlayerControl control)
        {
            return control switch
            {
                PlayerControl.MoveForward => "Move Forward",
                PlayerControl.MoveBackward => "Move Backward",
                PlayerControl.MoveLeft => "Move Left",
                PlayerControl.MoveRight => "Move Right",
                PlayerControl.Sprint => "Sprint",
                PlayerControl.Jump => "Jump",
                PlayerControl.Crouch => "Crouch",
                PlayerControl.Interact => "Interact",
                PlayerControl.Inventory => "Inventory",
                PlayerControl.RotateInventoryItem => "Rotate Held Item",
                PlayerControl.SwapShoulder => "Swap Shoulder",
                PlayerControl.WeaponSlotOne => "Weapon Slot 1",
                PlayerControl.WeaponSlotTwo => "Weapon Slot 2",
                PlayerControl.Pause => "Pause Menu",
                _ => control.ToString()
            };
        }

        public static string KeyName(Key key)
        {
            return key switch
            {
                Key.Digit1 => "1",
                Key.Digit2 => "2",
                Key.LeftShift => "Left Shift",
                Key.RightShift => "Right Shift",
                Key.LeftCtrl => "Left Ctrl",
                Key.RightCtrl => "Right Ctrl",
                Key.Space => "Space",
                Key.Escape => "Escape",
                _ => key.ToString()
            };
        }

        public static string DefaultKeyName(PlayerControl control)
        {
            return KeyName(GetDefaultKey(control));
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            for (int index = 0;
                 index < AllControls.Length;
                 index++)
            {
                PlayerControl control = AllControls[index];
                int stored = PlayerPrefs.GetInt(
                    PreferenceKey(control),
                    (int)GetDefaultKey(control));
                CurrentKeys[index] = Enum.IsDefined(
                    typeof(Key),
                    stored)
                        ? (Key)stored
                        : GetDefaultKey(control);
            }
            loaded = true;
        }

        private static void Save(PlayerControl control)
        {
            PlayerPrefs.SetInt(
                PreferenceKey(control),
                (int)CurrentKeys[(int)control]);
        }

        private static string PreferenceKey(PlayerControl control)
        {
            return PreferencePrefix + control;
        }

        public static Key GetDefaultKey(PlayerControl control)
        {
            return control switch
            {
                PlayerControl.MoveForward => Key.W,
                PlayerControl.MoveBackward => Key.S,
                PlayerControl.MoveLeft => Key.A,
                PlayerControl.MoveRight => Key.D,
                PlayerControl.Sprint => Key.LeftShift,
                PlayerControl.Jump => Key.Space,
                PlayerControl.Crouch => Key.C,
                PlayerControl.Interact => Key.F,
                PlayerControl.Inventory => Key.Tab,
                PlayerControl.RotateInventoryItem => Key.R,
                PlayerControl.SwapShoulder => Key.X,
                PlayerControl.WeaponSlotOne => Key.Digit1,
                PlayerControl.WeaponSlotTwo => Key.Digit2,
                PlayerControl.Pause => Key.Escape,
                _ => Key.None
            };
        }
    }
}
