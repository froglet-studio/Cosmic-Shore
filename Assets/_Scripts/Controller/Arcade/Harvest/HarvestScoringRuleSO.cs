using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Harvest scoring: Rampage's mechanics - metric <see cref="ScoringMetric.PrismsDestroyed"/> (hostile prisms only,
    /// so your own colour never scores), golf-timed (winners carry their finish time, losers a prisms-remaining
    /// sentinel), first domain to <see cref="GameDataSO.PrismTargetCount"/> wins - so it subclasses rather than
    /// re-implements (Wrecking Ball's shape). It exists as its own type so the mode owns its asset, and every line the
    /// player reads is an authored template rather than Rampage's literals.
    ///
    /// <para>What makes the metric MEAN something is upstream of the rule: the Thresher's chain cuts
    /// (<c>Prism.Slice</c>), ball hits (<c>Prism.Damage</c>) and blasts (<c>ExplosionImpactor</c>, not anonymous) all
    /// name its pilot as the attacker, so all three land on the one stat this rule reads. A Mass Mode steal destroys
    /// nothing and scores nothing: the game stays a Space game.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Harvest", fileName = "HarvestScoringRule")]
    public class HarvestScoringRuleSO : RampageScoringRuleSO
    {
        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = hostile prisms their domain still needed.")]
        [SerializeField] string prismsLeftTemplate = "{count} Prisms Left";

        [Tooltip("Every pilot's breakdown column. {count} = the hostile prisms they destroyed.")]
        [SerializeField] string prismsDestroyedTemplate = "{count} Prisms";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "HARVEST TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the prisms their domain still needed).")]
        [SerializeField] string defeatLabel = "PRISMS LEFT";

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction (Rampage's): finish times below every loser sentinel.
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
                TemplateText.Fill(prismsDestroyedTemplate, "count", LiveMetric(s).ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
