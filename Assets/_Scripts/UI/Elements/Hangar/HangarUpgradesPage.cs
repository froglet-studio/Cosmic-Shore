using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's UPGRADES tab (rev 4, Docs/MENU_REDESIGN_B.md section 41): the vessel's four elements,
    /// each with its level pips, the ability it scales, its level-5 upgrade and an UPGRADE button with the
    /// crystal price of the next level (the owner, 2026-10-17: "there should be a button called upgrade and
    /// there should be an animation when you upgrade"; price 100 x the next level).
    ///
    /// <para><b>Look-only</b> by the owner's decision: a bought level shapes the hull on the stage (its
    /// element blend shapes, the same weights a match would use) and nothing in a match reads it. The
    /// levels are <see cref="HangarUpgradeStore"/>'s; the crystals come out of the player's real wallet
    /// (<see cref="PlayerDataService.TrySpendCrystals"/>).</para>
    ///
    /// <para>The upgrade moment: crystals fly from the button to the hull, then the next pip flashes white
    /// and settles in the element's colour, the hull morphs to the new level and swells, a ring bursts and
    /// "LEVEL N" names what grew (<see cref="HangarStageFx"/>, <see cref="HangarVesselStage.Pulse"/>).</para>
    /// </summary>
    public class HangarUpgradesPage : MonoBehaviour, IHangarTabPage
    {
        [System.Serializable]
        public class Row
        {
            public Element element = Element.Charge;
            public KitButton button;
            public TMP_Text titleText;
            public TMP_Text lineText;
            public TMP_Text upgradeText;
            public TMP_Text levelText;
            [Tooltip("The ten level pips, lowest first.")]
            public Image[] pips = new Image[10];
            public Image elementGlyph;
            public Image glow;
            [Tooltip("The breathing element glow behind the glyph.")]
            public KitElementHalo halo;
            [Tooltip("UPGRADE - buys the next level.")]
            public KitButton upgradeButton;
            public TMP_Text upgradeButtonText;
            [HideInInspector] public int level;
            [HideInInspector] public bool open;
        }

        [Header("Rows (Charge, Mass, Space, Time)")]
        [SerializeField] private Row[] rows = new Row[4];

        [Header("Header")]
        [SerializeField] private TMP_Text headerLineText;

        [Tooltip("What an upgrade row's glyph, halo and earned pips draw in: gold, because an upgrade is spent crystals. Docs/UI_COLOR_RULES.md: an element never has a fixed colour in the UI - its glyph's shape says which it is.")]
        [SerializeField] private CosmicShore.UI.UIColorToken elementTint = CosmicShore.UI.UIColorToken.Purchase;

        [Header("Price")]
        [Tooltip("Crystals per level: the next level costs this x that level (the owner: 100).")]
        [SerializeField, Min(1)] private int crystalsPerLevel = 100;

        [Header("Look")]
        [Tooltip("Pip colour while off.")]
        [SerializeField] private Color pipOff = new(0.13f, 0.15f, 0.27f, 1f);
        [Tooltip("Seconds the new pip flashes white before settling in the element's colour.")]
        [SerializeField, Min(0.05f)] private float pipFlashSeconds = 0.45f;
        [Tooltip("Seconds the hull takes to morph to the new level.")]
        [SerializeField, Min(0.05f)] private float morphSeconds = 0.5f;

        [Header("Words")]
        [SerializeField] private string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [Tooltip("Header line.")]
        [SerializeField] private string defaultLine = "Spend crystals to raise an element. The hull changes as it grows.";
        [Tooltip("Header line for a vessel with no element shapes.")]
        [SerializeField] private string noMorphLine = "This hull keeps its shape at every level.";
        [Tooltip("Row title; {element} and {ability} are filled in.")]
        [SerializeField] private string titleTemplate = "{element} - {ability}";
        [Tooltip("Row line under the title; {level} is filled in.")]
        [SerializeField] private string levelTemplate = "Level {level} of 10";
        [Tooltip("The level-5 upgrade; {upgrade} is filled in.")]
        [SerializeField] private string upgradeTemplate = "L5 {upgrade}";
        [Tooltip("Title of a slot with no ability yet.")]
        [SerializeField] private string openSlotTitle = "Open slot";
        [Tooltip("The UPGRADE button; {cost} is filled in.")]
        [SerializeField] private string buyTemplate = "UPGRADE  {cost}";
        [Tooltip("The button once the element is at level 10.")]
        [SerializeField] private string maxedWord = "MAX";
        [Tooltip("The banner's title after an upgrade; {level} is filled in.")]
        [SerializeField] private string bannerTemplate = "LEVEL {level}";
        [Tooltip("The banner's line; {ability} is filled in. Empty shows nothing.")]
        [SerializeField] private string bannerLineTemplate = "{ability} grows";

        SO_Vessel _vessel;
        HangarVesselStage _stage;
        ElementalAbilityMapSO _map;
        Row _flashRow;
        float _flashT;           // seconds since the crystals landed (negative while they fly)
        bool _animating, _landed;
        int _flashPip, _pendingFrom;

        void Awake()
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                var r = row;
                if (r.upgradeButton) r.upgradeButton.onClick.AddListener(() => Upgrade(r));
                if (r.button) r.button.onClick.AddListener(() => Focus(r));
            }
        }

        void OnEnable() => PlayerDataService.OnCrystalBalanceChanged += HandleBalance;
        void OnDisable() => PlayerDataService.OnCrystalBalanceChanged -= HandleBalance;

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
            _map = vessel ? ElementalAbilityMapSO.LoadFor(vessel.Class) : null;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                var entry = _map ? _map.GetEntry(row.element) : null;
                row.open = entry == null || string.IsNullOrEmpty(entry.AbilityLabel);
                row.level = vessel ? HangarUpgradeStore.GetLevel(vessel.Class, row.element) : 0;
                if (row.titleText)
                    row.titleText.text = Fill(titleTemplate, ("element", ElementName(i, row)), ("ability", row.open ? openSlotTitle : entry.AbilityLabel));
                if (row.upgradeText)
                    row.upgradeText.text = row.open || string.IsNullOrEmpty(entry.UpgradeLabel) ? string.Empty
                        : Fill(upgradeTemplate, ("upgrade", entry.UpgradeLabel));
                if (row.elementGlyph) row.elementGlyph.color = Colour(row.element);
                if (row.halo) row.halo.SetColour(Colour(row.element));
            }
            _animating = false;
            _flashRow = null;
            ApplyOwnedForm();
            Redraw(null);
        }

        /// <summary>Every visit shows the hull in the form the player bought.</summary>
        public void SetActivePage(bool active)
        {
            if (active) ApplyOwnedForm();
        }

        void ApplyOwnedForm()
        {
            if (!_stage) return;
            _stage.ResetMorphs();
            foreach (var row in rows)
                if (row != null && row.level > 0) _stage.SetElementLevel(row.element, row.level);
        }

        void Focus(Row row)
        {
            if (row.halo) row.halo.Flash();
            Redraw(row);
        }

        void Upgrade(Row row)
        {
            if (!_vessel || row.open || row.level >= HangarUpgradeStore.MaxLevel) return;
            int cost = HangarUpgradeStore.CostOfNext(row.level, crystalsPerLevel);
            var wallet = PlayerDataService.Instance;
            // With the server wallet on, the server sells the level and records it (fair play T3).
            if (!wallet || !wallet.TryBuyHangarLevel((int)_vessel.Class, (int)row.element, cost)) { Redraw(row); return; }

            int from = row.level;
            row.level = from + 1;
            HangarUpgradeStore.SetLevel(_vessel.Class, row.element, row.level);

            // The moment: crystals fly to the hull; when they land the pip flashes, the hull morphs and
            // swells, the ring bursts and the banner names the new level.
            var fx = _stage ? _stage.Fx : null;
            if (fx && row.upgradeButton) fx.FlyCrystals(row.upgradeButton.transform.position);
            _flashRow = row;
            _flashPip = row.level - 1;
            _pendingFrom = from;
            _flashT = fx ? -fx.FlySeconds : 0f;       // counts up to 0 while the crystals fly
            _landed = false;
            _animating = true;
            Redraw(row, holdPip: _flashPip);
        }

        void Update()
        {
            if (!_animating) return;
            if (_flashRow == null) { _animating = false; return; }
            _flashT += Time.unscaledDeltaTime;
            Color c = Colour(_flashRow.element);

            if (!_landed && _flashT >= 0f) { _landed = true; Land(_flashRow, c); }
            if (_flashT < 0f) return;

            var pip = _flashPip >= 0 && _flashPip < _flashRow.pips.Length ? _flashRow.pips[_flashPip] : null;
            float k = _flashT / pipFlashSeconds;
            if (pip) pip.color = k < 0.35f ? Color.Lerp(pipOff, Color.white, k / 0.35f) : Color.Lerp(Color.white, c, (k - 0.35f) / 0.65f);
            if (k >= 1f)
            {
                if (pip) pip.color = c;
                _flashRow = null;
                _animating = false;
            }
        }

        void Land(Row row, Color c)
        {
            if (row.halo) row.halo.Flash();
            if (_stage)
            {
                if (_stage.HasMorphs) _stage.AnimateElementLevel(row.element, _pendingFrom, row.level, morphSeconds);
                _stage.Pulse(c);
            }
            var fx = _stage ? _stage.Fx : null;
            if (fx)
            {
                fx.Ring(c);
                int i = System.Array.IndexOf(rows, row);
                var entry = _map ? _map.GetEntry(row.element) : null;
                string line = entry == null || string.IsNullOrEmpty(bannerLineTemplate) ? string.Empty
                    : Fill(bannerLineTemplate, ("ability", entry.AbilityLabel));
                fx.ShowBanner(ElementName(i, row), Fill(bannerTemplate, ("level", row.level.ToString())), line, c);
            }
        }

        void HandleBalance(int balance) => Redraw(null);

        void Redraw(Row focused, int holdPip = -1)
        {
            int balance = PlayerDataService.Instance ? PlayerDataService.Instance.GetCrystalBalance() : 0;
            foreach (var row in rows)
            {
                if (row == null) continue;
                Color c = Colour(row.element);
                for (int p = 0; p < row.pips.Length; p++)
                {
                    if (!row.pips[p]) continue;
                    bool held = row == _flashRow && p == holdPip;
                    row.pips[p].color = p < row.level && !held ? c : pipOff;
                }
                if (row.levelText) row.levelText.text = row.level.ToString();
                if (row.lineText) row.lineText.text = Fill(levelTemplate, ("level", row.level.ToString()));
                if (row.glow) row.glow.color = new Color(c.r, c.g, c.b, row == focused ? 0.9f : 0f);

                bool maxed = row.level >= HangarUpgradeStore.MaxLevel;
                int cost = HangarUpgradeStore.CostOfNext(row.level, crystalsPerLevel);
                if (row.upgradeButtonText)
                    row.upgradeButtonText.text = maxed ? maxedWord : Fill(buyTemplate, ("cost", cost.ToString()));
                if (row.upgradeButton) row.upgradeButton.interactable = !row.open && !maxed && balance >= cost;
            }
            if (!headerLineText) return;
            headerLineText.text = _stage && !_stage.HasMorphs ? noMorphLine : defaultLine;
        }

        string ElementName(int i, Row row) => i >= 0 && i < elementNames.Length ? elementNames[i] : row.element.ToString();

        Color Colour(Element e) => CosmicShore.UI.UITheme.Resolve(null, elementTint);

        static string Fill(string template, params (string key, string value)[] tokens)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            foreach (var (k, v) in tokens) template = template.Replace("{" + k + "}", v);
            return template;
        }
    }
}
