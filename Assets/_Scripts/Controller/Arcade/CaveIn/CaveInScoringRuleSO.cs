using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cave-in scoring - Rampage's rule on <see cref="ScoringMetric.PrismsDestroyed"/>, inherited (first DOMAIN to
    /// <see cref="GameDataSO.PrismTargetCount"/> hostile prisms, golf-timed, team-major results), as Shatterplane's.
    ///
    /// <para>What counts is what Rampage counts: every prism a pilot destroys that is not their own domain's. The
    /// Meerkat breaks nothing by flying into it; its prisms break when a soliton SWALLOWS them and fires them out of
    /// its way out, and a fired prism that runs into another destroys both, credited to the soliton's maker
    /// (<c>MeerkatSwallow</c>, <c>PrismMotion.Annihilate</c>). A Meerkat's own trail and exit portals are its own
    /// colour and never score, so the game stays a Space game.</para>
    ///
    /// <para>Only the words are the mode's own, and every one is an authored field.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/CaveIn", fileName = "CaveInScoringRule")]
    public class CaveInScoringRuleSO : RampageScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = prisms their domain still needed.")]
        [SerializeField] string prismsLeftTemplate = "{count} Prisms Left";
        [Tooltip("Every pilot's breakdown column. {count} = prisms that pilot broke.")]
        [SerializeField] string breakdownTemplate = "{count} Prisms";
        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";
        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "CAVE-IN TIME";
        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";
        [Tooltip("The reveal's label under a loss (the value is the prisms their domain still needed).")]
        [SerializeField] string defeatLabel = "PRISMS LEFT";

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : TemplateText.Fill(prismsLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(breakdownTemplate, "count", LiveMetric(s).ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
