using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Thresher's HUD readouts (design: <c>R_VesselActions/THRESHER.md</c>). The row order IS
    /// the element map: <b>Wrecking Ball (charge) · Mass Mode (mass) · Winch (space) · Plant
    /// (time)</b>.
    ///
    /// <para>Two gauges, each answering the one question its trigger poses:</para>
    /// <list type="number">
    /// <item><b>Ball heat</b> (charge) — the ball's speed as a fraction of smash speed, in the
    /// ball's own colour (domain → red at smash, lit with the Charge upgrade), so the card and the
    /// ball in the world always agree.</item>
    /// <item><b>Banked energy</b> (space) — how much explosion RADIUS the chain's kills have banked
    /// for the ball's next blast (<c>ThresherExecutor.EnergyRadius01</c>: empty = a plain blast,
    /// full = the bank has bought the whole <c>energyMaxDiameterScale</c>). It eases from iron to
    /// the palette's lime as it fills (<c>colorBlendRate</c>) and empties the moment a blast spends
    /// it. (Until 2026-10-13 this card showed how far the chain was let out; the READY cue now
    /// lives on the chain alone.)</item>
    /// </list>
    /// Mass (the A toggle) and Time (a hold) are states with nothing a pilot waits on, so they get no gauge;
    /// Mass Mode and the chain's banked energy read on the ball itself (lit colour; a lime-leaning rim).
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only
    /// re-anchors its captured rest scales in <see cref="SetAbilityUpgraded"/>.
    /// </summary>
    public class ThresherHUDView : VesselHUDView
    {
        [Header("Ball heat (Charge row)")]
        [Tooltip("Filled Image on the Charge plate: the ball's speed / smash speed. The ability lockup " +
                 "re-homes and masks it to the plate at build time, so keep writing fillAmount on this object.")]
        [SerializeField] Image ballHeatGauge;

        [Header("Banked energy (Space row)")]
        [Tooltip("Filled Image on the Space plate: the explosion radius the chain's kills have banked for the " +
                 "ball's next blast. Iron when empty, lime when full.")]
        [FormerlySerializedAs("chainOutGauge")]
        [SerializeField] Image energyGauge;

        [Tooltip("Energy gauge colour when the bank is empty (it eases to the palette's lime as it fills).")]
        [SerializeField] Color chainColor = new(0.75f, 0.75f, 0.78f, 1f);
        [Tooltip("How fast the energy gauge eases to its colour (1/s, exponential). Matches " +
                 "ThresherConfigSO.colorBlendRate, so the card and the ball's rim change together.")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a vessel swap.</summary>
        public override void Initialize()
        {
            SetBallHeat(0f, Color.white);
            SetEnergy(0f, Color.white);
        }

        /// <summary>Push the ball's heat (speed / smash speed) and its current colour (gamma).</summary>
        public void SetBallHeat(float heat01, Color ballColor)
        {
            if (!ballHeatGauge) return;
            ballHeatGauge.fillAmount = Mathf.Clamp01(heat01);
            ballHeatGauge.color = ballColor;
        }

        /// <summary>Push the banked explosion radius (0..1) and the lime it fills toward (gamma).</summary>
        public void SetEnergy(float radius01, Color fullColor)
        {
            if (!energyGauge) return;
            float e = Mathf.Clamp01(radius01);
            energyGauge.fillAmount = e;
            Color target = Color.Lerp(chainColor, fullColor, e);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            energyGauge.color = Color.Lerp(energyGauge.color, target, k);
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
            if (element == Element.Space && energyGauge)
                energyGauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
