using CosmicShore.Data;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// One contributor to the per-episode fitness score. Each component:
    ///   - observes the episode by reading the trainee's EpisodeObservation per frame
    ///   - emits a single Raw score at the end of the episode, usually from RoundStats
    ///
    /// Aggregation is the runner's job. Components don't know about each other, and
    /// they never mutate anything — they are witnesses, not participants.
    /// </summary>
    public interface IFitnessComponent
    {
        string Label { get; }
        void OnEpisodeStart(EpisodeObservation obs);
        void OnFrame(EpisodeObservation obs);
        float Evaluate(EpisodeObservation obs, IRoundStats roundStats);
    }
}
