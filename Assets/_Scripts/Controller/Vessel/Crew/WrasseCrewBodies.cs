using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Wrasse hull's two bodies (Garrett, 2026-10-20: "give the wrasse and grouper proper 3d vessels with animations
    /// etc." - "the wrasse and grouper should be visible to others"). Lives on the Wrasse root beside its NetworkObject.
    ///
    /// <para><b>The Wrasse</b> (the kite, model <c>WrasseKite.fbx</c>) is drawn where <see cref="KiteNavigator"/> flies
    /// it. The navigator runs only on the machine that flies the hull (the pilot's, or the host for an AI crew), so that
    /// machine publishes the kite's pose here - its offset from the hull and its rotation, quantized - and every other
    /// peer places the same model there, eased, riding the hull's own interpolated transform. The model swims: a wave
    /// down <c>spine.1</c> / <c>spine.2</c> / <c>tail</c> paced by the kite's speed, the fins flapping with it.</para>
    ///
    /// <para><b>The Grouper</b> (the hull, model <c>Grouper.fbx</c>) is the vessel's drawn geometry (it is in
    /// <see cref="VesselCustomization"/>'s ship geometries, so the domain paint, vision shading and every other reader
    /// treat it as the hull). It is puppeted from the hull's MEASURED motion, so it reads the same on every peer: the
    /// tail beats faster with speed and bends out of a turn, the fins bank, the jaw opens and the dorsal rises once the
    /// hull flies above cruise (a boost). Its element morphs need nothing here: the hull's <see cref="VesselAnimation"/> sits on
    /// the root and drives every element-labelled shape key on every skinned mesh under it, these models' included.</para>
    ///
    /// <para>Bones are found BY NAME (vessel-model contract: FBX sub-object ids are not derivable offline), and a
    /// model that is missing leaves its half of this component idle - the view then keeps drawing the flat kite.</para>
    ///
    /// <para><b>Channel discipline.</b> This component writes bone <c>localRotation</c> only, over the rest pose it
    /// captured; the morphs write vertices only.</para>
    /// </summary>
    public sealed class WrasseCrewBodies : NetworkBehaviour
    {
        // The kite's offset is quantized before it is written, so a kite at rest does not dirty the variable every tick.
        const float OffsetQuantum = 0.05f;
        const float RotationQuantumDegrees = 1f;

        [Header("Models (found by name under this hull)")]
        [Tooltip("Name of the Wrasse's model root (WrasseKite.fbx nested under the hull). Placed in world space where the " +
                 "kite flies.")]
        [SerializeField] string kiteModelName = "WrasseKite";
        [Tooltip("Name of the Grouper's model root (Grouper.fbx nested under the hull's orientation handle).")]
        [SerializeField] string grouperModelName = "Grouper";
        [Tooltip("Rotation (degrees) from the kite's flight frame (+Z along its heading, +Y the board's up) to the model's " +
                 "own frame. Zero when the model's nose is +Z and its back +Y in Unity.")]
        [SerializeField] Vector3 kiteModelEuler = Vector3.zero;
        [Tooltip("World size of the Wrasse model relative to its authored size, whatever the hull's own scale.")]
        [SerializeField, Min(0.01f)] float kiteModelScale = 1f;

        [Header("Wrasse swim")]
        [Tooltip("Swim beats per second with the kite still on the board.")]
        [SerializeField, Min(0f)] float swimHz = 1.2f;
        [Tooltip("Extra beats per second per 100 u/s of the kite's speed across the board.")]
        [SerializeField, Min(0f)] float swimHzPer100 = 1.4f;
        [Tooltip("Half-amplitude (degrees) of the body wave at spine.1, spine.2 and tail. Grows toward the tail, the way " +
                 "a wrasse swims.")]
        [SerializeField] Vector3 swimDegrees = new(8f, 14f, 24f);
        [Tooltip("Phase each bone lags the one before it, in beats.")]
        [SerializeField, Range(0f, 0.5f)] float swimLagBeats = 0.18f;
        [Tooltip("Half-amplitude (degrees) of the pectoral fins' flap, about the body's long axis.")]
        [SerializeField] float finFlapDegrees = 28f;
        [Tooltip("Degrees the body bends out of a turn at full turn rate (the kite turning 360 °/s).")]
        [SerializeField] float swimTurnBendDegrees = 18f;

        [Header("Grouper puppetry")]
        [Tooltip("The hull's cruise speed (u/s): the tail's beat rate and the boost pose are measured against it.")]
        [SerializeField, Min(1f)] float grouperCruiseSpeed = 60f;
        [Tooltip("The hull's boosted speed (u/s): the jaw is fully open and the dorsal fully raised here.")]
        [SerializeField, Min(1f)] float grouperBoostSpeed = 260f;
        [Tooltip("Tail beats per second at a standstill and at cruise.")]
        [SerializeField] Vector2 grouperTailHz = new(0.6f, 1.4f);
        [Tooltip("Half-amplitude (degrees) of the tail's beat at tail.1 and tail.2.")]
        [SerializeField] Vector2 grouperTailDegrees = new(7f, 16f);
        [Tooltip("Degrees the tail bends out of a turn at the yaw rate below.")]
        [SerializeField] float grouperTurnBendDegrees = 22f;
        [Tooltip("Yaw rate (degrees per second) that bends the tail and banks the fins all the way.")]
        [SerializeField, Min(1f)] float grouperFullYawRate = 110f;
        [Tooltip("Degrees the pectoral fins scull at a standstill (falls to a third at cruise).")]
        [SerializeField] float grouperFinScullDegrees = 14f;
        [Tooltip("Degrees the outside fin rises against the inside one at the full yaw rate.")]
        [SerializeField] float grouperFinBankDegrees = 26f;
        [Tooltip("Degrees the jaw opens at boost speed (positive opens downward about the body's right axis).")]
        [SerializeField] float grouperJawDegrees = 24f;
        [Tooltip("Degrees the dorsal fin rises at boost speed or the full yaw rate.")]
        [SerializeField] float grouperDorsalDegrees = 30f;

        [Header("Network")]
        [Tooltip("How fast another peer's copy of the kite closes on the published pose (per second).")]
        [SerializeField, Min(0.1f)] float remoteSmoothing = 14f;

        // Bit 0: a kite is seated and drawn.
        readonly NetworkVariable<byte> n_flags = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        // The kite's position minus the hull's, in world axes.
        readonly NetworkVariable<Vector3> n_offset = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_rotation = new(Quaternion.identity,
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>True where the kite's pose is DECIDED: the owning machine, or anywhere while not network-spawned.</summary>
        bool IsAuthority => !IsSpawned || IsOwner;

        KiteNavigator _nav;
        IVesselStatus _status;
        SkinnedMeshRenderer _hullRenderer;      // the hull's original drawn mesh: the bodies wear its layer

        // the Wrasse
        Transform _kite;
        SkinnedMeshRenderer _kiteRenderer;
        Vector3 _kiteRestScale;
        Bone[] _kiteSpine;          // spine.1, spine.2, tail
        Bone _kiteFinL, _kiteFinR;
        Vector3 _kiteOffset, _kitePrevOffset;
        Quaternion _kiteRotation = Quaternion.identity, _kitePrevRotation = Quaternion.identity;
        bool _kiteShown, _kiteSnap = true;
        float _swimPhase;
        Material _kitePainted;

        // the Grouper
        Transform _grouper;
        Bone _jaw, _finL, _finR, _dorsal, _tail1, _tail2;
        Vector3 _prevHullPosition, _prevHullForward;
        bool _hullPrevValid;
        float _hullSpeed, _hullYawRate, _tailPhase;

        struct Bone
        {
            public Transform T;
            public Quaternion Rest;
            public Vector3 Up, Right, Forward;    // the body's axes, in the bone's local frame at rest

            public bool Valid => T;

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
            TryGetComponent(out _nav);
            if (TryGetComponent(out IVessel vessel)) _status = vessel.VesselStatus;
            var anim = GetComponentInChildren<VesselAnimation>(true);
            if (anim) _hullRenderer = anim.SkinnedMeshRenderer;

            ResolveKite();
            ResolveGrouper();
        }

        void ResolveKite()
        {
            _kite = FindDeep(transform, kiteModelName);
            if (!_kite) return;
            _kiteRenderer = _kite.GetComponentInChildren<SkinnedMeshRenderer>(true);
            _kiteRestScale = _kite.localScale;
            var bodyFrame = _kite.rotation * Quaternion.Inverse(Quaternion.Euler(kiteModelEuler));
            _kiteSpine = new[]
            {
                Bone.Find(_kite, "spine.1", bodyFrame), Bone.Find(_kite, "spine.2", bodyFrame), Bone.Find(_kite, "tail", bodyFrame),
            };
            _kiteFinL = Bone.Find(_kite, "fin.L", bodyFrame);
            _kiteFinR = Bone.Find(_kite, "fin.R", bodyFrame);
            CopyLayer(_kite);
            _kite.gameObject.SetActive(false);
        }

        void ResolveGrouper()
        {
            _grouper = FindDeep(transform, grouperModelName);
            if (!_grouper) return;
            // The Grouper flies with the hull: its body frame IS the hull's (nose +Z, back +Y).
            var bodyFrame = transform.rotation;
            _jaw = Bone.Find(_grouper, "jaw", bodyFrame);
            _finL = Bone.Find(_grouper, "fin.L", bodyFrame);
            _finR = Bone.Find(_grouper, "fin.R", bodyFrame);
            _dorsal = Bone.Find(_grouper, "dorsal", bodyFrame);
            _tail1 = Bone.Find(_grouper, "tail.1", bodyFrame);
            _tail2 = Bone.Find(_grouper, "tail.2", bodyFrame);
            CopyLayer(_grouper);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_kite) UpdateKite(dt);
            if (_grouper) UpdateGrouper(dt);
        }

        // --- the Wrasse ----------------------------------------------------------------------------------------------

        void UpdateKite(float dt)
        {
            bool seated = false;
            if (IsAuthority)
            {
                Vector3 pos = default;
                Quaternion rot = Quaternion.identity;
                if (_nav) seated = _nav.TryGetKitePose(out pos, out rot);
                if (seated)
                {
                    _kiteOffset = pos - transform.position;
                    _kiteRotation = rot;
                    Publish(true, _kiteOffset, rot);
                }
                else Publish(false, Vector3.zero, Quaternion.identity);
            }
            else
            {
                seated = (n_flags.Value & 1) != 0;
                if (seated)
                {
                    float a = _kiteSnap ? 1f : 1f - Mathf.Exp(-remoteSmoothing * dt);
                    _kiteOffset = Vector3.Lerp(_kiteOffset, n_offset.Value, a);
                    _kiteRotation = Quaternion.Slerp(_kiteRotation, n_rotation.Value, a);
                }
            }

            if (_nav) _nav.BodyDrawsKite = seated;
            if (seated != _kiteShown)
            {
                _kiteShown = seated;
                _kite.gameObject.SetActive(seated);
                _kiteSnap = true;
            }
            if (!seated) return;

            float parentScale = _kite.parent ? Mathf.Max(1e-4f, _kite.parent.lossyScale.x) : 1f;
            _kite.localScale = _kiteRestScale * (kiteModelScale / parentScale);
            _kite.SetPositionAndRotation(transform.position + _kiteOffset, _kiteRotation * Quaternion.Euler(kiteModelEuler));
            PaintKite();

            // swim, paced by the kite's speed across the board (its offset's rate, so the hull's own flight does not count)
            float speed = _kiteSnap ? 0f : (_kiteOffset - _kitePrevOffset).magnitude / dt;
            float turn = _kiteSnap ? 0f : SignedYaw(_kitePrevRotation, _kiteRotation) / dt;
            _kitePrevOffset = _kiteOffset;
            _kitePrevRotation = _kiteRotation;
            _kiteSnap = false;

            _swimPhase = Mathf.Repeat(_swimPhase + (swimHz + swimHzPer100 * speed * 0.01f) * dt, 1f);
            float bend = Mathf.Clamp(turn / 360f, -1f, 1f) * swimTurnBendDegrees;
            for (int i = 0; i < _kiteSpine.Length; i++)
            {
                float wave = Mathf.Sin((_swimPhase - i * swimLagBeats) * 2f * Mathf.PI) * swimDegrees[i];
                _kiteSpine[i].Pose(wave + bend * (i + 1) / _kiteSpine.Length, 0f, 0f);
            }
            float flap = Mathf.Sin((_swimPhase + 0.25f) * 2f * Mathf.PI) * finFlapDegrees;
            _kiteFinL.Pose(0f, 0f, -flap);
            _kiteFinR.Pose(0f, 0f, flap);
        }

        void Publish(bool seated, Vector3 offset, Quaternion rotation)
        {
            if (!IsSpawned || !IsOwner) return;
            byte flags = (byte)(seated ? 1 : 0);
            if (flags != n_flags.Value) n_flags.Value = flags;
            if (!seated) return;
            var q = new Vector3(Quantize(offset.x), Quantize(offset.y), Quantize(offset.z));
            if ((q - n_offset.Value).sqrMagnitude > OffsetQuantum * OffsetQuantum * 0.25f) n_offset.Value = q;
            if (Quaternion.Angle(rotation, n_rotation.Value) > RotationQuantumDegrees) n_rotation.Value = rotation;
        }

        static float Quantize(float v) => Mathf.Round(v / OffsetQuantum) * OffsetQuantum;

        // The domain paint, on the kite's slot 0 (the fleet's Domain slot), following the hull's material.
        void PaintKite()
        {
            var mat = _status?.ShipMaterial;
            if (!mat || mat == _kitePainted || !_kiteRenderer) return;
            var mats = _kiteRenderer.sharedMaterials;
            if (mats.Length == 0) return;
            mats[0] = mat;
            _kiteRenderer.sharedMaterials = mats;
            _kitePainted = mat;
        }

        // --- the Grouper ---------------------------------------------------------------------------------------------

        void UpdateGrouper(float dt)
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

            float cruise01 = Mathf.Clamp01(_hullSpeed / grouperCruiseSpeed);
            float boost01 = Mathf.Clamp01(Mathf.InverseLerp(grouperCruiseSpeed * 1.3f, grouperBoostSpeed * 0.8f, _hullSpeed));
            float yaw01 = Mathf.Clamp(_hullYawRate / grouperFullYawRate, -1f, 1f);

            _tailPhase = Mathf.Repeat(_tailPhase + Mathf.Lerp(grouperTailHz.x, grouperTailHz.y, cruise01) * (1f + boost01) * dt, 1f);
            float beat = Mathf.Sin(_tailPhase * 2f * Mathf.PI);
            float lag = Mathf.Sin((_tailPhase - 0.2f) * 2f * Mathf.PI);
            // the tail bends OUT of a turn (a right turn swings it left), so the body arcs into the turn
            _tail1.Pose(beat * grouperTailDegrees.x + yaw01 * grouperTurnBendDegrees * 0.4f, 0f, 0f);
            _tail2.Pose(lag * grouperTailDegrees.y + yaw01 * grouperTurnBendDegrees * 0.6f, 0f, 0f);

            float scull = Mathf.Sin(_tailPhase * 4f * Mathf.PI) * grouperFinScullDegrees * Mathf.Lerp(1f, 0.33f, cruise01);
            float bank = yaw01 * grouperFinBankDegrees * 0.5f;
            _finL.Pose(0f, 0f, -(scull + bank));
            _finR.Pose(0f, 0f, scull - bank);

            _jaw.Pose(0f, boost01 * grouperJawDegrees, 0f);
            _dorsal.Pose(0f, Mathf.Max(boost01, Mathf.Abs(yaw01)) * grouperDorsalDegrees, 0f);
        }

        // --- helpers -------------------------------------------------------------------------------------------------

        static float SignedYaw(Quaternion from, Quaternion to)
        {
            var up = to * Vector3.up;
            return Vector3.SignedAngle(Vector3.ProjectOnPlane(from * Vector3.forward, up), to * Vector3.forward, up);
        }

        // The bodies wear the layer the hull's drawn geometry wears, so every camera that sees the hull sees them.
        void CopyLayer(Transform model)
        {
            int layer = _hullRenderer ? _hullRenderer.gameObject.layer : gameObject.layer;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

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

        public override void OnDestroy()
        {
            if (_nav) _nav.BodyDrawsKite = false;
            base.OnDestroy();
        }
    }
}
