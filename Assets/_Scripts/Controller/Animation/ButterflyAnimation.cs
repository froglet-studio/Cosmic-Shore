using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Butterfly's four wings (built by <see cref="ButterflyHullBuilder"/> /
    /// <see cref="ButterflyHullForm"/>). Design record: <c>R_VesselActions/BUTTERFLY.md</c> §2.2.
    ///
    /// <para><b>The beat IS the vessel.</b> Everything else about this hull is slow and still, so
    /// the wingbeat carries the whole read, and it is authored at FLEET SCALE — the Rhino's 80°
    /// wings are the calibration and 14–26° is invisible at a chase camera (the vessel skill's
    /// rule 24, which this vessel is the most exposed to: it is flown from 120 units).</para>
    ///
    /// <para><b>It is driven by SPEED, not by the stick.</b> A butterfly beats harder to go
    /// faster and holds a glide when it is already moving, so the beat's amplitude FALLS and its
    /// glide pose rises with <c>VesselStatus.Speed</c>. Driving it off the stick would freeze the
    /// wings under a boost, a danger-prism slow or an AI pilot — all three of which the stick
    /// knows nothing about.</para>
    ///
    /// <para><b>The hindwings LAG the forewings</b> by a fixed phase. That one number is most of
    /// what separates "butterfly" from "bird": a real butterfly's two pairs are coupled but not
    /// in phase, and at zero lag the four wings read as two.</para>
    ///
    /// <para><b>Channel discipline.</b> This component writes <c>localRotation</c> only; the
    /// element morph writes VERTICES only (the hinges do not move under any element). No channel
    /// has two writers, which is the same split the Scarab established.</para>
    /// </summary>
    public class ButterflyAnimation : VesselAnimation
    {
        [Header("Wings (resolved from the procedural hull by name if left empty)")]
        [SerializeField] Transform ForeWingL;
        [SerializeField] Transform ForeWingR;
        [SerializeField] Transform HindWingL;
        [SerializeField] Transform HindWingR;

        [Header("Beat (degrees — see the class note on fleet scale)")]
        [Tooltip("Half-amplitude of the wingbeat at a standstill, in degrees about the hinge. " +
                 "Large on purpose: this vessel is flown from 120 units and the beat is the only " +
                 "motion it has.")]
        [SerializeField] float beatAmplitude = 52f;
        [Tooltip("Half-amplitude at cruiseSpeed and above — the GLIDE. A butterfly at speed holds " +
                 "its wings; a hull that beats identically at every speed reads as a loop.")]
        [SerializeField] float beatAmplitudeAtCruise = 16f;
        [Tooltip("Beats per second at a standstill.")]
        [SerializeField, Min(0.05f)] float beatHz = 1.15f;
        [Tooltip("Beats per second at cruiseSpeed and above. Higher than the resting rate, but " +
                 "not by much — this is a meditative hull, not a hummingbird.")]
        [SerializeField, Min(0.05f)] float beatHzAtCruise = 1.7f;
        [Tooltip("Speed at which the glide pose is fully reached (world u/s). Author it at the " +
                 "vessel's own cruise — it is the denominator of every speed term below.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 55f;
        [Tooltip("Degrees the wings sit ABOVE the hinge plane while gliding — a soaring V. " +
                 "Added to the beat, so at speed the wings ride high and barely move.")]
        [SerializeField] float glideLift = 26f;
        [Tooltip("Phase the HINDwings lag the forewings, in beats (0.18 ≈ 65°). At 0 the four " +
                 "wings read as two.")]
        [SerializeField, Range(0f, 0.5f)] float hindLagBeats = 0.18f;

        [Header("Turn")]
        [Tooltip("Degrees the OUTSIDE wing rises above the inside one at full stick. The bank " +
                 "the camera reads comes from the flight model; this is the wings answering it.")]
        [SerializeField] float turnAsymmetry = 34f;
        [Tooltip("Degrees both wings sweep back along the hull at full pitch-up.")]
        [SerializeField] float pitchSweep = 18f;

        [Header("Spread (the Mass ability)")]
        [Tooltip("Degrees the wings hold ABOVE the hinge plane while the Mass spread is held — " +
                 "the broad, flat, gliding pose that explains the wide wake.")]
        [SerializeField] float spreadHoldAngle = 30f;
        [Tooltip("What the beat's amplitude is MULTIPLIED by while spread. Well under 1: wings " +
                 "held wide to paint are wings that are barely beating, and a full-amplitude " +
                 "flap under a broad stroke reads as the two abilities fighting.")]
        [SerializeField, Range(0f, 1f)] float spreadBeatScale = 0.35f;
        [Tooltip("Seconds for the wings to reach (and leave) the spread pose. Matched to the " +
                 "prism controller's own ~1.5s width lerp so the wings and the wake open together.")]
        [SerializeField, Min(0.01f)] float spreadBlendSeconds = 1.2f;

        [Header("Fold (the Time ability)")]
        [Tooltip("Degrees the wings close TOGETHER over the back while the Fold is held — the " +
                 "resting-butterfly pose. This is the vessel's tell that it is about to leave, " +
                 "and it is the one pose a pilot sees on every other Butterfly in the match.")]
        [SerializeField] float foldClosedAngle = 88f;
        [Tooltip("Seconds for the wings to close into (and out of) the fold pose.")]
        [SerializeField, Min(0.01f)] float foldBlendSeconds = 0.35f;

        ButterflyHullBuilder _hullBuilder;

        float _beatPhase;          // beats, accumulated — never reset, so the beat never jumps
        float _foldBlend;          // 0 = flying, 1 = wings closed
        float _foldTarget;
        float _spreadBlend;        // 0 = normal beat, 1 = wings held wide
        float _spreadTarget;

        // ---- procedural element morphs -----------------------------------------------------
        // This hull is generated, not an FBX, so its element morphs are the baked geometry deltas
        // the builder carries rather than blend shapes. This component owns only TIME and FEEL:
        // the weights glide on the fleet's shared VesselElementalMorphConfigSO (same duration,
        // same ease, same [0,10] band as every blend-shape vessel), tweens drive the cached
        // weights, and LateUpdate is the single push — after base.LateUpdate, mirroring the
        // base's write-last-wins defense.
        readonly float[] _morphWeights = new float[4];      // ButterflyHullForm.MorphElements order
        readonly Tween[] _morphTweens = new Tween[4];
        VesselElementalMorphConfigSO _procMorphConfig;
        bool _morphDirty;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _hullBuilder = GetComponentInChildren<ButterflyHullBuilder>(true);
            base.Initialize(vesselStatus);
            InitializeProceduralMorphs(vesselStatus);
        }

        protected override void ResolveParts()
        {
            // The builder names its parts, so an un-authored prefab still resolves. An authored
            // reference always wins (the base's rule), which is what lets real art drop in later.
            ForeWingL = ResolvePart(ForeWingL, "WingForeL");
            ForeWingR = ResolvePart(ForeWingR, "WingForeR");
            HindWingL = ResolvePart(HindWingL, "WingHindL");
            HindWingR = ResolvePart(HindWingR, "WingHindR");

            // The procedural wings rest at identity, so this is a no-op today — but it is what
            // lets authored art with angled rest poses drop in without tearing flat.
            CaptureRestRotations(ForeWingL, ForeWingR, HindWingL, HindWingR);
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(ForeWingL);
            Transforms.Add(ForeWingR);
            Transforms.Add(HindWingL);
            Transforms.Add(HindWingR);
        }

        /// <summary>
        /// Hold the wings WIDE (the Mass ability's spread) or let them beat normally. Driven by
        /// <c>SpreadWingsActionExecutor</c> on every peer — the wake it widens is conserved mass
        /// that every machine lays, so the wings that explain it must match everywhere too.
        /// </summary>
        public void SetSpread(bool spread) => _spreadTarget = spread ? 1f : 0f;

        /// <summary>
        /// Close the wings over the back (1) or release them (0). Driven by
        /// <c>FoldActionExecutor</c> while the Fold is held — on EVERY peer, because the fold's
        /// press replicates and every other pilot should be able to read that this Butterfly is
        /// about to leave.
        /// </summary>
        public void SetFolded(bool folded) => _foldTarget = folded ? 1f : 0f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
            => DriveWings(pitch, yaw);

        /// <summary>The base routes to Idle() INSTEAD of puppetry when the sticks centre, so the
        /// beat has to be driven from here too or a hands-off Butterfly freezes mid-flap — which
        /// is exactly the state a meditative pilot spends most of their time in.</summary>
        protected override void Idle() => DriveWings(0f, 0f);

        void DriveWings(float pitch, float yaw)
        {
            if (!ForeWingL || !ForeWingR) return;

            float speed01 = cruiseSpeed > 0f && VesselStatus != null
                ? Mathf.Clamp01(VesselStatus.Speed / cruiseSpeed)
                : 0f;

            // Fold blend, framerate-independent and symmetric in both directions.
            float foldStep = Time.deltaTime / Mathf.Max(0.01f, foldBlendSeconds);
            _foldBlend = Mathf.MoveTowards(_foldBlend, _foldTarget, foldStep);

            float spreadStep = Time.deltaTime / Mathf.Max(0.01f, spreadBlendSeconds);
            _spreadBlend = Mathf.MoveTowards(_spreadBlend, _spreadTarget, spreadStep);

            float hz = Mathf.Lerp(beatHz, beatHzAtCruise, speed01);
            _beatPhase += hz * Time.deltaTime;

            float amplitude = Mathf.Lerp(beatAmplitude, beatAmplitudeAtCruise, speed01);
            float lift = glideLift * speed01;

            // The spread quiets the beat and holds the wings wide — the pose that explains the
            // broad wake. It composes with the speed glide rather than replacing it, so a fast
            // spread Butterfly is flatter still.
            amplitude *= Mathf.Lerp(1f, spreadBeatScale, _spreadBlend);
            lift += spreadHoldAngle * _spreadBlend;

            // The fold silences the beat as it closes — a vessel folding space is holding still,
            // and a wing that kept flapping into the closed pose would read as a stuck animation.
            amplitude *= 1f - _foldBlend;

            float foreBeat = Mathf.Sin(_beatPhase * Mathf.PI * 2f) * amplitude;
            float hindBeat = Mathf.Sin((_beatPhase - hindLagBeats) * Mathf.PI * 2f) * amplitude;

            // Turn: the OUTSIDE wing rises. yaw > 0 is a right turn, so the LEFT wing is outside.
            float asym = yaw * turnAsymmetry * (1f - _foldBlend);
            // Pitch sweeps both wings back along the hull.
            float sweep = pitch * pitchSweep * (1f - _foldBlend);

            float closed = foldClosedAngle * _foldBlend;

            ApplyWing(ForeWingL, -1, foreBeat + lift - asym + closed, sweep);
            ApplyWing(ForeWingR, +1, foreBeat + lift + asym + closed, sweep);
            ApplyWing(HindWingL, -1, hindBeat + lift * 0.6f - asym * 0.7f + closed, sweep);
            ApplyWing(HindWingR, +1, hindBeat + lift * 0.6f + asym * 0.7f + closed, sweep);
        }

        /// <summary>
        /// Pose one wing. A wing rooted on the flank beats by rotating about the hull's FORWARD
        /// axis (local Z), which is why the sign flips per side — a shared angle would beat the
        /// two wings in opposite directions and read as a propeller. Yaw (local Y) is the
        /// fore/aft sweep.
        ///
        /// <para><b>Writes <c>localRotation</c> DIRECTLY rather than through the base's
        /// <see cref="VesselAnimation.RotatePartFromRest"/>, and that is load-bearing.</b> That
        /// helper lerps at the base's <c>lerpAmount</c> (authored 2), which is a first-order lag
        /// with a ~0.5 s time constant. A 1.15 Hz beat pushed through it comes out at roughly
        /// <c>1/sqrt(1 + (2*pi*f*tau)^2)</c> ≈ 27% of its amplitude and about 75° late — so an
        /// authored 52° beat would render as ~14°, which is exactly the band the fleet already
        /// knows reads as "the ship feels dead" (the vessel skill's rule 24). The lag exists to
        /// smooth a STICK; the beat is already a smooth continuous function of time and has
        /// nothing to gain from being filtered. The two terms that do come from the stick (turn
        /// asymmetry, sweep) arrive pre-eased, and the fold is a MoveTowards, so every input to
        /// this pose is already continuous.</para>
        ///
        /// <para>Composed with the captured REST rotation so authored art with an angled bind
        /// pose is not flattened onto identity — the rig-swap trap the base class documents. On
        /// the procedural hull the rest pose is identity and this term is free.</para>
        /// </summary>
        void ApplyWing(Transform wing, int side, float beatDegrees, float sweepDegrees)
        {
            if (!wing) return;
            wing.localRotation =
                Quaternion.Euler(0f, side * sweepDegrees, -side * beatDegrees) * RestRotationOf(wing);
        }

        // ---- element morph plumbing --------------------------------------------------------

        void InitializeProceduralMorphs(IVesselStatus vesselStatus)
        {
            if (!_hullBuilder) return;
            var resources = vesselStatus?.ResourceSystem;
            if (resources == null) return;

            _procMorphConfig = VesselElementalMorphConfigSO.LoadDefault();

            resources.OnElementLevelChange -= HandleElementLevelForMorph;   // detach-first
            resources.OnElementLevelChange += HandleElementLevelForMorph;

            // Seed the spawn silhouette instantly — the event only covers CHANGES, and a vessel
            // can spawn (or swap in) mid-session with levels already earned.
            foreach (var element in ButterflyHullForm.MorphElements)
                GlideMorph(element, resources.GetLevel(element), instant: true);
        }

        void HandleElementLevelForMorph(Element element, int level)
            => GlideMorph(element, level, instant: false);

        void GlideMorph(Element element, int level, bool instant)
        {
            int index = System.Array.IndexOf(ButterflyHullForm.MorphElements, element);
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

        /// <summary>Single writer for the procedural morph push, AFTER the base's shape-key write
        /// (the same authoritative-last ordering the base documents). Idempotent when no weight
        /// moved this frame — the builder rewrites nothing.</summary>
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
            // A pooled or swapped-away vessel must not carry a half-closed fold into its next
            // life — the wings would spawn shut and the beat would look broken.
            _foldTarget = 0f;
            _foldBlend = 0f;
            _spreadTarget = 0f;
            _spreadBlend = 0f;
        }
    }
}
