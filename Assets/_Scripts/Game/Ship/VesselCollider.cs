using UnityEngine;

namespace CosmicShore.Game
{
    public interface IVesselCollider
    {
        IShip Ship { get; }
    }
    
    public class VesselCollider : MonoBehaviour, IVesselCollider
    {
        [SerializeField, RequireInterface(typeof(IShip))]
        private Object shipObject;
        
        public IShip Ship => shipObject as IShip;
    }
}