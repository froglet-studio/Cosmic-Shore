using System.Collections.Generic;
using CosmicShore.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for account identity, economy, progression and lifecycle facts.
    /// Cloud key: "PLAYER_PROFILE"
    /// </summary>
    public sealed class PlayerProfileRepository : CloudDataRepository<PlayerProfileData>
    {
        public override string CloudKey => UGSKeys.PlayerProfile;

        public PlayerProfileRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(PlayerProfileData data)
        {
            data.Identity ??= new ProfileIdentity();
            data.Economy ??= new ProfileEconomy();
            data.Lifecycle ??= new ProfileLifecycle();

            data.Economy.UnlockedRewardIds ??= new List<string>();
        }
    }
}
