using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Warren scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.VolumeRemaining"/>: the mass a domain
    /// has STANDING in its own colour when the clock runs out - its Meerkats' trail and every exit portal their solitons
    /// rang their ways out with, both counted by <c>StatsManager</c> as in every mode (a portal prism is laid through the
    /// hull's own prism spawn channel, <c>MeerkatExitPortal</c>), and anything an I4 soliton's swallow STOLE into it.
    ///
    /// <para><b>Timed, because the score is a live stock</b> (Tapestry's reason): rival solitons swallow it and fire it
    /// back into the rest of it, so a first-past-the-post target would end the match on a number that was true for one
    /// frame. <see cref="IsObjectiveReached"/> never fires; the time monitor is the scene's only monitor.</para>
    ///
    /// <para><b>The genre is Mass through the metric</b> (<c>ModeGenre.TryElementForMetric</c>), so no mode row is
    /// needed. Glasshouse's rule with the garden column swapped for the prisms a pilot holds standing.</para>
    ///
    /// Every line the player reads is an authored template; an empty one shows nothing. Ties fall to the documented
    /// Jade -> Ruby -> Gold enum order (the base <see cref="ScoringRuleSO.ResolveWinner"/>), identical on every peer.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Warren", fileName = "WarrenScoringRule")]
    public class WarrenScoringRuleSO : ScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("Every pilot's score column. {count} = the volume they hold standing (trail and exit portals).")]
        [SerializeField] string volumeTemplate = "{count} Volume";

        [Tooltip("Every pilot's breakdown column. {count} = the prisms they hold standing.")]
        [SerializeField] string prismsTemplate = "{count} prisms";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under the value (the volume the local pilot holds standing).")]
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
                .ThenBy(s => s.Name, StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                TemplateText.Fill(volumeTemplate, "count", LiveMetric(s).ToString()),
                TemplateText.Fill(prismsTemplate, "count", s.PrismsRemaining.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            new ScoreReveal(didWin ? victoryHeader : defeatHeader, revealLabel, LiveMetric(localStats), false);
    }
}
