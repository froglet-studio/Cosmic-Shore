using StarWriter.Core;


public class StopGunsAction : ShipActionAbstractBase
{
    ShipData shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipData>();
    }
    public override void StartAction()
    {
        shipData.GunsActive = false;
    }

    public override void StopAction()
    {
    }
}
