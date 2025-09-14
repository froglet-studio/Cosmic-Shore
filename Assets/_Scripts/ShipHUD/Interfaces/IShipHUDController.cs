using UnityEngine;

namespace CosmicShore.Game
{
    public interface IVesselHUDController
    {
        void Initialize(IVesselStatus status, ShipHUDView view);
        void SetBlockPrefab(GameObject block);
    }
}
