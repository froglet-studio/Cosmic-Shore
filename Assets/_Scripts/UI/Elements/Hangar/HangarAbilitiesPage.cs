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
    /// names it (LT / RT / A / B on a pad, LSHIFT / SPACE on a keyboard, nothing on touch). Pressing that
    /// control - or tapping the row - makes the 3D vessel on the stage PERFORM it in the element's
    /// colour, and lights the row. The move is the ability's own kind (shot, boost, build, sense...), read
    /// from its name and description by <see cref="HangarAbilityKinds"/> (Docs/MENU_REDESIGN_B.md section 46).
    ///
    /// <para>The rows read the shipped <see cref="ElementalAbilityMapSO"/> (name, description, input)
    /// and the vessel's own HUD icon art; the control name comes from the fleet's one glyph table
    /// (<see cref="ControlGlyphSetSO"/>) through <see cref="InputHintBindingMap"/>, the same path the
    /// in-match keycaps take, so the Hangar can never name a different button than the HUD does.</para>
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

        [Header("Try it")]
        [Tooltip("TRY: the selected vessel flown for real in a bare arena (the stage's move is a stand-in).")]
        [SerializeField] private HangarAbilityPreview tryPreview;

        SO_Vessel _vessel;
        HangarVesselStage _stage;
        bool _active;
        bool _keyboard;

        readonly System.Collections.Generic.List<HangarAbilityPreview.ShowcaseAbility> _showcase = new();

        void OnEnable() { if (tryPreview) tryPreview.ShowcasePlayed += LightRow; }
        void OnDisable() { if (tryPreview) tryPreview.ShowcasePlayed -= LightRow; }

        void Awake()
        {
            if (!glyphSet) glyphSet = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            if (!lockupStyle) lockupStyle = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            foreach (var row in rows)
            {
                if (row?.button == null) continue;
                var r = row;
                r.button.onClick.AddListener(() => Fire(r));
            }
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
            if (tryPreview) tryPreview.SetVessel(vessel);
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
                row.open = open;
                if (row.glow) row.glow.color = new Color(c.r, c.g, c.b, 0f);
            }
            _showcase.Clear();
            foreach (var row in rows)
            {
                if (row == null || row.open || map == null) continue;
                var entry = map.GetEntry(row.element);
                bool presses = row.pad != HintBinding.None || row.key != HintBinding.None;
                _showcase.Add(new HangarAbilityPreview.ShowcaseAbility(row.element, entry.Input, presses));
            }
            if (tryPreview) tryPreview.SetShowcaseAbilities(_showcase);
            RefreshKeycaps();
        }

        public void SetActivePage(bool active)
        {
            _active = active;
            if (!active && tryPreview) tryPreview.End();    // TRY never outlives its tab
        }

        void Update()
        {
            if (HangarControlInput.TrackDevice() || _keyboard != HangarControlInput.LastWasKeyboard) RefreshKeycaps();
            float dt = Time.unscaledDeltaTime;
            foreach (var row in rows)
            {
                if (row == null) continue;
                if (_active && (HangarControlInput.WasPressed(row.pad) || HangarControlInput.WasPressed(row.key))) Fire(row);
                if (row.lit <= 0f) continue;
                row.lit = Mathf.Max(0f, row.lit - dt / glowSeconds);
                if (row.glow)
                {
                    var c = row.glow.color;
                    row.glow.color = new Color(c.r, c.g, c.b, row.lit);
                }
            }
        }

        void Fire(Row row)
        {
            if (row.button && !row.button.interactable) return;
            row.lit = 1f;
            if (row.halo) row.halo.Flash();
            // While TRY flies, the real ability is firing in the preview; the row only lights.
            if (_stage && !(tryPreview && tryPreview.IsRunning)) _stage.Perform(Colour(row.element), row.kind);
            // The real thing: the showcase stands the arena (or jumps to this ability) and the vessel performs it.
            if (tryPreview && (!tryPreview.IsRunning || tryPreview.IsShowcase)) tryPreview.Showcase(row.element);
        }

        void LightRow(Element element)
        {
            foreach (var row in rows)
            {
                if (row == null || row.element != element) continue;
                row.lit = 1f;
                if (row.halo) row.halo.Flash();
            }
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
