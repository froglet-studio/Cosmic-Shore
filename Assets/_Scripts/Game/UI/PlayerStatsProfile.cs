using System;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class PlayerStatsProfile
    {
        public int TotalGamesPlayed;
        public int TotalPlayAgainPressed;
        public long LastLoginTick;

        public WildlifeBlitzPlayerStatsProfile BlitzStats = new();
        public HexRacePlayerStatsProfile MultiHexStats = new();
        public JoustPlayerStatsProfile JoustStats = new();
        public CrystalCapturePlayerStatsProfile CrystalCaptureStats = new();
    }
}