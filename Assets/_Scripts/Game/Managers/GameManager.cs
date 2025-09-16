using System;
using CosmicShore.Utilities;
using System.Collections;
using CosmicShore.SOAP;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

// TODO: P1 - some work needs to be done to unify this with the MiniGame engine managers
namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : MonoBehaviour
    {
        const float WAIT_FOR_SECONDS_BEFORE_SCENELOAD = .5f;

        [SerializeField]
        SceneNameListSO _sceneNames;
        
        [SerializeField] 
        SO_GameList AllGames;
        
        [SerializeField]
        MiniGameDataSO miniGameData;

        [SerializeField]
        ScriptableEventBool _onSceneTransition;

        private void OnEnable()
        {
            miniGameData.OnLaunchGame += LaunchGame;
        }

        private void Start() => _onSceneTransition.Raise(true);

        private void OnDisable()
        {
            miniGameData.OnLaunchGame -= LaunchGame;
        }

        public void RestartGame() => StartCoroutine(StartSceneRoutine(SceneManager.GetActiveScene().name));

        public void ReturnToMainMenu() => StartCoroutine(StartSceneRoutine(_sceneNames.MainMenuScene));

        void LaunchGame()
        {
            if (miniGameData.IsMultiplayerMode)
                return;
            
            StartCoroutine(StartSceneRoutine(miniGameData.SceneName));   
        }
        
        IEnumerator StartSceneRoutine(string sceneName)
        {
            _onSceneTransition.Raise(false);
            
            yield return new WaitForSecondsRealtime(WAIT_FOR_SECONDS_BEFORE_SCENELOAD);
            SceneManager.LoadScene(sceneName);
        }

        private void OnApplicationQuit()
        {
            TeamAssigner.ClearCache();
            miniGameData.ResetData();
        }
    }
}