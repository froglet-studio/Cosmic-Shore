using CosmicShore.Environment.FlowField;
using CosmicShore.Game.AI; // TODO: code smell that this namespace needs to be included here
using CosmicShore.Utility.Singleton;
using UnityEngine;
using UnityEngine.SceneManagement;

// TODO: P1 - some work needs to be done to unify this with the MiniGame engine managers
namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {
        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        public delegate void OnGameOverEvent();
        public static event OnGameOverEvent onGameOver;

        [SerializeField] public SO_GameList AllGames;
        [SerializeField] public SO_ShipList AllShips;

        int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        [Header("Scene Names")]
        static string mainMenuScene = "Menu_Main";
        

        public static void EndGame()
        {
            Debug.Log("GameManager.EndGame");
            onGameOver?.Invoke();
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
            SceneManager.LoadScene(mainMenuScene);
            UnPauseGame();
            CameraManager.Instance.OnMainMenu();
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
            onPlayGame?.Invoke();
        }

        public void WaitOnAILoading(AIPilot aiPilot)
        {
            // TODO: P1 elemental crystals, FindObjectOfType may no work anymore for this
            aiPilot.CrystalTransform = FindObjectOfType<Crystal>().transform;
            aiPilot.flowFieldData = FindObjectOfType<FlowFieldData>();
        }
    }
}