namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired NetworkVariables and SOAP asset references silently.
    public enum CellPhase
    {
        None = 0,
        Sprout = 1,
        Quiet = 2,
        Settled = 3,
        Restless = 4,
        Frozen = 5,
        Rabid = 6,
    }
}
