using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class ToggleTurretModeAction : ToggleStationaryModeAction
    {
        [SerializeField] int resourceIndex = 0;

        public override void StartAction()
        {
            base.StartAction();
            var resource = Vessel.VesselStatus.ResourceSystem.Resources[resourceIndex];
            resource.resourceGainRate = VesselStatus.IsStationary ? resource.initialResourceGainRate * 2 : resource.initialResourceGainRate;
        }

        public override void StopAction()
        {
        
        }
    }
}
