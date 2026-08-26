using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace CosmicShore.UI
{
    /// <summary>
    /// Answers ONE question for the HUD: <b>which device is the player actually holding?</b> Xbox pad,
    /// PlayStation pad, keyboard/mouse, or touch. The ability lockup reads that answer and draws each
    /// card's control chip from the fleet's one glyph set.
    ///
    /// Detection is by LAST MEANINGFUL ACTUATION, not device presence: a connected-but-idle pad never
    /// steals the chips from the keyboard, and picking the pad back up switches within one input.
    /// Deliberately polls explicit controls (buttons/sticks/keys) instead of device.lastUpdateTime -
    /// DualShock sensor noise updates the timestamp every frame and would pin the pad set forever.
    ///
    /// <para><b>It draws nothing.</b> This component used to own the glyphs as well: three authored
    /// icon-set roots it toggled by device, a per-set list of hint visuals it lit and tinted, and a
    /// placement pass that moved each hint onto the ability icon its control drives. All of that is
    /// retired - the lockup draws the chip itself from <c>ControlGlyphSetSO</c>, so no vessel authors
    /// glyphs and there is nothing per-vessel left to place, light, or forget to wire. What remained
    /// was a second glyph display competing with the card's, on the three HUDs that still carried the
    /// roots, and it was kept alive by nothing but this component's own reference to it.</para>
    ///
    /// Lives on the vessel HUD (ensured by <c>VesselHUDController</c> when a vessel has none). Purely
    /// presentational - input strategy selection stays in <c>InputController</c>.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Input Device Icon Set Switcher")]
    public class InputDeviceIconSetSwitcher : MonoBehaviour
    {
        public enum IconSet { Xbox, PlayStation, KeyboardText, None }

        /// <summary>
        /// A physical control. The fleet's vocabulary for "which button is this?", shared by
        /// <see cref="InputHintBindingMap"/> (control ↔ input event) and <c>ControlGlyphSetSO</c>
        /// (control → the picture or label that depicts it).
        /// </summary>
        public enum HintBinding
        {
            None = 0,
            // Gamepad
            PadButtonSouth = 1, PadButtonNorth = 2, PadButtonEast = 3, PadButtonWest = 4,
            PadLeftShoulder = 5, PadRightShoulder = 6, PadLeftTrigger = 7, PadRightTrigger = 8,
            PadDpadUp = 9, PadDpadDown = 10, PadDpadLeft = 11, PadDpadRight = 12,
            // Keyboard / mouse (PC text set)
            KeyLeftShift = 20, KeyRightShift = 21, KeySpace = 22, KeyTab = 23,
            KeyQ = 24, KeyE = 25, KeyF = 26, KeyR = 27,
            MouseLeft = 40, MouseRight = 41,
        }

        [Header("Behaviour")]
        [Tooltip("Stick deflection that counts as 'using the pad'.")]
        [SerializeField, Range(0.05f, 0.9f)] private float stickActuationThreshold = 0.25f;

        public IconSet Current { get; private set; } = IconSet.None;

        /// <summary>Raised whenever the device set changes, so the ability lockup can re-draw its
        /// control chips for the device the player is actually holding.</summary>
        public event Action<IconSet> OnSetChanged;

        /// <summary>True while the player is on keyboard/mouse rather than a pad.</summary>
        public bool IsKeyboard => Current == IconSet.KeyboardText;

        private bool _applied;

        void OnEnable()
        {
            ApplySet(DetectInitialSet());
        }

        void Update()
        {
            var actuated = DetectActuatedSet();
            if (actuated.HasValue) ApplySet(actuated.Value);
        }

        // Starting state before any input: handhelds are touch (no chips), a connected pad shows its
        // own family, otherwise keyboard.
        IconSet DetectInitialSet()
        {
            if (SystemInfo.deviceType == DeviceType.Handheld && Gamepad.current == null)
                return IconSet.None;
            if (Gamepad.current != null)
                return SetForGamepad(Gamepad.current);
            return IconSet.KeyboardText;
        }

        IconSet? DetectActuatedSet()
        {
            var pad = Gamepad.current;
            if (pad != null && IsGamepadActuated(pad))
                return SetForGamepad(pad);

            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
                return IconSet.KeyboardText;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                return IconSet.KeyboardText;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return IconSet.None;

            return null;   // nothing meaningful this frame - keep the current set
        }

        bool IsGamepadActuated(Gamepad pad)
        {
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

        static IconSet SetForGamepad(Gamepad pad) =>
            pad is DualShockGamepad ? IconSet.PlayStation : IconSet.Xbox;

        void ApplySet(IconSet set)
        {
            if (_applied && set == Current) return;
            Current = set;
            _applied = true;
            OnSetChanged?.Invoke(set);
        }
    }
}
