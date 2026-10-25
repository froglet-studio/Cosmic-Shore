using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.UI
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene.
    /// Player name and avatar display driven by PlayerDataService.OnProfileChanged.
    /// </summary>
    public class HomeScreen : MonoBehaviour
    {
        [SerializeField] bool DebugFirstAppLaunch = false;
        [SerializeField] GameObject FirstAppLaunchScreen;
        [SerializeField] GameObject NavBar;
        [SerializeField] TMP_Text userNameText;
        [SerializeField] Image avatarImage;

        [Inject] private PlayerDataService playerDataService;

        enum PlayerPrefKeys
        {
            FirstAppLaunch = 0
        }

        bool _subscribed;

        // Subscribed on enable and dropped on disable, so a Home disabled and re-enabled (the
        // freestyle round trip, a scene reload) keeps following the profile. It used to subscribe
        // once in Start and unsubscribe in OnDisable, which left it deaf after the first disable.
        void OnEnable() => Subscribe();

        void Subscribe()
        {
            if (_subscribed || playerDataService == null) return;
            playerDataService.OnProfileChanged += OnProfileChanged;
            _subscribed = true;
            if (playerDataService.CurrentProfile != null)
                OnProfileChanged(playerDataService.CurrentProfile);
        }

        public void Start()
        {
            // Injection can land after OnEnable on the first frame.
            Subscribe();

            if (FirstAppLaunchExperience())
            {
                FirstAppLaunchScreen.SetActive(true);
                NavBar.SetActive(false);
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
            }

            // Implementation commented out until an updated design is available
            /*
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.FirstAppLaunch.ToString()))
            //if (PlayerPrefs.GetInt(PlayerPrefKeys.FirstAppLaunch.ToString(), -1234) == -1234)
            {
                PlayerPrefs.SetInt(PlayerPrefKeys.FirstAppLaunch.ToString(), 1);
                PlayerPrefs.Save();

                return true;
            }

            return false;
            */

            return false;
        }

        void OnProfileChanged(PlayerProfileData profile)
        {
            if (profile == null)
                return;

            // Use the refactored Identity shape if present (incoming branch); keep avatar logic from ours
            if (userNameText != null && profile.Identity != null)
                userNameText.text = profile.Identity.DisplayName;

            if (avatarImage != null && playerDataService != null && profile.Identity != null)
            {
                var sprite = playerDataService.GetAvatarSprite(profile.Identity.AvatarId);
                if (sprite != null)
                    avatarImage.sprite = sprite;
            }
        }

        void OnDisable()
        {
            if (_subscribed && playerDataService != null)
                playerDataService.OnProfileChanged -= OnProfileChanged;
            _subscribed = false;
        }
    }
}
