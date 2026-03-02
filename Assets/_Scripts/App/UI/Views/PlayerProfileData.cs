using System;
using System.Collections.Generic;

namespace CosmicShore.App.Profile
{
    [Serializable]
    public class PlayerProfileData
    {
        public string userId;
        public string displayName;
        public int avatarId;
        public List<string> unlockedRewardIds = new();
    }
}