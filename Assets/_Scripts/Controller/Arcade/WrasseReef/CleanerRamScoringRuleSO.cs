using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cleaner Ram scoring - first DOMAIN to the ram target wins, on <see cref="ScoringMetric.CombatPoints"/>. One RAM -
    /// the Wrasse's charged kite reaching a rival hull - pays the rule's strike points.
    ///
    /// <para><b>Jackstraw's rule, reused (Kelpwood's shape).</b> A ram is already a Strike-class combat hit:
    /// <c>KiteNavigator.Steal</c> admits it through <c>CombatHitDrain.TryAdmit</c> (one per victim per
    /// <c>stealCooldown</c>) and raises it on the combat-hit channel, so it is COUNTED (<c>StrikeHitsLanded</c>) in every
    /// mode. <see cref="JackstrawScoringRuleSO"/> already pays exactly that, with every line an authored template, so
    /// Cleaner Ram inherits it and authors its own words on its own asset.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Cleaner Ram", fileName = "CleanerRamScoringRule")]
    public class CleanerRamScoringRuleSO : JackstrawScoringRuleSO
    {
    }
}
