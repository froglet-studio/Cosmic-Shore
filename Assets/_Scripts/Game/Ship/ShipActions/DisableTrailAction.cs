using UnityEngine;

namespace CosmicShore
{
    public class DisableTrailAction : ShipAction
    {
        [SerializeField] float delay = 2f;
        public override void StartAction()
        {
            Ship.TrailSpawner.PauseTrailSpawner();
        }
        
        public override void StopAction()
        {
            Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(delay);
        }
    }
}
