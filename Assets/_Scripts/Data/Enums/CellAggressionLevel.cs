namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired NetworkVariables and SOAP asset references silently.
    //
    // Fauna aggression state within a Cell. Since the 3-phase collapse this maps 1:1
    // onto CellPhase (Calm→Level0, Restless→Level1, Frenzy→Level2) - the phase IS the
    // aggression band. The enum is kept distinct only because it indexes the per-level
    // multiplier arrays (cadence / consume-radius / speed) as a clean 0/1/2 index.
    // Flora are no longer staggered on separate phase rungs: they grow + plant at a
    // steady rate until Frenzy, so there is one ladder now, not two. See ECOSYSTEM.md §0.
    //
    // Level behaviors:
    //   Level0 - Fauna head toward the cell's crystal; normal cleanup cadence and avoidance.
    //   Level1 - Fauna head toward the nearest opposing-color centroid; tighter cadence,
    //            wider consume radius, higher speed.
    //   Level2 - Fauna head toward the nearest centroid of ANY color, disable friendly
    //            avoidance (same-domain fauna + ships), and are immune to danger prisms.
    //            Intended to be rare when the cell is truly overwhelmed.
    public enum CellAggressionLevel
    {
        Level0 = 0,
        Level1 = 1,
        Level2 = 2,
    }
}
