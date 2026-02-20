using System.Collections;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.AI
{
    /// <summary>
    /// Automates AI training races for overnight evolutionary learning.
    /// Drop this into a HexRace scene alongside the existing game controller.
    ///
    /// Setup:
    /// 1. Replace MiniGamePlayerSpawnerAdapter with TrainingPlayerSpawnerAdapter
    ///    and configure 3 AI entries in _initializeDatas (IsAI=true, AllowSpawning=true).
    /// 2. On the game controller, set numberOfRounds = 1, numberOfTurnsPerRound = 1.
    /// 3. Optionally reduce CountdownTimer.countdownDuration for faster cycles.
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

        [Header("Training Config")]
        [SerializeField] int maxRaces = 10000;
        [SerializeField] float delayBetweenRaces = 1f;
        [SerializeField] float raceTimeoutSeconds = 300f;

        int _racesCompleted;
        float _raceStartTime;
        bool _raceActive;

        void OnEnable()
        {
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
            // Ready button is about to be shown by SetupNewTurn().
            // Auto-click after one frame so the turn setup completes first.
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
        }

        void OnRaceEnd()
        {
            _raceActive = false;
            _racesCompleted++;
            LogProgress();

            if (_racesCompleted < maxRaces)
            {
                StartCoroutine(RestartRace());
            }
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

        void Update()
        {
            if (_raceActive && Time.time - _raceStartTime > raceTimeoutSeconds)
            {
                Debug.LogWarning($"[AITraining] Race timed out after {raceTimeoutSeconds}s, forcing turn end");
                _raceActive = false;
                gameData.InvokeGameTurnConditionsMet();
            }
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
                $"throttle={best.throttleBase:F2} standoff={best.skimStandoffDistance:F1} " +
                $"nudge={best.maxNudgeStrength:F3} avoid={best.avoidanceWeight:F3} " +
                $"detection={best.prismDetectionRadius:F0} fitness={best.fitness:F1}");
        }
    }
}
