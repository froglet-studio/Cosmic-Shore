using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Jacks' bodies (Garrett, 2026-10-21: "lets give it some models and get it ready to ship";
    /// <c>R_VesselActions/JACKS.md</c> §5). Lives on the Jacks root beside its NetworkObject, the way
    /// <see cref="WrasseCrewBodies"/> lives on the Wrasse's.
    ///
    /// <para><b>The two jacks ARE the vessel</b> (Garrett, 2026-10-21: reduce "Marlin and Jacks" to just the jacks, and save
    /// the marlin for a swordsman of its own). Two instances of <c>Jack.fbx</c> under the root are drawn where the
    /// <see cref="JacksExecutor"/> puts them: while HERDING, on the board where the thumbs run them, facing the way they
    /// swim, the spine waving with their speed, at <see cref="jackModelScale"/> (sized for the top-down view); while FLYING,
    /// beside the hull under the two thumbs, facing the hull's heading and rolling with it, at
    /// <see cref="flightModelScale"/> (sized for the chase camera). The Rhino's rig the hull was cloned from stays in the
    /// prefab, switched off: its colliders and the puppetry's bones are the hull's. Element morphs need nothing here: the
    /// hull's <see cref="VesselAnimation"/> drives every element-labelled shape key under the root, the jacks' included.</para>
    ///
    /// <para>The executor runs the jacks only on the machine that owns the hull (the player's, or the host for an AI), so
    /// that machine publishes both jacks, the decoy, the call and the mode here - herding as offsets from the hull in world
    /// axes, flying as places in the hull's own frame (so the formation turns with the hull between ticks) - quantized,
    /// and every other peer draws them there, eased. The executor on a non-owning peer reads them back
    /// (<see cref="TryGetRemoteCrew"/>), so the host steers the herd with the client's real jacks and decoy.</para>
    ///
    /// <para>Bones and models are found BY NAME (vessel-model contract: FBX sub-object ids are not derivable offline); a
    /// missing model leaves its half of this component idle.</para>
    /// </summary>
    public sealed class JacksBodies : NetworkBehaviour, IHullContactGate
    {
        /// <summary>The hull is invisible: once the jacks are seated its touch leaves crystals and prisms alone in EITHER mode
        /// (Garrett, 2026-10-25: "lets allow them to net the omicrystal and move it around", "i don't see prism moving i just
        /// see them getting destroy"), because the fish and the net are the Jacks' body - the fish collect what they touch,
        /// the net carries, drags and lights. Herding the hull is parked out of sight, and an unseen hull taking crystals
        /// or ramming trails was a hull nobody could see doing it. Read on every peer from the replicated seat.</summary>
        public bool TakesCrystalsByContact => !_jacksShown;

        /// <inheritdoc cref="TakesCrystalsByContact"/>
        public bool TakesPrismsByContact => !_jacksShown;

        // Offsets are quantized before they are written, so jacks at rest do not dirty the variables every tick.
        const float OffsetQuantum = 0.25f;
        const byte DecoyBit = 1, CallingBit = 2, SeatedBit = 4, FlyingBit = 8, PlaneBit = 16, DecoyBBit = 32;

        [Header("Models (found by name under this hull)")]
        [Tooltip("Name of the Rhino rig the hull was cloned from (left in the prefab, switched off: the puppetry still turns " +
                 "its bones, and the hull's colliders are its parts').")]
        [SerializeField] string replacedModelName = "rhino_shapekey_with_animations";
        [Tooltip("Names of the two jack model roots (Jack.fbx nested under the hull), in the executor's jack order: the " +
                 "left jack first.")]
        [SerializeField] string[] jackModelNames = { "Jack A", "Jack B" };
        [Tooltip("Herding: world size of a jack model relative to its authored 16 units, whatever the hull's own scale. Sized " +
                 "to read in the herding view (JacksConfigSO.ViewSpan across the short side).")]
        [SerializeField, Min(0.01f)] float jackModelScale = 2.5f;
        [Tooltip("Flying: world size of a jack model relative to its authored 16 units. Sized for the chase camera.")]
        [SerializeField, Min(0.01f)] float flightModelScale = 1f;
        [Tooltip("How fast a jack's size eases between its herding and flying sizes (per second).")]
        [SerializeField, Min(0.1f)] float modelScaleEase = 4f;
        [Tooltip("Rotation (degrees) from a jack's swim frame (+Z along its heading, +Y the board's up) to the model's own " +
                 "frame. Zero when the model's nose is +Z and its back +Y in Unity.")]
        [SerializeField] Vector3 jackModelEuler = Vector3.zero;

        [Header("Jack swim")]
        [Tooltip("Swim beats per second with a jack at rest.")]
        [SerializeField, Min(0f)] float jackSwimHz = 1.0f;
        [Tooltip("Extra beats per second per 100 u/s of a jack's speed (across the board, or the hull's while flying).")]
        [SerializeField, Min(0f)] float jackSwimHzPer100 = 0.8f;
        [Tooltip("Flying: the beat stops rising at this speed (u/s), so a boost does not blur the tail.")]
        [SerializeField, Min(1f)] float flightSwimSpeedCap = 200f;

        [Header("The decoys (Garrett, 2026-10-25: \"Let's have the decoy look like a third jack\")")]
        [Tooltip("How fast a decoy's jack grows in where it is laid, and shrinks away where it was (per second): a re-laid " +
                 "decoy shrinks out of its old place and grows into the new one, so nothing pops.")]
        [SerializeField, Min(0.1f)] float decoyGrowRate = 5f;
        [Tooltip("A decoy that moves further than this at once (u) is re-laid (shrinks out, grows in) rather than slid.")]
        [SerializeField, Min(0f)] float decoyRelayDistance = 8f;
        [Tooltip("Half-amplitude (degrees) of the body wave at spine.1, spine.2 and tail.")]
        [SerializeField] Vector3 jackSwimDegrees = new(6f, 12f, 22f);
        [Tooltip("Phase each bone lags the one before it, in beats.")]
        [SerializeField, Range(0f, 0.5f)] float jackSwimLagBeats = 0.18f;
        [Tooltip("Half-amplitude (degrees) of the pectoral fins' flap.")]
        [SerializeField] float jackFinFlapDegrees = 22f;
        [Tooltip("How fast a jack's heading turns toward the way it swims (per second).")]
        [SerializeField, Min(0.1f)] float jackTurnRate = 8f;
        [Tooltip("A herding jack slower than this (u/s) keeps its heading instead of turning to its drift.")]
        [SerializeField, Min(0f)] float jackHeadingMinSpeed = 6f;

        [Header("The near-field skimmer (the Rhino's sword capsule, hidden)")]
        [Tooltip("Name of the hull's skimmer (the Rhino's sword capsule): hidden, and made a small sphere at the hull, so the " +
                 "net, not a blade, is what reaches out (the jacks' crystals are collected through it).")]
        [SerializeField] string skimmerName = "ForceFieldSkimmer";
        [Tooltip("The near-field skimmer's radius round the hull (u).")]
        [SerializeField, Min(0.1f)] float skimmerRadius = 4f;

        [Header("The net (Garrett, 2026-10-24: \"lets make it a net that opens wider with xdiff\")")]
        [Tooltip("The net's strands. Empty: the jacks' own domain material (the hull's ship material, lit and opaque, so the " +
                 "net reads from every angle).")]
        [SerializeField] Material netMaterial;
        [Tooltip("Radius of one strand (u): each strand is a three-sided tube, a lit and faceted volume.")]
        [SerializeField, Min(0.02f)] float netStrandRadius = 0.3f;
        [Tooltip("Strands across the net (running from one fish to the other), the rim's two included.")]
        [SerializeField, Range(2, 12)] int netRows = 5;
        [Tooltip("Strands down the net (running from the mouth's top to its bottom through the bag), the fish's two included.")]
        [SerializeField, Range(2, 16)] int netColumns = 7;
        [Tooltip("Points along each strand.")]
        [SerializeField, Range(3, 24)] int netSamples = 10;
        [Tooltip("How fast the net grows open as the jacks take off, and shrinks away as they land (1/s): nothing pops in " +
                 "or out (the continuity law).")]
        [SerializeField, Min(0.1f)] float netGrowRate = 4f;

        [Header("Network")]
        [Tooltip("How fast another peer's copy of the jacks closes on the published places (per second).")]
        [SerializeField, Min(0.1f)] float remoteSmoothing = 10f;

        readonly NetworkVariable<byte> n_flags = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        // herding: the jacks' and the decoy's positions minus the hull's, in world axes; flying: the jacks' places in the
        // hull's frame (the decoy stays a world offset)
        readonly NetworkVariable<Vector3> n_jackA = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_jackB = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_decoy = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        // the second decoy (Time level 5, 2026-10-25), a world offset like the first
        readonly NetworkVariable<Vector3> n_decoyB = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>True where the jacks are DECIDED: the owning machine, or anywhere while not network-spawned.</summary>
        bool IsAuthority => !IsSpawned || IsOwner;

        JacksExecutor _crew;
        IVesselStatus _status;

        readonly Jack[] _jacks = new Jack[2];
        // each decoy is drawn as a third jack (a fourth with Time 5's second): a copy of the first jack's model, swimming in place
        readonly Jack[] _decoyJacks = new Jack[2];
        readonly float[] _decoyGrow = new float[2];
        readonly Vector3[] _decoyShown = new Vector3[2];
        Transform _skimmer;
        bool _skimmerSeated;
        NetMesh _net;
        float _netGrow;
        JacksMath.NetFrame _netShown;
        JacksNet.Swing _netTrail;
        bool _jacksShown, _snap = true, _flying;
        Vector3 _decoyOffset, _decoyOffsetB, _prevHullPosition;
        float _hullSpeed;

        sealed class Jack
        {
            public Transform T;
            public SkinnedMeshRenderer Renderer;
            public Vector3 RestScale;
            public Bone[] Spine;
            public Bone FinL, FinR;
            public Vector3 Offset, PrevOffset;   // from the hull, world axes (a flying place is turned into this each frame)
            public Vector3 Local;                // flying: the place in the hull's frame, eased on a remote peer
            public Vector3 Heading = Vector3.forward;
            public float Phase, Scale;
            public Material Painted;
        }

        struct Bone
        {
            public Transform T;
            public Quaternion Rest;
            public Vector3 Up, Right, Forward;    // the body's axes, in the bone's local frame at rest

            public static Bone Find(Transform root, string name, Quaternion bodyFrame)
            {
                var t = FindDeep(root, name);
                if (!t) return default;
                var toLocal = Quaternion.Inverse(t.rotation);
                return new Bone
                {
                    T = t, Rest = t.localRotation,
                    Up = toLocal * (bodyFrame * Vector3.up),
                    Right = toLocal * (bodyFrame * Vector3.right),
                    Forward = toLocal * (bodyFrame * Vector3.forward),
                };
            }

            /// <summary>Rest, then yaw (about the body's up), pitch (about its right) and roll (about its nose), degrees.</summary>
            public void Pose(float yaw, float pitch, float roll)
            {
                if (!T) return;
                T.localRotation = Rest * Quaternion.AngleAxis(yaw, Up) * Quaternion.AngleAxis(pitch, Right)
                                       * Quaternion.AngleAxis(roll, Forward);
            }
        }

        void Awake()
        {
            _crew = GetComponentInChildren<JacksExecutor>(true);
            if (TryGetComponent(out IVessel vessel)) _status = vessel.VesselStatus;
            int layer = HideReplacedRig();
            ResolveJacks(layer);
            HideSkimmer();
            _net = new NetMesh(this, layer);
        }

        public override void OnDestroy()
        {
            _net?.Dispose();
            base.OnDestroy();
        }

        /// <summary>The Rhino's rig is switched off (its bones stay: the puppetry still turns them). Returns its renderer's
        /// layer, the hull's drawn layer, for the jacks.</summary>
        int HideReplacedRig()
        {
            var replaced = FindDeep(transform, replacedModelName);
            if (!replaced) return gameObject.layer;
            var r = replaced.GetComponentInChildren<Renderer>(true);
            int layer = r ? r.gameObject.layer : gameObject.layer;
            replaced.gameObject.SetActive(false);
            return layer;
        }

        void ResolveJacks(int layer)
        {
            for (int k = 0; k < _jacks.Length && k < jackModelNames.Length; k++)
            {
                var t = FindDeep(transform, jackModelNames[k]);
                if (!t) continue;
                _jacks[k] = MakeJack(t, 0.37f * k);
                t.gameObject.SetActive(false);
                foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
            }
            // the decoys: copies of the first jack's model, drawn the same way (Garrett, 2026-10-25: "Let's have the decoy look
            // like a third jack"). A copy carries no collider: a decoy is a herder, never a body that touches anything.
            var source = _jacks[0];
            for (int s = 0; s < _decoyJacks.Length && source != null; s++)
            {
                var copy = Instantiate(source.T.gameObject, source.T.parent, false);
                copy.name = s == 0 ? "Decoy Jack" : "Decoy Jack B";
                foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
                copy.SetActive(false);
                _decoyJacks[s] = MakeJack(copy.transform, 0.61f + 0.29f * s);
                _decoyJacks[s].RestScale = source.RestScale;
            }
            // The puppetry's flares and its renderer reads land on a drawn jack, not on the hidden rig. Set before the vessel
            // initializes its animation (which keeps a renderer it was given).
            var anim = GetComponentInChildren<VesselAnimation>(true);
            if (anim && _jacks[0] != null && _jacks[0].Renderer) anim.SkinnedMeshRenderer = _jacks[0].Renderer;
        }

        Jack MakeJack(Transform t, float phase)
        {
            var bodyFrame = t.rotation * Quaternion.Inverse(Quaternion.Euler(jackModelEuler));
            var j = new Jack
            {
                T = t,
                Renderer = t.GetComponentInChildren<SkinnedMeshRenderer>(true),
                RestScale = t.localScale,
                Spine = new[] { Bone.Find(t, "spine.1", bodyFrame), Bone.Find(t, "spine.2", bodyFrame), Bone.Find(t, "tail", bodyFrame) },
                FinL = Bone.Find(t, "fin.L", bodyFrame),
                FinR = Bone.Find(t, "fin.R", bodyFrame),
                Phase = phase,
                Scale = flightModelScale,
            };
            if (j.Renderer) j.Renderer.updateWhenOffscreen = true;
            return j;
        }

        // ------------------------------------------------------------------ read by the executor on a non-owning peer

        /// <summary>
        /// The owner's jacks, decoy, call and mode, as world positions on this peer. False where this machine decides the
        /// jacks itself (the owner, or unspawned), or before the owner has published them.
        /// </summary>
        internal bool TryGetRemoteCrew(out Vector3 jackA, out Vector3 jackB, out bool hasDecoy, out Vector3 decoy,
                                       out bool hasDecoyB, out Vector3 decoyB, out bool calling, out bool flying, out bool plane)
        {
            byte flags = n_flags.Value;
            var p = transform.position;
            flying = (flags & FlyingBit) != 0;
            jackA = flying ? p + transform.rotation * n_jackA.Value : p + n_jackA.Value;
            jackB = flying ? p + transform.rotation * n_jackB.Value : p + n_jackB.Value;
            decoy = p + n_decoy.Value;
            hasDecoy = (flags & DecoyBit) != 0;
            decoyB = p + n_decoyB.Value;
            hasDecoyB = (flags & DecoyBBit) != 0;
            calling = (flags & CallingBit) != 0;
            plane = (flags & PlaneBit) != 0;
            return !IsAuthority && (flags & SeatedBit) != 0;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            MeasureHull(dt);
            UpdateJacks(dt);
        }

        void MeasureHull(float dt)
        {
            var pos = transform.position;
            float a = 1f - Mathf.Exp(-6f * dt);
            _hullSpeed = _snap ? 0f : Mathf.Lerp(_hullSpeed, (pos - _prevHullPosition).magnitude / dt, a);
            _prevHullPosition = pos;
        }

        // ------------------------------------------------------------------ the jacks

        void UpdateJacks(float dt)
        {
            bool seated;
            var rot = transform.rotation;
            if (IsAuthority)
            {
                seated = _crew && _crew.isActiveAndEnabled && _crew.Config;
                bool flying = seated && !_crew.Herding;
                bool calling = seated && _crew.Calling;
                bool plane = seated && _crew.PlaneAttract;
                bool decoy = false, decoyB = false;
                if (seated && _crew.TryGetDecoy(0, out var at))
                {
                    decoy = true;
                    _decoyOffset = at - transform.position;
                }
                if (seated && _crew.TryGetDecoy(1, out var atB))
                {
                    decoyB = true;
                    _decoyOffsetB = atB - transform.position;
                }
                Vector3 a = Vector3.zero, b = Vector3.zero;
                if (seated)
                {
                    a = flying ? _crew.JackLocal(0) : _crew.JackWorld(0) - transform.position;
                    b = flying ? _crew.JackLocal(1) : _crew.JackWorld(1) - transform.position;
                }
                Publish(seated, flying, calling, plane, decoy, decoyB, a, b);
                SetMode(flying);
                Place(0, a, true, rot);
                Place(1, b, true, rot);
            }
            else
            {
                byte flags = n_flags.Value;
                seated = (flags & SeatedBit) != 0;
                SetMode((flags & FlyingBit) != 0);
                Place(0, n_jackA.Value, false, rot);
                Place(1, n_jackB.Value, false, rot);
            }

            if (seated != _jacksShown)
            {
                _jacksShown = seated;
                foreach (var j in _jacks) if (j != null) j.T.gameObject.SetActive(seated);
                _snap = true;
            }
            SeatSkimmer();
            if (!seated)
            {
                _netGrow = 0f;
                _net?.Hide();
                UpdateDecoys(false, transform.up, dt);
                return;
            }

            var up = _flying ? transform.up : _crew && _crew.Config ? _crew.BoardUp : transform.up;
            for (int k = 0; k < _jacks.Length; k++)
                if (_jacks[k] != null) Swim(_jacks[k], up, dt);
            UpdateDecoys(seated, _crew && _crew.Config ? _crew.BoardUp : up, dt);   // the decoys lie on the board
            DrawNet();
            _snap = false;
        }

        /// <summary>A change of mode: a flying place and a herding offset are different frames, so the eased copies restart
        /// from where the jacks are drawn now.</summary>
        void SetMode(bool flying)
        {
            if (flying == _flying) return;
            _flying = flying;
            var toLocal = Quaternion.Inverse(transform.rotation);
            foreach (var j in _jacks)
                if (j != null) j.Local = toLocal * j.Offset;
        }

        void Place(int k, Vector3 value, bool exact, Quaternion hullRotation)
        {
            var j = _jacks[k];
            if (j == null) return;
            float a = exact || _snap ? 1f : 1f - Mathf.Exp(-remoteSmoothing * Time.deltaTime);
            if (_flying)
            {
                j.Local = Vector3.Lerp(j.Local, value, a);
                j.Offset = hullRotation * j.Local;
            }
            else j.Offset = Vector3.Lerp(j.Offset, value, a);
        }

        void Swim(Jack j, Vector3 up, float dt)
        {
            var step = j.Offset - j.PrevOffset;
            j.PrevOffset = j.Offset;
            float turn = 0f, speed;
            Quaternion facing;
            if (_flying)
            {
                // beside the hull: its heading and its roll, the swim beating with the hull's speed
                speed = Mathf.Min(_hullSpeed, flightSwimSpeedCap);
                j.Heading = transform.forward;
                facing = transform.rotation;
            }
            else
            {
                speed = _snap ? 0f : step.magnitude / dt;
                var flat = Vector3.ProjectOnPlane(step, up);
                var heading = Vector3.ProjectOnPlane(j.Heading, up);
                if (heading.sqrMagnitude < 1e-6f) heading = Vector3.ProjectOnPlane(transform.forward, up);
                if (heading.sqrMagnitude < 1e-6f) heading = Vector3.ProjectOnPlane(transform.up, up);
                heading.Normalize();
                if (!_snap && speed > jackHeadingMinSpeed && flat.sqrMagnitude > 1e-8f)
                {
                    var want = flat.normalized;
                    var next = Vector3.Slerp(heading, want, 1f - Mathf.Exp(-jackTurnRate * dt)).normalized;
                    turn = Vector3.SignedAngle(heading, next, up) / dt;
                    heading = next;
                }
                j.Heading = heading;
                facing = Quaternion.LookRotation(heading, up);
            }

            Pose(j, transform.position + j.Offset, facing, speed, turn, 1f, dt);
        }

        /// <summary>A drawn jack at <paramref name="position"/>, facing <paramref name="facing"/> (its swim frame), at its
        /// mode's size times <paramref name="grow"/>, its body beating with <paramref name="speed"/> and bent by
        /// <paramref name="turn"/> (degrees per second). The two jacks and the decoys alike.</summary>
        void Pose(Jack j, Vector3 position, Quaternion facing, float speed, float turn, float grow, float dt)
        {
            float wantScale = _flying ? flightModelScale : jackModelScale;
            j.Scale = _snap ? wantScale : Mathf.Lerp(j.Scale, wantScale, 1f - Mathf.Exp(-modelScaleEase * dt));
            float parentScale = j.T.parent ? Mathf.Max(1e-4f, j.T.parent.lossyScale.x) : 1f;
            j.T.localScale = j.RestScale * (j.Scale * grow / parentScale);
            j.T.SetPositionAndRotation(position, facing * Quaternion.Euler(jackModelEuler));
            Paint(j);

            j.Phase = Mathf.Repeat(j.Phase + (jackSwimHz + jackSwimHzPer100 * speed * 0.01f) * dt, 1f);
            // the body bends with the turn, as WrasseCrewBodies bends the kite
            float bend = Mathf.Clamp(turn / 360f, -1f, 1f) * 18f;
            for (int i = 0; i < j.Spine.Length; i++)
            {
                float wave = Mathf.Sin((j.Phase - i * jackSwimLagBeats) * 2f * Mathf.PI) * jackSwimDegrees[i];
                j.Spine[i].Pose(wave + bend * (i + 1) / j.Spine.Length, 0f, 0f);
            }
            float flap = Mathf.Sin((j.Phase + 0.25f) * 2f * Mathf.PI) * jackFinFlapDegrees;
            j.FinL.Pose(0f, 0f, -flap);
            j.FinR.Pose(0f, 0f, flap);
        }

        /// <summary>
        /// Each decoy down is drawn as a third jack (a fourth with Time 5's second; Garrett, 2026-10-25: "Let's have the decoy
        /// look like a third jack"), swimming in place on the board at the jacks' own size, on every peer: the owner's
        /// decoys here, the published ones elsewhere. Laid, it grows in; re-laid somewhere else, it shrinks out of the old
        /// place and grows into the new one (the continuity law); taken away, it shrinks out.
        /// </summary>
        void UpdateDecoys(bool seated, Vector3 up, float dt)
        {
            byte flags = n_flags.Value;
            for (int s = 0; s < _decoyJacks.Length; s++)
            {
                var j = _decoyJacks[s];
                if (j == null) continue;
                Vector3 at = default;
                bool on = seated && (IsAuthority
                    ? _crew && _crew.TryGetDecoy(s, out at)
                    : (flags & (s == 0 ? DecoyBit : DecoyBBit)) != 0);
                if (on && !IsAuthority) at = transform.position + (s == 0 ? n_decoy.Value : n_decoyB.Value);
                bool relay = on && _decoyGrow[s] > 0f && (at - _decoyShown[s]).sqrMagnitude > decoyRelayDistance * decoyRelayDistance;
                float step = decoyGrowRate * dt;
                if (!on || relay)
                {
                    _decoyGrow[s] = Mathf.MoveTowards(_decoyGrow[s], 0f, step);
                    if (relay && _decoyGrow[s] <= 0f) _decoyShown[s] = at;   // gone from the old place: grow in at the new one
                }
                else
                {
                    if (_decoyGrow[s] <= 0f)
                    {
                        // laid: it faces the way the hull does, on the board
                        _decoyShown[s] = at;
                        var heading = Vector3.ProjectOnPlane(transform.forward, up);
                        j.Heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector3.ProjectOnPlane(transform.up, up).normalized;
                    }
                    else _decoyShown[s] = at;   // a small move (the board settling) is followed
                    _decoyGrow[s] = Mathf.MoveTowards(_decoyGrow[s], 1f, step);
                }
                bool shown = _decoyGrow[s] > 0f;
                if (j.T.gameObject.activeSelf != shown) j.T.gameObject.SetActive(shown);
                if (!shown) continue;
                var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(j.Heading, up).sqrMagnitude > 1e-6f
                    ? Vector3.ProjectOnPlane(j.Heading, up) : transform.forward, up);
                Pose(j, _decoyShown[s], facing, 0f, 0f, Mathf.SmoothStep(0f, 1f, _decoyGrow[s]), dt);
            }
        }

        // ------------------------------------------------------------------ the skimmer and the net
        //
        // Garrett, 2026-10-24: "the spinning jack in the middle doesn't work. lets make it a net that opens wider with xdiff".
        // The Rhino's sword capsule stays the hull's near-field skimmer (the crystals the jacks collect go through it), hidden
        // and made a small sphere at the hull; the net between the fish is drawn here, on every peer, from the drawn jacks.
        // What the net catches is the executor's (JacksNet).

        void HideSkimmer()
        {
            _skimmer = FindDeep(transform, skimmerName);
            if (!_skimmer) return;
            if (_skimmer.TryGetComponent(out Skimmer skimmer)) skimmer.HasExternalScaleDriver = true;   // its size is ours
            foreach (var r in _skimmer.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        // once the hull's scale is final: a sphere of skimmerRadius at the hull (the capsule shortened to its two caps)
        void SeatSkimmer()
        {
            if (_skimmerSeated || !_skimmer) return;
            _skimmerSeated = true;
            float parentScale = _skimmer.parent ? Mathf.Max(1e-4f, _skimmer.parent.lossyScale.x) : 1f;
            _skimmer.localPosition = _skimmer.parent ? _skimmer.parent.InverseTransformPoint(transform.position) : Vector3.zero;
            _skimmer.localRotation = Quaternion.identity;
            _skimmer.localScale = Vector3.one * (2f * skimmerRadius / parentScale);
            foreach (var c in _skimmer.GetComponents<CapsuleCollider>())
            {
                c.center = Vector3.zero;
                c.radius = 0.5f;
                c.height = 1f;
            }
        }

        void DrawNet()
        {
            if (_net == null) return;
            bool want = _flying && _crew && _crew.Config && _jacks[0] != null && _jacks[1] != null;
            if (want)
            {
                Vector3 a = transform.position + _jacks[0].Offset, b = transform.position + _jacks[1].Offset;
                want = JacksNet.Formed(_crew.Config, a, b);
                if (want)
                {
                    var bag = _netTrail.Step(_crew.Config, transform.forward, Time.deltaTime);
                    _netShown = JacksNet.FrameFor(_crew.Config, a, b, transform.forward, transform.up, bag);
                }
                else _netTrail.Reset();
            }
            else _netTrail.Reset();
            // grows open while it is wanted and shrinks away (where it last hung) when it is not
            _netGrow = Mathf.MoveTowards(_netGrow, want ? 1f : 0f, netGrowRate * Time.deltaTime);
            if (_netGrow <= 0f)
            {
                _net.Hide();
                return;
            }
            var frame = _netShown;
            float grow = Mathf.SmoothStep(0f, 1f, _netGrow);
            frame.Height *= grow;
            frame.Depth *= grow;
            _net.Draw(frame, netMaterial ? netMaterial : _status?.ShipMaterial, grow);
        }

        /// <summary>
        /// The net's strands as one mesh in the world: rows from one fish to the other and columns from the mouth's top to
        /// its bottom through the bag, each a three-sided tube (a lit, faceted volume - never a flat line; the shaders doc's
        /// LOCKED rule). Rebuilt in place each frame it shows (no allocation); hidden while the jacks herd.
        /// </summary>
        sealed class NetMesh
        {
            readonly JacksBodies _owner;
            readonly GameObject _go;
            readonly MeshRenderer _renderer;
            readonly Mesh _mesh;
            readonly Vector3[] _verts, _normals;
            readonly Vector3[] _line;
            readonly int _rows, _cols, _samples;
            bool _shown;

            internal NetMesh(JacksBodies owner, int layer)
            {
                _owner = owner;
                _rows = owner.netRows;
                _cols = owner.netColumns;
                _samples = owner.netSamples;
                int lines = _rows + _cols, rings = lines * _samples;
                _verts = new Vector3[rings * 3];
                _normals = new Vector3[rings * 3];
                _line = new Vector3[_samples];
                var tris = new int[lines * (_samples - 1) * 18];
                int ti = 0;
                for (int l = 0; l < lines; l++)
                    for (int i = 0; i < _samples - 1; i++)
                    {
                        int r0 = (l * _samples + i) * 3, r1 = r0 + 3;
                        for (int k = 0; k < 3; k++)
                        {
                            int k1 = (k + 1) % 3;
                            // wound so each face's front is OUTSIDE the tube (Unity's front is Cross(b - a, c - a)); the
                            // first cut faced in, so the net vanished wherever only its inner faces were turned to the camera
                            tris[ti++] = r0 + k; tris[ti++] = r1 + k1; tris[ti++] = r1 + k;
                            tris[ti++] = r0 + k; tris[ti++] = r0 + k1; tris[ti++] = r1 + k1;
                        }
                    }
                _mesh = new Mesh { name = "JacksNet" };
                _mesh.MarkDynamic();
                _mesh.vertices = _verts;
                _mesh.normals = _normals;
                _mesh.triangles = tris;
                _go = new GameObject("JacksNet") { layer = layer };   // at the scene root: its vertices are in the world
                _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _renderer = _go.AddComponent<MeshRenderer>();
                _renderer.enabled = false;
            }

            internal void Draw(JacksMath.NetFrame f, Material material, float grow)
            {
                if (material && _renderer.sharedMaterial != material) _renderer.sharedMaterial = material;
                float radius = _owner.netStrandRadius * grow;
                int ring = 0;
                for (int r = 0; r < _rows; r++)
                {
                    float tt = Mathf.Lerp(-1f, 1f, r / (_rows - 1f));
                    for (int i = 0; i < _samples; i++) _line[i] = f.Point(i / (_samples - 1f), tt, 1f);
                    Tube(ref ring, f, radius);
                }
                for (int c = 0; c < _cols; c++)
                {
                    float s = c / (_cols - 1f);
                    for (int i = 0; i < _samples; i++) _line[i] = f.Point(s, Mathf.Lerp(-1f, 1f, i / (_samples - 1f)), 1f);
                    Tube(ref ring, f, radius);
                }
                _mesh.vertices = _verts;
                _mesh.normals = _normals;
                _mesh.RecalculateBounds();
                if (!_shown) { _renderer.enabled = true; _shown = true; }
            }

            void Tube(ref int ring, JacksMath.NetFrame f, float radius)
            {
                for (int i = 0; i < _samples; i++, ring++)
                {
                    var tangent = _line[Mathf.Min(i + 1, _samples - 1)] - _line[Mathf.Max(i - 1, 0)];
                    if (tangent.sqrMagnitude < 1e-8f) tangent = f.X;
                    tangent.Normalize();
                    var n1 = Vector3.Cross(tangent, f.Normal);
                    if (n1.sqrMagnitude < 1e-6f) n1 = Vector3.Cross(tangent, f.Y);
                    n1.Normalize();
                    var n2 = Vector3.Cross(tangent, n1);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * (2f * Mathf.PI / 3f);
                        var n = n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a);
                        _verts[ring * 3 + k] = _line[i] + n * radius;
                        _normals[ring * 3 + k] = n;
                    }
                }
            }

            internal void Hide()
            {
                if (!_shown) return;
                _renderer.enabled = false;
                _shown = false;
            }

            internal void Dispose()
            {
                if (_go) Object.Destroy(_go);
                if (_mesh) Object.Destroy(_mesh);
            }
        }

        void Publish(bool seated, bool flying, bool calling, bool plane, bool decoy, bool decoyB, Vector3 a, Vector3 b)
        {
            if (!IsSpawned || !IsOwner) return;
            byte flags = (byte)((seated ? SeatedBit : 0) | (flying ? FlyingBit : 0) | (calling ? CallingBit : 0)
                                | (plane ? PlaneBit : 0) | (decoy ? DecoyBit : 0) | (decoyB ? DecoyBBit : 0));
            if (flags != n_flags.Value) n_flags.Value = flags;
            if (!seated) return;
            Write(n_jackA, a);
            Write(n_jackB, b);
            if (decoy) Write(n_decoy, _decoyOffset);
            if (decoyB) Write(n_decoyB, _decoyOffsetB);
        }

        static void Write(NetworkVariable<Vector3> v, Vector3 value)
        {
            var q = new Vector3(Quantize(value.x), Quantize(value.y), Quantize(value.z));
            if ((q - v.Value).sqrMagnitude > OffsetQuantum * OffsetQuantum * 0.25f) v.Value = q;
        }

        static float Quantize(float v) => Mathf.Round(v / OffsetQuantum) * OffsetQuantum;

        // The domain paint, on a jack's slot 0 (the fleet's Domain slot), following the hull's material.
        void Paint(Jack j)
        {
            var mat = _status?.ShipMaterial;
            if (!mat || mat == j.Painted || !j.Renderer) return;
            var mats = j.Renderer.sharedMaterials;
            if (mats.Length == 0) return;
            mats[0] = mat;
            j.Renderer.sharedMaterials = mats;
            j.Painted = mat;
        }

        // ------------------------------------------------------------------ helpers

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit) return hit;
            }
            return null;
        }
    }
}
