using CosmicShore.Core;

public class DriftAction : ShipAction
{
    public override void StartAction()
    {
        Vessel.VesselStatus.VesselTransformer.PitchScaler *= 1.5f;
        Vessel.VesselStatus.VesselTransformer.YawScaler *= 1.5f;
        Vessel.VesselStatus.VesselTransformer.RollScaler *= 1.5f;
        Vessel.VesselStatus.Drifting = true;
    }

    public override void StopAction()
    {
        Vessel.VesselStatus.VesselTransformer.PitchScaler /= 1.5f;
        Vessel.VesselStatus.VesselTransformer.YawScaler /= 1.5f;
        Vessel.VesselStatus.VesselTransformer.RollScaler /= 1.5f;
        Vessel.VesselStatus.Drifting = false;
    }
}