using CosmicShore.Core;

public class DriftAction : ShipAction
{
    ShipStatus shipData;

    protected override void Start()
    {
        base.Start();
        shipData = Ship.ShipStatus;
    }
    public override void StartAction()
    {
        Ship.ShipTransformer.PitchScaler *= 1.5f;
        Ship.ShipTransformer.YawScaler *= 1.5f;
        Ship.ShipTransformer.RollScaler *= 1.5f;
        shipData.Drifting = true;
    }

    public override void StopAction()
    {
        Ship.ShipTransformer.PitchScaler /= 1.5f;
        Ship.ShipTransformer.YawScaler /= 1.5f;
        Ship.ShipTransformer.RollScaler /= 1.5f;
        shipData.Drifting = false;
    }
}