using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE ELEMENTAL BITE OF A LANDED HIT, DERIVED FROM WHAT THAT HIT IS WORTH.
    ///
    /// <para>The spec is one sentence — <i>a hit debuffs its victim in proportion to the points
    /// it scores</i> — and one constant: <b>ten points is one petal, on every element the hit
    /// touches</b>. A direct rocket strike (30) drains three petals each; ten bullets (1 apiece)
    /// drain one. "Petal" is the unit the player actually reads, and one petal is one integer
    /// element level, which is <see cref="NormalizedPerLevel"/> in the units
    /// <see cref="ResourceSystem.ApplyElementalEffect"/> takes.</para>
    ///
    /// <para><b>Why this lives on the HIT REPORT and not on a per-weapon effect asset.</b> One
    /// rocket lands in three ranked classes against one victim — shockwave, blast, direct — and
    /// a pilot close enough to take the inner one is always inside the outer ones. Authoring a
    /// drain per blast prefab would stack all three, so a centre-punch would bite six petals
    /// for a thirty-point event. <see cref="VesselCombatHitLatch"/> already solves exactly that
    /// problem for the SCORE: it pays the best tier once and reports what an upgrade
    /// SUPERSEDES so the caller can credit the difference. Draining from the same seam, with
    /// the same difference, makes the two agree by construction — a rocket bites its best tier
    /// and nothing more, whichever tiers happened to land and in whatever order.</para>
    ///
    /// <para><b>Two classes are deliberately NOT in this table</b> — <see cref="CombatHitClass.Debuff"/>
    /// and <see cref="CombatHitClass.Strike"/>. Their drain is authored per weapon because it
    /// carries per-weapon design this table cannot express: the Manta's bloom drains Mass and
    /// Space only (the rule that bars an overtaker from touching Time), and the Squirrel's
    /// overtake mirrors its debuff as an ally BUFF. Those assets are owned by
    /// <c>Tools/Build/author_combat_debuff_magnitudes.py</c>, which scales them by this same
    /// constant and asserts the two sets stay disjoint — a class draining from both would bite
    /// twice for one hit.</para>
    ///
    /// <para><b>Where it runs.</b> In the reporter's <c>Execute</c>, i.e. on the machine that
    /// owns the weapon — the same machine, and the same moment, as the per-asset drains it sits
    /// beside. Immunity is honoured inside <c>ApplyElementalEffect</c>, so a warded pilot keeps
    /// their levels and the score's own <c>requireDebuffableVictim</c> gate still agrees with
    /// what landed.</para>
    /// </summary>
    public static class CombatHitDrain
    {
        /// <summary>One integer element level ("one petal") in the normalized units
        /// <see cref="ResourceSystem.ApplyElementalEffect"/> takes. The element band is
        /// [-5, +15] levels over [-0.5, +1.5] normalized, and <c>ResourceSystem.IncrementLevel</c>
        /// is a bare <c>0.1f</c> — this is that same step, named.</summary>
        public const float NormalizedPerLevel = 0.1f;

        /// <summary>Ten points buy one petal. The whole rule.</summary>
        public const float PointsPerLevel = 10f;

        /// <summary>Seconds over which the drain decays back to baseline. Matches the shipped
        /// per-asset explosion drains, so a bullet's bite and a cone's bite fade alike.</summary>
        public const float DurationSeconds = 4f;

        static readonly Element[] Elements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>
        /// The fleet price of a hit class, in points. It is the SAME list Broadside authors
        /// (<c>BroadsideScoringRule.asset</c>) and is restated here because the drain is not a
        /// mode's business: a hit bites the same in every mode and is only PAID where a rule
        /// prices it. <c>author_combat_debuff_magnitudes.py</c> reads both and fails if they
        /// disagree, so the restatement cannot drift.
        /// </summary>
        public static int PointsFor(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.Bullet           => 1,
            CombatHitClass.Strike           => 8,
            CombatHitClass.Debuff           => 12,
            CombatHitClass.MissileShockwave => 10,
            CombatHitClass.MissileBlast     => 20,
            CombatHitClass.MissileDirect    => 30,
            _                               => 0,
        };

        /// <summary>True for the classes whose drain is authored on a per-weapon effect asset
        /// instead (see the type doc). This table contributes nothing for those.</summary>
        public static bool DrainAuthoredPerWeapon(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Debuff || hitClass == CombatHitClass.Strike;

        /// <summary>Signed magnitude this class drains from EACH element it touches (negative),
        /// in <c>ApplyElementalEffect</c>'s normalized units. Zero for a per-weapon class.</summary>
        public static float PerElementFor(CombatHitClass hitClass) =>
            DrainAuthoredPerWeapon(hitClass)
                ? 0f
                : -PointsFor(hitClass) / PointsPerLevel * NormalizedPerLevel;

        /// <summary>
        /// The missile class a latch rank stands for - the inverse of
        /// <see cref="CombatHitClasses.MissileProximityRank"/>, needed because the latch reports
        /// the RANK (a small stable int) rather than the class it superseded.
        ///
        /// <para>Rank 0 means "nothing was superseded" and is never passed here:
        /// <see cref="Apply"/> guards on <c>supersededRank &gt; 0</c>, which is the only thing
        /// that makes the netting below correct. The fallback exists so the switch is total and
        /// is not a meaningful answer - do not read it as one.</para>
        /// </summary>
        public static CombatHitClass ClassForMissileRank(int rank) => rank switch
        {
            1 => CombatHitClass.MissileShockwave,
            2 => CombatHitClass.MissileBlast,
            3 => CombatHitClass.MissileDirect,
            _ => CombatHitClass.Bullet,   // unreachable from Apply - see the guard above
        };

        /// <summary>
        /// Drain the victim for the hit that was just ADMITTED by the latch.
        /// </summary>
        /// <param name="supersededRank">
        /// The latch's own report of what this admission replaces (0 = a fresh hit). A rocket
        /// upgrading from shockwave to direct has already bitten the shockwave's petal, so only
        /// the DIFFERENCE is applied — exactly as <c>CombatHitScoring</c> credits only the
        /// difference in points.
        /// </param>
        public static void Apply(IVesselStatus victim, CombatHitClass hitClass, int supersededRank,
                                 ElementalDebuffSources source)
        {
            if (victim == null) return;

            float magnitude = PerElementFor(hitClass);
            if (magnitude >= 0f) return;                 // per-weapon class, or an unpriced one

            if (supersededRank > 0)
                magnitude -= PerElementFor(ClassForMissileRank(supersededRank));
            if (magnitude >= 0f) return;                 // nothing left to take

            var resources = victim.ResourceSystem;
            if (resources == null) return;

            for (int i = 0; i < Elements.Length; i++)
                resources.ApplyElementalEffect(Elements[i], magnitude, DurationSeconds, source);
        }
    }
}
