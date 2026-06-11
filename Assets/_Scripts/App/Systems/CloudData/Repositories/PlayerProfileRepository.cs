using System.Collections.Generic;
using CosmicShore.App.Profile;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for player profile data (display name, avatar, crystals, rewards).
    /// Cloud key: "player_profile"
    /// </summary>
    public sealed class PlayerProfileRepository : CloudDataRepository<PlayerProfileData>
    {
        public override string CloudKey => UGSKeys.PlayerProfile;

        public PlayerProfileRepository(ICloudSaveProvider provider) : base(provider) { }

        protected override void OnAfterLoad(PlayerProfileData data)
        {
            data.unlockedRewardIds ??= new List<string>();
        }
    }
}
