using UnityEngine;

namespace CosmicShore.Game
{
    public interface IShipHUDController
    {
        void Initialize(IShipStatus status, ShipHUDView view);
        void SetBlockPrefab(GameObject block);
    }
}
