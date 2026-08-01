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

        // Bone names of the rigged dolphin model (dolphin_shapekey_with_animations.fbx), which was
        // authored FOR this script: six jets (top/middle/bottom x l/r), two jaws and two wings.
        // Only the jaws hang off 'fuse'; each wing and jet sits behind its own 'winghold'/'jethold'
        // parent, and those parents carry the rest angles that fan the engines out - which is why
        // the drift re-parent restores each part's own parent rather than a shared node. Legacy
        // names from the older part-per-mesh model follow as fallbacks, so this resolves on either
        // art. See VesselAnimation.ResolvePart.
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

            // Drive every part around the pose it was authored in. The legacy wings and noses rest
            // at identity (unchanged), the six engine cases at 26-169 degrees, and the rig's bones
            // at their own fan-out angles - all now animate relative to that instead of toward a
            // bare Euler.
            CaptureRestRotations(Chassis, LeftWing, RightWing, NoseTop, NoseBottom, topJaw, bottomJaw,
                                 ThrusterTopLeft, ThrusterTopRight, ThrusterLeft,
                                 ThrusterRight, ThrusterBottomLeft, ThrusterBottomRight);

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

            CaptureHomeParents();
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
                ReparentToDrift(DriftHandle);
                wingPosition = forwardWingPosition;
                thrusterPosition = backwardThrusterPosition;
            }
            else
            {
                ReparentHome();
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


            // Each thruster is driven around ITS OWN rest pose, looked up per part. The previous
            // InitialRotations[partIndex] indexing was offset by two against animationTransforms
            // (InitialRotations starts with the two nose entries), so every engine animated around
            // a neighbour's rest pose - harmless while all six rested at identity, wrong on the
            // Dolphin's authored 26-169 degree engine cases and fatal on a rig.
            for (int partIndex = 0; partIndex < animationTransforms.Count; partIndex++)
            {
                AnimatePart(animationTransforms[partIndex], pitchScalar, yawScalar, rollScalar, thrusterPosition);
            }

        }

        // Swings the wings and thrusters out to the drift handle while drifting, and HOME again
        // when not. "Home" is each part's own authored parent, captured at Initialize - never a
        // single shared node. On the part-per-mesh art every one of these was a direct child of
        // Chassis, so this is exactly the old behaviour; on the rigged model they are bones whose
        // parents ('winghold.l/r', 'jetholdT/m/B.l/r') carry the rest angles that fan the six
        // engines out, and re-homing them all onto 'fuse' would permanently flatten the armature
        // and collapse the jets onto one point.
        readonly Dictionary<Transform, Transform> _homeParents = new();

        void CaptureHomeParents()
        {
            _homeParents.Clear();
            foreach (var part in DriftParts())
                if (part) _homeParents[part] = part.parent;
        }

        IEnumerable<Transform> DriftParts()
        {
            yield return RightWing;
            yield return LeftWing;
            yield return ThrusterTopRight;
            yield return ThrusterRight;
            yield return ThrusterBottomRight;
            yield return ThrusterBottomLeft;
            yield return ThrusterLeft;
            yield return ThrusterTopLeft;
        }

        void ReparentToDrift(Transform driftHandle)
        {
            if (!driftHandle) return;
            foreach (var part in DriftParts())
                SetParent(part, driftHandle);
        }

        void ReparentHome()
        {
            foreach (var part in DriftParts())
                if (part && _homeParents.TryGetValue(part, out var home))
                    SetParent(part, home);
        }

        static void SetParent(Transform part, Transform parent)
        {
            if (part && parent && part.parent != parent) part.parent = parent;
        }

        // Every animated part is driven around its OWN rest pose. This is the same rotation math
        // the old InitialRotation overload produced (its caller and the base method swapped
        // yaw/roll twice, netting Euler(pitch, yaw, roll) * rest) - it just reads the part's own
        // captured rest instead of one indexed out of a misaligned list. The chassis and wings
        // rest at identity on the legacy art, so they are unaffected there.
        void AnimatePart(Transform part, float pitch, float yaw, float roll, Vector3 position)
        {
            if (!part) return;
            RotatePartFromRest(part, pitch, yaw, roll);

            part.localPosition = Vector3.Lerp(part.localPosition, position, lerpAmount * Time.deltaTime);
        }

        // The jaws open around their rest pose too - identity on the legacy nose halves, the rig's
        // authored jaw angle on 'jaw.u'/'jaw.b'.
        private void calculateBlastAngle(float currentAmmo)
        {
            if (topJaw) topJaw.localRotation = Quaternion.Euler(-21 * currentAmmo, 0, 0) * RestRotationOf(topJaw);
            if (bottomJaw) bottomJaw.localRotation = Quaternion.Euler(21 * currentAmmo, 0, 0) * RestRotationOf(bottomJaw);
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

            // Idle() pairs InitialRotations with Transforms BY INDEX, so this list is built in
            // Transforms order, one entry per part. LocalRotationOf keeps that alignment even
            // when a part is unbound (it contributes identity instead of throwing).
            for (int i = 0; i < Transforms.Count; i++)
                InitialRotations.Add(LocalRotationOf(Transforms[i]));
        }
    }
}