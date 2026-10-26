using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's PILOTS tab (rev 3: "bring the old pilots feature here"). The old Captains come back
    /// as pilots: one per element for each vessel (<c>ElementalCaptains</c>), each with the portrait,
    /// name, "The Space Sparrow" line, quote, what they are best at and how they fly as an AI wingmate.
    /// Four cards along the bottom pick which one the hero shows.
    ///
    /// <para>Read-only today. The captain levels / XP and the upgrade purchase died with the PlayFab
    /// catalogue, and the new Pilots system (PilotSO, Docs/PILOTS_AND_ARTIFACTS.md) is not on
    /// bleeding-edge, so this shows who the pilots ARE; choosing one to fly with is that system's
    /// job.</para>
    ///
    /// <para>Rev 24 (Shombith, 2026-10-26): PILOTS is a tab of its own and the header above the vessel names the picked
    /// pilot - "PILOT - MASS - SPARROW", the name, the quote (<see cref="IHangarHeader"/>). The pilots are four rows -
    /// portrait, name, what they are best at, and their element's flower at the level the player has bought for it on
    /// this vessel. Moving the pad onto a row picks it. The rev-3 hero fields stay optional for a layout that has one.</para>
    /// </summary>
    public class HangarPilotsPage : MonoBehaviour, IHangarTabPage, IHangarHeader
    {
        [System.Serializable]
        public class Card
        {
            public CanvasGroup group;
            public KitButton button;
            public Image portrait;
            public TMP_Text nameText;
            public Image elementGlyph;
            public Image underline;
            [Tooltip("Rev 24: what the pilot is best at (SO_Captain.Description).")]
            public TMP_Text lineText;
            [Tooltip("Rev 24: the pilot's element as the HUD's flower, at the level the player bought on this vessel.")]
            public KitElementFlower flower;
        }

        [SerializeField] private SO_CaptainList captains;
        [Tooltip("What a pilot card's element glyph draws in. Docs/UI_COLOR_RULES.md: an element never has a fixed colour in the UI - its glyph's shape says which it is.")]
        [SerializeField] private CosmicShore.UI.UIColorToken elementTint = CosmicShore.UI.UIColorToken.NeutralLightest;

        [Header("Hero")]
        [SerializeField] private CanvasGroup heroGroup;
        [SerializeField] private Image heroPortrait;
        [SerializeField] private Image heroFrame;
        [SerializeField] private TMP_Text heroEyebrowText;
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text heroDescriptionText;
        [SerializeField] private TMP_Text heroQuoteText;
        [SerializeField] private TMP_Text heroAiText;
        [SerializeField] private CanvasGroup emptyGroup;

        [Header("Cards")]
        [SerializeField] private Card[] cards = new Card[4];

        [Header("Words")]
        [Tooltip("The hero's eyebrow; {element} and {vessel} are filled in.")]
        [SerializeField] private string eyebrowTemplate = "The {element} {vessel}";
        [Tooltip("The quote, wrapped; {quote} is filled in.")]
        [SerializeField] private string quoteTemplate = "“{quote}”";
        [Tooltip("The AI line; {behaviour} is filled in.")]
        [SerializeField] private string aiTemplate = "As your wingmate: {behaviour}";
        [SerializeField] private string[] elementNames = { "Charge", "Mass", "Space", "Time" };

        [Header("Header (Rev 24)")]
        [Tooltip("The header's kicker while PILOTS is shown; {element} and {vessel} are filled in. Upper-cased.")]
        [SerializeField] private string headerKickerTemplate = "PILOT · {element} · {vessel}";

        readonly List<SO_Captain> _pilots = new();
        string _vesselName;
        VesselClassType _vesselClass = VesselClassType.Any;
        int _selected;
        bool _active;
        GameObject _lastSelected;

        public event System.Action HeaderChanged;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i]?.button) cards[i].button.onClick.AddListener(() => Select(index));
            }
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _pilots.Clear();
            _vesselName = vessel ? vessel.Name : string.Empty;
            _vesselClass = vessel ? vessel.Class : VesselClassType.Any;
            if (vessel && captains && captains.CaptainList != null)
                foreach (var c in captains.CaptainList)
                    if (c && c.Vessel && c.Vessel.Class == vessel.Class) _pilots.Add(c);
            _pilots.Sort((a, b) => ((int)a.PrimaryElement).CompareTo((int)b.PrimaryElement));

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                bool used = i < _pilots.Count;
                KitVisibility.SetShown(card.group, used);
                if (!used) continue;
                var p = _pilots[i];
                if (card.portrait) { card.portrait.sprite = p.Image; card.portrait.enabled = p.Image; }
                if (card.nameText) card.nameText.text = p.Name;
                if (card.elementGlyph) card.elementGlyph.color = Colour(p);
                if (card.lineText) card.lineText.text = p.Description;
                if (card.flower)
                    card.flower.Set(p.PrimaryElement, _vesselClass == VesselClassType.Any ? HangarUpgradeStore.StartLevel
                                                                                          : HangarUpgradeStore.GetLevel(_vesselClass, p.PrimaryElement));
            }
            KitVisibility.SetShown(heroGroup, _pilots.Count > 0);
            KitVisibility.SetShown(emptyGroup, _pilots.Count == 0);
            Select(0);
        }

        public void SetActivePage(bool active) => _active = active;

        public bool TryGetHeader(out string kicker, out string title, out string line)
        {
            kicker = title = line = string.Empty;
            if (_pilots.Count == 0) return false;
            var p = _pilots[_selected];
            kicker = TemplateText.Fill(headerKickerTemplate, "element", ElementName(p), "vessel", _vesselName).ToUpperInvariant();
            title = p.Name.ToUpperInvariant();
            line = string.IsNullOrEmpty(p.Flavor) ? p.Description : quoteTemplate.Replace("{quote}", p.Flavor.Trim('“', '”', '"'));
            return true;
        }

        void Update()
        {
            // The pad moving onto a pilot picks it: one reference compare a frame while the tab is shown.
            if (!_active || !EventSystem.current) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            for (int i = 0; i < cards.Length && i < _pilots.Count; i++)
                if (cards[i]?.button && cards[i].button.gameObject == selected && i != _selected) Select(i);
        }

        void Select(int index)
        {
            if (_pilots.Count == 0) return;
            _selected = Mathf.Clamp(index, 0, _pilots.Count - 1);
            var p = _pilots[_selected];
            Color c = Colour(p);
            if (heroPortrait) { heroPortrait.sprite = p.Image; heroPortrait.enabled = p.Image; }
            if (heroFrame) heroFrame.color = c;
            if (heroEyebrowText)
            {
                heroEyebrowText.text = eyebrowTemplate.Replace("{element}", ElementName(p)).Replace("{vessel}", _vesselName);
                heroEyebrowText.color = c;
            }
            if (heroNameText) heroNameText.text = p.Name;
            if (heroDescriptionText) heroDescriptionText.text = p.Description;
            if (heroQuoteText) heroQuoteText.text = string.IsNullOrEmpty(p.Flavor) ? string.Empty : quoteTemplate.Replace("{quote}", p.Flavor.Trim('“', '”', '"'));
            if (heroAiText) heroAiText.text = string.IsNullOrEmpty(p.AIBehaviorDescription) ? string.Empty : aiTemplate.Replace("{behaviour}", p.AIBehaviorDescription);
            for (int i = 0; i < cards.Length; i++)
                if (cards[i]?.underline) cards[i].underline.color = i == _selected ? CosmicShore.UI.UITheme.Resolve(null, CosmicShore.UI.UIColorToken.Selection) : Color.clear;   // a real selection
            HeaderChanged?.Invoke();
        }

        string ElementName(SO_Captain p)
        {
            int i = (int)p.PrimaryElement - 1;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : p.PrimaryElement.ToString();
        }

        Color Colour(SO_Captain p) => CosmicShore.UI.UITheme.Resolve(null, elementTint);
    }
}
