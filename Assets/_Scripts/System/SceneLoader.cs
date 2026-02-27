using System;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    public class SceneLoader : NetworkBehaviour
    {
        [SerializeField] float waitBeforeLoading = 0.5f;
        [Inject] GameDataSO gameData;
        
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

        #region Public API

        /// <summary>
        /// Main entry point.
        /// </summary>
        void LoadScene(string sceneName, bool useNetworkSceneLoading)
        {
            LoadSceneAsync(sceneName, useNetworkSceneLoading).Forget();
        }

        /// <summary>
        /// Automatically decides based on whether a host/server is running.
        /// When the Netcode host is active, network scene loading ensures
        /// scene-placed NetworkObjects are properly spawned in the new scene.
        /// </summary>
        private void LaunchGame()
        {
            var nm = NetworkManager.Singleton;
            bool useNetworkSceneLoading = nm != null && nm.IsServer;
            LoadScene(gameData.SceneName, useNetworkSceneLoading);
        }

        #endregion

        #region Core Logic

        private async UniTaskVoid LoadSceneAsync(string sceneName, bool useNetworkSceneLoading)
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

            // 🔥 IMPORTANT: Only server loads network scenes
            if (nm.IsServer)
            {
                LoadNetworkSceneOnServer(sceneName);
            }
            else
            {
                RequestSceneLoadServerRpc(sceneName);
            }
        }

        private void LoadNetworkSceneOnServer(string sceneName)
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

        #region RPC

        [ServerRpc(RequireOwnership = false)]
        private void RequestSceneLoadServerRpc(string sceneName)
        {
            LoadNetworkSceneOnServer(sceneName);
        }

        #endregion

        private void OnApplicationQuit()
        {
            if (gameData) gameData.ResetAllData();
        }
    }
}