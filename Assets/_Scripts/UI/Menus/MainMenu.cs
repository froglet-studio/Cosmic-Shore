using StarWriter.Core.HangerBuilder;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarWriter.Core.UI
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] bool DebugFirstAppLaunch = false;
        [SerializeField] GameObject FirstAppLaunchScreen;
        [SerializeField] GameObject NavBar;

        enum PlayerPrefKeys
        {
            FirstAppLaunch
        }

        GameManager gameManager;

        public void Start()
        {
            Debug.Log("MainMenu.cs start");
            gameManager = GameManager.Instance;

            if (FirstAppLaunchExperience())
            {
                FirstAppLaunchScreen.SetActive(true);
                NavBar.SetActive(false);
            }
        }

        public void OnClickSoar()
        {
            MiniGame.PlayerShipType = ShipTypes.Manta;
            MiniGame.PlayerPilot = Hangar.Instance.SoarPilot;
            MiniGame.IntensityLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameFreestyle");
        }
        public void OnClickSmash()
        {
            MiniGame.PlayerShipType = ShipTypes.Rhino;
            MiniGame.PlayerPilot = Hangar.Instance.SmashPilot;
            MiniGame.IntensityLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameFreestyle");
        }
        public void OnClickSport()
        {
            MiniGame.PlayerShipType = ShipTypes.Manta;
            MiniGame.PlayerPilot = Hangar.Instance.SportPilot;
            MiniGame.IntensityLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameCellularBrawl2v2");
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

        bool FirstAppLaunchExperience()
        {
            Debug.Log("MainMenu.cs first app launch");
            if (DebugFirstAppLaunch)
            {
                PlayerPrefs.DeleteKey(PlayerPrefKeys.FirstAppLaunch.ToString());
                Debug.Log("MainMenu.cs DebugFirstAppLaunch - delete first app launch key");
            }

            if (!PlayerPrefs.HasKey(PlayerPrefKeys.FirstAppLaunch.ToString()))
            //if (PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString(), -1234) == -1234)
            {
                Debug.Log("MainMenu.cs first app launch - did not have key");
                Debug.Log("MainMenu.cs - " + PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString()));
                PlayerPrefs.SetInt(PlayerPrefKeys.FirstAppLaunch.ToString(), 1);
                PlayerPrefs.Save();
                Debug.Log("MainMenu.cs - " + PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString()));
                if (!PlayerPrefs.HasKey(PlayerPrefKeys.FirstAppLaunch.ToString()))
                    Debug.Log("MainMenu.cs first app launch - still did not have fucking key");

                return true;
            }

            return false;
        }
    }
}