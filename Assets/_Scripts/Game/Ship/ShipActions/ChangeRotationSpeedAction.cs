using CosmicShore.Core;
using UnityEngine;

public class ChangeRotationSpeedAction : ShipAction
{
    [SerializeField] float rotationSpeedMultiplier = 5f;
    [SerializeField] bool pitch = true;
    [SerializeField] bool yaw = true;
    [SerializeField] bool roll = true;

    public override void StartAction()
    {
        if (pitch) Vessel.VesselStatus.VesselTransformer.PitchScaler *= rotationSpeedMultiplier;
        if (yaw) Vessel.VesselStatus.VesselTransformer.YawScaler *= rotationSpeedMultiplier;
        if (roll) Vessel.VesselStatus.VesselTransformer.RollScaler *= rotationSpeedMultiplier;
    }

    public override void StopAction()
    {
        if (pitch) Vessel.VesselStatus.VesselTransformer.PitchScaler /= rotationSpeedMultiplier;
        if (yaw) Vessel.VesselStatus.VesselTransformer.YawScaler /= rotationSpeedMultiplier;
        if (roll) Vessel.VesselStatus.VesselTransformer.RollScaler /= rotationSpeedMultiplier;
    }
}