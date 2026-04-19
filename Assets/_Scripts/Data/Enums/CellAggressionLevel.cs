namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.
    //
    // Fauna aggression state within a Cell. Separately, the Cell also regulates flora
    // planting and growing via independent prism-count thresholds — these do not share
    // the same levels because the user spec staggers flora and fauna events along a
    // single prism-count axis (0 -> 1000 -> 4000 -> 8000 -> 10000 -> 15000).
    //
    // Level behaviors:
    //   Level0 - Fauna head toward the cell's crystal; normal cleanup cadence and avoidance.
    //   Level1 - Fauna head toward the nearest opposing-color centroid; tighter cadence,
    //            wider consume radius, higher speed.
    //   Level2 - Fauna head toward the nearest centroid of ANY color, disable friendly
    //            avoidance (same-domain fauna + ships), and are immune to danger prisms.
    //            Intended to be rare (~1 in 10 matches) when the cell is truly overwhelmed.
    public enum CellAggressionLevel
    {
        Level0 = 0,
        Level1 = 1,
        Level2 = 2,
    }
}
