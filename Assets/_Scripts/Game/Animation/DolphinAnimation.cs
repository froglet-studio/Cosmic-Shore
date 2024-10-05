using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Animation
{
    class DolphinAnimation : ShipAnimation
    {
        [SerializeField] Transform Fusilage;
        [SerializeField] Transform LeftWing;
        [SerializeField] Transform RightWing;
        [SerializeField] Transform TailStart;
        [SerializeField] Transform TailEnd;
        [SerializeField] Transform LeftTail;
        [SerializeField] Transform RightTail;

        [SerializeField] float animationScaler = 25f;
        [SerializeField] float yawAnimationScaler = 15f;
        [SerializeField] float rollAnimationScaler = 15f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            RotatePart(LeftWing,
                Brake(throttle) * yawAnimationScaler,
                -(throttle - yaw) * yawAnimationScaler,
                (roll - pitch) * rollAnimationScaler);

            RotatePart(RightWing,
                        Brake(throttle) * yawAnimationScaler,
                        (throttle + yaw) * yawAnimationScaler,
                        (roll + pitch) * rollAnimationScaler);

            var pitchScalar = pitch * animationScaler;
            var yawScalar = yaw * animationScaler;
            var rollScalar = roll * animationScaler;

            foreach (var part in new List<Transform>() { Fusilage, TailStart, TailEnd, LeftTail, RightTail })
                RotatePart(part, pitchScalar, yawScalar, rollScalar);
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(Fusilage);
            Transforms.Add(LeftWing);
            Transforms.Add(RightWing);
            Transforms.Add(TailStart);
            Transforms.Add(TailEnd);
            Transforms.Add(LeftTail);
            Transforms.Add(RightTail);
        }
    }
}