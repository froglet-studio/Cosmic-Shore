using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace CosmicShore.UI
{
    /// <summary>
    /// Auto-detects the input device the player is actually using and shows the matching binding-hint
    /// set at the bottom of the ability HUD: the Xbox icon set, the PlayStation icon set, or the
    /// keyboard text set. Touch hides the controller hints entirely - the on-screen ability buttons
    /// ARE the touch controls, so pad glyphs there are misinformation.
    ///
    /// Detection is by LAST MEANINGFUL ACTUATION, not device presence: a connected-but-idle pad never
    /// steals the hints from the keyboard, and picking the pad back up switches within one input.
    /// Deliberately polls explicit controls (buttons/sticks/keys) instead of device.lastUpdateTime -
    /// DualShock sensor noise updates the timestamp every frame and would pin the pad set forever.
    ///
    /// Lives on the vessel HUD prefab root, next to the icon roots it drives. Purely presentational -
    /// input strategy selection stays in <c>InputController</c>.
    /// </summary>
    public class InputDeviceIconSetSwitcher : MonoBehaviour
    {
        public enum IconSet { Xbox, PlayStation, KeyboardText, None }

        [Header("Icon set roots")]
        [Tooltip("Root of the Xbox controller glyphs (XBOX_Icon_Root).")]
        [SerializeField] private GameObject xboxIconRoot;
        [Tooltip("Root of the PlayStation controller glyphs (PS_Icon_Root).")]
        [SerializeField] private GameObject psIconRoot;
        [Tooltip("Root of the keyboard text hints. Optional until the text set is authored - see " +
                 "'Show Xbox Set When No Keyboard Root' for the interim behaviour.")]
        [SerializeField] private GameObject keyboardTextRoot;

        [Header("Behaviour")]
        [Tooltip("While no keyboard text root is authored, keep showing the Xbox set for " +
                 "keyboard/mouse play instead of hiding every hint (preserves the pre-switcher look). " +
                 "Wire keyboardTextRoot and this flag stops mattering.")]
        [SerializeField] private bool showXboxSetWhenNoKeyboardRoot = true;
        [Tooltip("Stick deflection that counts as 'using the pad'.")]
        [SerializeField, Range(0.05f, 0.9f)] private float stickActuationThreshold = 0.25f;

        public IconSet Current { get; private set; } = IconSet.None;

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

        // Starting state before any input: handhelds are touch (no hints), a connected pad shows its
        // own family, otherwise keyboard.
        IconSet DetectInitialSet()
        {
            if (SystemInfo.deviceType == DeviceType.Handheld && Gamepad.current == null)
                return IconSet.None;
            if (Gamepad.current != null)
                return SetForGamepad(Gamepad.current);
            return KeyboardSet();
        }

        IconSet? DetectActuatedSet()
        {
            var pad = Gamepad.current;
            if (pad != null && IsGamepadActuated(pad))
                return SetForGamepad(pad);

            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
                return KeyboardSet();

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                return KeyboardSet();

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

        IconSet KeyboardSet() =>
            keyboardTextRoot || !showXboxSetWhenNoKeyboardRoot ? IconSet.KeyboardText : IconSet.Xbox;

        void ApplySet(IconSet set)
        {
            if (_applied && set == Current) return;
            Current = set;
            _applied = true;

            if (xboxIconRoot) xboxIconRoot.SetActive(set == IconSet.Xbox);
            if (psIconRoot) psIconRoot.SetActive(set == IconSet.PlayStation);
            if (keyboardTextRoot) keyboardTextRoot.SetActive(set == IconSet.KeyboardText);
        }
    }
}
