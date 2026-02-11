using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

[CreateAssetMenu(fileName = "DriftAction", menuName = "ScriptableObjects/Vessel Actions/Drift")]
public class DriftActionSO : ShipActionSO
{
    [SerializeField] float Mult = 1.5f;
    Vector3 savedRotations = Vector3.zero;
    [SerializeField] float driftDamping = 0f;
    [SerializeField] bool isSharpDrifting;

    [SerializeField] ScriptableEventNoParam isDrifting;
    [SerializeField] ScriptableEventNoParam isDoubleDrifting;
    [SerializeField] ScriptableEventNoParam driftEnded;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var t = vesselStatus.VesselTransformer;
        savedRotations = new Vector3(t.PitchScaler, t.YawScaler, t.RollScaler);
        t.PitchScaler *= Mult;
        t.YawScaler   *= Mult;
        t.RollScaler  *= Mult;
        t.DriftDamping = driftDamping;
        vesselStatus.IsDrifting = true;

        if (isSharpDrifting)
        {
            isDoubleDrifting.Raise();
        }
        else
        {
            isDrifting.Raise();
        }
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        var t = vesselStatus.VesselTransformer;
        t.PitchScaler = savedRotations.x;
        t.YawScaler = savedRotations.y;
        t.RollScaler = savedRotations.z;
        vesselStatus.IsDrifting = false;
        driftEnded.Raise();
    }
}