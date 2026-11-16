using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Depth Charge scoring - first DOMAIN to the prism target wins, on <see cref="ScoringMetric.PrismsDestroyed"/>
    /// (hostile prisms only: your own colour never scores). Harvest's rule (itself Rampage's mechanics) reused, with
    /// Depth Charge's own words on its own asset - Kelpwood's pattern.
    ///
    /// <para>What makes the metric mean something is upstream of the rule: the Orca's missile and bomb bursts are
    /// <c>AOEExplosion</c>s that name its pilot as the attacker, the sniper's line damages every hostile prism on it in the
    /// pilot's name, and the fast gun's rounds carry their own impact container. All of it lands on the one stat this rule
    /// reads.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Depth Charge", fileName = "DepthChargeScoringRule")]
    public class DepthChargeScoringRuleSO : HarvestScoringRuleSO
    {
    }
}
