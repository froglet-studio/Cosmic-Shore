using CosmicShore.Core;
using UnityEngine;

public class ToggleTurretModeAction : ToggleStationaryModeAction
{
    [SerializeField] int resourceIndex = 0;

    public override void StartAction()
    {
        base.StartAction();
        var resource = Ship.ShipStatus.ResourceSystem.Resources[resourceIndex];
        resource.resourceGainRate = ShipStatus.IsStationary ? resource.initialResourceGainRate * 2 : resource.initialResourceGainRate;
    }

    public override void StopAction()
    {
        
    }
}
