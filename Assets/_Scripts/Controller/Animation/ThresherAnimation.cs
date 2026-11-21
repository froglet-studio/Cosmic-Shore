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
    /// <para><b>The tail is the last links of the cord, and the cord is the weapon.</b> The chain leaves from
    /// the whip's tip, so the tail hangs along it (<see cref="ThresherSwimMath"/>, Garrett 2026-10-19: "remove
    /// the tail wiggle when flying. let the tail more naturally sway as though it is the last links of the
    /// cord"): every joint follows its share of the bend toward a point down the chain
    /// (<see cref="ThresherExecutor.TryGetCordPoint"/>) on a damped spring that lags behind the hull's own
    /// turns. Towed straight, it lies straight; a turn, a planted orbit or a swinging ball sways it and it
    /// settles. It never beats on its own. The stick banks the hull into a turn and angles the pectorals; the
    /// wrecking ball drives the rest, read off the vessel's own <see cref="ThresherExecutor"/>: planted, the
    /// hull banks into the orbit as the spin builds; a stop flares the pectorals like brakes; a smash or a
    /// launch CRACKS the tip like a whip. The vessel's own transform, its flight path and its trail never move
    /// with any of it.</para>
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
    /// <see cref="ButterflyAnimation"/> / <see cref="StoatAnimation"/> lesson: those lag a pose into a fraction
    /// of its authored amplitude. The tail is its own springs; the stick terms are eased here (the base calls
    /// <see cref="Idle"/> with centred sticks the frame a touch lifts, which used to snap the bank flat), and
    /// the ball terms are smoothed here.</para>
    ///
    /// <para><b>Amplitudes are fleet-scale</b> (the vessel contract, rule 24): planted, the tail swings its tip
    /// round to the ball, up to 110° off the hull's axis, which reads at the 55 u chase camera.</para>
    /// </summary>
    public class ThresherAnimation : VesselAnimation
    {
        [Header("Tail (the last links of the cord)")]
        [Tooltip("How far down the chain (world units from the hull's centre) the tail aims. Farther reads the " +
                 "cord's overall line, nearer its last links' swing.")]
        [SerializeField, Min(1f)] float cordSampleDistance = 12f;
        [Tooltip("The most the tail bends sideways to lie along the cord, at the tip, in degrees.")]
        [SerializeField, Range(0f, 150f)] float maxBendYawDegrees = ThresherSwimMath.DefaultMaxYaw;
        [Tooltip("The most the tail bends up or down to lie along the cord, at the tip, in degrees.")]
        [SerializeField, Range(0f, 90f)] float maxBendPitchDegrees = ThresherSwimMath.DefaultMaxPitch;
        [Tooltip("The peduncle's spring, in Hz. Stiffer follows the cord sooner.")]
        [SerializeField, Min(0.05f)] float rootFrequency = ThresherSwimMath.DefaultRootFrequency;
        [Tooltip("The tip's spring, in Hz. Softer than the root, so the tip lags and sways.")]
        [SerializeField, Min(0.05f)] float tipFrequency = ThresherSwimMath.DefaultTipFrequency;
        [Tooltip("Damping ratio of the tail's springs. Under 1 the tail overshoots and settles; 1 never overshoots.")]
        [SerializeField, Range(0.05f, 2f)] float tailDamping = ThresherSwimMath.DefaultDamping;
        [Tooltip("0..1: how much of the hull's own turn the peduncle resists (keeps pointing where it was).")]
        [SerializeField, Range(0f, 1f)] float rootInertia = ThresherSwimMath.DefaultRootInertia;
        [Tooltip("0..1: how much of the hull's own turn the tip resists. Higher swings the tip wider in a turn.")]
        [SerializeField, Range(0f, 1f)] float tipInertia = ThresherSwimMath.DefaultTipInertia;

        [Header("Stick")]
        [Tooltip("Degrees the hull banks into a full-stick turn.")]
        [SerializeField] float bankDegrees = 22f;
        [Tooltip("How fast the stick terms (bank, pectorals) ease to the stick, per second. Keeps a touch " +
                 "lifting off the screen from snapping the bank flat.")]
        [SerializeField, Min(0.1f)] float stickEaseRate = 10f;
        [Tooltip("Degrees the pectorals pitch with the stick (angle of attack).")]
        [SerializeField] float pectoralPitchDegrees = 24f;
        [Tooltip("Degrees the pectorals twist against each other in a full-stick turn (the inside fin's leading edge up).")]
        [SerializeField] float pectoralTwistDegrees = 26f;

        [Header("The ball")]
        [Tooltip("Planted, the hull banks into the orbit by up to this many degrees, with the spin.")]
        [SerializeField] float orbitBankDegrees = 34f;
        [Tooltip("Degrees the pectorals flare down while the ball is stopping (they read as brakes).")]
        [SerializeField] float brakeFlareDegrees = 40f;
        [Tooltip("Degrees per second a smash or a launch kicks the scythe's tip with, toward the ball, like a " +
                 "whip; its spring brings it back.")]
        [SerializeField, Min(0f)] float crackKick = ThresherSwimMath.DefaultCrackKick;
        [Tooltip("How fast the ball terms (bank, flare) ease to their target, per second.")]
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
        Quaternion[] _chainRot;
        Vector3[] _chainPos;
        ThresherSwimMath.Joint[] _joints;   // the tail's springs, root first
        float[] _yawTargets, _pitchTargets;
        Vector3 _tailRestDir;               // peduncle to whip tip at rest, in the hull's space
        float _tailLength;
        Quaternion _lastRootRot;            // the vessel's own rotation last frame: the hull's turn drags the tail
        bool _hasRootRot;
        readonly Transform[] _pectorals = new Transform[2];        // left, right
        readonly Quaternion[] _pectoralRestRot = new Quaternion[2];
        float _orbitBank, _flare, _pitchEased, _yawEased, _ballSide = 1f;
        int _lastCombo;
        bool _wasHot;

        // ---- procedural element morphs (the StoatAnimation plumbing, verbatim in shape) ----
        readonly float[] _morphWeights = new float[4];      // ThresherHullForm.MorphElements order
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        ThresherSwimMath.Settings Tail => new()
        {
            MaxYawDegrees = maxBendYawDegrees,
            MaxPitchDegrees = maxBendPitchDegrees,
            RootFrequency = rootFrequency,
            TipFrequency = tipFrequency,
            Damping = tailDamping,
            RootInertia = rootInertia,
            TipInertia = tipInertia,
            CrackKick = crackKick,
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
            _chainRot = new Quaternion[n];
            _chainPos = new Vector3[n];
            _joints = new ThresherSwimMath.Joint[n];
            _yawTargets = new float[n];
            _pitchTargets = new float[n];
            for (int j = 0; j < n; j++)
            {
                var t = _chain[j];
                _chainRest[j] = t ? _hullTransform.InverseTransformPoint(t.position) : (j > 0 ? _chainRest[j - 1] : Vector3.zero);
                _chainRestRot[j] = t ? toHull * t.rotation : Quaternion.identity;
            }
            // The tail's rest line runs from the peduncle to where the chain leaves: the model's `hitch` (a child
            // of the last joint), else the last segment carried on once more.
            var hitch = FindDeep(_hullTransform, "hitch");
            Vector3 tip = hitch ? _hullTransform.InverseTransformPoint(hitch.position)
                : n > 1 ? 2f * _chainRest[n - 1] - _chainRest[n - 2] : _chainRest[0] + Vector3.back;
            _tailRestDir = tip - _chainRest[0];
            _tailLength = _tailRestDir.magnitude;
            _hasRootRot = false;
            for (int i = 0; i < _pectorals.Length; i++)
                _pectoralRestRot[i] = _pectorals[i] ? toHull * _pectorals[i].rotation : Quaternion.identity;
        }

        static Transform FindDeep(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName) return child;
                var found = FindDeep(child, childName);
                if (found) return found;
            }
            return null;
        }

        // The parts are resolved by name in ResolveParts.
        protected override void AssignTransforms() { }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle) => Drive(pitch, yaw);

        /// <summary>The base calls Idle() INSTEAD of puppetry when the sticks centre; a hands-off Thresher's tail
        /// still hangs along its cord, so the pose is driven from here too.</summary>
        protected override void Idle() => Drive(0f, 0f);

        void Drive(float pitch, float yaw)
        {
            if (!_hullTransform || _chainRest == null) return;
            float dt = Time.deltaTime;
            float ks = 1f - Mathf.Exp(-stickEaseRate * dt);
            _pitchEased = Mathf.Lerp(_pitchEased, Mathf.Clamp(pitch, -1f, 1f), ks);
            _yawEased = Mathf.Lerp(_yawEased, Mathf.Clamp(yaw, -1f, 1f), ks);
            pitch = _pitchEased;
            yaw = _yawEased;

            ReadBall(out float bankTarget, out float flareTarget, out float kick);
            float k = 1f - Mathf.Exp(-ballEaseRate * dt);
            _orbitBank = Mathf.Lerp(_orbitBank, bankTarget, k);
            _flare = Mathf.Lerp(_flare, flareTarget, k);

            // The body first (every part below is posed in its frame): banks into the turn and into an orbit.
            _hullTransform.localRotation = _hullRestRot * Quaternion.Euler(0f, 0f, -yaw * bankDegrees - _orbitBank);
            Quaternion hullRot = _hullTransform.rotation;

            // The tail: the last links of the cord. Aim at a point down the chain, spring each joint toward its
            // share of that bend, and let the hull's own turn drag it (ThresherSwimMath).
            var tail = Tail;
            float bendYaw = 0f, bendPitch = 0f;
            if (_executor && _executor.TryGetCordPoint(cordSampleDistance, out var cordPoint))
                ThresherSwimMath.CordBend(tail, _tailRestDir, _hullTransform.InverseTransformPoint(cordPoint) - _chainRest[0],
                    _tailLength, out bendYaw, out bendPitch);
            ThresherSwimMath.JointTargets(bendYaw, bendPitch, _yawTargets, _pitchTargets);
            HullTurn(out float turnYaw, out float turnPitch);
            ThresherSwimMath.Step(tail, _joints, _yawTargets, _pitchTargets, turnYaw, turnPitch, kick, dt);
            for (int j = 0; j < _joints.Length; j++) _chainRot[j] = ThresherSwimMath.JointRotation(_joints[j]);
            ThresherSwimMath.PoseChain(_chainRest, _chainRot, _chainPos);
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

        /// <summary>How far the VESSEL itself (not the banking hull) turned since last frame, degrees: nose right
        /// and nose down positive. Zero on the first frame after a reset.</summary>
        void HullTurn(out float yaw, out float pitch)
        {
            yaw = pitch = 0f;
            var root = _hullTransform.parent ? _hullTransform.parent : _hullTransform;
            Quaternion now = root.rotation;
            if (_hasRootRot)
            {
                Vector3 f = Quaternion.Inverse(_lastRootRot) * now * Vector3.forward;
                yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                pitch = Mathf.Atan2(-f.y, Mathf.Sqrt(f.x * f.x + f.z * f.z)) * Mathf.Rad2Deg;
            }
            _lastRootRot = now;
            _hasRootRot = true;
        }

        /// <summary>What the wrecking ball asks of the pose this frame: the bank into a planted orbit (growing with
        /// the spin), the brake flare while it stops, and a crack (a kick of the tip toward the ball) on a new
        /// smash or a launch. The tail's own pull toward the ball is the cord's (<see cref="Drive"/>).</summary>
        void ReadBall(out float bank, out float flare, out float kick)
        {
            bank = 0f; flare = 0f; kick = 0f;
            if (!_executor) return;

            // Which side of the hull the ball is on (+1 = right), in the hull's own frame. Held until the ball is
            // clearly on the other side: towed straight behind, it sits on the centreline and a bare sign flickered.
            Vector3 local = _hullTransform.parent
                ? _hullTransform.parent.InverseTransformPoint(_executor.BallPosition)
                : _executor.BallPosition - _hullTransform.position;
            if (Mathf.Abs(local.x) > 0.2f * local.magnitude) _ballSide = Mathf.Sign(local.x);

            switch (_executor.Mode)
            {
                case ThresherMode.Skidding:
                    flare = 1f;
                    break;
                case ThresherMode.Pivot:
                    bank = _ballSide * orbitBankDegrees * Mathf.Lerp(0.35f, 1f, _executor.Spin01);
                    break;
            }

            // A crack: a new smash in the combo, the ball turning red-hot, or a launch. A ball on the right is a
            // NEGATIVE yaw in the tail's (Unity's) convention.
            bool hot = _executor.IsHot;
            int combo = _executor.Combo;
            if ((combo > _lastCombo && combo > 0) || (hot && !_wasHot) || _executor.LaunchedThisStep)
                kick = -_ballSide * crackKick;
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
            _orbitBank = _flare = _pitchEased = _yawEased = 0f;
            if (_joints != null) System.Array.Clear(_joints, 0, _joints.Length);
            _hasRootRot = false;
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

        // A pooled or swapped-away vessel must not carry a mid-beat pose into its next life.
        void OnDisable() => ResetPose();
    }
}
