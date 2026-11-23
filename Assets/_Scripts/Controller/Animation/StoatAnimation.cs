using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Stoat's plated hull. Design record: <c>R_VesselActions/STOAT.md</c> §2.1-§2.2.
    ///
    /// <para><b>Two hulls, one lope.</b> Since 2026-10-18 the hull is a MODEL
    /// (<c>Stoat.fbx</c>, built by <c>Tools/Build/vessel_model/specs/stoat.py</c>) nested under the
    /// prefab's hull transform: its BONES are the parts (<c>plate.0</c> .. <c>plate.4</c> with
    /// <c>hull</c> as the core plate, <c>nose</c>, <c>tail</c> .. <c>tail.5</c>, <c>leg.1.L</c> ..
    /// <c>leg.2.R</c>), each resting where the procedural part pivoted, and its element morphs are
    /// blend shapes the base class drives by name. The procedural placeholder
    /// (<see cref="StoatHullBuilder"/> / <see cref="StoatHullForm"/>) still works when a prefab carries
    /// it, until the model is accepted. Either way the parts are posed from their REST in the hull's
    /// frame, so a bone that rests at an imported orientation under a parent that is not hull-aligned
    /// moves exactly as an identity-rest procedural part did.</para>
    ///
    /// <para><b>The bounding lope is the vessel's read</b> — the user's pick from the Stoat
    /// viewer (round 2, Option 2): a BODY-ONLY bound. The drawn hull rises nose-up, arches long at
    /// the top, lands nose-down and bunched with the legs reaching, over and over; the vessel's own
    /// transform, its flight path and its prism trail never move with it (<see cref="StoatLopeMath"/>).
    /// On top of it the stick bends the spine into a turn and swings the tail out of it.</para>
    ///
    /// <para><b>Channel discipline.</b> This component writes TRANSFORMS only: the hull's
    /// localPosition / localRotation / localScale (lift, pitch, stretch) and each plate's, the
    /// head's, the tail's and the legs' local pose. The builder writes VERTICES only (the element
    /// morph). No channel has two writers.</para>
    ///
    /// <para><b>Writes poses directly, not through the base's lerp helpers</b> — the
    /// <see cref="ButterflyAnimation"/> lesson: those lag a 0.66 Hz bound into a fraction of its
    /// authored amplitude. The lope is already a smooth function of time; the stick term is eased HERE
    /// (<c>stickEaseRate</c>), because on a phone lifting the thumb is an instant <c>Idle()</c> that never
    /// sees the input layer's eased stick (vessel skill rule 53).</para>
    /// </summary>
    public class StoatAnimation : VesselAnimation
    {
        [Header("Bounding lope (the user's tuning; world units)")]
        [Tooltip("Peak lift of the drawn body above the vessel's path. The path itself never moves.")]
        [SerializeField, Min(0f)] float lopeAmplitude = 1.6f;
        [Tooltip("Bounds per second at a standstill.")]
        [SerializeField, Min(0f)] float lopeRate = 0.66f;
        [Tooltip("0..1: how much the spine arches at the top of the bound and bunches at the landing.")]
        [SerializeField, Range(0f, 1f)] float lopeArch = 0.45f;
        [Tooltip("0..1: extra stretch at the top of the bound and squash at the landing.")]
        [SerializeField, Range(0f, 1f)] float lopeSquash = 0.2f;
        [Tooltip("0..1: how much the bound quickens with speed. 0 = the same unhurried lope at every speed.")]
        [SerializeField, Range(0f, 1f)] float lopeSpeedLink = 0f;
        [Tooltip("Speed (world u/s) treated as cruise for the speed terms. The Squirrel hull this was cloned from cruises near 60.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 60f;

        [Header("Spine, head, tail, legs")]
        [Tooltip("How far the middle of the spine rises above the ends at a full arch, world units.")]
        [SerializeField, Min(0f)] float spineArchLift = 0.35f;
        [Tooltip("Degrees the front and back plates yaw into a full-stick turn (front into it, back out of it).")]
        [SerializeField] float turnBendDegrees = 12f;
        [Tooltip("Degrees the head turns into a full-stick turn.")]
        [SerializeField] float headTurnDegrees = 18f;
        [Tooltip("Degrees the head nods with the arch.")]
        [SerializeField] float headNodDegrees = 14f;
        [Tooltip("Degrees the tail tip swings OUT of a full-stick turn.")]
        [SerializeField] float tailSwingDegrees = 28f;
        [Tooltip("How high the tail rides at the landing, world units, at its tip.")]
        [SerializeField, Min(0f)] float tailLift = 0.45f;
        [Tooltip("Degrees the legs reach fore and aft at the landing.")]
        [SerializeField] float legReachDegrees = 52f;
        [Tooltip("How fast the stick terms (spine bend, head turn, tail swing) ease to the stick, per second. Keeps a " +
                 "touch lifting off the screen from snapping a full-stick bend straight in one frame.")]
        [SerializeField, Min(0.1f)] float stickEaseRate = 10f;

        [Header("Hull")]
        [Tooltip("The transform the lope lifts, pitches and stretches (and the tide spaghettifies): the GameObject " +
                 "that holds the hull model. Leave empty and it is the procedural builder's own transform, or else " +
                 "the parent of the model the vessel's SkinnedMeshRenderer belongs to.")]
        [SerializeField] Transform hullVisual;

        // Bone names first (the model, Stoat.fbx), the procedural builder's part names after.
        static readonly string[][] PlateNames =
        {
            new[] { "plate.0", "StoatPlate0" }, new[] { "plate.1", "StoatPlate1" }, new[] { "hull", "StoatCore" },
            new[] { "plate.3", "StoatPlate3" }, new[] { "plate.4", "StoatPlate4" },
        };
        static readonly string[][] TailNames =
        {
            new[] { "tail", "StoatTail0" }, new[] { "tail.2", "StoatTail1" }, new[] { "tail.3", "StoatTail2" },
            new[] { "tail.4", "StoatTail3" }, new[] { "tail.5", "StoatTailTip" },
        };
        static readonly string[][] LegNames =
        {
            new[] { "leg.1.L", "StoatLegFL" }, new[] { "leg.1.R", "StoatLegFR" },
            new[] { "leg.2.L", "StoatLegBL" }, new[] { "leg.2.R", "StoatLegBR" },
        };
        static readonly string[] HeadNames = { "nose", "StoatHead" };
        const int ModelCoreIndex = 2;   // `hull`, the middle plate

        StoatHullBuilder _hull;          // the procedural placeholder, when the prefab still carries it
        Transform _hullTransform;
        Vector3 _hullRestPos, _hullRestScale;
        Quaternion _hullRestRot;
        Transform[] _plates, _tail, _legs;
        Transform _head;
        int _coreIndex;
        float _bodyLength;
        // Each part's rest in the HULL TRANSFORM's frame (what the pose is composed onto) and its local rest
        // (what a pooled vessel is reset to). On the procedural hull the two coincide and the rotation is identity.
        Vector3[] _plateRest, _tailRest, _legRest;
        Quaternion[] _plateRestRot, _tailRestRot, _legRestRot;
        Vector3[] _plateLocal, _tailLocal, _legLocal;
        Quaternion[] _plateLocalRot, _tailLocalRot, _legLocalRot;
        Vector3 _headRest, _headLocal;
        Quaternion _headRestRot, _headLocalRot;
        float _phase;
        float _yawEased;                 // the stick, eased here: Idle() never sees the input layer's eased stick

        // ---- procedural element morphs (the ButterflyAnimation plumbing, verbatim in shape) ----
        readonly float[] _morphWeights = new float[4];      // StoatHullForm.MorphElements order
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        StoatLopeMath.Settings Lope => new()
        {
            Amplitude = lopeAmplitude, Rate = lopeRate, Arch = lopeArch, Squash = lopeSquash, SpeedLink = lopeSpeedLink,
        };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hull = GetComponentInChildren<StoatHullBuilder>(true);
            _hullTransform = hullVisual ? hullVisual : (_hull ? _hull.transform : null);
            // The model (Stoat.fbx, nested under the hull transform) is found, not wired: its sub-objects'
            // fileIDs exist only after Unity imports it. Its element shapes are driven by the base class by
            // name; the flares need the renderer; the lope lifts the hull 1.6 u and the Space morph reaches
            // past the bind-pose bounds, so it must not be culled on them.
            if (!SkinnedMeshRenderer)
                SkinnedMeshRenderer = _hullTransform
                    ? _hullTransform.GetComponentInChildren<SkinnedMeshRenderer>(true)
                    : GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            if (!_hullTransform && SkinnedMeshRenderer)
            {
                var modelRoot = SkinnedMeshRenderer.transform.parent;
                _hullTransform = modelRoot && modelRoot.parent ? modelRoot.parent : modelRoot;
            }
            base.Initialize(vesselStatus);
            ResolveHullParts();
            CaptureRest();
            InitializeProceduralMorphs(vesselStatus);
        }

        /// <summary>The parts, from the procedural builder when the prefab carries one, else the model's bones
        /// (looked up under the hull transform only, so a same-named object elsewhere on the vessel - the
        /// VesselTail, the HUD - can never be taken for a bone).</summary>
        void ResolveHullParts()
        {
            if (_hull)
            {
                _plates = Copy(_hull.Plates);
                _tail = Copy(_hull.Tail);
                _legs = Copy(_hull.Legs);
                _head = _hull.Head;
                _coreIndex = _hull.CoreIndex;
                _bodyLength = _hull.BodyLength;
                return;
            }
            if (!_hullTransform)
            {
                CSDebug.LogWarning($"[StoatAnimation] '{name}' has neither a StoatHullBuilder nor a hull model; the lope has nothing to move.");
                return;
            }

            var byName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in _hullTransform.GetComponentsInChildren<Transform>(true))
                if (t != _hullTransform && !byName.ContainsKey(t.name)) byName[t.name] = t;
            var missing = new List<string>();
            _plates = Lookup(byName, PlateNames, missing);
            _tail = Lookup(byName, TailNames, missing);
            _legs = Lookup(byName, LegNames, missing);
            _head = Lookup(byName, HeadNames, missing);
            _coreIndex = ModelCoreIndex;
            if (missing.Count > 0)
                CSDebug.LogWarning($"[StoatAnimation] '{name}' could not resolve hull bone(s): {string.Join(", ", missing)}. " +
                                   "They will not lope - check that the model under the hull is Stoat.fbx (specs/stoat.py).");

            // The body length the arch's slope is measured over: the nose plate to the hip plate, one plate
            // length past each centre (4 u on the shipped hull, the placeholder's BodyLength).
            var first = _plates[0];
            var last = _plates[_plates.Length - 1];
            _bodyLength = first && last
                ? Mathf.Abs(_hullTransform.InverseTransformPoint(first.position).z - _hullTransform.InverseTransformPoint(last.position).z)
                  * _plates.Length / Mathf.Max(1, _plates.Length - 1)
                : 4f;
        }

        static Transform[] Copy(IReadOnlyList<Transform> list)
        {
            var array = new Transform[list.Count];
            for (int i = 0; i < array.Length; i++) array[i] = list[i];
            return array;
        }

        static Transform[] Lookup(Dictionary<string, Transform> byName, string[][] names, List<string> missing)
        {
            var array = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++) array[i] = Lookup(byName, names[i], missing);
            return array;
        }

        static Transform Lookup(Dictionary<string, Transform> byName, string[] candidates, List<string> missing)
        {
            for (int i = 0; i < candidates.Length; i++)
                if (byName.TryGetValue(candidates[i], out var found)) return found;
            missing.Add(candidates[0]);
            return null;
        }

        void CaptureRest()
        {
            if (!_hullTransform || _plates == null) return;
            _hullRestPos = _hullTransform.localPosition;
            _hullRestRot = _hullTransform.localRotation;
            _hullRestScale = _hullTransform.localScale;
            Capture(_plates, out _plateRest, out _plateRestRot, out _plateLocal, out _plateLocalRot);
            Capture(_tail, out _tailRest, out _tailRestRot, out _tailLocal, out _tailLocalRot);
            Capture(_legs, out _legRest, out _legRestRot, out _legLocal, out _legLocalRot);
            if (_head) CaptureOne(_head, out _headRest, out _headRestRot, out _headLocal, out _headLocalRot);
        }

        void Capture(Transform[] parts, out Vector3[] rest, out Quaternion[] restRot, out Vector3[] local, out Quaternion[] localRot)
        {
            rest = new Vector3[parts.Length];
            restRot = new Quaternion[parts.Length];
            local = new Vector3[parts.Length];
            localRot = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                restRot[i] = localRot[i] = Quaternion.identity;
                if (parts[i]) CaptureOne(parts[i], out rest[i], out restRot[i], out local[i], out localRot[i]);
            }
        }

        void CaptureOne(Transform part, out Vector3 rest, out Quaternion restRot, out Vector3 local, out Quaternion localRot)
        {
            local = part.localPosition;
            localRot = part.localRotation;
            if (part.parent == _hullTransform)
            {
                rest = local;
                restRot = localRot;
                return;
            }
            rest = _hullTransform.InverseTransformPoint(part.position);
            restRot = Quaternion.Inverse(_hullTransform.rotation) * part.rotation;
        }

        /// <summary>Pose a part in the HULL TRANSFORM's frame: <paramref name="position"/> and
        /// <paramref name="rotation"/> are where it sits and how it is turned relative to the hull. A direct child
        /// (the procedural parts) is written locally, exactly as before; a bone deeper in the model is carried
        /// there through the hull's live transform, so its parent chain's own rest orientation never enters.</summary>
        void SetPose(Transform part, Vector3 position, Quaternion rotation)
        {
            if (part.parent == _hullTransform)
            {
                part.localPosition = position;
                part.localRotation = rotation;
                return;
            }
            part.SetPositionAndRotation(_hullTransform.TransformPoint(position), _hullTransform.rotation * rotation);
        }

        void SetRotation(Transform part, Quaternion rotation)
        {
            if (part.parent == _hullTransform) part.localRotation = rotation;
            else part.rotation = _hullTransform.rotation * rotation;
        }

        // The parts come from the builder or, on the model, by bone name (Initialize); the base class's
        // Transforms list is not used, so nothing to assign.
        protected override void AssignTransforms() { }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) => Drive(yaw);

        /// <summary>The base calls Idle() INSTEAD of puppetry when the sticks centre; a hands-off
        /// Stoat still lopes, so the pose is driven from here too.</summary>
        protected override void Idle() => Drive(0f);

        void Drive(float yaw)
        {
            if (!_hullTransform || _plateRest == null) return;

            float speed01 = VesselStatus != null ? Mathf.Clamp01(VesselStatus.Speed / cruiseSpeed) : 0f;
            var lope = Lope;
            _phase += StoatLopeMath.PhaseRate(lope, speed01) * Time.deltaTime;
            if (_phase > Mathf.PI * 64f) _phase -= Mathf.PI * 64f;   // keep the float small; sin² has period π
            var pose = StoatLopeMath.Evaluate(lope, _phase, speed01);
            _yawEased = Mathf.Lerp(_yawEased, Mathf.Clamp(yaw, -1f, 1f), 1f - Mathf.Exp(-stickEaseRate * Time.deltaTime));
            yaw = _yawEased;

            // The whole drawn hull: lift, pitch, stretch (volume kept). The vessel's transform is untouched.
            _hullTransform.localPosition = _hullRestPos + Vector3.up * pose.BodyLift;
            _hullTransform.localRotation = _hullRestRot * Quaternion.Euler(pose.PitchDegrees, 0f, 0f);
            // Spaghettification (Docs/BLACK_HOLE.md §11): near a horizon the tide stretches the drawn hull
            // along its length — falling into a black hole, and in reverse leaving a white one. The hull's
            // own axis stands in for the line to the hole, which is where a hull falling in or flying out
            // points. Volume kept, like the lope's own stretch it multiplies.
            float tide = Mathf.Exp(BlackHoleWarp.VesselLogStretch(transform.position, out _));
            float stretch = pose.Stretch * tide;
            float girth = 1f / Mathf.Sqrt(Mathf.Max(0.1f, stretch));
            _hullTransform.localScale = Vector3.Scale(_hullRestScale, new Vector3(girth, girth, stretch));

            // The spine: plates rise toward the middle when arched (∩) and sink when bunched (∪),
            // each tilted along the curve's slope; the stick bends the front into a turn and the back out.
            int n = _plates.Length, core = _coreIndex;
            float bodyLength = Mathf.Max(0.01f, _bodyLength);
            for (int i = 0; i < n; i++)
            {
                var plate = _plates[i];
                if (!plate || i == core) continue;   // the core rides the hull transform itself
                float t = StoatHullForm.SegmentT(i, n);
                float lift = spineArchLift * pose.Spine * (Mathf.Sin(Mathf.PI * t) - 1f);
                // Slope of y = A·sin(πt) along +Z (t runs nose → hip, so z falls as t rises): a ∩
                // arch tilts the front plates nose-DOWN, which is a positive X rotation.
                float tilt = Mathf.Atan(spineArchLift * pose.Spine * Mathf.PI / bodyLength * Mathf.Cos(Mathf.PI * t)) * Mathf.Rad2Deg;
                float bend = yaw * turnBendDegrees * (1f - 2f * t);
                SetPose(plate, _plateRest[i] + Vector3.up * lift, Quaternion.Euler(tilt, bend, 0f) * _plateRestRot[i]);
            }

            if (_head)
                SetPose(_head, _headRest + Vector3.up * spineArchLift * pose.Spine * -1f,
                    Quaternion.Euler(headNodDegrees * pose.Spine, yaw * headTurnDegrees, 0f) * _headRestRot);

            // The tail rides up at the landing and swings OUT of a turn, more toward the tip.
            int tn = _tail.Length;
            for (int j = 0; j < tn; j++)
            {
                var seg = _tail[j];
                if (!seg) continue;
                float f = (j + 1f) / tn;
                SetPose(seg, _tailRest[j] + Vector3.up * (tailLift * f * pose.Legs),
                    Quaternion.Euler(0f, -yaw * tailSwingDegrees * f, 0f) * _tailRestRot[j]);
            }

            // Legs reach at the landing: the front pair forward (a negative X rotation swings a
            // downward leg toward +Z), the back pair back.
            for (int k = 0; k < _legs.Length; k++)
            {
                var leg = _legs[k];
                if (!leg) continue;
                float sign = k < 2 ? -1f : 1f;
                SetRotation(leg, Quaternion.Euler(sign * legReachDegrees * pose.Legs, 0f, 0f) * _legRestRot[k]);
            }
        }

        void ResetPose()
        {
            if (!_hullTransform || _plateRest == null) return;
            _hullTransform.localPosition = _hullRestPos;
            _hullTransform.localRotation = _hullRestRot;
            _hullTransform.localScale = _hullRestScale;
            ResetParts(_plates, _plateLocal, _plateLocalRot);
            ResetParts(_tail, _tailLocal, _tailLocalRot);
            ResetParts(_legs, _legLocal, _legLocalRot);
            if (_head) { _head.localPosition = _headLocal; _head.localRotation = _headLocalRot; }
            _yawEased = 0f;
        }

        void ResetParts(Transform[] parts, Vector3[] local, Quaternion[] localRot)
        {
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] && parts[i] != _hullTransform)
                {
                    parts[i].localPosition = local[i];
                    parts[i].localRotation = localRot[i];
                }
        }

        // ---- element morph plumbing (the procedural placeholder only: the model's morphs are blend
        // shapes named Charge / Mass / Space / Time, which the base class drives) ---------------

        void InitializeProceduralMorphs(IVesselStatus vesselStatus)
        {
            if (!_hull) return;
            var resources = vesselStatus?.ResourceSystem;
            if (resources == null) return;
            _procMorphConfig = VesselElementalMorphConfigSO.LoadDefault();
            resources.OnElementLevelChange -= HandleElementLevelForMorph;   // detach-first
            resources.OnElementLevelChange += HandleElementLevelForMorph;
            // Seed the spawn silhouette instantly — the event only covers CHANGES.
            foreach (var element in StoatHullForm.MorphElements)
                GlideMorph(element, resources.GetLevel(element), instant: true);
        }

        void HandleElementLevelForMorph(Element element, int level) => GlideMorph(element, level, instant: false);

        void GlideMorph(Element element, int level, bool instant)
        {
            int index = System.Array.IndexOf(StoatHullForm.MorphElements, element);
            if (index < 0) return;   // None/Omni are not morph targets
            float target = VesselElementalMorphConfigSO.NormalizedMorphWeight(level);
            _morphTweens[index]?.Kill();
            if (instant || _procMorphConfig == null || _procMorphConfig.MorphDuration <= 0f)
            {
                _morphWeights[index] = target;
                _morphDirty = true;
                return;
            }
            _morphTweens[index] = DOTween
                .To(() => _morphWeights[index],
                    weight => { _morphWeights[index] = weight; _morphDirty = true; },
                    target, _procMorphConfig.MorphDuration)
                .SetEase(_procMorphConfig.MorphEase)
                .SetLink(gameObject);
        }

        /// <summary>Single writer for the morph push, after the base's shape-key write.</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!_morphDirty || !_hull) return;
            _morphDirty = false;
            _hull.ApplyElementMorphWeights(_morphWeights[0], _morphWeights[1], _morphWeights[2], _morphWeights[3]);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            for (int i = 0; i < _morphTweens.Length; i++) _morphTweens[i]?.Kill();
            if (VesselStatus?.ResourceSystem != null)
                VesselStatus.ResourceSystem.OnElementLevelChange -= HandleElementLevelForMorph;
        }

        // A pooled or swapped-away vessel must not carry a mid-bound pose into its next life.
        void OnDisable() => ResetPose();
    }
}
