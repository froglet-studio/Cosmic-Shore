using System;
using CosmicShore.Utilities;
using CosmicShore.SOAP;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : MonoBehaviour
    {
        const float WAIT_FOR_SECONDS_BEFORE_SCENELOAD = 0.5f;

        [SerializeField] SceneNameListSO _sceneNames;
        [SerializeField] SO_GameList AllGames;
        [SerializeField] MiniGameDataSO miniGameData;
        [SerializeField] ScriptableEventBool _onSceneTransition;

        private void OnEnable()
        {
            miniGameData.OnLaunchGame += LaunchGame;
        }

        private void Start() => _onSceneTransition.Raise(true);

        private void OnDisable()
        {
            miniGameData.OnLaunchGame -= LaunchGame;
        }

        public void RestartGame() => LoadSceneAsync(SceneManager.GetActiveScene().name).Forget();

        public void ReturnToMainMenu() => LoadSceneAsync(_sceneNames.MainMenuScene).Forget();

        void LaunchGame()
        {
            /*if (miniGameData.IsMultiplayerMode)
                return;*/

            LoadSceneAsync(miniGameData.SceneName).Forget();
        }

        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            _onSceneTransition.Raise(false);

            miniGameData.ResetOnSceneChanged();
            
            // Delay is realtime so it still works if Time.timeScale = 0
            await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD), 
                DelayType.UnscaledDeltaTime);

            SceneManager.LoadScene(sceneName);
        }

        private void OnApplicationQuit()
        {
            TeamAssigner.ClearCache();
            miniGameData.ResetData();
        }
    }
}