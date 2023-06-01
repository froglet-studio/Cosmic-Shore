using UnityEngine;

class SharkAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;
    [SerializeField] Transform LeftWing;
    [SerializeField] Transform RightWing;
    [SerializeField] Transform Head;
    [SerializeField] Transform Tail;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        AnimatePart(LeftWing,
                    Brake(throttle) * yawAnimationScaler,
                    0,
                    (roll + pitch) * animationScaler);

        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    0,
                    (roll - pitch) * animationScaler);

        AnimatePart(Fusilage,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(Head,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(Tail,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
    }

    protected override void AssignTransforms()
    {
        Transforms.Add(LeftWing);
        Transforms.Add(RightWing);
        Transforms.Add(Fusilage);
    }
}