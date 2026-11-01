using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Gulag scoring - first DOMAIN to the snipe target wins, on
    /// <see cref="ScoringMetric.CombatPoints"/>. One SNIPE - a Sniper Shot that strips a rival pilot -
    /// pays <see cref="snipePoints"/>.
    ///
    /// <para><b>A snipe is a DEBUFF-class hit, and that is not a choice this rule makes.</b> The Serpent's
    /// <c>SniperShotAction.asset</c> reports its pilot strip as <see cref="CombatHitClass.Debuff"/>
    /// (<c>hitClass: 2</c>), so every snipe has been COUNTED (<c>DebuffHitsLanded</c>) in every mode since
    /// the rifle shipped; this rule is the one that PAYS for it. Counted everywhere, scored once - the Dog
    /// Fight / Dustup split. Every other class pays <see cref="otherHitPoints"/> (0): the crystal blast
    /// and the walls are the Serpent's too, and a rule that paid for them would hide a mis-authored hull
    /// behind a working scoreboard.</para>
    ///
    /// Golf-timed like Dustup: the winning domain's pilots carry their finish time, losers a sentinel
    /// encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Serpent Gulag", fileName = "SerpentGulagScoringRule")]
    public class SerpentGulagScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one SNIPE - a Sniper Shot that strips a rival pilot (a Debuff-class combat hit, " +
                 "credited by CombatHitScoring once per victim per 0.5 s).")]
        [Min(0)] [SerializeField] int snipePoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only the rifle scores in a sniper duel.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = snipes their domain still needed.")]
        [SerializeField] string snipesLeftTemplate = "{count} Snipes Left";

        [Tooltip("Every pilot's breakdown column. {count} = the snipes they landed.")]
        [SerializeField] string snipesLandedTemplate = "{count} snipes";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "GULAG TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the snipes their domain still needed).")]
        [SerializeField] string defeatLabel = "SNIPES LEFT";

        public int SnipePoints => snipePoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Debuff ? snipePoints : otherHitPoints;

        protected override int TargetCount(GameDataSO gameData) => gameData.CombatPointTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0.
                winner = Domains.Blue;
                return false;
            }

            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (DomainValue(gameData, d) >= target)
                {
                    winner = d;
                    return true;
                }
            }
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeSkimRaceLoserScore(Remaining(gameData, stats.Domain));
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : TemplateText.Fill(snipesLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(snipesLandedTemplate, "count", s.DebuffHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
