using CosmicShore.App.Systems;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.SOAP;
using Cysharp.Threading.Tasks;
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
        GameDataSO gameData;
        
        [SerializeField]
        CanvasGroup canvasGroup;

        GameSetting gameSetting;

        /// <summary>
        /// stores if the local player input was paused before entering pause menu.
        /// </summary>
        bool wasLocalPlayerInputPausedBefore;

        void Awake() => Hide();
        
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
            TogglePlayerPauseWithDelay(false);
            Hide();
        }

        public void OnClickMultiplayerPauseButton()
        {
            TogglePlayerPauseWithDelay(true);
            Show();
        }
        
        /// <summary>
        /// On click the resume button from UI
        /// </summary>
        public void OnClickResumeGameButton()
        {
            PauseSystem.TogglePauseGame(false);
            Hide();
            
            if (!wasLocalPlayerInputPausedBefore)
                TogglePlayerPauseWithDelay(false);
        }

        /// <summary>
        /// On click the pause button from UI
        /// </summary>
        public void OnClickPauseGameButton()
        {
            PauseSystem.TogglePauseGame(true);
            Show();
            
            wasLocalPlayerInputPausedBefore = gameData.LocalPlayer.InputStatus.Paused;
            if (!wasLocalPlayerInputPausedBefore)
                TogglePlayerPauseWithDelay(true);
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
        
        async UniTaskVoid TogglePlayerPauseWithDelay(bool toggle)
        {
            await UniTask.Yield();
            gameData.LocalPlayer?.InputController.SetPause(toggle);
        }
    }
}