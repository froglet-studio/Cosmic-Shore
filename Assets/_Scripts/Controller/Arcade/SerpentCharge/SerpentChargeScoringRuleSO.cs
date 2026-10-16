using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Charge scoring: <b>SNIPES</b>. A CHARGE game is about hitting other pilots with your
    /// abilities (the Space games are the ones about destroying prisms - Garrett, 2026-10-16), so
    /// the only thing that scores here is a Sniper Shot that strips a rival pilot. First DOMAIN to
    /// <see cref="GameDataSO.CombatPointTargetCount"/> wins, golf-timed - the Dustup shape
    /// (<see cref="DustupScoringRuleSO"/>), on <see cref="ScoringMetric.CombatPoints"/>.
    ///
    /// <para><b>Nothing new is counted.</b> The round's pilot strip was already a landed combat hit
    /// of class <see cref="CombatHitClass.Debuff"/> (<c>SniperShotAction.asset</c> authors
    /// <c>hitClass: 2</c>), credited platform-wide by <c>CombatHitScoring.Credit</c> into
    /// <c>DebuffHitsLanded</c> (the raw count) and <c>CombatPoints</c> (at this rule's price). This
    /// rule prices it and every other class at zero. Prisms the round destroys are still destroyed
    /// and still counted by <c>StatsManager</c>, and they pay nothing here.</para>
    ///
    /// <para>The same-victim window is the weapon's own (<c>sameVictimCooldownSeconds</c>, 0.5 s),
    /// far under the rifle's 5.4-12 s cooldown, so every round that connects pays.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Serpent Charge", fileName = "SerpentChargeScoringRule")]
    public class SerpentChargeScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one SNIPE - a Sniper Shot that strips an opposing pilot (the sniper's hit " +
                 "class is Debuff). Paid into CombatPoints by CombatHitScoring.Credit. Authored by " +
                 "Tools/Build/author_serpent_charge_assets.py.")]
        [Min(0)] [SerializeField] int snipePoints = 1;

        [Tooltip("Points for any OTHER combat-hit class. Zero on purpose: the Serpent fires nothing " +
                 "but the rifle, and a rule that paid for bullets or rockets would hide a " +
                 "mis-authored vessel roster behind a working scoreboard.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        public int SnipePoints => snipePoints;

        // Priced explicitly per class (CombatHitClass's own warning: an `x == A ? a : b` rule
        // silently prices every future member as b).
        public override int PointsForCombatHit(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.Debuff => snipePoints,
            CombatHitClass.Bullet => otherHitPoints,
            CombatHitClass.MissileDirect => otherHitPoints,
            CombatHitClass.MissileBlast => otherHitPoints,
            CombatHitClass.MissileShockwave => otherHitPoints,
            CombatHitClass.Strike => otherHitPoints,
            _ => 0,
        };

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
            // Same sentinel scheme as every time-based golf mode (GolfScoreSentinels is the single
            // source of truth): winners carry their finish time, losers their team's snipes left.
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
                    : $"{Remaining(gameData, s.Domain)} Snipes Left",
                $"{s.DebuffHitsLanded} Snipes")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "SERPENT CHARGE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "SNIPES LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
