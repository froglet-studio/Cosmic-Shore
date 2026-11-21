using System.Linq;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hijack: the Urchin heist race. Same shape as Rampage - a domain race to a target with a
    /// golf-timed finish (the winning domain's pilots score their finish time, everyone else a
    /// sentinel encoding their team's remaining count) - over a different metric:
    /// <see cref="ScoringMetric.PrismsStolen"/> against <see cref="GameDataSO.PrismTargetCount"/>,
    /// authored in FrogletTools &gt; Game Modes &gt; End Game Conditions.
    ///
    /// <para><b>Stealing, not destroying, and the difference is the whole mode.</b> Nothing in the
    /// Switchyard is ever removed: a stolen prism changes hands and stays where it is, so the same
    /// prism can pay both sides all match and the arena a domain is winning in is the same arena
    /// it started in. That is what lets the mode run with no food web, no respawn and no despawn
    /// and still be a race - and it is why the metric is deliberately a COUNT rather than
    /// <c>VolumeStolen</c>: riding your own colour GROWS a prism, so a volume metric would quietly
    /// pay a re-stealer more than the pilot who took it first, and reward camping one rail over
    /// raiding a new one.</para>
    ///
    /// <para>Overrides only the two places Rampage names its own subject: the scoreboard's
    /// per-row wording and the reveal label. The end condition, the winner and the golf sentinel
    /// scheme are all inherited unchanged.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Hijack", fileName = "HijackScoringRule")]
    public class HijackScoringRuleSO : RampageScoringRuleSO
    {
        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction (finish times below every loser sentinel).
            // Teammates are ordered by their OWN steals - LiveMetric, not Rampage's destruction
            // count, which is a flat zero in a mode where nothing is destroyed and would have
            // silently degraded the tiebreak to the name.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => LiveMetric(s))
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} To Steal",
                $"{LiveMetric(s)} Stolen")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "HEIST TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "LEFT TO STEAL", Remaining(gameData, localStats.Domain), false);
    }
}
