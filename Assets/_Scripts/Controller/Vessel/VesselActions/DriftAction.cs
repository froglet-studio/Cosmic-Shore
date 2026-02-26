using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    public class DriftAction : ShipAction
    {
        public override void StartAction()
        {
            Vessel.VesselStatus.VesselTransformer.PitchScaler *= 1.5f;
            Vessel.VesselStatus.VesselTransformer.YawScaler *= 1.5f;
            Vessel.VesselStatus.VesselTransformer.RollScaler *= 1.5f;
            Vessel.VesselStatus.IsDrifting = true;
        }

        public override void StopAction()
        {
            Vessel.VesselStatus.VesselTransformer.PitchScaler /= 1.5f;
            Vessel.VesselStatus.VesselTransformer.YawScaler /= 1.5f;
            Vessel.VesselStatus.VesselTransformer.RollScaler /= 1.5f;
            Vessel.VesselStatus.IsDrifting = false;
        }
    }
}
