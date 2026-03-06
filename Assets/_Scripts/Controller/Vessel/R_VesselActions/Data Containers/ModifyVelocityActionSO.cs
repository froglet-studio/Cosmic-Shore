using CosmicShore.Core;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ModifyForwardVelocityAction", menuName = "ScriptableObjects/Vessel Actions/Modify Forward Velocity")]
    public class ModifyVelocityActionSO
        : ShipActionSO
    {
        [SerializeField] float magnitude;
        [SerializeField] float duration;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            execs.AudioSystem.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);
            vesselStatus.VesselTransformer.ModifyVelocity(vesselStatus.Vessel.Transform.forward * magnitude, duration);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // No action needed on stop
        }
    
    }
}
