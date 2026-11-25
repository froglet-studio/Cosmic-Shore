using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flitstrike scoring - first DOMAIN to the strike target wins, on <see cref="ScoringMetric.CombatPoints"/>. One
    /// STRIKE - a Honeyguide's Flit (or a Charge upgrade riding its trigger: the lasso, Blink Drive, the portals, Rosen
    /// Bridge, the FTL Chromatool, Monte Cannon) reaching a rival hull - pays the rule's strike points.
    ///
    /// <para><b>Jackstraw's rule, reused (Kelpwood's precedent).</b> Every Honeyguide strike on a vessel is already a
    /// Strike-class combat hit: <c>HoneyguideCrew.StrikeVessel</c> admits it through <c>CombatHitDrain.TryAdmit</c> and
    /// raises it on the combat-hit channel, so it is COUNTED (<c>StrikeHitsLanded</c>) in every mode.
    /// <see cref="JackstrawScoringRuleSO"/> pays exactly that - a Strike for its points, every other class nothing (so
    /// the Peck gun's rounds never score here) - with every line an authored template, so Flitstrike inherits it and
    /// authors its own words on its own asset.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Flitstrike", fileName = "FlitstrikeScoringRule")]
    public class FlitstrikeScoringRuleSO : JackstrawScoringRuleSO
    {
    }
}
