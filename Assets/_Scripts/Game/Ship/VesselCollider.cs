using UnityEngine;

namespace CosmicShore.Game
{
    // DEPRECATED - Use R_ImpactCollider instead.
    public interface IVesselCollider
    {
        IShip Ship { get; }
        IShipStatus ShipStatus => Ship.ShipStatus;
    }
    
    public class VesselCollider : MonoBehaviour, IVesselCollider
    {
        [SerializeField, RequireInterface(typeof(IShip))]
        private Object shipObject;
        
        public IShip Ship => shipObject as IShip;
    }
}