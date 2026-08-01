using CosmicShore.Gameplay;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
namespace CosmicShore.Gameplay
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

        // Bone names of the rigged dolphin model (dolphin_shapekey_with_animations.fbx), which
        // was authored FOR this script: six jets (top/middle/bottom x l/r), two jaws and two
        // wings, all hanging off 'fuse'. Legacy names from the older part-per-mesh model follow
        // as fallbacks, so this resolves on either art. See VesselAnimation.ResolvePart.
        protected override void ResolveParts()
        {
            Chassis = ResolvePart(Chassis, "fuse", "Chassis", "Dolphin_Test");
            LeftWing = ResolvePart(LeftWing, "wing.l", "LeftWing");
            RightWing = ResolvePart(RightWing, "wing.r", "RightWing.001", "RightWing");

            ThrusterTopLeft = ResolvePart(ThrusterTopLeft, "jetT.l", "Engine case Left.1");
            ThrusterTopRight = ResolvePart(ThrusterTopRight, "jetT.r", "Engine case Right.1");
            ThrusterLeft = ResolvePart(ThrusterLeft, "jetm.l", "Engine case Left.2");
            ThrusterRight = ResolvePart(ThrusterRight, "jetm.r", "Engine case Right.2");
            ThrusterBottomLeft = ResolvePart(ThrusterBottomLeft, "jetB.l", "Engine case Left.3");
            ThrusterBottomRight = ResolvePart(ThrusterBottomRight, "jetB.r", "Engine case Right.3");

            // The rigged model's jaws ARE its nose halves - one pair of bones serves both roles.
            topJaw = ResolvePart(topJaw, "jaw.u", "TopNose");
            bottomJaw = ResolvePart(bottomJaw, "jaw.b", "bottomNose");
            NoseTop = ResolvePart(NoseTop, "jaw.u", "TopNose");
            NoseBottom = ResolvePart(NoseBottom, "jaw.b", "bottomNose");

            DriftHandle = ResolvePart(DriftHandle, "DriftHandle");

            ReportUnresolvedParts();
        }

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
                SafeLookRotation.TrySet(DriftHandle, VesselStatus.Course, transform.up, DriftHandle ? DriftHandle.gameObject : gameObject, logError: false);
                Reparent(DriftHandle);
                wingPosition = forwardWingPosition;
                thrusterPosition = backwardThrusterPosition;
            }
            else
            {
                Reparent(Chassis);
                wingPosition = defaultWingPosition;
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

        // Swings the wings and thrusters between the chassis and the drift handle. On the rigged
        // model these parts are BONES; a SkinnedMeshRenderer reads its bones wherever they sit in
        // the hierarchy, so re-parenting still skins - it just drives the deformation from the
        // drift handle's space, which is the intent.
        void Reparent(Transform newParent)
        {
            if (!newParent) return;
            SetParent(RightWing, newParent);
            SetParent(LeftWing, newParent);
            SetParent(ThrusterTopRight, newParent);
            SetParent(ThrusterRight, newParent);
            SetParent(ThrusterBottomRight, newParent);
            SetParent(ThrusterBottomLeft, newParent);
            SetParent(ThrusterLeft, newParent);
            SetParent(ThrusterTopLeft, newParent);
        }

        static void SetParent(Transform part, Transform parent)
        {
            if (part && part.parent != parent) part.parent = parent;
        }

        void AnimatePart(Transform part, float pitch, float yaw, float roll, Vector3 position)
        {
            if (!part) return;
            base.RotatePart(part, pitch, yaw, roll);

            part.localPosition = Vector3.Lerp(part.localPosition, position, lerpAmount * Time.deltaTime);
        }

        void AnimatePart(Transform part, float pitch, float yaw, float roll, Vector3 position, Quaternion InitialRotation)
        {
            if (!part) return;
            base.RotatePart(part, pitch, roll, yaw, InitialRotation);

            part.localPosition = Vector3.Lerp(part.localPosition, position, lerpAmount * Time.deltaTime);
        }

        private void calculateBlastAngle(float currentAmmo)
        {
            if (topJaw) topJaw.localRotation = Quaternion.Euler(-21 * currentAmmo, 0, 0);
            if (bottomJaw) bottomJaw.localRotation = Quaternion.Euler(21 * currentAmmo, 0, 0);
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

            // LocalRotationOf keeps the index alignment PerformShipPuppetry relies on even when
            // a part is unbound (it contributes identity instead of throwing).
            InitialRotations.Add(LocalRotationOf(NoseTop));
            InitialRotations.Add(LocalRotationOf(NoseBottom));
            InitialRotations.Add(LocalRotationOf(ThrusterTopRight));
            InitialRotations.Add(LocalRotationOf(ThrusterRight));
            InitialRotations.Add(LocalRotationOf(ThrusterBottomRight));
            InitialRotations.Add(LocalRotationOf(ThrusterBottomLeft));
            InitialRotations.Add(LocalRotationOf(ThrusterLeft));
            InitialRotations.Add(LocalRotationOf(ThrusterTopLeft));
            InitialRotations.Add(LocalRotationOf(topJaw));
            InitialRotations.Add(LocalRotationOf(bottomJaw));
        }
    }
}