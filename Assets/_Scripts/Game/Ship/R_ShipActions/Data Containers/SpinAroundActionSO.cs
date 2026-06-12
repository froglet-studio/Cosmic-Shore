using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "SpinAroundAction", menuName = "ScriptableObjects/Vessel Actions/Spin Around")]
public class SpinAroundActionSO : ShipActionSO
{
    [Header("Config")]
    [Tooltip("Yaw angle in degrees applied as an instant flat spin.")]
    [SerializeField] float spinDegrees = 180f;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        => vesselStatus?.VesselTransformer?.FlatSpinShip(spinDegrees);

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
    }
}
