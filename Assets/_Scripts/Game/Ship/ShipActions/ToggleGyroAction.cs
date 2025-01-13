using CosmicShore.Game.IO;
using UnityEngine;


public class ToggleGyroAction : ShipAction
{
    InputController inputController;

    protected override void Start()
    {
        if (Ship != null) inputController = Ship.InputController;
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