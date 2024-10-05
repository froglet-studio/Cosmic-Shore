using UnityEngine;

namespace CosmicShore.Game.Animation
{
    class MantaAnimation : ShipAnimation
    {
        [SerializeField] Transform Fusilage;
        [SerializeField] Transform LeftWing;
        [SerializeField] Transform RightWing;

        [SerializeField] Transform ThrusterTopRight;
        [SerializeField] Transform ThrusterTopLeft;
        [SerializeField] Transform ThrusterBottomLeft;
        [SerializeField] Transform ThrusterBottomRight;

        [SerializeField] float smallAnimationScaler = 10f;
        [SerializeField] float mediumAnimationScaler = 30f;
        [SerializeField] float bigAnimationScaler = 45f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            RotatePart(Fusilage,
                        pitch * mediumAnimationScaler,
                        yaw * mediumAnimationScaler,
                        -roll * mediumAnimationScaler,
                        InitialRotations[0]);

            RotatePart(LeftWing,
                        pitch * mediumAnimationScaler,
                        (-throttle + yaw) * bigAnimationScaler,
                        (-roll - pitch) * smallAnimationScaler,
                        InitialRotations[1]);

            RotatePart(RightWing,
                        pitch * mediumAnimationScaler,
                        (throttle + yaw) * bigAnimationScaler,
                        (-roll + pitch) * smallAnimationScaler,
                        InitialRotations[2]);

            RotatePart(ThrusterBottomLeft,
                        pitch * mediumAnimationScaler,
                        -yaw * mediumAnimationScaler,
                        (-roll + throttle) * smallAnimationScaler,
                        InitialRotations[5]);

            RotatePart(ThrusterTopRight,
                        pitch * mediumAnimationScaler,
                        -yaw * mediumAnimationScaler,
                        (-roll + throttle) * smallAnimationScaler,
                        InitialRotations[4]);

            RotatePart(ThrusterTopLeft,
                        pitch * mediumAnimationScaler,
                        -(yaw) * mediumAnimationScaler,
                        (-roll - throttle) * smallAnimationScaler,
                        InitialRotations[3]);

            RotatePart(ThrusterBottomRight,
                        pitch * mediumAnimationScaler,
                        -(yaw) * mediumAnimationScaler,
                        (-roll - throttle) * smallAnimationScaler,
                        InitialRotations[6]);

        }

        protected override void AssignTransforms()
        {
            Transforms.Add(Fusilage);
            Transforms.Add(LeftWing);
            Transforms.Add(RightWing);
            Transforms.Add(ThrusterTopLeft);
            Transforms.Add(ThrusterTopRight);
            Transforms.Add(ThrusterBottomLeft);
            Transforms.Add(ThrusterBottomRight);

            InitialRotations.Add(Fusilage.localRotation);
            InitialRotations.Add(LeftWing.localRotation);
            InitialRotations.Add(RightWing.localRotation);
            InitialRotations.Add(ThrusterTopLeft.localRotation);
            InitialRotations.Add(ThrusterTopRight.localRotation);
            InitialRotations.Add(ThrusterBottomLeft.localRotation);
            InitialRotations.Add(ThrusterBottomRight.localRotation);
        }
    }
}