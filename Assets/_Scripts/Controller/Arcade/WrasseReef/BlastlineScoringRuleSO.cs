using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Blastline scoring - Rampage's mechanics (metric <see cref="ScoringMetric.PrismsDestroyed"/>, hostile prisms only,
    /// golf-timed, first domain to <see cref="GameDataSO.PrismTargetCount"/> wins) with every line an authored template:
    /// Harvest's rule, reused, with Blastline's words on its own asset.
    ///
    /// <para>What makes the metric mean something is upstream: the Space loop's blast is a real <c>AOEExplosion</c> that
    /// names the Wrasse's pilot (not anonymous), and the kite's head pops with <c>Prism.Damage(impact, own, name)</c>, so
    /// both land on the one stat this rule reads. A Mass cluster destroys nothing and scores nothing here.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Blastline", fileName = "BlastlineScoringRule")]
    public class BlastlineScoringRuleSO : HarvestScoringRuleSO
    {
    }
}
