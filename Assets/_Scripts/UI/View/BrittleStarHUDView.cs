using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Brittle Star's HUD readouts (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §4). The row order IS the element
    /// map: <b>Charge Mode (charge) · Creation Mode (mass) · Space Mode (space) · Speed Mode (time)</b>.
    ///
    /// <para>One gauge per card, each answering the one question its card poses. No card turns the CTA lime for "on"
    /// (Garrett, 2026-11-01: "i don't put the CTA color as the on color, it's too much"): the card's own parts say the
    /// mode is on, and a gauge keeps its resting colour unless something is wrong (the guns overheated).</para>
    /// <list type="number">
    /// <item><b>Charge</b> — how many rings can I throw? The spare-ring stock (0, half, full of two).</item>
    /// <item><b>Space</b> — how hot are the guns? Heat 0..1, in the HOT colour while overheated.</item>
    /// <item><b>Creation</b>, <b>Speed</b> — each mode's switch, eased 0..1: a SWITCH moving between its ends, never a
    /// meter being spent (the Stoat's Mass card).</item>
    /// </list>
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only re-anchors its captured
    /// rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class BrittleStarHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the spare-ring stock (one throw each).")]
        [SerializeField] Image ringGauge;
        [Tooltip("Mass card: Creation Mode's switch.")]
        [SerializeField] Image creationGauge;
        [Tooltip("Space card: the guns' heat.")]
        [SerializeField] Image destructionGauge;
        [Tooltip("Time card: Speed Mode's switch.")]
        [SerializeField] Image speedGauge;

        [Header("Charge card: the five cords (icon_kit specs/brittlestar.py, Garrett picked C 2026-11-01)")]
        [Tooltip("The cords loose, a whip each (BrittleStar_ChargeWhips), white: fades out as RT pulls.")]
        [SerializeField] Image chargeWhipsIcon;
        [Tooltip("The cords pulled straight (BrittleStar_ChargeSabers), white: fades in as RT pulls.")]
        [SerializeField] Image chargeSabersIcon;
        [Tooltip("The Brittle Star nose-on (BrittleStar_ChargeStar), the domain colour.")]
        [SerializeField] Image chargeStarIcon;
        [Tooltip("Alpha of the cords while Charge Mode is off.")]
        [SerializeField, Range(0f, 1f)] float cordsOffAlpha = 0.25f;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("The guns are overheated (the Space card).")]
        [SerializeField] Color hotColor = new(1f, 0.32f, 0.18f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest (no spare rings, cold guns, every mode off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(ringGauge, 0f);
            Seat(creationGauge, 0f);
            Seat(destructionGauge, 0f);
            Seat(speedGauge, 0f);
            SetCords(0f, 0f, new Color(0f, 0f, 0f, 0f));
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = gaugeColor;
        }

        public void SetCharge(float spareStock01, bool on) => Push(ringGauge, spareStock01, gaugeColor);
        public void SetCreation(float on01) => Push(creationGauge, on01, gaugeColor);
        public void SetSpace(float heat01, bool on, bool overheated) =>
            Push(destructionGauge, heat01, overheated ? hotColor : gaugeColor);
        public void SetSpeed(float on01) => Push(speedGauge, on01, gaugeColor);

        /// <summary>
        /// The Charge card's cords: whips while Charge Mode is on and RT is loose, crossfading into sabers as RT pulls
        /// (<paramref name="pull01"/>, <c>BrittleStarExecutor.CordPull01</c>); dim whips with the mode off
        /// (<paramref name="cordsOut01"/>, the cords' eased presence). The star wears <paramref name="domain"/>
        /// (alpha 0 = not resolved yet, keep the last).
        /// </summary>
        public void SetCords(float cordsOut01, float pull01, Color domain)
        {
            if (domain.a > 0f) _domain = domain;
            float out01 = Mathf.Clamp01(cordsOut01), pull = Mathf.Clamp01(pull01) * out01;
            float cords = Mathf.Lerp(cordsOffAlpha, 1f, out01);
            if (chargeWhipsIcon) chargeWhipsIcon.color = new Color(1f, 1f, 1f, cords * (1f - pull));
            if (chargeSabersIcon) chargeSabersIcon.color = new Color(1f, 1f, 1f, cords * pull);
            if (chargeStarIcon) chargeStarIcon.color = _domain;
        }

        Color _domain = Color.white;

        void Push(Image gauge, float fill01, Color color)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, color, k);
        }

        /// <summary>
        /// Re-anchor every gauge's rest scale after the base's upgrade handling, or the next gauge write settles it back to
        /// the pre-upgrade scale and wipes the lockup's persistent bump (vessel contract rule 9).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            var gauge = element switch
            {
                Element.Charge => ringGauge,
                Element.Mass => creationGauge,
                Element.Space => destructionGauge,
                Element.Time => speedGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
