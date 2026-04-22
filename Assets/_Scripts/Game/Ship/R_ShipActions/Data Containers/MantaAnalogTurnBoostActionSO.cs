using CosmicShore.Game;
using UnityEngine;

/// <summary>
/// No-op action SO used as a gamepad override for the Manta's trigger mappings.
/// Prevents the old Yawstery and Boost actions from firing on gamepad while
/// MantaAnalogTurnBoostExecutor handles analog trigger input per-frame.
/// </summary>
[CreateAssetMenu(
    fileName = "MantaAnalogTurnBoostAction",
    menuName = "ScriptableObjects/Vessel Actions/Manta Analog Turn+Boost")]
public sealed class MantaAnalogTurnBoostActionSO : ShipActionSO
{
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
}
