using UnityEngine;

namespace CosmicShore
{
    public class DisableTrailAction : ShipAction
    {
        [SerializeField] float delay = 2f;
        public override void StartAction()
        {
            Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
        }
        
        public override void StopAction()
        {
            Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(delay);
        }
    }
}
