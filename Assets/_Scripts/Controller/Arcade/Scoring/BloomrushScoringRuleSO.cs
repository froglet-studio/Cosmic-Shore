using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bloomrush: the Manta party game — tag everything you fly past, then reach a crystal
    /// before the fuses burn down and set it all off at once. The FIRST timed highest-score
    /// mode in this family: the round ends when the clock does (a scene-authored
    /// <c>NetworkTimeBasedTurnMonitor</c>), never on a target, so
    /// <see cref="IsObjectiveReached"/> is a permanent no.
    ///
    /// Metric = <see cref="ScoringMetric.VolumeDestroyed"/> — hostile prism VOLUME, because
    /// the Manta's whole kit is about volume, and because it makes the spec's side objective
    /// free: a crystal-cashed bloom is authored BIGGER than a fuse fizzle, so "bombs that
    /// time out score only a fraction of a cashed bloom" is a blast-size fact, not a scoring
    /// special case.
    ///
    /// Tiebreaker = FUSES BEATEN (<see cref="IRoundStats.FusesBeaten"/>, domain-summed) — the
    /// Time element's identity as a stat: more Time means more targets strung together before
    /// the fuses burn down. Enum order (Jade → Ruby → Gold) stays the deterministic last
    /// resort, as everywhere.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Bloomrush", fileName = "BloomrushScoringRule")]
    public class BloomrushScoringRuleSO : ScoringRuleSO
    {
        static int SumFusesBeaten(GameDataSO gameData, Domains domain)
        {
            int sum = 0;
            var list = gameData.RoundStatsList;
            for (int i = 0, n = list.Count; i < n; i++)
            {
                var stats = list[i];
                if (stats != null && stats.Domain == domain)
                    sum += stats.FusesBeaten;
            }
            return sum;
        }

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            // Timed mode: only the clock ends the turn. The time monitor is the sole monitor
            // in the scene, so returning false here never stalls the end condition.
            winner = Domains.Blue;
            return false;
        }

        public override Domains ResolveWinner(GameDataSO gameData)
        {
            Domains best = Domains.Blue;
            int bestVolume = -1, bestFuses = -1;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int volume = ScoringMetrics.SumByDomain(gameData, metric, d);
                int fuses = SumFusesBeaten(gameData, d);
                // Strict > with enum-order iteration = the documented Jade → Ruby → Gold last
                // resort, identical on every machine.
                if (volume > bestVolume || (volume == bestVolume && fuses > bestFuses))
                {
                    best = d;
                    bestVolume = volume;
                    bestFuses = fuses;
                }
            }
            return best;
        }

        public override List<Domains> ResolvePlacementOrder(GameDataSO gameData)
        {
            var ordered = new List<Domains>();
            var list = gameData != null ? gameData.RoundStatsList : null;
            if (list == null) return ordered;

            for (int i = 0; i < list.Count; i++)
            {
                var stats = list[i];
                if (stats == null || stats.Domain == Domains.Blue) continue;
                if (!ordered.Contains(stats.Domain)) ordered.Add(stats.Domain);
            }

            ordered.Sort((a, b) =>
            {
                int byVolume = ScoringMetrics.SumByDomain(gameData, metric, b)
                    .CompareTo(ScoringMetrics.SumByDomain(gameData, metric, a));
                if (byVolume != 0) return byVolume;
                int byFuses = SumFusesBeaten(gameData, b).CompareTo(SumFusesBeaten(gameData, a));
                return byFuses != 0 ? byFuses : ((int)a).CompareTo((int)b);
            });
            return ordered;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            // Points mode: every pilot carries their own bloomed volume. Higher is better,
            // no golf sentinels — the team result folds through the domain sums.
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                stats.Score = stats.HostileVolumeDestroyed;
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // TEAM-major: rows group by domain placement, teammates ordered by their own
            // volume, name as the final tiebreak so every peer builds an identical list.
            var placement = ResolvePlacementOrder(gameData);
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => { int i = placement.IndexOf(s.Domain); return i < 0 ? int.MaxValue : i; })
                .ThenByDescending(s => s.HostileVolumeDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{LiveMetric(s)} Volume",
                // The breakdown: how much of the board they cashed vs let burn down.
                $"{s.FusesBeaten} fuses beaten")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "VOLUME BLOOMED", LiveMetric(localStats), true)
                : new ScoreReveal("DEFEAT", "VOLUME BLOOMED", LiveMetric(localStats), false);
    }
}
