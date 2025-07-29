using System;
using CosmicShore.Core;
using UnityEngine;

public class ToggleStationaryModeAction : ShipAction
{
    public event Action<bool> OnStationaryToggled;
    
    public override void StartAction()
    {
        ShipStatus.IsStationary = !ShipStatus.IsStationary;
        bool isOn = ShipStatus.IsStationary;

        if (isOn)
            Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
        else
            Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);

        OnStationaryToggled?.Invoke(isOn);  
    }

    public override void StopAction()
    {
        
    }
}
