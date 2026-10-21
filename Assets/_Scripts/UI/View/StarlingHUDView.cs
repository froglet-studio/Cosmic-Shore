using CosmicShore.Data;
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
    /// and the three templates are serialized here, and an empty field shows nothing. Tokens: <c>{name}</c>,
    /// <c>{level}</c>, <c>{phase}</c>, <c>{seconds}</c>, <c>{count}</c>, <c>{cap}</c>.</para>
    ///
    /// <para><b>The objective card</b> (<see cref="CoreAbility.Objective"/>, directly left of the omnicrystal's): the phase's
    /// target as a glyph (<see cref="objectiveIcons"/>, in the cycle's order), which DRAINS from the top as the phase timer
    /// runs out over a faint copy of itself. <b>The card details</b> (a trigger held while a pick is pressed, or a long
    /// press): a large card in the middle of the screen, its words filled from the templates below.</para>
    ///
    /// <para>Each gauge (one filled Image per card) is what you hold of that element out of the 15 a card can reach, and
    /// turns READY while the hand offers that card. Colour here is a GAUGE channel; the upgrade lives on the lockup's
    /// card, so this view only re-anchors its rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).</para>
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
        [Tooltip("Under a card with no hand dealt: the levels you hold of the element. Tokens: {level}.")]
        [SerializeField] string heldTemplate = "{level}";

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
        [Tooltip("One line per number on the card, at that level. Tokens: {label} {value} {unit}.")]
        [SerializeField] string detailsStatTemplate = "{label}: {value}{unit}";
        [Tooltip("The card's next jolt above the level you hold. Tokens: {level} {text}.")]
        [SerializeField] string detailsMilestoneTemplate = "At Lv {level}: {text}";
        [Tooltip("With no hand dealt: one line per card you hold of the element. Tokens: {name} {level}.")]
        [SerializeField] string detailsHeldTemplate = "{name}  Lv {level}";
        [Tooltip("With no hand dealt and nothing held of the element.")]
        [SerializeField] string detailsNothingHeld = "Nothing held yet";
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

        public string PhaseTemplate => phaseTemplate;
        public string SwarmTemplate => swarmTemplate;
        public string CardNameTemplate => cardNameTemplate;
        public string CardLevelTemplate => cardLevelTemplate;
        public string HeldTemplate => heldTemplate;
        public string DetailsTitleTemplate => detailsTitleTemplate;
        public string DetailsLevelTemplate => detailsLevelTemplate;
        public string DetailsStatTemplate => detailsStatTemplate;
        public string DetailsMilestoneTemplate => detailsMilestoneTemplate;
        public string DetailsHeldTemplate => detailsHeldTemplate;
        public string DetailsNothingHeld => detailsNothingHeld;

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
            // The card lives on the root canvas, outside this view, so it goes with the view.
            if (_details) Destroy(_details.gameObject);
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
