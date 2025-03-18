using UnityEngine;
using CosmicShore.Core;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

// TODO: Move the remaining functions into Settings Modal and get rid of this script
namespace CosmicShore.App.UI.Screens
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject MiniGameHUD;

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
        }

        /// <summary>
        /// Pauses the game 
        /// </summary>
        public void OnClickPauseGameButton()
        {
            GameManager.PauseGame();
        }

        public void OnClickMainMenu()
        {
            GameManager.ReturnToLobby();
        }
    }
}