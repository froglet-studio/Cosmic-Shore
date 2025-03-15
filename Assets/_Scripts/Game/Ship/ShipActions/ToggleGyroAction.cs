using CosmicShore.Game;
using CosmicShore.Game.IO;
using UnityEngine;


public class ToggleGyroAction : ShipAction
{
    InputController inputController;

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        if (Ship != null) inputController = Ship.ShipStatus.InputController;
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