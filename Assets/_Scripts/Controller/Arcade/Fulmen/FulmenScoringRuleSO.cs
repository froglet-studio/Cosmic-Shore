using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fulmen scoring - Rampage's rule on <see cref="ScoringMetric.PrismsDestroyed"/>, inherited (first DOMAIN to
    /// <see cref="GameDataSO.PrismTargetCount"/> hostile prisms, golf-timed, team-major results). The score is mass
    /// taken off the board, so the card's genre reads Space through the metric (<c>ModeGenre</c>).
    ///
    /// <para>What counts is exactly what Rampage counts: every prism the pilot destroys that is not their own domain's.
    /// A Fulmen bolt's burn-out is <c>Prism.BurnOut</c>, which is <c>Prism.Damage</c> in the igniter's name (the pilot
    /// who took the crystal), so every forest prism a bolt burns, and every prism its chained child bolts burn, is that
    /// pilot's. Forest flora carry no roster owner, so all of it is hostile to everyone; a rival's trail is hostile, your
    /// own never scores.</para>
    ///
    /// <para>Only the words are the mode's own, and every one is an authored field (the results columns and the reveal),
    /// so a human edits or blanks them in the inspector.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Fulmen", fileName = "FulmenScoringRule")]
    public class FulmenScoringRuleSO : RampageScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = prisms their domain still needed.")]
        [SerializeField] string prismsLeftTemplate = "{count} Prisms Left";
        [Tooltip("Every pilot's breakdown column. {count} = prisms that pilot destroyed.")]
        [SerializeField] string breakdownTemplate = "{count} Burnt";
        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";
        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "FULMEN TIME";
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
