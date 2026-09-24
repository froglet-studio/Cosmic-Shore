namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A thing that throws a SHOCKWAVE FRONT through the mass around it
    /// (Docs/PRISM_ANIMATION.md §4.7.3).
    ///
    /// <para><b>It asks for a CAPABILITY, never for a type.</b> <see cref="PrismWakeSource"/> knows
    /// nothing about explosions: it asks whichever component on its own GameObject answers this
    /// interface how far its blast reaches and how far through its sweep it has got, and publishes a
    /// front accordingly. Today the one carrier is the Sparrow's HEAVY skyburst WARHEAD — the
    /// 10-point shockwave blast (<c>AOEExplosion</c>, granted a source at its one spawn site in
    /// <c>ProjectileDetonatorSO</c>); the interface is what lets the next one arrive without this
    /// file, the publisher or the shader learning its name.</para>
    ///
    /// <para><b>It answers a REACH, not a velocity.</b> The first cut of this effect was a wake about
    /// a fast vessel's PATH, so it asked for velocity and gated on speed — and its engage speed was
    /// authored above the top speed of the hull the mode actually flew, so the window never opened
    /// and the effect was reported as "too subtle" rather than as absent. A blast's criterion was
    /// never speed: it is whether there is a blast, and how far it goes off. Both are facts the
    /// carrier already holds for gameplay reasons, so neither can be authored wrong independently of
    /// the weapon it describes.</para>
    ///
    /// <para><b>It also answers a PROGRESS, and that is what makes the front the blast's OWN
    /// wavefront rather than a pulse beside it.</b> A shockwave blast already has a travelling
    /// spherical front — its trigger volume expands from nothing to its full radius over its own
    /// authored duration, and that radius is the number its damage pass uses — so the front's
    /// position is not a free parameter and must not be integrated on a clock of its own. The
    /// carrier reports where its wavefront is; the source maps that into the shell's legal travel
    /// band and draws it. The two therefore cannot drift, and there is no pulse rate to author
    /// wrong (a free-running clock was what the travelling-round cut needed, and it went away with
    /// that carrier).</para>
    ///
    /// <para><b>Both are asked EVERY frame, not measured once.</b> A progress obviously moves; the
    /// reach is asked live too, because a carrier is entitled to resize (the previous carrier's
    /// warhead radius grew 20x in flight as MASS scaled the round). A cached reach draws the wrong
    /// blast.</para>
    ///
    /// <para><b>A carrier must answer TRUE with progress 0 before its front starts moving.</b> That
    /// one frame is what makes the residency swap invisible: the source publishes a live slot with a
    /// strength of exactly zero, so <c>PrismWake.Flush</c> swaps the prisms in the blast's volume to
    /// the high-poly mesh on a frame where the map provably cannot have moved a vertex (§4.2). An
    /// explosion gets this for free — <c>Initialize</c> and <c>Detonate</c> run a frame before its
    /// expansion loop does — and a carrier that started reporting mid-sweep would POP.</para>
    /// </summary>
    public interface IPrismWakeCarrier
    {
        /// <summary>
        /// This frame's shockwave, or false for "no shockwave right now": a blast that has been
        /// cancelled, one whose sweep is over, anything mid-teardown. Returning false is the
        /// ordinary way to switch a front off; the source eases it out rather than cutting it.
        /// </summary>
        /// <param name="reach">The radius the blast will actually reach, world units.</param>
        /// <param name="progress01">How far through its single outward sweep the wavefront has got:
        /// 0 on the frame the blast is armed and before it has expanded at all, 1 at full reach.</param>
        bool TryGetShockwave(out float reach, out float progress01);
    }
}
