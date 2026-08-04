using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
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
