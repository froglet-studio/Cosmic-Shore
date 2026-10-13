using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Thresher's shark hull (built by <see cref="ThresherHullBuilder"/> /
    /// <see cref="ThresherHullForm"/>). Design record: <c>R_VesselActions/THRESHER.md</c> §5b.
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
    /// sway) and each part's local pose. The builder writes VERTICES only (the element morph). No channel has
    /// two writers.</para>
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

        ThresherHullBuilder _hull;
        ThresherExecutor _executor;
        Transform _hullTransform;
        Quaternion _hullRestRot;
        Vector3[] _chainRest;              // tail base pivot, then every scythe segment's
        Quaternion[] _chainLocal, _chainRot;
        Vector3[] _chainPos;
        float[] _jointYaws;
        Vector3[] _pectoralRest;
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
            if (!_hull)
                CSDebug.LogWarning($"[ThresherAnimation] '{name}' has no ThresherHullBuilder in its children; the swim has nothing to move.");
            // The vessel's OWN executor (it rides this prefab's ShipActions) — never another hull's.
            _executor = GetComponentInChildren<ThresherExecutor>(true);
            base.Initialize(vesselStatus);
            CaptureRest();
            InitializeProceduralMorphs(vesselStatus);
        }

        void CaptureRest()
        {
            if (!_hull || !_hull.TailBase) return;
            _hullTransform = _hull.transform;
            _hullRestRot = _hullTransform.localRotation;

            int n = 1 + _hull.Lobes.Count;
            _chainRest = new Vector3[n];
            _chainLocal = new Quaternion[n];
            _chainRot = new Quaternion[n];
            _chainPos = new Vector3[n];
            _jointYaws = new float[n];
            _chainRest[0] = _hull.TailBase.localPosition;
            for (int k = 0; k < _hull.Lobes.Count; k++)
                _chainRest[k + 1] = _hull.Lobes[k] ? _hull.Lobes[k].localPosition : _chainRest[k];

            _pectoralRest = new Vector3[_hull.Pectorals.Count];
            for (int i = 0; i < _pectoralRest.Length; i++)
                if (_hull.Pectorals[i]) _pectoralRest[i] = _hull.Pectorals[i].localPosition;
        }

        // The parts are the builder's, found through it rather than by name, so nothing to resolve.
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

            // The tail: one chain, beat + steer + curl + crack, posed so no joint ever opens.
            ThresherSwimMath.JointYaws(swim, _phase, Mathf.Max(speed01, Mathf.Abs(yaw)), yaw, _whip, _crack, _crackSide, _jointYaws);
            for (int j = 0; j < _jointYaws.Length; j++) _chainLocal[j] = Quaternion.Euler(0f, _jointYaws[j], 0f);
            ThresherSwimMath.PoseChain(_chainRest, _chainLocal, _chainPos, _chainRot);
            _hull.TailBase.localPosition = _chainPos[0];
            _hull.TailBase.localRotation = _chainRot[0];
            for (int i = 0; i < _hull.Lobes.Count; i++)
            {
                var lobe = _hull.Lobes[i];
                if (!lobe) continue;
                lobe.localPosition = _chainPos[i + 1];
                lobe.localRotation = _chainRot[i + 1];
            }

            // The body: banks into the turn and into an orbit, and counter-sways against the beat.
            float sway = -bodySwayDegrees * Mathf.Sin(_phase) * Mathf.Lerp(0.45f, 1f, speed01);
            _hullTransform.localRotation = _hullRestRot * Quaternion.Euler(0f, sway, -yaw * bankDegrees - _orbitBank);

            // Pectorals: angle of attack with the stick, twisted against each other in a turn (ailerons:
            // the inside fin's leading edge up), and both tips flared DOWN while the ball stops.
            for (int i = 0; i < _hull.Pectorals.Count; i++)
            {
                var fin = _hull.Pectorals[i];
                if (!fin) continue;
                float side = i == 0 ? -1f : 1f;
                float attack = -pitch * pectoralPitchDegrees + side * yaw * pectoralTwistDegrees;
                // A positive Z roll swings +X up toward +Y: it DROPS the left fin's tip (it hangs toward -X)
                // and lifts the right's, so a flare is the opposite sign on each side.
                float flare = -side * _flare * brakeFlareDegrees;
                fin.localRotation = Quaternion.Euler(attack, 0f, flare);
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
            if (_hull.TailBase) { _hull.TailBase.localPosition = _chainRest[0]; _hull.TailBase.localRotation = Quaternion.identity; }
            for (int i = 0; i < _hull.Lobes.Count && i + 1 < _chainRest.Length; i++)
                if (_hull.Lobes[i]) { _hull.Lobes[i].localPosition = _chainRest[i + 1]; _hull.Lobes[i].localRotation = Quaternion.identity; }
            for (int i = 0; i < _hull.Pectorals.Count; i++)
                if (_hull.Pectorals[i]) _hull.Pectorals[i].localRotation = Quaternion.identity;
            _whip = _orbitBank = _flare = _crack = 0f;
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
