using CosmicShore.App.Systems;
using CosmicShore.Utilities;
using System.Collections;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

// TODO: P1 - some work needs to be done to unify this with the MiniGame engine managers
namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : Singleton<GameManager>
    {
        const float WAIT_FOR_SECONDS_BEFORE_RETURN_TO_MAIN_MENU = .5f;

        [SerializeField]
        SceneNameListSO _sceneNames;
        
        [SerializeField] public SO_GameList AllGames;

        [SerializeField]
        ScriptableEventNoParam _onStartSceneTransition;

        [SerializeField]
        ScriptableEventNoParam _onReturnToMainMenu;
        
        [SerializeField] 
        ScriptableEventNoParam _onPlayGame;
        
        [SerializeField]
        ScriptableEventNoParam _onGameOver;

        public void StartGame() => _onPlayGame.Raise();

        public void EndGame() => _onGameOver.Raise();

        public void RestartGame()
        {
            // Debug.Log("GameManager.RestartGame");

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            PauseSystem.TogglePauseGame(true);
        }

        public void ReturnToMainMenu() => StartCoroutine(ReturnToMainMenuCoroutine());

        IEnumerator ReturnToMainMenuCoroutine()
        {
            _onStartSceneTransition.Raise();
            
            yield return new WaitForSecondsRealtime(WAIT_FOR_SECONDS_BEFORE_RETURN_TO_MAIN_MENU);
            
            SceneManager.LoadScene(_sceneNames.MainMenuScene);
            PauseSystem.TogglePauseGame(false);
        }
    }
}