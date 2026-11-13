using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
    /// it. This gauge is the only place the lime shows: the chain's READY cue and the crosshair's
    /// timing cue were removed on 2026-10-20, left to feel.</item>
    /// </list>
    /// The four cards are drawn in parts (<c>Tools/Build/icon_kit/specs/thresher.py</c>) that
    /// <see cref="RefreshParts"/> paints from the ball's live state: hot, Mass Mode, the chain's rigidity, planted.
    /// Mass (the A toggle) and Time (a hold) are states with nothing a pilot waits on, so they get no gauge;
    /// Mass Mode and the chain's banked energy read on the ball itself (lit colour; a rim burning toward white).
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
                 "ball's next blast. Iron when empty, the pilot's domain when full.")]
        [FormerlySerializedAs("chainOutGauge")]
        [SerializeField] Image energyGauge;

        [Tooltip("Energy gauge colour when the bank is empty (it eases to the pilot's domain as it fills).")]
        [SerializeField] Color chainColor = new(0.75f, 0.75f, 0.78f, 1f);
        [Tooltip("How fast the energy gauge eases to its colour (1/s, exponential). Matches " +
                 "ThresherConfigSO.colorBlendRate, so the card and the ball's rim change together.")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        [Header("Ability parts (icon kit: Tools/Build/icon_kit/specs/thresher.py)")]
        [Tooltip("Charge card: the shark towing (domain).")]
        [SerializeField] Image wreckSharkIcon;
        [Tooltip("Charge card: the chain (white).")]
        [SerializeField] Image wreckChainIcon;
        [Tooltip("Charge card: the ball - white cruising, danger at smash speed.")]
        [SerializeField] Image wreckBallIcon;
        [Tooltip("Charge card: the rival prism ahead of the ball (danger), standing while the ball is cool.")]
        [SerializeField] Image wreckPrismIcon;
        [Tooltip("Charge card: the prism split by a smash (danger).")]
        [SerializeField] Image wreckSplitIcon;
        [Tooltip("Charge card: the smash's blast (white).")]
        [SerializeField] Image wreckBlastIcon;
        [Tooltip("Charge card, Magnet (Charge 5): the field drawing the ball (white).")]
        [SerializeField] Image wreckMagnetIcon;
        [Tooltip("Mass card: the shark towing (domain).")]
        [SerializeField] Image massSharkIcon;
        [Tooltip("Mass card: the chain (white).")]
        [SerializeField] Image massChainIcon;
        [Tooltip("Mass card: the ball - domain in Mass Mode, white breaking.")]
        [SerializeField] Image massBallIcon;
        [Tooltip("Mass card: the prisms the ball lays (domain; faint off Mass Mode).")]
        [SerializeField] Image massRowIcon;
        [Tooltip("Mass card, Armoured Trail (Mass 5): the row's shield rims (white).")]
        [SerializeField] Image massShieldIcon;
        [Tooltip("Space card: the shark (domain).")]
        [SerializeField] Image winchSharkIcon;
        [Tooltip("Space card: the chain slack, sagging (white, faint) - RT released.")]
        [SerializeField] Image winchSlackIcon;
        [Tooltip("Space card: the chain pulled rigid (white) - RT pulled.")]
        [SerializeField] Image winchRigidIcon;
        [Tooltip("Space card: the ball (white).")]
        [SerializeField] Image winchBallIcon;
        [Tooltip("Space card: the ball's swing while the chain is rigid (white).")]
        [SerializeField] Image winchSwingIcon;
        [Tooltip("Space card, Breaker (Space 5): a super-shielded prism in the ball's path (white).")]
        [SerializeField] Image winchBreakerIcon;
        [Tooltip("Time card: the shark on its orbit (domain).")]
        [SerializeField] Image plantSharkIcon;
        [Tooltip("Time card: the orbit (white; faint flying free).")]
        [SerializeField] Image plantOrbitIcon;
        [Tooltip("Time card: the chain to the pivot (white; planted).")]
        [SerializeField] Image plantChainIcon;
        [Tooltip("Time card: the ball stopped as a pivot (white).")]
        [SerializeField] Image plantBallIcon;
        [Tooltip("Time card, Anchor Ward (Time 5): the ward round the planted orbit (white).")]
        [SerializeField] Image plantWardIcon;
        [Tooltip("Time card, Anchor Ward (Time 5): the danger prism the ward turns away (danger).")]
        [SerializeField] Image plantDangerIcon;
        [Tooltip("Rival colour on the parts (the fleet danger orange).")]
        [SerializeField] Color dangerColor = new(1f, 0.3294f, 0.1020f, 1f);
        [Tooltip("Alpha of a part that is there but not in play (the Mass row off Mass Mode, the slack chain, the free orbit).")]
        [SerializeField, Range(0f, 1f)] float idlePartAlpha = 0.35f;
        [Tooltip("Trigger depth above which the Winch card shows the chain rigid.")]
        [SerializeField, Range(0f, 1f)] float rigidAbove = 0.5f;

        Color _domain = Color.white;
        bool _hot;
        bool _massMode = true;
        bool _rigid;
        bool _planted;

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a vessel swap.</summary>
        public override void Initialize()
        {
            SetBallHeat(0f, Color.white);
            SetEnergy(0f, Color.white);
            _hot = false;
            _massMode = true;   // the hull spawns in Mass Mode (ThresherExecutor.Initialize)
            _rigid = false;
            _planted = false;
            RefreshParts();
        }

        /// <summary>The pilot's domain colour (alpha 0 = keep the last); every shark on the cards wears it.</summary>
        public void SetDomainColor(Color domain)
        {
            if (domain.a <= 0f || domain == _domain) return;
            _domain = domain;
            RefreshParts();
        }

        /// <summary>The ball's live state: at smash speed, in Mass Mode, the chain pulled rigid (RT depth), planted (LT).</summary>
        public void SetBallState(bool hot, bool massMode, float rigidity01, bool planted)
        {
            bool rigid = rigidity01 >= rigidAbove;
            if (hot == _hot && massMode == _massMode && rigid == _rigid && planted == _planted) return;
            _hot = hot;
            _massMode = massMode;
            _rigid = rigid;
            _planted = planted;
            RefreshParts();
        }

        void RefreshParts()
        {
            // Mass Mode never smashes (THRESHER.md 2b), so the Charge card is cool there too.
            bool smash = _hot && !_massMode;
            var danger = ColorTrace.Retrace(ColorTrace.Ui, dangerColor);
            Paint(wreckSharkIcon, _domain, 1f);
            Paint(wreckChainIcon, Color.white, 1f);
            Paint(wreckBallIcon, smash ? danger : Color.white, 1f);
            Paint(wreckPrismIcon, danger, smash ? 0f : 1f);
            Paint(wreckSplitIcon, danger, smash ? 1f : 0f);
            Paint(wreckBlastIcon, Color.white, smash ? 1f : 0f);
            Paint(wreckMagnetIcon, Color.white, IsAbilityUpgraded(Element.Charge) ? 1f : 0f);

            Paint(massSharkIcon, _domain, 1f);
            Paint(massChainIcon, Color.white, 1f);
            Paint(massBallIcon, _massMode ? _domain : Color.white, 1f);
            Paint(massRowIcon, _domain, _massMode ? 1f : idlePartAlpha);
            Paint(massShieldIcon, Color.white, _massMode && IsAbilityUpgraded(Element.Mass) ? 1f : 0f);

            Paint(winchSharkIcon, _domain, 1f);
            Paint(winchSlackIcon, Color.white, _rigid ? 0f : 0.7f);
            Paint(winchRigidIcon, Color.white, _rigid ? 1f : 0f);
            Paint(winchBallIcon, Color.white, 1f);
            Paint(winchSwingIcon, Color.white, _rigid ? 1f : 0f);
            Paint(winchBreakerIcon, Color.white, IsAbilityUpgraded(Element.Space) ? 1f : 0f);

            Paint(plantSharkIcon, _domain, 1f);
            Paint(plantOrbitIcon, Color.white, _planted ? 1f : idlePartAlpha);
            Paint(plantChainIcon, Color.white, _planted ? 1f : 0f);
            Paint(plantBallIcon, Color.white, 1f);
            bool ward = _planted && IsAbilityUpgraded(Element.Time);
            Paint(plantWardIcon, Color.white, ward ? 1f : 0f);
            Paint(plantDangerIcon, danger, ward ? 1f : 0f);
        }

        static void Paint(Image part, Color color, float alpha)
        {
            if (!part) return;
            var c = new Color(color.r, color.g, color.b, color.a * alpha);
            if (part.color != c) part.color = c;
        }

        /// <summary>Push the ball's heat (speed / smash speed) and its current colour (gamma).</summary>
        public void SetBallHeat(float heat01, Color ballColor)
        {
            if (!ballHeatGauge) return;
            ballHeatGauge.fillAmount = Mathf.Clamp01(heat01);
            ballHeatGauge.color = ballColor;
        }

        /// <summary>Push the banked explosion radius (0..1) and the colour it fills toward (gamma): the pilot's domain.
        /// It was the palette's lime (the CTA colour) until the 2026-11-07 icon pass; the CTA lime is never a card's
        /// "on" colour (Garrett, 2026-11-01).</summary>
        public void SetEnergy(float radius01, Color fullColor)
        {
            if (!energyGauge) return;
            float e = Mathf.Clamp01(radius01);
            energyGauge.fillAmount = e;
            Color target = Color.Lerp(ColorTrace.Retrace(ColorTrace.Ui, chainColor), fullColor, e);
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
            RefreshParts();
            if (element == Element.Charge && ballHeatGauge)
                ballHeatGauge.transform.localScale = AbilityIconRestScale(element);
            if (element == Element.Space && energyGauge)
                energyGauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
