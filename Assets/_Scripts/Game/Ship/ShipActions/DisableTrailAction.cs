using UnityEngine;
using CosmicShore.Game.Ship;
namespace CosmicShore.Game.Ship.ShipActions
{
    public class DisableTrailAction : ShipAction
    {
        public override void StartAction()
        {
            Vessel.VesselStatus.VesselPrismController.StopSpawn();
        }
        
        public override void StopAction()
        {
            Vessel.VesselStatus.VesselPrismController.StartSpawn();
        }
    }
}
