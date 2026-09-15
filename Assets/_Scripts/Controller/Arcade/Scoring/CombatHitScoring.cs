using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The ONE place a landed vessel-vs-vessel hit turns into numbers.
    ///
    /// Two call sites reach it - <c>StatsManager.CombatHitLanded</c> (the server recording its
    /// own guns and every AI's) and <c>Player.ReportCombatHit_ServerRpc</c> (the server
    /// recording a client's forwarded shot) - and they must not be able to disagree about what
    /// a hit is worth, so neither owns the arithmetic.
    ///
    /// The split of responsibility is deliberate: the two RAW COUNTS are platform facts that
    /// mean the same thing everywhere, while the POINTS are the mode's opinion, read off its
    /// own <see cref="ScoringRuleSO.PointsForCombatHit"/>. Every mode whose rule pays nothing
    /// still accumulates the counts (they are cheap, and they make "how much shooting happened"
    /// answerable in any mode), and only Dog Fight turns them into a score.
    ///
    /// Server-side only by construction: both callers run on the server, and
    /// <c>IRoundStats</c>'s setters push through server-write NetworkVariables.
    /// </summary>
    public static class CombatHitScoring
    {
        /// <param name="supersededRank">
        /// The missile proximity rank this hit REPLACES (0 = a fresh hit). One rocket reaches a
        /// victim through up to three ranked classes and must pay only its best, so an upgrade
        /// credits the DIFFERENCE and does not touch the raw count - the rocket already counted
        /// as one landed missile when its outermost tier arrived. See
        /// <c>VesselCombatHitLatch.TryAdmit</c>, which is the only thing that can produce a
        /// non-zero rank here.
        /// </param>
        public static void Credit(IRoundStats shooterStats, CombatHitClass hitClass, ScoringRuleSO rule,
                                  int supersededRank = 0)
        {
            if (shooterStats == null) return;

            bool isUpgrade = supersededRank > 0;

            // RAW COUNTS are per EVENT, not per tier: an upgrade is the same rocket arriving
            // closer than it was first credited for, so counting it again would report two
            // missile hits for one rocket.
            if (!isUpgrade)
            {
                if (CombatHitClasses.IsMissile(hitClass))      shooterStats.MissileHitsLanded++;
                else if (hitClass == CombatHitClass.Debuff)    shooterStats.DebuffHitsLanded++;
                else                                          shooterStats.BulletHitsLanded++;
            }

            if (rule == null) return;

            int points = rule.PointsForCombatHit(hitClass);

            // Pay only what the upgrade is WORTH ON TOP of the tier already paid. The rule is
            // asked for the superseded tier's price rather than that price being remembered,
            // so a mode can retune its values mid-match and an in-flight upgrade still nets out
            // to exactly the new best tier.
            if (isUpgrade)
                points -= rule.PointsForCombatHit(ClassForRank(supersededRank));

            if (points != 0) shooterStats.CombatPoints += points;
        }

        /// <summary>The missile class a proximity rank names - the inverse of
        /// <see cref="CombatHitClasses.MissileProximityRank"/>, needed because the wire carries
        /// the rank (a small stable int) rather than the class it superseded.</summary>
        static CombatHitClass ClassForRank(int rank) => rank switch
        {
            1 => CombatHitClass.MissileShockwave,
            2 => CombatHitClass.MissileBlast,
            _ => CombatHitClass.MissileDirect,
        };
    }
}
