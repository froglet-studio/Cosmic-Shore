using UnityEngine;

namespace CosmicShore
{
    public class UserProfile
    {
        public string UniqueID => SystemInfo.deviceUniqueIdentifier;
        public string AvatarUrl { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public bool IsNewlyCreated { get; set; }
    }
}
