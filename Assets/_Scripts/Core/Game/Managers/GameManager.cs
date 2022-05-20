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

        [SerializeField]
        private bool isGyroEnabled = true;

        private AudioManager audioManager;
        private GameSetting gameSettings;

        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        public delegate void OnToggleGyroEvent(bool status);
        public static event OnToggleGyroEvent onToggleGyro;

        void Start()
        {
            PlayerPrefs.SetInt("Skip Tutorial", 0);
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
            gameSettings.TutorialEnabled = !gameSettings.TutorialEnabled;
            //Set PlayerPrefs Tutorial status
            if (gameSettings.TutorialEnabled == true)
            {
                PlayerPrefs.SetInt("tutorialEnabled", 1);  //tutorial enabled
            }
            else
            {
                PlayerPrefs.SetInt("tutorialEnabled", 0);  //tutorial disabled
            }
            UnPauseGame();
            SceneManager.LoadScene(1);
        }
        public void TurnGyroON()
        {
            onToggleGyro(true);
        }

        public void TurnGyroOFF()
        {
            onToggleGyro(false);
        }

        /// <summary>
        /// Toggles the Gyro On/Off
        /// </summary>
        public void OnClickGyroToggleButton()
        {
            // Set gameSettings Gyro status
            gameSettings.GyroEnabled = isGyroEnabled = !isGyroEnabled;
            onToggleGyro(isGyroEnabled);

            // Set PlayerPrefs Gyro status
            if (isGyroEnabled == true)
            {
                PlayerPrefs.SetInt("gyroEnabled", 1); //gyro enabled

            }
            else
            {
                PlayerPrefs.SetInt("gyroEnabled", 0);  //gyro disabled
            }
        }
        /// <summary>
        /// Starts Tutorial or Game bases on hasSkippedTutorial status
        /// </summary>
        public void OnClickPlayButton()
        {
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
            SceneManager.LoadScene(2);
            //audioManager.
        }
        /// <summary>
        /// UnPauses game play
        /// </summary>
        public void UnPauseGame()
        {
            if (PauseSystem.GetIsPaused()) { TogglePauseGame(); }
        }
        /// <summary>
        /// Pauses game play
        /// </summary>
        public void TogglePauseGame()
        {
            PauseSystem.TogglePauseGame();
        }

        public void WaitOnPlayerLoading()
        {
            Debug.Log("WaitOnPlayerLoading");
            onPlayGame?.Invoke();
        }
    }
}

