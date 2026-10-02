using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tapestry scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.VolumeRemaining"/>:
    /// the prism volume a domain has STANDING when the clock runs out.
    ///
    /// <para><b>Timed, because the score is a live stock.</b> Every other domain race counts up to
    /// a target and can only count up. This one falls whenever a rival's dust destroys, shrinks or
    /// steals your mass, so a first-past-the-post target would end a match on a number that was
    /// true for one frame. "Most standing at the whistle" is the only end condition that means what
    /// it says, and it is what makes the last thirty seconds a raid rather than a formality.
    /// <see cref="IsObjectiveReached"/> therefore never fires; the time monitor is the scene's only
    /// monitor (Bloomrush's shape).</para>
    ///
    /// <para><b>A steal moves TWO scores.</b> <c>StatsManager.PrismStolen</c> credits the thief's
    /// VolumeRemaining and debits the victim's, so a raid that steals is worth twice a raid that
    /// destroys - which is exactly why the Butterfly's dust rolls one in three of each.</para>
    ///
    /// Ties fall to the documented Jade -> Ruby -> Gold enum order (the base
    /// <see cref="ScoringRuleSO.ResolveWinner"/>), identical on every machine.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Tapestry", fileName = "TapestryScoringRule")]
    public class TapestryScoringRuleSO : ScoringRuleSO
    {
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            // Timed mode: only the clock ends the turn.
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            // Points mode: every pilot carries the volume they hold. Higher is better.
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                stats.Score = LiveMetric(stats);
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // TEAM-major: rows group by domain placement, teammates by their own standing volume,
            // name as the final tiebreak so every peer builds an identical list.
            var placement = ResolvePlacementOrder(gameData);
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => { int i = placement.IndexOf(s.Domain); return i < 0 ? int.MaxValue : i; })
                .ThenByDescending(LiveMetric)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{LiveMetric(s)} Volume",
                // The breakdown: how much of what they hold they TOOK rather than painted.
                $"{Mathf.RoundToInt(s.VolumeStolen)} stolen")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "VOLUME STANDING", LiveMetric(localStats), true)
                : new ScoreReveal("DEFEAT", "VOLUME STANDING", LiveMetric(localStats), false);
    }
}
