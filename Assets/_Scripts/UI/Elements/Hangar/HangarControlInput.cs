using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// Reads the PHYSICAL control behind a <see cref="HintBinding"/> straight off the device, so the
    /// Hangar can answer "the player just pressed RT" without a vessel (and its input strategy) being
    /// alive. Also tracks which device the player last used, so labels follow the hand on the controls.
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

        /// <summary>True on the frame the control behind <paramref name="binding"/> goes down.</summary>
        public static bool WasPressed(HintBinding binding)
        {
            var pad = Gamepad.current;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            switch (binding)
            {
                case HintBinding.PadButtonSouth: return pad != null && pad.buttonSouth.wasPressedThisFrame;
                case HintBinding.PadButtonNorth: return pad != null && pad.buttonNorth.wasPressedThisFrame;
                case HintBinding.PadButtonEast: return pad != null && pad.buttonEast.wasPressedThisFrame;
                case HintBinding.PadButtonWest: return pad != null && pad.buttonWest.wasPressedThisFrame;
                case HintBinding.PadLeftShoulder: return pad != null && pad.leftShoulder.wasPressedThisFrame;
                case HintBinding.PadRightShoulder: return pad != null && pad.rightShoulder.wasPressedThisFrame;
                case HintBinding.PadLeftTrigger: return pad != null && pad.leftTrigger.wasPressedThisFrame;
                case HintBinding.PadRightTrigger: return pad != null && pad.rightTrigger.wasPressedThisFrame;
                case HintBinding.KeyLeftShift: return kb != null && kb.leftShiftKey.wasPressedThisFrame;
                case HintBinding.KeyRightShift: return kb != null && kb.rightShiftKey.wasPressedThisFrame;
                case HintBinding.KeySpace: return kb != null && kb.spaceKey.wasPressedThisFrame;
                case HintBinding.KeyTab: return kb != null && kb.tabKey.wasPressedThisFrame;
                case HintBinding.KeyQ: return kb != null && kb.qKey.wasPressedThisFrame;
                case HintBinding.KeyE: return kb != null && kb.eKey.wasPressedThisFrame;
                case HintBinding.KeyF: return kb != null && kb.fKey.wasPressedThisFrame;
                case HintBinding.KeyR: return kb != null && kb.rKey.wasPressedThisFrame;
                // A mouse button over the UI is a click on the UI, not an ability.
                case HintBinding.MouseLeft: return mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUi();
                case HintBinding.MouseRight: return mouse != null && mouse.rightButton.wasPressedThisFrame && !OverUi();
                default: return false;
            }
        }

        static bool OverUi() => EventSystem.current && EventSystem.current.IsPointerOverGameObject();
    }
}
