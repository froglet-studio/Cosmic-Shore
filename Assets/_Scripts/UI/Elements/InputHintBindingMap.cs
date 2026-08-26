using System.Collections.Generic;
using CosmicShore.Data;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The physical-control → <see cref="InputEvents"/> table, mirroring what the input strategies
    /// actually raise (<c>GamepadInputStrategy</c>, <c>KeyboardInputStrategy</c>,
    /// <c>SingleStickMouseInputStrategy</c> — NOT the unreferenced <c>KeyboardMouseInputStrategy</c>,
    /// which this doc used to name and which no selector instantiates).
    ///
    /// This is the missing link that lets a control hint find the ability it labels. A hint knows its
    /// physical control (LT, RT, A, …); this maps that to the input events a vessel can bind; the
    /// vessel's <c>R_VesselActionHandler</c> and <c>ElementalAbilityMapSO</c> turn those into the
    /// ability — and therefore the ability ICON — the hint belongs to. Rearranging the icon row then
    /// moves the label with it, because the label was never bound to a position in the first place.
    ///
    /// Keep in sync with the input strategies. A control that raises no ability input maps to nothing
    /// and its hint simply stays where it was authored.
    /// </summary>
    public static class InputHintBindingMap
    {
        static readonly InputEvents[] Nothing = new InputEvents[0];

        // Triggers (pad) and the shift keys (keyboard) raise the same per-side events: the combined
        // one while held, and the "Only" variant while the opposite side is released. A vessel may
        // bind either, so a hint has to accept both.
        static readonly InputEvents[] LeftSide =
            { InputEvents.LeftStickAction, InputEvents.OnlyLeftStickAction };
        static readonly InputEvents[] RightSide =
            { InputEvents.RightStickAction, InputEvents.OnlyRightStickAction };

        static readonly Dictionary<HintBinding, InputEvents[]> Map = new()
        {
            [HintBinding.PadLeftTrigger]  = LeftSide,
            [HintBinding.PadRightTrigger] = RightSide,
            [HintBinding.KeyLeftShift]    = LeftSide,
            [HintBinding.KeyRightShift]   = RightSide,

            [HintBinding.PadButtonSouth]   = new[] { InputEvents.Button1Action },
            [HintBinding.PadButtonEast]    = new[] { InputEvents.Button2Action },
            [HintBinding.PadButtonWest]    = new[] { InputEvents.Button3Action },
            [HintBinding.PadRightShoulder] = new[] { InputEvents.FlipAction },

            // The keyboard's three action keys. Both desktop schemes raise these
            // (KeyboardInputStrategy and SingleStickMouseInputStrategy share their button half),
            // and without them BindingFor(..., keyboard: true) returned None for every ability
            // bound to a pad face button — which is half the Sparrow's row, drawn blank on the
            // one device most people play on.
            [HintBinding.KeySpace] = new[] { InputEvents.Button1Action },
            [HintBinding.KeyB]     = new[] { InputEvents.Button2Action },
            [HintBinding.KeyN]     = new[] { InputEvents.Button3Action },
        };

        /// <summary>
        /// A keyboard control's PAD twin — the same logical control at its other address.
        ///
        /// <para><see cref="ControlGlyphSetSO"/> keys one entry per logical control and that entry
        /// carries BOTH representations (<c>padGlyph</c> and <c>keyboardLabel</c>), but
        /// <see cref="BindingFor"/> answers with a different <see cref="HintBinding"/> per device —
        /// so a keyboard lookup asked the glyph set for <c>KeyLeftShift</c> while the label was
        /// authored on <c>PadLeftTrigger</c>, found nothing, and drew a blank chip. Every keyboard
        /// label in the project was dead on arrival for that reason.</para>
        ///
        /// <para>Resolving through here rather than duplicating rows keeps one authored row per
        /// control, so a label and its glyph cannot drift apart. An asset may still author a
        /// keyboard-specific row: <c>ControlGlyphSetSO.For</c> tries the exact binding first and
        /// only then the canonical one.</para>
        /// </summary>
        public static HintBinding Canonical(HintBinding binding) => binding switch
        {
            HintBinding.KeyLeftShift  => HintBinding.PadLeftTrigger,
            HintBinding.KeyRightShift => HintBinding.PadRightTrigger,
            HintBinding.KeySpace      => HintBinding.PadButtonSouth,
            HintBinding.KeyB          => HintBinding.PadButtonEast,
            HintBinding.KeyN          => HintBinding.PadButtonWest,
            _                         => binding,
        };

        /// <summary>
        /// The input events this physical control raises. Empty for controls that drive no ability
        /// (steering, throttle, unmapped glyphs).
        /// </summary>
        public static IReadOnlyList<InputEvents> InputEventsFor(HintBinding binding)
            => Map.TryGetValue(binding, out var events) ? events : Nothing;

        /// <summary>
        /// The INVERSE: which physical control an ability's authored input corresponds to, for the
        /// pad or for the keyboard. This is what lets the ability lockup draw a control chip from
        /// the vessel's own <c>ElementalAbilityMapSO</c> rather than from per-vessel authored
        /// glyphs - the chain ability → input → control → artwork is derived at every step, so a
        /// label can never be confidently wrong the way a hand-authored one can.
        ///
        /// <para>Returns <see cref="HintBinding.None"/> for an input no control raises: a passive
        /// ability (<c>FullSpeedStraightAction</c>) has no button, and on the keyboard several pad
        /// buttons have no equivalent at all. Both are honestly blank rather than mislabelled.</para>
        /// </summary>
        public static HintBinding BindingFor(InputEvents input, bool keyboard)
        {
            foreach (var pair in Map)
            {
                if (IsKeyboard(pair.Key) != keyboard) continue;
                foreach (var candidate in pair.Value)
                    if (candidate == input) return pair.Key;
            }
            return HintBinding.None;
        }

        static bool IsKeyboard(HintBinding binding) => binding >= HintBinding.KeyLeftShift;
    }
}
