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
    /// <para>A core card draws the ability plate, its gauge, its cooldown veil and its control
    /// chip. Above it, where an elemental card carries its flower, it carries an <b>emblem</b> if
    /// its binding names one and <b>nothing at all</b> if it does not - there is never a flower,
    /// because there is no element to fill. <see cref="Drift"/> is the bare case;
    /// <see cref="OmniCrystal"/> is the emblem case. The cards sit to the LEFT of the four
    /// elemental ones, in
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

        /// <summary>
        /// What this hull does when it flies through an OMNI crystal. Non-elemental for the
        /// literal reason the name gives: an omni crystal is every element and therefore none, so
        /// no flower belongs over it - its card carries the omni crystal's own emblem instead.
        ///
        /// <para>It is bound to no input on any vessel and never will be: collecting a crystal is
        /// CONTACT, so the card draws no control chip (<c>FullSpeedStraightAction</c>, the input
        /// enum's zero, is this project's passive sentinel and maps to no physical control).</para>
        ///
        /// <para>Every vessel has this card, because every vessel can fly through a crystal. What
        /// it DOES differs per hull, measured off the shipped <c>vesselCrystalEffects</c>: the
        /// Squirrel lays a ring of SHIELDED prisms, the Manta detonates its planted bombs, the
        /// Dolphin, Rhino and Serpent each fire a blast, the Sparrow takes a debuff ward - and two
        /// hulls do nothing a card could draw, the Urchin (haptics only) and the Scarab (nothing
        /// at all, deliberately: its SKIMMER forges the crystal into a ball before the hull ever
        /// reaches it, so the hull's own branch is empty by design). A hull whose card has no art
        /// yet renders LOCKED, which is the honest state and exactly what a locked card is
        /// for.</para>
        /// </summary>
        OmniCrystal = 2,
    }
}
