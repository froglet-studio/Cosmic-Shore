namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>
    /// What KIND of weapon landed a vessel-vs-vessel hit. The platform counts the classes as raw
    /// facts (<c>IRoundStats.BulletHitsLanded</c> / <c>MissileHitsLanded</c> /
    /// <c>DebuffHitsLanded</c>) and leaves what each is WORTH to the mode's
    /// <c>ScoringRuleSO.PointsForCombatHit</c> - so a raw hit count stays comparable across modes
    /// and only Dog Fight's rule knows what a rocket is worth.
    ///
    /// The class is authored on the effect asset, not inferred at runtime: the same effect
    /// script sits in the Sparrow's full-auto container marked <see cref="Bullet"/> and in its
    /// skyburst container marked <see cref="MissileDirect"/>, which is what makes "is this a
    /// bullet?" a data question rather than a chain of prefab-name checks.
    ///
    /// <b>A ROCKET LANDS IN THREE CLASSES, and they are RANKED rather than additive.</b> One
    /// skyburst throws three concentric things at a pilot - the round itself, the prism blast,
    /// and the warhead shockwave - and a victim close enough to take the inner one is always
    /// also inside the outer ones. So <c>VesselCombatHitLatch</c> pays the BEST class a single
    /// rocket achieved against a given victim and nothing more (see its "upgrade" rule): a
    /// clean centre-punch is worth a direct hit, not the sum of all three. That is what lets a
    /// mode price closeness - the shockwave is the common outcome, the blast is closer, the
    /// direct hit is rare - without a rocket ever paying three times for one victim.
    ///
    /// <b>Adding a member is safe; assuming there are three is not.</b> A rule that reads
    /// <c>hitClass == X ? a : b</c> silently prices every future member as <c>b</c>. Price them
    /// explicitly (Dog Fight does) or return 0.
    /// </summary>
    public enum CombatHitClass
    {
        /// <summary>A direct gun round - the Sparrow's full-auto tracer and its turret-stance
        /// prism round, which are one weapon class. Cheap, frequent, low value.</summary>
        Bullet = 0,

        /// <summary>A rocket that physically CONNECTED - the round's own hit sphere on an
        /// opposing hull. The innermost and rarest of the three missile classes: the proximity
        /// fuze detonates the round at 20x its hit radius, so it normally goes off well before
        /// a direct strike is possible and this class is reserved for the case where it
        /// somehow did not.</summary>
        MissileDirect = 1,

        /// <summary>An area DEBUFF: an opposing pilot caught in a blast that strips their element
        /// levels rather than their hull - today the Dolphin's crystal cone. It is a separate
        /// class rather than a missile because it is a different verb: nothing is fired, nothing
        /// is destroyed, and what lands is elemental. The Bends is the only mode that pays for
        /// it; everywhere else it is counted and worth nothing, exactly like the others.</summary>
        Debuff = 2,

        /// <summary>Caught in a rocket's PRISM BLAST - the middle radius, the detonation that
        /// tears up the arena. Closer than the shockwave and further in than a direct hit, so
        /// it is the uncommon middle tier.</summary>
        MissileBlast = 3,

        /// <summary>Caught in a rocket's SHOCKWAVE - the outermost radius, the warhead blast
        /// that carries the elemental debuff and touches no mass. This is the ordinary outcome
        /// of a proximity kill and therefore the cheapest of the three.</summary>
        MissileShockwave = 4,
    }

    /// <summary>
    /// Helpers over <see cref="CombatHitClass"/> that more than one system needs to agree on.
    /// </summary>
    public static class CombatHitClasses
    {
        /// <summary>
        /// How close this class means the rocket got - higher is closer and rarer. Used by
        /// <c>VesselCombatHitLatch</c> to let one rocket UPGRADE its score against a victim
        /// when an inner class lands after an outer one, and to refuse the reverse.
        ///
        /// <para>Non-missile classes rank 0: they are not tiers of one event, so they never
        /// upgrade and never suppress each other - a bullet and a debuff latch on their own
        /// keys and are unaffected by this ordering.</para>
        /// </summary>
        public static int MissileProximityRank(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.MissileShockwave => 1,
            CombatHitClass.MissileBlast     => 2,
            CombatHitClass.MissileDirect    => 3,
            _                               => 0,
        };

        /// <summary>True for the three classes one rocket can land, which share a latch entry
        /// per victim so a single rocket pays once, at its best tier.</summary>
        public static bool IsMissile(CombatHitClass hitClass) => MissileProximityRank(hitClass) > 0;
    }
}
