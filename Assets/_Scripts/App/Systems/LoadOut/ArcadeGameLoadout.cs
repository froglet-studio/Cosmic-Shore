namespace CosmicShore.App.Systems.Loadout
{
    /// <summary>
    /// Launch information for a specific game type
    /// </summary>
    public struct ArcadeGameLoadout
    {
        public Loadout Loadout;
        public MiniGames GameMode;

        public ArcadeGameLoadout(MiniGames gameMode, Loadout loadout)
        {
            GameMode = gameMode;
            Loadout = loadout;
        }
    }
}