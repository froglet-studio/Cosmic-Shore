using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Brittle Star's HUD readouts (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §4). The row order IS the element
    /// map: <b>Ring Detonation (charge) · Creation Mode (mass) · Destruction Mode (space) · Speed Mode (time)</b>.
    ///
    /// <para>One gauge per card, each answering the one question its card poses:</para>
    /// <list type="number">
    /// <item><b>Ring</b> (charge) — is there a ring to throw? Full while mounted, filling as one is rebuilt, empty while
    /// the hull flies ringless; in the READY colour while a crystal charge is stored (a second ring is waiting).</item>
    /// <item><b>Creation</b>, <b>Destruction</b>, <b>Speed</b> — each mode's switch, eased 0..1: a SWITCH moving between
    /// its ends, never a meter being spent (the Stoat's Mass card). All three turn the ready colour in Overdrive.</item>
    /// </list>
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only re-anchors its captured
    /// rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class BrittleStarHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the support ring — full while mounted, filling while rebuilt.")]
        [SerializeField] Image ringGauge;
        [Tooltip("Mass card: Creation Mode's switch.")]
        [SerializeField] Image creationGauge;
        [Tooltip("Space card: Destruction Mode's switch.")]
        [SerializeField] Image destructionGauge;
        [Tooltip("Time card: Speed Mode's switch.")]
        [SerializeField] Image speedGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("A stored crystal charge (the Ring card) and Overdrive (the three mode cards) — the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest (a mounted ring, every mode off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(ringGauge, 1f);
            Seat(creationGauge, 0f);
            Seat(destructionGauge, 0f);
            Seat(speedGauge, 0f);
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = gaugeColor;
        }

        public void SetRing(float presence01, bool chargeStored) => Push(ringGauge, presence01, chargeStored);
        public void SetCreation(float on01, bool overdrive) => Push(creationGauge, on01, overdrive);
        public void SetDestruction(float on01, bool overdrive) => Push(destructionGauge, on01, overdrive);
        public void SetSpeed(float on01, bool overdrive) => Push(speedGauge, on01, overdrive);

        void Push(Image gauge, float fill01, bool ready)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, ready ? readyColor : gaugeColor, k);
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
