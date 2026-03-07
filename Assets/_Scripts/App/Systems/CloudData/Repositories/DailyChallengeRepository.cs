using System.Collections.Generic;
using CosmicShore.App.Systems.CloudData.Models;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for daily challenge state (replaces PlayerPrefs storage).
    /// Cloud key: "DAILY_CHALLENGE"
    /// </summary>
    public sealed class DailyChallengeRepository : CloudDataRepository<DailyChallengeCloudData>
    {
        public override string CloudKey => UGSKeys.DailyChallenge;

        public DailyChallengeRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(DailyChallengeCloudData data)
        {
            data.RewardTiers ??= new List<RewardTierState> { new(), new(), new() };
        }
    }
}
