using System;
using UnityEngine;

namespace CosmicShore
{
    public class PlayerProfile
    {
        public string UniqueID => SystemInfo.deviceUniqueIdentifier;
        public string AvatarUrl { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public bool IsNewlyCreated { get; set; }
        public int ProfileIconId
        {
            get
            {
                if (AvatarUrl == null)
                    return 1;

                return int.Parse(AvatarUrl);
            }
        }

        public const string DefaultPlayerName = "Player";

        public PlayerProfile(string displayName = DefaultPlayerName, string avatarUrl = "1")
        {
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
        }

        public void Update(string displayName, string avatarUrl)
        {
            DisplayName = string.IsNullOrEmpty(displayName) ? DefaultPlayerName : displayName;
            AvatarUrl = avatarUrl;
        }
    }
}