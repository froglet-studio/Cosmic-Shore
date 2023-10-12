using StarWriter.Core.IO;

public class ToggleGyroAction : ShipAction
{
    InputController inputController;

    void Start()
    {
        inputController = ship.InputController;
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