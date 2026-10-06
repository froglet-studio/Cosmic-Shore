namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A prismscape built as a STACK of nested layers - sheets one inside another, stitched by struts that cross
    /// them (<see cref="NestedGyroidFlora"/>). Implemented by the structure that owns the prisms (for a flora, its
    /// <see cref="LifeForm"/>, reached through <see cref="HealthPrism.LifeForm"/>).
    ///
    /// <para>It exists because a stack defeats the surface ride's ground rule. <see cref="BlockscapeFollower"/>
    /// takes the NEAREST prism as its floor, which on a single shell is the shell; in a stack whose layers lie
    /// closer together than its plates lie apart, the nearest prism is often on the next layer, and the rider
    /// wanders through the stack on its own (measured on the nested gyroid: an in-sheet roll visited five of
    /// seven sheets, Tools/Build/nested_gyroid_harness). Nothing geometric can fix that, because how far apart
    /// two layers are is not something a prism's position says. So the structure SAYS it, and the ride holds
    /// its layer unless the pilot pitches toward the next one (Docs/ECOSYSTEM.md §58.4).</para>
    /// </summary>
    public interface ILayeredPrismscape
    {
        /// <summary>
        /// Where <paramref name="prism"/> sits through the stack: an integer that increases one step per layer
        /// crossed, in the direction every one of the structure's prisms' local +z (forward) points. Sheet i of
        /// the nested gyroid is 2i and a strut between sheets i and i+1 is 2i+1. False when the prism is not
        /// (or is no longer) part of this structure.
        /// </summary>
        bool TryGetStackCoordinate(Prism prism, out int coordinate);
    }
}
