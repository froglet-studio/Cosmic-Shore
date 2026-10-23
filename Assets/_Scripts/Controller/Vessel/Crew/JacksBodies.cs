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
    public sealed class JacksBodies : NetworkBehaviour
    {
        // Offsets are quantized before they are written, so jacks at rest do not dirty the variables every tick.
        const float OffsetQuantum = 0.25f;
        const byte DecoyBit = 1, CallingBit = 2, SeatedBit = 4, FlyingBit = 8;

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

        [Header("The jack between the fish (the Rhino's sword, made three axes)")]
        [Tooltip("Name of the skimmer under the hull that becomes the jack: the Rhino's sword capsule (its collider is the " +
                 "Y arm; the prefab adds the X and Z arms' colliders beside it).")]
        [SerializeField] string toyJackName = "ForceFieldSkimmer";
        [Tooltip("How far the arms reach toward the fish: at 1 the arm tips turn in the planes the two fish swim in (each " +
                 "fish at the centre of the triangle three tips draw round it); below 1 the jack sits inside the pair.")]
        [SerializeField, Min(0f)] float toyJackReach = 1f;
        [Tooltip("Shortest arm, centre to tip (u), however close the fish come.")]
        [SerializeField, Min(0.1f)] float toyJackMinArm = 4f;
        [Tooltip("Longest arm, centre to tip (u): herding, the fish are a board apart, and the jack stops growing here.")]
        [SerializeField, Min(0.1f)] float toyJackMaxArm = 40f;
        [Tooltip("Radius of each arm (u): the sword's capsule, made round.")]
        [SerializeField, Min(0.05f)] float toyJackArmRadius = 1.2f;
        [Tooltip("How fast the jack spins about its (1,1,1) diagonal, the line between the fish (degrees per second).")]
        [SerializeField] float toyJackSpinDegreesPerSecond = 120f;
        [Tooltip("Whether the jack skims while the jacks herd. Off: it is drawn between the fish on the board, but its " +
                 "colliders are off, so a parked hull does not skim (or vacuum crystals) in the middle of the board.")]
        [SerializeField] bool toyJackSkimsWhileHerding;

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

        /// <summary>True where the jacks are DECIDED: the owning machine, or anywhere while not network-spawned.</summary>
        bool IsAuthority => !IsSpawned || IsOwner;

        JacksExecutor _crew;
        IVesselStatus _status;

        readonly Jack[] _jacks = new Jack[2];
        ToyJack _toy;
        bool _jacksShown, _snap = true, _flying;
        Vector3 _decoyOffset, _prevHullPosition;
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

        /// <summary>The jack between the fish: the sword skimmer's own transform, its three colliders and three arms.</summary>
        sealed class ToyJack
        {
            public Transform T;
            public readonly List<CapsuleCollider> Colliders = new();
            public Behaviour Impactor;
            public readonly List<Renderer> Arms = new();
            public float Spin;
            public bool? Shown, Skims;
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
            BuildToyJack();
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
                    Scale = flightModelScale,
                };
                if (_jacks[k].Renderer) _jacks[k].Renderer.updateWhenOffscreen = true;
                t.gameObject.SetActive(false);
                foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
            }
            // The puppetry's flares and its renderer reads land on a drawn jack, not on the hidden rig. Set before the vessel
            // initializes its animation (which keeps a renderer it was given).
            var anim = GetComponentInChildren<VesselAnimation>(true);
            if (anim && _jacks[0] != null && _jacks[0].Renderer) anim.SkinnedMeshRenderer = _jacks[0].Renderer;
        }

        // ------------------------------------------------------------------ read by the executor on a non-owning peer

        /// <summary>
        /// The owner's jacks, decoy, call and mode, as world positions on this peer. False where this machine decides the
        /// jacks itself (the owner, or unspawned), or before the owner has published them.
        /// </summary>
        internal bool TryGetRemoteCrew(out Vector3 jackA, out Vector3 jackB, out bool hasDecoy, out Vector3 decoy, out bool calling,
                                       out bool flying)
        {
            byte flags = n_flags.Value;
            var p = transform.position;
            flying = (flags & FlyingBit) != 0;
            jackA = flying ? p + transform.rotation * n_jackA.Value : p + n_jackA.Value;
            jackB = flying ? p + transform.rotation * n_jackB.Value : p + n_jackB.Value;
            decoy = p + n_decoy.Value;
            hasDecoy = (flags & DecoyBit) != 0;
            calling = (flags & CallingBit) != 0;
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
                bool decoy = false;
                if (seated && _crew.TryGetDecoy(out var at))
                {
                    decoy = true;
                    _decoyOffset = at - transform.position;
                }
                Vector3 a = Vector3.zero, b = Vector3.zero;
                if (seated)
                {
                    a = flying ? _crew.JackLocal(0) : _crew.JackWorld(0) - transform.position;
                    b = flying ? _crew.JackLocal(1) : _crew.JackWorld(1) - transform.position;
                }
                Publish(seated, flying, calling, decoy, a, b, _decoyOffset);
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
            if (!seated)
            {
                ShowToyJack(false, false);
                return;
            }

            var up = _flying ? transform.up : _crew && _crew.Config ? _crew.BoardUp : transform.up;
            for (int k = 0; k < _jacks.Length; k++)
                if (_jacks[k] != null) Swim(_jacks[k], up, dt);
            PoseToyJack(up, dt);
            _snap = false;
        }

        // ------------------------------------------------------------------ the jack between the fish
        //
        // Garrett, 2026-10-23: "instead of a sword (1 axis) make it all three orthogonal axes (like a jack in the game of
        // jacks). then if it was a coordinate system, spin it about the (1,1,1) axis and place that axis dynamically so it
        // the same axis that draws a line between the two fish." The Rhino's sword capsule stays the hull's skimmer (its
        // impactor, its kinematics); it becomes the jack: a uniform scale of one arm's length, three capsule colliders
        // (X, Y, Z; the prefab carries the two new ones, so the impactor's probe set holds all three from its first
        // OnEnable), three arms drawn with the blade's own material, posed on every peer from the drawn jacks.

        static readonly Quaternion[] ArmTurns =
            { Quaternion.Euler(0f, 0f, -90f), Quaternion.identity, Quaternion.Euler(90f, 0f, 0f) };   // capsule Y -> X, Y, Z

        void BuildToyJack()
        {
            var t = FindDeep(transform, toyJackName);
            if (!t) return;
            _toy = new ToyJack { T = t };
            if (t.TryGetComponent(out Skimmer skimmer)) skimmer.HasExternalScaleDriver = true;   // the jack owns its scale
            if (t.TryGetComponent(out SkimmerImpactor impactor)) _toy.Impactor = impactor;
            t.GetComponents(_toy.Colliders);
            // the blade's own renderer and its crackle overlay stand down; the jack draws three arms in the blade's material
            t.TryGetComponent(out MeshRenderer blade);
            t.TryGetComponent(out MeshFilter bladeMesh);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (!blade || !bladeMesh || !bladeMesh.sharedMesh) return;
            for (int axis = 0; axis < 3; axis++)
            {
                var arm = new GameObject($"Jack arm {"XYZ"[axis]}") { layer = t.gameObject.layer };
                arm.transform.SetParent(t, false);
                arm.transform.localRotation = ArmTurns[axis];
                arm.AddComponent<MeshFilter>().sharedMesh = bladeMesh.sharedMesh;
                var r = arm.AddComponent<MeshRenderer>();
                r.sharedMaterial = blade.sharedMaterial;
                r.shadowCastingMode = blade.shadowCastingMode;
                r.receiveShadows = blade.receiveShadows;
                _toy.Arms.Add(r);
            }
        }

        void PoseToyJack(Vector3 up, float dt)
        {
            if (_toy == null || _jacks[0] == null || _jacks[1] == null) return;
            var a = transform.position + _jacks[0].Offset;
            var b = transform.position + _jacks[1].Offset;
            float arm = JacksMath.JackArm(Vector3.Distance(a, b), toyJackReach, toyJackMinArm, toyJackMaxArm);
            _toy.Spin = Mathf.Repeat(_toy.Spin + toyJackSpinDegreesPerSecond * dt, 360f);

            var t = _toy.T;
            t.SetPositionAndRotation(0.5f * (a + b), JacksMath.JackRotation(b - a, up, _toy.Spin));
            float parentScale = t.parent ? Mathf.Max(1e-4f, t.parent.lossyScale.x) : 1f;
            t.localScale = Vector3.one * (arm / parentScale);

            // in the jack's own frame (one unit = one arm): the built-in capsule spans y -1..1 with radius 0.5
            float radius = Mathf.Min(toyJackArmRadius / arm, 0.5f);
            for (int i = 0; i < _toy.Colliders.Count; i++)
            {
                var c = _toy.Colliders[i];
                c.center = Vector3.zero;
                c.radius = radius;
                c.height = 2f;
            }
            var armScale = new Vector3(2f * radius, 1f, 2f * radius);
            for (int i = 0; i < _toy.Arms.Count; i++) _toy.Arms[i].transform.localScale = armScale;
            ShowToyJack(true, _flying || toyJackSkimsWhileHerding);
        }

        void ShowToyJack(bool shown, bool skims)
        {
            if (_toy == null) return;
            if (_toy.Shown != shown)
            {
                _toy.Shown = shown;
                for (int i = 0; i < _toy.Arms.Count; i++) _toy.Arms[i].enabled = shown;
            }
            if (_toy.Skims != skims)
            {
                // colliders off and the impactor bounced, so it forgets what it was touching (no exit fires for an off collider)
                _toy.Skims = skims;
                for (int i = 0; i < _toy.Colliders.Count; i++) _toy.Colliders[i].enabled = skims;
                if (_toy.Impactor) _toy.Impactor.enabled = skims;
            }
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

            float wantScale = _flying ? flightModelScale : jackModelScale;
            j.Scale = _snap ? wantScale : Mathf.Lerp(j.Scale, wantScale, 1f - Mathf.Exp(-modelScaleEase * dt));
            float parentScale = j.T.parent ? Mathf.Max(1e-4f, j.T.parent.lossyScale.x) : 1f;
            j.T.localScale = j.RestScale * (j.Scale / parentScale);
            j.T.SetPositionAndRotation(transform.position + j.Offset, facing * Quaternion.Euler(jackModelEuler));
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

        void Publish(bool seated, bool flying, bool calling, bool decoy, Vector3 a, Vector3 b, Vector3 decoyOffset)
        {
            if (!IsSpawned || !IsOwner) return;
            byte flags = (byte)((seated ? SeatedBit : 0) | (flying ? FlyingBit : 0) | (calling ? CallingBit : 0)
                                | (decoy ? DecoyBit : 0));
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
