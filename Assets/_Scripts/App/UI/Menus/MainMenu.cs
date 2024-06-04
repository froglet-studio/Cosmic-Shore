using CosmicShore.Core;
using CosmicShore.Game.Arcade;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.App.UI.Menus
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

        public void Start()
        {
            Debug.Log("MainMenu.cs start");

            if (FirstAppLaunchExperience())
            {
                FirstAppLaunchScreen.SetActive(true);
                NavBar.SetActive(false);
            }
        }

        public void OnClickSoar()
        {
            MiniGame.PlayerShipType = ShipTypes.Manta;
            MiniGame.PlayerCaptain = Hangar.Instance.SoarCaptain;
            MiniGame.IntensityLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameFreestyle");
        }
        public void OnClickSmash()
        {
            MiniGame.PlayerShipType = ShipTypes.Rhino;
            MiniGame.PlayerCaptain = Hangar.Instance.SmashCaptain;
            MiniGame.IntensityLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameFreestyle");
        }
        public void OnClickSport()
        {
            MiniGame.PlayerShipType = ShipTypes.Manta;
            MiniGame.PlayerCaptain = Hangar.Instance.SportCaptain;
            MiniGame.IntensityLevel = 1;
            MiniGame.NumberOfPlayers = 1;

            SceneManager.LoadScene("MinigameCellularBrawl2v2");
        }

        /// <summary>
        /// Detect whether the app has been launched in the past by looking for a specific player pref key.
        /// This enables the app to show a special initial app flow to new users. 
        /// 
        /// *Consider replacing this implementation with a quest progression.
        /// </summary>
        /// <returns>True if the app has never been launched before (player pref key doesn't exist). False otherwise.</returns>
        bool FirstAppLaunchExperience()
        {
            

            if (DebugFirstAppLaunch)
            {
                PlayerPrefs.DeleteKey(PlayerPrefKeys.FirstAppLaunch.ToString());
                Debug.Log("MainMenu.cs DebugFirstAppLaunch - delete first app launch key");
            }

            // Implementation commented out until an updated design is available
            /*
            Debug.Log("MainMenu.cs first app launch");
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
            */

            return false;
        }
    }
}