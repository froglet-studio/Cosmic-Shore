using System.Linq;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    [RequireComponent (typeof (Image))]
    public class ProfileImage : MonoBehaviour
    {
        // Optional local fallback. The avatar sprite is normally resolved through
        // PlayerDataService (the live UGS profile source); this list is only used if
        // the service is unavailable.
        [SerializeField] SO_ProfileIconList ProfileIcons;

        Image _image;
        bool _subscribed;

        void Awake()
        {
            _image = GetComponent<Image>();
        }

        void OnEnable()
        {
            TryBind();
        }

        void Start()
        {
            // OnEnable can run before the PlayerDataService singleton exists during bootstrap;
            // retry here so the avatar always ends up bound to live profile changes.
            TryBind();
        }

        void TryBind()
        {
            // PlayerDataService is a DontDestroyOnLoad singleton created during bootstrap.
            var service = PlayerDataService.Instance;
            if (service != null && !_subscribed)
            {
                service.OnProfileChanged += OnProfileChanged;
                _subscribed = true;
            }

            Refresh();
        }

        void OnDisable()
        {
            var service = PlayerDataService.Instance;
            if (service != null && _subscribed)
                service.OnProfileChanged -= OnProfileChanged;
            _subscribed = false;
        }

        void OnProfileChanged(PlayerProfileData profile)
        {
            Refresh();
        }

        void Refresh()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            var sprite = GetProfileImage();
            if (sprite != null)
                _image.sprite = sprite;
        }

        public Sprite GetProfileImage()
        {
            var service = PlayerDataService.Instance;
            if (service != null && service.CurrentProfile != null)
                return service.GetAvatarSprite(service.CurrentProfile.Identity.AvatarId);

            // Fallback: resolve avatar 0 from the local list if the service isn't ready.
            if (ProfileIcons != null && ProfileIcons.profileIcons is { Count: > 0 })
                return ProfileIcons.profileIcons.First().IconSprite;

            return null;
        }
    }
}
