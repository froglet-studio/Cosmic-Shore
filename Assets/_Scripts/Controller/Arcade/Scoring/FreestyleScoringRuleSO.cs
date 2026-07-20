using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Multiplayer Freestyle - the scoreless sandbox ("No rules, time, or score"). The rule
    /// exists so the mode's live centerline feed (trail volume created) is config-driven like
    /// every other mode, not because freestyle has an objective: the sandbox never ends
    /// (<see cref="IsObjectiveReached"/> is always false), no scores are assigned, and no
    /// results are ever built - players leave via the Main Menu button.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Freestyle", fileName = "FreestyleScoringRule")]
    public class FreestyleScoringRuleSO : ScoringRuleSO
    {
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            // Sandbox: Score is the live metric feed; there is no end state to assign for.
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData) => new();

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            new ScoreReveal("FREESTYLE", "", (int)localStats.Score, false);
    }
}
