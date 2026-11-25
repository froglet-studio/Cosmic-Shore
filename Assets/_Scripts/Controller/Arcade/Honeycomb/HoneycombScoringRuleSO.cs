using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Honeycomb scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.VolumeRemaining"/>: the prism volume
    /// a domain has STANDING in its own colour when the clock runs out (the Honeyguide's trail and every comb cell of its
    /// own colour a hive laid, minus whatever rival pecks broke).
    ///
    /// <para><b>Timed, Tapestry's reason.</b> The score is a live stock that rival Pecks chip, so a first-past-the-post
    /// target would end a match on a number that was true for one frame. <see cref="IsObjectiveReached"/> never fires;
    /// the time monitor is the scene's only monitor.</para>
    ///
    /// <para>Every word is an authored field (the results columns and the reveal), so a human edits or blanks them in
    /// the inspector. Ties fall to the documented Jade -> Ruby -> Gold enum order (the base
    /// <see cref="ScoringRuleSO.ResolveWinner"/>), identical on every machine.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Honeycomb", fileName = "HoneycombScoringRule")]
    public class HoneycombScoringRuleSO : ScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("Every pilot's score column. {count} = the volume they have standing.")]
        [SerializeField] string volumeTemplate = "{count} Volume";
        [Tooltip("Every pilot's breakdown column. {count} = the prisms they have standing.")]
        [SerializeField] string breakdownTemplate = "{count} prisms standing";

        [Header("Reveal")]
        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";
        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";
        [Tooltip("The reveal's label on both sides (the value is the volume the pilot has standing, a count, never a time).")]
        [SerializeField] string revealLabel = "VOLUME STANDING";

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
            // TEAM-major: rows group by domain placement, teammates by their own standing volume, name as the final
            // tiebreak so every peer builds an identical list.
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
                TemplateText.Fill(volumeTemplate, "count", LiveMetric(s).ToString()),
                TemplateText.Fill(breakdownTemplate, "count", s.PrismsRemaining.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        // Points mode: the value is a VOLUME on both sides, so it is never formatted as a time.
        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            new ScoreReveal(didWin ? victoryHeader : defeatHeader, revealLabel, LiveMetric(localStats), false);
    }
}
