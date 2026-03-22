using CosmicShore.App.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
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

        void OnEnable()
        {
            var service = PlayerDataService.Instance;
            if (service != null)
            {
                service.OnProfileChanged += RefreshUI;

                if (service.CurrentProfile != null)
                    RefreshUI(service.CurrentProfile);
            }
        }

        void OnDisable()
        {
            var service = PlayerDataService.Instance;
            if (service != null)
                service.OnProfileChanged -= RefreshUI;
        }

        void RefreshUI(PlayerProfileData data)
        {
            if (displayNameText != null)
                displayNameText.text = data.displayName;

            if (avatarImage != null)
            {
                var service = PlayerDataService.Instance;
                var sprite = service != null ? service.GetAvatarSprite(data.avatarId) : null;
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }
        }
    }
}
