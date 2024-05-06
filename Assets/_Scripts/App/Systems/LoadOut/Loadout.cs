namespace CosmicShore.App.Systems.Loadout
{
    /// <summary>
    /// Game launch configuration
    /// </summary>
    public struct Loadout
    {
        public int Intensity;
        public int PlayerCount;
        public ShipTypes ShipType;
        public MiniGames GameMode;

        /// <summary>
        /// If all configuration is default, the loadout has never been initialized
        /// </summary>
        public readonly bool Initialized { get => !(Intensity == 0 && PlayerCount == 0 && ShipType == ShipTypes.Random && GameMode == MiniGames.Random); }

        public Loadout(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
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