using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEngine;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine.Serialization;

/// <summary>
/// Displays and controls toggles and buttons on the Pause Menu Panel
/// </summary>

// TODO: P1 - Need to unify this menu code with Main Menu Code
namespace CosmicShore.UI
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField]
        ScriptableEventNoParam _onClickToMainMenu;

        [SerializeField]
        GameDataSO gameData;

        [FormerlySerializedAs("canvasGroup")]
        [SerializeField] GameObject pauseMenuPanel;
        [SerializeField]
        ModalWindowManager settingsModalWindowManager;

        [Tooltip("Replay button - hidden for non-host clients in multiplayer. Leave unassigned if the prefab has no replay button.")]
        [SerializeField] GameObject replayButton;

        [Tooltip("Main Menu button - hidden for non-host clients in multiplayer (the host's return takes the whole party back). Leave unassigned if the prefab has no main menu button.")]
        [SerializeField] GameObject mainMenuButton;

        [Tooltip("Game controller for the active scene. Required for the Replay button to work. Wire the scene's MiniGameControllerBase subclass.")]
        [SerializeField] MiniGameControllerBase gameController;

        [Inject] GameSetting gameSetting;
        [Inject] AudioSystem audioSystem;

        /// <summary>
        /// stores if the local player input was paused before entering pause menu.
        /// </summary>
        bool wasLocalPlayerInputPausedBefore;

        /// <summary>
        /// True while Hide() itself is closing the modal, so the OnModalClosed
        /// callback only reacts to closes we did NOT initiate (e.g. gamepad B).
        /// </summary>
        bool isHidingFromCode;

        void Start()
        {
            if (settingsModalWindowManager != null)
                settingsModalWindowManager.OnModalClosed += HandleModalClosed;
        }

        /// <summary>
        /// Pays the pause panel's one-time activation cost - child Awake/OnEnable, layout
        /// rebuild, TMP mesh generation, the modal's backdrop creation - at scene start,
        /// behind the loading veil, instead of on the player's first pause tap
        /// mid-gameplay. The panel is activated invisible for two frames, then
        /// deactivated again. Called by MiniGameHUD.Start; the panel starts inactive in
        /// every scene, so it cannot warm itself.
        /// </summary>
        public void Prewarm() => PrewarmAsync().Forget();

        async UniTaskVoid PrewarmAsync()
        {
            if (pauseMenuPanel == null || pauseMenuPanel.activeSelf) return;

            var canvasGroup = pauseMenuPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = pauseMenuPanel.AddComponent<CanvasGroup>();

            float restAlpha = canvasGroup.alpha;
            bool restBlocksRaycasts = canvasGroup.blocksRaycasts;
            bool restInteractable = canvasGroup.interactable;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            pauseMenuPanel.SetActive(true);

            // Two frames: activation work runs this frame, Start-queued work and the
            // resulting layout/TMP rebuilds complete on the next.
            await UniTask.DelayFrame(2);
            if (this == null || pauseMenuPanel == null) return;

            // If the player managed to open the pause menu inside the warm window,
            // leave it up - only restore the group so it is visible.
            if (settingsModalWindowManager == null || !settingsModalWindowManager.IsOpen)
                pauseMenuPanel.SetActive(false);

            canvasGroup.alpha = restAlpha;
            canvasGroup.blocksRaycasts = restBlocksRaycasts;
            canvasGroup.interactable = restInteractable;
        }

        void OnDestroy()
        {
            if (settingsModalWindowManager != null)
                settingsModalWindowManager.OnModalClosed -= HandleModalClosed;
        }

        /// <summary>
        /// The pause modal was dismissed by a path that bypasses our buttons - the
        /// ModalWindowManager's gamepad B (East) close. Route it through the same
        /// resume flow as the on-screen Resume button so the game and the player's
        /// input actually unpause instead of leaving the vessel frozen.
        /// </summary>
        void HandleModalClosed()
        {
            if (isHidingFromCode) return;
            if (!pauseMenuPanel.activeInHierarchy) return;

            if (PauseSystem.Paused)
                OnClickResumeGameButton();
            else
                OnClickMultiplayerResumeGameButton();
        }

        /// <summary>
        /// Toggles the Master Volume On/Off
        /// </summary>
        public void OnClickToggleMusic() => gameSetting.ChangeMusicEnabledSetting();

        /// <summary>
        /// Toggles the Inverted Y Axis Controls
        /// </summary>
        public void OnClickToggleInvertY() => gameSetting.ChangeInvertYEnabledStatus();

        /// <summary>
        /// Routes the restart through the active MiniGameController - the same path
        /// the scoreboard's Play Again uses. In multiplayer, non-host clients are
        /// filtered out by the controller (and the button is hidden by Show()).
        /// </summary>
        public void OnClickReplayButton()
        {
            if (gameController == null)
            {
                CSDebug.LogError("[PauseMenu] gameController not assigned - wire the scene's MiniGameControllerBase in the inspector.");
                return;
            }

            PauseSystem.TogglePauseGame(false);
            Hide();
            gameController.RequestReplay();
        }

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

        /// <summary>
        /// Host only - the host's return carries every client back to Menu_Main via
        /// the network scene load. Defense in depth: the button is hidden for non-host
        /// clients (ConfigureHostOnlyButtons), but guard the call path too.
        /// </summary>
        public void OnClickMainMenu()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                CSDebug.LogWarning("[PauseMenu] Main Menu ignored - only the host can return the party to the menu.");
                return;
            }

            _onClickToMainMenu.Raise();
        }

        public void Show()
        {
            ConfigureHostOnlyButtons();
            pauseMenuPanel.gameObject.SetActive(true);
            settingsModalWindowManager.ModalWindowIn();
            audioSystem.PlayGameplaySFX(GameplaySFXCategory.PauseOpen);
        }

        /// <summary>
        /// Host-only gating for the replay and main menu buttons. Mirrors the
        /// Scoreboard's ConfigureLobbyButtons logic so non-host clients can't
        /// trigger a restart or a host-authoritative return to menu.
        /// </summary>
        void ConfigureHostOnlyButtons()
        {
            var nm = NetworkManager.Singleton;
            bool isClient = nm == null || !nm.IsServer;

            if (replayButton)   replayButton.SetActive(!isClient);
            if (mainMenuButton) mainMenuButton.SetActive(!isClient);
        }

        public void Hide()
        {
            isHidingFromCode = true;
            settingsModalWindowManager.ModalWindowOut();
            isHidingFromCode = false;

            pauseMenuPanel.gameObject.SetActive(false);
            audioSystem.PlayGameplaySFX(GameplaySFXCategory.PauseClose);
        }

        async UniTaskVoid TogglePlayerPauseWithDelay(bool toggle)
        {
            await UniTask.Yield();
            gameData.LocalPlayer?.InputController.SetPause(toggle);
        }
    }
}