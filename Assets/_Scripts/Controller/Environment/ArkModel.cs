using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark's MODEL (<see cref="ArkModelDefinitionSO"/>): the mothership drawn as a model instead of a spindle of
    /// plates, with its hull prisms seated in the model's own sockets and its jets and wings flying the turn.
    ///
    ///   • <b>The hull</b> is still the Ark's: ordinary conserved prism mass in the Ark's domain, laid through the
    ///     canonical path by <see cref="Ark.LayHullAsync"/>. The model only says WHERE each prism goes and WHAT it is:
    ///     one lay per socket of the rig table (<see cref="ArkRigTable"/>), its kind the socket's shield tier
    ///     (super-shielded stellations, shielded octahedra, plain gills, danger blades), its size the one the socket
    ///     was cut for. Sockets on the hull ride the Ark's root; a jaw's blades ride the jaw, the way a creature's body
    ///     prisms ride its bones (the mover contract: they are told when the jaw moves them).
    ///   • <b>The domain</b> the hull is laid in also paints the model: the Domain slot's placeholder material is
    ///     replaced with the domain's ship material, the identity swap every vessel makes.
    ///   • <b>The puppetry</b> plays the model's own takes, blended by how the Ark is actually turning: its yaw rate
    ///     plays the yaw take (the gimbal rings swing) and, banked, the roll take (the jaws as canards, the outer
    ///     plates as spoilers); its pitch rate the pitch take; its acceleration the speed takes. A take is a rotation
    ///     about the bone's own pivot in model space, conjugated into the bone's frame each frame, so a jet pitches
    ///     about its ring's yawed axis like a real gimbal. Nothing is played past full: a bone two inputs move (a jet
    ///     pitches AND banks about its own x axis) shares one take's worth between them (<see cref="ArkRigTable.Budget"/>),
    ///     the envelope the model's clearance gate proved over every blend the Ark can fly.
    ///   • <b>The actions</b> are the model's other takes, played on events: the hangar's clam-jaw doors open while a
    ///     squadmate is in the hold's grab reach (the hold is open to you: a world fact shown in the world), and the
    ///     lance slides out of its cradles when the hold hands a payload over (<see cref="ArkHold.ParcelGranted"/>).
    ///   • <b>The reactor</b> (<see cref="Ark.Reactor"/>): the omni crystal in the well is as big as the charge aboard
    ///     and its collar turns, faster while the hull regrows; a harvested crystal flies into it
    ///     (<see cref="ReactorPivot"/>).
    /// </summary>
    public sealed class ArkModel : MonoBehaviour
    {
        /// <summary>One batch of hull lays and the transform they ride.</summary>
        public readonly struct LayBatch
        {
            public readonly Transform Parent;
            public readonly List<PrismLay> Lays;
            public readonly bool RidesBone;

            public LayBatch(Transform parent, List<PrismLay> lays, bool ridesBone)
            {
                Parent = parent;
                Lays = lays;
                RidesBone = ridesBone;
            }
        }

        struct Puppet
        {
            public Transform Bone;
            public Quaternion RestLocal, RestInArk;
            public Vector3 RestLocalPosition;
            public Quaternion PitchUp, PitchDown, YawRight, YawLeft, RollRight, RollLeft, SpeedUp, SpeedDown;
            public Vector4 Moves;                          // 1 per input (pitch, yaw, roll, speed) that has a take on this bone
            public Quaternion Release, HangarOpen;         // the action takes (identity = the action leaves the bone)
            public Vector3 ReleaseOffset;                  // the release take's slide, in the bone's parent's space
        }

        const float FrameMismatchTolerance = 0.5f;        // model units: a bone further than this from its table pivot
        const float RiderNotifyInput = 0.005f;            // a jaw move below this much input (~0.035 deg) is not worth a re-file
        const float HangarScanSeconds = 0.25f;            // how often the hangar looks for a squadmate in reach
        const string ReactorBone = "omni";                // the omni crystal in the reactor well
        const string ReactorCollarBone = "omni.ring";     // its collar

        ArkModelDefinitionSO _def;
        Ark _ark;
        ArkRigTable _table;
        Transform _root;
        float _scale = 1f;
        readonly Dictionary<string, Transform> _bones = new();
        readonly List<Puppet> _puppets = new();
        readonly List<Prism> _riders = new();

        Quaternion _lastRotation;
        float _lastSpeed;
        bool _hasLast;
        Vector4 _input;                                    // smoothed: pitch, yaw, roll, speed in [-1, 1]
        Vector4 _riderInputAtNotify;

        // Actions: 0 = rest, 1 = the take in full.
        float _release, _hangar;
        float _releaseUntil = float.NegativeInfinity;
        bool _hangarWanted;
        float _nextHangarScanAt;
        ArkHold _hold;

        // Reactor: the omni crystal shows the charge, its collar turns.
        Transform _reactor, _collar;
        Vector3 _reactorRestScale = Vector3.one;
        Quaternion _collarRestLocal, _collarRestInArk;
        float _reactorFill = 1f, _shownFill = 1f, _collarAngle;
        bool _regrowing, _reactorDriven;              // an Ark without a reactor leaves the crystal as built

        /// <summary>The sockets the hull wears - its full-health count.</summary>
        public int SocketCount => _table?.sockets.Length ?? 0;

        /// <summary>The live input blend (pitch, yaw, roll, speed), for the HUD or a test.</summary>
        public Vector4 Input => _input;

        /// <summary>Where the omni crystal sits in the reactor well (a harvested crystal flies here); the Ark's root when
        /// the model has no reactor bone.</summary>
        public Transform ReactorPivot => _reactor ? _reactor : (_ark ? _ark.transform : transform);

        /// <summary>The action takes now, 0..1: the lance out, the hangar open.</summary>
        public float ReleaseAmount => _release;
        public float HangarAmount => _hangar;

        /// <summary>
        /// Instantiate <paramref name="def"/>'s model under <paramref name="ark"/> and bind its rig table. Null (with a
        /// warning naming what is missing) when the definition cannot draw an Ark - the caller keeps the spindle hull.
        /// </summary>
        public static ArkModel Attach(Ark ark, ArkModelDefinitionSO def)
        {
            if (!ark || !def) return null;
            if (!def.Model || !def.RigTable)
            {
                CSDebug.LogWarning($"[Ark] The Ark model '{def.name}' has no {(def.Model ? "rig table" : "model")} - " +
                                   "the Ark keeps its spindle hull.", def);
                return null;
            }
            var table = ArkRigTable.Parse(def.RigTable.text, out string error);
            if (table == null)
            {
                CSDebug.LogWarning($"[Ark] The Ark model '{def.name}': {error} - the Ark keeps its spindle hull.", def);
                return null;
            }

            var host = ark.gameObject.AddComponent<ArkModel>();
            host.Bind(ark, def, table);
            return host;
        }

        void Bind(Ark ark, ArkModelDefinitionSO def, ArkRigTable table)
        {
            _ark = ark;
            _def = def;
            _table = table;
            _scale = def.Scale;

            // The model keeps its own imported root pose (the FBX's frame conversion lives there) and sits at the Ark's
            // origin, so the table's model space IS the Ark's local space, scaled.
            var go = Instantiate(def.Model, ark.transform, false);
            go.name = "ArkModel";
            _root = go.transform;
            _root.localPosition = Vector3.zero;
            _root.localScale *= _scale;

            IndexBones(_root);
            BindPuppets();
            BindReactor();

            _hold = ark.Hold;
            if (_hold) _hold.ParcelGranted += OnParcelGranted;
        }

        void OnDestroy()
        {
            if (_hold) _hold.ParcelGranted -= OnParcelGranted;
        }

        void OnParcelGranted(PayloadCarrier carrier) => _releaseUntil = Time.time + _def.ReleaseSeconds;

        void BindReactor()
        {
            if (_bones.TryGetValue(ReactorBone, out _reactor)) _reactorRestScale = _reactor.localScale;
            if (_bones.TryGetValue(ReactorCollarBone, out _collar))
            {
                _collarRestLocal = _collar.localRotation;
                _collarRestInArk = Quaternion.Inverse(_ark.transform.rotation) * _collar.rotation;
            }
        }

        /// <summary>The reactor's state, from the Ark: how full it is (0..1) and whether the hull is regrowing.</summary>
        public void SetReactor(float fill01, bool regrowing)
        {
            _reactorFill = Mathf.Clamp01(fill01);
            _regrowing = regrowing;
            _reactorDriven = true;
        }

        void IndexBones(Transform t)
        {
            if (!_bones.ContainsKey(t.name)) _bones[t.name] = t;
            for (int i = 0; i < t.childCount; i++) IndexBones(t.GetChild(i));
        }

        void BindPuppets()
        {
            var arkRotationInverse = Quaternion.Inverse(_ark.transform.rotation);
            string mismatch = null;
            foreach (var entry in _table.bones)
            {
                if (entry == null || string.IsNullOrEmpty(entry.bone)) continue;
                if (!_bones.TryGetValue(entry.bone, out var bone))
                {
                    CSDebug.LogWarning($"[Ark] The Ark model has no bone '{entry.bone}' that its rig table puppets - " +
                                       "rebuild the model and its table together.", _def);
                    continue;
                }
                var puppet = new Puppet
                {
                    Bone = bone,
                    RestLocal = bone.localRotation,
                    RestInArk = arkRotationInverse * bone.rotation,
                    RestLocalPosition = bone.localPosition,
                    PitchUp = ArkRigTable.Q(entry.pitchUp), PitchDown = ArkRigTable.Q(entry.pitchDown),
                    YawRight = ArkRigTable.Q(entry.yawRight), YawLeft = ArkRigTable.Q(entry.yawLeft),
                    RollRight = ArkRigTable.Q(entry.rollRight), RollLeft = ArkRigTable.Q(entry.rollLeft),
                    SpeedUp = ArkRigTable.Q(entry.speedUp), SpeedDown = ArkRigTable.Q(entry.speedDown),
                    Release = ArkRigTable.Q(entry.release), HangarOpen = ArkRigTable.Q(entry.hangarOpen),
                };
                // The release take's slide is a model-space vector (the Ark's space, scaled); the bone moves in its
                // parent's space, so it is carried there once, at rest.
                var slide = entry.ReleaseMove * _scale;
                if (slide.sqrMagnitude > 1e-8f)
                {
                    var world = _ark.transform.TransformVector(slide);
                    puppet.ReleaseOffset = bone.parent ? bone.parent.InverseTransformVector(world) : world;
                }
                puppet.Moves = new Vector4(Moves(puppet.PitchUp, puppet.PitchDown), Moves(puppet.YawRight, puppet.YawLeft),
                                           Moves(puppet.RollRight, puppet.RollLeft), Moves(puppet.SpeedUp, puppet.SpeedDown));
                _puppets.Add(puppet);
                // The table's pivots are where the build put the bones: a model imported in another frame (scale,
                // axis conversion) shows up here, once, instead of as jets swinging about the wrong points.
                var inArk = _ark.transform.InverseTransformPoint(bone.position);
                if (mismatch == null && (inArk - entry.Head * _scale).magnitude > FrameMismatchTolerance * _scale)
                    mismatch = $"'{entry.bone}' at {inArk} where the table puts it at {entry.Head * _scale}";
            }
            if (mismatch != null)
                CSDebug.LogWarning($"[Ark] The Ark model's frame does not match its rig table ({mismatch}): check the FBX's " +
                                   "import (globalScale 1, useFileUnits, no axis bake) against ArkReverse.fbx.meta.", _def);
        }

        static float Moves(Quaternion positive, Quaternion negative) =>
            Quaternion.Angle(Quaternion.identity, positive) > 0.01f || Quaternion.Angle(Quaternion.identity, negative) > 0.01f ? 1f : 0f;

        // ── Hull ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The hull's lays, one per socket, in <paramref name="domain"/>, grouped by the transform each rides: the Ark's
        /// root for the hull's sockets, the jaw for a jaw's blades. Read at REST (before the first frame of
        /// puppetry), which is the pose the sockets were cut in.
        /// </summary>
        public List<LayBatch> BuildLays(Domains domain)
        {
            var batches = new List<LayBatch>();
            var byParent = new Dictionary<Transform, List<PrismLay>>();
            var arkT = _ark.transform;
            foreach (var socket in _table.sockets)
            {
                if (socket == null) continue;
                var parent = arkT;
                if (!socket.RidesHull && !_bones.TryGetValue(socket.bone, out parent))
                {
                    CSDebug.LogWarning($"[Ark] Socket '{socket.name}' rides bone '{socket.bone}', which the model lacks - " +
                                       "it rides the hull instead.", _def);
                    parent = arkT;
                }
                var worldPos = arkT.TransformPoint(socket.Position * _scale);
                var worldRot = arkT.rotation * socket.Rotation;
                float parentScale = Mathf.Max(1e-4f, parent.lossyScale.x);
                var lay = new PrismLay(new SpawnPoint(parent.InverseTransformPoint(worldPos),
                                                      Quaternion.Inverse(parent.rotation) * worldRot,
                                                      socket.Size * (_scale * arkT.lossyScale.x / parentScale)),
                                       domain, socket.Kind);
                if (!byParent.TryGetValue(parent, out var lays))
                {
                    lays = new List<PrismLay>();
                    byParent[parent] = lays;
                    batches.Add(new LayBatch(parent, lays, parent != arkT));
                }
                lays.Add(lay);
            }
            return batches;
        }

        /// <summary>
        /// The hull as <paramref name="def"/>'s model wears it, one lay per socket in the model's own space (scaled), in
        /// <paramref name="domain"/>: the Ark's shape without an Ark, for a miniature (the Wander toy's emblem). Null
        /// when the definition has no readable rig table.
        /// </summary>
        public static List<PrismLay> SocketLays(ArkModelDefinitionSO def, Domains domain)
        {
            if (!def || !def.RigTable) return null;
            var table = ArkRigTable.Parse(def.RigTable.text, out _);
            if (table == null) return null;
            var lays = new List<PrismLay>(table.sockets.Length);
            foreach (var socket in table.sockets)
            {
                if (socket == null) continue;
                lays.Add(new PrismLay(new SpawnPoint(socket.Position * def.Scale, socket.Rotation, socket.Size * def.Scale),
                                      domain, socket.Kind));
            }
            return lays;
        }

        /// <summary>Prisms laid on a bone: the puppetry tells them when it moves them.</summary>
        public void AddRiders(List<Prism> prisms)
        {
            if (prisms != null) _riders.AddRange(prisms);
        }

        /// <summary>One prism regrown on a bone (<see cref="Ark"/>'s reactor).</summary>
        public void AddRider(Prism prism)
        {
            if (prism) _riders.Add(prism);
        }

        /// <summary>
        /// Paint the model's Domain slot in <paramref name="domain"/>: every renderer slot that wears one of the
        /// definition's placeholder materials takes the domain's ship material (the identity swap vessels make). With
        /// no theme (or no set for the domain) the placeholder stays.
        /// </summary>
        public void ApplyDomain(Domains domain, ThemeManagerDataContainerSO theme)
        {
            if (!_root || _def.DomainReplacesMaterials.Count == 0) return;
            Material ship = theme?.TeamMaterialSets != null && theme.TeamMaterialSets.TryGetValue(domain, out var set) && set
                ? set.ShipMaterial
                : null;
            if (!ship) return;
            foreach (var renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) continue;
                    for (int j = 0; j < _def.DomainReplacesMaterials.Count; j++)
                    {
                        if (materials[i] != _def.DomainReplacesMaterials[j]) continue;
                        materials[i] = ship;
                        changed = true;
                        break;
                    }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        // ── Puppetry ─────────────────────────────────────────────────────────

        void LateUpdate()
        {
            if (!_ark || _puppets.Count == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 1e-5f) return;

            var rotation = _ark.transform.rotation;
            float speed = _ark.Speed;
            var target = Vector4.zero;
            if (_hasLast && !_ark.IsHullLost)
            {
                // The turn this frame, about the Ark's own axes: y is yaw (+ to the right), x is pitch (+ tips the
                // nose DOWN in Unity, so a rising nose is -x).
                (Quaternion.Inverse(_lastRotation) * rotation).ToAngleAxis(out float degrees, out Vector3 axis);
                if (degrees > 180f) degrees -= 360f;
                var rate = float.IsNaN(axis.x) || float.IsInfinity(axis.x) ? Vector3.zero : axis * (degrees / dt);
                float yaw = Mathf.Clamp(rate.y / _def.YawRateFull, -1f, 1f);
                target = new Vector4(
                    Mathf.Clamp(-rate.x / _def.PitchRateFull, -1f, 1f),
                    yaw,
                    yaw * _def.BankFromYaw,
                    Mathf.Clamp((speed - _lastSpeed) / dt / _def.AccelFull, -1f, 1f));
            }
            _lastRotation = rotation;
            _lastSpeed = speed;
            _hasLast = true;

            float follow = 1f - Mathf.Exp(-dt / _def.Response);
            _input += (target - _input) * follow;

            TickActions(dt);
            Pose(_input * _def.Amplitude);
            PoseReactor(dt);
        }

        /// <summary>
        /// The actions ease toward what the world says: the lance out for a while after the hold hands a payload over,
        /// the hangar open while a squadmate is in the hold's grab reach (and never on a fallen Ark).
        /// </summary>
        void TickActions(float dt)
        {
            float now = Time.time;
            if (now >= _nextHangarScanAt)
            {
                _nextHangarScanAt = now + HangarScanSeconds;
                _hangarWanted = !_ark.IsHullLost && SquadmateInReach();
            }
            float follow = 1f - Mathf.Exp(-dt / _def.ActionResponse);
            _release += ((now < _releaseUntil && !_ark.IsHullLost ? 1f : 0f) - _release) * follow;
            _hangar += ((_hangarWanted ? 1f : 0f) - _hangar) * follow;
        }

        /// <summary>A live vessel of the Ark's domain within the hold's grab reach: the one who could take a payload.</summary>
        bool SquadmateInReach()
        {
            float reach = _def.HangarReach > 0f ? _def.HangarReach : ArkHold.Config.GrabReach;
            float reachSqr = reach * reach;
            var centre = _ark.Position;
            var live = VesselStatus.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var vessel = live[i];
                if (!vessel) continue;
                IVesselStatus status = vessel;
                // A vessel with no player yet has no side to ask (its Domain getter would log one).
                if (status.Player == null || status.Domain != _ark.Domain) continue;
                if ((vessel.transform.position - centre).sqrMagnitude <= reachSqr) return true;
            }
            return false;
        }

        void Pose(Vector4 a)
        {
            for (int i = 0; i < _puppets.Count; i++)
            {
                var p = _puppets[i];
                var b = ArkRigTable.Budget(a, p.Moves);
                var delta = ArkRigTable.Blend(p.PitchUp, p.PitchDown, b.x)
                            * ArkRigTable.Blend(p.YawRight, p.YawLeft, b.y)
                            * ArkRigTable.Blend(p.RollRight, p.RollLeft, b.z)
                            * ArkRigTable.Blend(p.SpeedUp, p.SpeedDown, b.w)
                            * ArkRigTable.Blend(p.Release, Quaternion.identity, _release)
                            * ArkRigTable.Blend(p.HangarOpen, Quaternion.identity, _hangar);
                p.Bone.localRotation = ArkRigTable.PoseLocal(p.RestLocal, p.RestInArk, delta);
                if (p.ReleaseOffset != Vector3.zero)
                    p.Bone.localPosition = p.RestLocalPosition + p.ReleaseOffset * Mathf.Clamp01(_release);
            }

            // The jaws' blades ride the jaws: re-file them (spatial index, shell, render entity) when the jaws moved.
            if (_riders.Count == 0) return;
            float moved = Mathf.Max(Mathf.Abs(a.x - _riderInputAtNotify.x), Mathf.Abs(a.z - _riderInputAtNotify.z));
            if (moved < RiderNotifyInput) return;
            _riderInputAtNotify = a;
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                var prism = _riders[i];
                // A blade the hold handed to a payload carrier (Ark.TakeHullPrisms) is the carrier's to move now.
                if (!prism || !prism.transform.IsChildOf(_root))
                {
                    _riders.RemoveAt(i);
                    continue;
                }
                if (!prism.destroyed && prism.gameObject.activeInHierarchy)
                    prism.NotifyPositionChanged();
            }
        }

        /// <summary>
        /// The omni crystal in the well is as big as the charge aboard (a spent reactor holds a small dark seed, never
        /// nothing), easing so a harvest swells it and a regrowth draws it down; its collar turns about the well's axis,
        /// faster while the hull regrows. Model bones, moved like the jets - never a prism.
        /// </summary>
        void PoseReactor(float dt)
        {
            if (!_reactorDriven) return;
            if (_reactor)
            {
                _shownFill += (_reactorFill - _shownFill) * (1f - Mathf.Exp(-dt / _def.ActionResponse));
                _reactor.localScale = _reactorRestScale * Mathf.Lerp(_def.EmptyCrystalScale, 1f, _shownFill);
            }
            if (_collar)
            {
                float spin = _reactorFill <= 0f ? 0f : _regrowing ? _def.CollarSpinRegrowing : _def.CollarSpinIdle;
                _collarAngle = Mathf.Repeat(_collarAngle + spin * dt, 360f);
                _collar.localRotation = ArkRigTable.PoseLocal(_collarRestLocal, _collarRestInArk,
                    Quaternion.AngleAxis(_collarAngle, Vector3.up));
            }
        }
    }
}
