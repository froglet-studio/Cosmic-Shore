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
        [Tooltip("Overnight queue. Null means Learn runs Scenario alone. " +
                 "A missing reference on an older asset stays null.")]
        public TrainingScheduleSO Schedule;
        public TrainingSessionStateSO State;
        public TrainingArchiveSO Archive;
        public TrainingTelemetrySO Telemetry;

        [Header("Deployment (player-vs-trained-AI)")]
        [Tooltip("If true, AI vessels in normal (non-training) gameplay receive trained genomes " +
                 "from the archive. Lets the user immediately play against the latest training.")]
        public bool DeployArchiveInNormalPlay = true;

        [Tooltip("Off: intensities 1–3 fly the intensity-4 genome through IntensityDitherer. " +
                 "The stored genes are not copied or mutated for the lower tier. " +
                 "On: look up a genome stored for that exact intensity and fly it raw. " +
                 "A missing value on an older asset is off, so lower tiers stay dithered.")]
        public bool UseStoredGenomeForLowerIntensity;

        [Header("Run limits")]
        [Tooltip("Completed episodes before Learn stops on its own. -1 runs until Stop. " +
                 "A missing value on an older asset reads as 0, which the runner also treats as overnight.")]
        public int TargetEpisodes = -1;

        [Tooltip("Seconds one match may sit wedged before the runner force-ends it. " +
                 "0 means the 180 second default. This is not the scenario's episode cap.")]
        public float WatchdogSeconds = 180f;

        [Tooltip("Set by Play against trained AI. The launcher starts a normal match: " +
                 "the host stays human, IsGeneticTrainingSession stays off, and the deployment service " +
                 "installs TrainingPilot on the AI seats.")]
        public bool HumanPlaysThisLaunch;

        [Header("Overnight batch (optional, off unless you opt in)")]
        [Tooltip("Game-clock multiplier for a host-only training run. 0 and 1 leave Time.timeScale alone, " +
                 "so a missing field on an older asset cannot pause the match. Values above 1 speed the " +
                 "simulation, capped at 8. Ignored while another client is connected, and ignored for " +
                 "Play against trained AI. This does not write sticks, Course, or transforms, and it does " +
                 "not cull, decay, or despawn mass.")]
        public float SimulationTimeScale;

        [Tooltip("Pauses the audio listener for a training launch. Restored when the launcher is destroyed. " +
                 "Does not delete audio objects.")]
        public bool MuteAudio;

        [Tooltip("Disables Camera.main while a training launch is running, and enables it again on stop. " +
                 "Does not move the camera and does not suppress the speed tunnel.")]
        public bool DisableCameraRendering;

        public const float MaxSimulationTimeScale = 8f;

        /// <summary>
        /// 0 (an older asset that never had the field) and 1 leave the clock alone.
        /// Anything faster is clamped so a typo cannot run the physics at an absurd rate.
        /// </summary>
        public static float ResolveTimeScale(float authored)
        {
            if (authored <= 1f) return 1f;
            return Mathf.Min(authored, MaxSimulationTimeScale);
        }
    }
}
