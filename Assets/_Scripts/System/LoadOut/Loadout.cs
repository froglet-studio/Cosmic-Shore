using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Game launch configuration
    /// </summary>
    public struct Loadout
    {
        public int Intensity;
        public int PlayerCount;
        public VesselClassType VesselType;
        public GameModes GameMode;
        /// <summary>
        /// TOMBSTONE (2026-07-20): the solo/multiplayer split is retired - every game runs
        /// the networked single-host model, so callers now always pass true. Kept only
        /// because the bool is part of the persisted loadout / cloud-save schema
        /// (LoadoutCloudData.IsMultiplayer); do not branch on it.
        /// </summary>
        public bool IsMultiplayer;

        /// <summary>
        /// If all configuration is default, the loadout has never been initialized
        /// </summary>
        public readonly bool Initialized { get => !(Intensity == 0 && PlayerCount == 0 && VesselType == VesselClassType.Random && GameMode == GameModes.Random); }

        public Loadout(int intensity, int playerCount, VesselClassType vesselType, GameModes gameMode, bool isMultiplayer)
        {
            Intensity = intensity;
            PlayerCount = playerCount;
            VesselType = vesselType;
            GameMode = gameMode;
            IsMultiplayer = isMultiplayer;
        }
        public override readonly string ToString()
        {
            return Intensity + "_" + PlayerCount + "_" + VesselType + "_" + GameMode;
        }
    }
}