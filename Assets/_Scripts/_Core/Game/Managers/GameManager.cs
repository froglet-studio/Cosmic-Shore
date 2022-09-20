using UnityEngine;
using UnityEngine.SceneManagement;
using TailGlider.Utility.Singleton;
using UnityEngine.Advertisements;
using System.Collections;
using StarWriter.Core.Audio;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {
        public Player player;

        private GameSetting gameSettings;

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

        public bool PhoneFlipState { get; private set; }

        public ScreenOrientation currentOrientation;

        CameraManager cameraManager;

        AnalyticsManager analyticsManager;

        private readonly float phoneFlipThreshold = .3f;
        private int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        string mainMenuScene = "Menu_Main";
        string highScoreGameScene = "Game_HighScore";
        string tutorialGameScene = "Game_Tutorial";

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

            DataPersistenceManager.Instance.LoadGame();
        }
        void Update()
        {
            // We don't want the phone flip to flop like a fish out of water if the phone is mostly parallel to the ground
            if (Mathf.Abs(UnityEngine.Input.acceleration.y) < phoneFlipThreshold) return;

            if (UnityEngine.Input.acceleration.y < 0)
            {
                currentOrientation = ScreenOrientation.LandscapeLeft;
                PhoneFlipState = true;
                onPhoneFlip(PhoneFlipState);
            }
            else
            {
                currentOrientation = ScreenOrientation.LandscapeRight;
                PhoneFlipState = false;
                onPhoneFlip(PhoneFlipState);
            }
        }

        public void OnClickTutorialButton()
        {
            UnPauseGame();
            SceneManager.LoadScene(tutorialGameScene);
        }

        /// <summary>
        /// Toggles the Gyro On/Off
        /// </summary>
        public void OnClickGyroToggleButton()
        {
            // Set gameSettings Gyro status
            gameSettings.ChangeGyroEnabledStatus();
        }

        /// <summary>
        /// Starts Tutorial or Game based on hasSkippedTutorial status
        /// </summary>
        public void OnClickPlayButton()
        {
            deathCount = 0;
            analyticsManager.LogLevelStart();
            UnPauseGame();
            SceneManager.LoadScene(highScoreGameScene);
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

            // We disabled the player's colliders during the tail collision. let's turn them back on
            //StartCoroutine(ToggleCollisionCoroutine());

            // TODO: getting an error with the below line that timescale can only be set from the main thread,
            // but the code works... so...
            UnPauseGame();
        }

        //IEnumerator ToggleCollisionCoroutine()
        //{
        //    yield return new WaitForSeconds(.5f);
        //    player.ToggleCollision(true);
        //}

        public static void EndGame()
        {
            Debug.Log("GameManager.EndGame");
            onGameOver?.Invoke();
        }

        public void RestartGame()
        {
            Debug.Log("GameManager.RestartGame");
            deathCount = 0;

            SceneManager.LoadScene(highScoreGameScene);
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
            if (PauseSystem.GetIsPaused()) TogglePauseGame();
        }

        public static void PauseGame()
        {
            if (!PauseSystem.GetIsPaused()) TogglePauseGame();
        }

        public static void TogglePauseGame()
        {
            PauseSystem.TogglePauseGame();
        }

        public void WaitOnPlayerLoading()
        {
            onPlayGame?.Invoke();
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