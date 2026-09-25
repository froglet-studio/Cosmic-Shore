namespace CosmicShore.Data
{
    /// <summary>
    /// A vessel action that <b>no element upgrades</b> - core flight rather than one of the hull's
    /// four elemental abilities. The Squirrel's drift is the first: it is how the vessel carves a
    /// corner its turn rate could not otherwise make, every pilot always has it, and no element
    /// touches it.
    ///
    /// <para>This is the KEY of a non-elemental ability lockup card, and it exists for the same
    /// reason <see cref="Element"/> keys the elemental ones: a card is addressed by a compile-time
    /// name rather than by a string a prefab can typo, and a member added here is the whole of what
    /// a new non-elemental ability costs.</para>
    ///
    /// <para>A core card draws the ability plate, its gauge, its cooldown veil and its control chip
    /// and <b>no element cell at all</b> - there is no flower above it, because there is no element
    /// to fill. The cards sit to the LEFT of the four elemental ones, in
    /// <see cref="CosmicShore.UI.VesselHUDView.CoreAbilityDisplayOrder"/> order, so the elemental row
    /// still reads left-to-right as charge / mass / space / time with nothing interleaved.</para>
    ///
    /// <para>Static values, like every enum in this project, so Unity serialization cannot drift.</para>
    /// </summary>
    public enum CoreAbility
    {
        None = 0,

        /// <summary>
        /// Carving a turn the hull's own rotation rate cannot reach. Core flight on every vessel
        /// that has it, bound to a trigger, upgraded by nothing - and the reason this enum exists.
        /// </summary>
        Drift = 1,
    }
}
