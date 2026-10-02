using System;
using UnityEngine;

namespace CosmicShore.Core
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
                // The null guard was already here; empty and non-numeric are the same case
                // and were not. AvatarUrl is a cloud-backed string whose "unset" value is at
                // least as likely to be "" as null, and a FormatException thrown from a
                // property GETTER surfaces at whatever read it - a profile row, a scoreboard,
                // a party slot - rather than anywhere near the bad data. Falls back to the
                // same 1 the null path and this class's own constructor default already use,
                // so every input that worked before is unchanged.
                return int.TryParse(AvatarUrl, out var iconId) ? iconId : 1;
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