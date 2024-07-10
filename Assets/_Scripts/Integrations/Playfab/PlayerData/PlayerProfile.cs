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
        public int ProfileIconId { get => Int32.Parse(AvatarUrl); }

        public PlayerProfile(string displayName, string avatarUrl)
        {
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
        }
    }
}
