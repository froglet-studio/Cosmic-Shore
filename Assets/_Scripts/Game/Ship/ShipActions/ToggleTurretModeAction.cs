using CosmicShore.Core;
using UnityEngine;

public class ToggleTurretModeAction : ShipAction
{
    [SerializeField] int resourceIndex = 0;

    public override void StartAction()
    {
        ShipStatus.Stationary = !ShipStatus.Stationary;
        var resource = Ship.ShipStatus.ResourceSystem.Resources[resourceIndex];
        resource.resourceGainRate = ShipStatus.Stationary ? resource.initialResourceGainRate * 2 : resource.initialResourceGainRate;
        if (ShipStatus.Stationary) Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
        else Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
    }

    public override void StopAction()
    {
        
    }
}
