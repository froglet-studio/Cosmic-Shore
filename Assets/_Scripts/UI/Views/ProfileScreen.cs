using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Profile screen in the menu.
    /// Drives the NamePanel display name text (and avatar, if wired) from PlayerDataService.
    /// </summary>
    public class ProfileScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image avatarImage;

        // Use the persistent singleton rather than [Inject]: it is reliably available from
        // bootstrap (Awake) and avoids both the DI-injection timing gap (OnEnable fires before
        // [Inject] fields are populated) and a NullReferenceException if injection ever fails.
        PlayerDataService Service => PlayerDataService.Instance;

        void Start()
        {
            if (displayNameText == null)
                displayNameText = GetComponentInChildren<TMP_Text>();
        }

        void OnEnable()
        {
            var service = Service;
            if (service == null)
                return;

            service.OnProfileChanged += Refresh;

            if (service.CurrentProfile != null)
                Refresh(service.CurrentProfile);
        }

        void OnDisable()
        {
            var service = Service;
            if (service != null)
                service.OnProfileChanged -= Refresh;
        }

        void Refresh(PlayerProfileData profile)
        {
            if (profile == null) return;

            if (displayNameText != null)
                displayNameText.text = profile.Identity.DisplayName;

            if (avatarImage != null && Service != null)
            {
                var sprite = Service.GetAvatarSprite(profile.Identity.AvatarId);
                if (sprite != null)
                    avatarImage.sprite = sprite;
            }
        }
    }
}
