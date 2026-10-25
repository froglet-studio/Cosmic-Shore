using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Angler's own hull, <c>Angler.fbx</c> (built by <c>Tools/Build/vessel_model/specs/angler.py</c>;
    /// design record <c>R_VesselActions/ANGLER.md</c> §9). Until 2026-10-18 the Angler wore the Stoat's procedural hull
    /// and lope as a stand-in body; its own hull gets its own, simpler motion, and <see cref="StoatAnimation"/> stays
    /// the Stoat's.
    ///
    /// <para><b>What moves.</b> The tail boom (<c>tail</c>) and the caudal stabiliser (<c>tail.2</c>) swim in a slow
    /// beat and swing OUT of a turn; the stabilisers (<c>fin.L</c> / <c>fin.R</c>) idle and lift with the pitch stick;
    /// the sensor mast (<c>lure</c>) bobs; and while the LURE is held (<see cref="AnglerCommandExecutor.Luring"/>) the
    /// jaw (<c>jaw.B</c>) opens and the lure bulb (<c>esca</c>, Engine slot, so it glows) swells - the ship's own esca,
    /// beside the world mark the executor draws at the lure point. Near a horizon the hull transform is stretched by the
    /// tide exactly as the stand-in's was (<c>BlackHoleWarp.VesselLogStretch</c>).</para>
    ///
    /// <para><b>Channels.</b> This writes the bones' rotations, the bulb's scale and the hull transform's scale; the
    /// element morphs are the model's blend shapes (Charge / Mass / Space / Time), which the base class drives by name.
    /// Every bone is posed from its REST in the hull transform's frame, so its imported rest orientation never enters.
    /// Poses are written directly (the eases are explicit), never through the base's lerp helpers. The stick terms
    /// are eased here (<c>stickEaseRate</c>), and <c>Idle()</c> drives the same eased path to rest: on a phone lifting
    /// the thumb is an instant <c>Idle()</c> (vessel skill rule 53).</para>
    /// </summary>
    public class AnglerAnimation : VesselAnimation
    {
        [Header("Hull")]
        [Tooltip("The transform that holds the hull model (AnglerHull); the tide stretches it. Leave empty and it is " +
                 "the parent of the model the vessel's SkinnedMeshRenderer belongs to.")]
        [SerializeField] Transform hullVisual;

        [Header("Swim")]
        [Tooltip("Tail beats per second.")]
        [SerializeField, Min(0f)] float swimRate = 0.5f;
        [Tooltip("Degrees the tail boom swings either way in the idle beat; the caudal swings further.")]
        [SerializeField, Min(0f)] float swimDegrees = 6f;
        [Tooltip("Degrees the tail boom swings OUT of a full-stick turn.")]
        [SerializeField] float turnTailDegrees = 18f;
        [Tooltip("The caudal's swing as a multiple of the boom's.")]
        [SerializeField, Min(0f)] float caudalFollow = 1.6f;
        [Tooltip("How fast the stick terms (tail swing, stabiliser lift) ease to the stick, per second. Keeps a touch " +
                 "lifting off the screen from snapping a full-stick swing straight in one frame.")]
        [SerializeField, Min(0.1f)] float stickEaseRate = 10f;

        [Header("Stabilisers and mast")]
        [Tooltip("Degrees the stabilisers lift with a full pitch stick (both tips together).")]
        [SerializeField] float finPitchDegrees = 14f;
        [Tooltip("Degrees of the stabilisers' idle flap.")]
        [SerializeField, Min(0f)] float finIdleDegrees = 4f;
        [Tooltip("Degrees the sensor mast bobs.")]
        [SerializeField, Min(0f)] float mastBobDegrees = 3f;

        [Header("Lure")]
        [Tooltip("Degrees the jaw opens while the lure is held.")]
        [SerializeField, Min(0f)] float jawOpenDegrees = 22f;
        [Tooltip("The lure bulb's scale while the lure is held (1 = at rest).")]
        [SerializeField, Min(1f)] float escaLitScale = 1.6f;
        [Tooltip("How fast the jaw and the bulb ease in and out, per second (1 / seconds for the whole travel).")]
        [SerializeField, Min(0.01f)] float lureEaseRate = 4f;

        Transform _hullTransform;
        Vector3 _hullRestScale;
        Transform _jaw, _mast, _esca, _finL, _finR, _tail, _caudal;
        Quaternion _jawRest, _mastRest, _finLRest, _finRRest, _tailRest, _caudalRest;   // in the hull transform's frame
        Quaternion _jawLocal, _mastLocal, _finLLocal, _finRLocal, _tailLocal, _caudalLocal;
        Vector3 _escaScale;
        AnglerCommandExecutor _angler;
        float _phase, _lure01;
        float _pitchEased, _yawEased;    // the stick, eased here: Idle() never sees the input layer's eased stick

        public override void Initialize(IVesselStatus vesselStatus)
        {
            // The model (Angler.fbx, nested under the hull transform) is found, not wired: its sub-objects' fileIDs exist
            // only after Unity imports it. The base drives its element shapes by name; the flares need the renderer; the
            // morphs and the swim reach past the bind-pose bounds, so it must not be culled on them.
            if (!SkinnedMeshRenderer)
                SkinnedMeshRenderer = hullVisual
                    ? hullVisual.GetComponentInChildren<SkinnedMeshRenderer>(true)
                    : GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            _hullTransform = hullVisual;
            if (!_hullTransform && SkinnedMeshRenderer)
            {
                var modelRoot = SkinnedMeshRenderer.transform.parent;
                _hullTransform = modelRoot && modelRoot.parent ? modelRoot.parent : modelRoot;
            }
            _angler = vesselStatus?.Transform ? vesselStatus.Transform.GetComponentInChildren<AnglerCommandExecutor>(true)
                                              : GetComponentInChildren<AnglerCommandExecutor>(true);
            base.Initialize(vesselStatus);
            ResolveBones();
        }

        // The bones are found by name under the hull transform (ResolveBones), so nothing to assign.
        protected override void AssignTransforms() { }

        void ResolveBones()
        {
            if (!_hullTransform)
            {
                CSDebug.LogWarning($"[AnglerAnimation] '{name}' has no hull model (Angler.fbx); nothing will swim.");
                return;
            }
            _hullRestScale = _hullTransform.localScale;
            var byName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in _hullTransform.GetComponentsInChildren<Transform>(true))
                if (t != _hullTransform && !byName.ContainsKey(t.name)) byName[t.name] = t;
            var missing = new List<string>();
            _jaw = Find(byName, "jaw.B", missing);
            _mast = Find(byName, "lure", missing);
            _esca = Find(byName, "esca", missing);
            _finL = Find(byName, "fin.L", missing);
            _finR = Find(byName, "fin.R", missing);
            _tail = Find(byName, "tail", missing);
            _caudal = Find(byName, "tail.2", missing);
            if (missing.Count > 0)
                CSDebug.LogWarning($"[AnglerAnimation] '{name}' could not resolve hull bone(s): {string.Join(", ", missing)}. " +
                                   "They will not move - check that the model under the hull is Angler.fbx (specs/angler.py).");
            Capture(_jaw, out _jawRest, out _jawLocal);
            Capture(_mast, out _mastRest, out _mastLocal);
            Capture(_finL, out _finLRest, out _finLLocal);
            Capture(_finR, out _finRRest, out _finRLocal);
            Capture(_tail, out _tailRest, out _tailLocal);
            Capture(_caudal, out _caudalRest, out _caudalLocal);
            _escaScale = _esca ? _esca.localScale : Vector3.one;
        }

        static Transform Find(Dictionary<string, Transform> byName, string bone, List<string> missing)
        {
            if (byName.TryGetValue(bone, out var found)) return found;
            missing.Add(bone);
            return null;
        }

        void Capture(Transform part, out Quaternion restInHull, out Quaternion local)
        {
            restInHull = local = Quaternion.identity;
            if (!part) return;
            local = part.localRotation;
            restInHull = Quaternion.Inverse(_hullTransform.rotation) * part.rotation;
        }

        /// <summary>Turn a bone by <paramref name="turn"/> about the HULL's axes, from its rest.</summary>
        void Turn(Transform part, Quaternion turn, Quaternion restInHull)
        {
            if (part) part.rotation = _hullTransform.rotation * turn * restInHull;
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) => Drive(pitch, yaw);

        /// <summary>The base calls Idle() INSTEAD of puppetry when the sticks centre; a hands-off Angler still swims.</summary>
        protected override void Idle() => Drive(0f, 0f);

        void Drive(float pitch, float yaw)
        {
            if (!_hullTransform) return;
            float dt = Time.deltaTime;
            _phase += Mathf.PI * 2f * swimRate * dt;
            if (_phase > Mathf.PI * 64f) _phase -= Mathf.PI * 64f;
            float ks = 1f - Mathf.Exp(-stickEaseRate * dt);
            _pitchEased = Mathf.Lerp(_pitchEased, Mathf.Clamp(pitch, -1f, 1f), ks);
            _yawEased = Mathf.Lerp(_yawEased, Mathf.Clamp(yaw, -1f, 1f), ks);
            pitch = _pitchEased;
            yaw = _yawEased;

            // The tide: near a horizon the drawn hull is stretched along its length, volume kept (the stand-in's rule).
            float stretch = Mathf.Exp(BlackHoleWarp.VesselLogStretch(transform.position, out _));
            float girth = 1f / Mathf.Sqrt(Mathf.Max(0.1f, stretch));
            _hullTransform.localScale = Vector3.Scale(_hullRestScale, new Vector3(girth, girth, stretch));

            // The tail swims and swings OUT of a turn: yaw > 0 is a right turn, and a positive Y turn carries a rearward
            // part toward -X, the pilot's left - the outside of a right turn.
            float swing = Mathf.Sin(_phase) * swimDegrees + yaw * turnTailDegrees;
            Turn(_tail, Quaternion.Euler(0f, swing, 0f), _tailRest);
            float lagged = Mathf.Sin(_phase - 0.9f) * swimDegrees + yaw * turnTailDegrees;
            Turn(_caudal, Quaternion.Euler(0f, lagged * caudalFollow, 0f), _caudalRest);

            // The stabilisers: an idle flap and a lift with the pitch stick, both tips together (the left one on -X lifts
            // under a negative Z turn, the right one under a positive).
            float dihedral = pitch * finPitchDegrees + Mathf.Sin(_phase * 0.5f) * finIdleDegrees;
            Turn(_finL, Quaternion.Euler(0f, 0f, -dihedral), _finLRest);
            Turn(_finR, Quaternion.Euler(0f, 0f, dihedral), _finRRest);

            // The lure: the jaw opens (a positive X turn drops a forward-pointing jaw) and the bulb swells while it is held.
            bool luring = _angler && _angler.Luring;
            _lure01 = Mathf.MoveTowards(_lure01, luring ? 1f : 0f, lureEaseRate * dt);
            float eased = _lure01 * _lure01 * (3f - 2f * _lure01);
            Turn(_jaw, Quaternion.Euler(jawOpenDegrees * eased, 0f, 0f), _jawRest);
            Turn(_mast, Quaternion.Euler(Mathf.Sin(_phase * 0.5f + 1.3f) * mastBobDegrees, 0f, 0f), _mastRest);
            if (_esca) _esca.localScale = _escaScale * Mathf.Lerp(1f, escaLitScale, eased);
        }

        void ResetPose()
        {
            if (!_hullTransform) return;
            _hullTransform.localScale = _hullRestScale;
            if (_jaw) _jaw.localRotation = _jawLocal;
            if (_mast) _mast.localRotation = _mastLocal;
            if (_finL) _finL.localRotation = _finLLocal;
            if (_finR) _finR.localRotation = _finRLocal;
            if (_tail) _tail.localRotation = _tailLocal;
            if (_caudal) _caudal.localRotation = _caudalLocal;
            if (_esca) _esca.localScale = _escaScale;
            _lure01 = 0f;
            _pitchEased = _yawEased = 0f;
        }

        // A pooled or swapped-away vessel must not carry a mid-swim pose into its next life.
        void OnDisable() => ResetPose();
    }
}
