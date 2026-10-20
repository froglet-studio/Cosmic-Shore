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

        // The DRAWN hull is the rigged model (rhino_shapekey_with_animations.fbx, nested under
        // 'Rhino_Test (1)' since 2026-10-18): a 'fuse' body, front armour plates 'wing1.*' carrying
        // the back plates 'wing2.*' and the disc engines 'jet.*'. The puppetry still turns the
        // prefab's own part transforms ('Wing front left', 'engine left' ...), because they carry
        // the hull colliders and the pod jets; each drawn bone FOLLOWS its part rigidly (see
        // FollowParts), so the hull moves exactly as the placeholder did, every collider keeps
        // enclosing its drawn plate, and no rest-frame or sign question can arise on the bones.
        const string LeftWingBone = "wing1.l";
        const string RightWingBone = "wing1.r";
        const string LeftEngineBone = "jet.l";
        const string RightEngineBone = "jet.r";

        /// <summary>A drawn bone held rigidly in the frame of the part the puppetry turns.</summary>
        struct BoneFollower
        {
            public Transform Bone;
            public Transform Part;
            public Vector3 PositionInPart;
            public Quaternion RotationInPart;
        }

        // Wings first: the engine bones are their children, so they are placed after their parents.
        readonly BoneFollower[] _followers = new BoneFollower[4];
        int _followerCount;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            // The model is found, not wired: its sub-objects' fileIDs exist only after Unity imports
            // it. Its element shapes are driven by the base class by name; the flares need the
            // renderer; and the wing fold (up to 80 degrees) and the morphs reach past the bind-pose
            // bounds, so it must not be culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            base.Initialize(vesselStatus);
        }

        // Part names: the authored references win (the prefab wires the placeholder's transforms);
        // the rig's bone names come first among the fallbacks, so an unwired prefab drives the bones
        // directly. See VesselAnimation.ResolvePart.
        protected override void ResolveParts()
        {
            Fusilage = ResolvePart(Fusilage, "fuse", "Fusilage", "Rhino_Test (1)");
            LeftWing = ResolvePart(LeftWing, LeftWingBone, "Wing front left");
            RightWing = ResolvePart(RightWing, RightWingBone, "Wing front right");
            LeftEngine = ResolvePart(LeftEngine, LeftEngineBone, "engine left");
            RightEngine = ResolvePart(RightEngine, RightEngineBone, "engine right");

            // The prefab's parts rest at identity, so this is a no-op on them; a bone driven directly
            // (an unwired prefab) rests at its bind angle and must be driven relative to it.
            CaptureRestRotations(Fusilage, LeftWing, RightWing, LeftEngine, RightEngine);

            // Captured here, at the authored pose, before anything animates.
            _followerCount = 0;
            Follow(LeftWing, ResolvePart(null, LeftWingBone));
            Follow(RightWing, ResolvePart(null, RightWingBone));
            Follow(LeftEngine, ResolvePart(null, LeftEngineBone));
            Follow(RightEngine, ResolvePart(null, RightEngineBone));

            ReportUnresolvedParts();
        }

        void Follow(Transform part, Transform bone)
        {
            // A part that IS the bone (an unwired prefab resolved the rig directly) needs no follower.
            if (!part || !bone || part == bone || _followerCount >= _followers.Length) return;
            _followers[_followerCount++] = new BoneFollower
            {
                Bone = bone,
                Part = part,
                PositionInPart = part.InverseTransformPoint(bone.position),
                RotationInPart = Quaternion.Inverse(part.rotation) * bone.rotation,
            };
        }

        protected override void Update()
        {
            base.Update();
            FollowParts();
        }

        /// <summary>Places every drawn bone where its puppeted part carries it. Per frame, no allocation.</summary>
        void FollowParts()
        {
            for (int i = 0; i < _followerCount; i++)
            {
                ref readonly var f = ref _followers[i];
                if (!f.Bone || !f.Part) continue;
                f.Bone.SetPositionAndRotation(f.Part.TransformPoint(f.PositionInPart), f.Part.rotation * f.RotationInPart);
            }
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            RotatePartFromRest(LeftWing,
                        0,
                        -Brake(throttle) * yawAnimationScaler,
                        (-1 + throttle) * yawAnimationScaler);

            RotatePartFromRest(RightWing,
                        0,
                        Brake(throttle) * yawAnimationScaler,
                        (1 - throttle) * yawAnimationScaler);

            RotatePartFromRest(Fusilage,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler);

            RotatePartFromRest(LeftEngine,
                        0,
                        Brake(throttle) * yawAnimationScaler,
                        -(-1 + throttle) * yawAnimationScaler);

            RotatePartFromRest(RightEngine,
                        0,
                        -Brake(throttle) * yawAnimationScaler,
                        -(1 - throttle) * yawAnimationScaler);
        }

        // Every part PerformShipPuppetry drives is listed, so Idle eases every one of them back to
        // rest. The engines were missing, and swung through yawAnimationScaler (80 deg) like the
        // wings, so an idling Rhino settled its wings and hull and left both engines wherever the
        // last turn had parked them. Appended, so the index-matched InitialRotations of the first
        // three are unchanged.
        protected override void AssignTransforms()
        {
            Transforms.Add(LeftWing);
            Transforms.Add(RightWing);
            Transforms.Add(Fusilage);
            Transforms.Add(LeftEngine);
            Transforms.Add(RightEngine);
        }
    }
}
