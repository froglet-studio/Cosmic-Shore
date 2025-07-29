namespace CosmicShore.Game
{
    public interface IShipHUDController
    {
        void InitializeShipHUD(ShipClassType type);
        void OnButtonPressed(int buttonNumber);
    }
}
