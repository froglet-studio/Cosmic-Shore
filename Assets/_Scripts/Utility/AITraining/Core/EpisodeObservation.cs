using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// The slim per-frame view of a trainee that fitness components read.
    /// Written once per frame by TrainingModulator; never written by fitness code.
    ///
    /// Deliberately tiny: the heavyweight world model (target/threat/prism scans)
    /// died with the parallel policy pilot. Fitness is now judged from what the
    /// vessel DID (RoundStats) plus this handful of live signals — which is also
    /// what keeps the observer's per-frame cost near zero during overnight runs.
    /// </summary>
    public class EpisodeObservation
    {
        public IVesselStatus VesselStatus;
        public string PlayerName;
        public Vector3 Position;
        public float Speed;
        public bool IsBoosting;
        public bool IsDrifting;
        public float EpisodeTime;
    }
}
