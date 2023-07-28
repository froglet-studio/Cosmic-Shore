using StarWriter.Core;
using StarWriter.Core.IO;
public class ToggleTurretModeAction : ShipActionAbstractBase
{
    ShipStatus shipData;

    void Start()
    {
        shipData = ship.ShipData;
    }

    public override void StartAction()
    {
        shipData.Stationary = !shipData.Stationary;
        shipData.ElevatedAmmoGain = shipData.Stationary;
        if (shipData.Stationary) ship.TrailSpawner.PauseTrailSpawner();
        else ship.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
    }

    public override void StopAction()
    {
        
    }
}
