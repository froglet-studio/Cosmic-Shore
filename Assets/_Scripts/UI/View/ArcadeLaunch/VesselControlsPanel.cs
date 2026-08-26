using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's controls block: how you FLY this mode's hull, and what its four abilities
    /// do — the thing a player needs before the match, in the order they need it.
    ///
    /// <para><b>Flight first, then abilities.</b> A player who cannot yaw cannot use an ability, so
    /// the authored flight rows (yaw, pitch, throttle, roll) come first and the derived ability rows
    /// follow. Flight axes are authored because they are a property of the INPUT SCHEME rather than
    /// of any vessel — the same four sticks fly every hull, so deriving them per vessel would be
    /// deriving a constant.</para>
    ///
    /// <para><b>Every ability row is DERIVED, exactly as the ability lockup derives its control
    /// chip</b> (<c>Docs/ABILITY_LOCKUP.md</c>): the vessel's <see cref="ElementalAbilityMapSO"/>
    /// names the ability and the <see cref="InputEvents"/> it rides,
    /// <see cref="InputHintBindingMap.BindingFor"/> turns that into a physical control, and
    /// <see cref="ControlGlyphSetSO"/> turns the control into artwork and a name. Nothing is
    /// authored per vessel, so re-binding an ability moves its whole row with it and a wrong label
    /// is structurally impossible.</para>
    ///
    /// <para>Rows run charge → mass → space → time, the fleet's ability-row order, so the block
    /// reads the way the in-game HUD row reads.</para>
    ///
    /// <para><b>The demonstration is one sweep, not N loops.</b> One phase advances here; the row
    /// whose turn it is plays the ability lockup's own three beats — press flash, the clockwise
    /// recharge veil, the ready flash — and every other row is dimmed. So the block teaches one
    /// control at a time, in the game's own visual language, and an off-screen panel costs nothing
    /// (<see cref="Update"/> returns on its first line).</para>
    /// </summary>
    public class VesselControlsPanel : MonoBehaviour
    {
        /// <summary>
        /// One authored flight row. These are not derivable: a flight axis is a property of the
        /// input scheme, not of a vessel, and no asset in the project maps "yaw" to a sprite.
        /// </summary>
        [Serializable]
        public class FlightControl
        {
            [Tooltip("e.g. 'Left stick — Yaw & Pitch'. Shown as the row's headline.")]
            public string Headline = "";

            [TextArea(1, 3)]
            [Tooltip("What it does, in one line.")]
            public string Description = "";

            [Tooltip("Artwork for this axis. Optional - the row keeps its prefab sprite without one.")]
            public Sprite Icon;

            [Tooltip("Optional physical control, so this row can draw a chip like an ability row " +
                     "does. Leave as None for an axis that is not a single button.")]
            public HintBinding Control = HintBinding.None;
        }

        [Header("Rows")]
        [SerializeField, Tooltip("Container the rows are built under. Put a Vertical Layout Group " +
                                 "on it; this component writes no rects.")]
        RectTransform rowContainer;

        [SerializeField, Tooltip("Row prefab, one per control.")]
        VesselControlRow rowPrefab;

        [Header("Flight controls (authored - the same sticks fly every hull)")]
        [SerializeField, Tooltip("Shown ABOVE the ability rows, in this order. Empty draws " +
                                 "abilities only, which is a reasonable panel - just a quieter one.")]
        List<FlightControl> flightControls = new()
        {
            new FlightControl { Headline = "Left stick", Description = "Steer - yaw and pitch." },
            new FlightControl { Headline = "Right stick", Description = "Throttle and roll." },
        };

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

        [Header("Copy")]
        [SerializeField, Tooltip("Headline for an ability the player fires with a control. " +
                                 "{0} = control name (RT, LT, A…), {1} = ability name.")]
        string abilityHeadlineFormat = "Press {0} to activate {1}";

        [SerializeField, Tooltip("Headline for an ability with no control at all - a passive. " +
                                 "{0} = ability name.")]
        string passiveHeadlineFormat = "{0} (passive)";

        [Header("Demonstration")]
        [SerializeField, Tooltip("Seconds each row holds the demonstration before it travels on.")]
        [Min(0.5f)] float rowDwellSeconds = 2.2f;

        [SerializeField, Tooltip("Fraction of a row's turn spent travelling in and out of the " +
                                 "highlight.")]
        [Range(0.05f, 0.5f)] float sweepRampFraction = 0.25f;

        [SerializeField, Tooltip("Fraction of a row's turn the recharge veil takes to clear. The " +
                                 "beats are press flash → veil sweeps off → ready flash, which is " +
                                 "the ability lockup's own grammar (Docs/ABILITY_LOCKUP.md).")]
        [Range(0.2f, 0.9f)] float cooldownFraction = 0.6f;

        /// <summary>Charge → mass → space → time: the fleet's ability-row order.</summary>
        static readonly Element[] DisplayOrder =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        readonly List<VesselControlRow> _rows = new();
        int _liveRows;
        float _phase;
        int _lastBeatRow = -1;
        VesselClassType _boundVessel = VesselClassType.Any;

        // Held so a device change can re-derive the rows without losing the ability ARTWORK, which
        // only the vessel asset carries. Re-showing with the class alone would silently drop every
        // icon back to the prefab's placeholder.
        SO_Vessel _boundVesselAsset;
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
        /// Draw the controls for a hull. <see cref="VesselClassType.Any"/> — a mode that does not
        /// lock a vessel — still draws the FLIGHT rows: how you steer is true whatever you end up
        /// flying. Only the ability rows need a definite hull.
        /// </summary>
        public void Show(VesselClassType vesselClass, SO_Vessel vessel = null)
        {
            _boundVessel = vesselClass;
            _boundVesselAsset = vessel;
            _phase = 0f;
            _lastBeatRow = -1;

            if (vesselNameText)
                vesselNameText.text = vessel && !string.IsNullOrWhiteSpace(vessel.Name)
                    ? vessel.Name
                    : vesselClass is VesselClassType.Any or VesselClassType.Random
                        ? string.Empty
                        : vesselClass.ToString();

            if (vesselIcon)
            {
                var sprite = vessel ? vessel.IconActive : null;
                vesselIcon.gameObject.SetActive(sprite);
                if (sprite) vesselIcon.sprite = sprite;
            }

            _keyboardWhenBound = UsingKeyboard();
            EnsureGlyphSet();
            EnsureBarsConfig();

            int used = BuildFlightRows(0);
            used = BuildAbilityRows(vesselClass, vessel, used);

            for (int i = used; i < _rows.Count; i++)
                if (_rows[i]) _rows[i].gameObject.SetActive(false);

            _liveRows = used;
        }

        /// <summary>Take every row down — no card selected.</summary>
        public void Clear()
        {
            foreach (var row in _rows)
                if (row) row.gameObject.SetActive(false);
            _liveRows = 0;
            _lastBeatRow = -1;
        }

        int BuildFlightRows(int used)
        {
            if (flightControls == null) return used;

            foreach (var control in flightControls)
            {
                if (control == null || string.IsNullOrWhiteSpace(control.Headline)) continue;

                var row = RowAt(used);
                if (!row) return used;

                var glyph = glyphSet ? glyphSet.For(control.Control) : null;

                row.Bind(Element.None,
                         control.Headline,
                         control.Description,
                         control.Icon,
                         petal: null,                         // no element upgrades an axis
                         padGlyph: _keyboardWhenBound ? null : glyph?.padGlyph,
                         keyboardLabel: _keyboardWhenBound ? glyph?.keyboardLabel : null,
                         showsCooldown: false);               // steering does not recharge
                used++;
            }
            return used;
        }

        int BuildAbilityRows(VesselClassType vesselClass, SO_Vessel vessel, int used)
        {
            if (vesselClass is VesselClassType.Any or VesselClassType.Random) return used;

            var map = ElementalAbilityMapSO.LoadFor(vesselClass);
            if (!map)
            {
                // Not a fault: a vessel without a map simply has no authored ability set yet, and
                // the flight rows above still say everything that is true.
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ArcadeLaunch] No ElementalAbilityMap for {vesselClass}; flight rows only.");
                return used;
            }

            foreach (var element in DisplayOrder)
            {
                var entry = map.GetEntry(element);
                if (entry == null) continue;

                // An unauthored slot is a real state on three vessels today (the maps ship with
                // "(open design slot)" entries). Drawing it would promise an ability that does not
                // exist, so the row is simply not made.
                if (string.IsNullOrWhiteSpace(entry.AbilityLabel) ||
                    entry.AbilityLabel.Contains("open design slot"))
                    continue;

                var row = RowAt(used);
                if (!row) return used;

                var binding = InputHintBindingMap.BindingFor(entry.Input, _keyboardWhenBound);
                var glyph = glyphSet ? glyphSet.For(binding) : null;
                string controlName = ControlDisplayName(binding, glyph, _keyboardWhenBound);

                string headline = string.IsNullOrEmpty(controlName)
                    ? string.Format(passiveHeadlineFormat, entry.AbilityLabel)
                    : string.Format(abilityHeadlineFormat, controlName, entry.AbilityLabel);

                row.Bind(element,
                         headline,
                         entry.AbilityDescription,
                         ResolveAbilityIcon(vessel, entry.AbilityLabel),
                         barsConfig ? barsConfig.GetPetalSprite(element) : null,
                         _keyboardWhenBound ? null : glyph?.padGlyph,
                         _keyboardWhenBound ? glyph?.keyboardLabel : null,
                         // A passive has nothing to fire and therefore nothing to recharge.
                         showsCooldown: binding != HintBinding.None);

                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ArcadeLaunch] {map.VesselClass} {element}: '{entry.AbilityLabel}' " +
                    $"input={entry.Input} control={binding} glyph={(glyph != null)}");

                used++;
            }
            return used;
        }

        /// <summary>
        /// What to CALL a control in a sentence. The keyboard's own label when the player is on a
        /// keyboard (that is exactly what the fleet authored it for); the pad's short name
        /// otherwise.
        ///
        /// <para>The pad names are one family's vocabulary — "RT", "A" — which is the same
        /// single-family simplification the shipped glyph set already makes, and the same recorded
        /// cost: a PlayStation player reads Xbox names (<c>Docs/ABILITY_LOCKUP.md</c>). Fixing it is
        /// one field on <c>ControlGlyphSetSO</c> and would fix both surfaces at once.</para>
        /// </summary>
        static string ControlDisplayName(HintBinding binding, ControlGlyphSetSO.Glyph glyph, bool keyboard)
        {
            if (binding == HintBinding.None) return string.Empty;

            if (keyboard)
                return glyph != null && !string.IsNullOrWhiteSpace(glyph.keyboardLabel)
                    ? glyph.keyboardLabel
                    : string.Empty;      // no keyboard equivalent - blank is the honest answer

            return binding switch
            {
                HintBinding.PadLeftTrigger => "LT",
                HintBinding.PadRightTrigger => "RT",
                HintBinding.PadLeftShoulder => "LB",
                HintBinding.PadRightShoulder => "RB",
                HintBinding.PadButtonSouth => "A",
                HintBinding.PadButtonEast => "B",
                HintBinding.PadButtonWest => "X",
                HintBinding.PadButtonNorth => "Y",
                _ => string.Empty,
            };
        }

        /// <summary>
        /// The ability's own artwork, matched off the vessel asset by NAME. The elemental map is the
        /// authority on which abilities exist and what they are called; the vessel's ability assets
        /// are where the art lives, and they carry the same names. No match is an ordinary answer —
        /// the row keeps its prefab sprite and the element petal still says which element owns the
        /// slot.
        /// </summary>
        static Sprite ResolveAbilityIcon(SO_Vessel vessel, string abilityLabel)
        {
            if (!vessel || vessel.Abilities == null || string.IsNullOrWhiteSpace(abilityLabel))
                return null;

            foreach (var ability in vessel.Abilities)
            {
                if (!ability || string.IsNullOrWhiteSpace(ability.Name)) continue;
                if (!string.Equals(ability.Name.Trim(), abilityLabel.Trim(),
                                   StringComparison.OrdinalIgnoreCase)) continue;

                return ability.IconActive ? ability.IconActive : ability.IconInactive;
            }
            return null;
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

        void Update()
        {
            if (_liveRows <= 0) return;

            // Unscaled: the menu can hold timeScale at 0 while this panel is open.
            _phase += Time.unscaledDeltaTime / Mathf.Max(0.5f, rowDwellSeconds);
            if (_phase >= _liveRows) _phase -= _liveRows;

            int beatRow = Mathf.Clamp((int)_phase, 0, _liveRows - 1);
            float within = _phase - beatRow;               // 0..1 through this row's turn

            for (int i = 0; i < _liveRows; i++)
                if (_rows[i]) _rows[i].SetSweep(SweepWeight(i));

            DriveBeat(beatRow, within);
        }

        /// <summary>
        /// The in-game grammar, replayed on whichever row's turn it is: the press flash at the top
        /// of the turn, the recharge veil sweeping off the icon, then the ready flash the moment it
        /// clears. Exactly the three beats <c>AbilityLockupView</c> plays on a real ability, with
        /// the same colours out of the same style asset.
        /// </summary>
        void DriveBeat(int beatRow, float within)
        {
            var row = _rows[beatRow];
            if (!row) return;

            if (beatRow != _lastBeatRow)
            {
                // Entering a row: clear the previous one so a turn cut short never leaves a veil
                // parked over an icon.
                if (_lastBeatRow >= 0 && _lastBeatRow < _rows.Count && _rows[_lastBeatRow])
                {
                    _rows[_lastBeatRow].SetCooldown(0f);
                    _rows[_lastBeatRow].SetFlash(Color.clear);
                }
                _lastBeatRow = beatRow;
                if (row.DemonstratesCooldown) row.SetFlash(row.PressFlashColor);
            }

            if (!row.DemonstratesCooldown) return;

            float span = Mathf.Max(0.01f, cooldownFraction);
            if (within <= span)
            {
                // The veil DEPLETES: 1 the instant it fires, 0 when it is ready.
                row.SetCooldown(1f - within / span);
                row.SetFlash(FadeToClear(row.PressFlashColor, within / span * 4f));
                return;
            }

            row.SetCooldown(0f);
            // The beat the player is actually waiting for, held briefly then decayed.
            float after = (within - span) / Mathf.Max(0.01f, 1f - span);
            row.SetFlash(FadeToClear(row.ReadyFlashColor, after * 2f));
        }

        static Color FadeToClear(Color color, float t01)
            => new(color.r, color.g, color.b, color.a * Mathf.Clamp01(1f - t01));

        /// <summary>
        /// How lit row <paramref name="index"/> is right now. A triangular window one row wide,
        /// wrapped, so the highlight travels off the last row and onto the first without a seam.
        /// </summary>
        float SweepWeight(int index)
        {
            float distance = Mathf.Abs(_phase - index);
            distance = Mathf.Min(distance, _liveRows - distance);   // wrap
            float ramp = Mathf.Max(0.01f, sweepRampFraction * 2f);
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / ramp));
        }

        void HandleDeviceSetChanged(InputDeviceIconSetSwitcher.IconSet _)
        {
            // The chip and the sentence are the point of the row, so a device change re-derives
            // every one of them rather than leaving pad art in front of a keyboard player.
            if (UsingKeyboard() == _keyboardWhenBound) return;
            Show(_boundVessel, _boundVesselAsset);
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
