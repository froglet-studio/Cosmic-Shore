namespace CosmicShore.Data
{
    /// <summary>
    /// The Milestone 2 voyage classes (Docs/Voyages/MILESTONE_2.md; Shombith, 2026-11-24: "class names ok"): a label on a
    /// vessel that says what it is good for on a voyage. A class is never a kit change (CLAUDE.md, a game mode never
    /// changes the vessel); which vessels carry which class and which classes each voyage wants live in
    /// <c>VoyageClassesSO</c>. Values are fixed for serialization.
    /// </summary>
    public enum VoyageClass
    {
        None = 0,
        Guardian = 1,
        Protector = 2,
        Destroyer = 3,
        Cultivator = 4,
        Hunter = 5,
        Shepherd = 6,
        Pathfinder = 7,
    }
}
