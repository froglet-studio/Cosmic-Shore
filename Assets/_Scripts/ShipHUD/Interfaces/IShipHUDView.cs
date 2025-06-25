using CosmicShore;

public interface IShipHUDView
{
    // Called right after spawn to set up logic/event hooks
    void Initialize(IShipHUDController controller);
}
