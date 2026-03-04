using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persistent scene-loading and game-restart service.
    ///
    /// Handles:
    ///   - Launching gameplay scenes (local + network-aware)
    ///   - Restarting the current game (single-player + network-synced)
    ///   - Returning to the main menu
    ///   - Application quit cleanup
    ///
    /// Registered as a DI singleton via AppManager. Lives on a DontDestroyOnLoad root.
    /// Scene-placed EventListeners wire SOAP events (restart / return-to-menu) to public methods.
    /// </summary>
    public class SceneLoader : NetworkBehaviour
    {
        [SerializeField] float waitBeforeLoading = 0.5f;
        [Inject] GameDataSO gameData;
        [Inject] SceneNameListSO _sceneNames;
        [Inject] ApplicationStateMachine _appStateMachine;
        [Inject] SceneTransitionManager _sceneTransitionManager;

        #region Unity Lifecycle

        private void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            if (!gameData)
            {
                Debug.LogError("[SceneLoader] gameData was not injected — check AppManager DI registration.");
                return;
            }
            gameData.OnLaunchGame.OnRaised += LaunchGame;
            gameData.InvokeSceneTransition(true);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (!gameData) return;
            gameData.OnLaunchGame.OnRaised -= LaunchGame;
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
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
        /// When the Netcode host is active, network scene loading ensures
        /// scene-placed NetworkObjects are properly spawned in the new scene.
        /// </summary>
        void LaunchGame()
        {
            // ScreenSwitcher pauses the game (timeScale=0) on non-HOME screens.
            // Unpause before scene transition so spawn delays (DeltaTime-based) are not blocked.
            PauseSystem.TogglePauseGame(false);

            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LaunchGame — Scene={gameData.SceneName}, Mode={gameData.GameMode}, " +
                      $"IsMultiplayer={gameData.IsMultiplayerMode}, Vessel={gameData.selectedVesselClass.Value}, " +
                      $"Intensity={gameData.SelectedIntensity.Value}, PlayerCount={gameData.SelectedPlayerCount.Value}, " +
                      $"AIBackfill={gameData.RequestedAIBackfillCount}</color>");

            gameData.EnsureMinimumAIBackfill();

            _appStateMachine?.TransitionTo(ApplicationState.LoadingGame);
            Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] AppState → LoadingGame</color>");

            // Show splash overlay during scene transition.
            // One-shot listener fades it out once the game scene signals ready.
            _sceneTransitionManager?.SetFadeImmediate(1f);
            Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] Splash overlay set to black (alpha=1). Subscribed FadeFromSplashOnReady to OnClientReady</color>");
            gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;

            var nm = NetworkManager.Singleton;
            bool useNetworkSceneLoading = nm != null && nm.IsServer;
            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] useNetworkSceneLoading={useNetworkSceneLoading} (nm exists={nm != null}, IsServer={nm?.IsServer})</color>");

            // Sync game config to all clients before loading the scene.
            // The waitBeforeLoading delay in LoadSceneAsync gives time for RPC delivery.
            if (useNetworkSceneLoading)
            {
                Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] Sending SyncGameConfigToClients_ClientRpc</color>");
                SyncGameConfigToClients_ClientRpc(
                    gameData.SceneName,
                    (int)gameData.GameMode,
                    gameData.IsMultiplayerMode,
                    (int)gameData.selectedVesselClass.Value,
                    gameData.SelectedIntensity.Value,
                    gameData.SelectedPlayerCount.Value,
                    gameData.RequestedAIBackfillCount
                );
            }

            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Calling LoadSceneAsync({gameData.SceneName}, network={useNetworkSceneLoading})</color>");
            LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
        }

        void FadeFromSplashOnReady()
        {
            Debug.Log("<color=#FFFFFF><b>[FLOW-8] [SceneLoader] FadeFromSplashOnReady — OnClientReady fired! Fading splash out now.</b></color>");
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
            _sceneTransitionManager?.FadeFromBlack().Forget();
        }

        /// <summary>
        /// Load the main menu scene.
        /// Called by SOAP EventListener (EventOnClickToMainMenuButton).
        /// Uses network scene loading when a Netcode host is active.
        /// </summary>
        public void ReturnToMainMenu()
        {
            _appStateMachine?.TransitionTo(ApplicationState.MainMenu);
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            var nm = NetworkManager.Singleton;
            bool useNetworkSceneLoading = nm != null && nm.IsServer;
            LoadSceneAsync(menuScene, useNetworkSceneLoading).Forget();
        }

        async UniTaskVoid LoadSceneAsync(string sceneName, bool useNetworkSceneLoading)
        {
            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}, network={useNetworkSceneLoading}, waitBeforeLoading={waitBeforeLoading}s</color>");
            gameData.InvokeSceneTransition(false);

            // Clear vessel references on persistent Players before scene transition.
            // Vessels have destroyWithScene=true and are auto-handled by Netcode.
            // Setting NetVesselId to 0 propagates to clients via OnNetVesselIdChanged,
            // which nulls Player.Vessel — preventing stale references to destroyed objects.
            if (useNetworkSceneLoading)
                ClearPlayerVesselReferences();

            gameData.ResetRuntimeData();
            Debug.Log("<color=#FF8C00>[FLOW-3] [SceneLoader] ResetRuntimeData done. Waiting before load...</color>");

            await UniTask.Delay(
                TimeSpan.FromSeconds(waitBeforeLoading),
                DelayType.UnscaledDeltaTime
            );

            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Wait complete. Loading scene now: {sceneName}</color>");

            if (!useNetworkSceneLoading)
            {
                Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Using LOCAL scene load for {sceneName}</color>");
                SceneManager.LoadScene(sceneName);
                return;
            }

            var nm = NetworkManager.Singleton;

            if (nm == null)
            {
                Debug.LogWarning("<color=#FF8C00>[FLOW-3] [SceneLoader] NetworkManager missing. Falling back to local load.</color>");
                SceneManager.LoadScene(sceneName);
                return;
            }

            if (nm.IsServer)
            {
                LoadNetworkSceneOnServer(sceneName);
            }
            else
            {
                RequestSceneLoadServerRpc(sceneName);
            }
        }

        void ClearPlayerVesselReferences()
        {
            foreach (var player in gameData.Players)
            {
                if (player is Player netPlayer && netPlayer.IsSpawned)
                    netPlayer.NetVesselId.Value = 0;
            }
        }

        void LoadNetworkSceneOnServer(string sceneName)
        {
            var nm = NetworkManager.Singleton;

            if (nm?.SceneManager == null)
            {
                Debug.LogError("<color=#FF0000>[FLOW-3] [SceneLoader] Network SceneManager missing!</color>");
                return;
            }

            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Server loading network scene: {sceneName} via nm.SceneManager.LoadScene</color>");
            nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        #endregion

        #region Restart / Replay

        /// <summary>
        /// Reset the current game for replay without reloading the scene.
        /// Called by SOAP EventListener (EventOnClickToRestartButtonNoParam).
        ///
        /// In multiplayer, the request is routed through a ServerRpc so all
        /// clients reset in sync.
        /// </summary>
        public void RestartGame()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsConnectedClient && !nm.IsServer)
            {
                RestartGameServerRpc();
                return;
            }

            ExecuteRestart();
        }

        void ExecuteRestart()
        {
            gameData.ResetStatsDataForReplay();
            gameData.ResetForReplay();

            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }

        #endregion

        #region RPCs

        [ServerRpc(RequireOwnership = false)]
        void RequestSceneLoadServerRpc(string sceneName)
        {
            LoadNetworkSceneOnServer(sceneName);
        }

        [ServerRpc(RequireOwnership = false)]
        void RestartGameServerRpc()
        {
            gameData.ResetStatsDataForReplay();
            RestartGameClientRpc();
        }

        [ClientRpc]
        void RestartGameClientRpc()
        {
            gameData.ResetForReplay();

            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }

        /// <summary>
        /// Syncs the host's game configuration to all clients before a network scene load.
        /// Clients update their local GameDataSO so that Player.OnNetworkSpawn() reads the
        /// correct vessel class and HexRaceController generates an identical track.
        /// </summary>
        [ClientRpc]
        void SyncGameConfigToClients_ClientRpc(
            string sceneName, int gameMode, bool isMultiplayer,
            int vesselClass, int intensity, int playerCount, int aiBackfillCount)
        {
            if (IsServer) return; // Host already has correct values

            gameData.SceneName = sceneName;
            gameData.GameMode = (GameModes)gameMode;
            gameData.IsMultiplayerMode = isMultiplayer;
            gameData.selectedVesselClass.Value = (VesselClassType)vesselClass;
            gameData.SelectedIntensity.Value = intensity;
            gameData.SelectedPlayerCount.Value = playerCount;
            gameData.RequestedAIBackfillCount = aiBackfillCount;
        }

        #endregion

        private void OnApplicationQuit()
        {
            if (gameData) gameData.ResetAllData();
        }
    }
}
