namespace CosmicShore.App.Systems.Loadout
{
    public struct Loadout
    {
        public int Intensity;
        public int PlayerCount;
        public ShipTypes ShipType;
        public MiniGames GameMode;

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

        public readonly bool Uninitialized()
        {
            return Intensity == 0 && PlayerCount == 0 && ShipType == ShipTypes.Random && GameMode == MiniGames.Random;
        }
    }
}