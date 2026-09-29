using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Termite queen's procedural hull (<see cref="TermiteHullBuilder"/> /
    /// <see cref="TermiteHullForm"/>). Design record: <c>R_VesselActions/TERMITE.md</c> §2.3.
    ///
    /// <para><b>Two bodies, one read.</b> A termite queen at rest folds her four wings flat over her
    /// back; in flight she spreads them and they blur. So the WINGS are driven by SPEED — folded
    /// when she hovers at her command point, spread and buzzing when she moves — never by the
    /// stick, because a commander hull steered by pointing has no stick deflection to read, and an
    /// AI or a boost moves the ship without the stick knowing (the vessel skill's rule 24).</para>
    ///
    /// <para>The ABDOMEN breathes, lags behind the queen's turns (it is a heavy trailing mass on a
    /// waist joint), and PULSES when a card is played — a laying queen contracting. The pulse is
    /// the one piece of feedback every other pilot in the match can read off her body.</para>
    ///
    /// <para>Amplitudes are FLEET-SCALE (the Rhino's 80° wings are the calibration). 14–26° is
    /// invisible at the commander camera, which sits further back than most of the fleet's.</para>
    ///
    /// <para><b>Channel discipline.</b> This component writes <c>localRotation</c> on the wings and
    /// <c>localRotation</c> + <c>localScale</c> on the abdomen; the element morph writes VERTICES
    /// only (no pivot moves under any element — the form asserts it). No channel has two writers.
    /// Wing poses are written DIRECTLY rather than through the base's lagged
    /// <see cref="VesselAnimation.RotatePartFromRest"/>, whose ~0.5 s first-order lag would erase
    /// a buzz — the same reasoning <see cref="ButterflyAnimation"/> records.</para>
    /// </summary>
    public class TermiteAnimation : VesselAnimation
    {
        [Header("Parts (resolved from the procedural hull by name if left empty)")]
        [SerializeField] Transform ForeWingL;
        [SerializeField] Transform ForeWingR;
        [SerializeField] Transform HindWingL;
        [SerializeField] Transform HindWingR;
        [SerializeField] Transform Abdomen;

        [Header("Speed")]
        [Tooltip("Speed at which the wings are fully spread and buzzing (world u/s). Author it at " +
                 "the queen's own command cruise.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 70f;

        [Header("Wings — folded at rest")]
        [Tooltip("Degrees each wing sweeps BACK over the abdomen when the queen hovers. Near 90 " +
                 "lays the four wings flat along her back, the way a resting termite holds them.")]
        [SerializeField] float foldBackAngle = 82f;
        [Tooltip("Degrees the wings sweep back at full cruise — spread, but still raked.")]
        [SerializeField] float spreadBackAngle = 18f;
        [Tooltip("Degrees the folded wings LIFT about their hinge so they clear the abdomen. The " +
                 "hinges sit on the thorax, which is much lower than a queen's swollen abdomen, so " +
                 "a wing folded flat at hinge height would pass straight through her. Measured: the " +
                 "abdomen's crown is ~2.1u above the hinge about 7u back, i.e. ~17 degrees.")]
        [SerializeField] float foldLift = 18f;

        [Header("Wings — buzz")]
        [Tooltip("Half-amplitude of the wing beat at cruise, degrees about the hinge.")]
        [SerializeField] float buzzAmplitude = 38f;
        [Tooltip("Beats per second at cruise. High — a termite flutters, it does not glide.")]
        [SerializeField, Min(0.1f)] float buzzHz = 6.5f;
        [Tooltip("Phase the HINDwings lag the forewings, in beats.")]
        [SerializeField, Range(0f, 0.5f)] float hindLagBeats = 0.25f;

        [Header("Abdomen")]
        [Tooltip("Fractional swell of the abdomen's girth on each breath.")]
        [SerializeField, Range(0f, 0.2f)] float breathDepth = 0.05f;
        [SerializeField, Min(0.05f)] float breathHz = 0.55f;
        [Tooltip("Degrees the abdomen swings AGAINST a turn at full yaw — a heavy trailing mass on a " +
                 "waist joint.")]
        [SerializeField] float turnSwing = 22f;
        [Tooltip("Degrees the abdomen droops when hovering (and lifts level at cruise).")]
        [SerializeField] float hoverDroop = 9f;
        [Tooltip("Peak extra girth on a card-played pulse (0.2 = +20%).")]
        [SerializeField, Range(0f, 0.6f)] float cardPulseDepth = 0.22f;
        [Tooltip("Seconds for a card pulse to settle.")]
        [SerializeField, Min(0.05f)] float cardPulseSeconds = 0.6f;

        TermiteHullBuilder _hullBuilder;
        float _beatPhase;
        float _breathPhase;
        float _swing;           // smoothed abdomen yaw swing
        float _cardPulse;       // 0..1, decays
        Vector3 _lastForward;

        readonly float[] _morphWeights = new float[4];
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hullBuilder = GetComponentInChildren<TermiteHullBuilder>(true);
            base.Initialize(vesselStatus);
            InitializeProceduralMorphs(vesselStatus);
            _lastForward = transform.forward;
        }

        protected override void ResolveParts()
        {
            ForeWingL = ResolvePart(ForeWingL, "WingForeL");
            ForeWingR = ResolvePart(ForeWingR, "WingForeR");
            HindWingL = ResolvePart(HindWingL, "WingHindL");
            HindWingR = ResolvePart(HindWingR, "WingHindR");
            Abdomen = ResolvePart(Abdomen, "Abdomen");
            CaptureRestRotations(ForeWingL, ForeWingR, HindWingL, HindWingR, Abdomen);
            ReportUnresolvedParts();
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(ForeWingL);
            Transforms.Add(ForeWingR);
            Transforms.Add(HindWingL);
            Transforms.Add(HindWingR);
            Transforms.Add(Abdomen);
        }

        /// <summary>
        /// The queen contracts — played on EVERY peer when a card resolves, because the press
        /// replicates and every pilot in the match should be able to read that she just laid a
        /// brood, founded a mound or spent her drones.
        /// </summary>
        public void PlayCardPulse() => _cardPulse = 1f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
            => Drive();

        /// <summary>The base routes to Idle() instead of puppetry when the sticks centre — which
        /// for a commander hull is almost always — so the body is driven from here too.</summary>
        protected override void Idle() => Drive();

        void Drive()
        {
            float dt = Time.deltaTime;
            float speed01 = VesselStatus != null ? Mathf.Clamp01(VesselStatus.Speed / cruiseSpeed) : 0f;

            // Yaw RATE from the queen's own heading change, not the stick: a commander hull turns
            // because it is steering toward a point, and an AI turns without the stick at all.
            Vector3 fwd = transform.forward;
            float yawRate = dt > 1e-5f
                ? Vector3.SignedAngle(_lastForward, fwd, transform.up) / dt
                : 0f;
            _lastForward = fwd;
            float yaw01 = Mathf.Clamp(yawRate / 120f, -1f, 1f);

            // ---- wings ----
            float hz = Mathf.Lerp(0f, buzzHz, speed01);
            _beatPhase += hz * dt;
            float amplitude = buzzAmplitude * speed01;
            float back = Mathf.Lerp(foldBackAngle, spreadBackAngle, speed01);
            float lift = foldLift * (1f - speed01);

            float fore = Mathf.Sin(_beatPhase * Mathf.PI * 2f) * amplitude;
            float hind = Mathf.Sin((_beatPhase - hindLagBeats) * Mathf.PI * 2f) * amplitude;

            ApplyWing(ForeWingL, -1, back, fore + lift);
            ApplyWing(ForeWingR, +1, back, fore + lift);
            // Hindwings fold a hair further and lift a hair less (they root lower and further
            // back, so the abdomen's crown is closer), so the folded stack reads as four wings.
            ApplyWing(HindWingL, -1, back + 4f * (1f - speed01), hind + lift * 0.9f);
            ApplyWing(HindWingR, +1, back + 4f * (1f - speed01), hind + lift * 0.9f);

            // ---- abdomen ----
            if (Abdomen)
            {
                _breathPhase += breathHz * dt;
                _cardPulse = Mathf.MoveTowards(_cardPulse, 0f, dt / Mathf.Max(0.05f, cardPulseSeconds));
                float pulse = cardPulseDepth * Mathf.Sin(_cardPulse * Mathf.PI * 0.5f);
                float girth = 1f + breathDepth * Mathf.Sin(_breathPhase * Mathf.PI * 2f) + pulse;
                Abdomen.localScale = new Vector3(girth, girth, 1f - pulse * 0.35f);

                float targetSwing = -yaw01 * turnSwing;
                _swing = Mathf.Lerp(_swing, targetSwing, 1f - Mathf.Exp(-4f * dt));
                float droop = hoverDroop * (1f - speed01);
                Abdomen.localRotation = Quaternion.Euler(droop, _swing, 0f) * RestRotationOf(Abdomen);
            }
        }

        /// <summary>
        /// Pose one wing: a FLAP about the hull's forward axis raises (positive) or lowers the tip —
        /// the beat plus the fold lift — then a YAW about the hinge sweeps it back (side-signed,
        /// so both sides fold toward the tail). Flap first, so the lift is carried round with the
        /// sweep and a folded wing's tip stays raised over the abdomen. Composed with the
        /// captured rest rotation so authored art with an angled bind pose is not flattened.
        /// </summary>
        void ApplyWing(Transform wing, int side, float backDegrees, float beatDegrees)
        {
            if (!wing) return;
            wing.localRotation =
                Quaternion.Euler(0f, side * backDegrees, 0f) *
                // +side, not -side: Quaternion.Euler(0,0,t) carries +x toward +y (the standard
                // rotation matrix; Unity's handedness changes how it LOOKS, not the maths), so for
                // the right wing (+x) a positive flap raises the tip and for the left wing (-x) the
                // negated angle raises it too.
                Quaternion.Euler(0f, 0f, side * beatDegrees) *
                RestRotationOf(wing);
        }

        // ---- element morph plumbing (the Butterfly's, verbatim in shape) -----------------

        void InitializeProceduralMorphs(IVesselStatus vesselStatus)
        {
            if (!_hullBuilder) return;
            var resources = vesselStatus?.ResourceSystem;
            if (resources == null) return;

            _procMorphConfig = VesselElementalMorphConfigSO.LoadDefault();

            resources.OnElementLevelChange -= HandleElementLevelForMorph;   // detach-first
            resources.OnElementLevelChange += HandleElementLevelForMorph;

            foreach (var element in TermiteHullForm.MorphElements)
                GlideMorph(element, resources.GetLevel(element), instant: true);
        }

        void HandleElementLevelForMorph(Element element, int level)
            => GlideMorph(element, level, instant: false);

        void GlideMorph(Element element, int level, bool instant)
        {
            int index = System.Array.IndexOf(TermiteHullForm.MorphElements, element);
            if (index < 0) return;

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

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!_morphDirty || !_hullBuilder) return;
            _morphDirty = false;
            _hullBuilder.ApplyElementMorphWeights(_morphWeights[0], _morphWeights[1],
                                                  _morphWeights[2], _morphWeights[3]);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            for (int i = 0; i < _morphTweens.Length; i++) _morphTweens[i]?.Kill();
            if (VesselStatus?.ResourceSystem != null)
                VesselStatus.ResourceSystem.OnElementLevelChange -= HandleElementLevelForMorph;
        }

        void OnDisable()
        {
            // A swapped-away vessel must not carry a pulse or a swing into its next life.
            _cardPulse = 0f;
            _swing = 0f;
            if (Abdomen) Abdomen.localScale = Vector3.one;
        }
    }
}
