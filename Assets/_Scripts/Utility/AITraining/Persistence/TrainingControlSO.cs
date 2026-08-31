using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Single hand-off asset between the editor's Learn button and the runtime
    /// auto-launcher. The editor sets the four cross-references and flips
    /// AutoStartOnPlay; the auto-launcher reads them when play mode begins and
    /// drives the entire Bootstrap → Auth → Menu → Game flow without further
    /// user input.
    ///
    /// Living as an SO (rather than EditorPrefs / SessionState) means the runtime
    /// can read it inside a build, not just inside the editor — so a packaged
    /// trainer build is also possible.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingControl",
        menuName = "ScriptableObjects/AI Training/Control",
        order = 199)]
    public class TrainingControlSO : ScriptableObject
    {
        [Header("Auto-Launch")]
        [Tooltip("If true, the auto-launcher takes over once play mode begins: it waits for " +
                 "ApplicationState.MainMenu, configures GameDataSO, and launches the scenario's " +
                 "game scene with all-AI players.")]
        public bool AutoStartOnPlay;

        [Header("Active Scenario")]
        public TrainingScenarioSO Scenario;
        public TrainingSessionStateSO State;
        public TrainingArchiveSO Archive;
        public TrainingTelemetrySO Telemetry;

        [Header("Deployment (player-vs-trained-AI)")]
        [Tooltip("If true, AI vessels in normal (non-training) gameplay receive trained genomes " +
                 "from the archive. Lets the user immediately play against the latest training.")]
        public bool DeployArchiveInNormalPlay = true;
    }
}
