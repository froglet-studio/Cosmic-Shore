using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "DriftAction", menuName = "ScriptableObjects/Vessel Actions/Drift")]
public class DriftActionSO : ShipActionSO
{
    [SerializeField] float Mult = 1.5f;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var t = vesselStatus.VesselTransformer;
        t.PitchScaler *= Mult;
        t.YawScaler   *= Mult;
        t.RollScaler  *= Mult;
        vesselStatus.IsDrifting = true;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var t = vesselStatus.VesselTransformer;
        t.PitchScaler /= Mult;
        t.YawScaler   /= Mult;
        t.RollScaler  /= Mult;
        vesselStatus.IsDrifting = false;
    }
}