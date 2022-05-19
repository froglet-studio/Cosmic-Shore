using UnityEngine;
using UnityEngine.SceneManagement;
using Amoebius.Utility.Singleton;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {          
        [SerializeField]
        private bool skipTutorial = false;

        [SerializeField]
        private bool isGyroEnabled = true;

        private GameSetting gameSettings;

        public delegate void OnPlayGameEvent();
        public static event OnPlayGameEvent onPlayGame;

        public delegate void OnToggleGyroEvent(bool status);
        public static event OnToggleGyroEvent onToggleGyro;

        void Start()
        {
            //PlayerPrefs.SetInt("Skip Tutorial", 1);
            gameSettings = GameSetting.Instance;

            if (PlayerPrefs.GetInt("Skip Tutorial") == 1) // 0 false and 1 true
            {
                skipTutorial = true;
            }
            else { skipTutorial = false; }
        }

        public void OnClickTutorialToggleButton()
        {
            //SceneManager.LoadScene(1);  can we just load the scene and keep it simple?
            // Set PlayerPrefs Tutorial status
            if (gameSettings.TutorialEnabled == true)
            {
                PlayerPrefs.SetInt("tutorialEnabled", 1);  //tutorial enabled
            }
            else
            {
                PlayerPrefs.SetInt("tutorialEnabled", 0);  //tutorial disabled
            }
            // Set gameSettings Tutorial status
            gameSettings.TutorialEnabled = !gameSettings.TutorialEnabled;

        }


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

        public void OnClickPlayButton()
        {
            if (skipTutorial)
            {
                SceneManager.LoadScene(2);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
        }

        public void OnClickResumeButton()
        {
            TogglePauseGame();
        }

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

