using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Salvo: the Sparrow demolition race. Identical scoring shape to Rampage - the metric is
    /// <see cref="ScoringMetric.PrismsDestroyed"/> against <see cref="GameDataSO.PrismTargetCount"/>
    /// (authored via FrogletTools &gt; Game Modes &gt; End Game Conditions), golf-timed: the
    /// winning domain's pilots score their finish time, everyone else a sentinel encoding
    /// their team's remaining prisms. The only override is the end-game reveal's label - a
    /// player should read "SALVO TIME", not another mode's name.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Salvo", fileName = "SalvoScoringRule")]
    public class SalvoScoringRuleSO : RampageScoringRuleSO
    {
        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "SALVO TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "PRISMS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
