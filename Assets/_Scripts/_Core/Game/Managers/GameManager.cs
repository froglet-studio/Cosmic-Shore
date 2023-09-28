using StarWriter.Core.IO;
using StarWriter.Utility.Singleton;
using UnityEngine;
using UnityEngine.SceneManagement;

// TODO: P1 - some work needs to be done to unify this with the MiniGame engine managers
namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {
        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        public delegate void OnDeathEvent();
        public static event OnDeathEvent onDeath;

        public delegate void OnGameOverEvent();
        public static event OnGameOverEvent onGameOver;

        [SerializeField] public SO_GameList AllGames;
        [SerializeField] public SO_ShipList AllShips;

        /* Singleton References */
        static CameraManager cameraManager;

        int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        [Header("Scene Names")]
        static string mainMenuScene = "Menu_Main";

        // TODO: should this live somewhere else?
        // In order to support the splash screen always showing in the correct orientation, we use this method as a work around.
        // In the build settings, we set orientation to AutoRotate, then lock to LandscapeLeft as the app is launching here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void AutoRun()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        void Start()
        {
            cameraManager = CameraManager.Instance;
        }

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
            cameraManager.OnMainMenu();
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