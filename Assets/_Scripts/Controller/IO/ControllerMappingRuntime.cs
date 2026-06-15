using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CosmicShore.Gameplay
{
    public static class ControllerMappingRuntime
    {
        private static readonly GamepadButtonSource[] ButtonScanOrder =
        {
            GamepadButtonSource.ButtonSouth,
            GamepadButtonSource.ButtonEast,
            GamepadButtonSource.ButtonWest,
            GamepadButtonSource.ButtonNorth,
            GamepadButtonSource.LeftShoulder,
            GamepadButtonSource.RightShoulder,
            GamepadButtonSource.LeftTrigger,
            GamepadButtonSource.RightTrigger,
            GamepadButtonSource.Select,
            GamepadButtonSource.Start,
            GamepadButtonSource.LeftStickPress,
            GamepadButtonSource.RightStickPress,
            GamepadButtonSource.DpadUp,
            GamepadButtonSource.DpadDown,
            GamepadButtonSource.DpadLeft,
            GamepadButtonSource.DpadRight,
        };

        public static Vector2 ReadVector(Gamepad gamepad, GamepadVectorSource source, ControllerMappingProfile mapping, bool leftOutput)
        {
            if (gamepad == null)
                return Vector2.zero;

            Vector2 value = source switch
            {
                GamepadVectorSource.LeftStick => gamepad.leftStick.ReadValue(),
                GamepadVectorSource.RightStick => gamepad.rightStick.ReadValue(),
                GamepadVectorSource.Dpad => gamepad.dpad.ReadValue(),
                _ => Vector2.zero,
            };

            var deadzone = mapping != null ? mapping.stickDeadzone : 0.08f;
            if (value.magnitude < deadzone)
                value = Vector2.zero;

            if (mapping != null)
            {
                if (leftOutput)
                {
                    if (mapping.invertLeftStickX) value.x *= -1f;
                    if (mapping.invertLeftStickY) value.y *= -1f;
                }
                else
                {
                    if (mapping.invertRightStickX) value.x *= -1f;
                    if (mapping.invertRightStickY) value.y *= -1f;
                }
            }

            return value;
        }

        public static float ReadButtonValue(Gamepad gamepad, GamepadButtonSource source, float pressPoint)
        {
            var control = GetButton(gamepad, source);
            if (control == null)
                return 0f;

            var value = control.ReadValue();
            return value >= pressPoint ? value : 0f;
        }

        public static bool WasPressedThisFrame(Gamepad gamepad, GamepadButtonSource source)
        {
            var control = GetButton(gamepad, source);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool WasReleasedThisFrame(Gamepad gamepad, GamepadButtonSource source)
        {
            var control = GetButton(gamepad, source);
            return control != null && control.wasReleasedThisFrame;
        }

        public static GamepadVectorSource DetectMovedVector(Gamepad gamepad, float threshold = 0.55f)
        {
            if (gamepad == null)
                return GamepadVectorSource.None;

            var candidates = new Dictionary<GamepadVectorSource, float>
            {
                { GamepadVectorSource.LeftStick, gamepad.leftStick.ReadValue().magnitude },
                { GamepadVectorSource.RightStick, gamepad.rightStick.ReadValue().magnitude },
                { GamepadVectorSource.Dpad, gamepad.dpad.ReadValue().magnitude },
            };

            var best = GamepadVectorSource.None;
            var bestMagnitude = threshold;
            foreach (var candidate in candidates)
            {
                if (candidate.Value > bestMagnitude)
                {
                    best = candidate.Key;
                    bestMagnitude = candidate.Value;
                }
            }

            return best;
        }

        public static GamepadButtonSource DetectPressedButton(Gamepad gamepad, float threshold = 0.55f)
        {
            if (gamepad == null)
                return GamepadButtonSource.None;

            foreach (var source in ButtonScanOrder)
            {
                var control = GetButton(gamepad, source);
                if (control == null)
                    continue;

                if (control.wasPressedThisFrame || control.ReadValue() >= threshold)
                    return source;
            }

            return GamepadButtonSource.None;
        }

        public static string GetVectorDisplayName(GamepadVectorSource source)
        {
            return source switch
            {
                GamepadVectorSource.LeftStick => "Left Stick",
                GamepadVectorSource.RightStick => "Right Stick",
                GamepadVectorSource.Dpad => "D-Pad",
                _ => "Unassigned",
            };
        }

        public static string GetButtonDisplayName(GamepadButtonSource source)
        {
            return source switch
            {
                GamepadButtonSource.ButtonSouth => "South Face",
                GamepadButtonSource.ButtonEast => "East Face",
                GamepadButtonSource.ButtonWest => "West Face",
                GamepadButtonSource.ButtonNorth => "North Face",
                GamepadButtonSource.LeftShoulder => "Left Shoulder",
                GamepadButtonSource.RightShoulder => "Right Shoulder",
                GamepadButtonSource.LeftTrigger => "Left Trigger",
                GamepadButtonSource.RightTrigger => "Right Trigger",
                GamepadButtonSource.Select => "Select / View",
                GamepadButtonSource.Start => "Start / Menu",
                GamepadButtonSource.LeftStickPress => "Left Stick Press",
                GamepadButtonSource.RightStickPress => "Right Stick Press",
                GamepadButtonSource.DpadUp => "D-Pad Up",
                GamepadButtonSource.DpadDown => "D-Pad Down",
                GamepadButtonSource.DpadLeft => "D-Pad Left",
                GamepadButtonSource.DpadRight => "D-Pad Right",
                _ => "Unassigned",
            };
        }

        private static ButtonControl GetButton(Gamepad gamepad, GamepadButtonSource source)
        {
            if (gamepad == null)
                return null;

            return source switch
            {
                GamepadButtonSource.ButtonSouth => gamepad.buttonSouth,
                GamepadButtonSource.ButtonEast => gamepad.buttonEast,
                GamepadButtonSource.ButtonWest => gamepad.buttonWest,
                GamepadButtonSource.ButtonNorth => gamepad.buttonNorth,
                GamepadButtonSource.LeftShoulder => gamepad.leftShoulder,
                GamepadButtonSource.RightShoulder => gamepad.rightShoulder,
                GamepadButtonSource.LeftTrigger => gamepad.leftTrigger,
                GamepadButtonSource.RightTrigger => gamepad.rightTrigger,
                GamepadButtonSource.Select => gamepad.selectButton,
                GamepadButtonSource.Start => gamepad.startButton,
                GamepadButtonSource.LeftStickPress => gamepad.leftStickButton,
                GamepadButtonSource.RightStickPress => gamepad.rightStickButton,
                GamepadButtonSource.DpadUp => gamepad.dpad.up,
                GamepadButtonSource.DpadDown => gamepad.dpad.down,
                GamepadButtonSource.DpadLeft => gamepad.dpad.left,
                GamepadButtonSource.DpadRight => gamepad.dpad.right,
                _ => null,
            };
        }
    }
}
