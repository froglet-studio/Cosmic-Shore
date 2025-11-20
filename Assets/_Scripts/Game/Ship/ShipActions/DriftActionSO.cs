using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "DriftSO", menuName = "ScriptableObjects/Vessel Actions/Drift")]
public class DriftSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        vesselStatus.VesselTransformer.PitchScaler *= 1.5f;
        vesselStatus.VesselTransformer.YawScaler *= 1.5f;
        vesselStatus.VesselTransformer.RollScaler *= 1.5f;
        vesselStatus.Drifting = true;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        vesselStatus.VesselTransformer.PitchScaler /= 1.5f;
        vesselStatus.VesselTransformer.YawScaler /= 1.5f;
        vesselStatus.VesselTransformer.RollScaler /= 1.5f;
        vesselStatus.Drifting = false;
    }
}