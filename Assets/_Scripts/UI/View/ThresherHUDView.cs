using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Thresher's HUD readouts (design: <c>R_VesselActions/THRESHER.md</c>). The row order IS
    /// the element map: <b>Wrecking Ball (charge) · Heavy Iron (mass) · Winch (space) · Plant
    /// (time)</b>.
    ///
    /// <para>Two gauges, each answering the one question its trigger poses:</para>
    /// <list type="number">
    /// <item><b>Ball heat</b> (charge) — the ball's speed as a fraction of smash speed, in the
    /// ball's own colour (domain → red at smash, lit with the Charge upgrade), so the card and the
    /// ball in the world always agree.</item>
    /// <item><b>Chain out</b> (space) — how far the winch has let the chain out; it turns the
    /// palette's lime while releasing the right trigger NOW would crack the ball past smash speed
    /// (READY), the same cue the chain itself gives. Its colour EASES to lime and back
    /// (<c>colorBlendRate</c>), the same quick blend as the chain's — never a flicker.</item>
    /// </list>
    /// Mass and Time are passive/held states with nothing a pilot waits on, so they get no gauge.
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only
    /// re-anchors its captured rest scales in <see cref="SetAbilityUpgraded"/>.
    /// </summary>
    public class ThresherHUDView : VesselHUDView
    {
        [Header("Ball heat (Charge row)")]
        [Tooltip("Filled Image on the Charge plate: the ball's speed / smash speed. The ability lockup " +
                 "re-homes and masks it to the plate at build time, so keep writing fillAmount on this object.")]
        [SerializeField] Image ballHeatGauge;

        [Header("Chain out (Space row)")]
        [Tooltip("Filled Image on the Space plate: how far the chain is let out. Lime while READY.")]
        [SerializeField] Image chainOutGauge;

        [Tooltip("Chain-out gauge colour when not READY.")]
        [SerializeField] Color chainColor = new(0.75f, 0.75f, 0.78f, 1f);
        [Tooltip("How fast the chain-out gauge eases to lime at READY and back (1/s, exponential). Matches " +
                 "ThresherConfigSO.colorBlendRate, so the card and the chain in the world change together.")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a vessel swap.</summary>
        public override void Initialize()
        {
            SetBallHeat(0f, Color.white);
            SetChainOut(0f, false, Color.white);
        }

        /// <summary>Push the ball's heat (speed / smash speed) and its current colour (gamma).</summary>
        public void SetBallHeat(float heat01, Color ballColor)
        {
            if (!ballHeatGauge) return;
            ballHeatGauge.fillAmount = Mathf.Clamp01(heat01);
            ballHeatGauge.color = ballColor;
        }

        /// <summary>Push how far the chain is out, and whether a release now would smash.</summary>
        public void SetChainOut(float out01, bool ready, Color readyColor)
        {
            if (!chainOutGauge) return;
            chainOutGauge.fillAmount = Mathf.Clamp01(out01);
            Color target = ready ? readyColor : chainColor;
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            chainOutGauge.color = Color.Lerp(chainOutGauge.color, target, k);
        }

        /// <summary>
        /// Re-anchor the gauges' rest scales after the base's upgrade handling, or the next gauge
        /// write settles them back to the pre-upgrade scale and wipes the lockup's persistent bump
        /// (<c>SquirrelVesselHUDView</c> is the reference).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Charge && ballHeatGauge)
                ballHeatGauge.transform.localScale = AbilityIconRestScale(element);
            if (element == Element.Space && chainOutGauge)
                chainOutGauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
