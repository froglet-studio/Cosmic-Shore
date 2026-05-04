using CosmicShore.Data;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// One contributor to the per-episode fitness score. Each component:
    ///   - registers any tunable genes it cares about (rare; weights live in the profile)
    ///   - observes the episode by reading DecisionContext + RoundStats per frame
    ///   - emits a single Raw score at the end of the episode
    ///
    /// Aggregation is the runner's job. Components don't need to know about each other.
    /// </summary>
    public interface IFitnessComponent
    {
        string Label { get; }
        void OnEpisodeStart(DecisionContext ctx);
        void OnFrame(DecisionContext ctx);
        float Evaluate(DecisionContext ctx, IRoundStats roundStats);
    }
}
