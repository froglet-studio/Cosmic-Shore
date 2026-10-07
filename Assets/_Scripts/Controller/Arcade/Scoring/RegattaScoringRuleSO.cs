using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta's TEAM race: a team's score is the SUM of every gate its pilots have threaded,
    /// and the highest team total wins.
    ///
    /// <para><b>What changes from the gate-race rule it extends.</b> <see cref="GateRaceScoringRuleSO"/>
    /// folds a domain by its LEAD RUNNER, so only the fastest pilot ever counted and a slow
    /// teammate's laps were worth nothing. Here every pilot's laps count: <see cref="DomainValue"/>
    /// is <see cref="ScoringMetrics.SumByDomain"/>, and because that is the one seam every domain
    /// reader goes through, the HUD's domain boxes, the comeback deficit, the placement order and
    /// the winner all read the team total together.</para>
    ///
    /// <para><b>When the race ends.</b> The moment ANY pilot threads the last gate of the last
    /// lap - the course length is unchanged, so a race is still three laps long. Ending on a team
    /// total reaching some target would make the race length a function of the team size; ending
    /// on the first finisher keeps one clock for everyone, and the team total at that moment
    /// decides it. A tie goes to the team that put the finisher across (they got there first).</para>
    ///
    /// <para><b>Points, not golf.</b> Each pilot's Score is their own gate count and higher wins,
    /// so the end-game domain totals (<c>GameDataSO.CalculateDomainStats</c>, which sums Score)
    /// read exactly the team sum this rule decided on. <c>RegattaController</c> runs points rules
    /// to match.</para>
    ///
    /// <para>Team SIZE is not normalised: a team with more pilots has more laps to sum. The host
    /// shapes the teams on the launch panel (Add AI), which is where that balance is set.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Regatta", fileName = "RegattaScoringRule")]
    public class RegattaScoringRuleSO : GateRaceScoringRuleSO
    {
        /// <summary>A team's score: every pilot's gates, summed.</summary>
        public override int DomainValue(GameDataSO gameData, Domains domain) =>
            ScoringMetrics.SumByDomain(gameData, metric, domain);

        /// <summary>
        /// Course left for the domain's LEAD RUNNER - "how far is this team from ending the
        /// race". The base reading (target minus the team SUM) means nothing once the fold is a
        /// sum, and nothing here may read it.
        /// </summary>
        public override int Remaining(GameDataSO gameData, Domains domain) =>
            Mathf.Max(0, TargetCount(gameData) - ScoringMetrics.BestByDomain(gameData, metric, domain));

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;

            // Never end on a target of 0 - see GateRaceScoringRuleSO.IsObjectiveReached.
            int target = TargetCount(gameData);
            if (target <= 0) return false;

            if (!TryFindFinisherDomain(gameData, target, out var finisher)) return false;

            winner = ResolveTeamWinner(gameData, finisher);
            return true;
        }

        public override Domains ResolveWinner(GameDataSO gameData)
        {
            int target = TargetCount(gameData);
            var finisher = target > 0 && TryFindFinisherDomain(gameData, target, out var d) ? d : Domains.Blue;
            return ResolveTeamWinner(gameData, finisher);
        }

        /// <summary>
        /// The first active domain (Jade → Ruby → Gold) with a pilot who has flown the whole
        /// course. Only one pilot can cross on the frame the race ends, so in practice there is
        /// one; the fixed order only matters for a same-poll double finish.
        /// </summary>
        bool TryFindFinisherDomain(GameDataSO gameData, int target, out Domains finisher)
        {
            finisher = Domains.Blue;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (ScoringMetrics.BestByDomain(gameData, metric, d) < target) continue;
                finisher = d;
                return true;
            }
            return false;
        }

        /// <summary>Highest team total; a tie goes to <paramref name="finisher"/>'s team, then
        /// to <see cref="GameDataSO.ActiveDomains"/> order so every machine agrees.</summary>
        Domains ResolveTeamWinner(GameDataSO gameData, Domains finisher)
        {
            Domains best = Domains.Blue;
            int bestSum = -1;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int sum = DomainValue(gameData, d);
                if (sum > bestSum || (sum == bestSum && d == finisher))
                {
                    bestSum = sum;
                    best = d;
                }
            }
            return best;
        }

        /// <summary>Each pilot scores their OWN gates; the team total is the sum of those.</summary>
        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = LiveMetric(stats);
        }

        /// <summary>
        /// Pilots grouped by team, the winning team first: team total, then own gates, then
        /// name - so the scoreboard reads as the team standings it decided on.
        /// </summary>
        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var winner = gameData.WinnerDomain;
            var ordered = gameData.RoundStatsList
                .OrderByDescending(s => s.Domain == winner)
                .ThenByDescending(s => DomainValue(gameData, s.Domain))
                .ThenBy(s => (int)s.Domain)
                .ThenByDescending(s => LiveMetric(s))
                .ThenBy(s => s.Name, StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                LiveMetric(s),
                $"{LiveMetric(s)} {GatesNoun(LiveMetric(s))}",
                $"Team {DomainValue(gameData, s.Domain)}")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} {GatesNoun(diff).ToUpperInvariant()}",
                localStats != null ? DomainValue(gameData, localStats.Domain) : 0,
                false);
        }

        static string GatesNoun(int n) => n == 1 ? "Gate" : "Gates";
    }
}
