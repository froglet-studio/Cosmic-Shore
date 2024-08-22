using UnityEngine;
using CosmicShore.Core;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

// TODO: P1 - Need to unify this menu code with Main Menu Code
namespace CosmicShore.App.UI.Screens
{
    public class PauseMenu : MonoBehaviour
    {
        //[SerializeField] GameMenu gameMenu;
        [SerializeField] GameObject MiniGameHUD;

        GameSetting gameSetting;

        // Start is called before the first frame update
        void Start()
        {
            gameSetting = GameSetting.Instance;
        }

        /// <summary>
        /// Toggles the Master Volume On/Off
        /// </summary>
        public void OnClickToggleMusic()
        {
            gameSetting.ChangeMusicEnabledSetting();
        }

        /// <summary>
        /// Toggles the Inverted Y Axis Controls
        /// </summary>
        public void OnClickToggleInvertY()
        {
            gameSetting.ChangeInvertYEnabledStatus();
        }

        public void OnClickReplayButton()
        {
            GameManager.Instance.RestartGame();
        }

        /// <summary>
        /// UnPauses the game 
        /// </summary>
        public void OnClickResumeGameButton()
        {
            GameManager.UnPauseGame();
            MiniGameHUD.SetActive(true);
        }

        /// <summary>
        /// Pauses the game 
        /// </summary>
        public void OnClickPauseGameButton()
        {
            GameManager.PauseGame();
            MiniGameHUD.SetActive(false);
        }

        public void OnClickMainMenu()
        {
            GameManager.ReturnToLobby();
        }
    }
}