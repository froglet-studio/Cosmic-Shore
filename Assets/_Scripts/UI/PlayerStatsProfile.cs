using System;

namespace CosmicShore.UI
{
    [Serializable]
    public class PlayerStatsProfile
    {
        public long LastLoginTick;

        public HexRacePlayerStatsProfile MultiHexStats = new();
        public JoustPlayerStatsProfile JoustStats = new();
        public CrystalCapturePlayerStatsProfile CrystalCaptureStats = new();
    }
}
