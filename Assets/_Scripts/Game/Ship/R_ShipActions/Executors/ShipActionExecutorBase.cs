using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Game.Ship
{
    public abstract class ShipActionExecutorBase : MonoBehaviour
    {
        public virtual void Initialize(IVesselStatus shipStatus) { }
    }
}
