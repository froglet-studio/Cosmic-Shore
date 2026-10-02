using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel's <b>JET</b> — an engine plume that tells a pilot their own ship is moving.
    ///
    /// A jet is mounted on the MODEL, at whatever that hull calls an engine, and it points where
    /// that engine points. That is the whole placement rule: a jet must come out of somewhere the
    /// model says thrust comes out of, so it reads as the ship working rather than as a decal
    /// stuck behind it. Contrast <see cref="VesselTail"/>, which hangs off the vessel ROOT because
    /// its job is a silhouette at range, not a mechanism up close.
    ///
    /// <b>A jet is TUNED for its own pilot, not hidden from everyone else.</b> Its size, its
    /// placement and its short life are all chosen against the pilot's own camera — that is what
    /// "tuned for the pilot" means — but other players still see it, and should: a rival's plumes
    /// are how you read their thrust in a close fight. Only the TAIL is authored for distance.
    ///
    /// The look is authored on the shared prefab
    /// (<c>_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab</c>) and the colour comes from
    /// <see cref="VesselTailAndJets"/>, which paints every tail and jet with the vessel's live
    /// domain. What the marker buys is that "does this vessel have jets, and where?" is a question
    /// the audit tool and the vessel spec can ask of any prefab, instead of a name convention that
    /// the next hull spells differently.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselJet : MonoBehaviour
    {
        [Tooltip("Multiplies the prefab's authored plume width for THIS vessel — same reason and " +
                 "same derivation as VesselTail.widthScale.")]
        [SerializeField] float widthScale = 1f;

        [Tooltip("Optional. Name of the bone or model part this jet mounts on, e.g. 'b_Tail1.L'. " +
                 "Leave empty to stay wherever the prefab parents it. Resolved by NAME at Awake " +
                 "and re-parented, keeping this instance's authored local position as an offset " +
                 "FROM the bone — so (0,0,0) means pinned exactly to it.")]
        [SerializeField] string mountBone;

        [Tooltip("How long this jet takes to swing end-for-end when its vessel starts travelling " +
                 "backwards, in seconds. Never 0: an engine that is pointing one way on one frame " +
                 "and the other way on the next has popped, and nothing on this platform pops.")]
        [SerializeField, Min(0.01f)] float reverseFlipSeconds = 0.25f;

        [Tooltip("How far past broadside the vessel's course has to swing before the jets commit " +
                 "to turning around, as a dot product against the hull's forward. Read as a BAND " +
                 "(flip below -this, return above +this, hold in between) so a course hovering on " +
                 "the threshold cannot flap the plumes.")]
        [SerializeField, Range(0.05f, 0.95f)] float reverseCourseBand = 0.25f;

        /// <summary>The local rotation this jet was authored at, captured AFTER
        /// <see cref="MountOnBone"/> so it is the pose relative to whatever actually parents it.
        /// The flip below is expressed against this rather than against identity, so a jet that is
        /// authored canted on its engine stays canted when it turns around.</summary>
        Quaternion _restRotation;

        /// <summary>0 = pointing the way the hull is built to point, 1 = swung end-for-end.
        /// Eased, never toggled.</summary>
        float _flip01;

        Transform _vesselRoot;
        IVesselStatus _vesselStatus;
        bool _canReverse;

        /// <summary>
        /// The bone or model part this jet mounts on, empty when it stays where the prefab
        /// parented it. Exposed so tooling can ask the jet rather than reaching into the
        /// serialized field by name — a <c>FindProperty</c> that misses after a rename returns
        /// null, which is indistinguishable from "this jet has no mount" and makes a reader
        /// silently report the wrong number.
        /// </summary>
        public string MountBone => mountBone;

        void Awake()
        {
            MountOnBone();
            // AFTER the mount: the rest pose is this jet's rotation relative to whatever actually
            // parents it, so a jet authored canted on its engine stays canted when it turns around.
            _restRotation = transform.localRotation;
            ResolveReverseCapability();
            VesselFXWidth.Apply(this, widthScale);
        }

        /// <summary>
        /// <b>A jet points where its vessel is THRUSTING, which on a hull that can fly backwards is
        /// not always where the hull is pointing.</b> A reversing Urchin's engines swing end-for-end
        /// and its plumes stream out past its own nose — the same thing its trail does, for the same
        /// reason, and the reading the pilot needs in order to believe the ship is under power
        /// rather than being dragged.
        ///
        /// Driven off <c>VesselStatus.Course</c>, which is <b>replicated</b>
        /// (<c>VesselController.n_Course</c>, owner-write), so a rival's jets turn around on every
        /// machine with no new networking and no new state — this asks only for something every
        /// peer already holds. That matters here: a remote replica's transformer is switched off
        /// and computes nothing at all, so a flip derived from local flight state would have been
        /// visible to its own pilot alone.
        ///
        /// Gated on <see cref="VesselTransformer.CanReverse"/> — a CODE property of the flight
        /// model, true today only for the Urchin — rather than on the course dot alone, and the
        /// reason is a hull this has nothing to do with: a DRIFTING vessel's course legitimately
        /// swings past broadside while the pilot spins the nose, so a bare dot test would have
        /// turned a Squirrel's plumes around mid-drift. The capability gate makes this a provable
        /// no-op for every hull that cannot command reverse, at one cached bool per jet per frame.
        /// </summary>
        void LateUpdate()
        {
            if (!_canReverse) return;

            float dot = Vector3.Dot(_vesselStatus.Course, _vesselRoot.forward);

            // A BAND, not a threshold: below -band commit to reversed, above +band commit to
            // forward, and in between hold whatever was last committed. A course sitting on a bare
            // sign test flaps the plumes end-for-end every frame it wanders across it.
            float target = _flip01;
            if (dot < -reverseCourseBand) target = 1f;
            else if (dot > reverseCourseBand) target = 0f;

            _flip01 = Mathf.MoveTowards(_flip01, target,
                                        Time.deltaTime / Mathf.Max(0.01f, reverseFlipSeconds));

            // 180° about the jet's own LOCAL Y maps its local +Z to -Z whatever the mount bone's
            // world orientation happens to be, so the plume reverses on a canted engine exactly as
            // it does on a square one — and composes cleanly with the bone puppetry above it, which
            // writes the PARENT's rotation and never this one.
            transform.localRotation = _restRotation * Quaternion.Euler(0f, 180f * _flip01, 0f);
        }

        /// <summary>
        /// Resolve ONCE, at <c>Awake</c>, whether this jet's vessel can fly backwards — and leave
        /// <see cref="LateUpdate"/> as a single cached bool test on every hull that cannot, which
        /// is the whole fleet bar the Urchin.
        ///
        /// Awake rather than lazily, because every component this asks for is on the vessel prefab
        /// and therefore exists from the moment it is instantiated — the same assumption
        /// <see cref="MountOnBone"/> already makes one line above. None of them has to have run its
        /// own <c>Awake</c> for this to be correct: the question is which flight model this hull
        /// COMPILES, not what state it is in.
        /// </summary>
        void ResolveReverseCapability()
        {
            var owner = GetComponentInParent<VesselTailAndJets>();
            if (owner == null) return;

            _vesselRoot = owner.transform;
            _vesselStatus = owner.GetComponent<IVesselStatus>();

            var transformer = owner.GetComponent<VesselTransformer>();
            _canReverse = transformer != null && transformer.CanReverse
                          && _vesselStatus != null && _vesselRoot;
        }

        /// <summary>
        /// Re-parent onto a named bone on this vessel's model.
        ///
        /// <b>Why by NAME and not by reference.</b> A jet that belongs on an engine has to follow
        /// that engine when the model animates, which means parenting to a bone. A bone inside a
        /// nested model prefab can only be referenced from the vessel prefab by the model file's
        /// own sub-asset id, and that id does not survive a re-export — so an FX mount authored
        /// that way silently detaches the next time the art is updated. Resolving by name is the
        /// same choice <see cref="VesselAnimation"/> already makes for animated parts, and for the
        /// same reason: it is what makes an art swap cheap (<c>Docs/VESSEL_CONSTRUCTION.md</c> §5).
        ///
        /// Fails LOUD and harmlessly: an unresolvable name is an error naming the vessel and the
        /// bone, and the jet stays where the prefab put it rather than vanishing.
        /// </summary>
        /// <summary>
        /// Resolve this jet's <see cref="MountBone"/> against its own vessel, or null when it
        /// declares none or the name does not resolve. This is the ONE implementation: the audit
        /// tool calls it too, so what it reports is what <see cref="MountOnBone"/> will do rather
        /// than a second transcription of the same search that drifts the first time this one is
        /// retuned.
        /// </summary>
        public Transform ResolveMountBone()
        {
            if (string.IsNullOrEmpty(mountBone)) return null;

            // Search from the vessel, not from transform.root — during a spawn the root may still
            // be the scene root, and a bone on a DIFFERENT vessel must never be a candidate.
            var owner = GetComponentInParent<VesselTailAndJets>();
            Transform searchRoot = owner != null ? owner.transform : transform.root;
            return FindDescendant(searchRoot, mountBone);
        }

        void MountOnBone()
        {
            if (string.IsNullOrEmpty(mountBone)) return;

            Transform bone = ResolveMountBone();
            if (bone == null)
            {
                var owner = GetComponentInParent<VesselTailAndJets>();
                string vessel = owner != null ? owner.name : transform.root.name;
                Debug.LogError($"[VesselJet] '{name}' on '{vessel}' wants to mount on bone " +
                               $"'{mountBone}', which is not in that vessel's hierarchy. The jet is " +
                               $"left where the prefab parented it.", this);
                return;
            }

            // worldPositionStays:false keeps the authored local TRS, so the instance's own
            // position is an offset FROM the bone and (0,0,0) pins it exactly.
            transform.SetParent(bone, false);
        }

        static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), childName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
