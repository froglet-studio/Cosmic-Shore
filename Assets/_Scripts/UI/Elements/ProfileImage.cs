using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(Image))]
    public class ProfileImage : MonoBehaviour
    {
        [SerializeField] SO_ProfileIconList ProfileIcons;

        [Inject] PlayerDataService playerDataService;

        Image _image;

        void Awake()
        {
            _image = GetComponent<Image>();
        }

        void Start()
        {
            if (playerDataService == null) return;

            playerDataService.OnProfileChanged += OnProfileChanged;

            if (playerDataService.CurrentProfile != null)
                ApplySprite(playerDataService.CurrentProfile.avatarId);
        }

        void OnDestroy()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnProfileChanged;
        }

        void OnProfileChanged(PlayerProfileData profile)
        {
            if (profile == null) return;
            ApplySprite(profile.avatarId);
        }

        void ApplySprite(int avatarId)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (ProfileIcons == null || ProfileIcons.profileIcons == null) return;

            Sprite sprite = null;
            foreach (var icon in ProfileIcons.profileIcons)
            {
                if (icon.Id == avatarId)
                {
                    sprite = icon.IconSprite;
                    break;
                }
            }
            if (sprite == null && ProfileIcons.profileIcons.Count > 0)
                sprite = ProfileIcons.profileIcons[0].IconSprite;

            _image.sprite = sprite;
            _image.enabled = sprite != null;

            CSDebug.Log($"ProfileImage - applied sprite for avatarId={avatarId}");
        }
    }
}
