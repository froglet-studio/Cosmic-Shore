using System.Collections.Generic;
using CosmicShore.Data;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The physical-control → <see cref="InputEvents"/> table, mirroring what the input strategies
    /// actually raise (<c>GamepadInputStrategy</c>, <c>KeyboardMouseInputStrategy</c>).
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
        };

        /// <summary>
        /// The input events this physical control raises. Empty for controls that drive no ability
        /// (steering, throttle, unmapped glyphs).
        /// </summary>
        public static IReadOnlyList<InputEvents> InputEventsFor(HintBinding binding)
            => Map.TryGetValue(binding, out var events) ? events : Nothing;
    }
}
