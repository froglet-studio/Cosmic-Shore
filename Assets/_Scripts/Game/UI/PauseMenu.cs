using CosmicShore.App.Systems;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.SOAP;
using Obvious.Soap;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

// TODO: P1 - Need to unify this menu code with Main Menu Code
namespace CosmicShore.App.UI.Screens
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] 
        ScriptableEventNoParam _onClickToMainMenu;
        
        [SerializeField] 
        ScriptableEventNoParam _onClickToRestartButton;
        
        [SerializeField] 
        GameObject MiniGameHUD;
        
        [SerializeField]
        GameDataSO gameData;

        GameSetting gameSetting;

        // Start is called before the first frame update
        void Start() => gameSetting = GameSetting.Instance;

        /// <summary>
        /// Toggles the Master Volume On/Off
        /// </summary>
        public void OnClickToggleMusic() => gameSetting.ChangeMusicEnabledSetting();

        /// <summary>
        /// Toggles the Inverted Y Axis Controls
        /// </summary>
        public void OnClickToggleInvertY() => gameSetting.ChangeInvertYEnabledStatus();

        public void OnClickReplayButton() => _onClickToRestartButton.Raise();

        public void OnClickMultiplayerResumeGameButton()
        {
            MiniGameHUD.SetActive(true);
            gameData.LocalPlayer.InputController.SetPause(false);
        }

        public void OnClickMultiplayerPauseButton()
        {
            MiniGameHUD.SetActive(false);
            gameData.LocalPlayer.InputController.SetPause(true);
        }
        
        /// <summary>
        /// UnPauses the game 
        /// </summary>
        public void OnClickResumeGameButton()
        {
            PauseSystem.TogglePauseGame(false);
            MiniGameHUD.SetActive(true);
        }

        /// <summary>
        /// Pauses the game 
        /// </summary>
        public void OnClickPauseGameButton()
        {
            PauseSystem.TogglePauseGame(true);
            MiniGameHUD.SetActive(false);
        }

        public void OnClickMainMenu() => _onClickToMainMenu.Raise();
    }
}