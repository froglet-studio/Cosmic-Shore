namespace CosmicShore.Game
{
    public interface IShipHUDController
    {
        void InitializeShipHUD(ShipTypes type);
        void OnButtonPressed(int buttonNumber);
    }
}
