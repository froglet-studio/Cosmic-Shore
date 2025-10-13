using UnityEngine;

[CreateAssetMenu(fileName = "ToggleStationaryModeAction", menuName = "ScriptableObjects/Vessel Actions/Toggle Stationary Mode")]
public class ToggleTranslationModeActionSO : ShipActionSO
{
    public enum Mode { Sparrow, Serpent }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Sparrow;

    public Mode StationaryMode => mode;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<ToggleStationaryModeActionExecutor>()?.Toggle(this, Ship, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
    {
    }
}