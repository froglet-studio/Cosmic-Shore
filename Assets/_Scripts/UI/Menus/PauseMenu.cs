using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.Audio;
using UnityEngine.UI;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

namespace StarWriter.UI
{
    public class PauseMenu : MonoBehaviour
    {
        GameManager gameManager;
        GameSetting gameSetting;

        [SerializeField] GameMenu gameMenu;

        public Button pauseButton;

        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;
            gameSetting = GameSetting.Instance;

        }
        /// <summary>
        /// Toggles the Master Volume On/Off
        /// </summary>
        public void OnToggleMusic()
        {
            gameSetting.ChangeAudioEnabledStatus();
        }

        /// <summary>
        /// Calls the Tutorial Scene to be loaded
        /// </summary>
        public void OnClickTutorialButton()
        {
            gameManager.OnClickTutorialButton();
        }
        /// <summary>
        /// Restarts the Game Scene
        /// </summary>
        public void OnClickRestartButton()
        {
            gameManager.OnClickPlayButton();
        }
        /// <summary>
        /// UnPauses the game 
        /// </summary>
        public void OnClickResumeGameButton()
        {
            GameManager.UnPauseGame();
            gameMenu.OnClickUnpauseGame();
        }
        public void OnClickResumeTutorialButton()
        {
            GameManager.UnPauseGame();
            pauseButton.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }
        public void OnClickRestartTutorialButton()
        {
            gameManager.OnClickTutorialButton();
        }

        public void OnClickMainMenu()
        {
            gameManager.ReturnToLobby();
        }
    }
}