using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction: SkimRace's golf-timed crystal race with two extra ways to LOSE. The first
    /// active domain whose summed crystals reach the target wins together and scores its
    /// finish time; a run the clock ends, or that the Rhino hunters end by eliminating every
    /// human, has NO winner - the controller reports <see cref="Domains.Blue"/> (the platform's
    /// "no team" sentinel) and every player scores the loser sentinel encoding their team's
    /// remaining crystals (golf: lower is better), so the scoreboard still ranks by how close
    /// each team got.
    ///
    /// The hunters are Blue and Blue is never in <see cref="GameDataSO.ActiveDomains"/>, so a
    /// crystal a hunter clips in passing can neither win nor count toward anyone's total.
    ///
    /// Target: <see cref="GameDataSO.CrystalTargetCount"/>, published by
    /// FrictionCrystalTurnMonitor (tool override, else 10/20/30/50 by intensity); 10 until the
    /// monitor has started, matching the intensity-1 table entry rather than SkimRace's 39.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Friction", fileName = "FrictionScoringRule")]
    public class FrictionScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) =>
            gameData.CrystalTargetCount > 0 ? gameData.CrystalTargetCount : 10;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (ScoringMetrics.SumByDomain(gameData, metric, d) >= target)
                {
                    winner = d;
                    return true;
                }
            }
            winner = Domains.Blue;
            return false;
        }

        /// <summary>
        /// Winners score their finish time, everyone else the crystals-left sentinel. Passing
        /// <see cref="Domains.Blue"/> as the winner is the no-winner case: no player is ever on
        /// Blue, so the whole roster takes the sentinel and the finish time is unused.
        /// </summary>
        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = winner != Domains.Blue && stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeSkimRaceLoserScore(Remaining(gameData, stats.Domain));
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CrystalsCollected);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Crystals Left",
                $"{LiveMetric(s)} Crystals")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        /// <summary>
        /// Three headlines, not two. A run nobody won is not a DEFEAT - nobody beat you - so it
        /// says what actually ended it: the hunters (this pilot was eliminated; a total wipe
        /// eliminates everyone, so the local reading is always right for the local player) or
        /// the clock. <c>IsEliminated</c> is a replicated RoundStats NetworkVariable, so the
        /// answer is the same on every peer.
        ///
        /// <paramref name="didWin"/> is only honoured while a winner was DECLARED. The generic
        /// callers derive it from "is my domain ranked first" when WinnerDomain is Blue, which
        /// in every other mode means "the mode never set it" - here it means nobody won, and
        /// the top-ranked team on a run the clock ended must not be told VICTORY.
        /// </summary>
        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            bool winnerDeclared = gameData.WinnerDomain != Domains.Blue;

            if (didWin && winnerDeclared)
                return new ScoreReveal("VICTORY", "RUN TIME", (int)localStats.Score, true);

            int crystalsLeft = Remaining(gameData, localStats.Domain);

            if (winnerDeclared)
                return new ScoreReveal("DEFEAT", "CRYSTALS LEFT", crystalsLeft, false);

            return localStats.IsEliminated
                ? new ScoreReveal("ELIMINATED", "CRYSTALS LEFT", crystalsLeft, false)
                : new ScoreReveal("TIME'S UP", "CRYSTALS LEFT", crystalsLeft, false);
        }
    }
}
