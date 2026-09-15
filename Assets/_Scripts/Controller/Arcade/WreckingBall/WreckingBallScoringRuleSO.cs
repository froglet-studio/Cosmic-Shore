using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wrecking Ball scoring: identical mechanics to Rampage's rule - metric
    /// <see cref="ScoringMetric.PrismsDestroyed"/> (hostile prisms only, so your own team's
    /// mass never scores), golf-timed (winners carry their finish time, losers a
    /// prisms-remaining sentinel), first domain to <see cref="GameDataSO.PrismTargetCount"/>
    /// wins - so it deliberately subclasses rather than re-implements. It exists as its own type
    /// so the mode owns its asset and its reveal wording.
    ///
    /// What makes the metric MEAN something here is upstream of the rule: a forged ball names
    /// its pilot as the attacker of every prism it eats (<c>AstroLeagueBall.PilotName</c>), and
    /// the cavitation plate already names its firer. Both land on the one stat this rule reads.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Wrecking Ball", fileName = "WreckingBallScoringRule")]
    public class WreckingBallScoringRuleSO : RampageScoringRuleSO
    {
        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "WRECKING TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "PRISMS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
