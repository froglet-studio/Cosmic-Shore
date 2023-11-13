using CosmicShore.Core;

public class DetachAction : ShipAction
{
    ShipStatus shipData;

    protected override void Start()
    {
        shipData = ship.GetComponent<ShipStatus>();
    }
    public override void StartAction()
    {
        if (shipData.Attached)
        {
            shipData.Attached = false;
            shipData.AttachedTrailBlock = null;
        }
    }

    public override void StopAction()
    {
        // Implementing Abstract Method
    }
}