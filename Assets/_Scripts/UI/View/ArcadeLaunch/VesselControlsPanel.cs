using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's controls block: what this mode's hull can do, and which control does it.
    ///
    /// <para><b>Everything on a row is DERIVED, exactly as the ability lockup derives its control
    /// chip</b> (<c>Docs/ABILITY_LOCKUP.md</c>): the vessel's <see cref="ElementalAbilityMapSO"/>
    /// names the ability and the <see cref="InputEvents"/> it rides,
    /// <see cref="InputHintBindingMap.BindingFor"/> turns that into a physical control, and
    /// <see cref="ControlGlyphSetSO"/> turns the control into artwork. Nothing here is authored
    /// per vessel, so re-binding an ability moves its chip with it and a wrong label is
    /// structurally impossible.</para>
    ///
    /// <para>The block runs in the order the HUD's ability row runs — charge, mass, space, time —
    /// so the launch panel reads left-to-right the same way the in-game row does. A vessel with no
    /// map draws no rows at all rather than four blanks.</para>
    ///
    /// <para><b>The animation is one sweep, not N loops.</b> One phase advances here and every row
    /// is handed its share of it, so the block reads as a single travelling highlight and a panel
    /// that is off screen costs nothing (<c>Update</c> returns on the first line).</para>
    /// </summary>
    public class VesselControlsPanel : MonoBehaviour
    {
        [Header("Rows")]
        [SerializeField, Tooltip("Container the rows are built under. Put a Vertical Layout Group " +
                                 "on it; the panel writes no rects.")]
        RectTransform rowContainer;

        [SerializeField, Tooltip("Row prefab, one per ability.")]
        VesselControlRow rowPrefab;

        [Header("Sources")]
        [SerializeField, Tooltip("Fleet control-glyph table. Empty loads Resources/ControlGlyphSet.")]
        ControlGlyphSetSO glyphSet;

        [SerializeField, Tooltip("Element petal artwork, shared with the HUD's flower row. Empty " +
                                 "loads Resources/ElementalBarsConfig.")]
        ElementalBarsConfigSO barsConfig;

        [SerializeField, Tooltip("Optional: the device switcher whose current device decides " +
                                 "whether rows draw pad glyphs or keyboard labels. Empty finds one " +
                                 "in the scene; none at all falls back to pad artwork.")]
        InputDeviceIconSetSwitcher deviceSwitcher;

        [Header("Header")]
        [SerializeField, Tooltip("Optional: the hull's name, so the block says whose controls " +
                                 "these are. Every arcade mode locks to one vessel.")]
        TMPro.TMP_Text vesselNameText;

        [SerializeField, Tooltip("Optional: the hull's icon.")]
        UnityEngine.UI.Image vesselIcon;

        [Header("Sweep")]
        [SerializeField, Tooltip("Seconds each row holds the highlight before it travels on.")]
        [Min(0.1f)] float rowDwellSeconds = 1.1f;

        [SerializeField, Tooltip("Fraction of a row's turn spent travelling in and out of the " +
                                 "highlight. 0.5 means it is never fully settled.")]
        [Range(0.05f, 0.5f)] float sweepRampFraction = 0.3f;

        /// <summary>Charge → mass → space → time: the fleet's ability-row order.</summary>
        static readonly Element[] DisplayOrder =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        readonly List<VesselControlRow> _rows = new();
        float _phase;
        VesselClassType _boundVessel = VesselClassType.Any;
        bool _keyboardWhenBound;

        /// <summary>The hull whose controls are on screen.</summary>
        public VesselClassType BoundVessel => _boundVessel;

        void OnEnable()
        {
            var switcher = ResolveSwitcher();
            if (switcher) switcher.OnSetChanged += HandleDeviceSetChanged;
        }

        void OnDisable()
        {
            if (deviceSwitcher) deviceSwitcher.OnSetChanged -= HandleDeviceSetChanged;
        }

        /// <summary>
        /// Draw the controls for a hull. <see cref="VesselClassType.Any"/> (a mode that does not
        /// lock a vessel) clears the block: with no hull there is no honest set of abilities to
        /// name, and four generic rows would be worse than none.
        /// </summary>
        public void Show(VesselClassType vesselClass, SO_Vessel vessel = null)
        {
            _boundVessel = vesselClass;
            _phase = 0f;

            if (vesselNameText)
                vesselNameText.text = vessel && !string.IsNullOrWhiteSpace(vessel.Name)
                    ? vessel.Name
                    : vesselClass == VesselClassType.Any ? string.Empty : vesselClass.ToString();

            if (vesselIcon)
            {
                var sprite = vessel ? vessel.IconActive : null;
                vesselIcon.gameObject.SetActive(sprite);
                if (sprite) vesselIcon.sprite = sprite;
            }

            if (vesselClass == VesselClassType.Any || vesselClass == VesselClassType.Random)
            {
                Clear();
                return;
            }

            var map = ElementalAbilityMapSO.LoadFor(vesselClass);
            if (!map)
            {
                // Not a fault: most of the fleet's maps exist, and a vessel without one simply has
                // no authored ability set to show yet.
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ArcadeLaunch] No ElementalAbilityMap for {vesselClass}; controls block empty.");
                Clear();
                return;
            }

            _keyboardWhenBound = UsingKeyboard();
            BuildRows(map, vessel);
        }

        /// <summary>Take every row down — a mode with no locked hull, or no card selected.</summary>
        public void Clear()
        {
            foreach (var row in _rows)
                if (row) row.gameObject.SetActive(false);
        }

        void BuildRows(ElementalAbilityMapSO map, SO_Vessel vessel)
        {
            EnsureGlyphSet();
            EnsureBarsConfig();

            int used = 0;
            foreach (var element in DisplayOrder)
            {
                var entry = map.GetEntry(element);
                if (entry == null) continue;

                // An unauthored slot is a real state on three vessels today (the map ships with
                // "(open design slot)" entries). Drawing it would promise an ability that does not
                // exist, so the row is simply not made.
                if (string.IsNullOrWhiteSpace(entry.AbilityLabel) ||
                    entry.AbilityLabel.Contains("open design slot"))
                    continue;

                var row = RowAt(used);
                if (!row) break;

                var binding = InputHintBindingMap.BindingFor(entry.Input, _keyboardWhenBound);
                var glyph = glyphSet ? glyphSet.For(binding) : null;

                row.Bind(
                    element,
                    entry.AbilityLabel,
                    entry.AbilityDescription,
                    ResolveAbilityIcon(vessel, entry.AbilityLabel),
                    barsConfig ? barsConfig.GetPetalSprite(element) : null,
                    _keyboardWhenBound ? null : glyph?.padGlyph,
                    _keyboardWhenBound ? glyph?.keyboardLabel : null);

                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ArcadeLaunch] {map.VesselClass} {element}: '{entry.AbilityLabel}' " +
                    $"input={entry.Input} control={binding} glyph={(glyph != null)}");

                used++;
            }

            for (int i = used; i < _rows.Count; i++)
                if (_rows[i]) _rows[i].gameObject.SetActive(false);
        }

        VesselControlRow RowAt(int index)
        {
            while (_rows.Count <= index)
            {
                if (!rowPrefab || !rowContainer)
                {
                    CSDebug.LogWarning("[ArcadeLaunch] VesselControlsPanel needs both a rowPrefab " +
                                       "and a rowContainer to draw anything.", this);
                    return null;
                }
                _rows.Add(Instantiate(rowPrefab, rowContainer));
            }
            return _rows[index];
        }

        /// <summary>
        /// The ability's own artwork, matched off the vessel asset by NAME. The elemental map is
        /// the authority on which abilities exist and what they are called; the vessel's ability
        /// assets are where the art lives, and they carry the same names. No match is an ordinary
        /// answer — the row then keeps its prefab sprite and the element petal still says which
        /// element owns the slot.
        /// </summary>
        static Sprite ResolveAbilityIcon(SO_Vessel vessel, string abilityLabel)
        {
            if (!vessel || vessel.Abilities == null || string.IsNullOrWhiteSpace(abilityLabel))
                return null;

            foreach (var ability in vessel.Abilities)
            {
                if (!ability || string.IsNullOrWhiteSpace(ability.Name)) continue;
                if (!string.Equals(ability.Name.Trim(), abilityLabel.Trim(),
                                   System.StringComparison.OrdinalIgnoreCase)) continue;

                return ability.IconActive ? ability.IconActive : ability.IconInactive;
            }
            return null;
        }

        void Update()
        {
            if (_rows.Count == 0) return;

            int live = 0;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] && _rows[i].gameObject.activeSelf) live++;

            if (live <= 0) return;

            // Unscaled: the menu can hold timeScale at 0 while this panel is open.
            _phase += Time.unscaledDeltaTime / Mathf.Max(0.1f, rowDwellSeconds);
            if (_phase >= live) _phase -= live;

            for (int i = 0; i < live; i++)
                if (_rows[i]) _rows[i].SetSweep(SweepWeight(i, live));
        }

        /// <summary>
        /// How lit row <paramref name="index"/> is right now. A triangular window one row wide,
        /// with its ramps set by <see cref="sweepRampFraction"/> — wrapped, so the highlight
        /// travels off the last row and onto the first without a seam.
        /// </summary>
        float SweepWeight(int index, int liveRows)
        {
            float distance = Mathf.Abs(_phase - index);
            distance = Mathf.Min(distance, liveRows - distance);   // wrap
            float ramp = Mathf.Max(0.01f, sweepRampFraction * 2f);
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / ramp));
        }

        void HandleDeviceSetChanged(InputDeviceIconSetSwitcher.IconSet _)
        {
            // The chip is the whole point of the row, so a device change re-derives every one of
            // them rather than leaving pad art in front of a keyboard player.
            if (_boundVessel == VesselClassType.Any) return;
            if (UsingKeyboard() == _keyboardWhenBound) return;
            Show(_boundVessel);
        }

        bool UsingKeyboard()
        {
            var switcher = ResolveSwitcher();
            return switcher && switcher.Current == InputDeviceIconSetSwitcher.IconSet.KeyboardText;
        }

        InputDeviceIconSetSwitcher ResolveSwitcher()
        {
            if (deviceSwitcher) return deviceSwitcher;
            deviceSwitcher = FindFirstObjectByType<InputDeviceIconSetSwitcher>(FindObjectsInactive.Include);
            return deviceSwitcher;
        }

        void EnsureGlyphSet()
        {
            if (!glyphSet) glyphSet = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
        }

        void EnsureBarsConfig()
        {
            if (!barsConfig) barsConfig = Resources.Load<ElementalBarsConfigSO>("ElementalBarsConfig");
        }
    }
}
