using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Angler's HUD readouts (design: <c>R_VesselActions/ANGLER.md</c> §4). The row order is the fleet's
    /// <b>charge · mass · space · time</b>; the Angler's PROPOSED abilities sit in it as <b>Command · Leash · Lure · Tame</b>.
    /// The element mapping is a proposal awaiting the designer (no <c>ElementalAbilityMaps/Angler.asset</c> is authored,
    /// vessel skill §3), so the row's order is the only place it shows.
    ///
    /// <para>One gauge per card:</para>
    /// <list type="number">
    /// <item><b>Command</b> — the picked squad's share that is out hunting (thrown or eating its order); the READY colour
    /// while RT is painting and something is painted.</item>
    /// <item><b>Leash</b> — the school's size against the leash's capacity.</item>
    /// <item><b>Lure</b> — the lure light, a switch eased 0..1; READY while it is out.</item>
    /// <item><b>Tame</b> — the furthest-along taming in the light.</item>
    /// </list>
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only re-anchors its captured
    /// rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class AnglerHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: Command - the picked squad's share out hunting.")]
        [SerializeField] Image commandGauge;
        [Tooltip("Mass card: Leash - the school against the leash's capacity.")]
        [SerializeField] Image leashGauge;
        [Tooltip("Space card: Lure - the light, eased on and off.")]
        [SerializeField] Image lureGauge;
        [Tooltip("Time card: Tame - the furthest-along taming in the light.")]
        [SerializeField] Image tameGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("Painting with something painted (Command) and the lure out (Lure) - the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest (nothing tamed, the light off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(commandGauge);
            Seat(leashGauge);
            Seat(lureGauge);
            Seat(tameGauge);
        }

        void Seat(Image gauge)
        {
            if (!gauge) return;
            gauge.fillAmount = 0f;
            gauge.color = gaugeColor;
        }

        public void SetCommand(float hunting01, bool painting) => Push(commandGauge, hunting01, painting);
        public void SetLeash(float school01) => Push(leashGauge, school01, false);
        public void SetLure(float on01, bool out_) => Push(lureGauge, on01, out_);
        public void SetTame(float tame01) => Push(tameGauge, tame01, tame01 >= 1f);

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
                Element.Charge => commandGauge,
                Element.Mass => leashGauge,
                Element.Space => lureGauge,
                Element.Time => tameGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
