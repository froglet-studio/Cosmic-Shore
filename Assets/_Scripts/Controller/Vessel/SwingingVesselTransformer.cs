using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The GIBBON's flight model: zero-gravity BRACHIATION on two arms.
    ///
    /// There is no throttle on this vessel (locked). Every unit of speed comes from the WINCH:
    /// a line cast at a prism, pulled taut by the vessel's own motion, and REELED. Reeling a
    /// taut line is positive work — angular momentum v·h is conserved, so the tangential speed
    /// rises as the line shortens — integrated in CLOSED FORM per frame (a logistic in ln h, so
    /// a hitch frame is exact), tempered by <see cref="pumpGain"/> (never above the conservation
    /// law) and SOFT-capped by an efficiency that fades to zero at <see cref="pumpSpeedCap"/>.
    /// Nothing is clamped; drag brings anything above the cap back. Mass is conserved: the arms
    /// never create or destroy a prism except through the sweep (an ACTIVE force).
    ///
    /// One stroke is one BEAT — CAST → SNAP (the slack line goes taut ONCE, at the exact
    /// ray–sphere instant: the outward radial velocity is redirected onto the tangent, keeping
    /// more of it the more glancing the grab) → REEL (speed rises as the arc tightens, the radius
    /// floored by a maximum swing rate so the release window is a real window, not a frame) →
    /// FLING (release: the velocity vector at that instant IS the launch). While TAUT the hull
    /// moves on the sphere ANALYTICALLY (rotate the radial and the velocity together by
    /// θ = |v|·dt/h) — an exact circle at any frame rate, zero numerical dissipation, one snap
    /// per grab. Two taut lines are the intersection CIRCLE, solved in closed form; a double
    /// catch on the far side of the chord is a FLING, not a catch (a rope twangs, it never stops
    /// you dead). Moving anchors are followed in their own frame, so swinging on a shark works
    /// and a release carries the shark's velocity.
    ///
    /// Two arms, two triggers (LT = left, RT = right), each stick aims its own arm inside a cone
    /// around the COURSE, resting at a yaw DERIVED from speed so the anchor is abeam when the
    /// spool LANDS. The latched arm's stick is the winch: up reels, down pays out (the brake),
    /// neutral holds — on every device, which is also what makes the trigger-depth reel honest
    /// on a keyboard. Alternating arms with swings that CARRIED pays a TEMPO bonus on the pump.
    ///
    /// Integration contract (see VesselTransformer): full <see cref="MoveShip"/> override that
    /// keeps <c>throttleMultiplier</c> (danger-prism slow: scales the ARC, never the pump) and
    /// <c>velocityShift</c> (knockback: slides the hull along the sphere) live in every state,
    /// publishes <c>speed</c> = |v| so the fleet API stays honest, and honours SetPose /
    /// SetCourseVelocity / SetInitialSpeed / a position teleport through
    /// <see cref="SyncExternalWrites"/>. The hull's whole rotation is owned here
    /// (<see cref="RotateShip"/>): nose on velocity, banked into the arc.
    ///
    /// Replicas (remote-owned vessels on this machine) run none of this — the hull's motion is
    /// replicated; the lines and reticles are not (open item, GIBBON.md). Under autopilot the
    /// transformer drives its OWN arms from the AI's live objective
    /// (<see cref="AIPilot.CurrentTargetPosition"/>): AIPilot only ever writes the stick sums.
    /// Design record: _Scripts/Controller/Vessel/R_VesselActions/GIBBON.md.
    /// </summary>
    public class SwingingVesselTransformer : VesselTransformer
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Knobs
        // ─────────────────────────────────────────────────────────────────────

        [Header("Arms - Cast")]
        [Tooltip("World units/second the line spools out at. Fast on purpose: 170u at 520 lands in a third of a second, so a cast reads as a decision, not a wait.")]
        [SerializeField] float castSpeed = 520f;
        [Tooltip("Longest line the arm can hold. Also the cast range: a cast that finds no prism inside this range is a WHIFF (visible retract at cast speed, tempo reset, no anchor).")]
        [SerializeField] float maxLineLength = 170f;
        [Tooltip("Floor on the shortest line. The LIVE floor is max(this, |v| / maxSwingRate, anchor size + hull radius + 2): a fast vessel cannot reel into a hamster wheel.")]
        [SerializeField] float minLineLength = 18f;
        [Tooltip("Highest orbital rate (radians/second) a taut line may reach. Bounds the line floor (h >= |v| / rate), so at 250 u/s the shortest line is 62u and the release window is 0.13-0.22 s instead of a frame. Also the nose's ability to follow. Author PitchScaler/YawScaler to this in deg/s so MinTurnRadius agrees.")]
        [SerializeField] float maxSwingRate = 4f;
        [Tooltip("The hull's own radius, for the geometric line floor (the orbit must clear the anchor prism).")]
        [SerializeField] float hullRadius = 4f;
        [Tooltip("Nearest a prism may be to count as a cast target - anything closer is already under the hull.")]
        [SerializeField] float minCastDistance = 8f;
        [Tooltip("Half-angle (degrees) around the aim ray inside which a prism is captured by a cast. Judged from the hand NOW: the resting yaw already encodes the arrival geometry (it is derived so the prism is abeam when the spool lands), so scoring from the predicted arrival point would count it twice and whiff every blind tap.")]
        [SerializeField] float captureConeDeg = 10f;
        [Tooltip("A locked prism is held until it leaves this many capture cones (hysteresis), or a rival is captureSwitchFactor times closer to the axis.")]
        [SerializeField] float captureExitFactor = 1.5f;
        [SerializeField] float captureSwitchFactor = 2f;
        [Tooltip("Half-angle (degrees) the stick can swing the arm's aim away from its rest, measured from the COURSE (velocity), not the nose.")]
        [SerializeField] float aimHalfConeDeg = 55f;
        [Tooltip("FLOOR on the resting yaw of each arm's aim (left arm this many degrees left of course, right arm right). The live rest yaw is acos(|v| / castSpeed) - 61 deg at 250 u/s, 73 at 150, 90 at rest - so the prism is ABEAM when the spool LANDS rather than when it is cast: a blind tap then snaps in 0.2-0.4 s with almost no whiplash instead of coasting past an anchor ahead.")]
        [SerializeField] float restAimYawFloorDeg = 25f;
        [Tooltip("Flip if stick-up aims the reticle down on your device.")]
        [SerializeField] bool invertVerticalAim = false;

        [Header("Winch")]
        [Tooltip("World units/second the line shortens at full reel.")]
        [SerializeField] float reelRate = 70f;
        [Tooltip("World units/second the line lengthens at full pay-out (latched stick DOWN) - the brake: paying out a taut line is the pump run backwards.")]
        [SerializeField] float payOutRate = 70f;
        [Tooltip("Trigger depth below which a held trigger only HOLDS the line (a pure turn, no reel). The reel is smoothstep(deadband, 1, depth) above it. The latched stick's Y reels too (up) so a digital trigger still has a winch.")]
        [SerializeField, Range(0f, 0.9f)] float reelDeadband = 0.35f;
        [Tooltip("Fraction of the angular-momentum law the winch applies (the exponent p in v ~ h^-p). 1 = strict conservation; tempo raises it toward 1 and never past it.")]
        [SerializeField, Range(0f, 1f)] float pumpGain = 0.35f;
        [Tooltip("Speed at which the pump's efficiency reaches zero (a logistic in ln h with this asymptote). A SOFT cap: nothing is clamped, the winch simply stops paying above it and drag brings the vessel back.")]
        [SerializeField] float pumpSpeedCap = 320f;
        [Tooltip("Speed kept at the SNAP for a perfectly glancing grab (velocity already tangential).")]
        [SerializeField, Range(0f, 1f)] float snapRedirectGlancing = 0.95f;
        [Tooltip("Speed kept at the SNAP for a head-on grab (flying straight away from the anchor). Blended by cos^2 of the angle between the velocity and its tangent: bad geometry costs speed instead of being rewarded with the biggest kick.")]
        [SerializeField, Range(0f, 1f)] float snapRedirectHeadOn = 0.35f;
        [Tooltip("If the tangential part of the velocity at the snap is under this fraction of the whole, the line BREAKS instead of stopping the hull dead (a rope cannot do that): released, speed kept, tempo reset.")]
        [SerializeField, Range(0f, 0.5f)] float lineBreakTangentFraction = 0.1f;
        [Tooltip("Acceleration toward the anchor while the line is SLACK and the winch reels - the winch taking up slack pulls you in (a zip). Scaled by the same efficiency as the pump so it can never out-run the cap. 0 = off.")]
        [SerializeField] float zipAccel = 110f;
        [Tooltip("The zip stops accelerating once the approach speed toward the anchor reaches this. Sized to the cap so the take-up is alive at race speeds.")]
        [SerializeField] float zipMaxApproachSpeed = 320f;
        [Tooltip("Extra separation kept between two held lines and the distance between their anchors (feasibility): the reel stalls before the two spheres stop intersecting.")]
        [SerializeField] float dualFeasibilityMargin = 0.5f;

        [Header("Flight")]
        [Tooltip("Quadratic drag, 1/units: v *= 1 / (1 + k * v * dt) - the exact solution, bit-identical at any frame rate. Small on purpose: a fling is meant to CARRY. At 0.00035 a 200 u/s coast loses ~7% per second.")]
        [SerializeField] float dragK = 0.00035f;
        [Tooltip("Degrees/second the sticks steer the velocity while NO line is taut - only from the component the two sticks AGREE on (a single stick is aim, not steering). Constant-rate, so the authority is the same at every speed.")]
        [SerializeField] float airControlDegPerSec = 75f;
        [Tooltip("Degrees/second a latched arm's stick X rolls the swing plane about the line - swing over or under the anchor. Negative flips the direction.")]
        [SerializeField] float swingSteerDegPerSec = 90f;

        [Header("Tempo")]
        [Tooltip("Seconds after a release inside which the OTHER arm latching counts as the next beat.")]
        [SerializeField] float tempoWindow = 0.9f;
        [Tooltip("A grab only counts toward the rhythm if it SWEPT at least this many degrees before release - rhythm is swings that carried, which is brachiation; a flutter-tap is not.")]
        [SerializeField] float tempoMinSweepDeg = 30f;
        [Tooltip("Highest tempo the rhythm can reach.")]
        [SerializeField] int tempoMax = 5;
        [Tooltip("Extra pump exponent per tempo level: p = pumpGain * (1 + tempo * bonus), capped at 1 (the conservation law).")]
        [SerializeField] float tempoPumpBonus = 0.12f;

        [Header("Slingshot")]
        [Tooltip("Letting go of the SECOND of two held lines within this many seconds of the first, while past the chord between the anchors, is the slingshot - the catapult between two boughs.")]
        [SerializeField] float slingshotWindow = 0.2f;
        [Tooltip("Speed multiplier the slingshot pays. The only speed source outside the winch, and it is a timing reward on speed the winch already built.")]
        [SerializeField] float slingshotBonus = 1.15f;

        [Header("Hull")]
        [Tooltip("Degrees/second the nose turns onto the velocity. Above maxSwingRate (229 deg/s at 4 rad/s) so the hull - and the camera that follows it - can always keep up with the orbit.")]
        [SerializeField] float noseTrackDegPerSec = 540f;
        [Tooltip("How far the hull banks INTO a taut arc (0 = level, 1 = the up vector points straight at the anchor).")]
        [SerializeField, Range(0f, 1f)] float bankIntoSwing = 0.6f;
        [Tooltip("Per-second rate the hull settles back toward world-up when no line is taut.")]
        [SerializeField] float uprightSettleRate = 1.5f;
        [Tooltip("Hand mount in hull space (x mirrored for the left arm): where the line leaves the hull.")]
        [SerializeField] Vector3 handMount = new(0.8f, -0.2f, 0.3f);

        [Header("Line Visual")]
        [Tooltip("Base world-radius of the line capsule. Thick enough to read as a 3D rod with shading, not a hairline.")]
        [SerializeField] float tetherRadius = 0.14f;
        [Tooltip("Optional lit material override. Left empty, a shaded HDR-cyan URP/Lit material is built at runtime so the line catches scene light (depth) and blooms.")]
        [SerializeField] Material tetherMaterial;
        [Tooltip("Seconds a RELEASED line takes to retract to the hand. A whiff retracts at castSpeed instead (one full stroke out and back). Continuity of existence: a visible line never pops out.")]
        [SerializeField] float tetherRetractDuration = 0.15f;
        [Tooltip("Line thickens up to this multiplier as speed rises, and again with tempo - the rhythm is felt on the thing you are timing.")]
        [SerializeField] float speedThicknessBoost = 1.5f;
        [Tooltip("Speed at which the thickness boost (and the release shake) saturate. Align to the tuned cruise band.")]
        [SerializeField] float speedThicknessRef = 200f;
        [Tooltip("Thickness multiplier of a SLACK line (the winch has not taken it up yet). Below 1 so taut reads as taut.")]
        [SerializeField, Range(0.2f, 1f)] float slackThickness = 0.55f;

        [Header("Reticle + Hands")]
        [Tooltip("World radius of the aim reticle at zero distance.")]
        [SerializeField] float reticleRadius = 1.2f;
        [Tooltip("Extra radius per unit of distance so the reticle reads at range (1.2 + 0.012*170 = 3.2u at max range).")]
        [SerializeField] float reticleRadiusPerDistance = 0.012f;
        [Tooltip("Scale multiplier when the reticle is LOCKED on a prism.")]
        [SerializeField] float reticleLockScale = 1.6f;
        [Tooltip("Per-second rate the reticle blooms in / withers out as the arm frees / latches.")]
        [SerializeField] float reticleFadeRate = 10f;
        [Tooltip("While an arm is latched its reticle becomes the FLING reticle: where the hull will be this many seconds after letting go. Thread it through the next hoop.")]
        [SerializeField] float flingLookAheadSeconds = 0.6f;
        [Tooltip("Visual thickness of the hand capsule that runs from the hull to the reticle while the arm is free.")]
        [SerializeField] float armRadius = 0.07f;

        [Header("Lightsaber Sweep")]
        [Tooltip("A TAUT line destroys every prism it sweeps through (anchors and super-shielded mass excepted). An active force: conserved mass leaves as debris.")]
        [SerializeField] bool sweepDestroysPrisms = true;
        [Tooltip("Blade kerf - a prism whose centre is within this distance of the line gets sliced.")]
        [SerializeField] float sweepBladeRadius = 2.5f;
        [Tooltip("Max world-distance the vessel moves between sweep sub-steps. Lower = denser coverage at high speed.")]
        [SerializeField] float sweepStepDistance = 4f;
        [Tooltip("How fast the kill-pulse on the line visual decays.")]
        [SerializeField] float sweepPulseDecay = 4f;

        [Header("Juice")]
        [Tooltip("Camera shake when a line SNAPS taut, scaled by the centripetal onset (v^2/h) over snapShakeAccelRef - how hard the swing genuinely is. Local human only. 0 = off.")]
        [SerializeField] float snapShakeIntensity = 0.6f;
        [SerializeField] float snapShakeAccelRef = 600f;
        [SerializeField] float snapShakeDuration = 0.18f;
        [Tooltip("Camera shake on release, scaled by speed/speedThicknessRef. Local human only. 0 = off.")]
        [SerializeField] float releaseShakeIntensity = 0.8f;
        [SerializeField] float releaseShakeDuration = 0.25f;
        [Tooltip("Releases below this fraction of speedThicknessRef don't shake.")]
        [SerializeField] float releaseShakeSpeedFloor = 0.05f;
        [Tooltip("Camera shake tick when the sweep slices at least one prism this frame. Local human only. 0 = off.")]
        [SerializeField] float sweepKillShakeIntensity = 0.2f;
        [SerializeField] float sweepKillShakeDuration = 0.1f;

        [Header("Audio")]
        [Tooltip("FMOD event when an arm is cast. Leave empty for silence.")]
        [SerializeField] EventReference castEvent;
        [Tooltip("FMOD event when a line snaps taut. Leave empty for silence.")]
        [SerializeField] EventReference snapEvent;
        [Tooltip("FMOD event on release (the fling). Leave empty for silence.")]
        [SerializeField] EventReference releaseEvent;
        [Tooltip("FMOD event when a cast finds nothing, or a line breaks. Leave empty for silence.")]
        [SerializeField] EventReference whiffEvent;
        [Tooltip("FMOD event when the tempo climbs a level. Leave empty for silence.")]
        [SerializeField] EventReference tempoUpEvent;
        [Tooltip("FMOD event on a slingshot. Leave empty for silence.")]
        [SerializeField] EventReference slingshotEvent;

        [Header("Autopilot")]
        [Tooltip("Reel an AI holds (AIPilot never writes a trigger).")]
        [SerializeField, Range(0f, 1f)] float aiReelDepth = 1f;
        [Tooltip("The AI lets go once its velocity is within this many degrees of the objective.")]
        [SerializeField] float aiReleaseAngleDeg = 12f;
        [Tooltip("The AI lets go after sweeping this many degrees around one anchor, whatever the objective is doing.")]
        [SerializeField] float aiMaxSwingDeg = 150f;
        [Tooltip("The AI lets go after holding one line this long.")]
        [SerializeField] float aiMaxSwingSeconds = 2.5f;
        [Tooltip("Seconds after a release before the AI casts again.")]
        [SerializeField] float aiRegrabDelay = 0.15f;
        [Tooltip("Degrees off course the AI aims its cast toward the objective when a turn is called for.")]
        [SerializeField] float aiCastYawDeg = 40f;

        [Header("Anchor Safety")]
        [Tooltip("An anchor whose transform moves faster than this (u/s) is a pooled prism re-issued somewhere else, not a creature: the line withers. Keep above 3x the fastest fauna cruise.")]
        [SerializeField] float anchorBreakSpeed = 600f;

        // ─────────────────────────────────────────────────────────────────────
        //  Types + state
        // ─────────────────────────────────────────────────────────────────────

        public enum ArmPhase { Free = 0, Casting = 1, Latched = 2 }
        public enum SwingState { FreeFlight = 0, Swing = 1, Slingshot = 2 }

        sealed class ArmState
        {
            public GibbonArm side;
            public ArmPhase phase;
            public bool triggerHeld;

            // latched
            public Prism anchorPrism;
            public Transform anchor;
            public float anchorIdentity;     // PrismProperties.TimeCreated at latch: a pool reuse changes it
            public Vector3 lastAnchorPos;
            public Vector3 anchorVelocity;
            public float lineLength;
            public float pumpFrom, pumpTo;    // the taut line's length before/after this frame's winch
            public float minLineGeom;
            public bool taut;
            public float latchTime;
            public float sweptAngle;
            public Vector3 lastRadial;
            public float reelApproach;        // u/s the winch closed the line this frame (for the carried velocity)

            // casting
            public Vector3 castDir;
            public Prism castTargetPrism;
            public float castExtension;

            // aim (free arm)
            public Vector3 aimDir;
            public Prism aimTarget;
            public Vector3 aimPoint;
            public float aimTargetAngle01;
            public Vector3 aiAimDir;

            // visuals
            public Transform line;
            public MeshRenderer lineRenderer;
            public bool retracting;
            public float retractTimer, retractDuration;
            public Vector3 retractEnd;
            public float sweepPulse;
            public Transform reticle;
            public MeshRenderer reticleRenderer;
            public float reticleAlpha;
            public Transform hand;
            public MeshRenderer handRenderer;
        }

        readonly ArmState left = new() { side = GibbonArm.Left };
        readonly ArmState right = new() { side = GibbonArm.Right };

        // The vessel's velocity. Free: its momentum. Taut: tangential orbital velocity + the
        // anchor's own velocity + the winch's radial approach - i.e. always what Δpos/dt is.
        Vector3 velocity;
        bool velocitySeeded;
        float lastPublishedSpeed;
        Vector3 lastPublishedCourse;
        Vector3 lastPublishedPos;

        int tempo;
        float lastReleaseTime = float.NegativeInfinity;
        GibbonArm lastReleasedArm;
        bool hasReleasedBefore;
        bool lastReleaseWasDualPartner;
        Vector3 lastReleasedAnchorPos;
        float aiNextCastTime;
        bool aiAlternate;
        bool wasAutopilot;
        Quaternion lastWrittenRotation = Quaternion.identity;
        bool rotationWritten;

        /// <summary>The vessel never strands: the fleet's MinimumSpeed floor (DefaultMinimumSpeed when unset). From rest, one full reel can raise it to at most floor * (maxLine/minLine)^pumpGain - the stated bound.</summary>
        float FloorSpeed => MinimumSpeed > 0f ? MinimumSpeed : Mathf.Max(DefaultMinimumSpeed, 0f);

        /// <summary>Lines that went taut this session (telemetry / harness).</summary>
        public int SnapCount { get; private set; }

        Material lineMaterial;
        Material reticleDimMaterial;
        Material reticleLockMaterial;
        bool visualsBuilt;

        static readonly List<Prism> queryScratch = new(256);
        static readonly string[] StateNames = { "FreeFlight", "Swing", "Slingshot" };

        // ─────────────────────────────────────────────────────────────────────
        //  Public API (HUD / actions / telemetry)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>True when at least one arm holds a prism.</summary>
        public bool IsSwinging => left.phase == ArmPhase.Latched || right.phase == ArmPhase.Latched;

        /// <summary>Vessel-level state for HUD/telemetry.</summary>
        public SwingState State =>
            (left.phase == ArmPhase.Latched && right.phase == ArmPhase.Latched) ? SwingState.Slingshot
            : IsSwinging ? SwingState.Swing
            : SwingState.FreeFlight;

        /// <summary>Zero-alloc state name for the debug telemetry readout.</summary>
        public string StateName => StateNames[(int)State];

        /// <summary>Current speed (u/s) - the momentum the vessel carries between grabs.</summary>
        public float CurrentSpeed => speed;

        /// <summary>The brachiation rhythm level, 0..tempoMax.</summary>
        public int Tempo => tempo;

        /// <summary>Highest tempo reachable (for HUD pips).</summary>
        public int TempoMax => tempoMax;

        /// <summary>Length of the shortest held line, or 0 when free.</summary>
        public float ActiveLineLength
        {
            get
            {
                float l = float.PositiveInfinity;
                if (left.phase == ArmPhase.Latched) l = Mathf.Min(l, left.lineLength);
                if (right.phase == ArmPhase.Latched) l = Mathf.Min(l, right.lineLength);
                return float.IsPositiveInfinity(l) ? 0f : l;
            }
        }

        /// <summary>Phase of one arm, for HUD.</summary>
        public ArmPhase PhaseOf(GibbonArm arm) => Arm(arm).phase;

        /// <summary>True when that arm's reticle currently rests on a capturable prism.</summary>
        public bool HasAimTarget(GibbonArm arm) => Arm(arm).phase == ArmPhase.Free && Arm(arm).aimTarget != null;

        /// <summary>
        /// Cast an arm at its reticle. Called by <see cref="GibbonArmActionSO.StartAction"/> on the
        /// trigger's press edge (replicated through the action handler like every ability), and by
        /// the autopilot policy. Holding is what winches; the action is only the edge.
        /// </summary>
        public void CastArm(GibbonArm arm)
        {
            if (IsReplica) return;
            var a = Arm(arm);
            a.triggerHeld = true;
            if (a.phase != ArmPhase.Free) return;
            BeginCast(a);
        }

        /// <summary>Let go with one arm (the fling). Trigger release edge, or the autopilot.</summary>
        public void ReleaseArm(GibbonArm arm)
        {
            if (IsReplica) return;
            var a = Arm(arm);
            a.triggerHeld = false;
            if (a.phase == ArmPhase.Free) return;
            ReleaseInternal(a, ReleaseKind.Fling);
        }

        /// <summary>Drop both lines with no feedback (turn reset, vessel swap, teleport).</summary>
        public void ReleaseAll()
        {
            ReleaseInternal(left, ReleaseKind.Silent);
            ReleaseInternal(right, ReleaseKind.Silent);
            left.triggerHeld = false;
            right.triggerHeld = false;
        }

        ArmState Arm(GibbonArm arm) => arm == GibbonArm.Left ? left : right;
        ArmState Other(ArmState a) => a == left ? right : left;

        enum ReleaseKind { Fling = 0, Silent = 1, Break = 2, Wither = 3 }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        bool IsReplica => VesselStatus?.Player != null && VesselStatus.Player.IsNetworkClient;

        bool IsLocalHumanVessel => VesselStatus != null && VesselStatus.IsLocalUser && !VesselStatus.IsInitializedAsAI;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            BuildVisuals();
            velocitySeeded = false;
        }

        public override void ResetTransformer()
        {
            ReleaseAll();
            tempo = 0;
            hasReleasedBefore = false;
            lastReleaseWasDualPartner = false;
            lastReleaseTime = float.NegativeInfinity;
            velocity = Vector3.zero;
            velocitySeeded = false;
            base.ResetTransformer();
        }

        void OnValidate()
        {
            // A NaN in v poisons Speed -> speed tunnel, HUD, replication. Keep every divisor sane.
            castSpeed = Mathf.Max(castSpeed, 1f);
            maxLineLength = Mathf.Max(maxLineLength, 2f);
            minLineLength = Mathf.Clamp(minLineLength, 1f, maxLineLength);
            maxSwingRate = Mathf.Max(maxSwingRate, 0.1f);
            pumpSpeedCap = Mathf.Max(pumpSpeedCap, 1f);
            dragK = Mathf.Max(dragK, 0f);
            reelRate = Mathf.Max(reelRate, 0f);
            payOutRate = Mathf.Max(payOutRate, 0f);
            captureConeDeg = Mathf.Clamp(captureConeDeg, 0.5f, 89f);
            tempoMax = Mathf.Max(tempoMax, 0);
            snapShakeAccelRef = Mathf.Max(snapShakeAccelRef, 1f);
            speedThicknessRef = Mathf.Max(speedThicknessRef, 1f);
            anchorBreakSpeed = Mathf.Max(anchorBreakSpeed, 1f);
        }

        void OnDestroy()
        {
            DestroyArmVisuals(left);
            DestroyArmVisuals(right);
            if (lineMaterial && lineMaterial != tetherMaterial) Destroy(lineMaterial);
            if (reticleDimMaterial) Destroy(reticleDimMaterial);
            if (reticleLockMaterial) Destroy(reticleLockMaterial);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Update
        // ─────────────────────────────────────────────────────────────────────

        protected override void Update()
        {
            if (VesselStatus == null || VesselStatus.IsStationary || IsReplica)
                return;

            float dt = Time.deltaTime;
            EnsureVelocitySeeded();

            bool autopilot = VesselStatus.AutoPilotEnabled;
            if (autopilot && !wasAutopilot)
            {
                // A trigger held when autopilot engaged never delivers its release (the action
                // handler drops human input under autopilot): start the arm policy clean.
                ReleaseAll();
            }
            wasAutopilot = autopilot;
            if (autopilot) TickAutopilotAim();

            UpdateAim(left, autopilot);
            UpdateAim(right, autopilot);

            if (autopilot) TickAutopilotArms();

            TickCasting(left, dt);
            TickCasting(right, dt);
            ValidateAnchor(left, dt);
            ValidateAnchor(right, dt);
            TickTempoDecay();

            Vector3 frameStartPos = transform.position;

            base.Update(); // RotateShip (ours) -> restricted branch / modifiers -> MoveShip (ours)

            SweepLine(left, frameStartPos);
            SweepLine(right, frameStartPos);
            UpdateVisuals(dt);
        }

        void EnsureVelocitySeeded()
        {
            if (velocitySeeded) return;
            float s = Mathf.Max(speed, FloorSpeed);
            Vector3 dir = VesselStatus.Course.sqrMagnitude > 0.5f ? VesselStatus.Course.normalized : transform.forward;
            lastWrittenRotation = transform.rotation;
            rotationWritten = true;
            velocity = dir * s;
            speed = s;
            lastPublishedSpeed = s;
            lastPublishedCourse = dir;
            lastPublishedPos = transform.position;
            VesselStatus.Course = dir;
            velocitySeeded = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Aim + capture
        // ─────────────────────────────────────────────────────────────────────

        Vector3 CourseDir => velocity.sqrMagnitude > 1e-6f ? velocity.normalized : transform.forward;

        /// <summary>
        /// The frame the arms aim in: forward = COURSE, up = WORLD up (the hull's up only when the
        /// course is near vertical). The hull banks into every arc and settles over ~0.7 s, and a
        /// frame that rolled with it sent the OTHER arm's resting reticle up-and-left after a
        /// right-hand swing - straight past the next bough of a canopy laid in the world's plane.
        /// The reticle is drawn in the world, so the pilot reads where it is whatever the camera
        /// roll; the AI and the canopy geometry read the world.
        /// </summary>
        void CourseFrame(out Vector3 fwd, out Vector3 rightAxis, out Vector3 upAxis)
        {
            fwd = CourseDir;
            rightAxis = Vector3.Cross(Vector3.up, fwd);
            if (rightAxis.sqrMagnitude < 0.09f) rightAxis = Vector3.Cross(transform.up, fwd);
            if (rightAxis.sqrMagnitude < 1e-6f) rightAxis = Vector3.Cross(Vector3.right, fwd);
            rightAxis.Normalize();
            upAxis = Vector3.Cross(fwd, rightAxis);
        }

        /// <summary>The resting yaw of an arm's aim: the anchor must be abeam when the spool LANDS.</summary>
        float RestAimYawDeg
        {
            get
            {
                float ratio = Mathf.Clamp01(velocity.magnitude / castSpeed);
                return Mathf.Max(restAimYawFloorDeg, Mathf.Acos(ratio) * Mathf.Rad2Deg);
            }
        }

        void UpdateAim(ArmState a, bool autopilot)
        {
            if (a.phase != ArmPhase.Free)
            {
                a.aimTarget = null;
                return;
            }

            Vector3 dir;
            if (autopilot)
            {
                dir = a.aiAimDir.sqrMagnitude > 0.5f ? a.aiAimDir : CourseDir;
            }
            else
            {
                Vector2 stick = InputStatus == null ? Vector2.zero
                    : (a.side == GibbonArm.Left ? InputStatus.EasedLeftJoystickPosition : InputStatus.EasedRightJoystickPosition);
                float sy = invertVerticalAim ? -stick.y : stick.y;
                float rest = RestAimYawDeg;
                float yaw = (a.side == GibbonArm.Left ? -rest : rest) + stick.x * aimHalfConeDeg;
                float pitch = sy * aimHalfConeDeg;
                CourseFrame(out var fwd, out var rightAxis, out var upAxis);
                dir = Quaternion.AngleAxis(yaw, upAxis) * Quaternion.AngleAxis(-pitch, rightAxis) * fwd;
            }

            a.aimDir = dir;
            a.aimTarget = FindCaptureTarget(a, dir, out a.aimPoint, out a.aimTargetAngle01);
        }

        /// <summary>
        /// The one spatial query a free arm makes: every prism inside the cast range, filtered to
        /// the capture cone around the aim ray (from the hand now - the resting yaw carries the
        /// arrival geometry), scored on-axis first and nearer second, with lock hysteresis so a
        /// lock does not flicker under the thumb. Through PrismSpatialIndex, never Physics (fresh
        /// prisms have no live collider for 0.6 s).
        /// </summary>
        Prism FindCaptureTarget(ArmState a, Vector3 dir, out Vector3 point, out float angle01)
        {
            Vector3 origin = transform.position;
            float range = maxLineLength;
            point = origin + dir * range;
            angle01 = 1f;

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return null;

            index.QuerySphere(origin + dir * (range * 0.5f), range * 0.5f + minCastDistance, queryScratch);
            float coneRad = Mathf.Max(captureConeDeg * Mathf.Deg2Rad, 1e-4f);
            float cosExit = Mathf.Cos(Mathf.Min(coneRad * captureExitFactor, Mathf.PI * 0.49f));
            float cosCone = Mathf.Cos(coneRad);
            float bestScore = float.PositiveInfinity;
            float bestAngle01 = 1f;
            Prism best = null;
            Prism held = a.aimTarget;
            bool heldValid = false;
            float heldAngle01 = 1f;
            string myName = VesselStatus.PlayerName;

            for (int i = 0; i < queryScratch.Count; i++)
            {
                var p = queryScratch[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(myName) && p.ownerID == myName) continue; // never your own arm
                if (p == left.anchorPrism || p == right.anchorPrism) continue;      // already held
                Vector3 pp = p.transform.position;
                Vector3 to = pp - origin;
                float dist = to.magnitude;
                if (dist < minCastDistance || dist > range) continue;

                float cos = Vector3.Dot(to, dir) / dist;
                bool isHeld = p == held;
                if (cos < (isHeld ? cosExit : cosCone)) continue;
                float ang01 = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) / coneRad;
                if (isHeld) { heldValid = true; heldAngle01 = ang01; }
                float score = ang01 + 0.3f * (dist / range);
                if (score < bestScore) { bestScore = score; best = p; bestAngle01 = ang01; }
            }

            // Hysteresis: keep the held lock unless the rival is decisively closer to the axis.
            if (heldValid && best != held && bestAngle01 * captureSwitchFactor > heldAngle01)
            {
                best = held;
                bestAngle01 = heldAngle01;
            }

            if (best != null)
            {
                point = best.transform.position;
                angle01 = bestAngle01;
            }
            return best;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cast / latch / release
        // ─────────────────────────────────────────────────────────────────────

        Vector3 HandPosition(ArmState a)
        {
            var m = handMount;
            if (a.side == GibbonArm.Left) m.x = -m.x;
            return transform.TransformPoint(m);
        }

        void BeginCast(ArmState a)
        {
            a.phase = ArmPhase.Casting;
            a.castTargetPrism = a.aimTarget;
            a.castDir = a.aimTarget != null
                ? (a.aimTarget.transform.position - HandPosition(a)).normalized
                : (a.aimDir.sqrMagnitude > 0.5f ? a.aimDir : CourseDir);
            a.castExtension = 0f;
            a.retracting = false;
            if (a.lineRenderer) a.lineRenderer.enabled = true;
            PlayOneShot(castEvent);
        }

        void TickCasting(ArmState a, float dt)
        {
            if (a.phase != ArmPhase.Casting) return;

            a.castExtension += castSpeed * dt;

            if (a.castTargetPrism != null)
            {
                if (!a.castTargetPrism.gameObject.activeInHierarchy)
                {
                    Whiff(a);
                    return;
                }
                Vector3 to = a.castTargetPrism.transform.position - HandPosition(a);
                a.castDir = to.sqrMagnitude > 1e-6f ? to.normalized : a.castDir;
                if (a.castExtension >= to.magnitude)
                    Latch(a, a.castTargetPrism);
                return;
            }

            if (a.castExtension >= maxLineLength)
                Whiff(a);
        }

        void Whiff(ArmState a)
        {
            float ext = Mathf.Min(a.castExtension, maxLineLength);
            BeginRetract(a, HandPosition(a) + a.castDir * ext, ext / castSpeed);
            a.phase = ArmPhase.Free;
            a.castTargetPrism = null;
            tempo = 0; // a broken rhythm is a broken rhythm
            PlayOneShot(whiffEvent);
        }

        void Latch(ArmState a, Prism prism)
        {
            a.phase = ArmPhase.Latched;
            a.anchorPrism = prism;
            a.anchor = prism.transform;
            a.anchorIdentity = prism.prismProperties != null ? prism.prismProperties.TimeCreated : 0f;
            a.lastAnchorPos = a.anchor.position;
            a.anchorVelocity = Vector3.zero;
            a.minLineGeom = 0.5f * a.anchor.lossyScale.magnitude + hullRadius + 2f;
            float d = Vector3.Distance(transform.position, a.lastAnchorPos);
            a.lineLength = Mathf.Clamp(d, LineFloor(a), maxLineLength);
            a.pumpFrom = a.pumpTo = a.lineLength;
            a.taut = false;
            a.reelApproach = 0f;
            a.latchTime = Time.time;
            a.sweptAngle = 0f;
            a.lastRadial = (transform.position - a.lastAnchorPos).normalized;
            a.castTargetPrism = null;
            a.retracting = false;
            if (a.lineRenderer) a.lineRenderer.enabled = true;
        }

        /// <summary>
        /// The live floor on a line: authored, geometric (clear the anchor), and the swing-rate
        /// bound on the ORBITAL speed (the tangential part of the velocity relative to the anchor -
        /// the winch's own radial approach must not count, or reeling raises the floor it is
        /// reeling toward).
        /// </summary>
        float LineFloor(ArmState a)
        {
            Vector3 vrel = velocity - a.anchorVelocity;
            if (a.anchor != null)
            {
                Vector3 rhat = (transform.position - a.anchor.position).normalized;
                if (rhat.sqrMagnitude > 0.5f) vrel = Vector3.ProjectOnPlane(vrel, rhat);
            }
            float vt = vrel.magnitude;
            return Mathf.Min(maxLineLength, Mathf.Max(minLineLength, Mathf.Max(a.minLineGeom, vt / maxSwingRate)));
        }

        void ReleaseInternal(ArmState a, ReleaseKind kind)
        {
            if (a.phase == ArmPhase.Free)
            {
                if (!a.retracting && a.lineRenderer) a.lineRenderer.enabled = false;
                return;
            }

            bool wasLatched = a.phase == ArmPhase.Latched;
            Vector3 farEnd = wasLatched && a.anchor != null
                ? a.anchor.position
                : HandPosition(a) + a.castDir * Mathf.Min(a.castExtension, maxLineLength);
            float swept = a.sweptAngle;
            float latchTime = a.latchTime;
            Vector3 anchorPos = a.lastAnchorPos;
            bool wasTaut = a.taut;
            bool partnerLatched = Other(a).phase == ArmPhase.Latched;

            a.phase = ArmPhase.Free;
            a.anchorPrism = null;
            a.anchor = null;
            a.castTargetPrism = null;
            a.taut = false;
            a.reelApproach = 0f;
            a.sweepPulse = 0f;

            if (kind == ReleaseKind.Silent)
            {
                a.retracting = false;
                if (a.lineRenderer) a.lineRenderer.enabled = false;
                return;
            }

            BeginRetract(a, farEnd, tetherRetractDuration);
            if (!wasLatched) return;

            float now = Time.time;
            if (kind == ReleaseKind.Fling)
            {
                // Tempo: the rhythm is swings that CARRIED, alternating, inside the window.
                bool chained = hasReleasedBefore && lastReleasedArm != a.side && (latchTime - lastReleaseTime) <= tempoWindow;
                if (swept >= tempoMinSweepDeg && chained)
                {
                    if (tempo < tempoMax) { tempo++; PlayOneShot(tempoUpEvent); }
                }
                else if (swept < tempoMinSweepDeg)
                {
                    tempo = 0;
                }

                // Slingshot: the second of two held lines let go inside the window, past the chord.
                if (lastReleaseWasDualPartner && (now - lastReleaseTime) <= slingshotWindow
                    && PastChord(lastReleasedAnchorPos, anchorPos))
                {
                    // Scaled by the winch's own efficiency: a timing reward on speed the winch
                    // built, never a source that compounds past the cap.
                    float vm = velocity.magnitude;
                    float eff = Mathf.Clamp01(1f - (vm * vm) / (pumpSpeedCap * pumpSpeedCap));
                    velocity *= 1f + (slingshotBonus - 1f) * eff;
                    PlayOneShot(slingshotEvent);
                }

                if (wasTaut) PlayReleaseShake();
                PlayOneShot(releaseEvent);
            }
            else
            {
                tempo = 0;
                PlayOneShot(whiffEvent);
            }

            lastReleaseTime = now;
            lastReleasedArm = a.side;
            lastReleasedAnchorPos = anchorPos;
            lastReleaseWasDualPartner = partnerLatched && kind == ReleaseKind.Fling;
            hasReleasedBefore = true;
        }

        bool PastChord(Vector3 anchorA, Vector3 anchorB)
        {
            Vector3 mid = 0.5f * (anchorA + anchorB);
            return Vector3.Dot(velocity, transform.position - mid) > 0f;
        }

        void BeginRetract(ArmState a, Vector3 farEnd, float duration)
        {
            bool visible = a.lineRenderer != null && a.lineRenderer.enabled;
            if (visible && duration > 0f)
            {
                a.retractEnd = farEnd;
                a.retractDuration = duration;
                a.retractTimer = duration;
                a.retracting = true;
            }
            else
            {
                a.retracting = false;
                if (a.lineRenderer) a.lineRenderer.enabled = false;
            }
        }

        void ValidateAnchor(ArmState a, float dt)
        {
            if (a.phase != ArmPhase.Latched) return;
            bool alive = a.anchor != null && a.anchorPrism != null && a.anchorPrism.gameObject.activeInHierarchy;
            if (alive && a.anchorPrism.prismProperties != null && a.anchorPrism.prismProperties.TimeCreated != a.anchorIdentity)
                alive = false; // the pool re-issued this prism as a different one
            if (alive)
            {
                Vector3 p = a.anchor.position;
                Vector3 delta = p - a.lastAnchorPos;
                float maxStep = anchorBreakSpeed * Mathf.Max(dt, 1e-4f);
                if (delta.sqrMagnitude > maxStep * maxStep) alive = false;
                else
                {
                    a.anchorVelocity = dt > 1e-5f ? delta / dt : Vector3.zero;
                    a.lastAnchorPos = p;
                }
            }
            if (!alive)
                ReleaseInternal(a, ReleaseKind.Wither);
        }

        void TickTempoDecay()
        {
            if (tempo == 0 || IsSwinging) return;
            if (Time.time - lastReleaseTime > tempoWindow) tempo = 0;
        }

        /// <summary>The winch input of a latched arm: reel-in (0..1) and pay-out (0..1).</summary>
        void ReadWinch(ArmState a, out float reelIn, out float payOut)
        {
            reelIn = 0f; payOut = 0f;
            if (VesselStatus.AutoPilotEnabled) { reelIn = aiReelDepth; return; }
            if (InputStatus == null) return;
            float analog = a.side == GibbonArm.Left ? InputStatus.LeftTriggerAnalog : InputStatus.RightTriggerAnalog;
            float fromTrigger = analog > reelDeadband ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reelDeadband, 1f, analog)) : 0f;
            Vector2 stick = a.side == GibbonArm.Left ? InputStatus.EasedLeftJoystickPosition : InputStatus.EasedRightJoystickPosition;
            float sy = invertVerticalAim ? -stick.y : stick.y;
            reelIn = Mathf.Max(fromTrigger, Mathf.Clamp01(sy));
            payOut = Mathf.Clamp01(-sy);
        }

        float SwingSteer(ArmState a)
        {
            if (VesselStatus.AutoPilotEnabled || InputStatus == null) return 0f;
            return a.side == GibbonArm.Left ? InputStatus.EasedLeftJoystickPosition.x : InputStatus.EasedRightJoystickPosition.x;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Movement
        // ─────────────────────────────────────────────────────────────────────

        protected override void MoveShip()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            SyncExternalWrites(dt);

            int tautCount = (left.taut ? 1 : 0) + (right.taut ? 1 : 0);
            if (tautCount == 0) ApplyAirControl(dt);

            Winch(dt);

            if (tautCount == 0)
            {
                ApplyDragTo(ref velocity, dt);
                Vector3 p0 = transform.position;
                transform.position = p0 + (throttleMultiplier * velocity + velocityShift) * dt;
                ResolveSnaps(p0, dt);
            }
            else if (tautCount == 1)
            {
                var a = left.taut ? left : right;
                SphereStep(a, dt);
                CheckOtherLine(a);
            }
            else
            {
                CircleStep(dt);
            }

            ApplySpeedFloor();
            Publish();
        }

        void SyncExternalWrites(float dt)
        {
            // SetInitialSpeed writes the protected `speed`; SetPose/SetCourseVelocity/the AI's drift
            // entry write VesselStatus.Course; SetPose and a vessel swap move the position. Adopt
            // all three the way the base vector model adopts its two.
            Vector3 pos = transform.position;
            float allowed = (velocity.magnitude * throttleMultiplier + velocityShift.magnitude + 2f * Mathf.Max(reelRate, payOutRate)) * dt + 1f;
            if ((pos - lastPublishedPos).sqrMagnitude > allowed * allowed)
            {
                // A teleport: whatever we were holding is now somewhere else entirely.
                ReleaseInternal(left, ReleaseKind.Wither);
                ReleaseInternal(right, ReleaseKind.Wither);
                velocity = transform.forward * velocity.magnitude;
            }
            if (rotationWritten && !SameRotation(accumulatedRotation, lastWrittenRotation))
            {
                // SetPose / the spin doors wrote accumulatedRotation outside RotateShip: the base
                // vector model would re-aim off it only when its own private seed flag is set, so
                // adopt it here - the new nose is the new course.
                velocity = (accumulatedRotation * Vector3.forward) * velocity.magnitude;
                lastWrittenRotation = accumulatedRotation;
            }
            if (!Mathf.Approximately(speed, lastPublishedSpeed))
                velocity = CourseDir * Mathf.Max(0f, speed);
            Vector3 course = VesselStatus.Course;
            if (course.sqrMagnitude > 0.5f && Vector3.Dot(course, lastPublishedCourse) < 0.99999f)
                velocity = course.normalized * velocity.magnitude;
        }

        /// <summary>
        /// Exact component equality, deliberately NOT Quaternion.Angle: the angle between two
        /// IDENTICAL unit quaternions is not zero in float32 (acos of a dot that rounds below 1),
        /// so a tolerance test fired every few frames and re-aimed the velocity along the hull -
        /// which turned the winch's radial approach into tangential speed: free energy from noise.
        /// RotateShip writes both fields from one value, so anything else that wrote
        /// accumulatedRotation differs in at least one component.
        /// </summary>
        static bool SameRotation(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

        void ApplyAirControl(float dt)
        {
            if (airControlDegPerSec <= 0f || InputStatus == null) return;
            Vector3 fwd = CourseDir;
            float x, y;
            if (VesselStatus.AutoPilotEnabled)
            {
                // AIPilot steers with the stick sums (yaw = XSum, pitch = YSum) exactly as it does
                // for every dual-stick hull.
                x = InputStatus.XSum;
                y = -InputStatus.YSum;
            }
            else
            {
                // Only the component both sticks AGREE on steers: a single deflected stick is
                // aiming its arm, and aiming must not move the cone the reticle lives in.
                Vector2 l = InputStatus.EasedLeftJoystickPosition, r = InputStatus.EasedRightJoystickPosition;
                x = Mathf.Sign(l.x) == Mathf.Sign(r.x) ? 0.5f * (l.x + r.x) : 0f;
                y = Mathf.Sign(l.y) == Mathf.Sign(r.y) ? 0.5f * (l.y + r.y) : 0f;
                if (invertVerticalAim) y = -y;
            }
            if (x * x + y * y < 1e-4f) return;
            CourseFrame(out _, out var rightAxis, out var upAxis);
            Vector3 want = Quaternion.AngleAxis(x * aimHalfConeDeg, upAxis) * Quaternion.AngleAxis(-y * aimHalfConeDeg, rightAxis) * fwd;
            float mag = velocity.magnitude;
            if (mag < 1e-4f) return;
            velocity = Vector3.RotateTowards(fwd, want, airControlDegPerSec * Mathf.Deg2Rad * dt, 0f) * mag;
        }

        /// <summary>Exact solution of dv/dt = -k v^2: 1/v grows linearly in k·dt. Bit-identical at any rate.</summary>
        void ApplyDragTo(ref Vector3 v, float dt)
        {
            if (dragK <= 0f) return;
            float mag = v.magnitude;
            v *= 1f / (1f + dragK * mag * dt);
        }

        float ApplyDragTo(float vMag, float dt) => dragK <= 0f ? vMag : vMag / (1f + dragK * vMag * dt);

        float PumpExponent => Mathf.Min(1f, pumpGain * (1f + tempo * tempoPumpBonus));

        /// <summary>
        /// Closed-form winch: the exact solution of dv/d(ln h) = -p·v·(1 - v²/c²) from h0 to h1.
        /// p = 1, c → ∞ is v·h = const. Reeling in (h1 &lt; h0) raises v, paying out lowers it, and
        /// v never crosses c from below. Exact whatever the frame length.
        /// </summary>
        float PumpClosedForm(float v0, float h0, float h1)
        {
            if (h0 <= 1e-4f || h1 <= 1e-4f || Mathf.Approximately(h0, h1) || v0 < 1e-3f) return v0;
            float c = pumpSpeedCap;
            if (v0 >= c) return v0;
            float p = PumpExponent;
            float b = ((c * c - v0 * v0) / (v0 * v0)) * Mathf.Pow(h1 / h0, 2f * p);
            return c / Mathf.Sqrt(1f + b);
        }

        void Winch(float dt)
        {
            WinchArm(left, dt);
            WinchArm(right, dt);
            if (left.phase == ArmPhase.Latched && right.phase == ArmPhase.Latched)
                EnforceDualFeasibility();
            ZipArm(left, dt);
            ZipArm(right, dt);
        }

        void WinchArm(ArmState a, float dt)
        {
            a.reelApproach = 0f;
            a.pumpFrom = a.pumpTo = a.lineLength;
            if (a.phase != ArmPhase.Latched || a.anchor == null) return;

            // Swing steer: the latched arm's stick X rolls the swing plane about the line.
            float steer = SwingSteer(a);
            if (Mathf.Abs(steer) > 1e-3f && swingSteerDegPerSec != 0f)
            {
                Vector3 rhat = (transform.position - a.anchor.position).normalized;
                if (rhat.sqrMagnitude > 0.5f)
                    velocity = Quaternion.AngleAxis(steer * swingSteerDegPerSec * dt, rhat) * velocity;
            }

            ReadWinch(a, out float reelIn, out float payOut);
            float target = a.lineLength - reelIn * reelRate * dt + payOut * payOutRate * dt;
            // The floor STOPS the reel; it never pays a line out. The pump raises the orbital
            // speed and with it the floor, so a line can legitimately sit below the floor of the
            // moment - hold it there rather than lengthening it (which is the pump run backwards,
            // and chattered between reel and pay-out at every frame rate).
            float floor = Mathf.Min(LineFloor(a), a.lineLength);
            target = Mathf.Clamp(target, floor, maxLineLength);
            a.lineLength = target;
            // The pump credits only a line that was TAUT when the winch turned; a slack line's
            // shortening is take-up, not work.
            a.pumpTo = a.lineLength;
            if (!a.taut) a.pumpFrom = a.lineLength;
            a.reelApproach = a.taut ? (a.pumpFrom - a.pumpTo) / dt : 0f;
        }

        /// <summary>Two held lines must keep intersecting: stall the reel at |A−B| + margin, and hold the circle's radius above the swing-rate floor.</summary>
        void EnforceDualFeasibility()
        {
            if (left.anchor == null || right.anchor == null) return;
            float d = Vector3.Distance(left.anchor.position, right.anchor.position);
            float minSum = d + dualFeasibilityMargin;
            float sum = left.lineLength + right.lineLength;
            if (sum < minSum)
            {
                float half = 0.5f * (minSum - sum);
                left.lineLength = Mathf.Min(maxLineLength, left.lineLength + half);
                right.lineLength = Mathf.Min(maxLineLength, right.lineLength + half);
                left.pumpTo = left.lineLength;
                right.pumpTo = right.lineLength;
            }
            if (left.taut && right.taut)
            {
                float hc = CircleRadius(left.lineLength, right.lineLength, d, out _);
                float floor = Mathf.Max(minLineLength, (velocity - 0.5f * (left.anchorVelocity + right.anchorVelocity)).magnitude / maxSwingRate);
                if (hc < floor)
                {
                    // Reject this frame's reel rather than solve for the exact stall point: the
                    // circle stops shrinking at the floor and the next frame tries again.
                    left.lineLength = left.pumpFrom; left.pumpTo = left.pumpFrom;
                    right.lineLength = right.pumpFrom; right.pumpTo = right.pumpFrom;
                }
            }
        }

        static float CircleRadius(float lA, float lB, float d, out float alongA)
        {
            alongA = d > 1e-4f ? (lA * lA - lB * lB + d * d) / (2f * d) : 0f;
            float h2 = lA * lA - alongA * alongA;
            return h2 > 0f ? Mathf.Sqrt(h2) : 0f;
        }

        void ZipArm(ArmState a, float dt)
        {
            if (a.phase != ArmPhase.Latched || a.taut || a.anchor == null || zipAccel <= 0f) return;
            ReadWinch(a, out float reelIn, out _);
            if (reelIn <= 0f) return;
            Vector3 r = transform.position - a.anchor.position;
            float d = r.magnitude;
            if (d < 1e-3f || d >= a.lineLength) return; // taut lines never zip; the sphere step owns them
            Vector3 rhat = r / d;
            Vector3 vrel = velocity - a.anchorVelocity;
            float approach = -Vector3.Dot(vrel, rhat);
            if (approach >= zipMaxApproachSpeed) return;
            float vm = vrel.magnitude;
            float eff = Mathf.Clamp01(1f - (vm * vm) / (pumpSpeedCap * pumpSpeedCap));
            float approach2 = Mathf.MoveTowards(approach, zipMaxApproachSpeed, zipAccel * reelIn * eff * dt);
            velocity -= rhat * (approach2 - approach);
        }

        /// <summary>
        /// After a free step, find the first latched-but-slack line that went taut along the chord
        /// (ray–sphere), snap there ONCE, and run the rest of the frame on the sphere. Both lines
        /// going taut past their chord in one frame is a FLING, not a catch.
        /// </summary>
        void ResolveSnaps(Vector3 p0, float dt)
        {
            Vector3 p1 = transform.position;
            float sL = SnapParam(left, p0, p1);
            float sR = SnapParam(right, p0, p1);
            bool hitL = sL >= 0f, hitR = sR >= 0f;
            if (!hitL && !hitR) return;

            if (hitL && hitR && PastChord(left.anchor.position, right.anchor.position))
            {
                // The double catch on the far side: the lines twang, the hull flies on.
                ReleaseInternal(left, ReleaseKind.Fling);
                ReleaseInternal(right, ReleaseKind.Fling);
                return;
            }

            ArmState first = (!hitR || (hitL && sL <= sR)) ? left : right;
            float s = first == left ? sL : sR;
            transform.position = Vector3.Lerp(p0, p1, s);
            if (!Snap(first)) return;
            float remaining = dt * (1f - s);
            if (remaining > 1e-5f) SphereStep(first, remaining);
            CheckOtherLine(first);
        }

        /// <summary>Chord parameter in [0,1] where a slack line goes taut, or -1 if it does not this frame.</summary>
        float SnapParam(ArmState a, Vector3 p0, Vector3 p1)
        {
            if (a.phase != ArmPhase.Latched || a.taut || a.anchor == null) return -1f;
            Vector3 anchor = a.anchor.position;
            float L = a.lineLength;
            Vector3 m = p0 - anchor;
            if (m.sqrMagnitude > L * L) return 0f; // already outside (came out of a restricted stance): snap now
            Vector3 d = p1 - p0;
            if ((p1 - anchor).sqrMagnitude <= L * L) return -1f;
            float aa = Vector3.Dot(d, d);
            if (aa < 1e-8f) return -1f;
            float bb = 2f * Vector3.Dot(m, d);
            float cc = Vector3.Dot(m, m) - L * L;
            float disc = bb * bb - 4f * aa * cc;
            if (disc < 0f) return 1f;
            float root = (-bb + Mathf.Sqrt(disc)) / (2f * aa); // the exit root: m starts inside
            return Mathf.Clamp01(root);
        }

        /// <summary>The snap: project onto the sphere, redirect the outward radial part onto the tangent (glancing keeps more). Returns false if the line broke.</summary>
        bool Snap(ArmState a)
        {
            Vector3 anchor = a.anchor.position;
            Vector3 r = transform.position - anchor;
            float d = r.magnitude;
            Vector3 rhat = d > 1e-4f ? r / d : RadialFallback();
            transform.position = anchor + rhat * a.lineLength;

            Vector3 vrel = velocity - a.anchorVelocity;
            float vr = Vector3.Dot(vrel, rhat);
            if (vr > 0f)
            {
                Vector3 vt = vrel - vr * rhat;
                float vtMag = vt.magnitude;
                float vrelMag = vrel.magnitude;
                if (vtMag < lineBreakTangentFraction * vrelMag)
                {
                    // A rope cannot stop you dead: the line breaks, the hull keeps its speed.
                    ReleaseInternal(a, ReleaseKind.Break);
                    return false;
                }
                float cos2 = (vtMag * vtMag) / Mathf.Max(vrelMag * vrelMag, 1e-6f);
                float retention = Mathf.Lerp(snapRedirectHeadOn, snapRedirectGlancing, cos2);
                float kept = retention * vrelMag;
                velocity = vt * (kept / vtMag) + a.anchorVelocity;
                PlaySnapShake(kept * kept / Mathf.Max(a.lineLength, 1e-3f));
            }
            a.taut = true;
            a.lastRadial = rhat;
            SnapCount++;
            PlayOneShot(snapEvent);
            return true;
        }

        /// <summary>After a single-line step, the OTHER latched line may have gone taut: snap it (→ circle next frame), or fling past the chord.</summary>
        void CheckOtherLine(ArmState a)
        {
            var o = Other(a);
            if (o.phase != ArmPhase.Latched || o.taut || o.anchor == null) return;
            Vector3 r = transform.position - o.anchor.position;
            if (r.sqrMagnitude <= o.lineLength * o.lineLength) return;
            if (a.taut && a.anchor != null && PastChord(a.anchor.position, o.anchor.position))
            {
                ReleaseInternal(a, ReleaseKind.Fling);
                ReleaseInternal(o, ReleaseKind.Fling);
                return;
            }
            Snap(o);
        }

        Vector3 RadialFallback()
        {
            Vector3 f = -transform.forward;
            return f.sqrMagnitude > 0.5f ? f : Vector3.up;
        }

        Vector3 TangentFallback(Vector3 rhat)
        {
            Vector3 t = Vector3.ProjectOnPlane(transform.forward, rhat);
            if (t.sqrMagnitude < 1e-6f) t = Vector3.ProjectOnPlane(transform.up, rhat);
            if (t.sqrMagnitude < 1e-6f) t = Vector3.ProjectOnPlane(Vector3.right, rhat);
            return t.normalized;
        }

        /// <summary>
        /// One taut line for a duration τ: exact motion on the sphere. Rotate the radial and the
        /// tangential velocity together by θ = throttleMultiplier·|v_t|·τ / h (throttleMultiplier
        /// scales the ARC, never the pump), winch the radius h0 → h1 with the closed-form pump,
        /// slide the knockback along the sphere, and carry the anchor's own velocity.
        /// </summary>
        void SphereStep(ArmState a, float tau)
        {
            Vector3 anchor = a.anchor.position;
            Vector3 va = a.anchorVelocity;
            // The radial lives in the anchor's frame: measure it against where the anchor WAS at
            // the start of this sub-step, rotate, then re-attach to where the anchor IS. Measured
            // against the new position the tow would be subtracted from the orbit (T8).
            Vector3 r = transform.position - (anchor - va * tau);
            float d = r.magnitude;
            Vector3 rhat = d > 1e-4f ? r / d : RadialFallback();

            Vector3 vrel = velocity - va;
            Vector3 vt = vrel - Vector3.Dot(vrel, rhat) * rhat;
            float vtMag = vt.magnitude;
            Vector3 that = vtMag > 1e-4f ? vt / vtMag : TangentFallback(rhat);

            vtMag = ApplyDragTo(vtMag, tau);
            vtMag = PumpClosedForm(vtMag, a.pumpFrom, a.pumpTo);
            float h = Mathf.Max(a.lineLength, 1e-3f);

            float theta = throttleMultiplier * vtMag * tau / h; // radians
            Vector3 n = Vector3.Cross(rhat, that);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(rhat, TangentFallback(rhat));
            Quaternion q = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, n.normalized);
            Vector3 rhat2 = q * rhat;
            Vector3 that2 = q * that;

            Vector3 pos = anchor + rhat2 * h + velocityShift * tau;
            Vector3 rr = pos - anchor;
            float dd = rr.magnitude;
            if (dd > h && dd > 1e-4f) { rhat2 = rr / dd; pos = anchor + rhat2 * h; }
            transform.position = pos;

            velocity = that2 * vtMag + va - rhat2 * a.reelApproach;
            a.sweptAngle += theta * Mathf.Rad2Deg;
            a.lastRadial = rhat2;
            a.taut = true;
            a.pumpFrom = a.pumpTo; // credited
        }

        /// <summary>
        /// Two taut lines: the hull lives on the intersection circle of the two spheres, solved in
        /// closed form (centre C on the anchor axis, radius h_c). Motion is exact rotation about the
        /// axis; the pump acts ONCE on h_c. The slingshot is the circle shrinking toward the axis.
        /// </summary>
        void CircleStep(float tau)
        {
            Vector3 A = left.anchor.position, B = right.anchor.position;
            Vector3 ab = B - A;
            float D = ab.magnitude;
            if (D < 1e-3f) { SphereStep(left.lineLength <= right.lineLength ? left : right, tau); return; }
            Vector3 u = ab / D;

            float hcOld = CircleRadius(left.pumpFrom, right.pumpFrom, D, out _);
            float hc = CircleRadius(left.lineLength, right.lineLength, D, out float along);
            hc = Mathf.Max(hc, 1e-3f);
            Vector3 C = A + u * along;

            // As in SphereStep: measure against the circle's centre at the START of the step (both
            // anchors towed back by their own velocity), rotate, re-attach to the current centre.
            Vector3 va = 0.5f * (left.anchorVelocity + right.anchorVelocity);
            Vector3 cStart = C - va * tau;
            Vector3 w = Vector3.ProjectOnPlane(transform.position - cStart, u);
            Vector3 what = w.sqrMagnitude > 1e-6f ? w.normalized : TangentFallback(u);

            Vector3 vrel = velocity - va;
            Vector3 that = Vector3.Cross(u, what);
            float vt = Vector3.Dot(vrel, that);
            float sign = vt >= 0f ? 1f : -1f;
            float vtMag = Mathf.Abs(vt);

            vtMag = ApplyDragTo(vtMag, tau);
            if (hcOld > 1e-3f) vtMag = PumpClosedForm(vtMag, hcOld, hc);

            float theta = throttleMultiplier * vtMag * tau / hc;
            Quaternion q = Quaternion.AngleAxis(sign * theta * Mathf.Rad2Deg, u);
            Vector3 what2 = q * what;
            Vector3 that2 = Vector3.Cross(u, what2);

            Vector3 pos = C + what2 * hc + velocityShift * tau;
            Vector3 w2 = Vector3.ProjectOnPlane(pos - C, u);
            if (w2.sqrMagnitude > 1e-6f) { what2 = w2.normalized; that2 = Vector3.Cross(u, what2); }
            transform.position = C + what2 * hc;

            float approach = hcOld > 1e-3f ? (hcOld - hc) / tau : 0f;
            velocity = that2 * (sign * vtMag) + va - what2 * approach;
            float deg = theta * Mathf.Rad2Deg;
            left.sweptAngle += deg; right.sweptAngle += deg;
            left.lastRadial = (transform.position - A).normalized;
            right.lastRadial = (transform.position - B).normalized;
            left.pumpFrom = left.pumpTo; right.pumpFrom = right.pumpTo;
        }

        void ApplySpeedFloor()
        {
            float floor = FloorSpeed;
            if (floor <= 0f) return;
            float mag = velocity.magnitude;
            if (mag >= floor) return;
            Vector3 dir = mag > 1e-4f ? velocity / mag
                : (lastPublishedCourse.sqrMagnitude > 0.5f ? lastPublishedCourse : transform.forward);
            velocity = dir * floor;
        }

        void Publish()
        {
            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z)
                || float.IsInfinity(velocity.x) || float.IsInfinity(velocity.y) || float.IsInfinity(velocity.z))
            {
                // Never let a bad frame reach Speed (speed tunnel, HUD, replication read it).
                velocity = transform.forward * Mathf.Max(FloorSpeed, 1f);
                ReleaseAll();
            }
            speed = velocity.magnitude;
            float effectiveSpeed = speed * throttleMultiplier;
            Vector3 course = speed > 1e-4f ? velocity / speed : transform.forward;
            VesselStatus.Speed = effectiveSpeed;
            VesselStatus.Course = course;
            lastPublishedSpeed = speed;
            lastPublishedCourse = course;
            lastPublishedPos = transform.position;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hull rotation: nose on velocity, banked into the arc
        // ─────────────────────────────────────────────────────────────────────

        protected override void RotateShip()
        {
            float dt = Time.deltaTime;
            Vector3 fwd = CourseDir;

            Vector3 upBase = Vector3.ProjectOnPlane(transform.up, fwd);
            if (upBase.sqrMagnitude < 1e-6f) upBase = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (upBase.sqrMagnitude < 1e-6f) upBase = Vector3.ProjectOnPlane(Vector3.right, fwd);
            upBase.Normalize();
            Vector3 upTarget = upBase;

            Vector3 centripetal = Vector3.zero;
            if (left.taut && left.anchor != null) centripetal += (left.anchor.position - transform.position).normalized;
            if (right.taut && right.anchor != null) centripetal += (right.anchor.position - transform.position).normalized;
            centripetal = Vector3.ProjectOnPlane(centripetal, fwd);

            if (centripetal.sqrMagnitude > 1e-4f && bankIntoSwing > 0f)
            {
                upTarget = Vector3.Slerp(upBase, centripetal.normalized, bankIntoSwing);
            }
            else if (uprightSettleRate > 0f)
            {
                Vector3 worldUp = Vector3.ProjectOnPlane(Vector3.up, fwd);
                if (worldUp.sqrMagnitude > 1e-4f)
                    upTarget = Vector3.Slerp(upBase, worldUp.normalized, 1f - Mathf.Exp(-uprightSettleRate * dt));
            }
            if (upTarget.sqrMagnitude < 1e-6f) upTarget = upBase;

            Quaternion target = Quaternion.LookRotation(fwd, upTarget);
            Quaternion next = Quaternion.RotateTowards(transform.rotation, target, noseTrackDegPerSec * dt);
            transform.rotation = next;
            accumulatedRotation = next;
            lastWrittenRotation = next;
            rotationWritten = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Autopilot: the vessel drives its own arms from the AI's live objective
        // ─────────────────────────────────────────────────────────────────────

        void TickAutopilotAim()
        {
            Vector3 desired = AutopilotDesiredDir();
            CourseFrame(out var fwd, out var r, out var u);
            float lateral = Vector3.Dot(desired, r);
            bool turning = Vector3.ProjectOnPlane(desired, fwd).magnitude > Mathf.Sin(10f * Mathf.Deg2Rad);
            float rest = RestAimYawDeg;

            for (int i = 0; i < 2; i++)
            {
                var a = i == 0 ? left : right;
                float sideSign = a.side == GibbonArm.Left ? -1f : 1f;
                if (turning && Mathf.Sign(lateral) == sideSign)
                    a.aiAimDir = Vector3.RotateTowards(fwd, desired, Mathf.Max(aiCastYawDeg, rest) * Mathf.Deg2Rad, 0f);
                else
                    a.aiAimDir = Quaternion.AngleAxis(sideSign * rest, u) * fwd;
            }
        }

        Vector3 AutopilotDesiredDir()
        {
            var ai = VesselStatus.AIPilot;
            Vector3 to = ai != null ? ai.CurrentTargetPosition - transform.position : CourseDir;
            return to.sqrMagnitude > 1e-4f ? to.normalized : CourseDir;
        }

        void TickAutopilotArms()
        {
            Vector3 desired = AutopilotDesiredDir();
            Vector3 fwd = CourseDir;
            float cosRelease = Mathf.Cos(aiReleaseAngleDeg * Mathf.Deg2Rad);
            float now = Time.time;

            for (int i = 0; i < 2; i++)
            {
                var a = i == 0 ? left : right;
                if (a.phase != ArmPhase.Latched) continue;
                bool aligned = a.taut && Vector3.Dot(fwd, desired) >= cosRelease;
                bool swept = a.sweptAngle >= aiMaxSwingDeg;
                bool timed = now - a.latchTime >= aiMaxSwingSeconds;
                if (aligned || swept || timed)
                    ReleaseArm(a.side);
            }

            if (left.phase != ArmPhase.Free || right.phase != ArmPhase.Free) return;
            if (now < aiNextCastTime) return;

            CourseFrame(out _, out var r, out _);
            float lateral = Vector3.Dot(desired, r);
            GibbonArm side;
            if (Mathf.Abs(lateral) > Mathf.Sin(10f * Mathf.Deg2Rad))
                side = lateral < 0f ? GibbonArm.Left : GibbonArm.Right;
            else
            {
                side = aiAlternate ? GibbonArm.Right : GibbonArm.Left;
                aiAlternate = !aiAlternate;
            }

            var arm = Arm(side);
            if (arm.aimTarget == null)
            {
                var other = Other(arm);
                if (other.aimTarget == null) return;
                arm = other;
                side = other.side;
            }
            CastArm(side);
            aiNextCastTime = now + aiRegrabDelay;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Sweep: a taut line is a blade
        // ─────────────────────────────────────────────────────────────────────

        void SweepLine(ArmState a, Vector3 prevPos)
        {
            if (!sweepDestroysPrisms || a.phase != ArmPhase.Latched || !a.taut || a.anchor == null) return;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            Vector3 anchorPos = a.anchor.position;
            Vector3 handNow = HandPosition(a);
            Vector3 handPrev = handNow - (transform.position - prevPos);
            float travel = (handNow - handPrev).magnitude;
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(sweepStepDistance, 0.5f)), 1, 12);
            int kills = 0;
            Vector3 impact = velocity;

            for (int s = 0; s < steps; s++)
            {
                float t = steps == 1 ? 1f : (float)s / (steps - 1);
                Vector3 hand = Vector3.Lerp(handPrev, handNow, t);
                index.QuerySegment(hand, anchorPos, sweepBladeRadius, queryScratch);
                for (int i = 0; i < queryScratch.Count; i++)
                {
                    var p = queryScratch[i];
                    if (p == null || !p.gameObject.activeInHierarchy) continue;
                    if (p == left.anchorPrism || p == right.anchorPrism) continue;
                    if (p.prismProperties != null && p.prismProperties.IsSuperShielded) continue;
                    p.Damage(impact, VesselStatus.Domain, VesselStatus.PlayerName);
                    kills++;
                }
            }

            if (kills > 0)
            {
                a.sweepPulse = 1f;
                if (sweepKillShakeIntensity > 0f && IsLocalHumanVessel)
                {
                    var cam = GetCameraController();
                    if (cam != null) cam.Shake(sweepKillShakeIntensity, sweepKillShakeDuration);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Visuals (runtime primitives; continuity of existence at both ends)
        // ─────────────────────────────────────────────────────────────────────

        void BuildVisuals()
        {
            if (visualsBuilt) return;
            visualsBuilt = true;
            EnsureMaterials();
            BuildArmVisuals(left, "Left");
            BuildArmVisuals(right, "Right");
        }

        void EnsureMaterials()
        {
            if (lineMaterial == null)
            {
                if (tetherMaterial != null) lineMaterial = tetherMaterial;
                else lineMaterial = MakeLitEmissive(new Color(0f, 0.35f, 0.4f, 1f), new Color(0f, 1.6f, 1.8f, 1f));
            }
            if (reticleDimMaterial == null)
                reticleDimMaterial = MakeLitEmissive(new Color(0.05f, 0.25f, 0.28f, 1f), new Color(0f, 0.6f, 0.7f, 1f));
            if (reticleLockMaterial == null)
                reticleLockMaterial = MakeLitEmissive(new Color(0.1f, 0.4f, 0.4f, 1f), new Color(0.2f, 2.4f, 2.4f, 1f));
        }

        static Material MakeLitEmissive(Color baseColor, Color emission)
        {
            // LIT, not Unlit: an unlit tube is a flat silhouette with no depth cue. URP/Lit gives
            // the rod a shaded gradient and a specular streak, and the HDR emission still blooms.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            bool lit = shader != null;
            if (!lit) shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var m = new Material(shader);
            if (lit)
            {
                m.color = baseColor;
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.85f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission);
            }
            else
            {
                var hot = emission; hot.a = 1f;
                m.color = hot;
                m.SetColor("_BaseColor", hot);
            }
            return m;
        }

        void BuildArmVisuals(ArmState a, string label)
        {
            a.line = CreatePrimitive(PrimitiveType.Capsule, $"Gibbon{label}Line", lineMaterial, out a.lineRenderer);
            a.lineRenderer.enabled = false;
            a.hand = CreatePrimitive(PrimitiveType.Capsule, $"Gibbon{label}Hand", lineMaterial, out a.handRenderer);
            a.reticle = CreatePrimitive(PrimitiveType.Sphere, $"Gibbon{label}Reticle", reticleDimMaterial, out a.reticleRenderer);
            a.reticle.localScale = Vector3.zero;
            a.reticleAlpha = 0f;
        }

        static Transform CreatePrimitive(PrimitiveType type, string childName, Material material, out MeshRenderer mr)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = childName;
            if (go.TryGetComponent<Collider>(out var col)) Destroy(col);
            mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (material != null) mr.sharedMaterial = material;
            return go.transform;
        }

        void DestroyArmVisuals(ArmState a)
        {
            if (a.line) Destroy(a.line.gameObject);
            if (a.hand) Destroy(a.hand.gameObject);
            if (a.reticle) Destroy(a.reticle.gameObject);
        }

        void UpdateVisuals(float dt)
        {
            if (!visualsBuilt) return;
            float speedT = Mathf.Clamp01(speed / speedThicknessRef);
            float heat = 1f + speedThicknessBoost * speedT + 0.15f * tempo;
            float radius = tetherRadius * heat;
            UpdateArmVisual(left, dt, radius);
            UpdateArmVisual(right, dt, radius);
        }

        void UpdateArmVisual(ArmState a, float dt, float radius)
        {
            Vector3 hand = HandPosition(a);

            // Line
            if (a.retracting)
            {
                a.retractTimer -= dt;
                float t = a.retractDuration > 0f ? Mathf.Clamp01(a.retractTimer / a.retractDuration) : 0f;
                if (t <= 0f)
                {
                    a.retracting = false;
                    if (a.lineRenderer) a.lineRenderer.enabled = false;
                }
                else
                {
                    PoseCapsule(a.line, hand, Vector3.Lerp(hand, a.retractEnd, t), radius * slackThickness);
                }
            }
            else if (a.phase == ArmPhase.Casting)
            {
                PoseCapsule(a.line, hand, hand + a.castDir * Mathf.Min(a.castExtension, maxLineLength), radius * slackThickness);
            }
            else if (a.phase == ArmPhase.Latched && a.anchor != null)
            {
                a.sweepPulse = Mathf.Max(0f, a.sweepPulse - sweepPulseDecay * dt);
                float r = radius * (a.taut ? 1f : slackThickness) * (1f + 0.8f * a.sweepPulse);
                PoseCapsule(a.line, hand, a.anchor.position, r);
            }

            // Reticle: the AIM reticle while free, the FLING reticle while latched (where the hull
            // will be flingLookAheadSeconds after letting go - thread it through the next hoop).
            bool free = a.phase == ArmPhase.Free && !a.retracting;
            bool latched = a.phase == ArmPhase.Latched;
            float targetAlpha = (free || latched) ? 1f : 0f;
            a.reticleAlpha = Mathf.MoveTowards(a.reticleAlpha, targetAlpha, reticleFadeRate * dt);
            if (a.reticleAlpha <= 0f)
            {
                if (a.reticleRenderer.enabled) a.reticleRenderer.enabled = false;
                if (a.handRenderer.enabled) a.handRenderer.enabled = false;
                return;
            }

            Vector3 point;
            bool locked;
            if (latched) { point = transform.position + velocity * flingLookAheadSeconds; locked = a.taut; }
            else if (free) { point = a.aimPoint; locked = a.aimTarget != null; }
            else { point = a.reticle.position; locked = false; }

            float dist = Vector3.Distance(hand, point);
            float size = (reticleRadius + reticleRadiusPerDistance * dist) * (locked ? reticleLockScale : 1f) * a.reticleAlpha;
            a.reticle.position = point;
            a.reticle.localScale = Vector3.one * (2f * size);
            var wantMat = locked ? reticleLockMaterial : reticleDimMaterial;
            if (a.reticleRenderer.sharedMaterial != wantMat) a.reticleRenderer.sharedMaterial = wantMat;
            if (!a.reticleRenderer.enabled) a.reticleRenderer.enabled = true;

            // The hand runs hull -> aim point only while the arm is free; latched, the line is the hand.
            if (free)
            {
                if (!a.handRenderer.enabled) a.handRenderer.enabled = true;
                PoseCapsule(a.hand, hand, point, armRadius * a.reticleAlpha);
            }
            else if (a.handRenderer.enabled)
            {
                a.handRenderer.enabled = false;
            }
        }

        static void PoseCapsule(Transform capsule, Vector3 from, Vector3 to, float radius)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) { capsule.localScale = Vector3.zero; return; }
            capsule.position = (from + to) * 0.5f;
            capsule.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            // A Unity capsule is 2 units tall at scale 1 (radius 0.5): scale.y = len/2, x/z = 2r.
            capsule.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Juice
        // ─────────────────────────────────────────────────────────────────────

        CustomCameraController GetCameraController()
        {
            var controller = CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() : null;
            return controller as CustomCameraController;
        }

        void PlaySnapShake(float centripetalAccel)
        {
            if (snapShakeIntensity <= 0f || !IsLocalHumanVessel) return;
            float t = Mathf.Clamp01(centripetalAccel / snapShakeAccelRef);
            if (t <= 0.02f) return;
            var cam = GetCameraController();
            if (cam != null) cam.Shake(snapShakeIntensity * t, snapShakeDuration);
        }

        void PlayReleaseShake()
        {
            if (releaseShakeIntensity <= 0f || !IsLocalHumanVessel) return;
            float t = Mathf.Clamp01(speed / speedThicknessRef);
            if (t < releaseShakeSpeedFloor) return;
            var cam = GetCameraController();
            if (cam != null) cam.Shake(releaseShakeIntensity * t, releaseShakeDuration);
        }

        void PlayOneShot(EventReference reference)
        {
            // Empty slot = silence (never a substitute event). The live Instance is the honest
            // resolver here: vessels are injected, but the transformer must not depend on it.
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio == null) return;
            audio.PlaySFXEvent(reference, transform.position);
        }
    }
}
