using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Riposte scoring - first DOMAIN to the strike target wins, on <see cref="ScoringMetric.CombatPoints"/>. One STRIKE -
    /// a Swordfish sword hit on a rival hull - pays the rule's strike points.
    ///
    /// <para><b>Jackstraw's rule, reused</b> (Kelpwood's pattern). A sword hit is already a Strike-class combat hit:
    /// <c>VesselCombatHitBySword</c> admits it (only while the Swordfish is faster than its victim) and raises it on the
    /// combat-hit channel, so it is COUNTED (<c>StrikeHitsLanded</c>) in every mode. <see cref="JackstrawScoringRuleSO"/>
    /// pays exactly that, with every line an authored template, so Riposte inherits it and authors its own words on its
    /// own asset.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Riposte", fileName = "RiposteScoringRule")]
    public class RiposteScoringRuleSO : JackstrawScoringRuleSO
    {
    }
}
