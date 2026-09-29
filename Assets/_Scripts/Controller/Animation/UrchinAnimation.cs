using UnityEngine;
using CosmicShore.Gameplay;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    class UrchinAnimation : VesselAnimation
    {
        [SerializeField] Transform Body;
        [SerializeField] Transform JetBottomLeft;
        [SerializeField] Transform JetBottomRight;
        [SerializeField] Transform ShroudBottomLeft;
        [SerializeField] Transform ShroudBottomRight;
        [SerializeField] Transform ShroudLeft;
        [SerializeField] Transform ShroudRight;
        [SerializeField] Transform LeftGun;
        [SerializeField] Transform RightGun;
        [SerializeField] Transform JetTopLeft;
        [SerializeField] Transform JetTopRight;
        [SerializeField] Transform ShroudTopLeft;
        [SerializeField] Transform ShroudTopRight;

        [SerializeField] float animationScaler = 25f;

        protected List<Transform> scaledParts = new();

        // Bone names of the rigged urchin model (urchan_shapekey_with_animations.fbx): a 'fuse'
        // body with top/bottom jets and a mid gun pair per side, each behind its own hold/rot
        // bone. Legacy part names from the older part-per-mesh model follow as fallbacks, so
        // this resolves on either art. See VesselAnimation.ResolvePart.
        protected override void ResolveParts()
        {
            Body = ResolvePart(Body, "fuse", "Body");
            LeftGun = ResolvePart(LeftGun, "gunM.l", "LeftGun");
            RightGun = ResolvePart(RightGun, "gunM.r", "RightGun");

            JetTopLeft = ResolvePart(JetTopLeft, "jetT.l", "JetTopLeft");
            JetTopRight = ResolvePart(JetTopRight, "jetT.r", "JetTopRight");
            JetBottomLeft = ResolvePart(JetBottomLeft, "jetB.l", "JetBottomLeft");
            JetBottomRight = ResolvePart(JetBottomRight, "jetB.r", "JetBottomRight");

            // Shrouds are the wing/shield panels. Declared for authoring; the puppetry below
            // does not drive them, so an unmatched shroud costs nothing.
            ShroudTopLeft = ResolvePart(ShroudTopLeft, "wingconrotT.l", "ShroudTopLeft");
            ShroudTopRight = ResolvePart(ShroudTopRight, "wingconrotT.r", "ShroudTopRight");
            ShroudBottomLeft = ResolvePart(ShroudBottomLeft, "wingconrotB.l", "ShroudBottomLeft");
            ShroudBottomRight = ResolvePart(ShroudBottomRight, "wingconrotB.r", "ShroudBottomRight");
            ShroudLeft = ResolvePart(ShroudLeft, "sheildconrot.l", "ShroudLeft");
            ShroudRight = ResolvePart(ShroudRight, "sheildconrot.r", "ShroudRight");

            // The legacy parts all rest at identity, so this is a no-op on the current art; the
            // rig's bones rest at large angles (gunM.l ~90 deg, jetT.l ~76) and must be driven
            // relative to that pose or the ship tears flat the moment it animates.
            CaptureRestRotations(Body, LeftGun, RightGun,
                                 JetTopLeft, JetTopRight, JetBottomLeft, JetBottomRight);

            ReportUnresolvedParts();
        }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            // Populated after base.Initialize so ResolveParts has bound the jets.
            scaledParts.Clear();
            AddScaledPart(JetBottomLeft);
            AddScaledPart(JetBottomRight);
            AddScaledPart(JetTopLeft);
            AddScaledPart(JetTopRight);
        }

        void AddScaledPart(Transform part)
        {
            if (part) scaledParts.Add(part);
        }

        protected override void Update()
        {
            base.Update();

            if (VesselStatus.IsAttached)
            {
                ResetParts(scaledParts, 6);
            }
            else
            {
                ScaleParts(scaledParts, 1.75f, 6);
            }
        }

        void ScaleParts(List<Transform> transforms, float scale, float speed)
        {
            foreach (Transform transform in transforms)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(scale, scale, scale), speed * Time.deltaTime);
            }
        }

        void ResetParts(List<Transform> transforms, float speed)
        {
            foreach (Transform transform in transforms)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, speed * Time.deltaTime);
            }
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {

            if (VesselStatus.IsAttached)
            {
                RotatePartFromRest(Body,
                   Time.deltaTime * 100f,
                    0,
                    0);
            }
            else
            {
                RotatePartFromRest(Body,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
            }

            RotatePartFromRest(LeftGun,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePartFromRest(RightGun,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePartFromRest(JetBottomLeft,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

            RotatePartFromRest(JetBottomRight,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

            RotatePartFromRest(JetTopLeft,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

            RotatePartFromRest(JetTopRight,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
        }

        protected override void AssignTransforms()
        {
            //Transforms.Add(Fusilage);
            //Transforms.Add(LeftWing);
            //Transforms.Add(RightWing);
        }
    }
}