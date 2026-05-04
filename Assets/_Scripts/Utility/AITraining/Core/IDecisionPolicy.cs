namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// One behavior module — e.g. "seek crystal", "skim prism", "boost when clear".
    /// Implementations should be pure: read DecisionContext, return a DecisionOutput,
    /// keep all tunable values in the genome via gene names registered in
    /// RegisterGenes (called once at process start).
    ///
    /// Adding a new policy is the supported way to grow the AI's sophistication.
    /// </summary>
    public interface IDecisionPolicy
    {
        /// <summary>
        /// Stable name used as the module key in the genome. Must match the moduleName
        /// passed to GeneRegistry.Register so structural mutation can toggle the policy.
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Called once at boot. Implementations register every gene they intend to read
        /// from the genome here so the population layer knows the search space.
        /// </summary>
        void RegisterGenes();

        /// <summary>
        /// Called once per pilot per episode after the genome is loaded.
        /// Use this to cache values that don't change during the episode.
        /// </summary>
        void OnEpisodeStart(TrainingGenome genome) { }

        /// <summary>
        /// Per-frame decision. Return DecisionOutput.Zero to abstain.
        /// </summary>
        DecisionOutput Decide(DecisionContext ctx);

        /// <summary>
        /// Optional cleanup at end of an episode.
        /// </summary>
        void OnEpisodeEnd() { }
    }
}
