using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Broadside: the ARENA brawl. The turn ends (<c>BroadsidePointTurnMonitor</c>) when an
    /// active domain's summed <see cref="IRoundStats.CombatPoints"/> reaches
    /// <see cref="GameDataSO.CombatPointTargetCount"/>.
    ///
    /// <b>This rule prices a VERB, and that is the whole reason a brawl can seat a mixed fleet.</b>
    /// Dog Fight prices one hull's weapons; here seven hulls arrive with four different ways of
    /// touching a rival, and the card has to be able to say what each is worth relative to the
    /// others. It prices them by WHAT THEY COST THE PILOT WHO LANDED ONE, never by which hull
    /// landed it - so no hull is named anywhere in this file and a hull added to the card
    /// tomorrow is priced by the verb it brings:
    ///
    ///   • <see cref="CombatHitClass.Bullet"/> - a round that connects. Cheap and frequent,
    ///     landed from range: the Sparrow's tracer and the Urchin's spike.
    ///   • <see cref="CombatHitClass.Strike"/> - a CONTACT hit. You had to fly into them, at a
    ///     closing speed you had to win: the Rhino's sword and the Squirrel's joust.
    ///   • <see cref="CombatHitClass.Debuff"/> - an area effect that strips a rival's elements,
    ///     which is the only class that makes its victim WORSE rather than merely counting: the
    ///     Dolphin's cone, the Scarab's plate and the Manta's bloom.
    ///   • The three MISSILE tiers - ranked by how close the rocket got, priced as Dog Fight
    ///     prices them, because it is the same rocket out of the same bay.
    ///
    /// <b>The switch is EXHAUSTIVE on purpose.</b> A rule written <c>hitClass == X ? a : b</c>
    /// prices every enum member added later as the default arm - which is exactly how The Bends'
    /// Debuff class was once paid at the bullet rate, and it is a live hazard in a mode whose
    /// whole premise is that new verbs keep arriving. A class this mode has no opinion about is
    /// worth 0 and says so.
    ///
    /// <b>A TEAM race, like every mode in this family.</b> Points pool per domain, and that is
    /// also the only shape the impact layer can express: every scoring path here refuses
    /// own-domain contact, so a same-domain pair with an individual win condition would be
    /// unable to fight at all. Domains ARE the sides.
    ///
    /// Golf-timed: the winning domain's pilots carry their finish time, everyone else a
    /// <see cref="GolfScoreSentinels"/> sentinel encoding their team's remaining points.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Broadside", fileName = "BroadsideScoringRule")]
    public class BroadsideScoringRuleSO : ScoringRuleSO
    {
        [Header("Hit values")]
        [Tooltip("Points for one landed ROUND - a Sparrow tracer or an Urchin spike on an " +
                 "opposing hull. The cheapest verb: it is landed from range and repeats.")]
        [Min(0)] [SerializeField] int bulletPoints = 1;

        [Tooltip("Points for a CONTACT strike - a Rhino sword sweep or a Squirrel joust. Priced " +
                 "well above a round because it is paid for in position: you have to be on top " +
                 "of a rival, at a closing speed you won, inside their own weapon's reach.")]
        [Min(0)] [SerializeField] int strikePoints = 8;

        [Tooltip("Points for an area DEBUFF - a Dolphin cone, a Scarab plate, a Manta bloom. The " +
                 "most valuable non-rocket verb because it is the only one that leaves its " +
                 "victim measurably worse for four seconds rather than merely counting.")]
        [Min(0)] [SerializeField] int debuffPoints = 12;

        [Tooltip("Points for a rocket's SHOCKWAVE - the outermost radius and the ordinary " +
                 "outcome of a proximity kill. Dog Fight's value, because it is the same bay.")]
        [Min(0)] [SerializeField] int missileShockwavePoints = 10;

        [Tooltip("Points for a rocket's PRISM BLAST - the middle radius. The victim has to be " +
                 "well inside it.")]
        [Min(0)] [SerializeField] int missileBlastPoints = 20;

        [Tooltip("Points for a DIRECT skyburst strike - rarest of the three, since the " +
                 "proximity fuze normally detonates the round well before one is possible.")]
        [Min(0)] [SerializeField] int missileDirectPoints = 30;

        public int BulletPoints => bulletPoints;
        public int StrikePoints => strikePoints;
        public int DebuffPoints => debuffPoints;

        /// <summary>
        /// The mode's price list. See the class doc for why this is a switch and not a ternary.
        ///
        /// <para>The three missile tiers are RANKED, not cumulative: one rocket pays the best
        /// tier it achieved against a victim and no more, which <c>VesselCombatHitLatch</c>
        /// enforces by upgrading its claim rather than opening a second one.</para>
        /// </summary>
        public override int PointsForCombatHit(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.Bullet           => bulletPoints,
            CombatHitClass.Strike           => strikePoints,
            CombatHitClass.Debuff           => debuffPoints,
            CombatHitClass.MissileShockwave => missileShockwavePoints,
            CombatHitClass.MissileBlast     => missileBlastPoints,
            CombatHitClass.MissileDirect    => missileDirectPoints,
            _                               => 0,
        };

        protected override int TargetCount(GameDataSO gameData) => gameData.CombatPointTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0,
                // or the match would finish on the countdown.
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
            // Golf order = TEAM-major by construction: finish times (winners) below every loser
            // sentinel, loser sentinels ordered by team deficit. Individual points order
            // teammates; name is the final tiebreak so every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Points Left",
                // The BREAKDOWN, not the points - and in a mixed-fleet brawl it is the only
                // line that says what a pilot actually did, because seven hulls reach the same
                // total by four different routes. Every count is a raw platform fact
                // (CombatHitScoring tallies each class explicitly), so a hull with no gun can
                // never report bullets.
                Breakdown(s))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        /// <summary>
        /// "3 rounds · 2 strikes · 1 blast" - only the verbs this pilot actually landed, so a
        /// Rhino's row is not padded with three zeroes for weapons it does not carry.
        /// </summary>
        static string Breakdown(IRoundStats s)
        {
            var parts = new List<string>(4);
            if (s.BulletHitsLanded > 0)  parts.Add($"{s.BulletHitsLanded} rounds");
            if (s.StrikeHitsLanded > 0)  parts.Add($"{s.StrikeHitsLanded} strikes");
            if (s.DebuffHitsLanded > 0)  parts.Add($"{s.DebuffHitsLanded} debuffs");
            if (s.MissileHitsLanded > 0) parts.Add($"{s.MissileHitsLanded} rockets");
            return parts.Count == 0 ? "no hits" : string.Join(" · ", parts);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "BROADSIDE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "POINTS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
