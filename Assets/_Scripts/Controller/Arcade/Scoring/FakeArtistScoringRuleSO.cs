using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fake Artist: free-for-all points, not golf, and the winner is an individual
    /// PLAYER, never a domain (12 players share 6 paint colors, so domain sums are
    /// meaningless - <see cref="UsesPerPlayerWinner"/> switches the shared end-game
    /// surfaces to the per-player path). Points accumulate on <c>GoalsScored</c> across
    /// rounds (+1 correct subject, +1 correct accusation, -1 when accused, +4 for the
    /// fake artist - all authored on FakeArtistConfig.asset); the game ends when any
    /// player reaches <see cref="GameDataSO.GoalTargetCount"/> (default 8, authored via
    /// Tools &gt; Cosmic Shore &gt; End Game Conditions).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/FakeArtist", fileName = "FakeArtistScoringRule")]
    public class FakeArtistScoringRuleSO : ScoringRuleSO
    {
        public override bool UsesPerPlayerWinner => true;

        protected override int TargetCount(GameDataSO gameData) => gameData.GoalTargetCount;

        /// <summary>
        /// PER-PLAYER end condition: first player at/above the point target. Never uses
        /// SumByDomain - multiple FFA players share a domain and the sum would trip early.
        /// Deterministic tie-break: most points, then name ordinal.
        /// </summary>
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;
            int target = TargetCount(gameData);
            if (target <= 0) return false; // target not synced yet - never end on 0

            var best = BestPlayer(gameData);
            if (best == null || best.GoalsScored < target) return false;

            winner = best.Domain;
            return true;
        }

        /// <summary>The local player's personal deficit (the base sums by domain).</summary>
        public override int Remaining(GameDataSO gameData, Domains domain)
        {
            int target = TargetCount(gameData);
            var local = gameData.LocalRoundStats;
            if (target <= 0 || local == null) return 0;
            return Mathf.Max(0, target - local.GoalsScored);
        }

        /// <summary>Best individual player's paint domain (shared surfaces expect a Domains).</summary>
        public override Domains ResolveWinner(GameDataSO gameData)
        {
            var best = BestPlayer(gameData);
            return best?.Domain ?? Domains.Blue;
        }

        public static IRoundStats BestPlayer(GameDataSO gameData)
        {
            var list = gameData != null ? gameData.RoundStatsList : null;
            if (list == null || list.Count == 0) return null;
            return list
                .Where(s => s != null)
                .OrderByDescending(s => s.GoalsScored)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.GoalsScored;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Name, StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Point{((int)s.Score == 1 ? "" : "s")}",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                didWin ? "MASTERPIECE" : $"{gameData.WinnerName} TAKES THE GALLERY",
                (int)localStats.Score,
                false);
        }
    }
}
