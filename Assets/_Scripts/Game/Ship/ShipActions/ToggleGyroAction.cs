using CosmicShore.Core;
using CosmicShore.Game.IO;

public class ToggleGyroAction : ShipAction
{
    InputController inputController;

    protected override void Start()
    {
        if (ship == null)
        {
            ship = GetComponentInParent<Ship>();
            if (ship == null)
            {
                ship = GetComponentInParent<ShipNetwork>();
            }
            inputController = ship.InputController;
        }
    }

    public override void StartAction()
    {
        inputController.OnToggleGyro(true);
    }

    public override void StopAction()
    {
        inputController.OnToggleGyro(false);
    }
}