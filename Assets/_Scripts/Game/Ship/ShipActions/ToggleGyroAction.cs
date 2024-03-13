using CosmicShore.Core;
using CosmicShore.Game.IO;

public class ToggleGyroAction : ShipAction
{
    InputController _inputController;

    protected override void Start()
    {
        if (ship is null)
        {
            ship = GetComponentInParent<Ship>();
            _inputController = ship.InputController;
        }
        
    }

    public override void StartAction()
    {
        _inputController.OnToggleGyro(true);
    }

    public override void StopAction()
    {
        _inputController.OnToggleGyro(false);
    }
}
