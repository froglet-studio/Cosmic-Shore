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

        [Header("Sword Follow")]
        [Tooltip("The Rhino's sword swing. The wings (and the pods they carry) lean with the blade's pose: its " +
                 "sideways swipe, its chop and every combo flourish. Found on the vessel when empty; a hull with " +
                 "no sword (the Jacks) keeps its wings on the flight puppetry alone.")]
        [SerializeField] ShieldSwipeActionExecutor swordSwing;
        [Tooltip("Degrees each wing turns about the vessel's up with the blade's swipe yaw, at the blade's full " +
                 "sideways sweep (90 degrees). Same sense as the blade, so the wings swing with it.")]
        [SerializeField] float swordFollowYawDegrees = 20f;
        [Tooltip("Degrees each wing rolls with the blade's swipe roll, at 90 degrees of blade roll: the hull " +
                 "banks into the cut.")]
        [SerializeField] float swordFollowRollDegrees = 30f;
        [Tooltip("Degrees each wing pitches with the blade's chop, at 90 degrees of blade pitch (the full " +
                 "both-triggers chop is 65): the wings bow into the strike.")]
        [SerializeField] float swordFollowPitchDegrees = 25f;
        [Tooltip("Degrees both wings sweep back per unit of a combo's lunge (thrust), mirrored left and right.")]
        [SerializeField] float swordLungeSweepDegrees = 20f;

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

        // The sword layer rides ON TOP of the flight puppetry: the puppetry eases each wing's
        // localRotation toward its target, so the layer is lifted off before it runs (it eases the
        // flight pose, never the sword's) and laid back on after.
        Quaternion _leftWingFlight, _rightWingFlight;
        bool _swordLayered;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            // The model is found, not wired: its sub-objects' fileIDs exist only after Unity imports
            // it. Its element shapes are driven by the base class by name; the flares need the
            // renderer; and the wing fold (up to 80 degrees) and the morphs reach past the bind-pose
            // bounds, so it must not be culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            if (!swordSwing) swordSwing = GetComponentInChildren<ShieldSwipeActionExecutor>(true);
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
            LiftSwordLayer();
            base.Update();
            LaySwordLayer();
            FollowParts();
        }

        void LiftSwordLayer()
        {
            if (!_swordLayered) return;
            if (LeftWing) LeftWing.localRotation = _leftWingFlight;
            if (RightWing) RightWing.localRotation = _rightWingFlight;
            _swordLayered = false;
        }

        /// <summary>
        /// Leans both wings with the blade. Each channel goes through a sine, so the lean peaks at
        /// 90 degrees of blade and stays continuous through a flourish that spins the blade a full
        /// turn and through the wrap the executor applies when the flourish ends. The turn is
        /// composed in the blade's order (yaw, roll, pitch) and in the blade's own frame, then
        /// carried into each wing's parent space, so the wings swing the way the sword does.
        /// </summary>
        void LaySwordLayer()
        {
            if (!swordSwing) return;
            var frame = swordSwing.PoseFrame;
            if (!frame) return;

            var pose = swordSwing.CurrentPose;
            float yaw = swordFollowYawDegrees * Mathf.Sin(pose.yaw * Mathf.Deg2Rad);
            float roll = swordFollowRollDegrees * Mathf.Sin(pose.roll * Mathf.Deg2Rad);
            float pitch = swordFollowPitchDegrees * Mathf.Sin(pose.pitch * Mathf.Deg2Rad);
            float sweep = swordLungeSweepDegrees * Mathf.Clamp(pose.thrust, -1f, 1f);

            // Right wing: +yaw about up swings its tip aft; the left wing mirrors it.
            if (LeftWing) _leftWingFlight = LeanWing(LeftWing, frame, yaw - sweep, roll, pitch);
            if (RightWing) _rightWingFlight = LeanWing(RightWing, frame, yaw + sweep, roll, pitch);
            _swordLayered = true;
        }

        /// <summary>Lays the lean on <paramref name="wing"/> and returns its flight pose.</summary>
        static Quaternion LeanWing(Transform wing, Transform frame, float yaw, float roll, float pitch)
        {
            Quaternion flight = wing.localRotation;
            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up)
                            * Quaternion.AngleAxis(roll, Vector3.forward)
                            * Quaternion.AngleAxis(pitch, Vector3.right);
            if (wing.parent && wing.parent != frame)
            {
                // The blade's frame expressed in the wing's parent space (the identity on the
                // shipped prefab, where both hang off the fuselage).
                Quaternion toParent = Quaternion.Inverse(wing.parent.rotation) * frame.rotation;
                turn = toParent * turn * Quaternion.Inverse(toParent);
            }
            wing.localRotation = turn * flight;
            return flight;
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
