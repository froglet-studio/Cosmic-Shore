using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persistent scene-loading service.
    ///
    /// Handles:
    ///   - Launching gameplay scenes (local + network-aware)
    ///   - Returning to the main menu
    ///   - Application quit cleanup
    ///
    /// Lives on a DontDestroyOnLoad root in the Bootstrap scene.
    /// Registered as a DI singleton via AppManager.
    /// Subscribes to SOAP events in code — no per-scene EventListenerNoParam wiring needed.
    ///
    /// Note: This is a plain MonoBehaviour (not NetworkBehaviour). Network-aware
    /// config sync is handled by MultiplayerMiniGameControllerBase.OnNetworkSpawn().
    /// Replay / restart is owned by MiniGameControllerBase.RequestReplay() — both
    /// the scoreboard and the pause menu call it directly.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] float waitBeforeLoading = 0.5f;

        [Header("SOAP Events (wired in Bootstrap inspector)")]
        [SerializeField] ScriptableEventNoParam _onClickToMainMenuButton;
        [SerializeField] ScriptableEventNoParam _onActiveSessionEnd;

        [Inject] GameDataSO gameData;
        [Inject] SceneNameListSO _sceneNames;
        [Inject] ApplicationStateMachine _appStateMachine;
        [Inject] SceneTransitionManager _sceneTransitionManager;

        #region Unity Lifecycle

        void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            if (!gameData)
            {
                Debug.LogError("[SceneLoader] gameData was not injected — check AppManager DI registration.");
                return;
            }

            gameData.OnLaunchGame.OnRaised += LaunchGame;
            gameData.InvokeSceneTransition(true);

            // Subscribe to SOAP events that were previously wired via per-scene EventListenerNoParam.
            if (_onClickToMainMenuButton)
                _onClickToMainMenuButton.OnRaised += ReturnToMainMenu;
            if (_onActiveSessionEnd)
                _onActiveSessionEnd.OnRaised += HandleActiveSessionEnd;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (!gameData) return;

            gameData.OnLaunchGame.OnRaised -= LaunchGame;
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;

            if (_onClickToMainMenuButton)
                _onClickToMainMenuButton.OnRaised -= ReturnToMainMenu;
            if (_onActiveSessionEnd)
                _onActiveSessionEnd.OnRaised -= HandleActiveSessionEnd;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (gameData)
                gameData.InvokeSceneTransition(true);
        }

        #endregion

        #region Scene Loading

        /// <summary>
        /// Automatically decides local vs network scene loading based on whether a host/server is running.
        /// </summary>
        void LaunchGame()
        {
            PauseSystem.TogglePauseGame(false);

            // Clear any saved modal return state so no stale modal reopens after the game.
            // The ScreenSwitcher in Menu_Main reads these keys on Start() and would
            // otherwise restore whatever modal was open when the game launched.
            PlayerPrefs.DeleteKey("ReturnToModal");
            PlayerPrefs.Save();

            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LaunchGame — Scene={gameData.SceneName}, Mode={gameData.GameMode}, " +
                      $"IsMultiplayer={gameData.IsMultiplayerMode}, Vessel={gameData.selectedVesselClass.Value}, " +
                      $"Intensity={gameData.SelectedIntensity.Value}, PlayerCount={gameData.SelectedPlayerCount.Value}, " +
                      $"AIBackfill={gameData.RequestedAIBackfillCount}</color>");

            _appStateMachine?.TransitionTo(ApplicationState.LoadingGame);

            // Show splash overlay during scene transition.
            _sceneTransitionManager?.SetFadeImmediate(1f);
            gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;

            var nm = NetworkManager.Singleton;

            // In multiplayer, only the server initiates scene loads.
            // Clients receive the scene transition via Netcode's scene management.
            // Without this guard, shared SOAP events (e.g. in MPPM) cause the client
            // to call SceneManager.LoadScene() locally, which races with the server's
            // network load and destroys AI NetworkObjects before they can replicate.
            if (nm != null && nm.IsListening && !nm.IsServer)
                return;

            bool useNetworkSceneLoading = nm != null && nm.IsServer;

            // Game config sync to clients is now handled by
            // MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()
            // in the game scene's OnNetworkSpawn, rather than here before scene load.

            LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
        }

        void FadeFromSplashOnReady()
        {
            Debug.Log("<color=#FFFFFF><b>[FLOW-8] [SceneLoader] FadeFromSplashOnReady — OnClientReady fired!</b></color>");
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
            _sceneTransitionManager?.FadeFromBlack().Forget();
        }

        /// <summary>
        /// Load the main menu scene.
        /// Subscribed to EventOnClickToMainMenuButton and called on session end.
        /// </summary>
        public void ReturnToMainMenu()
        {
            _appStateMachine?.TransitionTo(ApplicationState.MainMenu);

            // Prevent the game scene's ServerPlayerVesselInitializer from calling
            // NetworkManager.Shutdown() during the scene transition. The network
            // must stay alive for Menu_Main's vessel spawning pipeline.
            gameData.IsReturnToMenuTransition = true;

            // Clear stale return-to-screen/modal state so Menu_Main starts clean
            // on HOME with no modals open. These keys are set by ScreenSwitcher
            // during normal menu navigation but become stale when a scene
            // transition destroys modal GameObjects without proper ModalWindowOut().
            PlayerPrefs.DeleteKey("ReturnToScreen");
            PlayerPrefs.DeleteKey("ReturnToModal");
            PlayerPrefs.Save();

            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            var nm = NetworkManager.Singleton;

            // Clients rely on the server's Netcode scene management for transitions.
            if (nm != null && nm.IsListening && !nm.IsServer)
                return;

            bool useNetworkSceneLoading = nm != null && nm.IsServer;
            LoadSceneAsync(menuScene, useNetworkSceneLoading).Forget();
        }

        async UniTaskVoid LoadSceneAsync(string sceneName, bool useNetworkSceneLoading)
        {
            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}</color>");
            gameData.InvokeSceneTransition(false);

            if (useNetworkSceneLoading)
                ClearPlayerVesselReferences();

            gameData.ResetRuntimeData();

            await UniTask.Delay(
                TimeSpan.FromSeconds(waitBeforeLoading),
                DelayType.UnscaledDeltaTime
            );

            if (!useNetworkSceneLoading)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogWarning("[SceneLoader] NetworkManager missing. Falling back to local load.");
                SceneManager.LoadScene(sceneName);
                return;
            }

            if (nm.IsServer && nm.SceneManager != null)
            {
                nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[SceneLoader] Not server or SceneManager null — cannot load network scene.");
            }
        }

        void ClearPlayerVesselReferences()
        {
            Debug.Log($"<color=#00FFFF>[DESPAWN] ClearPlayerVesselReferences — Players={gameData.Players.Count}, Vessels={gameData.Vessels.Count}</color>");

            foreach (var player in gameData.Players)
            {
                if (player is Player netPlayer && netPlayer.IsSpawned)
                    netPlayer.NetVesselId.Value = 0;
            }

            // Explicitly despawn AI Player NetworkObjects so they don't persist
            // into Menu_Main. Human players survive (destroyWithScene=false from
            // connection approval) but AI players must be removed.
            // Must happen BEFORE vessel despawn — AI player destruction after vessel
            // despawn causes MissingReferenceException when VesselAnimation.Update()
            // accesses the destroyed Player on the same frame.
            for (int i = gameData.Players.Count - 1; i >= 0; i--)
            {
                if (gameData.Players[i] is Player aiPlayer
                    && aiPlayer.IsSpawned
                    && aiPlayer.NetIsAI.Value)
                {
                    aiPlayer.NetworkObject.Despawn(true);
                }
            }

            // Despawn all vessels and destroy their GameObjects. Using destroy=true
            // ensures VesselAnimation.Update() cannot run with stale Player references
            // during the scene transition.
            for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
            {
                var vessel = gameData.Vessels[i];
                if (vessel is VesselController vc && vc.IsSpawned)
                    vc.NetworkObject.Despawn(true);
            }

            gameData.Vessels.Clear();
        }

        #endregion

        #region Session End

        void HandleActiveSessionEnd()
        {
            // Clients rely on the server for session cleanup and scene transitions.
            // Without this guard, shared SOAP events in MPPM cause the client to
            // call ResetAllData() on the shared GameDataSO, wiping server state.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer)
                return;

            // Clear the stale party session reference so HostConnectionService
            // can create a fresh Relay-backed session when Menu_Main loads.
            // LeaveSession() deletes the game session and shuts down the network;
            // the party session created during initial auth is now stale.
            if (HostConnectionService.Instance != null)
                HostConnectionService.Instance.ClearStalePartySession();

            ReturnToMainMenu();
            gameData.ResetAllData();
        }

        #endregion

        void OnApplicationQuit()
        {
            if (gameData) gameData.ResetAllData();
        }
    }
}
