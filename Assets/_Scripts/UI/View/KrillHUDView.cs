using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Krill's HUD: the HAND rides the fleet's four-icon row (design: <c>R_VesselActions/KRILL.md</c> §5). The row order is
    /// the fleet's <b>charge · mass · space · time</b>, and each card shows the hand's card of that element: its name over
    /// the icon and the level it arrives at under it. The button on the card picks it (A = Mass, B = Time, X = Charge,
    /// Y = Space). With no hand dealt the cards dim and show what you hold of the element. Above the row: the phase and its
    /// seconds left, and the swarm's size.
    ///
    /// <para><b>Every word is authored</b> (CLAUDE.md, player-facing text): the card names are the deck's, the phase names
    /// and the three templates are serialized here, and an empty field shows nothing. Tokens: <c>{name}</c>,
    /// <c>{level}</c>, <c>{phase}</c>, <c>{seconds}</c>, <c>{count}</c>, <c>{cap}</c>.</para>
    ///
    /// <para>Each gauge (one filled Image per card) is what you hold of that element out of the 15 a card can reach, and
    /// turns READY while the hand offers that card. Colour here is a GAUGE channel; the upgrade lives on the lockup's
    /// card, so this view only re-anchors its rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).</para>
    /// </summary>
    public class KrillHUDView : VesselHUDView
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
        [SerializeField] string swarmTemplate = "{count} krill";
        [Tooltip("Over a card in the hand. Tokens: {name}.")]
        [SerializeField] string cardNameTemplate = "{name}";
        [Tooltip("Under a card in the hand: the level it arrives at. Tokens: {level}.")]
        [SerializeField] string cardLevelTemplate = "Lv {level}";
        [Tooltip("Under a card with no hand dealt: the levels you hold of the element. Tokens: {level}.")]
        [SerializeField] string heldTemplate = "{level}";

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

        const string HeaderName = "KrillHeader";
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
                _phase = Label(header, "KrillPhase", headerFontSize, new Vector2(0.5f, 1f), new Vector2(0f, 0f), TextAlignmentOptions.Top);
                _swarm = Label(header, "KrillSwarm", headerFontSize, new Vector2(0.5f, 0f), new Vector2(0f, 0f), TextAlignmentOptions.Bottom);
            }
            for (int slot = 0; slot < 4; slot++)
            {
                if (_names[slot]) continue;
                var element = AbilityDisplayOrder.Length > slot ? AbilityDisplayOrder[slot] : Element.None;
                if (!TryGetAbilityIcon(element, out var icon) || !icon) continue;
                var rt = icon.rectTransform;
                _names[slot] = Label(rt, "KrillCardName", cardFontSize, new Vector2(0.5f, 1f), new Vector2(0f, 6f), TextAlignmentOptions.Bottom);
                _names[slot].rectTransform.pivot = new Vector2(0.5f, 0f);
                _levels[slot] = Label(rt, "KrillCardLevel", cardFontSize, new Vector2(0.5f, 0f), new Vector2(0f, -6f), TextAlignmentOptions.Top);
                _levels[slot].rectTransform.pivot = new Vector2(0.5f, 1f);
            }
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
