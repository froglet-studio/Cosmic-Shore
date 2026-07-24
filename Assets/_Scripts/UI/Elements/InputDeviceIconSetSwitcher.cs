using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Auto-detects the input device the player is actually using and shows the matching binding-hint
    /// set at the bottom of the ability HUD: the Xbox icon set (XBOXRoot), the PlayStation icon set
    /// (PSRoot), or the PC keyboard text set (PCRoot). Touch hides the hints entirely - the on-screen
    /// ability buttons ARE the touch controls, so pad glyphs there are misinformation.
    ///
    /// Detection is by LAST MEANINGFUL ACTUATION, not device presence: a connected-but-idle pad never
    /// steals the hints from the keyboard, and picking the pad back up switches within one input.
    /// Deliberately polls explicit controls (buttons/sticks/keys) instead of device.lastUpdateTime -
    /// DualShock sensor noise updates the timestamp every frame and would pin the pad set forever.
    ///
    /// <b>Hint visuals.</b> Each set carries its own list of hint entries, held in a runtime
    /// dictionary keyed by set. A hint lights up while its bound control is held:
    ///   - controller sets swap between an ACTIVE and an INACTIVE sprite (plus optional tint),
    ///   - the PC set is text-only, so its entries change COLOR only (leave the sprites empty).
    /// Entries with binding None never light (static glyphs).
    ///
    /// Lives on the vessel HUD, next to the icon roots it drives. Purely presentational - input
    /// strategy selection stays in <c>InputController</c>.
    /// </summary>
    public class InputDeviceIconSetSwitcher : MonoBehaviour
    {
        public enum IconSet { Xbox, PlayStation, KeyboardText, None }

        /// <summary>The control a hint mirrors, polled while its set is visible.</summary>
        public enum HintBinding
        {
            None = 0,
            // Gamepad
            PadButtonSouth = 1, PadButtonNorth = 2, PadButtonEast = 3, PadButtonWest = 4,
            PadLeftShoulder = 5, PadRightShoulder = 6, PadLeftTrigger = 7, PadRightTrigger = 8,
            PadDpadUp = 9, PadDpadDown = 10, PadDpadLeft = 11, PadDpadRight = 12,
            // Keyboard / mouse (PC text set)
            KeyLeftShift = 20, KeyRightShift = 21, KeySpace = 22, KeyTab = 23,
            KeyQ = 24, KeyE = 25, KeyF = 26,
            MouseLeft = 40, MouseRight = 41,
        }

        [Serializable]
        public class HintVisual
        {
            [Tooltip("Designer note only (e.g. \"Drift L\").")]
            public string label;
            [Tooltip("The control this hint mirrors - it lights while the control is held. None = static.")]
            public HintBinding binding = HintBinding.None;

            [Header("Icon sets (sprite swap)")]
            [Tooltip("Glyph image. Leave empty for text-only entries (PC set).")]
            public Image icon;
            [Tooltip("Sprite while the bound control is held. Empty = tint-only.")]
            public Sprite activeIcon;
            [Tooltip("Sprite at rest. Empty = keep the authored sprite.")]
            public Sprite inactiveIcon;

            [Header("PC text set (color change only)")]
            [Tooltip("Hint text. PC entries wire this and leave the sprites empty.")]
            public TMP_Text text;

            [Header("Tint (applies to whichever of icon/text is wired)")]
            public Color activeColor = Color.white;
            public Color inactiveColor = new Color(0.65f, 0.65f, 0.7f, 1f);

            [NonSerialized] public bool Lit;
            [NonSerialized] public bool Applied;
        }

        [Serializable]
        public class IconSetVisuals
        {
            public IconSet set = IconSet.Xbox;
            public List<HintVisual> hints = new();
        }

        [Header("Icon set roots")]
        [Tooltip("Root of the Xbox controller glyphs (XBOXRoot).")]
        [SerializeField] private GameObject xboxIconRoot;
        [Tooltip("Root of the PlayStation controller glyphs (PSRoot).")]
        [SerializeField] private GameObject psIconRoot;
        [Tooltip("Root of the PC keyboard text hints (PCRoot).")]
        [SerializeField] private GameObject keyboardTextRoot;

        [Header("Hint visuals per set (active/inactive)")]
        [Tooltip("One entry per set. Controller hints swap active/inactive sprites; PC text hints " +
                 "change color only. Held in a dictionary keyed by set at runtime.")]
        [SerializeField] private List<IconSetVisuals> setVisuals = new();

        [Header("Behaviour")]
        [Tooltip("While no PC text root is wired, keep showing the Xbox set for keyboard/mouse play " +
                 "instead of hiding every hint. Wire keyboardTextRoot and this flag stops mattering.")]
        [SerializeField] private bool showXboxSetWhenNoKeyboardRoot = true;
        [Tooltip("Stick deflection that counts as 'using the pad'.")]
        [SerializeField, Range(0.05f, 0.9f)] private float stickActuationThreshold = 0.25f;

        public IconSet Current { get; private set; } = IconSet.None;

        // The requested lookup: set -> its hint visuals. Built once from the serialized list.
        private readonly Dictionary<IconSet, IconSetVisuals> _visualsBySet = new();

        private bool _applied;

        void Awake()
        {
            _visualsBySet.Clear();
            foreach (var sv in setVisuals)
                if (sv != null) _visualsBySet[sv.set] = sv;   // last entry wins on duplicates
        }

        void OnEnable()
        {
            ApplySet(DetectInitialSet());
        }

        void Update()
        {
            var actuated = DetectActuatedSet();
            if (actuated.HasValue) ApplySet(actuated.Value);
            DriveHintVisuals();
        }

        /// <summary>
        /// Manually light/rest a hint of the CURRENT set by its designer label - for hints bound to
        /// gameplay state rather than a raw control (binding None). Safe no-op on unknown labels.
        /// </summary>
        public void SetHintActive(string label, bool active)
        {
            if (!_visualsBySet.TryGetValue(Current, out var visuals)) return;
            foreach (var hint in visuals.hints)
                if (hint != null && hint.binding == HintBinding.None && hint.label == label && hint.Lit != active)
                {
                    hint.Lit = active;
                    hint.Applied = false;
                }
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

            // The freshly-shown set starts at rest.
            if (_visualsBySet.TryGetValue(set, out var visuals))
                foreach (var hint in visuals.hints)
                    if (hint != null) { hint.Lit = false; hint.Applied = false; }
        }

        // Lights each visible hint while its bound control is held. State-change driven - a hint's
        // visuals are only touched on the frame its held-state flips.
        void DriveHintVisuals()
        {
            if (!_visualsBySet.TryGetValue(Current, out var visuals)) return;

            foreach (var hint in visuals.hints)
            {
                if (hint == null) continue;

                if (hint.binding != HintBinding.None)
                {
                    bool held = IsBindingHeld(hint.binding);
                    if (held != hint.Lit) { hint.Lit = held; hint.Applied = false; }
                }

                if (hint.Applied) continue;
                hint.Applied = true;

                if (hint.icon)
                {
                    var sprite = hint.Lit ? hint.activeIcon : hint.inactiveIcon;
                    if (sprite) hint.icon.sprite = sprite;
                    hint.icon.color = hint.Lit ? hint.activeColor : hint.inactiveColor;
                }
                if (hint.text)
                    hint.text.color = hint.Lit ? hint.activeColor : hint.inactiveColor;
            }
        }

        static bool IsBindingHeld(HintBinding binding)
        {
            var pad = Gamepad.current;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            switch (binding)
            {
                case HintBinding.PadButtonSouth: return pad != null && pad.buttonSouth.isPressed;
                case HintBinding.PadButtonNorth: return pad != null && pad.buttonNorth.isPressed;
                case HintBinding.PadButtonEast: return pad != null && pad.buttonEast.isPressed;
                case HintBinding.PadButtonWest: return pad != null && pad.buttonWest.isPressed;
                case HintBinding.PadLeftShoulder: return pad != null && pad.leftShoulder.isPressed;
                case HintBinding.PadRightShoulder: return pad != null && pad.rightShoulder.isPressed;
                case HintBinding.PadLeftTrigger: return pad != null && pad.leftTrigger.isPressed;
                case HintBinding.PadRightTrigger: return pad != null && pad.rightTrigger.isPressed;
                case HintBinding.PadDpadUp: return pad != null && pad.dpad.up.isPressed;
                case HintBinding.PadDpadDown: return pad != null && pad.dpad.down.isPressed;
                case HintBinding.PadDpadLeft: return pad != null && pad.dpad.left.isPressed;
                case HintBinding.PadDpadRight: return pad != null && pad.dpad.right.isPressed;
                case HintBinding.KeyLeftShift: return kb != null && kb.leftShiftKey.isPressed;
                case HintBinding.KeyRightShift: return kb != null && kb.rightShiftKey.isPressed;
                case HintBinding.KeySpace: return kb != null && kb.spaceKey.isPressed;
                case HintBinding.KeyTab: return kb != null && kb.tabKey.isPressed;
                case HintBinding.KeyQ: return kb != null && kb.qKey.isPressed;
                case HintBinding.KeyE: return kb != null && kb.eKey.isPressed;
                case HintBinding.KeyF: return kb != null && kb.fKey.isPressed;
                case HintBinding.MouseLeft: return mouse != null && mouse.leftButton.isPressed;
                case HintBinding.MouseRight: return mouse != null && mouse.rightButton.isPressed;
                default: return false;
            }
        }
    }
}
