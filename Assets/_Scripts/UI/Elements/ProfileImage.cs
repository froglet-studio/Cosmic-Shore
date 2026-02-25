using CosmicShore.Integrations.PlayFab.PlayerData;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore
{
    [RequireComponent (typeof (Image))]
    public class ProfileImage : MonoBehaviour
    {
        [SerializeField] SO_ProfileIconList ProfileIcons;

        void OnEnable()
        {
            PlayerDataController.OnProfileLoaded += SetSprite;
            PlayerDataController.OnPlayerAvatarUpdated += SetSprite;
        }

        void OnDisable()
        {
            PlayerDataController.OnProfileLoaded -= SetSprite;
            PlayerDataController.OnPlayerAvatarUpdated -= SetSprite;
        }

        void Start()
        {
            SetSprite();
        }

        void SetSprite()
        {
            var image = GetComponent<Image>();
            image.sprite = GetProfileImage();
        }

        public Sprite GetProfileImage()
        {
            var profileIconId = PlayerDataController.PlayerProfile.ProfileIconId;
            CSDebug.Log($"ProfileImage - GetProfileImage - {profileIconId}");
            return ProfileIcons.profileIcons.FirstOrDefault(x => x.Id == profileIconId).IconSprite;
        }
    }
}