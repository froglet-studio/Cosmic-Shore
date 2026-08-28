using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The fleet's ONE answer to <b>which device is the player actually using?</b> — read by
    /// <see cref="InputController"/> to pick the input strategy and by
    /// <c>InputDeviceIconSetSwitcher</c> to pick the ability-chip glyphs.
    ///
    /// <para><b>It exists because those two disagreed.</b> The switcher had always detected by
    /// last meaningful ACTUATION, so a connected-but-idle pad never stole the chips from the
    /// keyboard — while <c>SelectStrategy</c> keyed on device PRESENCE (<c>Gamepad.current !=
    /// null</c>) and handed the pad every frame regardless. A player with a controller plugged in
    /// therefore watched the glyphs correctly follow their keyboard and mouse while the ship
    /// ignored both, and the only fix was to unplug the pad. Two systems answering the same
    /// question two ways is not a bug either one contains; it is a bug in there being two answers.</para>
    ///
    /// <para><b>Buttons and keys only — never mouse movement or stick drift below threshold.</b> A
    /// bumped desk must not take the ship away from a pad player mid-flight, and a noisy stick must
    /// not take it back. For the same reason this deliberately polls explicit controls rather than
    /// <c>device.lastUpdateTime</c>: DualShock sensor noise updates that timestamp every frame and
    /// would pin the pad forever.</para>
    /// </summary>
    public static class InputDeviceActuation
    {
        /// <summary>Stick deflection that counts as "using the pad".</summary>
        public const float DefaultStickActuationThreshold = 0.25f;

        /// <summary>
        /// The family the player was last seen using, or <see cref="InputDeviceFamily.None"/> when
        /// nothing meaningful happened this frame — in which case the caller keeps whatever it had.
        /// Sticky by construction: this only ever answers on a real actuation, so nothing thrashes
        /// between devices frame to frame.
        /// </summary>
        public static InputDeviceFamily DetectActuatedThisFrame(
            float stickActuationThreshold = DefaultStickActuationThreshold)
        {
            var pad = Gamepad.current;
            if (pad != null && IsGamepadActuated(pad, stickActuationThreshold))
                return InputDeviceFamily.Gamepad;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return InputDeviceFamily.KeyboardMouse;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame
                               || mouse.rightButton.wasPressedThisFrame
                               || mouse.middleButton.wasPressedThisFrame))
                return InputDeviceFamily.KeyboardMouse;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return InputDeviceFamily.Touch;

            return InputDeviceFamily.None;
        }

        /// <summary>
        /// What to assume before the player has touched anything: a handheld is touch, a connected
        /// pad is the pad (so a console/couch session starts correct rather than starting on a
        /// keyboard nobody is holding), otherwise keyboard and mouse.
        /// </summary>
        public static InputDeviceFamily DetectInitial()
        {
            if (SystemInfo.deviceType == DeviceType.Handheld && Gamepad.current == null)
                return InputDeviceFamily.Touch;
            if (Gamepad.current != null)
                return InputDeviceFamily.Gamepad;
            return InputDeviceFamily.KeyboardMouse;
        }

        public static bool IsGamepadActuated(Gamepad pad,
            float stickActuationThreshold = DefaultStickActuationThreshold)
        {
            if (pad == null) return false;

            if (pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
                pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
                pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame)
                return true;

            float t = stickActuationThreshold;
            return pad.leftStick.ReadValue().sqrMagnitude > t * t
                || pad.rightStick.ReadValue().sqrMagnitude > t * t
                || pad.leftTrigger.ReadValue() > t
                || pad.rightTrigger.ReadValue() > t;
        }
    }

    /// <summary>
    /// A class of input device, as far as "who is flying the ship" is concerned. Distinct from
    /// <c>InputDeviceType</c>, which names which STRATEGY is live (a keyboard-mouse family player
    /// may be on <c>Keyboard</c>, <c>MouseKeyboard</c> or <c>DualMouse</c>).
    /// </summary>
    public enum InputDeviceFamily
    {
        None = 0,
        Gamepad = 1,
        KeyboardMouse = 2,
        Touch = 3,
    }
}
