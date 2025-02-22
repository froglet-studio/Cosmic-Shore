using CosmicShore.Core;

public class DetachAction : ShipAction
{
    public override void StartAction()
    {
        if (ShipStatus.Attached)
        {
            ShipStatus.Attached = false;
            ShipStatus.AttachedTrailBlock = null;
        }
    }

    public override void StopAction()
    {
        // Implementing Abstract Method
    }
}