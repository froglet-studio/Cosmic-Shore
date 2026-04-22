using CosmicShore.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Repository for player settings/preferences (roams across devices).
    /// Cloud key: "PLAYER_SETTINGS"
    /// </summary>
    public sealed class PlayerSettingsRepository : CloudDataRepository<PlayerSettingsCloudData>
    {
        public override string CloudKey => UGSKeys.PlayerSettings;

        public PlayerSettingsRepository(ICloudSaveProvider provider) : base(provider) { }
    }
}
