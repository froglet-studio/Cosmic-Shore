using UnityEngine;

class SharkAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;
    [SerializeField] Transform LeftWing;
    [SerializeField] Transform RightWing;
    [SerializeField] Transform LeftEngine;
    [SerializeField] Transform RightEngine;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        AnimatePart(LeftWing,
                    0,
                    -Brake(throttle) * yawAnimationScaler,
                    (roll + pitch) * animationScaler);

        AnimatePart(RightWing,
                    0,
                    Brake(throttle) * yawAnimationScaler,
                    (roll - pitch) * animationScaler);

        AnimatePart(Fusilage,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(LeftEngine,
                    0,
                    Brake(throttle) * yawAnimationScaler,
                    0);

        AnimatePart(RightEngine,
                    0,
                    -Brake(throttle) * yawAnimationScaler,
                    0);
    }

    protected override void AssignTransforms()
    {
        Transforms.Add(LeftWing);
        Transforms.Add(RightWing);
        Transforms.Add(Fusilage);
    }
}