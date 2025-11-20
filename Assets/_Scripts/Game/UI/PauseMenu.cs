using CosmicShore.App.Systems;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.UI;
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
        MiniGameHUD MiniGameHUD;
        
        [SerializeField]
        GameDataSO gameData;
        
        [SerializeField]
        CanvasGroup canvasGroup;

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
            MiniGameHUD.ToggleView(true);
            gameData.LocalPlayer.InputController.SetPause(false);
        }

        public void OnClickMultiplayerPauseButton()
        {
            MiniGameHUD.ToggleView(false);
            gameData.LocalPlayer.InputController.SetPause(true);
        }
        
        /// <summary>
        /// UnPauses the game 
        /// </summary>
        public void OnClickResumeGameButton()
        {
            PauseSystem.TogglePauseGame(false);
            MiniGameHUD.ToggleView(true);
        }

        /// <summary>
        /// Pauses the game 
        /// </summary>
        public void OnClickPauseGameButton()
        {
            PauseSystem.TogglePauseGame(true);
            MiniGameHUD.ToggleView(false);
        }

        public void OnClickMainMenu() => _onClickToMainMenu.Raise();

        public void Show()
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        public void Hide()
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}