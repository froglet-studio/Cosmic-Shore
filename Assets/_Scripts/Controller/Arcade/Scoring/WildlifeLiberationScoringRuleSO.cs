using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Liberation: the hunt is a FREE-FOR-ALL. The turn ends
    /// (<c>WildlifeKillTurnMonitor</c>) when ONE PLAYER's
    /// <see cref="IRoundStats.LifeformsKilled"/> reaches
    /// <see cref="GameDataSO.LifeformTargetCount"/> - not when a domain's sum does.
    ///
    /// That makes this the platform's first per-individual race, and every override below exists
    /// for that one reason. The shared machinery aggregates by domain (three scores at most,
    /// teammates pooled), which is right for Skim Race / Joust / Scurry / Rampage / Ribcage and
    /// wrong here: with four hunters in one cage, pooling two of them would mean a player wins
    /// off a teammate's kills. So:
    ///
    ///   • <see cref="IsObjectiveReached"/> scans PLAYERS, not domains.
    ///   • <see cref="RemainingForPlayer"/> reads the individual, so your HUD counts down YOUR
    ///     hunt.
    ///   • <see cref="AssignScores"/> gives the finish time to the one hunter who got there.
    ///
    /// The DOMAIN sums are still published and still shown - the in-game HUD's domain panels are
    /// driven by <c>MultiplayerDomainGamesController.SyncDomainSumsRoutine</c> off this rule's
    /// <see cref="ScoringRuleSO.Metric"/> and keep working untouched - they are simply a
    /// secondary readout ("how is my colour doing") rather than the win condition.
    ///
    /// Golf-timed like every other race here: the winner carries their finish time, everyone
    /// else the shared <see cref="GolfScoreSentinels"/> encoding of how many kills they still
    /// needed, so lower is better and the winner always sorts first.
    ///
    /// A note on what does and does not score, because the ecology is a live system and not a
    /// pile of targets: only PLAYER-ATTRIBUTED kills count. A creature that starves, or that a
    /// shark eats, credits nobody - <c>Fauna.Die</c> filters engine attribution before it
    /// publishes, and <c>StatsManager.LifeformKilled</c> only credits a name on the player
    /// roster. So the swarm dying of its own accord can never move a scoreboard, and a hunter
    /// cannot farm the food web instead of hunting.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Wildlife Liberation",
        fileName = "WildlifeLiberationScoringRule")]
    public class WildlifeLiberationScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.LifeformTargetCount;

        /// <summary>
        /// The hunter currently closest to the target - and, once the turn has ended, THE winner.
        /// Ties break by name (ordinal) so every peer resolves the same player from the same
        /// data, the same way the domain rules tie-break on enum order.
        /// </summary>
        public IRoundStats ResolveWinningHunter(GameDataSO gameData)
        {
            var list = gameData != null ? gameData.RoundStatsList : null;
            if (list == null || list.Count == 0) return null;

            IRoundStats best = null;
            foreach (var s in list)
            {
                if (s == null) continue;
                if (best == null ||
                    s.LifeformsKilled > best.LifeformsKilled ||
                    (s.LifeformsKilled == best.LifeformsKilled &&
                     string.CompareOrdinal(s.Name, best.Name) < 0))
                    best = s;
            }
            return best;
        }

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;

            int target = TargetCount(gameData);
            // Never end on a target of 0 - the monitor may not have resolved/synced it yet, and
            // "0 kills wins" would end the turn on the countdown.
            if (target <= 0) return false;

            var leader = ResolveWinningHunter(gameData);
            if (leader == null || leader.LifeformsKilled < target) return false;

            winner = leader.Domain;
            return true;
        }

        /// <summary>
        /// The winning player's DOMAIN - what colours the end-game banner. The winner is still an
        /// individual; this only answers "what colour did they fly".
        /// </summary>
        public override Domains ResolveWinner(GameDataSO gameData) =>
            ResolveWinningHunter(gameData)?.Domain ?? Domains.Blue;

        /// <summary>Your OWN hunt's deficit, not your team's - see the class summary.</summary>
        public override int RemainingForPlayer(GameDataSO gameData, IRoundStats stats) =>
            stats == null ? 0 : Mathf.Max(0, TargetCount(gameData) - stats.LifeformsKilled);

        /// <summary>
        /// A domain's deficit = its BEST hunter's deficit (how close that colour is to putting
        /// someone over the line), not the sum of its players' shortfalls. Summing would make a
        /// two-player domain look further from winning than a one-player domain that is level
        /// with it, which is the opposite of true in a free-for-all.
        /// </summary>
        public override int Remaining(GameDataSO gameData, Domains domain)
        {
            int target = TargetCount(gameData);
            if (target <= 0) return 0;

            int bestKills = 0;
            var list = gameData != null ? gameData.RoundStatsList : null;
            if (list != null)
                foreach (var s in list)
                    if (s != null && s.Domain == domain && s.LifeformsKilled > bestKills)
                        bestKills = s.LifeformsKilled;

            return Mathf.Max(0, target - bestKills);
        }

        /// <summary>
        /// Winner (the individual) carries the finish time; everyone else the golf sentinel
        /// encoding THEIR OWN remaining kills. The <paramref name="winner"/> domain is ignored
        /// deliberately - in a free-for-all a teammate of the winner has not won, and scoring
        /// them as if they had is exactly the bug this mode has to avoid.
        /// </summary>
        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            var champion = ResolveWinningHunter(gameData);

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                stats.Score = ReferenceEquals(stats, champion)
                    ? finishTime
                    : GolfScoreSentinels.EncodeHexRaceLoserScore(RemainingForPlayer(gameData, stats));
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order: the single finish time sorts below every loser sentinel, and the
            // sentinels order losers by their own deficit - so the board reads as one hunt
            // leaderboard rather than a team table. Kills then name break remaining ties, so
            // every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.LifeformsKilled)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{RemainingForPlayer(gameData, s)} Kills Left",
                $"{LiveMetric(s)} Kills")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "HUNT TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "KILLS LEFT", RemainingForPlayer(gameData, localStats), false);
    }
}
