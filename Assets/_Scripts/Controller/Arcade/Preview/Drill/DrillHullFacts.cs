using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One hull's facts for the drill, read from the sources the platform already treats as
    /// canonical: the ability names and controls from its <see cref="ElementalAbilityMapSO"/>,
    /// the physical control from <see cref="InputHintBindingMap"/> (the chain the ability lockup
    /// draws its chips from, so a label here cannot disagree with the HUD), and every WORD of a
    /// control's name from authored text - the keyboard label from
    /// <see cref="ControlGlyphSetSO"/>, the pad label and the flight-control labels from
    /// <see cref="DrillLibrarySO.Strings"/>.
    /// </summary>
    public sealed class DrillHullFacts : IDrillHullFacts
    {
        readonly ElementalAbilityMapSO _map;
        readonly DrillLibrarySO _library;
        readonly ControlGlyphSetSO _glyphs;
        readonly bool _keyboard;

        public VesselClassType Vessel { get; }
        public FlightScheme Scheme { get; }
        public bool CanDrift { get; }
        public string VesselName { get; }
        public string ModeName { get; }

        public DrillHullFacts(VesselClassType vessel, FlightScheme scheme, bool canDrift,
                              ElementalAbilityMapSO map, DrillLibrarySO library, ControlGlyphSetSO glyphs,
                              bool keyboard, string vesselName, string modeName)
        {
            Vessel = vessel;
            Scheme = scheme;
            CanDrift = canDrift;
            _map = map;
            _library = library;
            _glyphs = glyphs;
            _keyboard = keyboard;
            VesselName = vesselName;
            ModeName = modeName;
        }

        ElementalAbilityEntry Entry(Element element) => _map != null ? _map.GetEntry(element) : null;

        public bool TryAbility(Element element, out string label, out string description)
        {
            var entry = Entry(element);
            label = entry?.AbilityLabel;
            description = entry?.AbilityDescription;
            return !string.IsNullOrEmpty(label);
        }

        /// <summary>
        /// An ability exists and has a button. <c>FullSpeedStraightAction</c> - the enum's zero -
        /// is both a real event and the "no button" sentinel a passive entry is authored with, so
        /// it never counts as a binding (the ability lockup's chip reads it the same way).
        /// </summary>
        public bool AbilityHasInput(Element element)
        {
            var entry = Entry(element);
            return entry != null && !string.IsNullOrEmpty(entry.AbilityLabel) &&
                   entry.Input != InputEvents.FullSpeedStraightAction;
        }

        public bool TryAbilityControlLabel(Element element, out string label)
        {
            label = null;
            if (!AbilityHasInput(element)) return false;

            var binding = InputHintBindingMap.BindingFor(Entry(element).Input, _keyboard);
            if (binding == InputDeviceIconSetSwitcher.HintBinding.None) return false;

            if (_keyboard)
                label = _glyphs != null ? _glyphs.For(binding)?.keyboardLabel : null;
            else if (_library != null)
                label = _library.PadLabel(binding) ?? _library.PadLabel(InputHintBindingMap.Canonical(binding));

            return !string.IsNullOrEmpty(label);
        }

        public bool TryControlLabel(DrillControl control, out string label)
        {
            label = _library != null ? _library.FlightLabel(control, Scheme, _keyboard) : null;
            return !string.IsNullOrEmpty(label);
        }
    }
}
