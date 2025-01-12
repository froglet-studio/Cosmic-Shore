using CosmicShore.Core;
using UnityEngine;

public class ToggleTurretModeAction : ShipAction
{
    ShipStatus shipData;
    [SerializeField] int resourceIndex = 0;

    protected override void Start()
    {
        shipData = Ship.ShipStatus;
    }

    public override void StartAction()
    {
        shipData.Stationary = !shipData.Stationary;
        var resource = Ship.ResourceSystem.Resources[resourceIndex];
        resource.resourceGainRate = shipData.Stationary ? resource.initialResourceGainRate * 2 : resource.initialResourceGainRate;
        if (shipData.Stationary) Ship.TrailSpawner.PauseTrailSpawner();
        else Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
    }

    public override void StopAction()
    {
        
    }
}
