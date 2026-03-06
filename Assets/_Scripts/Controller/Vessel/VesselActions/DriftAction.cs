using CosmicShore.Core;
using CosmicShore.Gameplay;
using Reflex.Attributes;

namespace CosmicShore.Gameplay
{
    public class DriftAction : ShipAction
    {
        [Inject] AudioSystem audioSystem;
        public override void StartAction()
        {
            audioSystem.PlayGameplaySFX(GameplaySFXCategory.DriftStart);
            Vessel.VesselStatus.VesselTransformer.PitchScaler *= 1.5f;
            Vessel.VesselStatus.VesselTransformer.YawScaler *= 1.5f;
            Vessel.VesselStatus.VesselTransformer.RollScaler *= 1.5f;
            Vessel.VesselStatus.IsDrifting = true;
        }

        public override void StopAction()
        {
            audioSystem.PlayGameplaySFX(GameplaySFXCategory.DriftEnd);
            Vessel.VesselStatus.VesselTransformer.PitchScaler /= 1.5f;
            Vessel.VesselStatus.VesselTransformer.YawScaler /= 1.5f;
            Vessel.VesselStatus.VesselTransformer.RollScaler /= 1.5f;
            Vessel.VesselStatus.IsDrifting = false;
        }
    }
}
