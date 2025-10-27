using UnityEngine;

namespace CosmicShore
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
