using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.Audio;
using UnityEngine.UI;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

// TODO: P1 - Need to unify this menu code with Main Menu Code
namespace StarWriter.UI
{
    public class PauseMenu : MonoBehaviour
    {
        //[SerializeField] GameMenu gameMenu;
        [SerializeField] GameObject MiniGameHUD;

        GameManager gameManager;
        GameSetting gameSetting;

        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;
            gameSetting = GameSetting.Instance;
        }

        /// <summary>
        /// Toggles the Master Volume On/Off
        /// </summary>
        public void OnClickToggleMusic()
        {
            gameSetting.ChangeAudioEnabledStatus();
        }

        /// <summary>
        /// Toggles the Inverted Y Axis Controls
        /// </summary>
        public void OnClickToggleInvertY()
        {
            gameSetting.ChangeInvertYEnabledStatus();
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
            MiniGameHUD.SetActive(true);
            //gameMenu.OnClickUnpauseGame();
        }

        /// <summary>
        /// Pauses the game 
        /// </summary>
        public void OnClickPauseGameButton()
        {
            GameManager.PauseGame();
            MiniGameHUD.SetActive(false);
            //gameMenu.OnClickPauseGame();
        }

        public void OnClickMainMenu()
        {
            gameManager.ReturnToLobby();
        }
    }
}