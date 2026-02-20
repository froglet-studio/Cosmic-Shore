using System.Collections;
using CosmicShore.Soap;
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

        [Header("Training Config")]
        [SerializeField] int maxRaces = 10000;
        [SerializeField] float delayBetweenRaces = 1f;
        [SerializeField] float raceTimeoutSeconds = 120f;
        [SerializeField, Range(1, 4)] int trainingIntensity = 4;

        int _racesCompleted;
        float _raceStartTime;
        bool _raceActive;
        bool _cameraInitialized;
        int _crystalTarget;

        void OnEnable()
        {
            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = trainingIntensity;

            gameData.OnMiniGameEnd += OnRaceEnd;
            gameData.OnMiniGameRoundStarted.OnRaised += OnRoundStarted;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
        }

        void OnDisable()
        {
            gameData.OnMiniGameEnd -= OnRaceEnd;
            gameData.OnMiniGameRoundStarted.OnRaised -= OnRoundStarted;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
        }

        void OnRoundStarted()
        {
            // Auto-click Ready after one frame so SetupNewTurn completes first.
            StartCoroutine(AutoClickReady());
        }

        IEnumerator AutoClickReady()
        {
            yield return null;
            gameController.OnReadyClicked();
        }

        void OnTurnStarted()
        {
            _raceActive = true;
            _raceStartTime = Time.time;

            // Cache the crystal target from the turn monitor (set during StartMonitor)
            if (turnMonitor != null)
            {
                if (int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int remaining))
                    _crystalTarget = remaining;
            }

            // Point camera at the first AI vessel on the first turn
            if (!_cameraInitialized && gameData.Players.Count > 0)
            {
                SetupSpectatorCamera();
                _cameraInitialized = true;
            }
        }

        void SetupSpectatorCamera()
        {
            var player = gameData.Players[0];
            var vessel = player.Vessel;
            if (vessel == null) return;

            vessel.VesselStatus.VesselCameraCustomizer.Initialize(vessel);
            Debug.Log($"[AITraining] Spectator camera following {vessel.VesselStatus.PlayerName}");
        }

        void Update()
        {
            if (!_raceActive) return;

            // Check if any AI has collected enough crystals to finish the race.
            // The standard CrystalCollisionTurnMonitor only watches LocalPlayer,
            // which doesn't exist in all-AI mode, so we check all players here.
            if (_crystalTarget > 0 && CheckAnyPlayerFinished())
            {
                Debug.Log($"[AITraining] An AI finished the race!");
                _raceActive = false;
                gameData.InvokeGameTurnConditionsMet();
                return;
            }

            // Safety timeout
            if (Time.time - _raceStartTime > raceTimeoutSeconds)
            {
                Debug.LogWarning($"[AITraining] Race timed out after {raceTimeoutSeconds}s, forcing turn end");
                _raceActive = false;
                gameData.InvokeGameTurnConditionsMet();
            }
        }

        bool CheckAnyPlayerFinished()
        {
            var stats = gameData.RoundStatsList;
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i].CrystalsCollected >= _crystalTarget)
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
            gameData.ResetForReplay();
        }

        void LogProgress()
        {
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
