using System;
using CosmicShore.App.Systems;
using CosmicShore.Game;
using CosmicShore.Utilities;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.Serialization;

namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : NetworkBehaviour
    {
        const float WAIT_FOR_SECONDS_BEFORE_SCENELOAD = 0.5f;

        [SerializeField] SceneNameListSO _sceneNames;
        [SerializeField] SO_GameList AllGames;
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] ScriptableEventBool _onSceneTransition;
        
        
        private void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
            gameData.OnLaunchGameScene += LaunchGameScene;
        }

        private void Start()
        {
            Debug.Log($"[ScenePerf] GameManager.Start — scene ready t={Time.realtimeSinceStartup:F3}");
            _onSceneTransition.Raise(true);
        }

        private void OnDisable()
        {
            gameData.OnLaunchGameScene -= LaunchGameScene;
        }
        
        public virtual void RestartGame()
        {
            gameData.ResetStatsDataForReplay();
            InvokeOnResetForReplay();

            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }

        public virtual void ReturnToMainMenu() => LoadSceneAsync(_sceneNames.MainMenuScene).Forget();

        protected void InvokeOnResetForReplay() => gameData.ResetForReplay();
        
        void LaunchGameScene()
        {
            /*if (miniGameData.IsMultiplayerMode)
                return;*/

            LoadSceneAsync(gameData.SceneName).Forget();
        }

        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            Debug.Log($"[ScenePerf] LoadSceneAsync START → '{sceneName}' t={Time.realtimeSinceStartup:F3}");

            _onSceneTransition.Raise(false);
            Debug.Log($"[ScenePerf] _onSceneTransition.Raise(false) done t={Time.realtimeSinceStartup:F3}");

            gameData.ResetRuntimeData();
            Debug.Log($"[ScenePerf] ResetRuntimeData done t={Time.realtimeSinceStartup:F3}");

            // Delay is realtime so it still works if Time.timeScale = 0
            await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD),
                DelayType.UnscaledDeltaTime);

            Debug.Log($"[ScenePerf] Pre-delay done, calling LoadSceneAsync t={Time.realtimeSinceStartup:F3}");
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op != null)
                await op.ToUniTask();
            Debug.Log($"[ScenePerf] LoadSceneAsync COMPLETE t={Time.realtimeSinceStartup:F3}");
        }

        private void OnApplicationQuit()
        {
            gameData.ResetAllData();
        }
    }
}