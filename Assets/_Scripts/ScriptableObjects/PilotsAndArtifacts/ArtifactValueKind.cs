namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// How an artifact's rank value applies to its stat's base value (<see cref="ArtifactSO.Apply"/>). Values serialize
    /// into artifact assets: never reorder.
    /// </summary>
    public enum ArtifactValueKind
    {
        Add = 1,        // base + value (a count, units, seconds)
        Percent = 2,    // base x (1 + value / 100)
        Multiply = 3,   // base x value
        Set = 4,        // the value replaces the base (a capacity: 4 pellets become 5)
    }
}
