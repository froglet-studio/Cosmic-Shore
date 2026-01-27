using System.Collections;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Game.Cinematics
{
    public sealed class EndGameCinematicController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;

        [Header("Cinematics")]
        [SerializeField] SceneCinematicLibrarySO sceneCinematicLibrary;

        bool isRunning;
        Coroutine runningRoutine;

        void OnEnable()
        {
            if (gameData == null) return;

            gameData.OnWinnerCalculated += OnWinnerCalculated;
        }

        void OnDisable()
        {
            if (gameData == null) return;

            gameData.OnWinnerCalculated -= OnWinnerCalculated;

            if (runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                runningRoutine = null;
            }

            isRunning = false;
        }

        void OnWinnerCalculated()
        {
            if (isRunning) return;
            isRunning = true;

            var cinematic = ResolveCinematicForThisScene();
            runningRoutine = StartCoroutine(RunEndGameCinematic(cinematic));
        }

        IEnumerator RunEndGameCinematic(CinematicDefinitionSO cinematic)
        {
            // 1) Take control away (AI drives)
            if (cinematic && cinematic.setLocalVesselToAI)
                SetLocalVesselAI(true);

            // 2) Wait
            float delay = cinematic ? Mathf.Max(0f, cinematic.delayBeforeEndScreen) : 0f;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // 3) Show end screen (Scoreboard listens to this)
            gameData.InvokeShowGameEndScreen();

            runningRoutine = null;
        }
        

        CinematicDefinitionSO ResolveCinematicForThisScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            if (sceneCinematicLibrary &&
                sceneCinematicLibrary.TryGet(sceneName, out var fromLibrary))
                return fromLibrary;
            return null;
        }

        void SetLocalVesselAI(bool isAI)
        {
            // GameDataSO already tracks the local player (set in AddPlayer)
            var player = gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            player.Vessel.ToggleAIPilot(isAI);
        }
    }
}