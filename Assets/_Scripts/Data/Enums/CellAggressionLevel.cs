namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired NetworkVariables and SOAP asset references silently.
    //
    // Fauna aggression state within a Cell, derived from the cell's CellPhase.
    // Separately, the Cell also regulates flora planting and growing via independent
    // phase gates — these do not share levels because the user spec staggers flora
    // and fauna events along a single prism-count axis.
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
