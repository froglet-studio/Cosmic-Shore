using CosmicShore.Core;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    public class DriftAction : ShipAction
    {
        public override void StartAction()
        {
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.DriftStart);
            Vessel.VesselStatus.VesselTransformer.PitchScaler *= 1.5f;
            Vessel.VesselStatus.VesselTransformer.YawScaler *= 1.5f;
            Vessel.VesselStatus.VesselTransformer.RollScaler *= 1.5f;
            Vessel.VesselStatus.IsDrifting = true;
        }

        public override void StopAction()
        {
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.DriftEnd);
            Vessel.VesselStatus.VesselTransformer.PitchScaler /= 1.5f;
            Vessel.VesselStatus.VesselTransformer.YawScaler /= 1.5f;
            Vessel.VesselStatus.VesselTransformer.RollScaler /= 1.5f;
            Vessel.VesselStatus.IsDrifting = false;
        }
    }
}
