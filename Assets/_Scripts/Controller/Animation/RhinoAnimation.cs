using UnityEngine;

namespace CosmicShore.Gameplay
{
    class RhinoAnimation : VesselAnimation
    {
        [SerializeField] Transform Fusilage;
        [SerializeField] Transform LeftWing;
        [SerializeField] Transform RightWing;
        [SerializeField] Transform LeftEngine;
        [SerializeField] Transform RightEngine;

        [SerializeField] float animationScaler = 25f;
        [SerializeField] float yawAnimationScaler = 80f;

        // Bone names of the rigged rhino model (rhino_shapekey_with_animations.fbx): a 'fuse'
        // body, front wings 'wing1.*' carrying the back wings 'wing2.*' and the engines 'jet.*'.
        // Legacy part names from the placeholder model follow as fallbacks, so this resolves on
        // either art. See VesselAnimation.ResolvePart.
        protected override void ResolveParts()
        {
            Fusilage = ResolvePart(Fusilage, "fuse", "Fusilage", "Rhino_Test (1)");
            LeftWing = ResolvePart(LeftWing, "wing1.l", "Wing front left");
            RightWing = ResolvePart(RightWing, "wing1.r", "Wing front right");
            LeftEngine = ResolvePart(LeftEngine, "jet.l", "engine left");
            RightEngine = ResolvePart(RightEngine, "jet.r", "engine right");

            ReportUnresolvedParts();
        }

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