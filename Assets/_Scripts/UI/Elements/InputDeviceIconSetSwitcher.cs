using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
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

            [Header("Ability attachment")]
            [Tooltip("Move this hint onto the ability icon its control actually drives, resolved at " +
                     "runtime from the vessel's action handler + ElementalAbilityMapSO. Rearranging " +
                     "the ability row then carries the label with it. Hints whose control drives no " +
                     "ability (or on a HUD with no ability row) are left exactly where they were " +
                     "authored, so this is safe to leave on.")]
            public bool attachToAbilityIcon = true;
            [Tooltip("LEGACY fallback offset, used ONLY on a HUD with no ability lockup. When the " +
                     "lockup is present the hint lands on that card's ControlChip socket at zero " +
                     "offset, so this value is ignored - the totem owns the chip's position.")]
            public Vector2 attachOffset = new Vector2(0f, -76f);

            [NonSerialized] public bool Lit;
            [NonSerialized] public bool Applied;
            /// <summary>Resolved ability icon (or lockup chip socket) this hint labels; null until bound.</summary>
            [NonSerialized] public RectTransform AbilityTarget;
            /// <summary>Offset actually used - zero when the target is a lockup chip socket.</summary>
            [NonSerialized] public Vector2 ResolvedOffset;
            /// <summary>
            /// Size the lockup wants this glyph drawn at, from its card's chip socket. Zero on a
            /// HUD with no lockup, where the hint keeps whatever size its own set root gives it.
            /// </summary>
            [NonSerialized] public Vector2 ChipSize;
            /// <summary>Latches the off-screen warning so it is reported once, not every frame.</summary>
            [NonSerialized] public bool OffScreenReported;
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

        /// <summary>Raised whenever the visible device set changes, so the ability lockup can
        /// re-draw its control chips for the device the player is actually holding.</summary>
        public event Action<IconSet> OnSetChanged;

        /// <summary>True while the player is on keyboard/mouse rather than a pad.</summary>
        public bool IsKeyboard => Current == IconSet.KeyboardText;

        /// <summary>
        /// The three device-glyph roots this switcher shows and hides. Exposed so the ability lockup
        /// can EXEMPT them when it retires a HUD's legacy content: they are direct children of the
        /// HUD root and would otherwise be swept away with the old UI, taking every control hint
        /// with them.
        /// </summary>
        public IEnumerable<GameObject> IconSetRoots
        {
            get
            {
                if (xboxIconRoot) yield return xboxIconRoot;
                if (psIconRoot) yield return psIconRoot;
                if (keyboardTextRoot) yield return keyboardTextRoot;
            }
        }

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
            TryApplyAbilityPlacement();
        }

        // ---------------------------------------------------------------
        // Ability attachment - a hint labels an ABILITY, not a position
        // ---------------------------------------------------------------

        int _placementAttempts;
        bool _placementPending;

        /// <summary>
        /// Binds every hint to the ability icon its control drives, then places it there. Called once
        /// from <c>VesselHUDController.Initialize</c>, after the action handler is initialized.
        ///
        /// The chain is entirely data-driven, which is the point: hint → physical control
        /// (<see cref="HintBinding"/>) → input events (<see cref="InputHintBindingMap"/>) → the ability
        /// bound to that input (the vessel's <c>ElementalAbilityMapSO</c>, falling back to a shared
        /// action asset in <c>R_VesselActionHandler</c> when the touch and gamepad maps use different
        /// events for one ability) → that element's icon in the HUD row. Move an icon, or reassign an
        /// ability to a different input in the action handler, and the label follows on its own.
        /// </summary>
        public void BindHintsToAbilities(IVesselStatus status, VesselHUDView view)
        {
            if (status == null || !view) return;

            var map = status.ElementalAbilityHandler ? status.ElementalAbilityHandler.Map : null;
            if (map == null) return;

            foreach (var visuals in setVisuals)
            {
                if (visuals?.hints == null) continue;
                foreach (var hint in visuals.hints)
                {
                    if (hint == null || !hint.attachToAbilityIcon || !HintRect(hint)) continue;

                    if (!TryResolveElement(status, map, hint.binding, out var element))
                    {
                        if (hint.binding != HintBinding.None)
                            Debug.LogWarning($"[InputDeviceIconSetSwitcher] Control hint '{hint.label}' " +
                                             $"({hint.binding}) drives no ability on this vessel - leaving it " +
                                             "where it was authored. Check the action handler's input map.", this);
                        continue;
                    }

                    // The lockup's chip socket is the canonical target: it is a child of the card,
                    // so the label moves with the totem and needs no per-vessel offset. The icon is
                    // the fallback for a HUD that predates the lockup.
                    if (view.TryGetAbilityChipSocket(element, out var chipSocket))
                    {
                        hint.AbilityTarget = chipSocket;
                        hint.ResolvedOffset = Vector2.zero;

                        // The socket is point-anchored, so its sizeDelta IS its size and can be read
                        // without a layout pass. Taking the size from the LOCKUP is the whole fix:
                        // the device sets author glyphs at wildly different sizes and the chip has
                        // to render them all the same.
                        hint.ChipSize = chipSocket.sizeDelta;
                        if (hint.icon) hint.icon.preserveAspect = true;
                    }
                    else if (view.TryGetAbilityIcon(element, out var abilityIcon))
                    {
                        hint.AbilityTarget = abilityIcon.rectTransform;
                        hint.ResolvedOffset = hint.attachOffset;
                    }
                    else
                    {
                        Debug.LogWarning($"[InputDeviceIconSetSwitcher] Control hint '{hint.label}' labels the " +
                                         $"'{element}' ability but the HUD binds neither a lockup card nor an " +
                                         "icon for it.", this);
                        continue;
                    }

                    _labelledElements.Add(element);
                }
            }

            _placementPending = true;
            _placementAttempts = 0;
            TryApplyAbilityPlacement();
            WarnOnUnlabelledAbilities(status, map);
            _labelledElements.Clear();
        }

        readonly HashSet<Element> _labelledElements = new();

        /// <summary>
        /// The other half of the contract: an ability the player can actually press should have a
        /// control label. Flags the case where an ability is bound to an input in the action handler
        /// but no hint claims it - the "I added a button but forgot the glyph" bug.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void WarnOnUnlabelledAbilities(IVesselStatus status, ElementalAbilityMapSO map)
        {
            var handler = status.ActionHandler;
            if (!handler) return;

            foreach (var entry in map.Entries)
            {
                if (entry == null || _labelledElements.Contains(entry.Element)) continue;
                if (!handler.HasBinding(entry.Input)) continue;   // passive ability - no button, no label
                Debug.LogWarning($"[InputDeviceIconSetSwitcher] The '{entry.Element}' ability " +
                                 $"('{entry.AbilityLabel}') is bound to {entry.Input} but no control hint " +
                                 "labels it. Add a hint with the matching HintBinding.", this);
            }
        }

        /// <summary>
        /// The LEGACY path, for a HUD with no ability lockup: re-anchor a hint over its ability icon
        /// without reparenting it. Needs a laid-out canvas, which may not exist on the frame the
        /// vessel spawns, so it retries for a short while.
        ///
        /// <para>Hints that were adopted into a lockup chip socket are skipped - they are CHILDREN
        /// of the socket and cannot drift, so there is nothing to recompute. Of the rest, only
        /// VISIBLE ones are placed (Unity does not lay out inactive hierarchies, so a hidden set
        /// would measure a stale rect), and <see cref="ApplySet"/> re-arms this every time a set is
        /// shown.</para>
        /// </summary>
        void TryApplyAbilityPlacement()
        {
            if (!_placementPending) return;

            bool allPlaced = true;
            foreach (var visuals in setVisuals)
            {
                if (visuals?.hints == null) continue;
                foreach (var hint in visuals.hints)
                {
                    if (hint?.AbilityTarget == null) continue;
                    var rt = HintRect(hint);
                    if (!rt || !rt.gameObject.activeInHierarchy) continue;   // placed when shown
                    if (!PlaceOnAbilityIcon(rt, hint.AbilityTarget, hint.ResolvedOffset, hint.ChipSize))
                        allPlaced = false;
                    else
                        WarnIfPlacedOffScreen(hint, rt);
                }
            }

            if (allPlaced || ++_placementAttempts > MaxPlacementAttempts)
                _placementPending = false;
        }

        const int MaxPlacementAttempts = 120;

        /// <summary>
        /// A placed hint that lands outside the canvas is silently invisible, which is exactly how this
        /// placement failed three times (a zeroed size, then a clamped anchor fraction plus a negative
        /// offset that pushed every glyph below the screen). Say so instead of rendering nothing.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void WarnIfPlacedOffScreen(HintVisual hint, RectTransform rt)
        {
            if (hint.OffScreenReported) return;

            var canvas = rt.GetComponentInParent<Canvas>();
            if (!canvas) return;
            var canvasRT = canvas.transform as RectTransform;
            if (!canvasRT) return;

            Rect canvasRect = canvasRT.rect;
            Vector2 local = canvasRT.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            Vector2 half = rt.rect.size * 0.5f;

            bool onScreen = local.x + half.x > canvasRect.xMin && local.x - half.x < canvasRect.xMax
                         && local.y + half.y > canvasRect.yMin && local.y - half.y < canvasRect.yMax;
            bool hasArea = rt.rect.width > 0.5f && rt.rect.height > 0.5f;

            if (onScreen && hasArea) return;

            hint.OffScreenReported = true;
            Debug.LogWarning($"[InputDeviceIconSetSwitcher] Control hint '{hint.label}' was placed where it " +
                             $"cannot be seen: rect {rt.rect.size} at canvas-local {local}, canvas {canvasRect}. " +
                             $"Check its offset ({hint.ResolvedOffset}) and the ability card it targets.");
        }

        static RectTransform HintRect(HintVisual hint)
        {
            if (hint.icon) return hint.icon.rectTransform;
            return hint.text ? hint.text.rectTransform : null;
        }

        /// <summary>
        /// Re-anchors a hint onto an ability icon WITHOUT reparenting it - it has to stay under its
        /// icon-set root so the set switcher can keep showing and hiding it. (Reparenting into the
        /// chip socket was tried and reverted: it took the glyphs out from under the roots the
        /// switcher toggles, and switching device sets stopped working.)
        ///
        /// <para><b>Size comes from the lockup, position from the target.</b> The device sets author
        /// their glyphs at wildly different sizes - measured off Squirrel.prefab, the pad strips are
        /// 269x11 with 50x50 glyphs while the PC strip is 366x22 with 106x22 text - so centring them
        /// all on one 24px chip socket gave them DIFFERENT clearances: the pad glyph overhung the
        /// card by 7px while the keyboard one sat 7px clear. Switching pad → keyboard → pad looked
        /// like the label had moved; it never moved, the two sets were never the same size. Passing
        /// a <paramref name="size"/> collapses the anchors to a point and states the size outright,
        /// so every set renders identically.</para>
        ///
        /// <para>Note this is safe where READING the size is not. The authored glyphs are pure
        /// stretch rects with a sizeDelta of zero, so their size comes entirely from their anchor
        /// span; collapsing the anchors and re-supplying the size from <c>rect.size</c> renders them
        /// at ZERO whenever that read happens before a layout pass or while the set root is
        /// inactive - which is every hint on vessel spawn. Supplying the size from the STYLE never
        /// reads a rect at all. With <paramref name="size"/> zero (a HUD with no lockup) the old
        /// span-preserving behaviour is kept exactly.</para>
        ///
        /// <para>The centre is written as a fraction of the hint's own parent, so the placement
        /// survives resolution and aspect changes the same way the ability row does. Returns false
        /// while the parent has no usable rect yet, so the caller can retry.</para>
        /// </summary>
        static bool PlaceOnAbilityIcon(RectTransform hint, RectTransform abilityIcon, Vector2 offset,
                                       Vector2 size)
        {
            if (hint.parent is not RectTransform parent) return false;

            Rect parentRect = parent.rect;
            if (Mathf.Abs(parentRect.width) < 0.01f || Mathf.Abs(parentRect.height) < 0.01f) return false;

            Vector3 targetWorld = abilityIcon.TransformPoint(abilityIcon.rect.center);
            Vector2 local = parent.InverseTransformPoint(targetWorld);

            // MUST be unclamped. The hint roots are thin strips (XBOXRoot is ~11 px tall) and the
            // ability row sits well above them, so the honest fraction is far outside 0..1.
            // Mathf.InverseLerp Clamp01s, which collapsed that to 1.0 and, with the negative
            // attachOffset on top, put every glyph below the bottom of the screen.
            var centre = new Vector2(
                InverseLerpUnclamped(parentRect.xMin, parentRect.xMax, local.x),
                InverseLerpUnclamped(parentRect.yMin, parentRect.yMax, local.y));
            if (!IsUsable(centre.x) || !IsUsable(centre.y)) return false;

            hint.pivot = new Vector2(0.5f, 0.5f);

            if (size.x > 0.5f && size.y > 0.5f)
            {
                hint.anchorMin = hint.anchorMax = centre;   // a point anchor: size is now absolute
                hint.sizeDelta = size;
            }
            else
            {
                Vector2 span = hint.anchorMax - hint.anchorMin;   // legacy: the span IS the size
                hint.anchorMin = centre - span * 0.5f;
                hint.anchorMax = centre + span * 0.5f;
            }

            hint.anchoredPosition = offset;
            return true;
        }

        // A fraction well outside the parent is legitimate (a hint can sit far from its set root), but
        // NaN or a runaway value means the rects were not laid out - retry rather than write garbage.
        static bool IsUsable(float f) => !float.IsNaN(f) && !float.IsInfinity(f) && Mathf.Abs(f) < 50f;

        /// <summary>
        /// Where <paramref name="value"/> lies between a and b, NOT clamped to 0..1.
        /// <see cref="Mathf.InverseLerp"/> deliberately clamps; this placement needs the honest ratio.
        /// </summary>
        static float InverseLerpUnclamped(float a, float b, float value)
            => Mathf.Approximately(a, b) ? float.NaN : (value - a) / (b - a);

        static bool TryResolveElement(IVesselStatus status, ElementalAbilityMapSO map,
            HintBinding binding, out Element element)
        {
            element = Element.None;

            var inputs = InputHintBindingMap.InputEventsFor(binding);
            if (inputs.Count == 0) return false;

            // 1. The ability's own authored input is one this control raises - the common case.
            foreach (var entry in map.Entries)
            {
                if (entry == null) continue;
                for (int i = 0; i < inputs.Count; i++)
                    if (entry.Input == inputs[i]) { element = entry.Element; return true; }
            }

            // 2. Otherwise go through the action handler: the ability's input and this control's input
            //    start the same action asset. Covers a vessel whose touch and gamepad maps drive one
            //    ability from two different input events.
            var handler = status.ActionHandler;
            if (!handler) return false;

            var viaControl = new List<ShipActionSO>();
            for (int i = 0; i < inputs.Count; i++)
                handler.CollectBoundActions(inputs[i], viaControl);
            if (viaControl.Count == 0) return false;

            var viaAbility = new List<ShipActionSO>();
            foreach (var entry in map.Entries)
            {
                if (entry == null) continue;
                viaAbility.Clear();
                handler.CollectBoundActions(entry.Input, viaAbility);
                for (int i = 0; i < viaAbility.Count; i++)
                    if (viaControl.Contains(viaAbility[i])) { element = entry.Element; return true; }
            }
            return false;
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

            // The set that just became visible has never been placed against a live rect - hidden
            // hierarchies are skipped by TryApplyAbilityPlacement precisely so a stale one can never
            // be baked in. Re-arm so it is placed on the next frame, every time.
            _placementPending = true;
            _placementAttempts = 0;
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
