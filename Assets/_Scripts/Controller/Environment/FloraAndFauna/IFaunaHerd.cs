namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A HERD: a leash whose creatures are a special swarm that its holder's upgrades can let BREAK the ecology's rules
    /// (the designer, 2026-10-21: "these are a special swarm that can be upgraded to break rules and should only starve down
    /// to a minimum size and break other rules if need be"). The Jacks hold one
    /// (<c>Controller/Vessel/R_VesselActions/JACKS.md</c>).
    ///
    /// <para>Every break is a method here, so the exceptions are fenced in one place and read by one reader each in
    /// <see cref="Fauna"/>:</para>
    /// <list type="bullet">
    /// <item><see cref="IFaunaLeash.Sustains"/>: the herd's starvation FLOOR (its first creatures, up to the minimum
    /// size, do not starve), the Angler keeper's exception grown to a herd. Predation and jousts still kill.</item>
    /// <item><see cref="EatsShields"/>: shielded (never super-shielded) mass is food for this creature.</item>
    /// <item><see cref="FeedsPerOffspringScale"/>: the holder scales how many feeds a birth takes.</item>
    /// <item><see cref="Born"/>: a herded creature's offspring joins the herd.</item>
    /// </list>
    /// A creature with no herd, or on a plain <see cref="IFaunaLeash"/>, keeps every rule.
    /// </summary>
    public interface IFaunaHerd : IFaunaLeash
    {
        /// <summary>True while shielded (never super-shielded) mass is food for <paramref name="creature"/>: the read of
        /// <c>Fauna.IsInedibleShield</c>. The Jacks: the Space level-5 upgrade.</summary>
        bool EatsShields(Fauna creature);

        /// <summary>Multiplies the feeds <paramref name="creature"/>'s species needs for a birth (1 = the species' own;
        /// smaller = faster breeding). The read of <c>Fauna.TryReproduce</c>. The Jacks: the Mass element.</summary>
        float FeedsPerOffspringScale(Fauna creature);

        /// <summary><paramref name="parent"/>, a creature of this herd, gave birth to <paramref name="child"/> (on the peer
        /// that simulates it). The holder may leash the child into the herd.</summary>
        void Born(Fauna parent, Fauna child);
    }
}
