using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's UPGRADES part (OVERVIEW, between Abilities and Games). Rev 24 (Shombith, 2026-10-26: "Use the petals
    /// in game of the elements with the bars"; "In upgrades we do upgrade the elemental petals and they always start with
    /// 1 value"): the vessel's four elements as the HUD's own flowers down the right, each at the level the player has
    /// bought (<see cref="HangarUpgradeStore"/>, from 1), and for the focused element what it scales, the flower NOW and
    /// NEXT, its level-5 upgrade and an UPGRADE button with the crystal price of the next level (100 x that level).
    ///
    /// <para><b>Look-only</b> by the owner's decision (2026-10-17): a bought level shapes the hull on the stage (its
    /// element blend shapes) and the flowers on the arcade cards; nothing in a match reads it. The crystals come out of
    /// the player's real wallet (<see cref="PlayerDataService.TrySpendCrystals"/>).</para>
    ///
    /// <para>The upgrade moment: crystals fly from the button to the hull; when they land the flower pops to its new
    /// level, the hull morphs and swells, a ring bursts and "LEVEL N" names what grew (<see cref="HangarStageFx"/>,
    /// <see cref="HangarVesselStage.Pulse"/>).</para>
    /// </summary>
    public class HangarUpgradesPage : MonoBehaviour, IHangarTabPage
    {
        [System.Serializable]
        public class Row
        {
            public Element element = Element.Charge;
            [Tooltip("The element's tile in the column: pressing or moving onto it focuses the element.")]
            public KitButton button;
            [Tooltip("The element's flower at the bought level.")]
            public KitElementFlower flower;
            public TMP_Text levelText;
            [Tooltip("The focused element's frame, drawn in the selection blue (a real selection).")]
            public Image selected;
            [HideInInspector] public int level;
            [HideInInspector] public bool open;
        }

        [Header("Elements (Charge, Mass, Space, Time)")]
        [SerializeField] private Row[] rows = new Row[4];

        [Header("The focused element")]
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private TMP_Text abilityText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private KitElementFlower nowFlower;
        [SerializeField] private TMP_Text nowText;
        [Tooltip("The arrow and the NEXT flower; hidden at the last level.")]
        [SerializeField] private CanvasGroup nextGroup;
        [SerializeField] private KitElementFlower nextFlower;
        [SerializeField] private TMP_Text nextText;
        [Tooltip("The level-5 upgrade box; hidden when the element names none.")]
        [SerializeField] private CanvasGroup upgradeGroup;
        [SerializeField] private TMP_Text upgradeEyebrowText;
        [SerializeField] private TMP_Text upgradeTitleText;
        [SerializeField] private TMP_Text upgradeLineText;
        [Tooltip("UPGRADE - buys the focused element's next level.")]
        [SerializeField] private KitButton upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonText;

        [Header("Header")]
        [Tooltip("Optional line under the part's title.")]
        [SerializeField] private TMP_Text headerLineText;

        [Tooltip("What the upgrade moment's ring, banner and flying crystals draw in over the vessel preview: silver " +
                 "(Garrett flagged the gold there, 2026-10-27; Docs/VesselModels/COLOR.md: the preview stays in the neutral " +
                 "family, as the cards' class livery does). The UPGRADE button itself stays gold: that is the spend. An " +
                 "element never has a fixed colour in the UI - its petal's shape says which it is.")]
        [SerializeField] private UIColorToken elementTint = UIColorToken.NeutralLightest;

        [Header("Price")]
        [Tooltip("Crystals per level: the next level costs this x that level (the owner: 100).")]
        [SerializeField, Min(1)] private int crystalsPerLevel = 100;

        [Header("Look")]
        [Tooltip("How far a flower swells when its level lands.")]
        [SerializeField, Min(0f)] private float popScale = 0.28f;
        [Tooltip("Seconds the pop takes to settle.")]
        [SerializeField, Min(0.05f)] private float popSeconds = 0.45f;
        [Tooltip("Seconds the hull takes to morph to the new level.")]
        [SerializeField, Min(0.05f)] private float morphSeconds = 0.5f;

        [Header("Words")]
        [SerializeField] private string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [Tooltip("Header line.")]
        [SerializeField] private string defaultLine = "Spend OmniCrystals to raise an element. The hull changes as it grows.";
        [Tooltip("Header line for a vessel with no element shapes.")]
        [SerializeField] private string noMorphLine = "This hull keeps its shape at every level.";
        [Tooltip("The focused element's step; {level} and {next} are filled in.")]
        [SerializeField] private string stepTemplate = "LEVEL {level} TO {next}";
        [Tooltip("The step at the last level; {level} is filled in.")]
        [SerializeField] private string maxedStepTemplate = "LEVEL {level} - MAX";
        [Tooltip("Under the NOW flower; {level} is filled in.")]
        [SerializeField] private string nowTemplate = "NOW {level}";
        [Tooltip("Under the NEXT flower; {level} is filled in.")]
        [SerializeField] private string nextTemplate = "NEXT {level}";
        [Tooltip("The level-5 box's eyebrow while it is ahead; {level} is filled in.")]
        [SerializeField] private string upgradeAheadTemplate = "AT LEVEL {level}";
        [Tooltip("The level-5 box's eyebrow once it is reached; {level} is filled in.")]
        [SerializeField] private string upgradeReachedTemplate = "UNLOCKED AT LEVEL {level}";
        [Tooltip("The level the upgrade arrives at (ElementalAbilityEntry: the level-5 upgrade).")]
        [SerializeField, Min(1)] private int upgradeLevel = 5;
        [Tooltip("Title of a slot with no ability yet.")]
        [SerializeField] private string openSlotTitle = "Open slot";
        [Tooltip("Line of a slot with no ability yet.")]
        [SerializeField] private string openSlotLine = "Not designed yet.";
        [Tooltip("The UPGRADE button; {cost} is filled in.")]
        [SerializeField] private string buyTemplate = "UPGRADE  {cost}";
        [Tooltip("The button once the element is at the last level.")]
        [SerializeField] private string maxedWord = "MAX";
        [Tooltip("The banner's title after an upgrade; {level} is filled in.")]
        [SerializeField] private string bannerTemplate = "LEVEL {level}";
        [Tooltip("The banner's line; {ability} is filled in. Empty shows nothing.")]
        [SerializeField] private string bannerLineTemplate = "{ability} grows";

        SO_Vessel _vessel;
        HangarVesselStage _stage;
        ElementalAbilityMapSO _map;
        int _focus;
        bool _active;
        GameObject _lastSelected;

        // The upgrade moment: crystals in flight (negative), then the pop.
        Row _landing;
        float _t;
        bool _animating, _landed;
        int _from;
        Transform _pop;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                int index = i;
                if (rows[i].button) rows[i].button.onClick.AddListener(() => Focus(index));
            }
            if (upgradeButton) upgradeButton.onClick.AddListener(Upgrade);
        }

        void OnEnable() => PlayerDataService.OnCrystalBalanceChanged += HandleBalance;
        void OnDisable() => PlayerDataService.OnCrystalBalanceChanged -= HandleBalance;

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
            _map = vessel ? ElementalAbilityMapSO.LoadFor(vessel.Class) : null;
            int firstOpen = -1;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                var entry = _map ? _map.GetEntry(row.element) : null;
                row.open = entry == null || string.IsNullOrEmpty(entry.AbilityLabel);
                row.level = vessel ? HangarUpgradeStore.GetLevel(vessel.Class, row.element) : HangarUpgradeStore.StartLevel;
                if (!row.open && firstOpen < 0) firstOpen = i;
            }
            _focus = Mathf.Max(0, firstOpen);
            _animating = false;
            _landing = null;
            if (_pop) _pop.localScale = Vector3.one;
            ApplyOwnedForm();
            Redraw();
        }

        /// <summary>Every visit shows the hull in the form the player bought.</summary>
        public void SetActivePage(bool active)
        {
            _active = active;
            if (active) ApplyOwnedForm();
        }

        void ApplyOwnedForm()
        {
            if (!_stage) return;
            _stage.ResetMorphs();
            foreach (var row in rows)
                if (row != null && row.level > 0) _stage.SetElementLevel(row.element, row.level);
        }

        void Focus(int index)
        {
            if (index < 0 || index >= rows.Length || index == _focus) return;
            _focus = index;
            Redraw();
        }

        void Upgrade()
        {
            var row = Focused;
            if (!_vessel || row == null || row.open || row.level >= HangarUpgradeStore.MaxLevel || _animating) return;
            int cost = HangarUpgradeStore.CostOfNext(row.level, crystalsPerLevel);
            var wallet = PlayerDataService.Instance;
            if (!wallet || !wallet.TrySpendCrystals(cost, "hangar_upgrade")) { Redraw(); return; }

            _from = row.level;
            HangarUpgradeStore.SetLevel(_vessel.Class, row.element, row.level + 1);

            // The moment: crystals fly to the hull; when they land the flower takes its new level and pops, the hull
            // morphs and swells, the ring bursts and the banner names the new level.
            var fx = _stage ? _stage.Fx : null;
            if (fx && upgradeButton) fx.FlyCrystals(upgradeButton.transform.position);
            _landing = row;
            _t = fx ? -fx.FlySeconds : 0f;       // counts up to 0 while the crystals fly
            _landed = false;
            _animating = true;
            Redraw();
        }

        void Update()
        {
            // The pad moving onto an element focuses it (no press needed): one reference compare a frame while shown.
            if (_active && EventSystem.current)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != _lastSelected)
                {
                    _lastSelected = selected;
                    for (int i = 0; i < rows.Length; i++)
                        if (rows[i]?.button && rows[i].button.gameObject == selected) Focus(i);
                }
            }

            if (!_animating) return;
            if (_landing == null) { _animating = false; return; }
            _t += Time.unscaledDeltaTime;
            if (!_landed && _t >= 0f) { _landed = true; Land(_landing); }
            if (_t < 0f || !_pop) return;
            float k = Mathf.Clamp01(_t / popSeconds);
            _pop.localScale = Vector3.one * (1f + popScale * Mathf.Sin(k * Mathf.PI) * (1f - k));
            if (k >= 1f)
            {
                _pop.localScale = Vector3.one;
                _landing = null;
                _animating = false;
            }
        }

        void Land(Row row)
        {
            row.level = Mathf.Min(row.level + 1, HangarUpgradeStore.MaxLevel);
            Redraw();
            _pop = row.flower ? row.flower.transform : null;
            Color c = UITheme.Resolve(null, elementTint);
            if (_stage)
            {
                if (_stage.HasMorphs) _stage.AnimateElementLevel(row.element, _from, row.level, morphSeconds);
                _stage.Pulse(c);
            }
            var fx = _stage ? _stage.Fx : null;
            if (!fx) return;
            fx.Ring(c);
            var entry = _map ? _map.GetEntry(row.element) : null;
            string line = entry == null ? string.Empty : TemplateText.Fill(bannerLineTemplate, "ability", entry.AbilityLabel);
            fx.ShowBanner(ElementName(row), TemplateText.Fill(bannerTemplate, "level", row.level.ToString()), line, c);
        }

        void HandleBalance(int balance) => Redraw();

        Row Focused => _focus >= 0 && _focus < rows.Length ? rows[_focus] : null;

        void Redraw()
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                if (row.flower) row.flower.Set(row.element, row.level);
                if (row.levelText) row.levelText.text = row.level.ToString();
                if (row.selected) row.selected.color = row == Focused ? UITheme.Resolve(null, UIColorToken.Selection) : Color.clear;
            }
            DrawFocused();
            if (headerLineText) headerLineText.text = _stage && !_stage.HasMorphs ? noMorphLine : defaultLine;
        }

        void DrawFocused()
        {
            var row = Focused;
            if (row == null) return;
            var entry = _map ? _map.GetEntry(row.element) : null;
            bool maxed = row.level >= HangarUpgradeStore.MaxLevel;
            string level = row.level.ToString(), next = (row.level + 1).ToString();

            if (elementText) elementText.text = ElementName(row).ToUpperInvariant();
            if (stepText) stepText.text = maxed ? TemplateText.Fill(maxedStepTemplate, "level", level)
                                                : TemplateText.Fill(stepTemplate, "level", level, "next", next);
            if (abilityText) abilityText.text = row.open ? openSlotTitle : entry.AbilityLabel;
            if (descriptionText) descriptionText.text = row.open ? openSlotLine : entry.AbilityDescription;
            if (nowFlower) nowFlower.Set(row.element, row.level);
            if (nowText) nowText.text = TemplateText.Fill(nowTemplate, "level", level);
            KitVisibility.SetShown(nextGroup, !maxed);
            if (nextFlower) nextFlower.Set(row.element, Mathf.Min(row.level + 1, HangarUpgradeStore.MaxLevel));
            if (nextText) nextText.text = TemplateText.Fill(nextTemplate, "level", next);

            bool hasUpgrade = !row.open && !string.IsNullOrEmpty(entry.UpgradeLabel);
            KitVisibility.SetShown(upgradeGroup, hasUpgrade);
            if (hasUpgrade)
            {
                string at = upgradeLevel.ToString();
                if (upgradeEyebrowText)
                    upgradeEyebrowText.text = TemplateText.Fill(row.level >= upgradeLevel ? upgradeReachedTemplate : upgradeAheadTemplate, "level", at);
                if (upgradeTitleText) upgradeTitleText.text = entry.UpgradeLabel;
                if (upgradeLineText) upgradeLineText.text = entry.UpgradeDescription;
            }

            int balance = PlayerDataService.Instance ? PlayerDataService.Instance.GetCrystalBalance() : 0;
            int cost = HangarUpgradeStore.CostOfNext(row.level, crystalsPerLevel);
            if (upgradeButtonText) upgradeButtonText.text = maxed ? maxedWord : TemplateText.Fill(buyTemplate, "cost", cost.ToString());
            if (upgradeButton) upgradeButton.interactable = !row.open && !maxed && balance >= cost && !_animating;
        }

        string ElementName(Row row)
        {
            int i = (int)row.element - 1;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : row.element.ToString();
        }
    }
}
