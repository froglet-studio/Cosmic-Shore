namespace CosmicShore.App.Systems.Loadout
{
    /// <summary>
    /// Game launch configuration
    /// </summary>
    public struct Loadout
    {
        public int Intensity;
        public int PlayerCount;
        public ShipClassType ShipType;
        public GameModes GameMode;

        /// <summary>
        /// If all configuration is default, the loadout has never been initialized
        /// </summary>
        public readonly bool Initialized { get => !(Intensity == 0 && PlayerCount == 0 && ShipType == ShipClassType.Random && GameMode == GameModes.Random); }

        public Loadout(int intensity, int playerCount, ShipClassType shipType, GameModes gameMode)
        {
            Intensity = intensity;
            PlayerCount = playerCount;
            ShipType = shipType;
            GameMode = gameMode;
        }
        public override readonly string ToString()
        {
            return Intensity + "_" + PlayerCount + "_" + ShipType + "_" + GameMode;
        }
    }
}