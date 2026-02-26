using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// UI component that displays the player's profile (display name + avatar).
    /// Place on a UI GameObject in the menu scene and wire the TMP_Text / Image references.
    /// Listens to PlayerDataService.OnProfileChanged to stay up-to-date.
    /// </summary>
    public class ProfileScreen : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image avatarImage;

        [Inject] private PlayerDataService playerDataService;

        void OnEnable()
        {
            if (playerDataService != null)
            {
                playerDataService.OnProfileChanged += RefreshUI;

                if (playerDataService.CurrentProfile != null)
                    RefreshUI(playerDataService.CurrentProfile);
            }
        }

        void OnDisable()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= RefreshUI;
        }

        void RefreshUI(PlayerProfileData data)
        {
            if (displayNameText != null)
                displayNameText.text = data.displayName;

            if (avatarImage != null)
            {
                var sprite = playerDataService != null ? playerDataService.GetAvatarSprite(data.avatarId) : null;
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }
        }
    }
}
