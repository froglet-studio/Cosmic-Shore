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
    /// Gorge scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.PrismsDestroyed"/> (hostile prisms
    /// destroyed): every prism a pilot's hull breaks, as every mode counts it, plus every prism of the reef the creature
    /// they ride sucks in. A possessed creature's suction names its rider (<c>Fauna.PossessedSuction</c> eats as the
    /// wasp's pilot), so <c>StatsManager</c> credits it on the machine that simulates the rider, and
    /// <see cref="GorgeController"/> credits the rest (a remote human's host feeds on the server).
    ///
    /// <para><b>Timed</b>, because the reef is finite and the wild herds eat it too: a target could be out of reach for
    /// every domain once the shoals are stripped. <see cref="IsObjectiveReached"/> never fires.</para>
    ///
    /// <para><b>The genre is Space through the metric</b> (<c>ModeGenre.TryElementForMetric</c>): prisms destroyed is
    /// "mass taken off the board", so no mode row is needed.</para>
    ///
    /// Every line the player reads is an authored template; an empty one shows nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Gorge", fileName = "GorgeScoringRule")]
    public class GorgeScoringRuleSO : ScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("Every pilot's score column. {count} = the prisms they took off the board (host and hull).")]
        [SerializeField] string prismsTemplate = "{count} Prisms";

        [Tooltip("Every pilot's breakdown column. {count} = the part of it the creatures they rode sucked in.")]
        [SerializeField] string eatenTemplate = "{count} sucked in";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under the value (the prisms the local pilot took off the board).")]
        [SerializeField] string revealLabel = "PRISMS GORGED";

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

        public override List<ScoreResult> BuildResults(GameDataSO gameData) => BuildResults(gameData, null);

        /// <summary>The results rows. <paramref name="eatenOf"/> answers each pilot's reef prisms sucked in by their hosts
        /// (the whistle's snapshot); null leaves the breakdown blank.</summary>
        public List<ScoreResult> BuildResults(GameDataSO gameData, Func<string, int> eatenOf)
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
                eatenOf == null ? string.Empty : TemplateText.Fill(eatenTemplate, "count", eatenOf(s.Name).ToString())))
                .ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            new ScoreReveal(didWin ? victoryHeader : defeatHeader, revealLabel, LiveMetric(localStats), false);
    }
}
