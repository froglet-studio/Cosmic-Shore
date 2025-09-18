using CosmicShore.Core;
using UnityEngine;

public class ApplyRotationAction : ShipAction
{
    [SerializeField] float rotationAmount = 45f;
    [SerializeField] bool pitch = true;
    [SerializeField] bool yaw = true;
    [SerializeField] bool roll = true;

    public override void StartAction()
    {
        if (pitch)
        {
            Vessel.VesselStatus.VesselTransformer.ApplyRotation(rotationAmount, Vessel.Transform.right);
        }
        
        if (yaw)
        {
            Vessel.VesselStatus.VesselTransformer.ApplyRotation(rotationAmount, Vessel.Transform.up);
        }
        
        if (roll)
        {
            Vessel.VesselStatus.VesselTransformer.ApplyRotation(rotationAmount, Vessel.Transform.forward);
        }
    }

    public override void StopAction()
    {
        // No need to undo the rotation when stopping
    }
}