using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rampage: points (not golf) - the destructive analog of Crystal Capture ("Scurry").
    /// The turn ends (<c>RampagePrismTurnMonitor</c>) when an active domain's summed
    /// <see cref="IRoundStats.HostilePrismsDestroyed"/> reaches the target
    /// (<see cref="GameDataSO.PrismTargetCount"/>, authored via Tools &gt; Cosmic Shore &gt;
    /// End Game Conditions); the winning domain is the highest destruction sum. Each player's
    /// score IS their hostile-prism count - higher is better. Scoring mass = ALL environment
    /// mass (flora/fauna, any color - StatsManager classifies non-roster-owned prisms hostile)
    /// plus OTHER domains' player-laid trails; your own/teammates' trails never score (the
    /// roster domain check filters them), so lay-and-shatter farming is worthless by
    /// construction. Results rank TEAM-major, matching Crystal Capture: every winning-domain
    /// player above every losing-domain player, domains by summed prisms destroyed.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Rampage", fileName = "RampageScoringRule")]
    public class RampageScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.PrismTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0,
                // or the turn would finish the instant the first prism shatters.
                winner = Domains.Blue;
                return false;
            }

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

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.HostilePrismsDestroyed;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // TEAM-major: domains by their placement (summed prisms destroyed, the same order
            // the Tournament fold + placement crystals use), then teammates by individual
            // count. Name is the final tiebreak so every peer produces an identical list even
            // when two players tie (List order alone is not identical across peers).
            var placement = ResolvePlacementOrder(gameData);
            var ordered = gameData.RoundStatsList
                .OrderBy(s => { int i = placement.IndexOf(s.Domain); return i < 0 ? int.MaxValue : i; })
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Prisms Smashed",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} PRISM{(diff == 1 ? "" : "S")}",
                (int)localStats.Score,
                false);
        }
    }
}
