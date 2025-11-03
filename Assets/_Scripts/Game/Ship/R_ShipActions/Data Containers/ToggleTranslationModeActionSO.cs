using UnityEngine;

[CreateAssetMenu(fileName="ToggleTranslationModeAction", menuName="ScriptableObjects/Vessel Actions/Toggle Translation Mode")]
public sealed class ToggleTranslationModeActionSO : ShipActionSO
{
    public enum Mode { Serpent, Sparrow }
    [SerializeField] private Mode stationaryMode = Mode.Serpent;
    public Mode StationaryMode => stationaryMode;

    [SerializeField] private bool edgeTriggered = true;
    public override bool IsEdgeTriggered => edgeTriggered;

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
    }
}