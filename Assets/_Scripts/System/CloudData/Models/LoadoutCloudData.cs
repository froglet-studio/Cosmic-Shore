using System;
using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists the player's loadouts to UGS Cloud Save: the saved player loadout
    /// slots, per-game last-used configs, and the active slot index.
    /// Mirror of the local <c>Loadout</c> / <c>ArcadeGameLoadout</c> structs.
    /// Cloud key: "LOADOUT_DATA".
    /// </summary>
    [Serializable]
    public class LoadoutCloudData
    {
        public List<LoadoutEntry> PlayerLoadouts = new();
        public List<GameLoadoutEntry> GameLoadouts = new();
        public int ActiveLoadoutIndex;
    }

    [Serializable]
    public class LoadoutEntry
    {
        public int Intensity;
        public int PlayerCount;
        public VesselClassType VesselType;
        public GameModes GameMode;
        public bool IsMultiplayer;
    }

    [Serializable]
    public class GameLoadoutEntry
    {
        public GameModes GameMode;
        public LoadoutEntry Loadout = new();
    }
}
