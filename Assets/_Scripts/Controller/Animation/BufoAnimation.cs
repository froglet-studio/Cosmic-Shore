using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    class BufoAnimation : VesselAnimation
    {
        [SerializeField] Transform Fusilage;
        [SerializeField] Transform ThrusterTopRight;
        [SerializeField] Transform TopWing;
        [SerializeField] Transform ThrusterBottomRight;
        [SerializeField] Transform ThrusterBottomLeft;
        [SerializeField] Transform BottomWing;
        [SerializeField] Transform ThrusterTopLeft;

        // The BODY lean, in degrees at full stick. The fleet authors this at 25 for a
        // fuselage (Dolphin/Rhino/Riptide/Urchin 25, Manta 30); the 80-ish scalers
        // elsewhere are yaw scalers that swing WINGS and ENGINES, not the hull. This
        // was a hard-coded 82 - over 3x the fleet value for a body - which read as the
        // ship visually over-rotating relative to where it was actually pointed.
        // Serialized rather than const so it is tunable in the Inspector like every
        // other vessel, instead of needing a recompile.
        [SerializeField, Tooltip("Body lean in degrees at full stick. Fleet standard for a fuselage is 25.")]
        float animationScalar = 25f;

        /// <summary>Thrusters lean slightly harder than the hull, preserving the original 1.05 ratio.</summary>
        float exaggeratedAnimationScalar => 1.05f * animationScalar;

        // The drawn hull is Grizzly.fbx (Docs/VesselModels/Grizzly.md), nested under Fusilage. The puppetry
        // still turns the wedge's part transforms, which carry the colliders and the jets; each drawn bone
        // FOLLOWS its part (see FollowParts), the RhinoAnimation pattern. The four engine pods follow their
        // thrusters rigidly, so a pod's mouth stays on its VesselJet. The reactor shell and the dig-in anchor
        // follow the fins only partly and about their own pivots: the fins swing about the hull's centre, and
        // a full follow would slide the shell off the shoulders.
        const string TopLeftEngineBone = "jet.T.L";
        const string TopRightEngineBone = "jet.T.R";
        const string BottomLeftEngineBone = "jet.B.L";
        const string BottomRightEngineBone = "jet.B.R";
        const string HumpBone = "hump";
        const string KeelBone = "keel";

        [Header("Drawn hull")]
        [SerializeField, Range(0f, 1f), Tooltip("How much of the top fin's swing the reactor shell (the hump) takes, about its own base. 1 is a rigid follow about the hull's centre.")]
        float humpFollow = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("How much of the bottom fin's swing the dig-in anchor takes, about its own root.")]
        float keelFollow = 0.35f;

        /// <summary>A drawn bone carried by the part the puppetry turns: rigidly (Weight 1), or partly about its own pivot.</summary>
        struct BoneFollower
        {
            public Transform Bone;
            public Transform Part;
            public float Weight;
            public Vector3 PositionInPart;
            public Quaternion RotationInPart;
            public Vector3 PositionInFrame;
            public Quaternion RotationInFrame;
            public Quaternion PartRest;
        }

        readonly BoneFollower[] _followers = new BoneFollower[6];
        int _followerCount;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            // The model is found, not wired: its sub-objects' fileIDs exist only after Unity imports it. The base
            // drives its element shapes by name; the flares need the renderer; and the pods swing past the
            // bind-pose bounds, so it must not be culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            base.Initialize(vesselStatus);
        }

        protected override void ResolveParts()
        {
            // Captured here, at the authored pose, before anything animates. A prefab without the model
            // resolves no bones and follows nothing.
            _followerCount = 0;
            Follow(ThrusterTopLeft, FindBone(TopLeftEngineBone), 1f);
            Follow(ThrusterTopRight, FindBone(TopRightEngineBone), 1f);
            Follow(ThrusterBottomLeft, FindBone(BottomLeftEngineBone), 1f);
            Follow(ThrusterBottomRight, FindBone(BottomRightEngineBone), 1f);
            Follow(TopWing, FindBone(HumpBone), humpFollow);
            Follow(BottomWing, FindBone(KeelBone), keelFollow);
            ReportUnresolvedParts();
        }

        Transform FindBone(string boneName) => SkinnedMeshRenderer ? ResolvePart(null, boneName) : null;

        void Follow(Transform part, Transform bone, float weight)
        {
            if (!part || !bone || part == bone || _followerCount >= _followers.Length) return;
            var frame = part.parent;
            _followers[_followerCount++] = new BoneFollower
            {
                Bone = bone,
                Part = part,
                Weight = weight,
                PositionInPart = part.InverseTransformPoint(bone.position),
                RotationInPart = Quaternion.Inverse(part.rotation) * bone.rotation,
                PositionInFrame = frame ? frame.InverseTransformPoint(bone.position) : bone.position,
                RotationInFrame = frame ? Quaternion.Inverse(frame.rotation) * bone.rotation : bone.rotation,
                PartRest = part.localRotation,
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
                if (f.Weight >= 1f)
                {
                    f.Bone.SetPositionAndRotation(f.Part.TransformPoint(f.PositionInPart), f.Part.rotation * f.RotationInPart);
                    continue;
                }

                var frame = f.Part.parent;
                if (!frame) continue;
                var swing = Quaternion.Slerp(Quaternion.identity, f.Part.localRotation * Quaternion.Inverse(f.PartRest), f.Weight);
                f.Bone.SetPositionAndRotation(frame.TransformPoint(f.PositionInFrame), frame.rotation * swing * f.RotationInFrame);
            }
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(Fusilage);
            //Transforms.Add(Turret);
            Transforms.Add(ThrusterTopRight);
            Transforms.Add(TopWing);
            Transforms.Add(ThrusterBottomRight);
            Transforms.Add(ThrusterBottomLeft);
            Transforms.Add(BottomWing);
            Transforms.Add(ThrusterTopLeft);
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            // Pitch leans the OTHER way on this hull: playtest (2026-10-08) read the Grizzly's
            // pitch puppetry as reversed against its turn. Every animated part sits in an
            // identity frame under the hull (Grizzly -> OrientationHandle -> Ship_Wedge_Body),
            // so this is a sign, not a missing composition term - yaw was not reported and is
            // left as it was.
            pitch = -pitch;

            var pitchScalar = pitch * exaggeratedAnimationScalar;
            var yawScalar = yaw * exaggeratedAnimationScalar;
            var rollScalar = roll * exaggeratedAnimationScalar;

            RotatePart(Fusilage, pitch * animationScalar, yaw * animationScalar, 0);
            //AnimatePart(Turret, pitchScalar * .7f, yawScalar, rollScalar);

            // Six explicit calls, same parts and order as the list this used to build: a new List every frame was the
            // vessel's only per-frame allocation in the first shared Unity capture (Grizzly Charge, 2026-10-16).
            RotatePart(ThrusterTopRight, pitchScalar, yawScalar, -yawScalar);
            RotatePart(TopWing, pitchScalar, yawScalar, -yawScalar);
            RotatePart(ThrusterBottomRight, pitchScalar, yawScalar, -yawScalar);
            RotatePart(ThrusterBottomLeft, pitchScalar, yawScalar, -yawScalar);
            RotatePart(BottomWing, pitchScalar, yawScalar, -yawScalar);
            RotatePart(ThrusterTopLeft, pitchScalar, yawScalar, -yawScalar);
        }

        protected override void RotatePart(Transform part, float pitch, float yaw, float roll)
        {
            Quaternion rotation = VesselStatus.IsPortrait ? Quaternion.Euler(yaw, -pitch, -roll) : Quaternion.Euler(pitch, yaw, roll);

            part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    rotation,
                                    lerpAmount * Time.deltaTime);
        }
    }
}