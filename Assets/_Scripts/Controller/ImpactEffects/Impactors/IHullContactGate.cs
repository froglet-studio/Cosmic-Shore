namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A hull whose TOUCH sometimes leaves things alone: the Jacks' invisible netting hull sits in the middle of its net's
    /// mouth, and without this it would collect the omni crystal (Garrett, 2026-10-25: "lets allow them to net the omicrystal
    /// and move it around") and ram the prisms (2026-10-25: "i don't see prism moving i just see them getting destroy")
    /// before the net could take them. On the vessel's root, beside its <see cref="VesselImpactor"/>; a hull without one
    /// touches everything as always. Only a TOUCH is gated: a contact a vessel hands over deliberately (a sweep, as the
    /// herding jacks' collection and the race rings do) still lands.
    /// </summary>
    public interface IHullContactGate
    {
        /// <summary>False while the hull's touch must leave crystals alone.</summary>
        bool TakesCrystalsByContact { get; }

        /// <summary>False while the hull's touch must leave prisms alone (it neither rams them nor is hurt by them).</summary>
        bool TakesPrismsByContact { get; }
    }
}
