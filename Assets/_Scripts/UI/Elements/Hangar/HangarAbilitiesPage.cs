using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's ABILITIES tab (rev 3): every ability of the selected vessel - its icon, name,
    /// element and what it does - with the control that fires it written as the player's own device
    /// names it (LT / RT / A / B on a pad, LSHIFT / SPACE on a keyboard, nothing on touch). Pressing the
    /// row (A on a pad, a click, a tap) or that ability's keyboard key makes the 3D vessel on the stage
    /// PERFORM it in the element's colour, and lights the row. The move is the ability's own kind (shot,
    /// boost, build, sense...), read from its name and description by <see cref="HangarAbilityKinds"/>
    /// (Docs/MENU_REDESIGN_B.md section 46).
    ///
    /// <para><b>The pad belongs to the menu here</b> (section 63). The Hangar is a screen, and the pad controls
    /// the abilities use most have menu jobs on it: LT / RT page the screens from the tab bar (section 64), LB / RB
    /// step the tabs and A presses whatever the pad has selected. Polling them fired an ability on a press meant for the menu (RT on a Sparrow
    /// paged to the Ark AND fired Pulsefire), so a pad keycap is a label of the in-match control and is never
    /// polled - every pad control alike, so the page does not depend on which ones the menu leaves free today.
    /// The keyboard keys the abilities use have no menu job, so they still play their ability.</para>
    ///
    /// <para>The rows read the shipped <see cref="ElementalAbilityMapSO"/> (name, description, input)
    /// and the vessel's own HUD icon art; the control name comes from the fleet's one glyph table
    /// (<see cref="ControlGlyphSetSO"/>) through <see cref="InputHintBindingMap"/>, the same path the
    /// in-match keycaps take, so the Hangar can never name a different button than the HUD does.</para>
    ///
    /// <para><b>The AI demo</b> (the owner, 2026-10-19: "the preview should only be played by AI and it should be low
    /// cost, very low cost ... just light weight to show the controls"). While the tab is on screen the AI presses each
    /// ability's control in turn: the keycap sinks, the row lights and the stage's display copy performs the move. It
    /// costs what the stage already costs - no world, no hull swap, no camera loan. It replaced TRY and the flown
    /// showcase, which stood a whole Barren cell, swapped the menu hull and borrowed the gameplay camera. A press of
    /// the player's own pauses the demo for a moment and it carries on from the next ability.</para>
    /// </summary>
    public class HangarAbilitiesPage : MonoBehaviour, IHangarTabPage
    {
        [System.Serializable]
        public class Row
        {
            public Element element = Element.Charge;
            public KitButton button;
            public Image icon;
            public TMP_Text nameText;
            public TMP_Text elementText;
            public TMP_Text descriptionText;
            public Image elementGlyph;
            [Tooltip("The keycap (hidden for a passive ability or on touch).")]
            public CanvasGroup keycapGroup;
            public TMP_Text keycapText;
            public Image keycapBorder;
            [Tooltip("The row's edge glow while lit.")]
            public Image glow;
            [Tooltip("The hex behind the ability icon, tinted in the element's colour.")]
            public Image hex;
            [Tooltip("The breathing element glow behind the hex.")]
            public KitElementHalo halo;
            [HideInInspector] public HintBinding pad, key;
            [HideInInspector] public float lit;
            [HideInInspector] public float pressed;
            [HideInInspector] public HangarAbilityKind kind;
            [HideInInspector] public bool open;
        }

        /// <summary>One vessel's element shown as another kind than its words read as.</summary>
        [System.Serializable]
        public struct KindOverride
        {
            public VesselClassType vessel;
            public Element element;
            public HangarAbilityKind kind;
        }

        [Header("Rows (Charge, Mass, Space, Time)")]
        [SerializeField] private Row[] rows = new Row[4];

        [Header("Data")]
        [SerializeField] private VesselPrefabContainer prefabs;
        [Tooltip("Loaded from Resources/ControlGlyphSet when empty.")]
        [SerializeField] private ControlGlyphSetSO glyphSet;
        [Tooltip("Element colours (AbilityLockupStyle - the same ones the in-match row wears).")]
        [SerializeField] private AbilityLockupStyleSO lockupStyle;

        [Header("Words")]
        [Tooltip("Element names, in the rows' order.")]
        [SerializeField] private string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [Tooltip("Name shown on a slot this vessel has no ability for yet.")]
        [SerializeField] private string openSlotName = "Open slot";
        [Tooltip("Line under an open slot.")]
        [SerializeField] private string openSlotLine = "Not designed yet.";
        [Tooltip("Keycap word for an ability with no button (it fires on its own).")]
        [SerializeField] private string passiveWord = "Passive";

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float glowSeconds = 0.9f;

        [Header("Stage moves")]
        [Tooltip("Each ability's stage move is read from its name and description (HangarAbilityKinds). A row here " +
                 "overrides that reading for one vessel's element.")]
        [SerializeField] private KindOverride[] kindOverrides = new KindOverride[0];

        [Header("AI demo")]
        [Tooltip("Seconds between the AI's presses: each ability's control in turn, and the vessel performs it.")]
        [SerializeField, Min(0.5f)] private float demoStepSeconds = 2.4f;
        [Tooltip("Seconds before the first press after the tab opens or the vessel changes.")]
        [SerializeField, Min(0f)] private float demoLeadSeconds = 0.8f;
        [Tooltip("Seconds the AI waits after the player presses a control before it carries on.")]
        [SerializeField, Min(0f)] private float demoResumeSeconds = 4f;
        [Tooltip("Seconds a pressed keycap stays sunk.")]
        [SerializeField, Min(0.05f)] private float keyPressSeconds = 0.35f;
        [Tooltip("Keycap scale at the bottom of a press.")]
        [SerializeField, Range(0.5f, 1f)] private float keyPressScale = 0.84f;

        SO_Vessel _vessel;
        HangarVesselStage _stage;
        bool _active;
        bool _keyboard;
        float _demoClock;
        int _demoNext;

        void Awake()
        {
            if (!glyphSet) glyphSet = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            if (!lockupStyle) lockupStyle = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            foreach (var row in rows)
            {
                if (row?.button == null) continue;
                var r = row;
                int index = System.Array.IndexOf(rows, r);
                r.button.onClick.AddListener(() => PlayerFire(index));
            }
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
            _demoNext = 0;
            _demoClock = demoLeadSeconds;
            var map = vessel ? ElementalAbilityMapSO.LoadFor(vessel.Class) : null;
            VesselHUDView hud = null;
            if (vessel && prefabs && prefabs.TryGetShipPrefab(vessel.Class, out Transform prefab, reportMissing: false))
                hud = prefab.GetComponentInChildren<VesselHUDView>(true);

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                var entry = map ? map.GetEntry(row.element) : null;
                bool open = entry == null || string.IsNullOrEmpty(entry.AbilityLabel);
                Color c = Colour(row.element);

                if (row.nameText) row.nameText.text = open ? openSlotName : entry.AbilityLabel;
                if (row.descriptionText) row.descriptionText.text = open ? openSlotLine : FirstSentence(entry.AbilityDescription);
                if (row.elementText)
                {
                    row.elementText.text = i < elementNames.Length ? elementNames[i] : row.element.ToString();
                    row.elementText.color = c;
                }
                if (row.elementGlyph) row.elementGlyph.color = c;
                if (row.hex) row.hex.color = open ? new Color(1f, 1f, 1f, 0.08f) : new Color(c.r, c.g, c.b, 0.35f);
                if (row.halo) row.halo.SetColour(open ? Color.clear : c);

                Sprite art = null;
                if (!open && hud && hud.TryGetAbilityIcon(row.element, out var icon) && icon) art = icon.sprite;
                if (row.icon)
                {
                    row.icon.sprite = art;
                    row.icon.enabled = art;
                }
                if (row.button) row.button.interactable = !open;

                row.kind = open ? HangarAbilityKind.Passive : KindOf(vessel.Class, row.element, entry);
                row.pad = open ? HintBinding.None : InputHintBindingMap.BindingFor(entry.Input, keyboard: false);
                row.key = open ? HintBinding.None : InputHintBindingMap.BindingFor(entry.Input, keyboard: true);
                row.lit = 0f;
                row.pressed = 0f;
                if (row.keycapGroup) row.keycapGroup.transform.localScale = Vector3.one;
                row.open = open;
                if (row.glow) row.glow.color = new Color(c.r, c.g, c.b, 0f);
            }
            RefreshKeycaps();
        }

        public void SetActivePage(bool active)
        {
            if (active && !_active) _demoClock = demoLeadSeconds;
            _active = active;
        }

        void Update()
        {
            if (HangarControlInput.TrackDevice() || _keyboard != HangarControlInput.LastWasKeyboard) RefreshKeycaps();
            float dt = Time.unscaledDeltaTime;
            if (_active && _stage && _stage.Showing)
            {
                _demoClock -= dt;
                if (_demoClock <= 0f)
                {
                    _demoClock = demoStepSeconds;
                    DemoStep();
                }
            }
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                // Only the keyboard key: the pad's controls are the menu's (see the class summary); a pad
                // reaches the row through the EventSystem, A on the focused row.
                if (_active && HangarControlInput.WasPressed(row.key))
                    PlayerFire(i);
                if (row.pressed > 0f)
                {
                    row.pressed = Mathf.Max(0f, row.pressed - dt / keyPressSeconds);
                    if (row.keycapGroup)
                        row.keycapGroup.transform.localScale = Vector3.one * Mathf.Lerp(1f, keyPressScale, Mathf.Sin(row.pressed * Mathf.PI));
                }
                if (row.lit <= 0f) continue;
                row.lit = Mathf.Max(0f, row.lit - dt / glowSeconds);
                if (row.glow)
                {
                    var c = row.glow.color;
                    row.glow.color = new Color(c.r, c.g, c.b, row.lit);
                }
            }
        }

        /// <summary>A row tapped or its control pressed by the player: play it, then let the AI carry on after it.</summary>
        void PlayerFire(int index)
        {
            if (!Fire(rows[index], holdTurn: true)) return;
            _demoNext = index + 1;
            _demoClock = demoResumeSeconds;
        }

        /// <summary>The AI's press: the next ability this vessel has, round and round.</summary>
        void DemoStep()
        {
            for (int n = 0; n < rows.Length; n++)
            {
                int i = (_demoNext + n) % rows.Length;
                var row = rows[i];
                if (row == null || row.open) continue;
                _demoNext = i + 1;
                Fire(row, holdTurn: false);
                return;
            }
        }

        bool Fire(Row row, bool holdTurn)
        {
            if (row == null || row.open || (row.button && !row.button.interactable)) return false;
            row.lit = 1f;
            row.pressed = 1f;
            if (row.halo) row.halo.Flash();
            if (_stage) _stage.Perform(Colour(row.element), row.kind, holdTurn);
            return true;
        }

        HangarAbilityKind KindOf(VesselClassType vessel, Element element, ElementalAbilityEntry entry)
        {
            if (kindOverrides != null)
                foreach (var o in kindOverrides)
                    if (o.vessel == vessel && o.element == element) return o.kind;
            return HangarAbilityKinds.Classify(entry.AbilityLabel, entry.AbilityDescription);
        }

        void RefreshKeycaps()
        {
            _keyboard = HangarControlInput.LastWasKeyboard;
            bool hints = KitInputDevice.ShowsControlHints;
            foreach (var row in rows)
            {
                if (row == null) continue;
                var binding = _keyboard ? row.key : row.pad;
                bool passive = row.pad == HintBinding.None && row.key == HintBinding.None && row.button && row.button.interactable;
                var glyph = glyphSet && binding != HintBinding.None ? glyphSet.For(binding) : null;
                string word = glyph == null ? null : _keyboard ? glyph.keyboardLabel : glyph.padLabel;
                if (passive) word = passiveWord;
                bool show = hints && !string.IsNullOrEmpty(word);
                KitVisibility.SetShown(row.keycapGroup, show);
                if (row.keycapText) row.keycapText.text = show ? word : string.Empty;
                if (row.keycapBorder) row.keycapBorder.color = Colour(row.element);
            }
        }

        Color Colour(Element e) => lockupStyle ? lockupStyle.ColourFor(e) : Color.white;

        /// <summary>The ability's first sentence - the row is a summary; the full text is the map's.</summary>
        static string FirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int dot = text.IndexOf(". ", System.StringComparison.Ordinal);
            return dot > 0 ? text.Substring(0, dot + 1) : text;
        }
    }
}
