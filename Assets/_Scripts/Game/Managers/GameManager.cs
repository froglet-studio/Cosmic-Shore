using CosmicShore.App.Systems;
using CosmicShore.Environment.FlowField;
using CosmicShore.Game.AI; // TODO: code smell that this namespace needs to be included here
using CosmicShore.Utilities;
using CosmicShore.Core;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// TODO: P1 - some work needs to be done to unify this with the MiniGame engine managers
namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {
        public static Action OnPlayGame;
        public static Action OnGameOver;

        [SerializeField] public SO_GameList AllGames;
        [SerializeField] public SO_ShipList AllShips;

        Animator SceneTransitionAnimator;
        int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        [Header("Scene Names")]
        static string mainMenuScene = "Menu_Main";
        

        public static void EndGame()
        {
            Debug.Log("GameManager.EndGame");
            OnGameOver?.Invoke();
        }

        public void RestartGame()
        {
            Debug.Log("GameManager.RestartGame");
            deathCount = 0;

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            UnPauseGame();
        }

        public static void ReturnToLobby()
        {
            Instance.StartCoroutine(ReturnToLobbyCoroutine());
        }

        public static void UnPauseGame()
        {
            if (PauseSystem.Paused) TogglePauseGame();
        }

        public static void PauseGame()
        {
            if (!PauseSystem.Paused) TogglePauseGame();
        }

        public static void TogglePauseGame()
        {
            PauseSystem.TogglePauseGame();
        }

        public void WaitOnPlayerLoading()
        {
            OnPlayGame?.Invoke();
        }

        public void WaitOnAILoading(AIPilot aiPilot)
        {
            aiPilot.TargetPosition = FindAnyObjectByType<Crystal>().transform.position;
            aiPilot.flowFieldData = FindAnyObjectByType<FlowFieldData>();
        }

        public void RegisterSceneTransitionAnimator(Animator animator)
        {
            SceneTransitionAnimator = animator;
        }

        static IEnumerator ReturnToLobbyCoroutine()
        {
            Instance.SceneTransitionAnimator?.SetTrigger("Start");

            yield return new WaitForSecondsRealtime(.5f);

            SceneManager.LoadScene(mainMenuScene);
            UnPauseGame();
            CustomCameraController.Instance.OnMainMenu();
        }
    }
}