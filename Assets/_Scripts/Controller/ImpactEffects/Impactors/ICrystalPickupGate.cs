namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A hull that sometimes does NOT take a crystal it touches (Garrett, 2026-10-25: "lets allow them to net the omicrystal
    /// and move it around" - the Jacks' netting hull would otherwise collect the omni crystal the moment it touched it, before
    /// the net could catch it). On the vessel's root, beside its <see cref="VesselImpactor"/>; a hull without one takes every
    /// crystal it touches, as always. Only a TOUCH is gated: a pickup a vessel hands over deliberately (a sweep, as the
    /// herding jacks' collection and the race rings do) still lands.
    /// </summary>
    public interface ICrystalPickupGate
    {
        /// <summary>False while the hull's touch must leave crystals alone.</summary>
        bool TakesCrystalsByContact { get; }
    }
}
