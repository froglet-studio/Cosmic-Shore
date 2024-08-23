using System;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace CosmicShore
{
    public class PlayerProfile
    {
        public string UniqueID => SystemInfo.deviceUniqueIdentifier;
        public string AvatarUrl { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public bool IsNewlyCreated { get; set; }
        public int ProfileIconId => int.Parse(AvatarUrl);

        public PlayerProfile(string displayName = "Player", string avatarUrl = "1")
        {
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
        }

        public void Update(string displayName, string avatarUrl)
        {
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
        }
    }
}
