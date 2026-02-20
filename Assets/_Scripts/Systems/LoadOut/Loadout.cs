namespace CosmicShore.Systems.Loadout
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