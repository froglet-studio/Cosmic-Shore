using CosmicShore.Core;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.UI
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