using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Charge scoring - Rampage's rule on <see cref="ScoringMetric.PrismsDestroyed"/>
    /// (first DOMAIN to <see cref="GameDataSO.PrismTargetCount"/>, golf-timed, team-major results)
    /// with ONE fold on top: every rival PILOT a sniper round strips is worth
    /// <see cref="PilotSnipePoints"/> prisms.
    ///
    /// <para><b>Why a fold and not a new metric.</b> Both halves are already counted platform-wide.
    /// A round's prism kills reach <c>HostilePrismsDestroyed</c> through <c>StatsManager</c> like
    /// every other destruction mode, and the round's pilot strip is already a landed combat hit of
    /// class <see cref="CombatHitClass.Debuff"/> (<c>SniperShotAction.asset</c> authors
    /// <c>hitClass: 2</c>), which <c>CombatHitScoring.Credit</c> pays into
    /// <c>IRoundStats.CombatPoints</c> at whatever <see cref="PointsForCombatHit"/> says. So this
    /// rule PRICES the strip and folds CombatPoints into <see cref="DomainValue"/> - the Undertow
    /// shape (two stats, one score). The end condition, the remaining count, the winner, the HUD's
    /// domain boxes and the comeback all read DomainValue, so they cannot disagree.</para>
    ///
    /// <para><b>Why a pilot is worth a stand.</b> A pilot is a hull ten units across at the end of
    /// a 1.5° cone, moving, on a rifle that fires once every 5.4-12 s - hitting one is the hardest
    /// shot in the mode, and the round that does it also takes everything hostile in front of the
    /// pilot. Priced at roughly one good line through a cactus stand, so a duel can swing a match
    /// without the forest ever becoming irrelevant. The same-victim window is the weapon's own
    /// (<c>sameVictimCooldownSeconds</c>, 0.5 s), far under the cooldown, so every connecting round
    /// pays.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Serpent Charge", fileName = "SerpentChargeScoringRule")]
    public class SerpentChargeScoringRuleSO : RampageScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Prisms one sniped rival PILOT is worth. Paid into CombatPoints by " +
                 "CombatHitScoring.Credit for every round that strips an opposing pilot (the " +
                 "sniper's hit class is Debuff) and folded into the domain score beside " +
                 "HostilePrismsDestroyed. Authored by Tools/Build/author_serpent_charge_assets.py.")]
        [Min(0)] [SerializeField] int pilotSnipePoints = 60;

        [Tooltip("Points for any OTHER combat-hit class. Zero on purpose: the Serpent fires nothing " +
                 "but the rifle, and a rule that paid for bullets or rockets would hide a " +
                 "mis-authored vessel roster behind a working scoreboard.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        public int PilotSnipePoints => pilotSnipePoints;

        // Priced explicitly per class (CombatHitClass's own warning: an `x == A ? a : b` rule
        // silently prices every future member as b).
        public override int PointsForCombatHit(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.Debuff => pilotSnipePoints,
            CombatHitClass.Bullet => otherHitPoints,
            CombatHitClass.MissileDirect => otherHitPoints,
            CombatHitClass.MissileBlast => otherHitPoints,
            CombatHitClass.MissileShockwave => otherHitPoints,
            CombatHitClass.Strike => otherHitPoints,
            _ => 0,
        };

        /// <summary>Hostile prisms destroyed plus sniped pilots (already weighted, in CombatPoints).</summary>
        public override int DomainValue(GameDataSO gameData, Domains domain) =>
            ScoringMetrics.SumByDomain(gameData, metric, domain)
            + ScoringMetrics.SumByDomain(gameData, ScoringMetric.CombatPoints, domain);

        /// <summary>
        /// Rampage's end condition reads the raw metric sum; this one must read the FOLD, or a
        /// domain whose snipes carried it past the target would be shown 0 remaining and never
        /// win. Same never-end-on-zero guard.
        /// </summary>
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
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

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Rampage's ordering (team-major golf, then the pilot's own contribution, then name so
            // every peer builds the same list), with the secondary line naming both halves.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.HostilePrismsDestroyed + s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Prisms Left",
                $"{LiveMetric(s)} Prisms, {s.DebuffHitsLanded} Snipes")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "SERPENT CHARGE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "PRISMS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
