// Ported from Assets/_Scripts/System/LoadOut/ArcadeGameLoadout.cs (Arc F 2b) — verbatim.
using CosmicShore.Data;

namespace CosmicShore.Core
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
