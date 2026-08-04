using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public abstract class ShipActionExecutorBase : MonoBehaviour
    {
        public virtual void Initialize(IVesselStatus shipStatus) { }
    }
}
