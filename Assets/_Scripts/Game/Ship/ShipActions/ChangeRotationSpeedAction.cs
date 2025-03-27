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
        if (pitch) Ship.ShipStatus.ShipTransformer.PitchScaler *= rotationSpeedMultiplier;
        if (yaw) Ship.ShipStatus.ShipTransformer.YawScaler *= rotationSpeedMultiplier;
        if (roll) Ship.ShipStatus.ShipTransformer.RollScaler *= rotationSpeedMultiplier;
    }

    public override void StopAction()
    {
        if (pitch) Ship.ShipStatus.ShipTransformer.PitchScaler /= rotationSpeedMultiplier;
        if (yaw) Ship.ShipStatus.ShipTransformer.YawScaler /= rotationSpeedMultiplier;
        if (roll) Ship.ShipStatus.ShipTransformer.RollScaler /= rotationSpeedMultiplier;
    }
}