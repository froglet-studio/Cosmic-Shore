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
    /// Longshot scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.PrismsDestroyed"/>: the hostile prisms a
    /// pilot broke (neutral lance prisms and rivals' mass; never their own domain's), credited by the platform's
    /// destruction path for every mode. The field is finite and a rival's trail is always there to shoot, so only the
    /// clock ends it (<see cref="IsObjectiveReached"/> never fires). The genre is Space through the metric
    /// (<c>ModeGenre</c>).
    ///
    /// Every line the player reads is an authored template; an empty one shows nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Longshot", fileName = "LongshotScoringRule")]
    public class LongshotScoringRuleSO : ScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("Every pilot's score column. {count} = the hostile prisms they broke.")]
        [SerializeField] string prismsTemplate = "{count} Prisms";

        [Tooltip("Every pilot's breakdown column. {volume} = the hostile volume they broke.")]
        [SerializeField] string volumeTemplate = "{volume} Volume";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under the value (the prisms the local pilot broke).")]
        [SerializeField] string revealLabel = "PRISMS BROKEN";

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
                TemplateText.Fill(prismsTemplate, "count", LiveMetric(s).ToString()),
                TemplateText.Fill(volumeTemplate, "volume", Mathf.RoundToInt(s.HostileVolumeDestroyed).ToString())))
                .ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            new ScoreReveal(didWin ? victoryHeader : defeatHeader, revealLabel, LiveMetric(localStats), false);
    }
}
