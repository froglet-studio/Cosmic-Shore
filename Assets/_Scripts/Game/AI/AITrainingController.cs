using System.Collections;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.AI
{
    /// <summary>
    /// Fully automated spectator training mode.
    /// Runs AI-only races in a loop with no human input required.
    ///
    /// Setup:
    /// 1. Use TrainingPlayerSpawnerAdapter with 3 AI entries (IsAI=true).
    /// 2. Set numberOfRounds = 1, numberOfTurnsPerRound = 1 on the game controller.
    /// 3. Assign the CrystalCollisionTurnMonitor so this controller knows the crystal target.
    /// 4. Assign the same PilotEvolution SO to this controller and to each AI vessel's
    ///    AIPilot + PilotFitnessTracker.
    /// 5. Press Play and walk away.
    /// </summary>
    public class AITrainingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] Arcade.MiniGameControllerBase gameController;
        [SerializeField] PilotEvolution evolution;
        [SerializeField] Arcade.CrystalCollisionTurnMonitor turnMonitor;
        [SerializeField] ScriptableEventBool toggleReadyButtonEvent;

        [Header("Training Config")]
        [SerializeField] int maxRaces = 10000;
        [SerializeField] float delayBetweenRaces = 1f;
        [SerializeField] float raceTimeoutSeconds = 120f;
        [SerializeField, Range(1, 4)] int trainingIntensity = 4;

        [Header("Crystal Target (fallback if turn monitor unavailable)")]
        [Tooltip("Set to 0 to read from the turn monitor automatically.")]
        [SerializeField] int fallbackCrystalTarget = 0;

        [Header("Spectator Camera")]
        [SerializeField] float cameraShowOthersInterval = 4f;
        [SerializeField] float cameraShowOthersDuration = 1.5f;

        int _racesCompleted;
        float _raceStartTime;
        bool _raceActive;
        int _crystalTarget;
        float _nextShowOtherTime;
        int _currentShowOtherIndex;
        Coroutine _cameraReturnCoroutine;

        void OnEnable()
        {
            if (gameData == null)
            {
                Debug.LogError("[AITraining] GameDataSO not assigned!", this);
                enabled = false;
                return;
            }

            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = trainingIntensity;

            gameData.OnMiniGameEnd += OnRaceEnd;
            gameData.OnMiniGameRoundStarted.OnRaised += OnRoundStarted;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
        }

        void OnDisable()
        {
            if (gameData == null) return;

            gameData.OnMiniGameEnd -= OnRaceEnd;
            gameData.OnMiniGameRoundStarted.OnRaised -= OnRoundStarted;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
        }

        void OnRoundStarted()
        {
            // Suppress the Ready button during training
            if (toggleReadyButtonEvent != null)
                toggleReadyButtonEvent.Raise(false);

            // Auto-click Ready after one frame so SetupNewTurn completes first.
            StartCoroutine(AutoClickReady());
        }

        IEnumerator AutoClickReady()
        {
            yield return null;
            if (gameController != null)
                gameController.OnReadyClicked();
            else
                Debug.LogError("[AITraining] MiniGameControllerBase not assigned! Cannot auto-start race.", this);
        }

        void OnTurnStarted()
        {
            _raceActive = true;
            _raceStartTime = Time.time;
            _nextShowOtherTime = Time.time + cameraShowOthersInterval;
            _currentShowOtherIndex = 0;

            // Read crystal target after one frame to ensure the turn monitor has run StartMonitor.
            // In all-AI mode, CrystalCollisionTurnMonitor.ownStats is null (no local player),
            // so GetRemainingCrystalsCountToCollect returns the total CrystalCollisions target.
            StartCoroutine(ReadCrystalTargetDeferred());

            // Point camera at the first AI vessel
            if (gameData.Players.Count > 0)
                SetupSpectatorCamera(0);
        }

        IEnumerator ReadCrystalTargetDeferred()
        {
            // Wait one frame so TurnMonitorController.StartMonitors() has run
            yield return null;

            if (turnMonitor != null)
            {
                if (int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int remaining) && remaining > 0)
                {
                    _crystalTarget = remaining;
                    Debug.Log($"[AITraining] Crystal target from monitor: {_crystalTarget}");
                    yield break;
                }
            }

            // Fallback: use inspector value
            if (fallbackCrystalTarget > 0)
            {
                _crystalTarget = fallbackCrystalTarget;
                Debug.Log($"[AITraining] Crystal target from fallback: {_crystalTarget}");
            }
            else
            {
                // Last resort default
                _crystalTarget = 39;
                Debug.LogWarning("[AITraining] Could not determine crystal target; defaulting to 39");
            }
        }

        void SetupSpectatorCamera(int playerIndex)
        {
            if (gameData.Players.Count <= playerIndex) return;

            var player = gameData.Players[playerIndex];
            var vessel = player.Vessel;
            if (vessel == null) return;

            var customizer = vessel.VesselStatus.VesselCameraCustomizer;
            if (customizer == null) return;

            customizer.Initialize(vessel);
        }

        void Update()
        {
            if (!_raceActive) return;

            // Switch camera to show other racers briefly
            if (Time.time > _nextShowOtherTime && gameData.Players.Count > 1)
            {
                _currentShowOtherIndex = (_currentShowOtherIndex + 1) % gameData.Players.Count;
                SetupSpectatorCamera(_currentShowOtherIndex);
                _nextShowOtherTime = Time.time + cameraShowOthersInterval;

                // Cancel any pending camera return so they don't stack
                if (_cameraReturnCoroutine != null)
                    StopCoroutine(_cameraReturnCoroutine);
                _cameraReturnCoroutine = StartCoroutine(ReturnToLeaderCamera());
            }

            // Check if any AI has collected enough crystals to finish the race.
            // The standard CrystalCollisionTurnMonitor only watches LocalPlayer,
            // which doesn't exist in all-AI mode, so we check all players here.
            if (_crystalTarget > 0 && CheckAnyPlayerFinished())
            {
                Debug.Log($"[AITraining] Race finished!");
                EndRace();
                return;
            }

            // Safety timeout
            if (Time.time - _raceStartTime > raceTimeoutSeconds)
            {
                Debug.LogWarning($"[AITraining] Race timed out after {raceTimeoutSeconds}s, forcing turn end");
                EndRace();
            }
        }

        void EndRace()
        {
            if (!_raceActive) return;
            _raceActive = false;

            // Cancel camera coroutines before signaling turn end
            if (_cameraReturnCoroutine != null)
            {
                StopCoroutine(_cameraReturnCoroutine);
                _cameraReturnCoroutine = null;
            }

            gameData.InvokeGameTurnConditionsMet();
        }

        IEnumerator ReturnToLeaderCamera()
        {
            yield return new WaitForSeconds(cameraShowOthersDuration);
            if (_raceActive && gameData.Players.Count > 0)
                SetupSpectatorCamera(0);
            _cameraReturnCoroutine = null;
        }

        bool CheckAnyPlayerFinished()
        {
            var stats = gameData.RoundStatsList;
            if (stats == null) return false;

            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] != null && stats[i].CrystalsCollected >= _crystalTarget)
                    return true;
            }
            return false;
        }

        void OnRaceEnd()
        {
            _raceActive = false;
            _racesCompleted++;
            LogProgress();

            if (_racesCompleted < maxRaces)
                StartCoroutine(RestartRace());
            else
            {
                Debug.Log($"[AITraining] === TRAINING COMPLETE === " +
                    $"{_racesCompleted} races over {evolution.Generation} generations");
                LogBestGenome();
            }
        }

        IEnumerator RestartRace()
        {
            yield return new WaitForSeconds(delayBetweenRaces);

            // Reset intensity in case anything changed it
            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = trainingIntensity;

            gameData.ResetForReplay();
        }

        void LogProgress()
        {
            if (evolution == null) return;

            var best = evolution.BestGenome;
            string bestFitness = best != null ? best.fitness.ToString("F1") : "N/A";

            if (_racesCompleted % 10 == 0 || _racesCompleted <= 5)
            {
                Debug.Log($"[AITraining] Race {_racesCompleted}/{maxRaces} | " +
                    $"Gen {evolution.Generation} | Pop {evolution.PopulationSize} | " +
                    $"Best fitness: {bestFitness}");
            }
        }

        void LogBestGenome()
        {
            if (evolution == null) return;

            var best = evolution.BestGenome;
            if (best == null) return;

            Debug.Log($"[AITraining] Best genome: " +
                $"throttle={best.throttleBase:F2} steering={best.steeringAggressiveness:F0} " +
                $"standoff={best.skimStandoffDistance:F1} nudge={best.maxNudgeStrength:F3} " +
                $"avoid={best.avoidanceWeight:F3} detection={best.prismDetectionRadius:F0} " +
                $"fitness={best.fitness:F1}");
        }
    }
}
