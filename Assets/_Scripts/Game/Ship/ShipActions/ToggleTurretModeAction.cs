using StarWriter.Core;

public class ToggleTurretModeAction : ShipAction
{
    ShipStatus shipData;

    protected override void Start()
    {
        shipData = ship.ShipStatus;
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
