using System;

namespace CosmicShore.Integrations.PlayFab.Economy
{
    [Serializable]
    public class DailyRewardInfo
    {
        public int DailyRewardIndex { get; set; }
        public DateTime ClaimedTime { get; set; }
    }
}