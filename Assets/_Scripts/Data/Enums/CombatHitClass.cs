namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>
    /// What KIND of weapon landed a vessel-vs-vessel hit. The platform counts the two classes
    /// separately (<c>IRoundStats.BulletHitsLanded</c> / <c>MissileHitsLanded</c>) and leaves
    /// what each is WORTH to the mode's <c>ScoringRuleSO.PointsForCombatHit</c> - so a raw hit
    /// count stays comparable across modes and only Dog Fight's rule knows that a rocket is
    /// worth fifty bullets.
    ///
    /// The class is authored on the effect asset, not inferred at runtime: the same effect
    /// script sits in the Sparrow's full-auto container marked <see cref="Bullet"/> and in its
    /// skyburst container marked <see cref="Missile"/>, which is what makes "is this a bullet?"
    /// a data question rather than a chain of prefab-name checks.
    /// </summary>
    public enum CombatHitClass
    {
        /// <summary>A direct gun round - the Sparrow's full-auto tracer, and any future
        /// vessel's equivalent. Cheap, frequent, low value.</summary>
        Bullet = 0,

        /// <summary>A rocket: either a direct strike or being caught in its blast. Both are the
        /// same event to the scoreboard, and both are latched together so ONE rocket can only
        /// ever score once against a given victim (a skyburst detonates on its own direct hit,
        /// so the two paths always fire back-to-back).</summary>
        Missile = 1,

        /// <summary>An area DEBUFF: an opposing pilot caught in a blast that strips their element
        /// levels rather than their hull - today the Dolphin's crystal cone. It is a separate
        /// class rather than a Missile because it is a different verb: nothing is fired, nothing
        /// is destroyed, and what lands is elemental. The Bends is the only mode that pays for
        /// it; everywhere else it is counted and worth nothing, exactly like the other two.</summary>
        Debuff = 2,
    }
}
