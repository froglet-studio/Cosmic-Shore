using UnityEngine;

class MantaAnimationTemp : ShipAnimation
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
                    (pitch) * animationScaler,
                    82+(throttle - yaw) * yawAnimationScaler);

        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    (- pitch) * animationScaler,
                    -82+(throttle + yaw) * yawAnimationScaler);

        AnimatePart(Fusilage,
                    -90+pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(ThrusterBottomLeft,
                    Brake(throttle) * yawAnimationScaler,
                    (roll + pitch + (3 * (1 - throttle))) * animationScaler, (throttle - yaw) * yawAnimationScaler);

        AnimatePart(ThrusterTopRight,
                    Brake(throttle) * yawAnimationScaler,
                    (roll + pitch + (3 * (1 - throttle))) * animationScaler, -(throttle + yaw) * yawAnimationScaler);

        AnimatePart(ThrusterTopLeft,
                    Brake(throttle) * yawAnimationScaler,
                    (roll - pitch - (3 * (1 - throttle))) * animationScaler, (throttle - yaw) * yawAnimationScaler);

        AnimatePart(ThrusterBottomRight,
                    Brake(throttle) * yawAnimationScaler,
                    (roll - pitch - (3 * (1 - throttle))) * animationScaler, -(throttle + yaw) * yawAnimationScaler);

    }

    protected override void AssignTransforms()
    {
        Transforms.Add(Fusilage);
        Transforms.Add(LeftWing);
        Transforms.Add(RightWing);
    }
}