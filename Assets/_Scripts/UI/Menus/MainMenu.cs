using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarWriter.Core.UI
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] MiniGamesMenu miniGamesMenu;

        GameManager gameManager;

        public void Start()
        {
            gameManager = GameManager.Instance;
        }

        public void OnClickSoar()
        {
            MiniGame.PlayerShipType = ShipTypes.Manta;
            MiniGame.DifficultyLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameSandbox");
        }
        public void OnClickSmash()
        {
            MiniGame.PlayerShipType = ShipTypes.Shark;
            MiniGame.DifficultyLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameSandbox");
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
        public void OnClickGameModeThree()
        {
            gameManager.OnClickTestGameModeThree();
        }
        public void OnClickGameModeFour()
        {
            gameManager.OnClickTestGameModeFour();
        }
        public void OnClickGameTestDesign()
        {
            gameManager.OnClickGameTestDesign();
        }
    }
}