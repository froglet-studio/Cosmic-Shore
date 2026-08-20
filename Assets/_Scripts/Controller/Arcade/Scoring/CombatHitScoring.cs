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
        public static void Credit(IRoundStats shooterStats, CombatHitClass hitClass, ScoringRuleSO rule)
        {
            if (shooterStats == null) return;

            switch (hitClass)
            {
                case CombatHitClass.Missile: shooterStats.MissileHitsLanded++; break;
                case CombatHitClass.Debuff:  shooterStats.DebuffHitsLanded++;  break;
                default:                     shooterStats.BulletHitsLanded++;  break;
            }

            int points = rule != null ? rule.PointsForCombatHit(hitClass) : 0;
            if (points != 0) shooterStats.CombatPoints += points;
        }
    }
}
