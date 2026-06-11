using System;

namespace CosmicShore.UI
{
    [Serializable]
    public class PlayerStatsProfile
    {
        public long LastLoginTick;

        public WildlifeBlitzPlayerStatsProfile BlitzStats = new();
        public HexRacePlayerStatsProfile MultiHexStats = new();
        public JoustPlayerStatsProfile JoustStats = new();
        public CrystalCapturePlayerStatsProfile CrystalCaptureStats = new();
    }
}
