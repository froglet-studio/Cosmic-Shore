namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marks a <see cref="ShipActionSO"/> whose entire effect is to SHOW OTHER PLAYERS what this
    /// vessel is lining up — a held preview of where its weapon would land, costing no resource,
    /// changing no motion, and destroying nothing. The Dolphin's Echo Sight is the only one today.
    ///
    /// <para><b>Why this exists rather than <see cref="AIPilot"/> naming the Dolphin's ability.</b>
    /// An autonomous pilot has a real reason to hold such an ability — it flies the same behaviour
    /// loop on every vessel, and "announce your aim while you commit to it" is a property of the
    /// LOOP, not of any one hull. Referencing <c>EchoSightActionSO</c> from the shared AI would put
    /// one vessel's ability inside the system that flies all eleven, and the next vessel to grow a
    /// telegraph would need an AI change to use it. With this, it needs one interface on its SO.</para>
    ///
    /// <para><b>The contract a telegraph has to keep, because the AI holds it blind.</b> It must be
    /// safe to press and release at arbitrary moments and for arbitrary durations: no cooldown to
    /// waste, no resource to drain, no ammunition to spend, and no effect on where the vessel goes.
    /// An ability that fails any of those is not a telegraph and must not carry this interface —
    /// the AI would be spending the vessel's economy every time it lined up a shot.</para>
    ///
    /// <para>It carries no members on purpose. The question it answers is "may a pilot hold this to
    /// announce its aim", which is a yes/no about the ability's NATURE; anything the pilot needs to
    /// know beyond that (which control it sits on) it gets from the binding via
    /// <see cref="R_VesselActionHandler.TryGetInputForAction{T}"/>.</para>
    /// </summary>
    public interface IAimTelegraphAction
    {
    }
}
