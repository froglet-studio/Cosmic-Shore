using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName="ToggleTranslationModeAction", menuName="ScriptableObjects/Vessel Actions/Toggle Translation Mode")]
public sealed class ToggleTranslationModeActionSO : ShipActionSO
{
    public enum Mode { Serpent, Sparrow }
    [SerializeField] private Mode stationaryMode = Mode.Serpent;
    public Mode StationaryMode => stationaryMode;

    public override bool IsEdgeTriggered => true; // press-only

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var exec = execs.Get<ToggleTranslationModeActionExecutor>();
        var v    = execs.VesselStatus;
        var ship = v?.Vessel;
        if (exec && v != null && ship != null)
            exec.Toggle(this, ship, v);
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        // NO-OP for toggle
    }
}
