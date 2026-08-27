namespace CosmicShore.Data
{
    /// <summary>
    /// How a Friction run finished. Written server-side by FrictionController and
    /// replicated to every client alongside the final scores, so end-game UI can tell
    /// "someone hit the crystal target" apart from the two no-winner outcomes.
    /// </summary>
    public enum FrictionOutcome
    {
        /// <summary>A player reached the per-intensity crystal target — there is a winner.</summary>
        TargetReached = 0,
        /// <summary>The clock ran out before anyone reached the target — nobody wins.</summary>
        TimeExpired = 1,
        /// <summary>The hunters eliminated every human player — nobody wins.</summary>
        AllHumansEliminated = 2,
    }
}
