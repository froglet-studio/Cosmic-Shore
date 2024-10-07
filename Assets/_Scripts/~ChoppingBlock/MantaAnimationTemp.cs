using UnityEngine;

namespace CosmicShore.Game.Animation
{
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

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            RotatePart(LeftWing,
                        Brake(throttle) * yawAnimationScaler,
                        (pitch) * animationScaler,
                        82 + (throttle - yaw) * yawAnimationScaler);

            RotatePart(RightWing,
                        Brake(throttle) * yawAnimationScaler,
                        (-pitch) * animationScaler,
                        -82 + (throttle + yaw) * yawAnimationScaler);

            RotatePart(Fusilage,
                        -90 + pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePart(ThrusterBottomLeft,
                        Brake(throttle) * yawAnimationScaler,
                        (roll + pitch + (3 * (1 - throttle))) * animationScaler, (throttle - yaw) * yawAnimationScaler);

            RotatePart(ThrusterTopRight,
                        Brake(throttle) * yawAnimationScaler,
                        (roll + pitch + (3 * (1 - throttle))) * animationScaler, -(throttle + yaw) * yawAnimationScaler);

            RotatePart(ThrusterTopLeft,
                        Brake(throttle) * yawAnimationScaler,
                        (roll - pitch - (3 * (1 - throttle))) * animationScaler, (throttle - yaw) * yawAnimationScaler);

            RotatePart(ThrusterBottomRight,
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
}