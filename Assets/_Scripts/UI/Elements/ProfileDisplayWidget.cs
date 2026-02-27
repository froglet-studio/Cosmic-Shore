using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Reusable UI widget that displays a player's display name and avatar.
    /// Place on any UI GameObject and wire the TMP_Text / Image references.
    /// Automatically subscribes to PlayerDataService.OnProfileChanged.
    /// </summary>
    public class ProfileDisplayWidget : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image avatarImage;

        [Inject] private PlayerDataService playerDataService;

        void OnEnable() => SubscribeAndRefresh();
        void Start() => SubscribeAndRefresh();

        void OnDisable()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= Refresh;
        }

        void SubscribeAndRefresh()
        {
            if (playerDataService == null) return;

            playerDataService.OnProfileChanged -= Refresh;
            playerDataService.OnProfileChanged += Refresh;

            if (playerDataService.CurrentProfile != null)
                Refresh(playerDataService.CurrentProfile);
        }

        void Refresh(PlayerProfileData profile)
        {
            if (profile == null) return;

            if (displayNameText != null)
                displayNameText.text = profile.displayName;

            if (avatarImage != null)
            {
                var sprite = playerDataService != null
                    ? playerDataService.GetAvatarSprite(profile.avatarId)
                    : null;
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }
        }
    }
}
