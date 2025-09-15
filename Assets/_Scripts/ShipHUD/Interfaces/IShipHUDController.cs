using UnityEngine;

namespace CosmicShore.Game
{
    public interface IVesselHUDController
    {
        void Initialize(IVesselStatus status, VesselHUDView view);
        void SetBlockPrefab(GameObject block);
    }
}
