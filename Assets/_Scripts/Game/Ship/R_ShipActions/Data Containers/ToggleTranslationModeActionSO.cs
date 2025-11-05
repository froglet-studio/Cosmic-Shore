using UnityEngine;

[CreateAssetMenu(fileName="ToggleTranslationModeAction", menuName="ScriptableObjects/Vessel Actions/Toggle Translation Mode")]
public sealed class ToggleTranslationModeActionSO : ShipActionSO
{
    public enum Mode { Serpent, Sparrow }
    [SerializeField] private Mode stationaryMode = Mode.Serpent;
    public Mode StationaryMode => stationaryMode;

    public override bool IsEdgeTriggered => true; // press-only

    public override void StartAction(ActionExecutorRegistry reg)
    {
        var exec = reg.Get<ToggleTranslationModeActionExecutor>();
        var v    = reg.VesselStatus;
        var ship = v?.Vessel;
        if (exec && v != null && ship != null)
            exec.Toggle(this, ship, v);
    }

    public override void StopAction(ActionExecutorRegistry reg)
    {
        // NO-OP for toggle
    }
}
