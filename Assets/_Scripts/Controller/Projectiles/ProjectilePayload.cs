namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What ONE flight of a round is carrying — the per-shot half of a projectile's behaviour,
    /// alongside the prefab's authoring, which says WHAT those payloads are and HOW BIG.
    ///
    /// <para>This exists because the Sparrow fires two missiles out of one bay: a cheap BASE
    /// rocket fired on the wing, and a HEAVY one fired from the turret stance. They differ in
    /// four things and share everything else — the model, the bay animation, the tail, the
    /// direct hit, the growth curve, the pool. A second prefab would express the difference and
    /// then charge for it forever, because every later skyburst edit would have to land twice
    /// (the "a DTO is a second place every field has to be added" failure). A per-shot parameter
    /// set is the shape this codebase already uses for exactly this — <c>spareOwnDomain</c>,
    /// <c>stopOnFirstPrismImpact</c> and <c>flightGrowthFactor</c> are all snapshots taken at
    /// fire time and handed to the gun.</para>
    ///
    /// <para><see cref="Default"/> is what every other gun in the fleet fires, and it is the
    /// value <see cref="Projectile.Initialize"/> resets to — so a caller that says nothing gets
    /// byte-identical behaviour to before this type existed, and a pooled reissue can never
    /// inherit the previous shot's payload.</para>
    /// </summary>
    public readonly struct ProjectilePayload
    {
        /// <summary>
        /// This flight carries its proximity fuze and the warhead blast the fuze exists to
        /// deliver. False collapses BOTH to zero — they are one weapon, not two knobs: the fuze
        /// is only ever the trigger for the warhead, and arming one without the other would draw
        /// a threat volume that cannot go off (or fire a shockwave nothing can set off early).
        /// The prefab still authors the radii.
        /// </summary>
        public readonly bool ArmWarhead;

        /// <summary>
        /// This flight's detonation may spawn the mass-CREATING blasts its effect assets list
        /// (<see cref="AOEExplosion.CreatesMass"/>). False drops them and keeps the destructive
        /// ones, so a round can lose its prism cairn without losing the explosion that opens a
        /// hole.
        /// </summary>
        public readonly bool CreateMassOnDetonation;

        /// <summary>
        /// This flight lays a line of prisms behind it as it travels, at the spacing, size and
        /// cap the projectile prefab authors. Off by default: a round that is not authored for
        /// one has nothing to lay, and a round that is must still be TOLD to, because that is
        /// the difference between the Sparrow's two missiles.
        /// </summary>
        public readonly bool LayPrismTrail;

        public ProjectilePayload(bool armWarhead, bool createMassOnDetonation, bool layPrismTrail)
        {
            ArmWarhead = armWarhead;
            CreateMassOnDetonation = createMassOnDetonation;
            LayPrismTrail = layPrismTrail;
        }

        /// <summary>Everything the prefab authors, and no flight trail — the fleet's behaviour
        /// before this type existed, and what an un-specified shot gets.</summary>
        public static ProjectilePayload Default => new(true, true, false);
    }
}
