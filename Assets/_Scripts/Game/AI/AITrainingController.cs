using System.Collections;
using CosmicShore.Game.Cinematics;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.AI
{
    /// <summary>
    /// Fully automated spectator training mode.
    /// Runs AI-only races in a loop with no human input required.
    /// Bypasses the standard EndGame → cinematic flow entirely so races
    /// can loop indefinitely for overnight unattended training.
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
        [SerializeField] int maxRaces = 100000;
        [SerializeField] float delayBetweenRaces = 0.5f;
        [SerializeField] float raceTimeoutSeconds = 180f;
        [SerializeField, Range(1, 4)] int trainingIntensity = 4;
        [SerializeField] float cameraShowOthersInterval = 4f;
        [SerializeField] float cameraShowOthersDuration = 1.5f;

        [Header("Stability")]
        [Tooltip("Run GC.Collect every N races to prevent memory pressure during overnight runs.")]
        [SerializeField] int gcCollectInterval = 50;

        int _racesCompleted;
        float _raceStartTime;
        bool _raceActive;
        bool _cameraInitialized;
        int _crystalTarget;
        float _nextShowOtherTime;
        int _currentShowOtherIndex;
        float _sessionStartTime;

        void OnEnable()
        {
            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = trainingIntensity;

            gameData.OnMiniGameRoundStarted.OnRaised += OnRoundStarted;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;

            _sessionStartTime = Time.realtimeSinceStartup;
        }

        void OnDisable()
        {
            gameData.OnMiniGameRoundStarted.OnRaised -= OnRoundStarted;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
        }

        void Start()
        {
            // Disable any end-game cinematic controllers in the scene.
            // These block forever waiting for a Continue button press in AI-only mode.
            var cinematicControllers = FindObjectsByType<EndGameCinematicController>(FindObjectsSortMode.None);
            foreach (var controller in cinematicControllers)
            {
                controller.enabled = false;
                Debug.Log($"[AITraining] Disabled {controller.GetType().Name} to prevent end-game blocking");
            }
        }

        void OnRoundStarted()
        {
            if (toggleReadyButtonEvent != null)
                toggleReadyButtonEvent.Raise(false);

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
            _nextShowOtherTime = Time.time + cameraShowOthersInterval;
            _currentShowOtherIndex = 0;

            if (turnMonitor != null)
            {
                if (int.TryParse(turnMonitor.GetRemainingCrystalsCountToCollect(), out int remaining))
                    _crystalTarget = remaining;
            }

            if (!_cameraInitialized && gameData.Players.Count > 0)
            {
                SetupSpectatorCamera(0);
                _cameraInitialized = true;
            }
        }

        void SetupSpectatorCamera(int playerIndex)
        {
            if (gameData.Players.Count <= playerIndex) return;

            var player = gameData.Players[playerIndex];
            var vessel = player.Vessel;
            if (vessel == null) return;

            vessel.VesselStatus.VesselCameraCustomizer.Initialize(vessel);
        }

        void Update()
        {
            if (!_raceActive) return;

            // Rotate spectator camera between racers
            if (Time.time > _nextShowOtherTime && gameData.Players.Count > 1)
            {
                _currentShowOtherIndex = (_currentShowOtherIndex + 1) % gameData.Players.Count;
                SetupSpectatorCamera(_currentShowOtherIndex);
                _nextShowOtherTime = Time.time + cameraShowOthersInterval;
                StartCoroutine(ReturnToLeaderCamera());
            }

            // Check if any AI has collected enough crystals to finish.
            // We bypass the standard TurnMonitor → EndTurn → EndRound → EndGame chain
            // entirely, because EndGame triggers a cinematic that blocks forever
            // waiting for a Continue button press in AI-only mode.
            if (_crystalTarget > 0 && CheckAnyPlayerFinished())
            {
                _raceActive = false;
                HandleRaceComplete(timedOut: false);
                return;
            }

            if (Time.time - _raceStartTime > raceTimeoutSeconds)
            {
                _raceActive = false;
                HandleRaceComplete(timedOut: true);
            }
        }

        void HandleRaceComplete(bool timedOut)
        {
            float elapsed = Time.time - _raceStartTime;
            _racesCompleted++;

            if (timedOut)
                Debug.LogWarning($"[AITraining] Race {_racesCompleted} timed out after {elapsed:F1}s");

            LogProgress();

            // Periodic GC to prevent memory pressure during overnight runs
            if (_racesCompleted % gcCollectInterval == 0)
            {
                System.GC.Collect();
                Debug.Log($"[AITraining] GC.Collect after {_racesCompleted} races");
            }

            if (_racesCompleted < maxRaces)
            {
                StartCoroutine(RestartRace());
            }
            else
            {
                float sessionHours = (Time.realtimeSinceStartup - _sessionStartTime) / 3600f;
                Debug.Log($"[AITraining] === TRAINING COMPLETE === " +
                    $"{_racesCompleted} races | {evolution.Generation} generations | " +
                    $"{sessionHours:F1} hours");
                LogBestGenome();
            }
        }

        IEnumerator ReturnToLeaderCamera()
        {
            yield return new WaitForSeconds(cameraShowOthersDuration);
            if (_raceActive && gameData.Players.Count > 0)
                SetupSpectatorCamera(0);
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

        IEnumerator RestartRace()
        {
            yield return new WaitForSeconds(delayBetweenRaces);

            // ResetForReplay handles everything:
            // 1. ResetStatsDataForReplay — cleans up round stats
            // 2. ResetPlayers — calls player.ResetForPlay() which stops AI pilots
            //    (triggering PilotFitnessTracker.StopTracking → ReportFitness)
            // 3. ResetRuntimeDataForReplay — resets RoundsPlayed, TurnsTakenThisRound
            // 4. Raises OnResetForReplay → SetupNewRound → SetupNewTurn
            //    which rebuilds the track and queues the next race start
            gameData.ResetForReplay();
        }

        void LogProgress()
        {
            var best = evolution.BestGenome;
            string bestFitness = best != null ? best.fitness.ToString("F1") : "N/A";
            float sessionMinutes = (Time.realtimeSinceStartup - _sessionStartTime) / 60f;

            if (_racesCompleted % 10 == 0 || _racesCompleted <= 5)
            {
                Debug.Log($"[AITraining] Race {_racesCompleted}/{maxRaces} | " +
                    $"Gen {evolution.Generation} | Pop {evolution.PopulationSize} | " +
                    $"Best fitness: {bestFitness} | Session: {sessionMinutes:F0}min");
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
