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

        [SerializeField, Tooltip("The fleet's vessel-prefab table, so a row can read an ability's " +
                                 "REAL icon off that vessel's own HUD. Empty loads the one asset " +
                                 "in the project.")]
        VesselPrefabContainer vesselPrefabs;

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
        [Tooltip("How much of an ability's authored description a row shows.\n\n" +
                 "ElementalAbilityMapSO.AbilityDescription is a DESIGN NOTE, not player copy - " +
                 "the shipped ones run to several hundred characters of mechanism - so quoting it " +
                 "whole buries the row it belongs to. FirstSentence is the default because that " +
                 "sentence is reliably the summary and the rest is rationale.")]
        [SerializeField] DescriptionStyle abilityDescriptionStyle = DescriptionStyle.FirstSentence;

        [SerializeField, Tooltip("Hard character cap on a row's description, whatever the style. " +
                                 "0 = no cap. A first sentence that runs long is still a wall.")]
        [Min(0)] int descriptionCharacterCap = 120;

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

        /// <summary>How much of an authored ability description a row quotes.</summary>
        public enum DescriptionStyle
        {
            /// <summary>Headline only - the sentence naming the control and the ability.</summary>
            None = 0,
            /// <summary>The first sentence of the authored description. The default.</summary>
            FirstSentence = 1,
            /// <summary>All of it. For a vessel whose descriptions are genuinely player copy.</summary>
            Full = 2,
        }

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

        // The HUD whose icons this card's rows are drawing, cached per card rather than per row.
        VesselHUDView _hudView;
        VesselClassType _hudVessel = VesselClassType.Any;

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

            // Every row goes DOWN before any goes up. Deactivating only the tail (rows the new
            // card did not need) leaves a row visible whenever the new hull has at least as many
            // as the old one - which is how a Sparrow card kept showing the Dolphin's drift: the
            // row was not stale data, it was a row nobody rebuilt because the count matched.
            foreach (var row in _rows)
                if (row) row.gameObject.SetActive(false);

            int used = BuildFlightRows(0);
            used = BuildAbilityRows(vesselClass, vessel, used);

            for (int i = used; i < _rows.Count; i++)
                if (_rows[i]) _rows[i].gameObject.SetActive(false);

            HideForeignRows();
            _liveRows = used;
        }

        /// <summary>Take every row down — no card selected.</summary>
        public void Clear()
        {
            foreach (var row in _rows)
                if (row) row.gameObject.SetActive(false);

            HideForeignRows();
            _liveRows = 0;
            _lastBeatRow = -1;
        }

        /// <summary>
        /// Switch off anything in the row container this panel did not build.
        ///
        /// <para>The container holds exactly what this panel put there, and the thing that is
        /// reliably NOT that is the hand-authored row the wirer cloned into a prefab: it keeps
        /// rendering its placeholder copy ("Press RT to active drift") above every real row, on
        /// every card, whatever hull is selected. A panel that owns a container has to own all of
        /// it - leaving one child to the scene is how a card ends up advertising another vessel's
        /// ability.</para>
        /// </summary>
        void HideForeignRows()
        {
            if (!rowContainer) return;

            foreach (Transform child in rowContainer)
            {
                if (!child.gameObject.activeSelf) continue;

                var row = child.GetComponent<VesselControlRow>();
                if (row && _rows.Contains(row)) continue;

                child.gameObject.SetActive(false);
            }
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
                         TrimDescription(entry.AbilityDescription),
                         ResolveAbilityIcon(vesselClass, vessel, element, entry.AbilityLabel),
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
        /// The part of an authored description a row should actually show.
        ///
        /// <para>"First sentence" means the first terminator followed by whitespace, so a decimal
        /// or an abbreviation mid-sentence does not cut it short. Newlines end a sentence too -
        /// these fields are written as prose blocks and the first line is routinely the summary.</para>
        /// </summary>
        string TrimDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;
            if (abilityDescriptionStyle == DescriptionStyle.None) return string.Empty;

            var text = description.Trim();

            if (abilityDescriptionStyle == DescriptionStyle.FirstSentence)
            {
                int cut = -1;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '\n' || c == '\r') { cut = i; break; }
                    if (c != '.' && c != '!' && c != '?') continue;
                    if (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])) { cut = i + 1; break; }
                }
                if (cut > 0) text = text[..cut].TrimEnd();
            }

            if (descriptionCharacterCap > 0 && text.Length > descriptionCharacterCap)
            {
                // Break on a word, not mid-word: an ellipsis after half a word reads as a bug.
                int space = text.LastIndexOf(' ', Mathf.Min(descriptionCharacterCap, text.Length - 1));
                text = (space > descriptionCharacterCap / 2 ? text[..space] : text[..descriptionCharacterCap])
                       .TrimEnd(' ', ',', ';', ':', '-') + "…";
            }

            return text;
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
        /// The ability's REAL icon: the one that vessel's own HUD draws for that element.
        ///
        /// <para>The HUD is the authority, and it is keyed by ELEMENT - which is exactly the key
        /// this row already has. Matching the vessel's ability ASSETS by name was the first
        /// attempt and it silently found nothing: <c>SO_VesselAbility.Name</c> and
        /// <c>ElementalAbilityEntry.AbilityLabel</c> are authored independently and do not agree,
        /// so every row fell back to the prefab's placeholder and the Sparrow's card showed four
        /// identical marks. A name match between two lists nobody keeps in step is a lookup that
        /// reports success by showing the wrong thing.</para>
        ///
        /// <para>The HUD prefab is READ, never instantiated - a prefab asset's components are
        /// inspectable as they are, so this costs one <c>GetComponentInChildren</c> per card.</para>
        ///
        /// <para>The name match survives as the fallback for a vessel whose HUD has no icon bound
        /// for that slot (three of the fleet bind none at all yet).</para>
        /// </summary>
        Sprite ResolveAbilityIcon(VesselClassType vesselClass, SO_Vessel vessel, Element element,
                                  string abilityLabel)
        {
            var hud = ResolveHudView(vesselClass);
            if (hud && hud.TryGetAbilityIcon(element, out var icon) && icon && icon.sprite)
                return icon.sprite;

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

        /// <summary>The vessel's HUD view, read off its prefab. Cached per card, not per row.</summary>
        VesselHUDView ResolveHudView(VesselClassType vesselClass)
        {
            if (_hudVessel == vesselClass) return _hudView;

            _hudVessel = vesselClass;
            _hudView = null;

            EnsureVesselPrefabs();
            if (vesselPrefabs && vesselPrefabs.TryGetShipPrefab(vesselClass, out var prefab) && prefab)
                _hudView = prefab.GetComponentInChildren<VesselHUDView>(true);

            if (!_hudView)
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ArcadeLaunch] No VesselHUDView on {vesselClass}'s prefab - ability icons " +
                    "fall back to the vessel asset's own artwork.");

            return _hudView;
        }

        bool _warnedNoVesselPrefabs;

        /// <summary>
        /// The vessel-prefab table has to be WIRED - unlike the glyph set and the bars config it
        /// does not live in Resources ('Assets/_SO_Assets/Vessel Prefab Container.asset'), so
        /// there is no load to fall back on. The Resources attempt stays for a project that later
        /// moves it there; when it fails, say so ONCE rather than quietly drawing placeholder
        /// icons on every card, which is the failure this replaced.
        /// </summary>
        void EnsureVesselPrefabs()
        {
            if (vesselPrefabs) return;

            vesselPrefabs = Resources.Load<VesselPrefabContainer>("VesselPrefabContainer");
            if (vesselPrefabs || _warnedNoVesselPrefabs) return;

            _warnedNoVesselPrefabs = true;
            CSDebug.LogWarning("[ArcadeLaunch] VesselControlsPanel has no VesselPrefabContainer, " +
                               "so ability rows cannot read a vessel's real HUD icons. Wire " +
                               "'Vessel Prefab Container.asset' on the panel.", this);
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
