using System;
using CosmicShore.Systems;
using CosmicShore.Game;
using CosmicShore.Soap;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : NetworkBehaviour
    {
        [Inject] protected CameraManager cameraManager;
        const float WAIT_FOR_SECONDS_BEFORE_SCENELOAD = 0.5f;

        [SerializeField] SceneNameListSO _sceneNames;
        [SerializeField] SO_GameList AllGames;
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] ScriptableEventBool _onSceneTransition;
        
        
        private void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
            gameData.OnLaunchGame.OnRaised += LaunchGame;
        }

        private void Start() => _onSceneTransition.Raise(true);

        private void OnDisable()
        {
            gameData.OnLaunchGame.OnRaised -= LaunchGame;
        }
        
        public virtual void RestartGame()
        {
            gameData.ResetStatsDataForReplay();
            InvokeOnResetForReplay();

            if (cameraManager)
                cameraManager.SnapPlayerCameraToTarget();
        }

        public virtual void ReturnToMainMenu() => LoadSceneAsync(_sceneNames.MainMenuScene).Forget();

        protected void InvokeOnResetForReplay() => gameData.ResetForReplay();
        
        void LaunchGame()
        {
            /*if (miniGameData.IsMultiplayerMode)
                return;*/

            LoadSceneAsync(gameData.SceneName).Forget();
        }

        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            _onSceneTransition.Raise(false);

            gameData.ResetRuntimeData();
            
            // Delay is realtime so it still works if Time.timeScale = 0
            await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD), 
                DelayType.UnscaledDeltaTime);

            SceneManager.LoadScene(sceneName);
        }

        private void OnApplicationQuit()
        {
            gameData.ResetAllData();
        }
    }
}