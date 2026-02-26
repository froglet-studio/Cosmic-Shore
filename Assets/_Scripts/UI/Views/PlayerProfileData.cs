using System;
using System.Collections.Generic;

namespace CosmicShore.UI
{
    [Serializable]
    public class PlayerProfileData
    {
        public string userId;
        public string displayName;
        public int avatarId;
        public int xp;
        public List<string> unlockedRewardIds = new();
    }
}