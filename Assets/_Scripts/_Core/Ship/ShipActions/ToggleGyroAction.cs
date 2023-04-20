using StarWriter.Core.Input;

public class ToggleGyroAction : ShipActionAbstractBase
{
    InputController inputController;

    void Start()
    {
        inputController = ship.inputController;
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