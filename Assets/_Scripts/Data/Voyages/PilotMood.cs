namespace CosmicShore.Data
{
    /// <summary>
    /// How a pilot feels about a voyage (Docs/Voyages/MILESTONE_2.md; Shombith, 2026-11-24: "mood effects ok, refusals
    /// yes"): read from the voyage type's Keen, Reluctant and Refuse factions (<see cref="VoyageTypeInfo"/>) and the
    /// pilot's faction by <see cref="VoyageMood.Of"/>. A Refuses pilot keeps the voyage from sailing until the seat is
    /// changed. Values are fixed for serialization.
    /// </summary>
    public enum PilotMood
    {
        None = 0,
        Keen = 1,
        Steady = 2,
        Reluctant = 3,
        Refuses = 4,
    }
}
