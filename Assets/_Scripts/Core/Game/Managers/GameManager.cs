using UnityEngine;
using UnityEngine.SceneManagement;
using Amoebius.Utility.Singleton;
using StarWriter.Core.Audio;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {          
        [SerializeField]
        private bool hasSkippedTutorial = false;

        private AudioManager audioManager;
        private GameSetting gameSettings;

        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        void Start()
        {
            
            gameSettings = GameSetting.Instance;
            audioManager = AudioManager.Instance;

            if (PlayerPrefs.GetInt("Skip Tutorial") == 1) // 0 false and 1 true
            {
                hasSkippedTutorial = true;
            }
            else { hasSkippedTutorial = false; }
        }

        /// <summary>
        /// Toggles the Tutorial On/Off
        /// </summary>
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
        /// Starts Tutorial or Game bases on hasSkippedTutorial status
        /// </summary>
        public void OnClickPlayButton()
        {
            UnPauseGame();
            SceneManager.LoadScene(2);
            //if (hasSkippedTutorial)
            //{
            //    SceneManager.LoadScene(2);
            //}
            //else
            //{
            //    SceneManager.LoadScene(1);
            //}
        }
        public void RestartGame()
        {
            UnPauseGame();
            //audioManager.PlayMusicClip(audioManager.ToggleMusicPlaylist());
            SceneManager.LoadScene(2);
        }
        public void ReturnToLobby()
        {
            SceneManager.LoadScene(0);
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
        /// UnPauses game play
        /// </summary>
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

