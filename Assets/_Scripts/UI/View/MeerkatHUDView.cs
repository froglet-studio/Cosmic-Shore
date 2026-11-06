using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Meerkat's HUD readouts (<c>R_VesselActions/MEERKAT.md</c> §10, Garrett's kit approved by Yash 2026-10-28). The row
    /// order IS the element map: <b>Sentinel Mode (charge) · Exit Portal (mass) · Soliton (space) · Faster Ride (time)</b>.
    ///
    /// <para>One plate gauge per card, each answering the one question its element poses:</para>
    /// <list type="number">
    /// <item><b>Sentinels</b> (charge): how many sentinels stand, out of the pool (3, 6 at L5), in the ready colour while
    /// Sentinel Mode is on, so Y's toggle reads even with no sentinel standing yet.</item>
    /// <item><b>Portal</b> (mass): how many of the last exit portal's six prisms still stand.</item>
    /// <item><b>Soliton</b> (space): the reticle growing back after a ride, in the ready colour once RT would form.</item>
    /// <item><b>Ride</b> (time): what is left of the ride through a soliton, in the ready colour while riding.</item>
    /// </list>
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only re-anchors its captured rest
    /// scales in <see cref="SetAbilityUpgraded"/> (<c>StoatHUDView</c>, whose HUD the Meerkat wore until 2026-11-03).
    /// </summary>
    public class MeerkatHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the sentinels standing, as a share of the pool.")]
        [SerializeField] Image sentinelGauge;
        [Tooltip("Mass card: the last exit portal's prisms still standing, as a share of six.")]
        [SerializeField] Image portalGauge;
        [Tooltip("Space card: the reticle's growth; full once RT would form a soliton.")]
        [SerializeField] Image solitonGauge;
        [Tooltip("Time card: what is left of the ride through a soliton.")]
        [SerializeField] Image rideGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("Sentinel Mode on, a soliton RT would form, and the ride under way.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a vessel swap.</summary>
        public override void Initialize()
        {
            Seat(sentinelGauge);
            Seat(portalGauge);
            Seat(solitonGauge);
            Seat(rideGauge);
        }

        void Seat(Image gauge)
        {
            if (!gauge) return;
            gauge.fillAmount = 0f;
            gauge.color = gaugeColor;
        }

        /// <summary>The Charge card: <paramref name="standing01"/> of the pool, in the ready colour while <paramref name="on"/>.</summary>
        public void SetSentinels(float standing01, bool on) => Push(sentinelGauge, standing01, on);

        /// <summary>The Mass card: the share of the last portal's prisms still standing.</summary>
        public void SetPortal(float standing01) => Push(portalGauge, standing01, false);

        /// <summary>The Space card: the reticle's growth, in the ready colour once <paramref name="canForm"/>.</summary>
        public void SetSoliton(float ready01, bool canForm) => Push(solitonGauge, ready01, canForm);

        /// <summary>The Time card: what is left of the ride, in the ready colour while <paramref name="riding"/>.</summary>
        public void SetRide(float left01, bool riding) => Push(rideGauge, left01, riding);

        void Push(Image gauge, float fill01, bool ready)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, ready ? readyColor : gaugeColor, k);
        }

        /// <summary>
        /// Re-anchor every gauge's rest scale after the base's upgrade handling, or the next gauge write settles it back
        /// to the pre-upgrade scale and wipes the lockup's persistent bump (vessel contract rule 9).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            var gauge = element switch
            {
                Element.Charge => sentinelGauge,
                Element.Mass => portalGauge,
                Element.Space => solitonGauge,
                Element.Time => rideGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
