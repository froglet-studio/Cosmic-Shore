using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.UI;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Repository for per-game-mode high scores and stats.
    /// Cloud key: "PLAYER_STATS_PROFILE"
    /// </summary>
    public sealed class PlayerStatsRepository : CloudDataRepository<PlayerStatsProfile>
    {
        public override string CloudKey => UGSKeys.PlayerStatsProfile;

        public PlayerStatsRepository(ICloudSaveProvider provider) : base(provider, 2f) { }

        protected override void OnAfterLoad(PlayerStatsProfile data)
        {
            data.BlitzStats ??= new WildlifeBlitzPlayerStatsProfile();
            data.MultiHexStats ??= new HexRacePlayerStatsProfile();
            data.JoustStats ??= new JoustPlayerStatsProfile();
            data.CrystalCaptureStats ??= new CrystalCapturePlayerStatsProfile();
        }
    }
}
