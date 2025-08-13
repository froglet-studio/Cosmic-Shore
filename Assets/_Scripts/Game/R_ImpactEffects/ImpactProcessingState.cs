namespace CosmicShore.Game
{
    /// <summary>Mutable state for a single impact processing pass.</summary>
    public sealed class ImpactProcessingState
    {
        /// <summary>When true, skip Crystal.ExecuteCommonVesselImpact().</summary>
        public bool SkipCrystalCommonImpact;
    }
}