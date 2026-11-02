using CosmicShore.Core;
using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "BoostAction", menuName = "ScriptableObjects/Vessel Actions/Boost")]
    public class BoostActionSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus == null) return;
            // Held, not a one-shot: Boost Activate loops (see HeldVesselSfx). This asset is shared
            // by every vessel, so the vessel itself owns the sound.
            HeldVesselSfx.Begin(execs ? execs.AudioSystem : null, GameplaySFXCategory.BoostActivate, SoundOwner(vesselStatus), vesselStatus);
            vesselStatus.IsBoosting = true;
            vesselStatus.IsStationary = false;
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus == null) return;
            HeldVesselSfx.End(execs ? execs.AudioSystem : null, GameplaySFXCategory.BoostActivate, SoundOwner(vesselStatus));
            vesselStatus.IsBoosting = false;
        }

        static Object SoundOwner(IVesselStatus vesselStatus)
            => vesselStatus.Vessel is Object vessel ? vessel : null;
    }
}
