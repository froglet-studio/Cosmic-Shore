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
            Players[0].defaultShip = SquadSystem.SquadLeader.Ship.Class;
        }
    }
}