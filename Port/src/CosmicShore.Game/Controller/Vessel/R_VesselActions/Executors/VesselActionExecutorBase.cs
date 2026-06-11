using CosmicShore.Gameplay;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public abstract class ShipActionExecutorBase : MonoBehaviour
    {
        public virtual void Initialize(IVesselStatus shipStatus) { }
    }
}
