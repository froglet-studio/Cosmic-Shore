using CosmicShore.Game.IO;
using UnityEngine;


public class ToggleGyroAction : ShipAction
{
    InputController inputController;

    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
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