using System;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.App.Systems
{
    [DefaultExecutionOrder(0)]
    public class SceneLoader : SingletonNetworkPersistent<SceneLoader>
    {
        const float WAIT_FOR_SECONDS_BEFORE_SCENELOAD = 0.5f;

        [SerializeField] protected GameDataSO gameData;
        

        #region Unity Lifecycle

        private void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
            gameData.OnLaunchGame.OnRaised += LaunchGame;
        }

        private void Start()
        {
            gameData.InvokeSceneTransition(true);
        }

        private void OnDisable()
        {
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
        /// Automatically decides based on multiplayer flag.
        /// </summary>
        private void LaunchGame()
        {
            bool useNetworkSceneLoading = gameData.IsMultiplayerMode;
            LoadScene(gameData.SceneName, useNetworkSceneLoading);
        }

        #endregion

        #region Core Logic

        private async UniTaskVoid LoadSceneAsync(string sceneName, bool useNetworkSceneLoading)
        {
            gameData.InvokeSceneTransition(false);
            gameData.ResetRuntimeData();

            await UniTask.Delay(
                TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD),
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
            gameData.ResetAllData();
        }
    }
}