using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-mode scoring strategy expressed as a ScriptableObject. One concrete asset per mode
    /// (HexRace / Joust / Crystal Capture) is dragged onto that mode's controller, which
    /// publishes it to <see cref="GameDataSO.ScoringRule"/>. Every shared scoring consumer -
    /// the network turn monitor (end condition + remaining), and (from later commits) the HUD,
    /// scoreboard and end-game cinematic - asks the rule instead of carrying per-mode forks.
    ///
    /// STATELESS: these assets are shared singletons, so a rule must be a pure function of the
    /// <see cref="GameDataSO"/> passed in - no per-game fields. The mode chooses its metric and
    /// golf/points style here; per-player formatting lives in the concrete rule (SRP).
    /// </summary>
    public abstract class ScoringRuleSO : ScriptableObject
    {
        [Header("Metric")]
        [Tooltip("The single per-player stat aggregated by Domain (drives HUD, remaining, end condition, scoreboard secondary).")]
        [SerializeField] protected ScoringMetric metric = ScoringMetric.Crystals;

        [Tooltip("Golf rules: lowest score wins (e.g. finish time). Off: highest score wins (e.g. points).")]
        [SerializeField] protected bool golfRules = true;

        public ScoringMetric Metric => metric;
        public bool GolfRules => golfRules;

        /// <summary>The metric value for one player - what the HUD card shows.</summary>
        public int LiveMetric(IRoundStats stats) => ScoringMetrics.Read(stats, metric);

        /// <summary>Remaining metric for a domain to reach the target (0 when met or for non-target modes).</summary>
        public virtual int Remaining(GameDataSO gameData, Domains domain) =>
            Mathf.Max(0, TargetCount(gameData) - ScoringMetrics.SumByDomain(gameData, metric, domain));

        /// <summary>
        /// What this mode pays for one landed vessel-vs-vessel hit. 0 - the default, and the
        /// answer in every mode but Dog Fight - means this mode does not score gunnery; the raw
        /// hit COUNTS still accumulate on <see cref="IRoundStats"/> either way, since they are
        /// a platform fact rather than a scoring opinion.
        ///
        /// Read once per hit on the server (<c>CombatHitScoring.Credit</c>), so the weighting
        /// lives in the mode's own asset instead of being baked into the metric reader - which
        /// is what lets <c>ScoringMetric.CombatPoints</c> stay a plain cumulative int.
        /// </summary>
        public virtual int PointsForCombatHit(CombatHitClass hitClass) => 0;

        /// <summary>
        /// Server-side END condition for the current turn. HexRace/Joust/CrystalCapture all end
        /// when an active domain's summed metric reaches the target; override for other shapes.
        /// </summary>
        public abstract bool IsObjectiveReached(GameDataSO gameData, out Domains winner);

        /// <summary>
        /// The winning domain at game end = the active domain with the highest metric sum.
        /// Ties break by <see cref="GameDataSO.ActiveDomains"/> order (Jade → Ruby → Gold), so
        /// identical inputs resolve identically on every machine.
        /// </summary>
        public virtual Domains ResolveWinner(GameDataSO gameData)
        {
            Domains best = Domains.Blue;
            int bestSum = -1;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int sum = ScoringMetrics.SumByDomain(gameData, metric, d);
                if (sum > bestSum)
                {
                    bestSum = sum;
                    best = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Full per-DOMAIN finishing order for the current game - the generalization of
        /// <see cref="ResolveWinner"/> to every place, ordered by summed metric (descending),
        /// ties broken by enum order (Jade → Ruby → Gold) so identical inputs resolve
        /// identically on every machine. Element 0 == <see cref="ResolveWinner"/>'s pick.
        /// Ranks every domain that actually fielded players (read from the synced
        /// <see cref="GameDataSO.RoundStatsList"/>), so it is valid on every peer once the
        /// mode's final-score ClientRpc has run. The Tournament/Shuffle fold and the
        /// Scoreboard's placement-crystal reward consume this - TEAM totals decide domain
        /// placement, never an individual player's rank.
        /// </summary>
        public virtual List<Domains> ResolvePlacementOrder(GameDataSO gameData)
        {
            var ordered = new List<Domains>();
            var list = gameData != null ? gameData.RoundStatsList : null;
            if (list == null) return ordered;

            for (int i = 0; i < list.Count; i++)
            {
                var stats = list[i];
                if (stats == null || stats.Domain == Domains.Blue) continue;   // Blue = no-team sentinel
                if (!ordered.Contains(stats.Domain)) ordered.Add(stats.Domain);
            }

            ordered.Sort((a, b) =>
            {
                int bySum = ScoringMetrics.SumByDomain(gameData, metric, b)
                    .CompareTo(ScoringMetrics.SumByDomain(gameData, metric, a));
                return bySum != 0 ? bySum : ((int)a).CompareTo((int)b);
            });
            return ordered;
        }

        /// <summary>Writes each player's <c>IRoundStats.Score</c> for the final ranking.</summary>
        public abstract void AssignScores(GameDataSO gameData, Domains winner, float finishTime);

        /// <summary>The single ranked, formatted results list every end-game surface reads.</summary>
        public abstract List<ScoreResult> BuildResults(GameDataSO gameData);

        /// <summary>The local player's cinematic reveal payload.</summary>
        public abstract ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin);

        /// <summary>The metric target that ends the game (0 for non-target modes).</summary>
        protected virtual int TargetCount(GameDataSO gameData) => 0;

        /// <summary>
        /// Absolute gap between the winning domain's metric sum and the best losing domain's -
        /// the "WON/LOST BY N" figure for the reveal.
        /// </summary>
        protected int DomainDelta(GameDataSO gameData)
        {
            int winnerSum = ScoringMetrics.SumByDomain(gameData, metric, gameData.WinnerDomain);
            int bestLosing = 0;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (d == gameData.WinnerDomain) continue;
                bestLosing = Mathf.Max(bestLosing, ScoringMetrics.SumByDomain(gameData, metric, d));
            }
            return Mathf.Abs(winnerSum - bestLosing);
        }
    }
}
