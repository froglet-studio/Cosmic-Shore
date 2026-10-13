using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Stoat's plated hull (built by <see cref="StoatHullBuilder"/> /
    /// <see cref="StoatHullForm"/>). Design record: <c>R_VesselActions/STOAT.md</c> §2.2.
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
    /// authored amplitude. The lope is already a smooth function of time; the stick terms are
    /// pre-eased by the input layer.</para>
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

        StoatHullBuilder _hull;
        Transform _hullTransform;
        Vector3 _hullRestPos, _hullRestScale;
        Quaternion _hullRestRot;
        Vector3[] _plateRest, _tailRest, _legRest;
        Vector3 _headRest;
        float _phase;

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
            if (!_hull)
                CSDebug.LogWarning($"[StoatAnimation] '{name}' has no StoatHullBuilder in its children; the lope has nothing to move.");
            base.Initialize(vesselStatus);
            CaptureRest();
            InitializeProceduralMorphs(vesselStatus);
        }

        void CaptureRest()
        {
            if (!_hull) return;
            _hullTransform = _hull.transform;
            _hullRestPos = _hullTransform.localPosition;
            _hullRestRot = _hullTransform.localRotation;
            _hullRestScale = _hullTransform.localScale;
            _plateRest = new Vector3[_hull.Plates.Count];
            for (int i = 0; i < _plateRest.Length; i++) if (_hull.Plates[i]) _plateRest[i] = _hull.Plates[i].localPosition;
            _tailRest = new Vector3[_hull.Tail.Count];
            for (int i = 0; i < _tailRest.Length; i++) if (_hull.Tail[i]) _tailRest[i] = _hull.Tail[i].localPosition;
            _legRest = new Vector3[_hull.Legs.Count];
            for (int i = 0; i < _legRest.Length; i++) if (_hull.Legs[i]) _legRest[i] = _hull.Legs[i].localPosition;
            if (_hull.Head) _headRest = _hull.Head.localPosition;
        }

        // The parts are the builder's, found through it rather than by name, so nothing to resolve.
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
            yaw = Mathf.Clamp(yaw, -1f, 1f);

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
            int n = _hull.Plates.Count, core = _hull.CoreIndex;
            float bodyLength = Mathf.Max(0.01f, _hull.BodyLength);
            for (int i = 0; i < n; i++)
            {
                var plate = _hull.Plates[i];
                if (!plate || i == core) continue;   // the core is the hull transform itself
                float t = StoatHullForm.SegmentT(i, n);
                float lift = spineArchLift * pose.Spine * (Mathf.Sin(Mathf.PI * t) - 1f);
                // Slope of y = A·sin(πt) along +Z (t runs nose → hip, so z falls as t rises): a ∩
                // arch tilts the front plates nose-DOWN, which is a positive X rotation.
                float tilt = Mathf.Atan(spineArchLift * pose.Spine * Mathf.PI / bodyLength * Mathf.Cos(Mathf.PI * t)) * Mathf.Rad2Deg;
                float bend = yaw * turnBendDegrees * (1f - 2f * t);
                plate.localPosition = _plateRest[i] + Vector3.up * lift;
                plate.localRotation = Quaternion.Euler(tilt, bend, 0f);
            }

            if (_hull.Head)
            {
                _hull.Head.localPosition = _headRest + Vector3.up * spineArchLift * pose.Spine * -1f;
                _hull.Head.localRotation = Quaternion.Euler(headNodDegrees * pose.Spine, yaw * headTurnDegrees, 0f);
            }

            // The tail rides up at the landing and swings OUT of a turn, more toward the tip.
            int tn = _hull.Tail.Count;
            for (int j = 0; j < tn; j++)
            {
                var seg = _hull.Tail[j];
                if (!seg) continue;
                float f = (j + 1f) / tn;
                seg.localPosition = _tailRest[j] + Vector3.up * (tailLift * f * pose.Legs);
                seg.localRotation = Quaternion.Euler(0f, -yaw * tailSwingDegrees * f, 0f);
            }

            // Legs reach at the landing: the front pair forward (a negative X rotation swings a
            // downward leg toward +Z), the back pair back.
            for (int k = 0; k < _hull.Legs.Count; k++)
            {
                var leg = _hull.Legs[k];
                if (!leg) continue;
                float sign = k < 2 ? -1f : 1f;
                leg.localRotation = Quaternion.Euler(sign * legReachDegrees * pose.Legs, 0f, 0f);
            }
        }

        void ResetPose()
        {
            if (!_hullTransform || _plateRest == null) return;
            _hullTransform.localPosition = _hullRestPos;
            _hullTransform.localRotation = _hullRestRot;
            _hullTransform.localScale = _hullRestScale;
            for (int i = 0; i < _hull.Plates.Count && i < _plateRest.Length; i++)
                if (_hull.Plates[i] && _hull.Plates[i] != _hullTransform)
                {
                    _hull.Plates[i].localPosition = _plateRest[i];
                    _hull.Plates[i].localRotation = Quaternion.identity;
                }
            for (int j = 0; j < _hull.Tail.Count && j < _tailRest.Length; j++)
                if (_hull.Tail[j]) { _hull.Tail[j].localPosition = _tailRest[j]; _hull.Tail[j].localRotation = Quaternion.identity; }
            for (int k = 0; k < _hull.Legs.Count; k++)
                if (_hull.Legs[k]) _hull.Legs[k].localRotation = Quaternion.identity;
            if (_hull.Head) { _hull.Head.localPosition = _headRest; _hull.Head.localRotation = Quaternion.identity; }
        }

        // ---- element morph plumbing --------------------------------------------------------

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
            if (instant || _procMorphConfig == null || _procMorphConfig.morphDuration <= 0f)
            {
                _morphWeights[index] = target;
                _morphDirty = true;
                return;
            }
            _morphTweens[index] = DOTween
                .To(() => _morphWeights[index],
                    weight => { _morphWeights[index] = weight; _morphDirty = true; },
                    target, _procMorphConfig.morphDuration)
                .SetEase(_procMorphConfig.morphEase)
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
