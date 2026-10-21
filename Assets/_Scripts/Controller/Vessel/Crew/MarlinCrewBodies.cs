using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marlin and Jacks' bodies (Garrett, 2026-10-21: "lets give it some models and get it ready to ship";
    /// <c>R_VesselActions/MARLIN_JACKS.md</c> §5). Lives on the Marlin root beside its NetworkObject, the way
    /// <see cref="WrasseCrewBodies"/> lives on the Wrasse's.
    ///
    /// <para><b>The marlin</b> (model <c>Marlin.fbx</c>, nested under the Rhino's fuselage part so it rides every turn the
    /// hull's <see cref="RhinoAnimation"/> gives it) is the vessel's drawn geometry; the Rhino's rig stays in the prefab,
    /// inactive, because the puppetry still turns its bones. It is puppeted from the hull's MEASURED motion, so it reads
    /// the same on every peer: the tail beats faster with speed and bends out of a turn, the pectoral fins follow the
    /// Rhino's front plates, and the sail stands up while the marlin CALLS (the herd's tell, replicated) and folds back
    /// at boost. Its element morphs need nothing here: the hull's <see cref="VesselAnimation"/> drives every
    /// element-labelled shape key under the root, these models' included.</para>
    ///
    /// <para><b>The jacks</b> (two instances of <c>Jack.fbx</c> under the root) are drawn where the
    /// <see cref="MarlinJacksExecutor"/> runs them, facing the way they swim on the board, the spine waving with their
    /// speed. The executor runs the jacks only on the machine that owns the hull (the pilot's, or the host for an AI), so
    /// that machine publishes both jacks, the decoy and the call here - offsets from the hull, quantized - and every other
    /// peer draws them there, eased. The executor on a non-owning peer reads them back
    /// (<see cref="TryGetRemoteCrew"/>), so the host steers the herd with the client's real jacks and decoy.</para>
    ///
    /// <para>Bones and models are found BY NAME (vessel-model contract: FBX sub-object ids are not derivable offline); a
    /// missing model leaves its half of this component idle and the old look in place.</para>
    /// </summary>
    public sealed class MarlinCrewBodies : NetworkBehaviour
    {
        // Offsets are quantized before they are written, so jacks at rest do not dirty the variables every tick.
        const float OffsetQuantum = 0.25f;
        const byte DecoyBit = 1, CallingBit = 2, SeatedBit = 4;

        [Header("Models (found by name under this hull)")]
        [Tooltip("Name of the marlin's model root (Marlin.fbx nested under the Rhino's fuselage part).")]
        [SerializeField] string marlinModelName = "Marlin";
        [Tooltip("Name of the Rhino rig it replaces (left in the prefab, inactive: the puppetry still turns its bones).")]
        [SerializeField] string replacedModelName = "rhino_shapekey_with_animations";
        [Tooltip("Names of the two jack model roots (Jack.fbx nested under the hull), in the executor's jack order.")]
        [SerializeField] string[] jackModelNames = { "Jack A", "Jack B" };
        [Tooltip("World size of a jack model relative to its authored 16 units, whatever the hull's own scale. Sized to " +
                 "read in the jacks' view (viewSpan across the short side).")]
        [SerializeField, Min(0.01f)] float jackModelScale = 2.5f;
        [Tooltip("Rotation (degrees) from a jack's swim frame (+Z along its heading, +Y the board's up) to the model's own " +
                 "frame. Zero when the model's nose is +Z and its back +Y in Unity.")]
        [SerializeField] Vector3 jackModelEuler = Vector3.zero;

        [Header("Jack swim")]
        [Tooltip("Swim beats per second with a jack at rest on the board.")]
        [SerializeField, Min(0f)] float jackSwimHz = 1.0f;
        [Tooltip("Extra beats per second per 100 u/s of a jack's speed across the board.")]
        [SerializeField, Min(0f)] float jackSwimHzPer100 = 0.8f;
        [Tooltip("Half-amplitude (degrees) of the body wave at spine.1, spine.2 and tail.")]
        [SerializeField] Vector3 jackSwimDegrees = new(6f, 12f, 22f);
        [Tooltip("Phase each bone lags the one before it, in beats.")]
        [SerializeField, Range(0f, 0.5f)] float jackSwimLagBeats = 0.18f;
        [Tooltip("Half-amplitude (degrees) of the pectoral fins' flap.")]
        [SerializeField] float jackFinFlapDegrees = 22f;
        [Tooltip("How fast a jack's heading turns toward the way it swims (per second).")]
        [SerializeField, Min(0.1f)] float jackTurnRate = 8f;
        [Tooltip("A jack slower than this (u/s) keeps its heading instead of turning to its drift.")]
        [SerializeField, Min(0f)] float jackHeadingMinSpeed = 6f;

        [Header("Marlin puppetry")]
        [Tooltip("The hull's cruise speed (u/s): the tail's beat rate and the boost fold are measured against it.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 60f;
        [Tooltip("The hull's boosted speed (u/s): the sail is fully folded here.")]
        [SerializeField, Min(1f)] float boostSpeed = 260f;
        [Tooltip("Tail beats per second at a standstill and at cruise.")]
        [SerializeField] Vector2 tailHz = new(0.6f, 1.6f);
        [Tooltip("Half-amplitude (degrees) of the tail's beat at tail.1 and tail.2.")]
        [SerializeField] Vector2 tailDegrees = new(6f, 14f);
        [Tooltip("Degrees the tail bends out of a turn at the yaw rate below.")]
        [SerializeField] float turnBendDegrees = 20f;
        [Tooltip("Yaw rate (degrees per second) that bends the tail all the way.")]
        [SerializeField, Min(1f)] float fullYawRate = 110f;
        [Tooltip("How much of the Rhino front plates' turn the pectoral fins follow (0 holds them at rest).")]
        [SerializeField, Range(0f, 1f)] float finFollow = 0.6f;
        [Tooltip("Degrees the sail stands up (forward) while the marlin calls the herd.")]
        [SerializeField] float sailCallDegrees = 14f;
        [Tooltip("Degrees the sail folds back at boost speed.")]
        [SerializeField] float sailBoostFoldDegrees = 40f;
        [Tooltip("How fast the sail moves toward its pose (per second).")]
        [SerializeField, Min(0.1f)] float sailRate = 5f;

        [Header("Network")]
        [Tooltip("How fast another peer's copy of the jacks closes on the published offsets (per second).")]
        [SerializeField, Min(0.1f)] float remoteSmoothing = 10f;

        readonly NetworkVariable<byte> n_flags = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        // the jacks' and the decoy's positions minus the hull's, in world axes
        readonly NetworkVariable<Vector3> n_jackA = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_jackB = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_decoy = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>True where the crew is DECIDED: the owning machine, or anywhere while not network-spawned.</summary>
        bool IsAuthority => !IsSpawned || IsOwner;

        MarlinJacksExecutor _crew;
        IVesselStatus _status;
        SkinnedMeshRenderer _hullRenderer;

        // the marlin
        Transform _marlin;
        Bone _sail, _finL, _finR, _tail1, _tail2;
        Transform _plateL, _plateR;
        Quaternion _plateLRest, _plateRRest;
        Vector3 _prevHullPosition, _prevHullForward;
        bool _hullPrevValid;
        float _hullSpeed, _hullYawRate, _tailPhase, _sailAngle;

        // the jacks
        readonly Jack[] _jacks = new Jack[2];
        bool _jacksShown, _snap = true;
        Vector3 _decoyOffset;

        sealed class Jack
        {
            public Transform T;
            public SkinnedMeshRenderer Renderer;
            public Vector3 RestScale;
            public Bone[] Spine;
            public Bone FinL, FinR;
            public Vector3 Offset, PrevOffset;
            public Vector3 Heading = Vector3.forward;
            public float Phase;
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

            /// <summary>Rest, then a turn given in the BODY's frame (a part's rotation away from its own rest).</summary>
            public void PoseBodyTurn(Quaternion bodyTurn, Quaternion bodyFrameInBone)
            {
                if (!T) return;
                T.localRotation = Rest * (bodyFrameInBone * bodyTurn * Quaternion.Inverse(bodyFrameInBone));
            }
        }

        Quaternion _finLBodyFrame, _finRBodyFrame;

        void Awake()
        {
            _crew = GetComponentInChildren<MarlinJacksExecutor>(true);
            if (TryGetComponent(out IVessel vessel)) _status = vessel.VesselStatus;
            ResolveMarlin();
            ResolveJacks();
        }

        void ResolveMarlin()
        {
            _marlin = FindDeep(transform, marlinModelName);
            if (!_marlin) return;
            var anim = GetComponentInChildren<VesselAnimation>(true);
            var marlinRenderer = _marlin.GetComponentInChildren<SkinnedMeshRenderer>(true);
            // The puppetry's flares and its renderer reads land on the drawn model, not on the hidden rig. Set before
            // the vessel initializes its animation (which keeps a renderer it was given).
            if (anim && marlinRenderer) anim.SkinnedMeshRenderer = marlinRenderer;
            if (marlinRenderer) marlinRenderer.updateWhenOffscreen = true;   // the fins and the morphs reach past the bind bounds
            _hullRenderer = marlinRenderer;
            var replaced = FindDeep(transform, replacedModelName);
            if (replaced) replaced.gameObject.SetActive(false);

            // The marlin flies with the hull: its body frame IS the hull's (nose +Z, back +Y).
            var bodyFrame = transform.rotation;
            _sail = Bone.Find(_marlin, "sail", bodyFrame);
            _finL = Bone.Find(_marlin, "fin.L", bodyFrame);
            _finR = Bone.Find(_marlin, "fin.R", bodyFrame);
            _tail1 = Bone.Find(_marlin, "tail.1", bodyFrame);
            _tail2 = Bone.Find(_marlin, "tail.2", bodyFrame);
            _plateL = FindDeep(transform, "Wing front left");
            _plateR = FindDeep(transform, "Wing front right");
            if (_plateL) _plateLRest = Quaternion.Inverse(bodyFrame) * _plateL.rotation;
            if (_plateR) _plateRRest = Quaternion.Inverse(bodyFrame) * _plateR.rotation;
            if (_finL.T) _finLBodyFrame = Quaternion.Inverse(_finL.T.rotation) * bodyFrame;
            if (_finR.T) _finRBodyFrame = Quaternion.Inverse(_finR.T.rotation) * bodyFrame;
        }

        void ResolveJacks()
        {
            for (int k = 0; k < _jacks.Length && k < jackModelNames.Length; k++)
            {
                var t = FindDeep(transform, jackModelNames[k]);
                if (!t) continue;
                var bodyFrame = t.rotation * Quaternion.Inverse(Quaternion.Euler(jackModelEuler));
                _jacks[k] = new Jack
                {
                    T = t,
                    Renderer = t.GetComponentInChildren<SkinnedMeshRenderer>(true),
                    RestScale = t.localScale,
                    Spine = new[] { Bone.Find(t, "spine.1", bodyFrame), Bone.Find(t, "spine.2", bodyFrame), Bone.Find(t, "tail", bodyFrame) },
                    FinL = Bone.Find(t, "fin.L", bodyFrame),
                    FinR = Bone.Find(t, "fin.R", bodyFrame),
                    Phase = 0.37f * k,
                };
                if (_jacks[k].Renderer) _jacks[k].Renderer.updateWhenOffscreen = true;
                t.gameObject.SetActive(false);
            }
            int layer = _hullRenderer ? _hullRenderer.gameObject.layer : gameObject.layer;
            foreach (var j in _jacks)
                if (j != null)
                    foreach (var c in j.T.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
        }

        // ------------------------------------------------------------------ read by the executor on a non-owning peer

        /// <summary>
        /// The owner's jacks, decoy and call, as world positions on this peer. False where this machine decides the crew
        /// itself (the owner, or unspawned), or before the owner has published a seated crew.
        /// </summary>
        internal bool TryGetRemoteCrew(out Vector3 jackA, out Vector3 jackB, out bool hasDecoy, out Vector3 decoy, out bool calling)
        {
            byte flags = n_flags.Value;
            var p = transform.position;
            jackA = p + n_jackA.Value;
            jackB = p + n_jackB.Value;
            decoy = p + n_decoy.Value;
            hasDecoy = (flags & DecoyBit) != 0;
            calling = (flags & CallingBit) != 0;
            return !IsAuthority && (flags & SeatedBit) != 0;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool calling = UpdateJacks(dt);
            if (_marlin) UpdateMarlin(dt, calling);
        }

        // ------------------------------------------------------------------ the jacks

        /// <returns>Whether the marlin is calling the herd (the sail's tell).</returns>
        bool UpdateJacks(float dt)
        {
            bool seated, calling;
            Vector3 a, b;
            if (IsAuthority)
            {
                seated = _crew && _crew.isActiveAndEnabled && _crew.Config;
                calling = seated && _crew.Calling;
                a = seated ? _crew.JackWorld(0) - transform.position : Vector3.zero;
                b = seated ? _crew.JackWorld(1) - transform.position : Vector3.zero;
                bool decoy = false;
                if (seated && _crew.TryGetDecoy(out var at))
                {
                    decoy = true;
                    _decoyOffset = at - transform.position;
                }
                Publish(seated, calling, decoy, a, b, _decoyOffset);
                Place(0, a, true);
                Place(1, b, true);
            }
            else
            {
                byte flags = n_flags.Value;
                seated = (flags & SeatedBit) != 0;
                calling = (flags & CallingBit) != 0;
                Place(0, n_jackA.Value, false);
                Place(1, n_jackB.Value, false);
            }

            if (seated != _jacksShown)
            {
                _jacksShown = seated;
                foreach (var j in _jacks) if (j != null) j.T.gameObject.SetActive(seated);
                _snap = true;
            }
            if (!seated) return calling;

            var up = _crew && _crew.Config ? _crew.BoardUp : transform.up;
            for (int k = 0; k < _jacks.Length; k++)
                if (_jacks[k] != null) Swim(_jacks[k], up, dt);
            _snap = false;
            return calling;
        }

        void Place(int k, Vector3 offset, bool exact)
        {
            var j = _jacks[k];
            if (j == null) return;
            float a = exact || _snap ? 1f : 1f - Mathf.Exp(-remoteSmoothing * Time.deltaTime);
            j.Offset = Vector3.Lerp(j.Offset, offset, a);
        }

        void Swim(Jack j, Vector3 up, float dt)
        {
            var step = j.Offset - j.PrevOffset;
            j.PrevOffset = j.Offset;
            float speed = _snap ? 0f : step.magnitude / dt;
            var flat = Vector3.ProjectOnPlane(step, up);
            float turn = 0f;
            var heading = Vector3.ProjectOnPlane(j.Heading, up);
            if (heading.sqrMagnitude < 1e-6f) heading = Vector3.ProjectOnPlane(transform.forward, up);
            heading.Normalize();
            if (!_snap && speed > jackHeadingMinSpeed && flat.sqrMagnitude > 1e-8f)
            {
                var want = flat.normalized;
                var next = Vector3.Slerp(heading, want, 1f - Mathf.Exp(-jackTurnRate * dt)).normalized;
                turn = Vector3.SignedAngle(heading, next, up) / dt;
                heading = next;
            }
            j.Heading = heading;

            float parentScale = j.T.parent ? Mathf.Max(1e-4f, j.T.parent.lossyScale.x) : 1f;
            j.T.localScale = j.RestScale * (jackModelScale / parentScale);
            j.T.SetPositionAndRotation(transform.position + j.Offset,
                                       Quaternion.LookRotation(heading, up) * Quaternion.Euler(jackModelEuler));
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

        void Publish(bool seated, bool calling, bool decoy, Vector3 a, Vector3 b, Vector3 decoyOffset)
        {
            if (!IsSpawned || !IsOwner) return;
            byte flags = (byte)((seated ? SeatedBit : 0) | (calling ? CallingBit : 0) | (decoy ? DecoyBit : 0));
            if (flags != n_flags.Value) n_flags.Value = flags;
            if (!seated) return;
            Write(n_jackA, a);
            Write(n_jackB, b);
            if (decoy) Write(n_decoy, decoyOffset);
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

        // ------------------------------------------------------------------ the marlin

        void UpdateMarlin(float dt, bool calling)
        {
            var pos = transform.position;
            var fwd = transform.forward;
            if (_hullPrevValid)
            {
                float a = 1f - Mathf.Exp(-6f * dt);
                _hullSpeed = Mathf.Lerp(_hullSpeed, (pos - _prevHullPosition).magnitude / dt, a);
                float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(_prevHullForward, transform.up), fwd, transform.up) / dt;
                _hullYawRate = Mathf.Lerp(_hullYawRate, yaw, a);
            }
            _prevHullPosition = pos;
            _prevHullForward = fwd;
            _hullPrevValid = true;

            float cruise01 = Mathf.Clamp01(_hullSpeed / cruiseSpeed);
            float boost01 = Mathf.Clamp01(Mathf.InverseLerp(cruiseSpeed * 1.3f, boostSpeed * 0.8f, _hullSpeed));
            float yaw01 = Mathf.Clamp(_hullYawRate / fullYawRate, -1f, 1f);

            _tailPhase = Mathf.Repeat(_tailPhase + Mathf.Lerp(tailHz.x, tailHz.y, cruise01) * (1f + boost01) * dt, 1f);
            float beat = Mathf.Sin(_tailPhase * 2f * Mathf.PI);
            float lag = Mathf.Sin((_tailPhase - 0.2f) * 2f * Mathf.PI);
            _tail1.Pose(beat * tailDegrees.x + yaw01 * turnBendDegrees * 0.4f, 0f, 0f);
            _tail2.Pose(lag * tailDegrees.y + yaw01 * turnBendDegrees * 0.6f, 0f, 0f);

            // the pectoral fins follow the Rhino's front plates (the puppetry turns them; their colliders stay the hull's)
            var bodyFrame = transform.rotation;
            if (_plateL && _finL.T)
                _finL.PoseBodyTurn(Quaternion.Slerp(Quaternion.identity,
                    Quaternion.Inverse(bodyFrame) * _plateL.rotation * Quaternion.Inverse(_plateLRest), finFollow), _finLBodyFrame);
            if (_plateR && _finR.T)
                _finR.PoseBodyTurn(Quaternion.Slerp(Quaternion.identity,
                    Quaternion.Inverse(bodyFrame) * _plateR.rotation * Quaternion.Inverse(_plateRRest), finFollow), _finRBodyFrame);

            // the sail: up while the marlin calls the herd (a tell every peer sees), folded back at boost
            float want = calling ? sailCallDegrees : -boost01 * sailBoostFoldDegrees;   // +pitch tips the top forward
            _sailAngle = Mathf.Lerp(_sailAngle, want, 1f - Mathf.Exp(-sailRate * dt));
            _sail.Pose(0f, _sailAngle, 0f);
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
