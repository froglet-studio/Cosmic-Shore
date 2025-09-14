using UnityEngine;

namespace CosmicShore.Game
{
    public interface IShipHUDController
    {
        void Initialize(IVesselStatus status, ShipHUDView view);
        void SetBlockPrefab(GameObject block);
    }
}
