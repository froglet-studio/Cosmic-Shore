using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Profile screen in the menu.
    /// Drives the NamePanel display name text from PlayerDataService.
    /// </summary>
    public class ProfileScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayNameText;

        [Inject] private PlayerDataService playerDataService;

        void Start()
        {
            if (displayNameText == null)
                displayNameText = GetComponentInChildren<TMP_Text>();

            playerDataService.OnProfileChanged += Refresh;

            if (playerDataService.CurrentProfile != null)
                Refresh(playerDataService.CurrentProfile);
        }

        void OnDisable()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= Refresh;
        }

        void Refresh(PlayerProfileData profile)
        {
            if (profile == null) return;

            if (displayNameText != null)
                displayNameText.text = profile.displayName;
        }
    }
}
