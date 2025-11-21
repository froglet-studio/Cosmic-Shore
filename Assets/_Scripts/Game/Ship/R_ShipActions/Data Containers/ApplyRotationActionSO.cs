using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "ApplyRotationAction", menuName = "ScriptableObjects/Vessel Actions/ApplyRotationActionSO")]
public class ApplyRotationActionSO : ShipActionSO
{
    [SerializeField] float rotationAmount = 45f;
    [SerializeField] bool pitch = true;
    [SerializeField] bool yaw   = true;
    [SerializeField] bool roll  = true;

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
 
        var transformer = vesselStatus.VesselTransformer;

        if (pitch)
            transformer.ApplyRotation(rotationAmount, vesselStatus.Transform.right);

        if (yaw)
            transformer.ApplyRotation(rotationAmount, vesselStatus.Transform.up);

        if (roll)
            transformer.ApplyRotation(rotationAmount, vesselStatus.Transform.forward);
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
    }
}