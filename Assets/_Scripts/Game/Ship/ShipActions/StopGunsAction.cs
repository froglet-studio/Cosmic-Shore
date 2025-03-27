using CosmicShore.Core;

public class StopGunsAction : ShipAction
{
    public override void StartAction()
    {
        ShipStatus.GunsActive = false;
    }

    public override void StopAction()
    {
    }
}