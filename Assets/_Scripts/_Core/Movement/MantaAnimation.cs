using UnityEngine;

class MantaAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;
    [SerializeField] Transform LeftWing;
    [SerializeField] Transform RightWing;
    [SerializeField] Transform ThrusterTopRight;
    [SerializeField] Transform ThrusterTopLeft;
    [SerializeField] Transform ThrusterBottomLeft;
    [SerializeField] Transform ThrusterBottomRight;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        AnimatePart(LeftWing,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler);

        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler);

        AnimatePart(Fusilage,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(ThrusterBottomLeft,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler);

        AnimatePart(ThrusterTopRight,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler);

        AnimatePart(ThrusterTopLeft,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler);

        AnimatePart(ThrusterBottomRight,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler);

    }

    protected override void AssignTransforms()
    {
        Transforms.Add(Fusilage);
        Transforms.Add(LeftWing);
        Transforms.Add(RightWing);
    }
}