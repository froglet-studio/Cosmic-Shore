using UnityEngine;

class MantaAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;
    [SerializeField] Transform LeftWing;
    [SerializeField] Transform RightWing;
    [SerializeField] Transform LeftWing2;
    [SerializeField] Transform RightWing2;
    [SerializeField] Transform LeftWing3;
    [SerializeField] Transform RightWing3;
    [SerializeField] Transform ThrusterTopRight;
    [SerializeField] Transform ThrusterTopLeft;
    [SerializeField] Transform ThrusterBottomLeft;
    [SerializeField] Transform ThrusterBottomRight;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        AnimatePart(Fusilage,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler,
                    InitialRotations[0]);

        AnimatePart(LeftWing,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler,
                    InitialRotations[1]);

        AnimatePart(LeftWing2,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler,
                    InitialRotations[3]);

        AnimatePart(LeftWing3,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch) * animationScaler,
                    InitialRotations[5]);

        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler,
                    InitialRotations[2]);

        AnimatePart(RightWing2,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler,
                    InitialRotations[4]);

        AnimatePart(RightWing3,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch) * animationScaler,
                    InitialRotations[6]);

        AnimatePart(ThrusterBottomLeft,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch + (3 * (1 - throttle))) * animationScaler);

        AnimatePart(ThrusterTopRight,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll + pitch  + (3 * (1 - throttle))) * animationScaler);

        AnimatePart(ThrusterTopLeft,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll - pitch - (3 * (1 - throttle))) * animationScaler);

        AnimatePart(ThrusterBottomRight,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch - (3 * (1 - throttle))) * animationScaler);

    }

    protected override void AssignTransforms()
    {
        Transforms.Add(Fusilage);
        Transforms.Add(LeftWing);
        Transforms.Add(RightWing);
        Transforms.Add(LeftWing2);
        Transforms.Add(RightWing2);
        Transforms.Add(LeftWing3);
        Transforms.Add(RightWing3);

        InitialRotations.Add(Fusilage.localRotation);
        InitialRotations.Add(LeftWing.localRotation);
        InitialRotations.Add(RightWing.localRotation);
        InitialRotations.Add(LeftWing2.localRotation);
        InitialRotations.Add(RightWing2.localRotation);
        InitialRotations.Add(LeftWing3.localRotation);
        InitialRotations.Add(RightWing3.localRotation);
    }
}