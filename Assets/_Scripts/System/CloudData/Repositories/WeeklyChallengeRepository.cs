using System.Collections.Generic;
using CosmicShore.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for weekly challenge state (replaces PlayerPrefs storage).
    /// Cloud key: "WEEKLY_CHALLENGE"
    /// </summary>
    public sealed class WeeklyChallengeRepository : CloudDataRepository<WeeklyChallengeCloudData>
    {
        public override string CloudKey => UGSKeys.WeeklyChallenge;

        public WeeklyChallengeRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(WeeklyChallengeCloudData data)
        {
            data.RewardTiers ??= new List<RewardTierState> { new(), new(), new() };
        }
    }
}
