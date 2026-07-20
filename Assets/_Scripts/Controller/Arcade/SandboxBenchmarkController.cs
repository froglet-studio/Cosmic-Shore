using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Endless free-flight controller for the benchmark / stress-test scene, on the
    /// networked single-host model every mode now uses: the always-on Relay host loads the
    /// scene, <c>ServerPlayerVesselInitializerWithAI</c> spawns the human's Squirrel plus
    /// the Settings-configured AI crowd (<c>GameDataSO.RequestedAIBackfillCount</c>, set by
    /// <c>BenchmarkSceneLauncher</c>), and the environment spawners ramp load - exactly the
    /// sustained workload a stress test wants.
    ///
    /// Extends the blitz controller for its keeper wiring (live score feed) but never ends:
    /// the scene wires NO turn monitors, so the turn-end path is unreachable, and
    /// <see cref="SetupNewTurn"/> auto-begins the turn instead of showing the Ready button -
    /// the scene "just works" on entry with zero clicks.
    /// </summary>
    public class SandboxBenchmarkController : MultiplayerWildlifeBlitzController
    {
        [Header("Sandbox / Benchmark")]
        [SerializeField, Tooltip("Begin the turn automatically after setup so the scene 'just works' " +
                                 "on entry - no Ready button click needed.")]
        bool autoStart = true;

        [SerializeField, Min(0f), Tooltip("Delay before auto-beginning, so spawners and DI settle first.")]
        float autoStartDelaySeconds = 1f;

        protected override bool ShowEndGameSequence => false;

        /// <summary>
        /// Server-only (the base round flow only calls this on the server). Skips the base's
        /// ShowReadyButton_ClientRpc and auto-begins: the countdown RPC activates players and
        /// starts the turn on every peer.
        /// </summary>
        protected override void SetupNewTurn()
        {
            if (autoStart && IsServer)
                Invoke(nameof(AutoBegin), autoStartDelaySeconds);
        }

        void AutoBegin()
        {
            if (countdownTimer != null)
                StartCountdownTimer();   // countdown → OnCountdownTimerEnded → SetPlayersActive + StartTurn (ClientRpc)
            else
                OnCountdownTimerEnded(); // no countdown wired → activate directly
        }
    }
}
