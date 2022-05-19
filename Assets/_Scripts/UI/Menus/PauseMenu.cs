using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.Audio;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

namespace StarWriter.UI
{
    public class PauseMenu : MonoBehaviour
    {
        GameManager gameManager;
        AudioManager audioManager;

        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;
            audioManager = AudioManager.Instance;

        }
        /// <summary>
        /// Toggles the Master Volume On/Off
        /// </summary>
        public void OnToggleMusic()
        {
            audioManager.ToggleMute();
        }
        /// <summary>
        /// Toggles the Gyroscope On/Off
        /// </summary>
        public void OnToggleGyro()
        {
            gameManager.OnClickGyroToggleButton();
        }
        /// <summary>
        /// Calls the Tutorial Scene to be loaded
        /// </summary>
        public void OnClickTutorialButton()
        {
            gameManager.OnClickTutorialToggleButton();
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
        public void OnClickResumeButton()
        {
            gameManager.UnPauseGame();
            transform.GetComponentInParent<GameMenu>().OnClickUnpauseGame();
        }


    }
}