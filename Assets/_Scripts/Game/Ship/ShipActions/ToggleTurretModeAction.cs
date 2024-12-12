using CosmicShore.Core;
using UnityEngine;

public class ToggleTurretModeAction : ShipAction
{
    ShipStatus shipData;
    [SerializeField] int resourceIndex = 0;

    protected override void Start()
    {
        shipData = ship.ShipStatus;
    }

    public override void StartAction()
    {
        shipData.Stationary = !shipData.Stationary;
        var resource = ship.ResourceSystem.Resources[resourceIndex];
        resource.resourceGainRate = shipData.Stationary ? resource.initialResourceGainRate * 2 : resource.initialResourceGainRate;
        if (shipData.Stationary) ship.TrailSpawner.PauseTrailSpawner();
        else ship.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
    }

    public override void StopAction()
    {
        
    }
}
