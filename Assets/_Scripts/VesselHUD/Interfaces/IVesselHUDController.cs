using CosmicShore.Game.Ship;

namespace CosmicShore.VesselHUD.Interfaces
{
    public interface IVesselHUDController
    {
        void Initialize(IVesselStatus vesselStatus);

        void SubscribeToEvents();
        void UnsubscribeFromEvents();

        void ShowHUD();
        void HideHUD();

        void SetBlockPrefab(UnityEngine.GameObject prefab);
    }
}