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

        //public ScreenOrientation initialOrientation;
        //private bool initialOrientationSet = false;

        CameraManager cameraManager;

        private readonly float phoneFlipThreshold = .3f;
        private int deathCount = 0;
        public int DeathCount { get { return deathCount; } }

        private void OnEnable()
        {
            AdsManager.adShowComplete += OnAdShowComplete;
            AdvertisementMenu.onDeclineAd += EndGame;   // TODO: let's move this to live in AdsManager
            ShipExplosionHandler.onExplosionCompletion += OnExplosionCompletion;
        }

        private void OnDisable()
        {
            AdsManager.adShowComplete -= OnAdShowComplete;
            AdvertisementMenu.onDeclineAd -= EndGame;
            ShipExplosionHandler.onExplosionCompletion -= OnExplosionCompletion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void AutoRun()
        {
            Debug.Log("Running before splash screen?");
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
        

        void Start()
        {
            cameraManager = CameraManager.Instance;
            gameSettings = GameSetting.Instance;

            //StartCoroutine(InitializeScreenOrientationCoroutine());
        }
        private void Update()
        {
            if (Mathf.Abs(UnityEngine.Input.acceleration.y) < phoneFlipThreshold) return;

            if (UnityEngine.Input.acceleration.y < 0)
                PhoneFlipState = true;
            else
                PhoneFlipState = false;

            // We started with the phone upside down, so now down is up
            /*if (initialOrientation == ScreenOrientation.LandscapeRight)
            {
                Debug.Log("Flipping phone flip state");
                PhoneFlipState = !PhoneFlipState;
            }

            Debug.Log($"Phone Flip State: {PhoneFlipState}");
            Debug.Log($"Input.acceleration.y: {UnityEngine.Input.acceleration.y}");*/

            onPhoneFlip(PhoneFlipState);
        }

        /*
        IEnumerator InitializeScreenOrientationCoroutine()
        {
            yield return new WaitForSeconds(5f);
            
            initialOrientation = (Mathf.Abs(UnityEngine.Input.acceleration.y) >= phoneFlipThreshold && UnityEngine.Input.acceleration.y >= 0)
                        ? ScreenOrientation.LandscapeRight
                        : ScreenOrientation.LandscapeLeft;

            Debug.Log($"UnityEngine.Input.acceleration.y: {UnityEngine.Input.acceleration.y}");
            Debug.Log($"InitialOrientation: {initialOrientation}");
            Debug.Log($"ScreenOrientation == LandscapeRight: {initialOrientation == ScreenOrientation.LandscapeRight}");
            Debug.Log($"ScreenOrientation == LandscapeLeft: {initialOrientation == ScreenOrientation.LandscapeLeft}");

            Screen.orientation = initialOrientation;

            initialOrientationSet = true;
        }
        */

        public void OnClickTutorialButton()
        {
            UnPauseGame();
            SceneManager.LoadScene(1);
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
            UnPauseGame();
            SceneManager.LoadScene(2);
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

            StartCoroutine(ToggleCollisionCoroutine());

            // TODO: getting an error with the below line that timescale can only be set from the main thread
            UnPauseGame();

            // TODO: unpause game and make sure player is in safe area
            // TODO: Garrett game scene stuff
        }

        IEnumerator ToggleCollisionCoroutine()
        {
            yield return new WaitForSeconds(.5f);
            player.ToggleCollision(true);
        }

        public static void EndGame()
        {
            Debug.Log("GameManager.EndGame");
            onGameOver?.Invoke();
            //Instance.player.ToggleCollision(true);
        }

        public void RestartGame()
        {
            Debug.Log("GameManager.RestartGame");
            deathCount = 0;
            
            SceneManager.LoadScene(2);
            UnPauseGame();

            Jukebox.Instance.PlayNextSong();
        }

        public void ReturnToLobby()
        {
            SceneManager.LoadScene(0);
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

        /// <summary>
        /// Toggles the Pause System
        /// </summary>
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
            Debug.Log($"Before - Screen.orientation: {Screen.orientation}");

            Screen.orientation = ScreenOrientation.LandscapeLeft;

            Debug.Log($"After - Screen.orientation: {Screen.orientation}");

            if (showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
            {
                Debug.Log("Unity Ads Rewarded Ad Completed. Extending game.");

                ExtendGame();
            }
        }
    }
}