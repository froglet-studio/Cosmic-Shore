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