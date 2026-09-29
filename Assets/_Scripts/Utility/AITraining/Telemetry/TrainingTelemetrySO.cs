using System;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// SOAP runtime data container for the editor window and any in-game HUD that
    /// wants to show training progress. The runner is the single writer; UI just
    /// reads.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingTelemetry",
        menuName = "ScriptableObjects/AI Training/Telemetry",
        order = 204)]
    public class TrainingTelemetrySO : ScriptableObject
    {
        [Header("Runtime Status")]
        public bool IsRunning;
        public string ActiveScenario;
        public int Generation;
        public int EpisodesCompleted;
        public int EpisodesPlanned;
        public float CurrentBestFitness;
        public float LastEpisodeFitness;
        public string LastEpisodeBreakdown;

        [Header("Lifecycle Events")]
        public ScriptableEventNoParam OnSessionStarted;
        public ScriptableEventNoParam OnSessionStopped;
        public ScriptableEventNoParam OnEpisodeStarted;
        public ScriptableEventNoParam OnEpisodeEnded;
        public ScriptableEventNoParam OnGenerationEvolved;
        public ScriptableEventNoParam OnArchiveDeployed;

        public Action OnAnyChange;

        public void RaiseAnyChange() => OnAnyChange?.Invoke();
    }
}
