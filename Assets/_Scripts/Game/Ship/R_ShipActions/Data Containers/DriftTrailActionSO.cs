using UnityEngine;

[CreateAssetMenu(fileName = "DriftTrailAction", menuName = "ScriptableObjects/Vessel Actions/Drift Trail")]
public class DriftTrailActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<DriftTrailActionExecutor>()?.Begin(this, Ship, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<DriftTrailActionExecutor>()?.End(this, Ship, ShipStatus);
}