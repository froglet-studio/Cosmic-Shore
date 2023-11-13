using CosmicShore.App.Systems.Squads;
using CosmicShore.Core.HangerBuilder;

namespace CosmicShore.Game.Arcade
{
    public class CellularBrawlMiniGame : MiniGame
    {
        protected override void Start()
        {
            base.Start();
            Hangar.Instance.SetPlayerVessel(SquadSystem.SquadLeader);
            Players[0].defaultShip = SquadSystem.SquadLeader.Ship.Class;
        }
    }
}