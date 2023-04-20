using StarWriter.Core;

public class ChargeBoostAction : ShipActionAbstractBase
{
    ShipData shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipData>();
    }
    public override void StartAction()
    {
        shipData.BoostCharging = true;
    }

    public override void StopAction()
    {
        shipData.BoostCharging = false;
    }
}