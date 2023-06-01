using UnityEngine;

class GunFishAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;
    [SerializeField] Transform LeftWing;
    [SerializeField] Transform RightWing;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        AnimatePart(LeftWing,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler);

        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler);

        AnimatePart(Fusilage,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
    }

    protected override void AssignTransforms()
    {
        Transforms.Add(Fusilage);
        Transforms.Add(LeftWing);
        Transforms.Add(RightWing);
    }
}