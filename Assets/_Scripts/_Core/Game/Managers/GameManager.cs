using UnityEngine;
using UnityEngine.SceneManagement;
using TailGlider.Utility.Singleton;
using StarWriter.Core.Audio;
using System;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {          
        [SerializeField]
        private bool hasSkippedTutorial = false;    // TODO: why is this a serialize field? Also, it never sets the playerpref, just reads from it. Also, it never uses the value.

        private GameSetting gameSettings;

        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        public delegate void OnDeathEvent();
        public static event OnDeathEvent onDeath;

        public delegate void OnExtendGameEvent();
        public static event OnExtendGameEvent onExtendGamePlay;

        private readonly float phoneFlipThreshold = .3f;

        public delegate void OnPhoneFlipEvent(bool state);
        public static event OnPhoneFlipEvent onPhoneFlip;

        CameraManager cameraManager;

        public bool isExtendedLife;

        private void OnEnable()
        {
            FuelSystem.zeroFuel += Death;
        }

        private void OnDisable()
        {
            FuelSystem.zeroFuel -= Death;
        }

        void Start()
        {
            cameraManager = CameraManager.Instance;
            gameSettings = GameSetting.Instance;

            isExtendedLife = false;

            if (PlayerPrefs.GetInt("Skip Tutorial") == 1) // 0 false and 1 true
            {
                hasSkippedTutorial = true;
            }
            else { hasSkippedTutorial = false; }
        }

        /// <summary>
        /// Toggles the Tutorial On/Off
        /// </summary>
        /// 

        private void Update()
        {
            if (Mathf.Abs(UnityEngine.Input.acceleration.y) < phoneFlipThreshold) return;
            if (UnityEngine.Input.acceleration.y < 0)
            {
                onPhoneFlip(true);
            }
            else
            {
                onPhoneFlip(false);
            }
        }
       


        public void OnClickTutorialToggleButton()
        {
            // Set gameSettings Tutorial status
            gameSettings.IsTutorialEnabled = !gameSettings.IsTutorialEnabled;
            //Set PlayerPrefs Tutorial status
            if (gameSettings.IsTutorialEnabled == true)
            {
                PlayerPrefs.SetInt("isTutorialEnabled", 1);  //tutorial enabled
            }
            else
            {
                PlayerPrefs.SetInt("isTutorialEnabled", 0);  //tutorial disabled
            }
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
            UnPauseGame();
            SceneManager.LoadScene(2);
        }

        private void Death()
        {
            PauseGame();
            onDeath?.Invoke();
        }

        public void ExtendGame()
        {
            UnPauseGame();
            onExtendGamePlay?.Invoke();
            //TODO unpause game and make sure player is in safe area
            //TODO  Garrett game scene stuff
        }

        public void RestartGame()
        {
            UnPauseGame();
            //audioManager.PlayMusicClip(audioManager.ToggleMusicPlaylist());
            SceneManager.LoadScene(2);
        }


        public void ReturnToLobby()
        {
            UnPauseGame();
            SceneManager.LoadScene(0);
            cameraManager.OnMainMenu();
        }

        public void UnPauseGame()
        {
            if (PauseSystem.GetIsPaused()) { TogglePauseGame(); }
        }

        public void PauseGame()
        {
            if (!PauseSystem.GetIsPaused()) { TogglePauseGame(); }
        }

        /// <summary>
        /// Toggles the Pause System
        /// </summary>
        public void TogglePauseGame()
        {
            PauseSystem.TogglePauseGame();
        }

        public void WaitOnPlayerLoading()
        {
            onPlayGame?.Invoke();
        }
    }
}