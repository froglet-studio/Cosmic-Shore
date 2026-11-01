using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Starlings' HUD: the HAND rides the fleet's four-icon row (design: <c>R_VesselActions/STARLING.md</c> §5). The row order is
    /// the fleet's <b>charge · mass · space · time</b>, and each card shows the hand's card of that element: its name over
    /// the icon and the level it arrives at under it. The button on the card picks it (A = Mass, B = Time, X = Charge,
    /// Y = Space). With no hand dealt the cards dim and show what you hold of the element. Above the row: the phase and its
    /// seconds left, and the swarm's size.
    ///
    /// <para><b>Every word is authored</b> (CLAUDE.md, player-facing text): the card names are the deck's, the phase names
    /// and the templates are serialized here, and an empty field shows nothing. Tokens: <c>{name}</c>,
    /// <c>{level}</c>, <c>{from}</c>, <c>{to}</c>, <c>{next}</c>, <c>{phase}</c>, <c>{seconds}</c>, <c>{count}</c>, <c>{cap}</c>.</para>
    ///
    /// <para><b>Level scaling</b> (Garrett, 2026-10-24: "show what the next level will do"): a hand card you already hold
    /// reads <c>Lv 3 → 7</c>, and the details show every number at both levels (<c>3.0 → 4.5</c>) and the jolts picking it
    /// unlocks; with no hand, each held card's numbers sit beside the next level's.</para>
    ///
    /// <para><b>The objective card</b> (<see cref="CoreAbility.Objective"/>, directly left of the omnicrystal's): the phase's
    /// target as a glyph (<see cref="objectiveIcons"/>, in the cycle's order), which DRAINS from the top as the phase timer
    /// runs out over a faint copy of itself. <b>The card details</b> (a trigger held while a pick is pressed, or a long
    /// press): a large card in the middle of the screen, its words filled from the templates below.</para>
    ///
    /// <para>Each gauge (one filled Image per card) is what you hold of that element out of the 15 a card can reach, and
    /// turns READY while the hand offers that card. Colour here is a GAUGE channel; the upgrade lives on the lockup's
    /// card, so this view only re-anchors its rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).</para>
    ///
    /// <para><b>The element hand</b> (Garrett, 2026-11-01): once a draw element is set, a hand is up to five cards of that
    /// element, shown as a row of cards above the icon row on the root canvas, the highlighted one raised and edged in
    /// the element's colour. A tap on a card raises <see cref="HandCardTapped"/>. With no hand up, the icon of the element
    /// the next hand is drawn from carries <see cref="NextDrawLabel"/>.</para>
    /// </summary>
    public class StarlingHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: Charge levels held, out of 15.")]
        [SerializeField] Image chargeGauge;
        [Tooltip("Mass card: Mass levels held, out of 15.")]
        [SerializeField] Image massGauge;
        [Tooltip("Space card: Space levels held, out of 15.")]
        [SerializeField] Image spaceGauge;
        [Tooltip("Time card: Time levels held, out of 15.")]
        [SerializeField] Image timeGauge;

        [Header("Words (authored; an empty field shows nothing)")]
        [Tooltip("The four phases' names, in the cycle's order: Omnicrystal, Elemental crystals, Opposing vessels, Opposing mass.")]
        [SerializeField] string[] phaseNames = { "Omnicrystal", "Elemental crystals", "Opposing vessels", "Opposing mass" };
        [Tooltip("The phase line. Tokens: {phase} {seconds}.")]
        [SerializeField] string phaseTemplate = "{phase}  {seconds}s";
        [Tooltip("The swarm's size. Tokens: {count} {cap}.")]
        [SerializeField] string swarmTemplate = "{count} starlings";
        [Tooltip("Over a card in the hand. Tokens: {name}.")]
        [SerializeField] string cardNameTemplate = "{name}";
        [Tooltip("Under a card in the hand: the level it arrives at. Tokens: {level}.")]
        [SerializeField] string cardLevelTemplate = "Lv {level}";
        [Tooltip("Under a card in the hand that you already hold: the level you hold and the level picking it takes it to. " +
                 "Tokens: {from} {to}.")]
        [SerializeField] string cardLevelUpTemplate = "Lv {from} \u2192 {to}";
        [Tooltip("Under a card with no hand dealt: the levels you hold of the element. Tokens: {level}.")]
        [SerializeField] string heldTemplate = "{level}";

        [Header("Element hand (Garrett, 2026-11-01): up to five cards of the draw element")]
        [Tooltip("Over the icon of the element the next hand is drawn from, while no spread hand is up.")]
        [SerializeField] string nextDrawLabel = "Next draw";
        [Tooltip("Under the element hand's row: how to take a card.")]
        [SerializeField] string handHint = "X / B to move  \u00b7  A to take  \u00b7  hold for details";
        [Tooltip("One card of the row (canvas units).")]
        [SerializeField] Vector2 handCardSize = new(190f, 96f);
        [Tooltip("The gap between two cards of the row (canvas units).")]
        [SerializeField, Min(0f)] float handCardGap = 14f;
        [Tooltip("The row's centre above the bottom of the screen (canvas units), clear of the icon row.")]
        [SerializeField] float handRowHeight = 300f;
        [Tooltip("How far the highlighted card rises (canvas units), and its scale.")]
        [SerializeField] float handFocusLift = 18f;
        [SerializeField, Min(1f)] float handFocusScale = 1.1f;
        [Tooltip("A card's face.")]
        [SerializeField] Color handCardBacking = new(0.03f, 0.05f, 0.12f, 0.92f);
        [Tooltip("A card that is not highlighted (its alpha dims the edge).")]
        [SerializeField] Color handIdleEdge = new(1f, 1f, 1f, 0.25f);

        [Header("Objective card (the core card left of the omnicrystal's)")]
        [Tooltip("The phase's target, in the cycle's order: Omnicrystal, Elemental crystals, Opposing vessels, Opposing mass.")]
        [SerializeField] Sprite[] objectiveIcons = new Sprite[4];
        [Tooltip("The drained part of the objective glyph (its alpha is how faint it shows).")]
        [SerializeField] Color objectiveSpentColor = new(1f, 1f, 1f, 0.22f);

        [Header("Card details: a large card in the middle of the screen (a trigger held while picking, or a long press)")]
        [Tooltip("The card's name, large. Tokens: {name}.")]
        [SerializeField] string detailsTitleTemplate = "{name}";
        [Tooltip("Under the name: the level the card arrives at. Tokens: {level}.")]
        [SerializeField] string detailsLevelTemplate = "Level {level}";
        [Tooltip("Under the name, for a card you already hold: the level held and the level picking it takes it to. " +
                 "Tokens: {from} {to}.")]
        [SerializeField] string detailsLevelUpTemplate = "Level {from} \u2192 {to}";
        [Tooltip("One line per number on the card, at that level. Tokens: {label} {value} {unit}.")]
        [SerializeField] string detailsStatTemplate = "{label}: {value}{unit}";
        [Tooltip("A number that changes between two levels (the level held and the next one, or the level picking takes it " +
                 "to), so the player sees how the card scales. A number that does not change uses detailsStatTemplate. " +
                 "Tokens: {label} {value} {next} {unit}.")]
        [SerializeField] string detailsStatUpTemplate = "{label}: {value}{unit} \u2192 <b>{next}{unit}</b>";
        [Tooltip("A jolt the card reaches on the way to the level shown (picking it, or the level it arrives at). " +
                 "Tokens: {level} {text}.")]
        [SerializeField] string detailsUnlockTemplate = "<b>Unlocks at Lv {level}:</b> {text}";
        [Tooltip("The card's next jolt above the level you hold. Tokens: {level} {text}.")]
        [SerializeField] string detailsMilestoneTemplate = "At Lv {level}: {text}";
        [Tooltip("With no hand dealt: the heading of each card you hold of the element, followed by its text, its numbers at " +
                 "the held level (detailsStatTemplate) and the jolts it has reached. Tokens: {name} {level}.")]
        [SerializeField] string detailsHeldTemplate = "<b>{name}</b>  Lv {level}";
        [Tooltip("With no hand dealt: the heading of a held card below the top level, whose numbers then show the next " +
                 "level beside the held one. Tokens: {name} {level} {next}.")]
        [SerializeField] string detailsHeldUpTemplate = "<b>{name}</b>  Lv {level} \u2192 {next}";
        [Tooltip("Under a card at the top level (15): nothing more to gain.")]
        [SerializeField] string detailsMaxLevel = "Max level";
        [Tooltip("With no hand dealt: a jolt a held card has reached, under its numbers. Tokens: {level} {text}.")]
        [SerializeField] string detailsReachedTemplate = "Since Lv {level}: {text}";
        [Tooltip("With no hand dealt and nothing held of the element.")]
        [SerializeField] string detailsNothingHeld = "Nothing held yet";
        [Tooltip("The last line of the card: how to turn to another element while the details are open.")]
        [SerializeField] string detailsBrowseHint = "<i>Press another element to see its cards</i>";
        [Tooltip("Font size of the card's name.")]
        [SerializeField, Min(6f)] float detailsTitleFontSize = 40f;
        [Tooltip("Font size of the card's level.")]
        [SerializeField, Min(6f)] float detailsLevelFontSize = 26f;
        [Tooltip("Font size of the card's text, numbers and next jolt.")]
        [SerializeField, Min(6f)] float detailsFontSize = 22f;
        [Tooltip("The card's face.")]
        [SerializeField] Color detailsBacking = new(0.03f, 0.05f, 0.12f, 0.95f);
        [Tooltip("The card's width (canvas units); its height fits its words.")]
        [SerializeField, Min(100f)] float detailsWidth = 600f;
        [Tooltip("The element glyph's size at the top of the card (canvas units).")]
        [SerializeField, Min(16f)] float detailsGlyphSize = 120f;

        [Header("Look")]
        [Tooltip("Font size of the card names and levels.")]
        [SerializeField, Min(6f)] float cardFontSize = 18f;
        [Tooltip("Font size of the phase line and the swarm's size.")]
        [SerializeField, Min(6f)] float headerFontSize = 24f;
        [Tooltip("A card's icon with no hand dealt (its alpha dims it).")]
        [SerializeField] Color idleIconColor = new(1f, 1f, 1f, 0.35f);
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("A card the hand offers - the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        const string HeaderName = "StarlingHeader";
        const string ObjectiveHostName = "StarlingObjectiveButton";
        const string DetailsName = "StarlingCardDetails";
        const float ObjectiveIconSize = 80f;
        Image _objectiveSpent, _objectiveFill;
        int _objectiveShown = -1;
        RectTransform _details;
        Image _detailsGlyph, _detailsRule;
        Outline _detailsEdge;
        TextMeshProUGUI _detailsTitle, _detailsLevel, _detailsText;
        string _detailsShown;
        AbilityLockupStyleSO _style;
        TextMeshProUGUI _phase, _swarm;
        readonly TextMeshProUGUI[] _names = new TextMeshProUGUI[4];
        readonly TextMeshProUGUI[] _levels = new TextMeshProUGUI[4];
        readonly string[] _nameShown = new string[4], _levelShown = new string[4];
        string _phaseShown, _swarmShown;

        public string NextDrawLabel => nextDrawLabel;
        public string HandHint => handHint;

        /// <summary>Raised when the pilot taps card <c>index</c> of the element hand's row.</summary>
        public event System.Action<int> HandCardTapped;

        const string HandRowName = "StarlingElementHand";
        RectTransform _handRow;
        readonly RectTransform[] _handCards = new RectTransform[StarlingRules.ElementHandSize];
        readonly Outline[] _handEdges = new Outline[StarlingRules.ElementHandSize];
        readonly TextMeshProUGUI[] _handNames = new TextMeshProUGUI[StarlingRules.ElementHandSize];
        readonly TextMeshProUGUI[] _handLevels = new TextMeshProUGUI[StarlingRules.ElementHandSize];
        readonly string[] _handNameShown = new string[StarlingRules.ElementHandSize];
        readonly string[] _handLevelShown = new string[StarlingRules.ElementHandSize];
        TextMeshProUGUI _handHintText;
        int _handCountShown = -1, _handFocusShown = -1;
        Element _handElementShown = Element.None;
        string _handHintShown;

        public string PhaseTemplate => phaseTemplate;
        public string SwarmTemplate => swarmTemplate;
        public string CardNameTemplate => cardNameTemplate;
        public string CardLevelTemplate => cardLevelTemplate;
        public string CardLevelUpTemplate => cardLevelUpTemplate;
        public string HeldTemplate => heldTemplate;
        public string DetailsTitleTemplate => detailsTitleTemplate;
        public string DetailsLevelTemplate => detailsLevelTemplate;
        public string DetailsLevelUpTemplate => detailsLevelUpTemplate;
        public string DetailsStatTemplate => detailsStatTemplate;
        public string DetailsStatUpTemplate => detailsStatUpTemplate;
        public string DetailsUnlockTemplate => detailsUnlockTemplate;
        public string DetailsMilestoneTemplate => detailsMilestoneTemplate;
        public string DetailsHeldTemplate => detailsHeldTemplate;
        public string DetailsHeldUpTemplate => detailsHeldUpTemplate;
        public string DetailsMaxLevel => detailsMaxLevel;
        public string DetailsReachedTemplate => detailsReachedTemplate;
        public string DetailsNothingHeld => detailsNothingHeld;
        public string DetailsBrowseHint => detailsBrowseHint;

        /// <summary>The authored name of phase <paramref name="index"/>; empty when none is authored.</summary>
        public string PhaseName(int index) => phaseNames != null && index >= 0 && index < phaseNames.Length ? phaseNames[index] : string.Empty;

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(chargeGauge);
            Seat(massGauge);
            Seat(spaceGauge);
            Seat(timeGauge);
            for (int i = 0; i < 4; i++) _nameShown[i] = _levelShown[i] = null;
            _phaseShown = _swarmShown = null;
            _objectiveShown = -1;
            _detailsShown = null;
            _handCountShown = _handFocusShown = -1;
            _handElementShown = Element.None;
            _handHintShown = null;
            for (int i = 0; i < _handNameShown.Length; i++) _handNameShown[i] = _handLevelShown[i] = null;
        }

        void Seat(Image gauge)
        {
            if (!gauge) return;
            gauge.fillAmount = 0f;
            gauge.color = gaugeColor;
        }

        /// <summary>Builds the header (phase line, swarm size) and each card's name and level labels. Idempotent by name.</summary>
        public override void EnsureGeneratedAbilityIcons()
        {
            base.EnsureGeneratedAbilityIcons();
            EnsureObjectiveCard();
            if (!_phase)
            {
                var header = transform.Find(HeaderName) as RectTransform;
                if (!header)
                {
                    header = new GameObject(HeaderName, typeof(RectTransform)).GetComponent<RectTransform>();
                    header.SetParent(transform, false);
                }
                header.anchorMin = new Vector2(0f, 1f);
                header.anchorMax = new Vector2(1f, 1f);
                header.pivot = new Vector2(0.5f, 0f);
                header.sizeDelta = new Vector2(0f, 64f);
                header.anchoredPosition = Vector2.zero;
                _phase = Label(header, "StarlingPhase", headerFontSize, new Vector2(0.5f, 1f), new Vector2(0f, 0f), TextAlignmentOptions.Top);
                _swarm = Label(header, "StarlingSwarm", headerFontSize, new Vector2(0.5f, 0f), new Vector2(0f, 0f), TextAlignmentOptions.Bottom);
            }
            for (int slot = 0; slot < 4; slot++)
            {
                if (_names[slot]) continue;
                var element = AbilityDisplayOrder.Length > slot ? AbilityDisplayOrder[slot] : Element.None;
                if (!TryGetAbilityIcon(element, out var icon) || !icon) continue;
                var rt = icon.rectTransform;
                _names[slot] = Label(rt, "StarlingCardName", cardFontSize, new Vector2(0.5f, 1f), new Vector2(0f, 6f), TextAlignmentOptions.Bottom);
                _names[slot].rectTransform.pivot = new Vector2(0.5f, 0f);
                _levels[slot] = Label(rt, "StarlingCardLevel", cardFontSize, new Vector2(0.5f, 0f), new Vector2(0f, -6f), TextAlignmentOptions.Top);
                _levels[slot].rectTransform.pivot = new Vector2(0.5f, 1f);
            }
        }

        /// <summary>
        /// The objective card's icon: a faint copy of the glyph (bound as the card's icon, so the lockup re-homes and scales it)
        /// with the bright, vertically filled glyph over it as a child. Its host is created outside the row; the lockup moves it.
        /// </summary>
        void EnsureObjectiveCard()
        {
            if (_objectiveSpent) return;
            var host = transform.Find(ObjectiveHostName) as RectTransform;
            if (!host)
            {
                host = new GameObject(ObjectiveHostName, typeof(RectTransform)).GetComponent<RectTransform>();
                host.SetParent(transform, false);
            }
            _objectiveSpent = ResolveGeneratedChild<Image>(host, "StarlingObjectiveIcon");
            var rt = _objectiveSpent.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ObjectiveIconSize, ObjectiveIconSize);
            _objectiveSpent.preserveAspect = true;
            _objectiveSpent.raycastTarget = false;
            _objectiveSpent.color = objectiveSpentColor;

            _objectiveFill = ResolveGeneratedChild<Image>(rt, "StarlingObjectiveFill");
            var fill = _objectiveFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            _objectiveFill.preserveAspect = true;
            _objectiveFill.raycastTarget = false;
            _objectiveFill.type = Image.Type.Filled;
            _objectiveFill.fillMethod = Image.FillMethod.Vertical;
            _objectiveFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _objectiveFill.fillAmount = 1f;

            BindCoreAbilityIcon(CoreAbility.Objective, _objectiveSpent);
            _objectiveShown = -1;
        }

        /// <summary>The objective card: phase <paramref name="phase"/>'s glyph, filled to the share of its timer left.</summary>
        public void SetObjective(int phase, float remaining01)
        {
            if (!_objectiveSpent) EnsureGeneratedAbilityIcons();
            if (!_objectiveSpent || !_objectiveFill) return;
            if (phase != _objectiveShown)
            {
                _objectiveShown = phase;
                var sprite = objectiveIcons != null && phase >= 0 && phase < objectiveIcons.Length ? objectiveIcons[phase] : null;
                _objectiveSpent.sprite = _objectiveFill.sprite = sprite;
                _objectiveSpent.enabled = _objectiveFill.enabled = sprite;
            }
            _objectiveFill.fillAmount = Mathf.Clamp01(remaining01);
        }

        /// <summary>
        /// The card details: a large card in the middle of the screen with <paramref name="element"/>'s glyph and colour, the
        /// card's name and level and its body (text, numbers, next jolt), all already filled. <see cref="Element.None"/> hides it.
        /// </summary>
        public void SetDetails(Element element, string title, string level, string body)
        {
            string key = element == Element.None ? string.Empty : $"{element}|{title}|{level}|{body}";
            if (key == _detailsShown) return;
            _detailsShown = key;
            if (!_details) BuildDetails();
            if (!_details) return;
            bool show = element != Element.None;
            _details.gameObject.SetActive(show);
            if (!show) return;

            _details.SetAsLastSibling();   // over the rest of the HUD
            Color accent = Accent(element);
            _detailsEdge.effectColor = accent;
            _detailsRule.color = accent;
            _detailsGlyph.sprite = TryGetAbilityIcon(element, out var icon) && icon ? icon.sprite : null;
            _detailsGlyph.enabled = _detailsGlyph.sprite;
            _detailsGlyph.color = accent;
            _detailsTitle.text = title ?? string.Empty;
            _detailsLevel.text = level ?? string.Empty;
            _detailsLevel.color = accent;
            _detailsText.text = body ?? string.Empty;
        }

        /// <summary>
        /// The element hand's row: <paramref name="count"/> cards of <paramref name="element"/> (0 hides it), card
        /// <paramref name="focus"/> highlighted, and the hint under it. The cards' words come from <see cref="SetHandCard"/>.
        /// </summary>
        public void SetHandRow(int count, int focus, Element element, string hint)
        {
            count = Mathf.Clamp(count, 0, _handCards.Length);
            if (count == _handCountShown && focus == _handFocusShown && element == _handElementShown && hint == _handHintShown) return;
            if (count > 0 && !_handRow) BuildHandRow();
            if (!_handRow) return;
            _handCountShown = count;
            _handFocusShown = focus;
            _handElementShown = element;
            _handHintShown = hint;
            _handRow.gameObject.SetActive(count > 0);
            if (count == 0) return;

            _handRow.SetAsLastSibling();
            Color accent = Accent(element);
            float pitch = handCardSize.x + handCardGap, left = -0.5f * pitch * (count - 1);
            for (int i = 0; i < _handCards.Length; i++)
            {
                var card = _handCards[i];
                bool on = i < count;
                card.gameObject.SetActive(on);
                if (!on) continue;
                bool focused = i == focus;
                card.anchoredPosition = new Vector2(left + i * pitch, focused ? handFocusLift : 0f);
                card.localScale = Vector3.one * (focused ? handFocusScale : 1f);
                _handEdges[i].effectColor = focused ? accent : handIdleEdge;
                _handEdges[i].effectDistance = focused ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
                _handLevels[i].color = accent;
            }
            _handHintText.text = hint ?? string.Empty;
        }

        /// <summary>Card <paramref name="index"/> of the element hand's row: its name and level line, already filled.</summary>
        public void SetHandCard(int index, string cardName, string level)
        {
            if (index < 0 || index >= _handCards.Length || !_handRow) return;
            cardName ??= string.Empty;
            level ??= string.Empty;
            if (cardName != _handNameShown[index]) { _handNameShown[index] = cardName; _handNames[index].text = cardName; }
            if (level != _handLevelShown[index]) { _handLevelShown[index] = level; _handLevels[index].text = level; }
        }

        /// <summary>Builds the hand row on the ROOT canvas, bottom centre, so no HUD layout moves it. Idempotent by name.</summary>
        void BuildHandRow()
        {
            var canvas = GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas.transform as RectTransform : transform as RectTransform;
            if (!root) return;
            _handRow = root.Find(HandRowName) as RectTransform;
            if (!_handRow)
            {
                _handRow = new GameObject(HandRowName, typeof(RectTransform)).GetComponent<RectTransform>();
                _handRow.SetParent(root, false);
            }
            _handRow.anchorMin = _handRow.anchorMax = new Vector2(0.5f, 0f);
            _handRow.pivot = new Vector2(0.5f, 0.5f);
            _handRow.anchoredPosition = new Vector2(0f, handRowHeight);
            _handRow.sizeDelta = new Vector2(_handCards.Length * (handCardSize.x + handCardGap), handCardSize.y);

            for (int i = 0; i < _handCards.Length; i++)
            {
                var face = ResolveGeneratedChild<Image>(_handRow, "Card" + i);
                face.color = handCardBacking;
                face.raycastTarget = true;
                var rt = face.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = handCardSize;
                _handCards[i] = rt;
                if (!face.TryGetComponent(out _handEdges[i])) _handEdges[i] = face.gameObject.AddComponent<Outline>();
                if (!face.TryGetComponent<Button>(out var button)) button = face.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };   // the pad drives the highlight, not UI navigation
                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => HandCardTapped?.Invoke(index));
                _handNames[i] = Label(rt, "Name", cardFontSize, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), TextAlignmentOptions.Center);
                _handNames[i].rectTransform.sizeDelta = new Vector2(handCardSize.x - 16f, 30f);
                _handNames[i].fontStyle = FontStyles.Bold;
                _handLevels[i] = Label(rt, "Level", cardFontSize, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), TextAlignmentOptions.Center);
                _handLevels[i].rectTransform.sizeDelta = new Vector2(handCardSize.x - 16f, 30f);
                _handNameShown[i] = _handLevelShown[i] = null;
            }
            _handHintText = Label(_handRow, "Hint", cardFontSize, new Vector2(0.5f, 0f), new Vector2(0f, -handFocusLift - 8f), TextAlignmentOptions.Top);
            _handHintText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _handHintText.rectTransform.sizeDelta = new Vector2(_handRow.sizeDelta.x, 30f);
            _handRow.gameObject.SetActive(false);
        }

        Color Accent(Element element)
        {
            if (!_style) _style = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            return _style ? _style.ColourFor(element) : Color.white;
        }

        /// <summary>Builds the card on the ROOT canvas, centred, so no HUD layout can push it off screen. Idempotent by name.</summary>
        void BuildDetails()
        {
            var canvas = GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas.transform as RectTransform : transform as RectTransform;
            if (!root) return;
            _details = root.Find(DetailsName) as RectTransform;
            if (!_details)
            {
                _details = new GameObject(DetailsName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                _details.SetParent(root, false);
            }
            _details.anchorMin = _details.anchorMax = _details.pivot = new Vector2(0.5f, 0.5f);
            _details.anchoredPosition = Vector2.zero;
            _details.sizeDelta = new Vector2(detailsWidth, _details.sizeDelta.y);
            if (_details.TryGetComponent<Image>(out var face))
            {
                face.color = detailsBacking;
                face.raycastTarget = false;
            }
            if (!_details.TryGetComponent(out _detailsEdge)) _detailsEdge = _details.gameObject.AddComponent<Outline>();
            _detailsEdge.effectDistance = new Vector2(4f, -4f);
            if (!_details.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter = _details.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (!_details.TryGetComponent<VerticalLayoutGroup>(out var layout)) layout = _details.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 32);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _detailsGlyph = ResolveGeneratedChild<Image>(_details, "Glyph");
            _detailsGlyph.preserveAspect = true;
            _detailsGlyph.raycastTarget = false;
            Size(_detailsGlyph.gameObject, detailsGlyphSize);
            _detailsTitle = DetailsLabel("Title", detailsTitleFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
            _detailsLevel = DetailsLabel("Level", detailsLevelFontSize, TextAlignmentOptions.Center, FontStyles.Normal);
            _detailsRule = ResolveGeneratedChild<Image>(_details, "Rule");
            _detailsRule.raycastTarget = false;
            Size(_detailsRule.gameObject, 3f);
            _detailsText = DetailsLabel("Body", detailsFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            _details.gameObject.SetActive(false);
        }

        TextMeshProUGUI DetailsLabel(string name, float size, TextAlignmentOptions align, FontStyles style)
        {
            var text = ResolveGeneratedChild<TextMeshProUGUI>(_details, name);
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static void Size(GameObject go, float height)
        {
            if (!go.TryGetComponent<LayoutElement>(out var element)) element = go.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
        }

        protected override void OnDestroy()
        {
            // The card and the hand row live on the root canvas, outside this view, so they go with the view.
            if (_details) Destroy(_details.gameObject);
            if (_handRow) Destroy(_handRow.gameObject);
            base.OnDestroy();
        }

        TextMeshProUGUI Label(RectTransform parent, string name, float size, Vector2 anchor, Vector2 offset, TextAlignmentOptions align)
        {
            var text = ResolveGeneratedChild<TextMeshProUGUI>(parent, name);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(220f, 30f);
            text.alignment = align;
            text.fontSize = size;
            // Never ellipsise: an overflowing label is a loud, fixable fault, a truncated one a wrong reading.
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        /// <summary>The phase line, already filled from <see cref="PhaseTemplate"/>.</summary>
        public void SetPhase(string line)
        {
            if (!_phase) EnsureGeneratedAbilityIcons();
            line ??= string.Empty;
            if (!_phase || line == _phaseShown) return;
            _phaseShown = line;
            _phase.text = line;
        }

        /// <summary>The swarm's size, already filled from <see cref="SwarmTemplate"/>.</summary>
        public void SetSwarm(string line)
        {
            if (!_swarm) EnsureGeneratedAbilityIcons();
            line ??= string.Empty;
            if (!_swarm || line == _swarmShown) return;
            _swarmShown = line;
            _swarm.text = line;
        }

        /// <summary>One card of the row (slot in <see cref="VesselHUDView.AbilityDisplayOrder"/>): its two labels, already filled,
        /// whether the hand offers it, and the share of the element held (0..1).</summary>
        public void SetCard(int slot, string cardName, string level, bool offered, float held01)
        {
            if (slot < 0 || slot >= 4) return;
            if (!_names[slot]) EnsureGeneratedAbilityIcons();
            cardName ??= string.Empty;
            level ??= string.Empty;
            if (_names[slot] && cardName != _nameShown[slot]) { _nameShown[slot] = cardName; _names[slot].text = cardName; }
            if (_levels[slot] && level != _levelShown[slot]) { _levelShown[slot] = level; _levels[slot].text = level; }
            var element = AbilityDisplayOrder[slot];
            if (TryGetAbilityIcon(element, out var icon) && icon) icon.color = offered ? Color.white : idleIconColor;
            Push(GaugeOf(element), held01, offered);
        }

        Image GaugeOf(Element element) => element switch
        {
            Element.Charge => chargeGauge,
            Element.Mass => massGauge,
            Element.Space => spaceGauge,
            Element.Time => timeGauge,
            _ => null,
        };

        void Push(Image gauge, float fill01, bool ready)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, ready ? readyColor : gaugeColor, k);
        }

        /// <summary>Re-anchor every gauge's rest scale after the base's upgrade handling (vessel contract rule 9).</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            var gauge = GaugeOf(element);
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
