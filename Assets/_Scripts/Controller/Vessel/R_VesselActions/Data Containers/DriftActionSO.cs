using CosmicShore.Core;
using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "DriftAction", menuName = "ScriptableObjects/Vessel Actions/Drift")]
    public class DriftActionSO : ShipActionSO
    {
        [SerializeField] float Mult = 1.5f;
        [SerializeField] float driftDamping = 0f;
        [SerializeField] bool isSharpDrifting;

        [SerializeField] ScriptableEventNoParam OnDriftingStarted;
        [SerializeField] ScriptableEventNoParam OnDoubleDriftingStarted;
        [SerializeField] ScriptableEventNoParam OnDriftEnded;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            execs.AudioSystem.PlayGameplaySFX(GameplaySFXCategory.DriftStart);
            var t = vesselStatus.VesselTransformer;
            t.BeginDrift(Mult, driftDamping, isSharpDrifting);
            vesselStatus.IsDrifting = true;

            if (isSharpDrifting)
                OnDoubleDriftingStarted.Raise();
            else
                OnDriftingStarted.Raise();
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            execs.AudioSystem.PlayGameplaySFX(GameplaySFXCategory.DriftEnd);
            var t = vesselStatus.VesselTransformer;
            t.EndDrift(isSharpDrifting);
            vesselStatus.IsDrifting = t.IsDriftActive;
            OnDriftEnded.Raise();
        }
    }
}
