
namespace CosmicShore.Data
{
    /// <summary>
    /// A vessel action that <b>no element upgrades</b> - the hull's own engine rather than one of its
    /// four elemental abilities. The Squirrel's skimming is the first: it is how the vessel banks
    /// boost energy, every pilot always has it, and it has no button and no cooldown.
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
        /// Grazing mass with the skimmer. The Squirrel's engine - it banks the boost energy every
        /// other Squirrel ability spends - and the reason this enum exists.
        /// </summary>
        Skim = 1,
    }
}
