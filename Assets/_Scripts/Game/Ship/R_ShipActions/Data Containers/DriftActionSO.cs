using UnityEngine;

[CreateAssetMenu(fileName = "DriftAction", menuName = "ScriptableObjects/Vessel Actions/Drift")]
public class DriftActionSO : ShipActionSO
{
    const float Mult = 1.5f;

    public override void StartAction(ActionExecutorRegistry execs)
    {
        var t = Ship.VesselStatus.VesselTransformer;
        t.PitchScaler *= Mult;
        t.YawScaler   *= Mult;
        t.RollScaler  *= Mult;
        Ship.VesselStatus.Drifting = true;
    }

    public override void StopAction(ActionExecutorRegistry execs)
    {
        var t = Ship.VesselStatus.VesselTransformer;
        t.PitchScaler /= Mult;
        t.YawScaler   /= Mult;
        t.RollScaler  /= Mult;
        Ship.VesselStatus.Drifting = false;
    }
}