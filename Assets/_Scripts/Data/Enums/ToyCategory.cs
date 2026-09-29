namespace CosmicShore.Data
{
    // Always assign static numeric values - Unity serialization drift on enum reordering
    // silently rewrites every authored asset that stores one.

    /// <summary>
    /// What kind of thing a freestyle <b>Toy</b> is, keyed on <i>what it changes</i>.
    ///
    /// <para>The categories are the FUNDAMENTALS a toy composes with, not a taxonomy invented for
    /// a menu: a toy earns its place by working through Vessel / Domain / Cell / Prisms rather
    /// than around them, so "which fundamental does this one reach for?" is the only division
    /// that stays true as toys are added. A toy that fits none of these is the signal to have the
    /// fundamentals conversation, not to add a fourth member here.</para>
    ///
    /// <para>Declared in CODE on each <c>ToyDefinitionSO</c> subclass rather than serialized on
    /// the asset: a toy's category is a property of what the toy IS, and an authored field is a
    /// field that can disagree with the behaviour underneath it. It is never deserialized, so no
    /// member is a silent default for a missing value.</para>
    /// </summary>
    public enum ToyCategory
    {
        /// <summary>
        /// Changes YOU - the hull you fly or the colours you wear. Composes with <b>Vessel</b>
        /// and <b>Domain</b>. Nothing about the world changes; you do.
        /// </summary>
        Pilot = 0,

        /// <summary>
        /// Changes WHERE YOU ARE - which cell you are in, or takes you out of it entirely.
        /// Composes with <b>Cells</b>. The heaviest class: a world arrives or leaves.
        /// </summary>
        World = 1,

        /// <summary>
        /// LEAVES SOMETHING BEHIND that lives on without you - conserved prism mass, or a
        /// population. Composes with <b>Prisms/Mass</b> and <b>Flora &amp; Fauna</b>. What it
        /// creates is an ordinary citizen of the cell, on no clock and under no special rule.
        /// </summary>
        Creation = 2,
    }
}
