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
        public int crystalBalance;
        public int xp;
        public List<string> unlockedRewardIds = new();

        // Unix epoch milliseconds (UTC) of first profile creation on this account.
        // Set once; used for install-relative cohorting / retention analysis.
        public long firstSeenUtc;
    }
}
