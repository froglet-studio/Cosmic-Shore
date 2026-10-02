namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// A sensor populates DecisionContext with world state for the policies to consume.
    /// Sensors run before policies each frame. Keeping them as separate objects (rather
    /// than baking the sampling into each policy) means the same world query is paid
    /// for once even when multiple policies want it.
    /// </summary>
    public interface ITrainingSensor
    {
        /// <summary>
        /// Called once when the pilot is installed on a vessel. The sensor caches any
        /// references it needs from the vessel here.
        /// </summary>
        void Bind(CosmicShore.Gameplay.IVessel vessel);

        /// <summary>
        /// Called once at the start of every episode so the sensor can clear per-run
        /// caches and reset accumulators.
        /// </summary>
        void OnEpisodeStart();

        /// <summary>
        /// Called once per pilot Update before any policy runs. Writes findings into
        /// the supplied context.
        /// </summary>
        void Sample(DecisionContext ctx);
    }
}
