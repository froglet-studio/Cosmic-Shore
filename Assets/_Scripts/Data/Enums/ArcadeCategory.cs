namespace CosmicShore.Data
{
    /// <summary>
    /// The arcade's four categories (Garrett, 2026-11-17, Docs/ArcadeRestructure/PLAN.md): each element category trains
    /// for one arena game, and Party is everything else. Mass and Space are ONE category, because creation and
    /// destruction are scored and trained together as a pair, as Brood Rush balances them.
    /// Answered by <see cref="ModeGenre.CategoryFor"/>.
    /// </summary>
    public enum ArcadeCategory
    {
        /// <summary>No category: the arcade filter's "All".</summary>
        None = 0,
        /// <summary>A race. Trains for Regatta.</summary>
        Time = 1,
        /// <summary>Working other pilots over. Trains for Broadside.</summary>
        Charge = 2,
        /// <summary>Control of the cell: lay your mass, tear theirs out. Trains for Brood Rush.</summary>
        MassSpace = 3,
        /// <summary>For fun; bends any rule it likes and says which in its description. Trains for nothing.</summary>
        Party = 4,
    }
}
