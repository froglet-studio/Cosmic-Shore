using UnityEngine;
using UnityEngine.SceneManagement;
using TailGlider.Utility.Singleton;
using UnityEngine.Advertisements;
using StarWriter.Core.Audio;
using UnityEngine.InputSystem;
using StarWriter.Core.Input;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {
        public Player player;

        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        public delegate void OnDeathEvent();
        public static event OnDeathEvent onDeath;

        public delegate void OnExtendGameEvent();
        public static event OnExtendGameEvent onExtendGamePlay;

        public delegate void OnGameOverEvent();
        public static event OnGameOverEvent onGameOver;

        public delegate void OnPhoneFlipEvent(bool state);
        public static event OnPhoneFlipEvent onPhoneFlip;

        public bool PhoneFlipState;

        public ScreenOrientation currentOrientation;

        /* Singleton References */
        GameSetting gameSettings;
        CameraManager cameraManager;
        AnalyticsManager analyticsManager;

        private int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        string mainMenuScene = "Menu_Main";
        [SerializeField] string gameTestModeZeroGameScene = "Game_HighScore";
        [SerializeField] string gameTestModeOneGameScene = "Game_TestModeOne";
        [SerializeField] string gameTestModeTwoGameScene = "Game_TestModeTwo";
        [SerializeField] string gameTestModeThreeGameScene = "Game_TestModeThree";
        [SerializeField] string gameTestModeFourGameScene = "Game_TestModeFour";
        string hangarScene = "Hangar";
        string tutorialGameScene = "Game_Tutorial";
        string ActiveGameScene = "";

        private void OnEnable()
        {
            AdsManager.adShowComplete += OnAdShowComplete;
            AdsManager.adShowFailure += OnAdShowFailure;
            AdvertisementMenu.onDeclineAd += EndGame;
            ShipExplosionHandler.onShipExplosionAnimationCompletion += OnExplosionCompletion;
        }

        private void OnDisable()
        {
            AdsManager.adShowComplete -= OnAdShowComplete;
            AdsManager.adShowFailure -= OnAdShowFailure;
            AdvertisementMenu.onDeclineAd -= EndGame;
            ShipExplosionHandler.onShipExplosionAnimationCompletion -= OnExplosionCompletion;
        }

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
            gameSettings = GameSetting.Instance;

            DataPersistenceManager.Instance.LoadGameData();
        }

        void Update()
        {
            if (Gamepad.current != null)
            {
                //if (UnityEngine.InputSystem.Gamepad.current.rightShoulder.wasPressedThisFrame)
                //{
                //    GamepadCameraFlip = !GamepadCameraFlip;
                //    Debug.Log($"Gamepad Camera Flip {GamepadCameraFlip}");
                //}
            }

            //// We don't want the phone flip to flop like a fish out of water if the phone is mostly parallel to the ground
            //if (Mathf.Abs(UnityEngine.Input.acceleration.y) < phoneFlipThreshold) return;

            if (PhoneFlipState)
            {
                currentOrientation = ScreenOrientation.LandscapeLeft;
                onPhoneFlip(PhoneFlipState);
            }
            else
            {
                currentOrientation = ScreenOrientation.LandscapeRight;
                onPhoneFlip(PhoneFlipState);
            }
        }
        /// <summary>
        /// Toggles the Gyro On/Off
        /// </summary>
        public void OnClickGyroToggleButton()
        {
            gameSettings.ChangeGyroEnabledStatus();
        }

        public void OnClickTutorialButton()
        {
            UnPauseGame();
            ActiveGameScene = tutorialGameScene;
            SceneManager.LoadScene(tutorialGameScene);
        }

        private void EnterGame(string scenename)
        {
            deathCount = 0;
            analyticsManager.LogLevelStart();
            UnPauseGame();
            ActiveGameScene = gameTestModeZeroGameScene;
            SceneManager.LoadScene(scenename);
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

        public void OnClickHangar()
        {
            SceneManager.LoadScene(hangarScene);
        }

        private void OnExplosionCompletion()
        {
            Debug.Log("GameManager.Death");

            PauseGame();

            deathCount++;
            onDeath?.Invoke();

            if (deathCount >= 2)
                EndGame();
        }

        public void ExtendGame()
        {
            Debug.Log("GameManager.ExtendGame");
            onExtendGamePlay?.Invoke();

            // TODO: getting an error with the below line that timescale can only be set from the main thread, but the code works... so...
            UnPauseGame();
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

            SceneManager.LoadScene(ActiveGameScene);
            UnPauseGame();

            Jukebox.Instance.PlayNextSong();
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

        public void WaitOnAILoading(AIPilot pilot)
        {
            // TODO: we should rename CrystalTransform to 'CrystalTransform'
            pilot.CrystalTransform = FindObjectOfType<Crystal>().transform;
            pilot.flowFieldData = FindObjectOfType<FlowFieldData>();
        }

        public void OnAdShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
            Debug.Log("GameManager.OnAdShowComplete");
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            if (showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
            {
                Debug.Log("Unity Ads Rewarded Ad Completed. Extending game.");

                ExtendGame();
            }
        }

        public void OnAdShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
            // Just pass through to the ad completion logic
            // TODO: We may want to use UnityAdsShowCompletionState.SKIPPED (which is correct) and do a different behavior here
            OnAdShowComplete(adUnitId, UnityAdsShowCompletionState.COMPLETED);
        }

        private void OnApplicationQuit()
        {
            DataPersistenceManager.Instance.SaveGame();
        }
    }
}