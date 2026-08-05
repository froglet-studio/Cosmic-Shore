namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired NetworkVariables and SOAP asset references silently.
    //
    // The cell's ecological state along its single state axis - live prism count
    // (Docs/ECOSYSTEM.md §1). There are exactly THREE active phases, and they map 1:1
    // onto the fauna aggression bands (CellAggressionLevel): the phase IS the aggression
    // band. (Earlier there were six rungs because flora-events and fauna-events were
    // staggered along separate thresholds; flora now grow + plant at a steady rate all
    // the way to Frenzy - the food web is the only down-force - so the staggering, and
    // the extra rungs that staged it, are gone. See ECOSYSTEM.md §0/§5.)
    //
    //   Calm    - low mass. Flora grow + plant freely; fauna idle toward the crystal (L0).
    //   Restless - mid mass. Fauna hunt the nearest opposing-color centroid (L1).
    //   Frenzy  - frenzy ceiling. Flora STOP; fauna seek any-domain density, drop friendly
    //             avoidance, and ignore danger prisms (L2). A cell only leaves Frenzy when
    //             an active force (fauna grazing / vessel abilities) eats its mass back
    //             below the Frenzy exit threshold - mass is conserved, there is no decay.
    public enum CellPhase
    {
        None = 0,
        Calm = 1,
        Restless = 2,
        Frenzy = 3,
    }
}
