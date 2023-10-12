using StarWriter.Core;


public class StopGunsAction : ShipAction
{
    ShipStatus shipData;

    void Start()
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
