using CosmicShore.Core;
using UnityEngine;

public class ToggleStationaryModeAction : ShipAction
{
    public override void StartAction()
    {
        ShipStatus.Stationary = !ShipStatus.Stationary;
        if (ShipStatus.Stationary) Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
        else Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
    }

    public override void StopAction()
    {
        
    }
}
