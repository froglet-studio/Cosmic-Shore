namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A thing that throws a SHOCKWAVE FRONT through the mass around it
    /// (Docs/PRISM_ANIMATION.md §4.7.3).
    ///
    /// <para><b>It asks for a CAPABILITY, never for a type.</b> <see cref="PrismWakeSource"/> knows
    /// nothing about missiles: it asks whichever component on its own GameObject answers this
    /// interface how far its blast reaches, and publishes a front out to exactly that radius. Today
    /// the one carrier is the Sparrow's HEAVY skyburst (<c>Projectile</c>); the interface is what
    /// lets the next one arrive without this file, the publisher or the shader learning its name.</para>
    ///
    /// <para><b>It answers a REACH, not a velocity.</b> The first cut of this effect was a wake about
    /// a fast vessel's PATH, so it asked for velocity and gated on speed — and its engage speed was
    /// authored above the top speed of the hull the mode actually flew, so the window never opened
    /// and the effect was reported as "too subtle" rather than as absent. A warhead's criterion was
    /// never speed: it is whether there is a warhead, and how far it goes off. Both of those are
    /// facts the carrier already holds for gameplay reasons, so neither can be authored wrong
    /// independently of the weapon it describes.</para>
    ///
    /// <para><b>The reach is asked for EVERY frame, not measured once.</b> A skyburst's warhead
    /// radius is a multiple of the round's own fitted hit radius, and the round swells up to 20x in
    /// the first fifth of its flight as MASS scales it — so the front a rocket throws grows with the
    /// blast it is drawing. Caching it would draw the launch bay's blast all the way to the target.</para>
    /// </summary>
    public interface IPrismWakeCarrier
    {
        /// <summary>
        /// This frame's shockwave reach in world units — the radius the blast will actually reach —
        /// or false for "no shockwave right now": a round that has not launched, one carrying no
        /// warhead (the Sparrow's BASE rocket), one already detonating, anything mid-teardown.
        /// Returning false is the ordinary way to switch a front off; the source eases it out rather
        /// than cutting it.
        /// </summary>
        bool TryGetShockwaveReach(out float reach);
    }
}
