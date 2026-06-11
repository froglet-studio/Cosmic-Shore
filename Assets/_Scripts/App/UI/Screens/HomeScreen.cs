using CosmicShore.App.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Screens
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene
    /// </summary>
    public class HomeScreen : MonoBehaviour
    {
        [SerializeField] bool DebugFirstAppLaunch = false;
        [SerializeField] GameObject FirstAppLaunchScreen;
        [SerializeField] GameObject NavBar;

        [Header("Profile Display")]
        [SerializeField] private TMP_Text usernameText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private PlayerDataService playerDataService;

        enum PlayerPrefKeys
        {
            FirstAppLaunch
        }

        public void Start()
        {
            CSDebug.Log("MainMenu.cs start");

            if (FirstAppLaunchExperience())
            {
                FirstAppLaunchScreen.SetActive(true);
                NavBar.SetActive(false);
            }

            // Prefer the Inspector reference; fall back to singleton (survives scene reload via DontDestroyOnLoad)
            if (playerDataService == null)
                playerDataService = PlayerDataService.Instance;

            if (playerDataService != null)
            {
                playerDataService.OnProfileChanged += RefreshProfile;

                if (playerDataService.IsInitialized)
                    RefreshProfile(playerDataService.CurrentProfile);
            }
        }

        void OnDestroy()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= RefreshProfile;
        }

        void RefreshProfile(PlayerProfileData profile)
        {
            if (profile == null) return;

            if (usernameText != null)
                usernameText.text = profile.displayName;

            if (avatarImage != null && playerDataService != null)
            {
                var sprite = playerDataService.GetAvatarSprite(profile.avatarId);
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }
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
                CSDebug.Log("MainMenu.cs DebugFirstAppLaunch - delete first app launch key");
            }

            // Implementation commented out until an updated design is available
            /*
            CSDebug.Log("MainMenu.cs first app launch");
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.FirstAppLaunch.ToString()))
            //if (PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString(), -1234) == -1234)
            {
                CSDebug.Log("MainMenu.cs first app launch - did not have key");
                CSDebug.Log("MainMenu.cs - " + PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString()));
                PlayerPrefs.SetInt(PlayerPrefKeys.FirstAppLaunch.ToString(), 1);
                PlayerPrefs.Save();
                CSDebug.Log("MainMenu.cs - " + PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString()));
                if (!PlayerPrefs.HasKey(PlayerPrefKeys.FirstAppLaunch.ToString()))
                    CSDebug.Log("MainMenu.cs first app launch - still did not have fucking key");

                return true;
            }

            return false;
            */

            return false;
        }
    }
}
