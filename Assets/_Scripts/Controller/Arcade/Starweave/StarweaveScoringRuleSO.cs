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
    /// Starweave scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.VolumeRemaining"/>: the prism volume a
    /// pilot has standing in their colour, kept by <c>StatsManager</c> for every mode (+ on lay and growth, - on destroy,
    /// shrink and steal). A live stock, so only the clock ends it (<see cref="IsObjectiveReached"/> never fires) - the
    /// Tapestry shape. The genre is Mass through the metric (<c>ModeGenre</c>), so no mode row is needed.
    ///
    /// Every line the player reads is an authored template; an empty one shows nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Starweave", fileName = "StarweaveScoringRule")]
    public class StarweaveScoringRuleSO : ScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("Every pilot's score column. {volume} = the volume they have standing.")]
        [SerializeField] string volumeTemplate = "{volume} Volume";

        [Tooltip("Every pilot's breakdown column. {count} = how many of their prisms stand.")]
        [SerializeField] string prismsTemplate = "{count} prisms standing";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under the value (the volume the local pilot has standing).")]
        [SerializeField] string revealLabel = "VOLUME WOVEN";

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                stats.Score = LiveMetric(stats);
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
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
                TemplateText.Fill(volumeTemplate, "volume", LiveMetric(s).ToString()),
                TemplateText.Fill(prismsTemplate, "count", s.PrismsRemaining.ToString())))
                .ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            new ScoreReveal(didWin ? victoryHeader : defeatHeader, revealLabel, LiveMetric(localStats), false);
    }
}
