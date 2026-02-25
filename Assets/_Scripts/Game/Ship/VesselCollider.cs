using UnityEngine;

namespace CosmicShore.Game.Ship
{
    // DEPRECATED - Use R_ImpactCollider instead.
    public interface IVesselCollider
    {
        IVessel Vessel { get; }
        IVesselStatus VesselStatus => Vessel.VesselStatus;
    }
    
    public class VesselCollider : MonoBehaviour, IVesselCollider
    {
        [SerializeField, RequireInterface(typeof(IVessel))]
        private Object shipObject;
        
        public IVessel Vessel => shipObject as IVessel;
    }
}