using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Trawl scoring - Harvest's rule, reused (Rampage's mechanics underneath): metric
    /// <see cref="ScoringMetric.PrismsDestroyed"/> (hostile prisms only, so your own colour never scores), golf-timed,
    /// first domain to <see cref="GameDataSO.PrismTargetCount"/> wins, every line an authored template. Trawl authors its
    /// own words on its own asset (Kelpwood's shape over Jackstraw's rule).
    ///
    /// <para>What makes the metric mean something is upstream of the rule: a prism the Jacks' net drags that runs into
    /// another destroys both, scored to the net's pilot (<c>PrismMotion.Annihilate</c> names it as the attacker,
    /// <c>Docs/PRISM_MOTION.md</c>), so both land on the one stat this rule reads. The netting hull itself rams nothing
    /// (<c>IHullContactGate</c>), and a shielded prism bounces instead of breaking, so the net is the only way to score.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Trawl", fileName = "TrawlScoringRule")]
    public class TrawlScoringRuleSO : HarvestScoringRuleSO
    {
    }
}
