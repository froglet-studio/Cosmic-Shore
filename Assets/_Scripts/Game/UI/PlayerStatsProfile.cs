using System;
using System.Collections.Generic;

namespace CosmicShore.Game.Analytics
{
    [Serializable]
    public class PlayerStatsProfile
    {
        public int TotalGamesPlayed;
        public int TotalPlayAgainPressed;
        public long LastLoginTick;

        public WildlifeBlitzPlayerStatsProfile BlitzStats = new();
        public HexRacePlayerStatsProfile HexRaceStats = new();
        public MultiplayerHexRacePlayerStatsProfile MultiHexStats = new();
    }
}