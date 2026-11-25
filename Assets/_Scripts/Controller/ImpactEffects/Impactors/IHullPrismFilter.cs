namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A hull whose TOUCH leaves particular prisms alone, asked per prism (<see cref="IHullContactGate"/> is the
    /// all-or-nothing twin). The Goby's hull leaves the pair's own prisms alone: its perch pads, the Pistol's dens and the
    /// ground its darts lay all sit where it perched, so turning away from a perch rammed and bounced off them (Will,
    /// 2026-11-24). On the vessel's root, beside its <see cref="VesselImpactor"/>; a hull without one touches every prism.
    /// Only a TOUCH is filtered: a contact a vessel hands over deliberately (a sweep) still lands.
    /// </summary>
    public interface IHullPrismFilter
    {
        /// <summary>True when the hull's touch must leave <paramref name="prism"/> alone (it neither rams it nor is hurt by it).</summary>
        bool LeavesAlone(Prism prism);
    }
}
