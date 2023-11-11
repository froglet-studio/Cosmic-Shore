using CosmicShore.Core;

public class StopGunsAction : ShipAction
{
    ShipStatus shipData;

    protected override void Start()
    {
        shipData = ship.GetComponent<ShipStatus>();
    }
    public override void StartAction()
    {
        shipData.GunsActive = false;
    }

    public override void StopAction()
    {
    }
}