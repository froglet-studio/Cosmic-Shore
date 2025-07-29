using CosmicShore.App.Systems.Squads;
using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.Economy;

namespace CosmicShore.Game.Arcade
{
    public class CellularBrawlMiniGame : MiniGame
    {
        protected override void Start()
        {
            base.Start();
            Hangar.Instance.SetPlayerCaptain(CaptainManager.Instance.GetCaptainByName(SquadSystem.SquadLeader.Name));

            // TODO - Cannot modify player datas directly... need other way of initialization.
            // Players[0].ShipType = SquadSystem.SquadLeader.Ship.Class;
        }
    }
}