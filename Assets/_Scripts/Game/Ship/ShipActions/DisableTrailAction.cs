using UnityEngine;

namespace CosmicShore
{
    public class DisableTrailAction : ShipAction
    {
        [SerializeField] float delay = 2f;
        public override void StartAction()
        {
            Vessel.VesselStatus.PrismSpawner.PauseTrailSpawner();
        }
        
        public override void StopAction()
        {
            Vessel.VesselStatus.PrismSpawner.RestartTrailSpawnerAfterDelay(delay);
        }
    }
}
