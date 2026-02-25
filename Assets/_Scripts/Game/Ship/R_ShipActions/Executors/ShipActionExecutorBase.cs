using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Game.Ship.R_ShipActions.Executors
{
    public abstract class ShipActionExecutorBase : MonoBehaviour
    {
        public virtual void Initialize(IVesselStatus shipStatus) { }
    }
}
