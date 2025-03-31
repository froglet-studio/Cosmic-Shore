using CosmicShore.Core;
using CosmicShore.Game;

public abstract class ShipAction : ElementalShipComponent
{
    public IShip Ship { get; private set; }
    protected IShipStatus ShipStatus => Ship.ShipStatus;
    protected ResourceSystem ResourceSystem => Ship.ShipStatus.ResourceSystem;

    public virtual void Initialize(IShip ship)
    {
        Ship = ship;
        BindElementalFloats(Ship);
    }

    public abstract void StartAction();
    public abstract void StopAction();
}