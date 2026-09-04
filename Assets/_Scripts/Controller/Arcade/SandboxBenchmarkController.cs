using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Endless free-flight controller for the benchmark / stress-test scene. Reuses the single-player
    /// pipeline (player + AI spawn, countdown, player activation) but never ends: with no TurnMonitor
    /// wired, <see cref="MiniGameControllerBase.EndTurn"/> is never reached, and <see cref="HasEndGame"/>
    /// is false. The human flies their Squirrel and AI Squirrels fly alongside while the environment
    /// spawners (flora/fauna/crystals/gyroids) ramp load up - exactly the sustained workload a stress
    /// test wants.
    ///
    /// Drop-in: replace the cloned reference scene's single-player controller with this, or keep that
    /// controller and simply remove its TurnMonitor (either yields an endless sandbox).
    /// </summary>
    public class SandboxBenchmarkController : SinglePlayerMiniGameControllerBase
    {
        [Header("Sandbox / Benchmark")]
        [SerializeField, Tooltip("Activate players automatically after setup so the scene 'just works' " +
                                 "on entry - no Ready button click needed.")]
        bool autoStart = true;

        [SerializeField, Min(0f), Tooltip("Delay before auto-activating, so spawners and DI settle first.")]
        float autoStartDelaySeconds = 1f;

        protected override bool HasEndGame => false;          // endless - never EndGame()
        protected override bool ShowEndGameSequence => false;

        protected override void SetupNewTurn()
        {
            // No TurnMonitor is wired in the benchmark scene, so the turn never ends on its own -
            // precisely what a free-flight stress test wants. Auto-begin for a frictionless entry.
            if (autoStart)
                Invoke(nameof(AutoBegin), autoStartDelaySeconds);
        }

        void AutoBegin()
        {
            if (countdownTimer != null)
                StartCountdownTimer();   // countdown → OnCountdownTimerEnded → SetPlayersActive + StartTurn
            else
                OnCountdownTimerEnded();  // no countdown wired → activate directly
        }
    }
}
