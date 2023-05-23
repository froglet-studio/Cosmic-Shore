using UnityEngine;
using UnityEngine.SceneManagement;
using TailGlider.Utility.Singleton;
using UnityEngine.Advertisements;
using StarWriter.Core.IO;

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

        /* Singleton References */
        CameraManager cameraManager;
        AnalyticsManager analyticsManager;

        int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        [Header("Scene Names")]
        [SerializeField] string mainMenuScene = "Menu_Main";
        [SerializeField] string gameTestModeZeroGameScene = "Game_HighScore";
        [SerializeField] string gameTestModeOneGameScene = "Game_TestModeOne";
        [SerializeField] string gameTestModeTwoGameScene = "Game_TestNodeInterior";
        [SerializeField] string gameTestModeThreeGameScene = "Game_TestModeThree";
        [SerializeField] string gameTestModeFourGameScene = "Game_TestModeFour";
        [SerializeField] string gameTestDesign = "Game_TestDesign";
        [SerializeField] string hangarScene = "Hangar";
        [SerializeField] string tutorialGameScene = "Game_Tutorial";

        void OnEnable()
        {
            AdsManager.AdShowComplete += OnAdShowComplete;
            AdsManager.AdShowFailure += OnAdShowFailure;
            AdvertisementMenu.onDeclineAd += EndGame;
            ShipExplosionHandler.onShipExplosionAnimationCompletion += OnExplosionCompletion;
        }

        void OnDisable()
        {
            AdsManager.AdShowComplete -= OnAdShowComplete;
            AdsManager.AdShowFailure -= OnAdShowFailure;
            AdvertisementMenu.onDeclineAd -= EndGame;
            ShipExplosionHandler.onShipExplosionAnimationCompletion -= OnExplosionCompletion;
        }

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
            analyticsManager = AnalyticsManager.Instance;

            DataPersistenceManager.Instance.LoadGameData();
        }

        public void OnClickTutorialButton()
        {
            UnPauseGame();
            SceneManager.LoadScene(tutorialGameScene);
        }

        public void OnClickPlayButton() //TODO make this general so you pass in the load scene
        {
            EnterGame(gameTestModeZeroGameScene);
        }

        public void OnClickTestGameModeOne()
        {
            EnterGame(gameTestModeOneGameScene);
        }

        public void OnClickTestGameModeTwo()
        {
            EnterGame(gameTestModeTwoGameScene);
        }

        public void OnClickTestGameModeThree()
        {
            EnterGame(gameTestModeThreeGameScene);
        }

        public void OnClickTestGameModeFour()
        {
            EnterGame(gameTestModeFourGameScene);
        }

        public void OnClickGameTestDesign()
        {
            EnterGame(gameTestDesign);
        }

        public void OnClickHangar()
        {
            SceneManager.LoadScene(hangarScene);
        }

        void EnterGame(string scenename)
        {
            deathCount = 0;
            analyticsManager.LogLevelStart();
            UnPauseGame();
            SceneManager.LoadScene(scenename);
        }

        void OnExplosionCompletion()
        {
            Debug.Log("GameManager.Death");

            PauseGame();

            deathCount++;
            onDeath?.Invoke();

            if (deathCount >= 2)
                EndGame();
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
        public void ReturnToLobby()
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
            aiPilot.CrystalTransform = FindObjectOfType<Crystal>().transform;
            aiPilot.flowFieldData = FindObjectOfType<FlowFieldData>();
        }

        public void OnAdShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
            Debug.Log("GameManager.OnAdShowComplete");
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            if (showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
            {
                Debug.Log("Unity Ads Rewarded Ad Completed. Extending game.");

            }
            if (showCompletionState.Equals(UnityAdsShowCompletionState.SKIPPED))
            {
                Debug.Log("Unity Ads Rewarded Ad SKIPPED due to ad failure. Extending game.");

            }
        }

        public void OnAdShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
            // Give them the benefit of the doubt and just pass through to the ad completion logic
            OnAdShowComplete(adUnitId, UnityAdsShowCompletionState.SKIPPED);
        }
    }
}