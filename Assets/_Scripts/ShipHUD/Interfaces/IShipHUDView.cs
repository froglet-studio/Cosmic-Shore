using CosmicShore;
using CosmicShore.Game.UI;
using UnityEngine;

public interface IShipHUDView
{
    // Called right after spawn to set up logic/event hooks
    void Initialize(IShipHUDController controller);
    ResourceDisplay GetResourceDisplay(string resourceName);
    Transform GetSilhouetteContainer();
    Transform GetTrailContainer();
}
