using CosmicShore.App.Systems;
using CosmicShore.App.Systems.Audio;
using CosmicShore.App.UI.Modals;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

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

        [FormerlySerializedAs("canvasGroup")]
        [SerializeField] GameObject pauseMenuPanel;
        [SerializeField]
        ModalWindowManager settingsModalWindowManager;

        [Header("Controller Navigation")]
        [Tooltip("First button to select when pause menu opens with a gamepad.")]
        [SerializeField] private Selectable firstPauseSelectable;

        [Tooltip("If true, this is a multiplayer pause menu (only pauses local player input, not the game).")]
        [SerializeField] private bool isMultiplayer;

        GameSetting gameSetting;

        /// <summary>
        /// stores if the local player input was paused before entering pause menu.
        /// </summary>
        bool wasLocalPlayerInputPausedBefore;

        bool IsOpen => pauseMenuPanel != null && pauseMenuPanel.activeSelf;

        void Start() => gameSetting = GameSetting.Instance;

        void Update()
        {
            if (Gamepad.current == null) return;

            // Start/Menu button toggles pause
            if (Gamepad.current.startButton.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    if (isMultiplayer)
                        OnClickMultiplayerResumeGameButton();
                    else
                        OnClickResumeGameButton();
                }
                else
                {
                    if (isMultiplayer)
                        OnClickMultiplayerPauseButton();
                    else
                        OnClickPauseGameButton();
                }
            }

            // B button closes pause menu
            if (IsOpen && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                if (isMultiplayer)
                    OnClickMultiplayerResumeGameButton();
                else
                    OnClickResumeGameButton();
            }
        }

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
            _ = TogglePlayerPauseWithDelay(false);
            Hide();
        }

        public void OnClickMultiplayerPauseButton()
        {
            _ = TogglePlayerPauseWithDelay(true);
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
                _ = TogglePlayerPauseWithDelay(false);
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
                _ = TogglePlayerPauseWithDelay(true);
        }

        public void OnClickMainMenu() => _onClickToMainMenu.Raise();

        public void Show()
        {
            pauseMenuPanel.gameObject.SetActive(true);
            settingsModalWindowManager.ModalWindowIn();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.PauseOpen);
            AutoFocusForGamepad();
        }

        private void AutoFocusForGamepad()
        {
            if (Gamepad.current == null || EventSystem.current == null) return;

            if (firstPauseSelectable != null && firstPauseSelectable.gameObject.activeInHierarchy && firstPauseSelectable.interactable)
            {
                EventSystem.current.SetSelectedGameObject(firstPauseSelectable.gameObject);
                return;
            }

            // Fallback: find first interactable button in the pause panel
            var selectables = pauseMenuPanel.GetComponentsInChildren<Selectable>(false);
            foreach (var s in selectables)
            {
                if (s.interactable && s.navigation.mode != Navigation.Mode.None)
                {
                    EventSystem.current.SetSelectedGameObject(s.gameObject);
                    return;
                }
            }
        }

        public void Hide()
        {
            settingsModalWindowManager.ModalWindowOut();
            pauseMenuPanel.gameObject.SetActive(false);
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.PauseClose);
        }
        
        async UniTaskVoid TogglePlayerPauseWithDelay(bool toggle)
        {
            await UniTask.Yield();
            gameData.LocalPlayer?.InputController.SetPause(toggle);
        }
    }
}