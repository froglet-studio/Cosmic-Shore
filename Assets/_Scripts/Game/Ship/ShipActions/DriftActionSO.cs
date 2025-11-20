using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "DriftSO", menuName = "ScriptableObjects/Vessel Actions/Drift")]
public class DriftSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs)
    {
        ShipStatus.VesselTransformer.PitchScaler *= 1.5f;
        ShipStatus.VesselTransformer.YawScaler *= 1.5f;
        ShipStatus.VesselTransformer.RollScaler *= 1.5f;
        ShipStatus.IsDrifting = true;
    }

    public override void StopAction(ActionExecutorRegistry execs)
    {
        ShipStatus.VesselTransformer.PitchScaler /= 1.5f;
        ShipStatus.VesselTransformer.YawScaler /= 1.5f;
        ShipStatus.VesselTransformer.RollScaler /= 1.5f;
        ShipStatus.IsDrifting = false;
    }
}