namespace CosmicShore.Data
{
    /// <summary>
    /// A capability a vessel class has, named once so pilots and artifacts can say what they need
    /// (Docs/PILOTS_AND_ARTIFACTS.md §4). A vessel declares the keywords it satisfies
    /// (<c>SO_Vessel.Keywords</c>); a pilot or artifact declares the ones it requires
    /// (<see cref="VesselRequirement"/>); a squad slot only offers what the chosen vessel meets.
    ///
    /// <para><b>Generalise first.</b> A keyword names a concept most of the fleet shares, so one
    /// pilot reaches many hulls: a trail is the 1-D case of <see cref="BasePrismProduction"/>, which
    /// is why a hull with no trail but a cadence of laid mass still qualifies. Add a member only
    /// when a concrete pilot or artifact needs a set no existing keyword names. This is a vocabulary
    /// the fleet grows into, not a per-feature flag.</para>
    ///
    /// <para>Values are fixed and never reordered: they serialize into vessel, pilot and artifact
    /// assets. A new member takes the next value after the highest one ON THE BASE BRANCH, never
    /// "the next free id" in a branch another branch is also extending.</para>
    /// </summary>
    public enum VesselKeyword
    {
        None = 0,

        /// <summary>Lays mass continuously or on a cadence. A trail is the 1-D case.</summary>
        BasePrismProduction = 1,

        /// <summary>A speed burst with a cooldown or a charge.</summary>
        Boost = 2,

        /// <summary>Fires something with a flight time.</summary>
        Projectile = 3,

        /// <summary>A contact skimmer that cuts or steals.</summary>
        Blade = 4,

        /// <summary>A vessel crystal explosion.</summary>
        CrystalBlast = 5,

        /// <summary>Can ride a prismscape (<c>TrailFollower</c> / <c>BlockscapeFollower</c>).</summary>
        Attach = 6,

        /// <summary>Tows or anchors something on a chain or line.</summary>
        Tether = 7,
    }
}
