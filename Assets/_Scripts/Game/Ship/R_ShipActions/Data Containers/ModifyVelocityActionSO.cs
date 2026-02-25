using UnityEngine;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Game.Ship;

namespace CosmicShore.Game.Ship.R_ShipActions.DataContainers
{
    [CreateAssetMenu(fileName = "ModifyForwardVelocityAction", menuName = "ScriptableObjects/Vessel Actions/Modify Forward Velocity")]
    public class ModifyVelocityActionSO
        : ShipActionSO
    {
        [SerializeField] float magnitude;
        [SerializeField] float duration;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            vesselStatus.VesselTransformer.ModifyVelocity(vesselStatus.Vessel.Transform.forward * magnitude, duration);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // No action needed on stop
        }
    
    }
}
