using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Kelpwood scoring - first DOMAIN to the bite target wins, on <see cref="ScoringMetric.CombatPoints"/>.
    /// One BITE - a creature an Angler locked on a rival reaching that rival's hull - pays the rule's strike points.
    ///
    /// <para><b>Jackstraw's rule, reused.</b> A bite is already a Strike-class combat hit: the Angler's
    /// <c>AnglerCommandExecutor.Bite</c> admits it through <c>CombatHitDrain.TryAdmit</c> (one per victim per
    /// <c>lockBiteCooldown</c>, 1 s) and raises it on the combat-hit channel, so it is COUNTED
    /// (<c>StrikeHitsLanded</c>) in every mode. <see cref="JackstrawScoringRuleSO"/> already pays exactly that - a
    /// Strike for its points, every other class nothing - with every line an authored template, so Kelpwood inherits
    /// it and authors its own words on its own asset. Nothing here is Thresher-specific but the base's tooltips.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Kelpwood", fileName = "KelpwoodScoringRule")]
    public class KelpwoodScoringRuleSO : JackstrawScoringRuleSO
    {
    }
}
