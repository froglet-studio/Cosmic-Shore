using System;
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

        #region Unity Lifecycle

        private void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
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
            if (!gameData) return;
            gameData.OnLaunchGame.OnRaised -= LaunchGame;
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
            var nm = NetworkManager.Singleton;
            bool useNetworkSceneLoading = nm != null && nm.IsServer;
            LoadSceneAsync(gameData.SceneName, useNetworkSceneLoading).Forget();
        }

        /// <summary>
        /// Load the main menu scene.
        /// Called by SOAP EventListener (EventOnClickToMainMenuButton).
        /// </summary>
        public void ReturnToMainMenu()
        {
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            LoadSceneAsync(menuScene, false).Forget();
        }

        async UniTaskVoid LoadSceneAsync(string sceneName, bool useNetworkSceneLoading)
        {
            gameData.InvokeSceneTransition(false);
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

            if (nm.IsServer)
            {
                LoadNetworkSceneOnServer(sceneName);
            }
            else
            {
                RequestSceneLoadServerRpc(sceneName);
            }
        }

        void LoadNetworkSceneOnServer(string sceneName)
        {
            var nm = NetworkManager.Singleton;

            if (nm?.SceneManager == null)
            {
                Debug.LogError("[SceneLoader] Network SceneManager missing.");
                return;
            }

            Debug.Log($"[SceneLoader] Server loading network scene: {sceneName}");
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

        #endregion

        private void OnApplicationQuit()
        {
            if (gameData) gameData.ResetAllData();
        }
    }
}
