namespace CosmicShore.App.Systems.Loadout
{
    /// <summary>
    /// Launch information for a specific game type
    /// </summary>
    public struct ArcadeGameLoadout
    {
        public Loadout Loadout;
        public GameModes GameMode;

        public ArcadeGameLoadout(GameModes gameMode, Loadout loadout)
        {
            GameMode = gameMode;
            Loadout = loadout;
        }
    }
}