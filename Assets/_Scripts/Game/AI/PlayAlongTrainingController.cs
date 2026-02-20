using System.Collections;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.AI
{
    /// <summary>
    /// Play-along training mode: you race alongside AI pilots that are evolving.
    /// The standard CrystalCollisionTurnMonitor handles end-of-race detection
    /// (it watches your crystal count). This controller just auto-restarts
    /// between races so you can keep playing without navigating menus.
    ///
    /// Setup:
    /// 1. Use the normal MiniGamePlayerSpawnerAdapter with your human player
    ///    plus AI entries (IsAI=true).
    /// 2. Set numberOfRounds = 1, numberOfTurnsPerRound = 1 on the game controller.
    /// 3. Assign the same PilotEvolution SO to each AI vessel's AIPilot + PilotFitnessTracker.
    /// 4. Press Play and race. The game restarts automatically after each race.
    /// </summary>
    public class PlayAlongTrainingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] PilotEvolution evolution;

        [Header("Config")]
        [SerializeField] float delayBetweenRaces = 3f;
        [SerializeField, Range(1, 4)] int trainingIntensity = 4;

        int _racesCompleted;

        void OnEnable()
        {
            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = trainingIntensity;

            gameData.OnMiniGameEnd += OnRaceEnd;
        }

        void OnDisable()
        {
            gameData.OnMiniGameEnd -= OnRaceEnd;
        }

        void OnRaceEnd()
        {
            _racesCompleted++;

            if (evolution != null)
            {
                var best = evolution.BestGenome;
                string bestFitness = best != null ? best.fitness.ToString("F1") : "N/A";
                Debug.Log($"[PlayAlongTraining] Race {_racesCompleted} complete | " +
                    $"Gen {evolution.Generation} | Best fitness: {bestFitness}");
            }

            StartCoroutine(RestartRace());
        }

        IEnumerator RestartRace()
        {
            yield return new WaitForSeconds(delayBetweenRaces);
            gameData.ResetForReplay();
        }
    }
}
