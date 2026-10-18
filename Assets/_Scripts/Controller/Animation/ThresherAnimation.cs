using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Thresher's hull. Design record: <c>R_VesselActions/THRESHER.md</c> §5b. Since 2026-10-18
    /// the hull is a MODEL (<c>Thresher.fbx</c>, built by /vessel-model from
    /// <c>Tools/Build/vessel_model/specs/thresher.py</c>) nested under the prefab's <c>ThresherHull</c>; its parts
    /// are BONES found by name (<c>tail</c>, <c>tail.2</c>-<c>tail.4</c>, <c>wing.L</c> / <c>wing.R</c>), and the
    /// procedural <see cref="ThresherHullBuilder"/>'s part names (<c>ThresherTailBase</c>, <c>ThresherLobe0</c>...,
    /// <c>ThresherPectoralL</c>...) still resolve, so either hull swims.
    ///
    /// <para><b>The read is a SHARK SWIMMING, and its tail is the weapon.</b> The scythe beats side to side
    /// as a travelling wave (<see cref="ThresherSwimMath"/>), quicker and fuller with speed; the stick banks
    /// the hull into a turn, swings the tail out of it and angles the pectorals. The wrecking ball drives the
    /// rest, read off the vessel's own <see cref="ThresherExecutor"/>: planted, the tail CURLS toward the ball
    /// and the hull banks into the orbit as the spin builds; a stop flares the pectorals like brakes; a smash
    /// or a launch CRACKS the tip like a whip. The vessel's own transform, its flight path and its trail never
    /// move with any of it.</para>
    ///
    /// <para><b>Channel discipline.</b> This component writes TRANSFORMS only: the hull's localRotation (bank,
    /// sway) and each part's pose. The element morph is the model's blend shapes (Charge / Mass / Space / Time,
    /// driven by the base class), or, on the procedural hull, the builder's vertices. No channel has two
    /// writers.</para>
    ///
    /// <para><b>Every part is posed in the HULL's frame onto the rest captured there</b>, so a bone that rests at
    /// an angle under a parent that is not hull-aligned turns about the hull's axes (the Butterfly lesson), and a
    /// procedural part resting at identity behaves exactly as before.</para>
    ///
    /// <para><b>Writes poses directly, not through the base's lerp helpers</b> — the
    /// <see cref="ButterflyAnimation"/> / <see cref="StoatAnimation"/> lesson: those lag a beat into a fraction
    /// of its authored amplitude. The beat is already a smooth function of time; the stick terms are pre-eased
    /// by the input layer and the ball terms are smoothed here.</para>
    ///
    /// <para><b>Amplitudes are fleet-scale</b> (the vessel contract, rule 24): a tail beat that reads at
    /// chase-camera distance — the Thresher's camera sits 55 u back — sweeps the scythe's tip through ~60°.</para>
    /// </summary>
    public class ThresherAnimation : VesselAnimation
    {
        [Header("Swim (the tail beat)")]
        [Tooltip("Tail beats per second at a standstill.")]
        [SerializeField, Min(0f)] float beatRate = ThresherSwimMath.DefaultBeatRate;
        [Tooltip("Tail beats per second at cruise speed.")]
        [SerializeField, Min(0f)] float beatRateAtCruise = ThresherSwimMath.DefaultBeatRateAtCruise;
        [Tooltip("Degrees the peduncle swings in a beat.")]
        [SerializeField] float baseSwingDegrees = ThresherSwimMath.DefaultBaseSwing;
        [Tooltip("Degrees EACH scythe segment adds in a beat; the wave grows to the tip.")]
        [SerializeField] float lobeSwingDegrees = ThresherSwimMath.DefaultLobeSwing;
        [Tooltip("Radians each segment trails the one before it — the wave travelling down the tail.")]
        [SerializeField, Range(0f, 2f)] float waveLag = ThresherSwimMath.DefaultWaveLag;
        [Tooltip("Speed (world u/s) treated as cruise for the speed terms. The Thresher cruises at its DefaultThrottleScaler, 110.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 110f;
        [Tooltip("Degrees the body counter-sways against the tail (a swimming shark's head moves too).")]
        [SerializeField] float bodySwayDegrees = 3f;

        [Header("Stick")]
        [Tooltip("Degrees the hull banks into a full-stick turn.")]
        [SerializeField] float bankDegrees = 22f;
        [Tooltip("Degrees the whole tail swings OUT of a full-stick turn, at the tip.")]
        [SerializeField] float turnSwingDegrees = ThresherSwimMath.DefaultTurnSwing;
        [Tooltip("Degrees the pectorals pitch with the stick (angle of attack).")]
        [SerializeField] float pectoralPitchDegrees = 24f;
        [Tooltip("Degrees the pectorals twist against each other in a full-stick turn (the inside fin's leading edge up).")]
        [SerializeField] float pectoralTwistDegrees = 26f;

        [Header("The ball")]
        [Tooltip("Planted (pivoting round the ball), the tail curls toward it by up to this many degrees at the tip, with the spin.")]
        [SerializeField] float whipCurlDegrees = ThresherSwimMath.DefaultWhipCurl;
        [Tooltip("Planted, the hull banks into the orbit by up to this many degrees, with the spin.")]
        [SerializeField] float orbitBankDegrees = 34f;
        [Tooltip("Degrees the pectorals flare down while the ball is stopping (they read as brakes).")]
        [SerializeField] float brakeFlareDegrees = 40f;
        [Tooltip("Degrees a smash or a launch cracks the scythe's tip, like a whip.")]
        [SerializeField] float crackDegrees = ThresherSwimMath.DefaultCrack;
        [Tooltip("How fast a crack decays, per second.")]
        [SerializeField, Min(0.1f)] float crackDecay = 6f;
        [Tooltip("How fast the ball terms (curl, bank, flare) ease to their target, per second.")]
        [SerializeField, Min(0.1f)] float ballEaseRate = 6f;

        [Header("Hull")]
        [Tooltip("The transform the swim banks and sways: ThresherHull, which holds the hull model and the jets. " +
                 "Leave empty and the child named 'ThresherHull' is used.")]
        [SerializeField] Transform hullRoot;

        /// <summary>The tail chain's joints by name, root first: the model's bone, then the procedural part.</summary>
        static readonly string[][] ChainNames =
        {
            new[] { "tail", "ThresherTailBase" },
            new[] { "tail.2", "ThresherLobe0" },
            new[] { "tail.3", "ThresherLobe1" },
            new[] { "tail.4", "ThresherLobe2" },
        };

        ThresherHullBuilder _hull;          // the procedural hull, when the prefab still carries it (element morphs only)
        ThresherExecutor _executor;
        Transform _hullTransform;
        Quaternion _hullRestRot;
        Transform[] _chain;                 // tail base, then every scythe segment
        Vector3[] _chainRest;               // each joint's pivot at rest, in the hull's space
        Quaternion[] _chainRestRot;         // each joint's orientation at rest, in the hull's frame
        Quaternion[] _chainLocal, _chainRot;
        Vector3[] _chainPos;
        float[] _jointYaws;
        readonly Transform[] _pectorals = new Transform[2];        // left, right
        readonly Quaternion[] _pectoralRestRot = new Quaternion[2];
        float _phase, _whip, _orbitBank, _flare, _crack, _crackSide = 1f;
        int _lastCombo;
        bool _wasHot;

        // ---- procedural element morphs (the StoatAnimation plumbing, verbatim in shape) ----
        readonly float[] _morphWeights = new float[4];      // ThresherHullForm.MorphElements order
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        ThresherSwimMath.Settings Swim => new()
        {
            BeatRate = beatRate,
            BeatRateAtCruise = beatRateAtCruise,
            BaseSwingDegrees = baseSwingDegrees,
            LobeSwingDegrees = lobeSwingDegrees,
            WaveLag = waveLag,
            TurnSwingDegrees = turnSwingDegrees,
            WhipCurlDegrees = whipCurlDegrees,
            CrackDegrees = crackDegrees,
        };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hull = GetComponentInChildren<ThresherHullBuilder>(true);
            // The model (Thresher.fbx, nested under ThresherHull) is found, not wired: its sub-objects' fileIDs
            // exist only after Unity imports it. The base drives its element shapes by name; the flares need the
            // renderer; and the beating whip and the Space morph reach past the bind-pose bounds, so it must not
            // be culled on them.
            if (!SkinnedMeshRenderer) SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (SkinnedMeshRenderer) SkinnedMeshRenderer.updateWhenOffscreen = true;
            // The vessel's OWN executor (it rides this prefab's ShipActions) — never another hull's.
            _executor = GetComponentInChildren<ThresherExecutor>(true);
            base.Initialize(vesselStatus);
            CaptureRest();
            InitializeProceduralMorphs(vesselStatus);
        }

        protected override void ResolveParts()
        {
            _hullTransform = hullRoot ? hullRoot : (_hull ? _hull.transform : transform.Find("ThresherHull"));
            if (!_hullTransform)
                CSDebug.LogWarning($"[ThresherAnimation] '{name}' has no ThresherHull; the swim has nothing to move.");

            int n = ChainNames.Length;
            _chain = new Transform[n];
            for (int j = 0; j < n; j++) _chain[j] = ResolvePart(null, ChainNames[j]);
            _pectorals[0] = ResolvePart(null, "wing.L", "ThresherPectoralL");
            _pectorals[1] = ResolvePart(null, "wing.R", "ThresherPectoralR");
            ReportUnresolvedParts();
        }

        /// <summary>Every part's rest pivot and orientation in the HULL's frame, captured once at the authored pose.</summary>
        void CaptureRest()
        {
            if (!_hullTransform || _chain == null || !_chain[0]) return;
            _hullRestRot = _hullTransform.localRotation;
            var toHull = Quaternion.Inverse(_hullTransform.rotation);

            int n = _chain.Length;
            _chainRest = new Vector3[n];
            _chainRestRot = new Quaternion[n];
            _chainLocal = new Quaternion[n];
            _chainRot = new Quaternion[n];
            _chainPos = new Vector3[n];
            _jointYaws = new float[n];
            for (int j = 0; j < n; j++)
            {
                var t = _chain[j];
                _chainRest[j] = t ? _hullTransform.InverseTransformPoint(t.position) : (j > 0 ? _chainRest[j - 1] : Vector3.zero);
                _chainRestRot[j] = t ? toHull * t.rotation : Quaternion.identity;
            }
            for (int i = 0; i < _pectorals.Length; i++)
                _pectoralRestRot[i] = _pectorals[i] ? toHull * _pectorals[i].rotation : Quaternion.identity;
        }

        // The parts are resolved by name in ResolveParts.
        protected override void AssignTransforms() { }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) => Drive(pitch, yaw);

        /// <summary>The base calls Idle() INSTEAD of puppetry when the sticks centre; a hands-off Thresher
        /// still swims, so the pose is driven from here too.</summary>
        protected override void Idle() => Drive(0f, 0f);

        void Drive(float pitch, float yaw)
        {
            if (!_hullTransform || _chainRest == null) return;
            float dt = Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -1f, 1f);
            yaw = Mathf.Clamp(yaw, -1f, 1f);

            float speed01 = VesselStatus != null ? Mathf.Clamp01(VesselStatus.Speed / cruiseSpeed) : 0f;
            var swim = Swim;
            _phase += ThresherSwimMath.BeatRate(swim, speed01) * Mathf.PI * 2f * dt;
            if (_phase > Mathf.PI * 64f) _phase -= Mathf.PI * 64f;   // keep the float small; sin has period 2π

            ReadBall(dt, out float whipTarget, out float bankTarget, out float flareTarget);
            float k = 1f - Mathf.Exp(-ballEaseRate * dt);
            _whip = Mathf.Lerp(_whip, whipTarget, k);
            _orbitBank = Mathf.Lerp(_orbitBank, bankTarget, k);
            _flare = Mathf.Lerp(_flare, flareTarget, k);
            _crack = Mathf.MoveTowards(_crack, 0f, crackDecay * dt);

            // The body first (every part below is posed in its frame): banks into the turn and into an orbit,
            // and counter-sways against the beat.
            float sway = -bodySwayDegrees * Mathf.Sin(_phase) * Mathf.Lerp(0.45f, 1f, speed01);
            _hullTransform.localRotation = _hullRestRot * Quaternion.Euler(0f, sway, -yaw * bankDegrees - _orbitBank);
            Quaternion hullRot = _hullTransform.rotation;

            // The tail: one chain, beat + steer + curl + crack, posed so no joint ever opens.
            // SIGN: ThresherSwimMath's yaw is positive toward the pilot's RIGHT (+X) - its steer term is negative
            // so a right turn swings the tail LEFT, "out of the turn", and its curl and crack are positive toward
            // a ball on the right. Unity's Euler(0, +a, 0) carries a part that trails the pivot (on -Z) to -X, the
            // LEFT, so the pose turns by -yaw. Until 2026-10-18 it turned by +yaw: the tail swung INTO a turn and
            // curled and cracked AWAY from the ball, and the body's counter-sway moved with the tail instead of
            // against it - never seen, because nobody had opened the procedural hull in Unity (THRESHER.md §5b).
            ThresherSwimMath.JointYaws(swim, _phase, Mathf.Max(speed01, Mathf.Abs(yaw)), yaw, _whip, _crack, _crackSide, _jointYaws);
            for (int j = 0; j < _jointYaws.Length; j++) _chainLocal[j] = Quaternion.Euler(0f, -_jointYaws[j], 0f);
            ThresherSwimMath.PoseChain(_chainRest, _chainLocal, _chainPos, _chainRot);
            for (int j = 0; j < _chain.Length; j++)
            {
                var part = _chain[j];
                if (!part) continue;
                part.SetPositionAndRotation(_hullTransform.TransformPoint(_chainPos[j]), hullRot * _chainRot[j] * _chainRestRot[j]);
            }

            // Pectorals: angle of attack with the stick, twisted against each other in a turn (ailerons:
            // the inside fin's leading edge up), and both tips flared DOWN while the ball stops.
            for (int i = 0; i < _pectorals.Length; i++)
            {
                var fin = _pectorals[i];
                if (!fin) continue;
                float side = i == 0 ? -1f : 1f;
                float attack = -pitch * pectoralPitchDegrees + side * yaw * pectoralTwistDegrees;
                // A positive Z roll swings +X up toward +Y: it DROPS the left fin's tip (it hangs toward -X)
                // and lifts the right's, so a flare is the opposite sign on each side.
                float flare = -side * _flare * brakeFlareDegrees;
                fin.rotation = hullRot * Quaternion.Euler(attack, 0f, flare) * _pectoralRestRot[i];
            }
        }

        /// <summary>What the wrecking ball asks of the pose this frame: the curl toward a planted ball and the
        /// bank into its orbit (both growing with the spin), the brake flare while it stops, and a crack on a
        /// new smash or a launch.</summary>
        void ReadBall(float dt, out float whip, out float bank, out float flare)
        {
            whip = 0f; bank = 0f; flare = 0f;
            if (!_executor) return;

            // Which side of the hull the ball is on (+1 = right), in the hull's own frame.
            Vector3 local = _hullTransform.parent
                ? _hullTransform.parent.InverseTransformPoint(_executor.BallPosition)
                : _executor.BallPosition - _hullTransform.position;
            float side = Mathf.Abs(local.x) > 1e-3f ? Mathf.Sign(local.x) : 1f;

            switch (_executor.Mode)
            {
                case ThresherMode.Skidding:
                    flare = 1f;
                    whip = side * 0.5f;
                    break;
                case ThresherMode.Pivot:
                    float spin = Mathf.Lerp(0.35f, 1f, _executor.Spin01);
                    whip = side * spin;
                    bank = side * orbitBankDegrees * spin;
                    break;
                case ThresherMode.Windmill:
                    whip = side * 0.6f;
                    break;
            }

            // A crack: a new smash in the combo, the ball turning red-hot, or a launch.
            bool hot = _executor.IsHot;
            int combo = _executor.Combo;
            if ((combo > _lastCombo && combo > 0) || (hot && !_wasHot) || _executor.LaunchedThisStep)
            {
                _crack = 1f;
                _crackSide = side;
            }
            _lastCombo = combo;
            _wasHot = hot;
        }

        void ResetPose()
        {
            if (!_hullTransform || _chainRest == null) return;
            _hullTransform.localRotation = _hullRestRot;
            Quaternion hullRot = _hullTransform.rotation;
            for (int j = 0; j < _chain.Length; j++)
                if (_chain[j]) _chain[j].SetPositionAndRotation(_hullTransform.TransformPoint(_chainRest[j]), hullRot * _chainRestRot[j]);
            for (int i = 0; i < _pectorals.Length; i++)
                if (_pectorals[i]) _pectorals[i].rotation = hullRot * _pectoralRestRot[i];
            _whip = _orbitBank = _flare = _crack = 0f;
        }

        // ---- element morph plumbing --------------------------------------------------------

        // Only while the prefab still carries the procedural hull: the model's element morphs are its blend shapes,
        // which the base class drives by name.
        void InitializeProceduralMorphs(IVesselStatus vesselStatus)
        {
            if (!_hull) return;
            var resources = vesselStatus?.ResourceSystem;
            if (resources == null) return;
            _procMorphConfig = VesselElementalMorphConfigSO.LoadDefault();
            resources.OnElementLevelChange -= HandleElementLevelForMorph;   // detach-first
            resources.OnElementLevelChange += HandleElementLevelForMorph;
            // Seed the spawn silhouette instantly — the event only covers CHANGES.
            foreach (var element in ThresherHullForm.MorphElements)
                GlideMorph(element, resources.GetLevel(element), instant: true);
        }

        void HandleElementLevelForMorph(Element element, int level) => GlideMorph(element, level, instant: false);

        void GlideMorph(Element element, int level, bool instant)
        {
            int index = System.Array.IndexOf(ThresherHullForm.MorphElements, element);
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

        // A pooled or swapped-away vessel must not carry a mid-beat pose into its next life.
        void OnDisable() => ResetPose();
    }
}
