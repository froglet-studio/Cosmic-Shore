using CosmicShore;
using CosmicShore.Game.UI;

public interface IShipHUDView
{
    // Called right after spawn to set up logic/event hooks
    void Initialize(IShipHUDController controller);
    ResourceDisplay GetResourceDisplay(string resourceName);
}
