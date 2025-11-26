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
            Ship.ShipStatus.ShipTransformer.ApplyRotation(rotationAmount, Ship.Transform.right);
        }

        if (yaw)
        {
            Ship.ShipStatus.ShipTransformer.ApplyRotation(rotationAmount, Ship.Transform.up);
        }

        if (roll)
        {
            Ship.ShipStatus.ShipTransformer.ApplyRotation(rotationAmount, Ship.Transform.forward);
        }
    }

    public override void StopAction()
    {
        // No need to undo the rotation when stopping
    }
}