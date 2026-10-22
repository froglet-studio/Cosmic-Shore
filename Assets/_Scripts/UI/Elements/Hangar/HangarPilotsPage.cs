using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
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
    /// </summary>
    public class HangarPilotsPage : MonoBehaviour, IHangarTabPage
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
        }

        [SerializeField] private SO_CaptainList captains;
        [SerializeField] private AbilityLockupStyleSO lockupStyle;

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

        readonly List<SO_Captain> _pilots = new();
        string _vesselName;
        int _selected;

        void Awake()
        {
            if (!lockupStyle) lockupStyle = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
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
            }
            KitVisibility.SetShown(heroGroup, _pilots.Count > 0);
            KitVisibility.SetShown(emptyGroup, _pilots.Count == 0);
            Select(0);
        }

        public void SetActivePage(bool active) { }

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
                if (cards[i]?.underline) cards[i].underline.color = i == _selected ? Colour(_pilots[Mathf.Min(i, _pilots.Count - 1)]) : Color.clear;
        }

        string ElementName(SO_Captain p)
        {
            int i = (int)p.PrimaryElement - 1;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : p.PrimaryElement.ToString();
        }

        Color Colour(SO_Captain p) => lockupStyle ? lockupStyle.ColourFor(p.PrimaryElement) : Color.white;
    }
}
