using CosmicShore.App.Systems.Squads;
using CosmicShore.Core;

namespace CosmicShore.Game.Arcade
{
    public class CellularBrawlMiniGame : MiniGame
    {
        protected override void Start()
        {
            base.Start();
            Hangar.Instance.SetPlayerGuide(SquadSystem.SquadLeader);
            Players[0].defaultShip = SquadSystem.SquadLeader.Ship.Class;
        }
    }
}