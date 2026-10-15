using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Shrike's HUD readouts (design: <c>R_VesselActions/SHRIKE.md</c> §4). The row order IS the element map:
    /// <b>Void Lance (charge) · Nest (mass) · Targeting Array (space) · Void Sling (time)</b>.
    /// <list type="number">
    /// <item><b>Lance</b> — its cooldown filling; the ready colour while docked (the dock bonus is running).</item>
    /// <item><b>Nest</b> — its cooldown filling; the ready colour while docked in a nest.</item>
    /// <item><b>Array</b> — full while a nest has a missile to spare; the ready colour while painting a lock.</item>
    /// <item><b>Sling</b> — its cooldown filling; the ready colour while slinging.</item>
    /// </list>
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only re-anchors its captured
    /// rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class ShrikeHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the Void Lance's cooldown.")]
        [SerializeField] Image lanceGauge;
        [Tooltip("Mass card: the Nest's cooldown.")]
        [SerializeField] Image nestGauge;
        [Tooltip("Space card: the Targeting Array — a missile to spare.")]
        [SerializeField] Image arrayGauge;
        [Tooltip("Time card: the Void Sling's cooldown.")]
        [SerializeField] Image slingGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("The live state of each card (docked, nested, locked, slinging) — the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest (everything ready, no nest). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(lanceGauge, 1f);
            Seat(nestGauge, 1f);
            Seat(arrayGauge, 0f);
            Seat(slingGauge, 1f);
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = gaugeColor;
        }

        public void SetLance(float ready01, bool live) => Push(lanceGauge, ready01, live);
        public void SetNest(float ready01, bool live) => Push(nestGauge, ready01, live);
        public void SetArray(float fill01, bool live) => Push(arrayGauge, fill01, live);
        public void SetSling(float ready01, bool live) => Push(slingGauge, ready01, live);

        void Push(Image gauge, float fill01, bool live)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, live ? readyColor : gaugeColor, k);
        }

        /// <summary>Re-anchor every gauge's rest scale after the base's upgrade handling (vessel contract rule 9).</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            var gauge = element switch
            {
                Element.Charge => lanceGauge,
                Element.Mass => nestGauge,
                Element.Space => arrayGauge,
                Element.Time => slingGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
