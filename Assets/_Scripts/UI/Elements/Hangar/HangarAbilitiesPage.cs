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
    /// names it (LT / RT / A / B on a pad, LSHIFT / SPACE on a keyboard, nothing on touch). When the player presses a
    /// row (click, tap or A) the 3D vessel on the stage PERFORMS that ability (in the neutral tint: elements carry no colour in the UI,
    /// Docs/UI_COLOR_RULES.md) and the row lights. The
    /// move is the ability's own kind (shot, boost, build, sense...), read from its name and description by
    /// <see cref="HangarAbilityKinds"/> (Docs/MENU_REDESIGN_B.md section 46).
    ///
    /// <para><b>A press plays it, and nothing else does</b> (Docs/MENU_REDESIGN_B.md section 84; Shombith, 2026-10-26:
    /// "Abilities when I click them then only the vessel should perform"). Each row is a button: a click, a tap or A on
    /// the focused row performs that ability. Nothing plays on its own any more (the AI demo of section 66 is gone), and
    /// the game's own controls still do nothing here - LT / RT page the screens, LB / RB step the tabs (section 63:
    /// polling RT fired Pulsefire while it paged to the Ark).</para>
    ///
    /// <para>The rows read the shipped <see cref="ElementalAbilityMapSO"/> (name, description, input)
    /// and the vessel's own HUD icon art; the control name comes from the fleet's one glyph table
    /// (<see cref="ControlGlyphSetSO"/>) through <see cref="InputHintBindingMap"/>, the same path the
    /// in-match keycaps take, so the Hangar can never name a different button than the HUD does.</para>
    ///
    /// <para><b>Light weight</b> (the owner, 2026-10-19: "it should be low cost, very low cost"): a press sinks the keycap,
    /// lights the row and the stage's display copy performs the move - no world, no hull swap, no camera loan.</para>
    /// </summary>
    public class HangarAbilitiesPage : MonoBehaviour, IHangarTabPage
    {
        [System.Serializable]
        public class Row
        {
            public Element element = Element.Charge;
            [Tooltip("The row's button: a press performs the ability on the stage (section 84); an open slot draws it disabled.")]
            public KitButton button;
            public Image icon;
            public TMP_Text nameText;
            public TMP_Text elementText;
            public TMP_Text descriptionText;
            public Image elementGlyph;
            [Tooltip("Rev 24: the element as the HUD's flower at the level the player bought on this vessel (replaces the glyph).")]
            public KitElementFlower flower;
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
            [Tooltip("Section 90 (Shombith 2026-10-27: \"in the same info tab we have both the 2D icons and the 3D view of " +
                     "them\"): the element's crystal in 3D, a 4x4 flipbook rendered from the crystal labs. Still on its first " +
                     "frame until the row is played; then it runs its loop once.")]
            public RawImage crystal;
            [Tooltip("Frames a second of this crystal's flipbook (16 frames are one loop of its effect).")]
            public float crystalFps = 8f;
            [HideInInspector] public HintBinding pad, key;
            [HideInInspector] public float lit;
            [HideInInspector] public float pressed;
            [HideInInspector] public HangarAbilityKind kind;
            [HideInInspector] public bool open;
            [HideInInspector] public float spin = -1f, play = -1f;
            [HideInInspector] public int frame = -1;
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
        [Tooltip("What an ability row's keycap border, halo and the stage's demo draw in. Docs/UI_COLOR_RULES.md: an element never has a fixed colour in the UI - its glyph's shape says which it is.")]
        [SerializeField] private CosmicShore.UI.UIColorToken elementTint = CosmicShore.UI.UIColorToken.NeutralLightest;

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
        [Tooltip("Seconds the 2D flower takes to turn one petal and settle when its row plays (rotate and scale).")]
        [SerializeField, Min(0.05f)] private float spinSeconds = 0.6f;
        [Tooltip("How far the 2D flower grows at the top of its turn (0.18 = 18 %).")]
        [SerializeField, Range(0f, 0.5f)] private float spinScale = 0.18f;

        [Header("Stage moves")]
        [Tooltip("Each ability's stage move is read from its name and description (HangarAbilityKinds). A row here " +
                 "overrides that reading for one vessel's element.")]
        [SerializeField] private KindOverride[] kindOverrides = new KindOverride[0];

        [Header("Press")]
        [Tooltip("Seconds a pressed keycap stays sunk.")]
        [SerializeField, Min(0.05f)] private float keyPressSeconds = 0.35f;
        [Tooltip("Keycap scale at the bottom of a press.")]
        [SerializeField, Range(0.5f, 1f)] private float keyPressScale = 0.84f;

        SO_Vessel _vessel;
        HangarVesselStage _stage;
        bool _active;
        bool _keyboard;
        bool _wired;

        void Awake()
        {
            if (!glyphSet) glyphSet = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            WireRows();
        }

        // Each row's button performs its own ability; wired once, the row read at press time.
        void WireRows()
        {
            if (_wired) return;
            _wired = true;
            foreach (var row in rows)
            {
                if (row?.button == null) continue;
                var r = row;
                row.button.onClick.AddListener(() => { if (_active) Fire(r); });
            }
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
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
                if (row.flower)
                    row.flower.Set(row.element, vessel ? HangarUpgradeStore.GetLevel(vessel.Class, row.element) : HangarUpgradeStore.StartLevel);
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
                row.spin = row.play = -1f;
                SetCrystalFrame(row, 0);
                if (row.flower) { row.flower.transform.localEulerAngles = Vector3.zero; row.flower.transform.localScale = Vector3.one; }
                if (row.keycapGroup) row.keycapGroup.transform.localScale = Vector3.one;
                row.open = open;
                if (row.glow) row.glow.color = new Color(c.r, c.g, c.b, 0f);
            }
            RefreshKeycaps();
        }

        public void SetActivePage(bool active) => _active = active;

        // A level bought on UPGRADES shows on these flowers too.
        void OnEnable() => HangarUpgradeStore.LevelChanged += HandleUpgrade;
        void OnDisable() => HangarUpgradeStore.LevelChanged -= HandleUpgrade;

        void HandleUpgrade(VesselClassType vessel, Element element, int level)
        {
            if (!_vessel || vessel != _vessel.Class) return;
            foreach (var row in rows)
                if (row != null && row.element == element && row.flower) row.flower.SetLevel(level);
        }

        void Update()
        {
            if (HangarControlInput.TrackDevice() || _keyboard != HangarControlInput.LastWasKeyboard) RefreshKeycaps();
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                if (row.pressed > 0f)
                {
                    row.pressed = Mathf.Max(0f, row.pressed - dt / keyPressSeconds);
                    if (row.keycapGroup)
                        row.keycapGroup.transform.localScale = Vector3.one * Mathf.Lerp(1f, keyPressScale, Mathf.Sin(row.pressed * Mathf.PI));
                }
                Animate(row, dt);
                if (row.lit <= 0f) continue;
                row.lit = Mathf.Max(0f, row.lit - dt / glowSeconds);
                if (row.glow)
                {
                    var c = row.glow.color;
                    row.glow.color = new Color(c.r, c.g, c.b, row.lit);
                }
            }
        }

        const int CrystalFrames = 16, CrystalColumns = 4;

        /// <summary>The played row's 2D flower turns one petal (72 degrees, so it lands looking as it started) and swells;
        /// its 3D crystal runs its loop once and rests on its first frame.</summary>
        void Animate(Row row, float dt)
        {
            if (row.spin >= 0f)
            {
                row.spin += dt / spinSeconds;
                float k = Mathf.Clamp01(row.spin);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);    // OutCubic
                if (row.flower)
                {
                    var t = row.flower.transform;
                    t.localEulerAngles = new Vector3(0f, 0f, -72f * e);
                    t.localScale = Vector3.one * (1f + spinScale * Mathf.Sin(Mathf.PI * k));
                    if (k >= 1f) { t.localEulerAngles = Vector3.zero; t.localScale = Vector3.one; }
                }
                if (k >= 1f) row.spin = -1f;
            }
            if (row.play >= 0f)
            {
                row.play += dt;
                int f = Mathf.FloorToInt(row.play * Mathf.Max(1f, row.crystalFps));
                if (f >= CrystalFrames) { row.play = -1f; f = 0; }
                SetCrystalFrame(row, f);
            }
        }

        static void SetCrystalFrame(Row row, int f)
        {
            if (!row.crystal || f == row.frame) return;
            row.frame = f;
            const float size = 1f / CrystalColumns;
            row.crystal.uvRect = new Rect(f % CrystalColumns * size, 1f - (f / CrystalColumns + 1) * size, size, size);
        }

        void Fire(Row row)
        {
            if (row == null || row.open || (row.button && !row.button.interactable)) return;
            row.lit = 1f;
            row.pressed = 1f;
            row.spin = 0f;
            row.play = 0f;
            if (row.halo) row.halo.Flash();
            if (_stage) _stage.Perform(Colour(row.element), row.kind);
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

        Color Colour(Element e) => CosmicShore.UI.UITheme.Resolve(null, elementTint);

        /// <summary>The ability's first sentence - the row is a summary; the full text is the map's.</summary>
        static string FirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int dot = text.IndexOf(". ", System.StringComparison.Ordinal);
            return dot > 0 ? text.Substring(0, dot + 1) : text;
        }
    }
}
