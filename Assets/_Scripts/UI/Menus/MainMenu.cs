using UnityEngine;


namespace StarWriter.Core.UI
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public GameObject Game_Options_Panel;

        GameManager gameManager;

        public void Start()
        {
            gameManager = GameManager.Instance;
        }

        public void OnClickPlayGame()
        {
            gameManager.OnClickPlayButton();
        }
        public void OnClickGameModeOne()
        {
            gameManager.OnClickTestGameModeOne();
        }
        public void OnClickGameModeTwo()
        {
            gameManager.OnClickTestGameModeTwo();
        }

        public void OnClickHangar()
        {
            gameManager.OnClickHangar();
        }

        public void OnClickOptionsMenuButton()
        {
            Game_Options_Panel.SetActive(true);
            gameObject.SetActive(false);
        }

        
    }
}


