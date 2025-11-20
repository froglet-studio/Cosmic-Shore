using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "DriftTrailAction", menuName = "ScriptableObjects/Vessel Actions/Drift Trail")]
public class DriftTrailActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<DriftTrailActionExecutor>()?.Begin(this, vesselStatus);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => execs?.Get<DriftTrailActionExecutor>()?.End(this, vesselStatus);
}