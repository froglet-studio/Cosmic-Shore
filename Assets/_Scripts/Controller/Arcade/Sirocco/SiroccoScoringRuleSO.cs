using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sirocco scoring - Rampage's rule on <see cref="ScoringMetric.PrismsDestroyed"/>, inherited
    /// whole (first DOMAIN to <see cref="GameDataSO.PrismTargetCount"/> hostile prisms, golf-timed,
    /// team-major results). Only the reveal wording is the mode's own, the Wrecking Ball shape.
    ///
    /// <para>What counts is exactly what Rampage counts, which is the point of reusing it: every
    /// prism the Butterfly's dust DESTROYS (one outcome in three on opposing mass - the others
    /// shrink it or steal it) and is not wearing the pilot's own colour. A shrunk prism can be
    /// dusted again and re-rolls, because the dust's roll hashes the prism's size; a stolen prism
    /// is yours from then on and the dust TENDS it instead. So a pass over a stand of opposing
    /// forest destroys a third of it and leaves the rest for the next pass - erosion, not
    /// demolition.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Sirocco", fileName = "SiroccoScoringRule")]
    public class SiroccoScoringRuleSO : RampageScoringRuleSO
    {
        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "SIROCCO TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "PRISMS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
