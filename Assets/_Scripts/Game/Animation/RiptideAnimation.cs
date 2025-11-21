using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Animation
{
    public class RiptideAnimation : VesselAnimation
    {
        [SerializeField] Transform DriftHandle;
        [SerializeField] Transform Chassis;

        [SerializeField] Transform NoseTop;
        [SerializeField] Transform RightWing;
        [SerializeField] Transform NoseBottom;
        [SerializeField] Transform LeftWing;

        [SerializeField] Transform ThrusterTopRight;
        [SerializeField] Transform ThrusterRight;
        [SerializeField] Transform ThrusterBottomRight;
        [SerializeField] Transform ThrusterBottomLeft;
        [SerializeField] Transform ThrusterLeft;
        [SerializeField] Transform ThrusterTopLeft;
        [SerializeField] Transform topJaw;
        [SerializeField] Transform bottomJaw;

        List<Transform> animationTransforms;
        const float animationScaler = 25f;
        const float exaggeratedAnimationScaler = 3 * animationScaler;

        static Vector3 defaultThrusterPosition = new(0, .15f, -1.7f);
        Vector3 backwardThrusterPosition = defaultThrusterPosition;
        Vector3 defaultWingPosition = Vector3.zero;
        Vector3 forwardWingPosition = new(0, 0, 2.3f);

        [SerializeField] int JawResourceIndex;

        private void OnDisable()
        {
            if (topJaw) VesselStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange -= calculateBlastAngle;
        }
        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (topJaw) base.VesselStatus.ResourceSystem.Resources[JawResourceIndex].OnResourceChange += calculateBlastAngle;

            animationTransforms = new List<Transform>() { ThrusterTopRight, ThrusterRight, ThrusterBottomRight, ThrusterBottomLeft, ThrusterLeft, ThrusterTopLeft };
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            Vector3 wingPosition;
            Vector3 thrusterPosition;

            AnimatePart(Chassis,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler,
                        Vector3.zero);

            if (VesselStatus.IsDrifting)
            {
                DriftHandle.rotation = Quaternion.LookRotation(VesselStatus.Course, transform.up);
                RightWing.parent = DriftHandle;
                LeftWing.parent = DriftHandle;
                wingPosition = forwardWingPosition;

                ThrusterTopRight.parent = DriftHandle;
                ThrusterRight.parent = DriftHandle;
                ThrusterBottomRight.parent = DriftHandle;
                ThrusterBottomLeft.parent = DriftHandle;
                ThrusterLeft.parent = DriftHandle;
                ThrusterTopLeft.parent = DriftHandle;
                thrusterPosition = backwardThrusterPosition;
            }
            else
            {
                RightWing.parent = Chassis;
                LeftWing.parent = Chassis;
                wingPosition = defaultWingPosition;

                ThrusterTopRight.parent = Chassis;
                ThrusterRight.parent = Chassis;
                ThrusterBottomRight.parent = Chassis;
                ThrusterBottomLeft.parent = Chassis;
                ThrusterLeft.parent = Chassis;
                ThrusterTopLeft.parent = Chassis;
                thrusterPosition = defaultThrusterPosition;
            }

            AnimatePart(RightWing,
                        Brake(throttle) * animationScaler,
                        (yaw + throttle) * exaggeratedAnimationScaler,
                        (roll + pitch) * animationScaler,
                        wingPosition);

            AnimatePart(LeftWing,
                        Brake(throttle) * animationScaler,
                        (yaw - throttle) * exaggeratedAnimationScaler,
                        (roll - pitch) * animationScaler,
                        wingPosition);

            var pitchScalar = pitch * exaggeratedAnimationScaler;
            var yawScalar = yaw * exaggeratedAnimationScaler;
            var rollScalar = roll * exaggeratedAnimationScaler;


            for (int partIndex = 0; partIndex < animationTransforms.Count; partIndex++)
            {
                AnimatePart(animationTransforms[partIndex], pitchScalar, yawScalar, rollScalar, thrusterPosition, InitialRotations[partIndex]);
            }

        }

        void AnimatePart(Transform part, float pitch, float yaw, float roll, Vector3 position)
        {
            base.RotatePart(part, pitch, yaw, roll);

            part.localPosition = Vector3.Lerp(part.localPosition, position, lerpAmount * Time.deltaTime);
        }

        void AnimatePart(Transform part, float pitch, float yaw, float roll, Vector3 position, Quaternion InitialRotation)
        {
            base.RotatePart(part, pitch, roll, yaw, InitialRotation);

            part.localPosition = Vector3.Lerp(part.localPosition, position, lerpAmount * Time.deltaTime);
        }

        private void calculateBlastAngle(float currentAmmo)
        {
            topJaw.transform.localRotation = Quaternion.Euler(-21 * currentAmmo, 0, 0);
            bottomJaw.transform.localRotation = Quaternion.Euler(21 * currentAmmo, 0, 0);
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(DriftHandle);
            Transforms.Add(NoseTop);
            Transforms.Add(RightWing);
            Transforms.Add(NoseBottom);
            Transforms.Add(LeftWing);
            Transforms.Add(ThrusterTopRight);
            Transforms.Add(ThrusterRight);
            Transforms.Add(ThrusterBottomRight);
            Transforms.Add(ThrusterBottomLeft);
            Transforms.Add(ThrusterLeft);
            Transforms.Add(ThrusterTopLeft);
            Transforms.Add(topJaw);
            Transforms.Add(bottomJaw);

            InitialRotations.Add(NoseTop.localRotation);
            InitialRotations.Add(NoseBottom.localRotation);
            InitialRotations.Add(ThrusterTopRight.localRotation);
            InitialRotations.Add(ThrusterRight.localRotation);
            InitialRotations.Add(ThrusterBottomRight.localRotation);
            InitialRotations.Add(ThrusterBottomLeft.localRotation);
            InitialRotations.Add(ThrusterLeft.localRotation);
            InitialRotations.Add(ThrusterTopLeft.localRotation);
            InitialRotations.Add(topJaw.localRotation);
            InitialRotations.Add(bottomJaw.localRotation);  
        }
    }
}