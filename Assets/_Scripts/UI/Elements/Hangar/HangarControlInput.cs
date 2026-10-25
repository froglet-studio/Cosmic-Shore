using UnityEngine.InputSystem;

namespace CosmicShore.UI
{
    /// <summary>
    /// Tracks which device the player last used - a pad or the keyboard / mouse - so the Hangar's keycaps name the
    /// controls of the hand that is on them. It reads no control as a press: the Hangar's ability rows are information
    /// (Docs/MENU_REDESIGN_B.md section 66), and on a menu screen every pad control is the menu's (section 63).
    /// </summary>
    public static class HangarControlInput
    {
        /// <summary>True once the keyboard / mouse was the last thing pressed; false for a pad.</summary>
        public static bool LastWasKeyboard { get; private set; }

        /// <summary>Call once per frame from whoever shows device labels; returns true when the device flipped.</summary>
        public static bool TrackDevice()
        {
            bool before = LastWasKeyboard;
            var pad = Gamepad.current;
            var kb = Keyboard.current;
            if (pad != null && AnyPadPress(pad)) LastWasKeyboard = false;
            else if (kb != null && kb.anyKey.wasPressedThisFrame) LastWasKeyboard = true;
            else if (pad == null && kb != null) LastWasKeyboard = true;
            return before != LastWasKeyboard;
        }

        static bool AnyPadPress(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
            || pad.buttonNorth.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame || pad.leftStick.ReadValue().sqrMagnitude > 0.25f;
    }
}
