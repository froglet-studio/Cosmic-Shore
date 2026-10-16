using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Angler's HUD readouts (design: <c>R_VesselActions/ANGLER.md</c> §7). The row order is the fleet's
    /// <b>charge · mass · space · time</b>, and the approved map (2026-10-16, <c>ElementalAbilityMaps/Angler.asset</c>) fills it
    /// as <b>Erase / Lock-on · Lure, Tame &amp; Grow · Paint · Dash to the School</b>.
    ///
    /// <para>One gauge per card:</para>
    /// <list type="number">
    /// <item><b>Charge</b> - the share of the school hunting a locked vessel; READY while LT erases.</item>
    /// <item><b>Mass</b> - the furthest-along taming in the light; READY while the lure is out.</item>
    /// <item><b>Space</b> - the picked squad's share out on a painted order; READY while RT paints with something painted.</item>
    /// <item><b>Time</b> - full when the dash is ready (a school to dash to, no dash in flight); READY then.</item>
    /// </list>
    /// The gauges are named for their element; each keeps the serialized name it had in round 2 (FormerlySerializedAs),
    /// so the HUD prefab's wiring survives the rename. Colour here is a GAUGE channel; the upgrade lives on the lockup's
    /// card, so this view only re-anchors its captured rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class AnglerHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: Erase / Lock-on - the share of the school hunting a locked vessel.")]
        [SerializeField, FormerlySerializedAs("commandGauge")] Image chargeGauge;
        [Tooltip("Mass card: Lure, Tame & Grow - the furthest-along taming in the light.")]
        [SerializeField, FormerlySerializedAs("leashGauge")] Image massGauge;
        [Tooltip("Space card: Paint - the picked squad's share out on a painted order.")]
        [SerializeField, FormerlySerializedAs("lureGauge")] Image spaceGauge;
        [Tooltip("Time card: Dash to the School - full when the dash is ready.")]
        [SerializeField, FormerlySerializedAs("tameGauge")] Image timeGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("A card's ability is live or ready (erasing, the lure out, painting, the dash ready) - the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest (nothing tamed, the light off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(chargeGauge);
            Seat(massGauge);
            Seat(spaceGauge);
            Seat(timeGauge);
        }

        void Seat(Image gauge)
        {
            if (!gauge) return;
            gauge.fillAmount = 0f;
            gauge.color = gaugeColor;
        }

        public void SetCharge(float hunting01, bool erasing) => Push(chargeGauge, hunting01, erasing);
        public void SetMass(float tame01, bool luring) => Push(massGauge, tame01, luring);
        public void SetSpace(float ordered01, bool painting) => Push(spaceGauge, ordered01, painting);
        public void SetTime(float ready01, bool ready) => Push(timeGauge, ready01, ready);

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
                Element.Charge => chargeGauge,
                Element.Mass => massGauge,
                Element.Space => spaceGauge,
                Element.Time => timeGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
