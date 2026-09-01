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
    /// <para><b>Authored rows come from ONE asset, and the default is none.</b>
    /// <see cref="ModeControlsLibrarySO"/> (<c>Resources/ModeControlsLibrary</c>) says what a
    /// mode's section shows besides the abilities - the stick primer that used to open every card
    /// lives there now, per mode, and ships switched off: a card's designated abilities and their
    /// controls ARE the section unless that mode's entry says otherwise. Editing what a mode's
    /// section opens with is editing that asset, never this panel or a scene.</para>
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

        [Header("Authored rows")]
        [SerializeField, Tooltip("LEGACY FALLBACK, used only when no Resources/ModeControlsLibrary " +
                                 "asset exists. With the library present - which is the shipped " +
                                 "state - the authored rows come from it PER MODE and this list is " +
                                 "never read. Kept so an older scene without the asset still draws.")]
        List<FlightControl> flightControls = new();

        [SerializeField, Tooltip("Per-mode section content. Empty loads " +
                                 "Resources/ModeControlsLibrary.")]
        ModeControlsLibrarySO controlsLibrary;

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
        [SerializeField, Tooltip("Heading above the mode's authored rows. Empty draws no heading.")]
        string flightSectionTitle = "Controls";

        [SerializeField, Tooltip("Heading above the derived ability rows. Empty draws no heading.")]
        string abilitySectionTitle = "Abilities";

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
        readonly List<int> _sweepRows = new();
        readonly List<HintBinding> _rowBindings = new();
        int _liveRows;
        float _phase;
        int _lastBeatRow = -1;
        VesselClassType _boundVessel = VesselClassType.Any;
        GameModes _boundMode = GameModes.Random;

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
        public void Show(VesselClassType vesselClass, SO_Vessel vessel = null,
                         GameModes mode = GameModes.Random)
        {
            _boundVessel = vesselClass;
            _boundMode = mode;
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

            EnsureControlsLibrary();

            // A mode may name the hull its card describes, which is the answer for a card listing
            // several (Scurry, Brood Rush, Freestyle) where the panel would otherwise describe
            // whichever happens to be first.
            if (controlsLibrary)
            {
                var named = controlsLibrary.VesselFor(mode);
                if (named is not (VesselClassType.Any or VesselClassType.Random))
                {
                    vesselClass = named;
                    if (vessel && vessel.Class != named) vessel = null;   // its icon is another hull's
                }
            }

            int used = BuildSection(flightSectionTitle, 0, at => BuildAuthoredRows(mode, at));
            if (!controlsLibrary || controlsLibrary.AbilityRowsFor(mode))
                used = BuildSection(abilitySectionTitle, used,
                                    at => BuildAbilityRows(mode, vesselClass, vessel, at));

            for (int i = used; i < _rows.Count; i++)
                if (_rows[i]) _rows[i].gameObject.SetActive(false);

            HideForeignRows();

            _liveRows = used;
            RebuildSweepOrder();
        }

        /// <summary>
        /// A heading, then whatever <paramref name="build"/> puts under it — and the heading is
        /// TAKEN BACK when the builder produced nothing, so a mode with no authored rows does not
        /// show an empty "CONTROLS" label. That is why the heading cannot be written until the rows
        /// beneath it have been counted.
        /// </summary>
        int BuildSection(string title, int used, Func<int, int> build)
        {
            int headerIndex = used;
            bool wantsHeader = !string.IsNullOrWhiteSpace(title);
            if (wantsHeader) used++;                    // reserve the slot, fill it below

            int after = build(used);
            if (after == used) return wantsHeader ? used - 1 : used;

            if (wantsHeader)
            {
                var header = RowAt(headerIndex);
                if (header) header.BindSection(title);
                RecordBinding(headerIndex, HintBinding.None);
            }
            return after;
        }

        /// <summary>
        /// The rows the demonstration travels through: every CONTROL row, never a heading.
        ///
        /// <para>Held as an index list rather than skipped inside the sweep, because the sweep is a
        /// POSITION along a list — leaving headings in it would spend a beat of every cycle
        /// highlighting a word, and the travel would visibly stall twice per pass.</para>
        /// </summary>
        void RebuildSweepOrder()
        {
            _sweepRows.Clear();
            for (int i = 0; i < _liveRows && i < _rows.Count; i++)
                if (_rows[i] && !_rows[i].IsSection) _sweepRows.Add(i);
        }

        /// <summary>Take every row down — no card selected.</summary>
        public void Clear()
        {
            foreach (var row in _rows)
                if (row) row.gameObject.SetActive(false);

            HideForeignRows();
            _sweepRows.Clear();
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

        /// <summary>
        /// The mode's authored rows, resolved library-first: the mode's own entry, else the
        /// library defaults, else - only when no library asset exists at all - this panel's
        /// legacy serialized list.
        /// </summary>
        int BuildAuthoredRows(GameModes mode, int used)
        {
            var rows = controlsLibrary ? controlsLibrary.RowsFor(mode) : flightControls;
            if (rows == null) return used;

            foreach (var control in rows)
            {
                if (control == null || string.IsNullOrWhiteSpace(control.Headline)) continue;

                var row = RowAt(used);
                if (!row) return used;

                var glyph = glyphSet ? glyphSet.For(control.Control) : null;

                RecordBinding(used, control.Control);
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

        int BuildAbilityRows(GameModes mode, VesselClassType vesselClass, SO_Vessel vessel, int used)
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

            var shown = controlsLibrary ? controlsLibrary.AbilitiesFor(mode) : null;

            foreach (var element in DisplayOrder)
            {
                // A mode may narrow the hull's four to the ones that matter in IT - Skim Race and
                // Joust are the same vessel, so without this both cards say exactly the same thing.
                // An empty (or absent) filter means all four, which is the right default: they are
                // the vessel's abilities and the vessel is what you fly.
                if (shown is { Count: > 0 } && !shown.Contains(element)) continue;

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

                RecordBinding(used, binding);

                // The GLYPH says which button, the ICON says which ability, the NAME names it.
                // Three marks and no sentence: "Press RT to activate Boost Ring" spends a line
                // restating the glyph beside it, and the authored AbilityDescription is a DESIGN
                // NOTE - several hundred characters of mechanism, written for engineers - which
                // buried the row it belonged to and dragged in prose about other modes entirely.
                row.Bind(element,
                         entry.AbilityLabel,
                         string.Empty,
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

        void RecordBinding(int index, HintBinding binding)
        {
            while (_rowBindings.Count <= index) _rowBindings.Add(HintBinding.None);
            _rowBindings[index] = binding;
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
            DriveLivePresses();

            if (_sweepRows.Count == 0) return;

            // Unscaled: the menu can hold timeScale at 0 while this panel is open.
            _phase += Time.unscaledDeltaTime / Mathf.Max(0.5f, rowDwellSeconds);
            if (_phase >= _sweepRows.Count) _phase -= _sweepRows.Count;

            int slot = Mathf.Clamp((int)_phase, 0, _sweepRows.Count - 1);
            float within = _phase - slot;                  // 0..1 through this row's turn

            for (int i = 0; i < _sweepRows.Count; i++)
            {
                var row = _rows[_sweepRows[i]];
                if (row) row.SetSweep(SweepWeight(i));
            }

            DriveBeat(slot, within);
        }

        /// <summary>
        /// While a preview is LIVE, pressing an ability's physical control lights its row - the
        /// block answers the button in real time, so "which row was that?" never needs asking.
        ///
        /// <para>Polled from the DEVICE, not plumbed through the vessel: the rows are labelled
        /// with physical controls (that is what a glyph is), so the honest signal is the physical
        /// control's own state - and it works identically for an ability the vessel consumed,
        /// buffered, or ignored. Gated on the preview window holding the stick, the same gate
        /// every direct device poll honours while the pad belongs to the vessel.</para>
        /// </summary>
        void DriveLivePresses()
        {
            bool live = ModePreviewWindow.AnyHasFocus;
            for (int i = 0; i < _liveRows && i < _rows.Count && i < _rowBindings.Count; i++)
            {
                var row = _rows[i];
                if (!row) continue;
                row.SetLivePress(live && IsBindingHeld(_rowBindings[i]));
            }
        }

        static bool IsBindingHeld(HintBinding binding)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            var keys = UnityEngine.InputSystem.Keyboard.current;

            return binding switch
            {
                HintBinding.PadLeftTrigger   => pad != null && pad.leftTrigger.isPressed,
                HintBinding.PadRightTrigger  => pad != null && pad.rightTrigger.isPressed,
                HintBinding.PadLeftShoulder  => pad != null && pad.leftShoulder.isPressed,
                HintBinding.PadRightShoulder => pad != null && pad.rightShoulder.isPressed,
                HintBinding.PadButtonSouth   => pad != null && pad.buttonSouth.isPressed,
                HintBinding.PadButtonNorth   => pad != null && pad.buttonNorth.isPressed,
                HintBinding.PadButtonEast    => pad != null && pad.buttonEast.isPressed,
                HintBinding.PadButtonWest    => pad != null && pad.buttonWest.isPressed,
                HintBinding.KeyLeftShift     => keys != null && keys.leftShiftKey.isPressed,
                HintBinding.KeyRightShift    => keys != null && keys.rightShiftKey.isPressed,
                HintBinding.KeySpace         => keys != null && keys.spaceKey.isPressed,
                HintBinding.KeyTab           => keys != null && keys.tabKey.isPressed,
                HintBinding.KeyQ             => keys != null && keys.qKey.isPressed,
                HintBinding.KeyE             => keys != null && keys.eKey.isPressed,
                HintBinding.KeyF             => keys != null && keys.fKey.isPressed,
                _ => false,
            };
        }

        /// <summary>
        /// The in-game grammar, replayed on whichever row's turn it is: the press flash at the top
        /// of the turn, the recharge veil sweeping off the icon, then the ready flash the moment it
        /// clears. Exactly the three beats <c>AbilityLockupView</c> plays on a real ability, with
        /// the same colours out of the same style asset.
        /// </summary>
        void DriveBeat(int slot, float within)
        {
            var row = _rows[_sweepRows[slot]];
            if (!row) return;

            if (slot != _lastBeatRow)
            {
                // Entering a row: clear the previous one so a turn cut short never leaves a veil
                // parked over an icon.
                if (_lastBeatRow >= 0 && _lastBeatRow < _sweepRows.Count)
                {
                    var previous = _rows[_sweepRows[_lastBeatRow]];
                    if (previous)
                    {
                        previous.SetCooldown(0f);
                        previous.SetFlash(Color.clear);
                    }
                }
                _lastBeatRow = slot;

                // EVERY row flashes on its turn - a flight row is a control you press too, and a
                // row that only dims and brightens reads as disabled next to one that flashes.
                // Only the RECHARGE is conditional, because only an ability has one.
                row.SetFlash(row.PressFlashColor);
            }

            if (!row.DemonstratesCooldown)
            {
                row.SetFlash(FadeToClear(row.PressFlashColor, within * 2f));
                return;
            }

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
            distance = Mathf.Min(distance, _sweepRows.Count - distance);   // wrap
            float ramp = Mathf.Max(0.01f, sweepRampFraction * 2f);
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / ramp));
        }

        void HandleDeviceSetChanged(InputDeviceIconSetSwitcher.IconSet _)
        {
            // The chip and the sentence are the point of the row, so a device change re-derives
            // every one of them rather than leaving pad art in front of a keyboard player.
            if (UsingKeyboard() == _keyboardWhenBound) return;
            Show(_boundVessel, _boundVesselAsset, _boundMode);
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

        void EnsureControlsLibrary()
        {
            if (!controlsLibrary)
                controlsLibrary = Resources.Load<ModeControlsLibrarySO>(ModeControlsLibrarySO.ResourcePath);
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
