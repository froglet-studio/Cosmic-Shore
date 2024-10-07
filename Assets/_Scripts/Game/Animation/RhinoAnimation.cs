using UnityEngine;

namespace CosmicShore.Game.Animation
{
    class RhinoAnimation : ShipAnimation
    {
        [SerializeField] Transform Fusilage;
        [SerializeField] Transform LeftWing;
        [SerializeField] Transform RightWing;
        [SerializeField] Transform LeftEngine;
        [SerializeField] Transform RightEngine;

        [SerializeField] float animationScaler = 25f;
        [SerializeField] float yawAnimationScaler = 80f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            RotatePart(LeftWing,
                        0,
                        -Brake(throttle) * yawAnimationScaler,
                        (-1 + throttle) * yawAnimationScaler);

            RotatePart(RightWing,
                        0,
                        Brake(throttle) * yawAnimationScaler,
                        (1 - throttle) * yawAnimationScaler);

            RotatePart(Fusilage,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePart(LeftEngine,
                        0,
                        Brake(throttle) * yawAnimationScaler,
                        -(-1 + throttle) * yawAnimationScaler);

            RotatePart(RightEngine,
                        0,
                        -Brake(throttle) * yawAnimationScaler,
                        -(1 - throttle) * yawAnimationScaler);
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(LeftWing);
            Transforms.Add(RightWing);
            Transforms.Add(Fusilage);
        }
    }
}