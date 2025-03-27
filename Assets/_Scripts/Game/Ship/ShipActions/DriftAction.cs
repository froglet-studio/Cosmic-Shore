using CosmicShore.Core;

public class DriftAction : ShipAction
{
    public override void StartAction()
    {
        Ship.ShipStatus.ShipTransformer.PitchScaler *= 1.5f;
        Ship.ShipStatus.ShipTransformer.YawScaler *= 1.5f;
        Ship.ShipStatus.ShipTransformer.RollScaler *= 1.5f;
        ShipStatus.Drifting = true;
    }

    public override void StopAction()
    {
        Ship.ShipStatus.ShipTransformer.PitchScaler /= 1.5f;
        Ship.ShipStatus.ShipTransformer.YawScaler /= 1.5f;
        Ship.ShipStatus.ShipTransformer.RollScaler /= 1.5f;
        ShipStatus.Drifting = false;
    }
}