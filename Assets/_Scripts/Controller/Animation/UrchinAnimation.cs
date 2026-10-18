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

        // ---- the drawn model -------------------------------------------------------------
        // Since the 2026-10-18 rig swap the prefab DRAWS urchan_shapekey_with_animations.fbx
        // (nested under OrientationHandle at localScale 0.474905) instead of the 13 MeshFilters
        // of Urchan_Test.fbx. The placeholder's part transforms are KEPT, because the 12 box
        // colliders, the four VesselJets and the gun muzzles live on them, and this script keeps
        // driving them exactly as before. Each pod's hold bone FOLLOWS its part: the rig's rest
        // is the placeholder's free-flight layout (every part within half a millimetre), so the
        // part's pose relative to its rest is applied to the bone relative to ITS rest - rotation,
        // position and the 1.75 <-> 1.0 tuck scale - and every collider keeps enclosing the pod
        // it was authored for. Bones are found by NAME (an FBX's sub-object fileIDs cannot be
        // wired offline); a prefab without the model simply follows nothing.
        static readonly string[] FollowBoneNames =
        {
            "fuse", "winggrowrotM.l", "winggrowrotM.r",
            "winggrowrotT.l", "winggrowrotT.r", "winggrowrotB.l", "winggrowrotB.r",
        };

        Transform[] _followParts = System.Array.Empty<Transform>();
        Transform[] _followBones = System.Array.Empty<Transform>();
        Quaternion[] _followRotation = System.Array.Empty<Quaternion>();   // bone rest in the part's rest frame
        Vector3[] _followOffset = System.Array.Empty<Vector3>();           // bone rest position, part-local
        Vector3[] _followBoneScale = System.Array.Empty<Vector3>();        // bone rest localScale
        float[] _followPartScale = System.Array.Empty<float>();            // part rest localScale (uniform)
        int _followCount;

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
            BindModelToParts();
        }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            // The model is found, not wired (its SkinnedMeshRenderer's fileID exists only after
            // Unity imports it). Its element shapes are collected by the base class by name; the
            // pods' tuck and the Space morph reach past the bind-pose bounds, so it must not be
            // culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;

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

        /// <summary>
        /// Pairs each driven placeholder part with the rig bone that carries the same piece of
        /// art (Body - fuse, LeftGun - winggrowrotM.l, JetTopLeft - winggrowrotT.l, ...: '.l' is
        /// the pilot's left, Unity -X, where the Left parts' colliders sit) and records both rests.
        /// Runs once, at the authored pose, from ResolveParts.
        /// </summary>
        void BindModelToParts()
        {
            _followCount = 0;
            if (!SkinnedMeshRenderer) return;
            var smr = SkinnedMeshRenderer.transform;
            var modelRoot = smr.parent ? smr.parent : smr;

            var parts = new[] { Body, LeftGun, RightGun, JetTopLeft, JetTopRight, JetBottomLeft, JetBottomRight };
            var bones = new Transform[FollowBoneNames.Length];
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                for (int i = 0; i < FollowBoneNames.Length; i++)
                    if (!bones[i] && string.Equals(t.name, FollowBoneNames[i], System.StringComparison.OrdinalIgnoreCase))
                        bones[i] = t;

            _followParts = new Transform[parts.Length];
            _followBones = new Transform[parts.Length];
            _followRotation = new Quaternion[parts.Length];
            _followOffset = new Vector3[parts.Length];
            _followBoneScale = new Vector3[parts.Length];
            _followPartScale = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var bone = bones[i];
                // A part that IS one of the model's own transforms (a prefab authored straight
                // onto the rig) needs no follower.
                if (!part || !bone || part == bone || part.IsChildOf(modelRoot)) continue;
                int n = _followCount++;
                _followParts[n] = part;
                _followBones[n] = bone;
                _followRotation[n] = Quaternion.Inverse(part.rotation) * bone.rotation;
                _followOffset[n] = part.InverseTransformPoint(bone.position);
                _followBoneScale[n] = bone.localScale;
                _followPartScale[n] = Mathf.Max(1e-4f, part.localScale.x);
            }
        }

        /// <summary>Carries every followed bone onto its part's current pose. Runs in LateUpdate,
        /// after every Update, so this frame's puppetry and tuck are what the skin draws.</summary>
        void FollowParts()
        {
            for (int i = 0; i < _followCount; i++)
            {
                var part = _followParts[i];
                var bone = _followBones[i];
                if (!part || !bone) continue;
                bone.SetPositionAndRotation(part.TransformPoint(_followOffset[i]), part.rotation * _followRotation[i]);
                bone.localScale = _followBoneScale[i] * (part.localScale.x / _followPartScale[i]);
            }
        }

        protected override void LateUpdate()
        {
            FollowParts();
            base.LateUpdate();
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