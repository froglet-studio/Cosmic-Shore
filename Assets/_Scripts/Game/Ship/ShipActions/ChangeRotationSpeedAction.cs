using CosmicShore.Core;
using UnityEngine;

public class ChangeRotationSpeedAction : ShipAction
{
    ShipStatus shipData;
    [SerializeField] float rotationSpeedMultiplier = 5f;
    [SerializeField] bool pitch = true;
    [SerializeField] bool yaw = true;
    [SerializeField] bool roll = true;

    protected override void Start()
    {
        base.Start();
        shipData = Ship.ShipStatus;
    }
    public override void StartAction()
    {
        if (pitch) Ship.ShipTransformer.PitchScaler *= rotationSpeedMultiplier;
        if (yaw) Ship.ShipTransformer.YawScaler *= rotationSpeedMultiplier;
        if (roll) Ship.ShipTransformer.RollScaler *= rotationSpeedMultiplier;
    }

    public override void StopAction()
    {
        if (pitch) Ship.ShipTransformer.PitchScaler /= rotationSpeedMultiplier;
        if (yaw) Ship.ShipTransformer.YawScaler /= rotationSpeedMultiplier;
        if (roll) Ship.ShipTransformer.RollScaler /= rotationSpeedMultiplier;
    }
}