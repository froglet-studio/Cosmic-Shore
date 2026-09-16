using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public class RiptideAnimation : VesselAnimation
    {
        [SerializeField] Transform Chassis;

        [SerializeField] Transform NoseTop;
        [SerializeField] Transform RightWing;
        [SerializeField] Transform NoseBottom;
        [SerializeField] Transform LeftWing;

        [SerializeField] Transform ThrusterTopRight;
        [SerializeField] Transform ThrusterRight;
        [SerializeField] Transform ThrusterBottomRight;
        [SerializeField] Transform ThrusterBottomLeft;
        [SerializeField] Transform ThrusterLeft;
        [SerializeField] Transform ThrusterTopLeft;
        [SerializeField] Transform topJaw;
        [SerializeField] Transform bottomJaw;

        List<Transform> animationTransforms;

        // The appendage frame of the previous frame. Read for two guards in PerformShipPuppetry:
        // the antiparallel HOLD (inside FromToRotation's degenerate cone) and the SLEW LIMIT.
        Quaternion _lastAppendageFrame = Quaternion.identity;

        // The cage may not turn faster than this. Legitimate cage motion is bounded by the hull's
        // ROLL rate (~110 deg/s; pure pitch/yaw aim leaves the cage invariant), so 360 deg/s is
        // 3x headroom that never engages in ordinary flight - while FromToRotation's churn near
        // the antipode is ~2/sin(angle-to-antipode) times the nose rate (68 deg/FRAME measured at
        // 3 degrees off a full-reverse aim), and leaving the hold cone is otherwise a one-frame
        // snap. Both are exact-written onto the parts since the cage stopped lerping, so the
        // limit is what keeps a reverse-aim sweep a fast swing instead of a strobe.
        const float CageSlewDegreesPerSecond = 360f;

        // THE CAGE ADOPTION - the re-parent's semantics without the re-parent. The legacy art
        // re-parented the appendages onto the course-aimed DriftHandle, which did two things at
        // once: it ADOPTED each part's current pose into the handle's coordinates (worldPosition-
        // preserving parent assignment), and it made the handle's frame carry the parts EXACTLY -
        // zero lag - while their local pose converged. A finite-rate lerp toward a hull-
        // independent target cannot reproduce that second half: the parts are parented under the
        // aiming HULL, so a 110 deg/s aim carries them off-station faster than lerpAmount 2 pulls
        // them back - a steady-state trail of ~1.9 world units and ~55 degrees at full aim rate,
        // collapsing whenever the aim slows. On screen that is the appendages being dragged
        // around by the nose and settling back: "the wings and jets still appear to move as I am
        // drifting and aiming". So during a drift each part's pose is written EXACTLY in cage
        // coordinates - the frame carries it with zero lag - while a single blend runs its
        // ADOPTED entry pose to its course-cage station (rest orientation, rest + clearance
        // position). Entry is continuous by construction (blend 0 IS the current pose, expressed
        // in cage coordinates); exit hands back to the ordinary lerped recovery, exactly as the
        // legacy re-parent-home did.
        bool _wasDrifting;
        float _driftBlend;
        readonly Dictionary<Transform, Quaternion> _driftEntryOrientation = new();
        readonly Dictionary<Transform, Vector3> _driftEntryPosition = new();

        const float animationScaler = 25f;
        const float exaggeratedAnimationScaler = 3 * animationScaler;

        // THE DRIFT CLEARANCE. A drifting Dolphin swings its hull to aim while it keeps sliding,
        // so the wings LUNGE FORWARD along the direction of travel and the fuselage and jaws turn
        // through the gap. That is the whole purpose of the positional half of this puppetry.
        //
        // THE OFFSETS ARE WORLD UNITS, AND EVERY DEFAULT BELOW IS A MEASUREMENT, NOT A FEEL VALUE.
        // This is the third representation these numbers have had, and the first with the ground
        // truth pinned:
        //
        //   * absolute units inherited from the legacy art shipped first - correct numbers, but
        //     nothing had established that the rig's bones live at the same world scale (the
        //     armature carries Lcl Scaling 100; Unity imports it as bones with lossyScale ~100
        //     whose WORLD poses land exactly on the hull);
        //   * fractions of a runtime-measured basis shipped next, to dodge that ambiguity. The
        //     first basis (the occlusion corridor's hull measure) resolved through the root bone
        //     and imported the 100x it was fleeing; the second (the parts' own reach, 1.96) was
        //     honest but STRUCTURALLY TOO SMALL: the proven pre-rig wing lunge is 2.2 world units
        //     from the rig's rest, and a |fraction| <= 1 clamp on a 1.96 basis cannot express it
        //     at any authoring.
        //
        // The ambiguity the fractions existed to dodge is now MEASURED AWAY (import model pinned
        // against the shipped colliders, residual 3e-5; the vessel root is scale 1; and
        // MovePartFromRest is an exact world-space round trip through any bone-chain scale), so
        // plain world units are both safe and the only representation that can state the measured
        // numbers. Tools/Build/verify_vessel_rig_puppetry_frames.py re-proves the round trip.
        [Header("Drift clearance (world units, measured against the shipped rig)")]
        [Tooltip("How far FORWARD (+z) the wings lunge while drifting, in world units in the " +
                 "COURSE frame, from the wing's RIG rest. 3.5 is the MEASURED true-clearance " +
                 "lunge: the aiming hull's jaw tip sweeps a 2.835-radius sphere about the vessel " +
                 "origin, and 3.5 is the smallest lunge (bisected L* = 3.492) that puts every " +
                 "wing vertex outside that sweep x1.05 gape margin - so the jaws cannot reach the " +
                 "wings at ANY drift aim angle. (2.2 was old-game parity, and the old game " +
                 "interpenetrated.)")]
        [SerializeField] float driftWingForward = 3.5f;

        [Tooltip("Engine offset during a DRIFT, world units along -z, in the COURSE frame, from " +
                 "the jet's RIG rest. Its own independent number since flight 12: it used to be " +
                 "the rest seat + a slide on top, so bumping the seat silently moved a drift " +
                 "clearance that had already been signed off. 1.25 is exactly that prior total " +
                 "(1.0 + 0.25), the drift position flight 12 confirmed perfect. The FLIGHT " +
                 "puppetry below never reads it and it never reads the flight pivots.")]
        [SerializeField] float driftJetBackwardTotal = 1.25f;

        // THE FLIGHT PUPPETRY IS THE LEGACY CHASSIS CHAIN, REPRODUCED IN VESSEL SPACE (flight 17).
        //
        // On the part-per-mesh art the wings and the six engine cases were CHILDREN of the
        // chassis (Dolphin_Test). That hierarchy did two things per frame that a rig's sibling
        // bones do not, and flights 1-16 reproduced neither:
        //
        //   * ORBIT. The chassis deflects Euler(25p, 25y, 25r) about its own origin and every
        //     child rides that turn - its POSITION swings around the chassis pivot, not only its
        //     orientation. A booster 1.7 wu aft of the pivot travels ~0.74 wu on a full 25-degree
        //     deflection; a wing bone 0.97 wu out travels ~0.42. Composing the chassis turn into
        //     the appendages' ORIENTATION alone (the previous construction) spun them in place
        //     about their own bone pivots while the fuselage tail swept away underneath them -
        //     which is what "the boosters and wings are the issue" looks like, and what no
        //     z-station could reach, because the seat was never the defect.
        //   * PIVOT. Each child's OWN turn is about ITS transform origin, and the art put that at
        //     ONE shared point per part class: all six engine cases at (0, 0.147, -2.047), both
        //     wings at (0, 0, 0.1). The code then LERPS that origin to a FLIGHT station every
        //     frame - engines to (0, 0.15, -1.7), wings to (0, 0, 0) - so in flight the boosters
        //     swing about a common seat 0.35 wu ahead of where they were authored, carrying their
        //     geometry +0.347 forward with them. On this rig each jet's bone sits at its own
        //     nozzle, 0.27-0.37 wu from that seat, and the wing bones at (+-0.969, 0, -0.114);
        //     turning a bone about itself is a different motion from turning it about the seat.
        //
        // Both fall out of ONE statement: each rig bone is a point RIGIDLY ATTACHED to the legacy
        // part frame, and it undergoes that frame's motion. The legacy frame goes from
        // (authoredPivot, rest) to (chassisTurn * flightPivot, chassisTurn * ownTurn * rest), so
        // a bone resting at R in the vessel frame lands at
        //
        //     chassisPivot + chassisTurn * (flightPivot + ownTurn * (R - chassisPivot - authoredPivot))
        //
        // with orientation chassisTurn * ownTurn * restOrientation. Every vertex the bone skins
        // therefore lands EXACTLY where a legacy vertex resting at the same point would - proven
        // to 1e-12 by Tools/Build/verify_vessel_rig_puppetry_frames.py check 8 - and at zero
        // input it reduces to rest + (flightPivot - authoredPivot): the +0.347 forward seat
        // flight 16 measured for the boosters, and the 0.1 wu aft drag on the wings that flight
        // 16 missed, both of them the per-frame drag bleeding-edge applies. The pivots are the
        // bleeding-edge prefab's and script's authored numbers verbatim; the chassis pivot is the
        // Chassis part's own rest position, read off the rig rather than authored.
        [Header("Flight puppetry - the legacy chassis chain (bleeding-edge's Dolphin.prefab, verbatim)")]
        [Tooltip("Where the legacy wing transforms were AUTHORED, in chassis (= vessel) space: " +
                 "both wings at (0, 0, 0.1) in bleeding-edge's Dolphin.prefab. The wing geometry " +
                 "is defined around this point, so it is the pivot the wing's own turn is taken " +
                 "about. A measurement, not a tuning.")]
        [SerializeField] Vector3 wingAuthoredPivot = new(0f, 0f, 0.1f);

        [Tooltip("Where bleeding-edge PARKS that transform every frame (defaultWingPosition = " +
                 "(0, 0, 0) in its RiptideAnimation.cs): the wings' pivot in flight. Its " +
                 "difference from the authored pivot is the 0.1 wu aft drag the wings show at " +
                 "rest on bleeding-edge - the rig's wing geometry rests at z -0.304, the old " +
                 "game drew it at -0.403.")]
        [SerializeField] Vector3 wingFlightPivot = Vector3.zero;

        [Tooltip("How far FORWARD of that measured station the wings are DRAWN, world units " +
                 "along +z. 0 is bleeding-edge's station verbatim; the shipped 0.5811 is the " +
                 "measurement that lands the wings' drawn centroid at CRUISE throttle on the " +
                 "drawn ship's own midpoint (+0.1781 in vessel space - the mean of the hull's " +
                 "-2.471 tail and +2.827 nose, taken over every cluster of the shipped rig). " +
                 "Applied through the one station BOTH the flight path and the resting layout " +
                 "read, so the two still cannot describe two different ships. The drift cage is " +
                 "independent of it by construction: its station is restPos + driftWingForward, " +
                 "absolute from rest, so flight 12's signed-off clearance is unchanged.")]
        [SerializeField] float wingStationForward = 0.5811f;

        [Tooltip("The throttle at which the wings sit UNSWEPT. Throttle here is " +
                 "InputStatus.XDiff, which runs 0..1 with 0.5 at neutral cruise - so " +
                 "bleeding-edge's bare `throttle` term holds the wings 37.5 degrees swept while " +
                 "the stick is centred, which is already 4 degrees past the measured 33.41 " +
                 "degrees at which their roots enter the fuselage. 0 reproduces bleeding-edge " +
                 "exactly. 0.5 is cruise - and it is also the pose ApplyRestingLayout has always " +
                 "drawn, so the live puppetry and the resting layout now agree at a centred stick " +
                 "instead of disagreeing by 37.5 degrees.")]
        [SerializeField] float wingThrottleNeutral = 0.5f;

        [Tooltip("Degrees of wing BACK-SWEEP per unit of throttle above the neutral above. 75 " +
                 "(with neutral 0) is bleeding-edge's exaggeratedAnimationScaler verbatim. The " +
                 "shipped 66 is measured against the shipped geometry: at the station above, the " +
                 "wing roots first touch a body cluster at 67.86 degrees per unit, so 66 is the " +
                 "largest whole value that keeps every wing vertex outside every body cluster " +
                 "across the whole throttle range. The YAW-stick term is untouched and still runs " +
                 "at exaggeratedAnimationScaler.")]
        [SerializeField] float wingThrottleSweep = 66f;

        [Tooltip("Where the legacy engine-case transforms were AUTHORED: all six at " +
                 "(0, 0.14725685, -2.0470345) in bleeding-edge's Dolphin.prefab. ONE shared " +
                 "point - the boosters radiate from it, so their own turn swings each about that " +
                 "seat, never about its own nozzle. A measurement, not a tuning.")]
        [SerializeField] Vector3 thrusterAuthoredPivot = new(0f, 0.14725685f, -2.0470345f);

        [Tooltip("Where bleeding-edge parks every engine case each frame (defaultThrusterPosition " +
                 "= (0, .15, -1.7) in its RiptideAnimation.cs): the boosters' pivot in flight, " +
                 "and the +0.347 forward seat flight 16 measured (with the 0.0027 wu lift that " +
                 "came with it). This is the dial for the boosters' STATION: bleeding-edge draws " +
                 "them at z -1.898..-1.540, the leading edge 0.572 wu inside the tail plane. The " +
                 "drift total above is independent of it by construction.")]
        [SerializeField] Vector3 thrusterFlightPivot = new(0f, 0.15f, -1.7f);

        [Tooltip("The boosters' OWN puppetry amplitude, degrees per unit stick on all three axes, " +
                 "composed after the chassis term and taken about the flight pivot. 75 is " +
                 "bleeding-edge's exaggeratedAnimationScaler (3 x the 25-degree chassis term): " +
                 "the legacy look. Flights 13-15 cut this to 5 to stop the boosters 'swinging " +
                 "off the body' - that swing was the bone-pivot construction, not the amplitude, " +
                 "and on the chain at 75 they sweep exactly as the old art did.")]
        [SerializeField] float thrusterAnimationScaler = 75f;

        [Tooltip("OFF = legacy parity: the wings and boosters take the roll input the way " +
                 "bleeding-edge does. ON = flight 8's ask ('rolling clockwise should do what " +
                 "rolling counterclockwise does now'): negates the roll input in the appendages' " +
                 "OWN terms only - they still ORBIT with the chassis's true roll, because the " +
                 "orbit IS the hierarchy and mirroring it would tear the parts off the body - so " +
                 "they deflect about their own pivot the other way. That ask was made against " +
                 "the bone-pivot construction, whose roll response was wrong for its own reasons " +
                 "(flight 15's 50-degree floor), so it ships OFF for a flight that judges the " +
                 "legacy chain first.")]
        [SerializeField] bool mirrorAppendageRoll = false;

        // Offsets are authored against a ~3.45-unit hull, so anything past about a hull length
        // is a typo, not a tuning. Bounds the absurd; tunes nothing.
        const float MaxOffsetWorldUnits = 4f;

        static float Sane(float worldUnits) =>
            Mathf.Clamp(worldUnits, -MaxOffsetWorldUnits, MaxOffsetWorldUnits);

        Vector3 ForwardWingOffset => new(0, 0, Sane(driftWingForward));

        // The wings' station: bleeding-edge's measured flight pivot plus the forward offset
        // flight 18 solved. ONE expression, read by the flight path and by the resting layout.
        Vector3 WingStation => wingFlightPivot + new Vector3(0, 0, Sane(wingStationForward));

        // Brake()'s exact mirror, and the wings' half of the same idea. Brake() answers "how far
        // BELOW its threshold is the throttle"; this answers "how far ABOVE cruise". Bleeding-edge
        // fed the wings the bare throttle, which never reaches zero because XDiff rests at 0.5 -
        // so the wings were swept into the hull at a centred stick and only got deeper from there.
        // Clamped at zero rather than signed: below cruise the wings belong to Brake()'s pitch,
        // and a signed term rakes them 33 degrees FORWARD at full brake, carrying the tips to
        // z +2.03 against jaws that start at +0.832.
        float SweepAboveCruise(float throttle)
            => throttle > wingThrottleNeutral ? throttle - wingThrottleNeutral : 0f;
        Vector3 BackwardThrusterOffset => new(0, 0, -Sane(driftJetBackwardTotal));

        // The point the chassis turns about, in the vessel frame: the Chassis part's own rest
        // position (the `fuse` bone, and Dolphin_Test before it, both sit at the vessel origin).
        // Captured in Initialize, after the rest positions are.
        Vector3 _chassisPivot;

        [Tooltip("Which ResourceSystem slot drives the jaw gape. 0 = Energy, the meter skimming " +
                 "fills and a crystal impact spends.")]
        [SerializeField] int JawResourceIndex;

        [Tooltip("Jaw gape in degrees at EMPTY energy, per jaw. The blast is a CAPSULE, not a " +
                 "sphere, even at rest — it already reaches atan((minExplosionScale / 2) / cone " +
                 "height) across the gape — so the jaws must not read fully closed. Today that is " +
                 "atan((400 / 2) / 2400) = 4.76 degrees.")]
        [SerializeField] float MinJawAngle = 4.7636f;

        [Tooltip("Jaw gape in degrees at FULL energy, per jaw. This is the hull's copy of the " +
                 "blast readout, so it MUST equal the crystal-impact blast's gape HALF-ANGLE at " +
                 "full energy: atan((maxExplosionScale / 2) / cone height). Today that is " +
                 "atan((2080 / 2) / 2400) = 23.43 degrees — DolphinVesselExplosionByCrystalEffect's " +
                 "_maxExplosionScale over AOEConicExplosion.prefab's height. Space scales both " +
                 "together, so the angle is invariant; change either number and this must follow.")]
        [SerializeField] float MaxJawAngle = 23.4287f;

        /// <summary>Jaw gape in degrees at full energy - the HUD's jaw icon mirrors this so the
        /// cockpit and the hull never disagree about how wide the next blast will be.</summary>
        public float MaxJawAngleDegrees => MaxJawAngle;

        /// <summary>Jaw gape in degrees at empty energy - the blast's resting gape, which is NOT
        /// zero. The HUD's jaw icon mirrors this for the same reason as the maximum.</summary>
        public float MinJawAngleDegrees => MinJawAngle;

        /// <summary>
        /// The EXACT gape half-angle at normalized energy <paramref name="t"/>, shared by the hull
        /// and the HUD icon so they cannot draw the same quantity two different ways.
        ///
        /// The blast's tip extent is LINEAR in energy (it lerps minExplosionScale → maxExplosionScale)
        /// while the angle is its arctangent, so lerping the ANGLES is wrong in between — that was a
        /// standing approximation here, worth up to a few degrees mid-charge. Lerping the TANGENTS
        /// and taking the arctangent is exact, and needs nothing but the two authored angles:
        ///
        ///     tan(angle(t)) = lerp(min, max, t) / (2 * coneHeight)
        ///                   = lerp(tan(minAngle), tan(maxAngle), t)
        ///
        /// so no dependency on the impact-effect SO is introduced to get it right.
        /// </summary>
        public static float GapeAngleAt(float t, float minDegrees, float maxDegrees)
        {
            t = Mathf.Clamp01(t);
            float tan = Mathf.Lerp(Mathf.Tan(minDegrees * Mathf.Deg2Rad),
                                   Mathf.Tan(maxDegrees * Mathf.Deg2Rad), t);
            return Mathf.Atan(tan) * Mathf.Rad2Deg;
        }

        // Bone names of the rigged dolphin model (dolphin_shapekey_with_animations.fbx), which was
        // authored FOR this script: six jets (top/middle/bottom x l/r), two jaws and two wings.
        // Only the jaws hang off 'fuse'; each wing and jet sits behind its own 'winghold'/'jethold'
        // parent, and those parents carry the rest angles that fan the engines out - which is why
        // the drift re-parent restores each part's own parent rather than a shared node. Legacy
        // names from the older part-per-mesh model follow as fallbacks, so this resolves on either
        // art. See VesselAnimation.ResolvePart.
        protected override void ResolveParts()
        {
            Chassis = ResolvePart(Chassis, "fuse", "Chassis", "Dolphin_Test");
            LeftWing = ResolvePart(LeftWing, "wing.l", "LeftWing");
            RightWing = ResolvePart(RightWing, "wing.r", "RightWing.001", "RightWing");

            ThrusterTopLeft = ResolvePart(ThrusterTopLeft, "jetT.l", "Engine case Left.1");
            ThrusterTopRight = ResolvePart(ThrusterTopRight, "jetT.r", "Engine case Right.1");
            ThrusterLeft = ResolvePart(ThrusterLeft, "jetm.l", "Engine case Left.2");
            ThrusterRight = ResolvePart(ThrusterRight, "jetm.r", "Engine case Right.2");
            ThrusterBottomLeft = ResolvePart(ThrusterBottomLeft, "jetB.l", "Engine case Left.3");
            ThrusterBottomRight = ResolvePart(ThrusterBottomRight, "jetB.r", "Engine case Right.3");

            // The rigged model's jaws ARE its nose halves - one pair of bones serves both roles.
            topJaw = ResolvePart(topJaw, "jaw.u", "TopNose");
            bottomJaw = ResolvePart(bottomJaw, "jaw.b", "bottomNose");
            NoseTop = ResolvePart(NoseTop, "jaw.u", "TopNose");
            NoseBottom = ResolvePart(NoseBottom, "jaw.b", "bottomNose");

            // Drive every part around the pose it was authored in. The legacy wings and noses rest
            // at identity (unchanged), the six engine cases at 26-169 degrees, and the rig's bones
            // at their own fan-out angles - all now animate relative to that instead of toward a
            // bare Euler.
            CaptureRestRotations(Chassis, LeftWing, RightWing, NoseTop, NoseBottom, topJaw, bottomJaw,
                                 ThrusterTopLeft, ThrusterTopRight, ThrusterLeft,
                                 ThrusterRight, ThrusterBottomLeft, ThrusterBottomRight);

            // Only the parts this animation actually POSITIONS - each anchored to the pose it was
            // authored in, under the parent it was authored under.
            CaptureRestPositions(Chassis, LeftWing, RightWing,
                                 ThrusterTopLeft, ThrusterTopRight, ThrusterLeft,
                                 ThrusterRight, ThrusterBottomLeft, ThrusterBottomRight);

            ReportUnresolvedParts();
        }

        // The jaw hookup is symmetric across OnEnable/OnDisable, not Initialize/OnDisable. It used
        // to subscribe in Initialize and detach in OnDisable, so a single disable/enable cycle -
        // pooling, a vessel swap, a HUD toggle, a scene transition - dropped the subscription for
        // good and the gape froze until the vessel was re-initialized.
        private void OnEnable()
        {
            // A component disabled mid-drift and re-enabled inside a LATER drift would otherwise
            // skip the entry adoption (stale _wasDrifting) and snap to station in one frame.
            _wasDrifting = false;
            AttachJawMeter();
        }

        private void OnDisable() => DetachJawMeter();

        Resource _jawMeter;

        void AttachJawMeter()
        {
            if (_jawMeter != null) return;

            // Guarded: a vessel enabled before Initialize (pooled prefab, aborted spawn) has no
            // VesselStatus yet - Initialize re-runs this once the status lands.
            var resources = VesselStatus?.ResourceSystem?.Resources;
            if (!topJaw || resources == null || (uint)JawResourceIndex >= resources.Count) return;

            _jawMeter = resources[JawResourceIndex];
            _jawMeter.OnResourceChange += calculateBlastAngle;
            calculateBlastAngle(_jawMeter.CurrentAmount); // seed the gape from the live meter
        }

        void DetachJawMeter()
        {
            if (_jawMeter == null) return;
            _jawMeter.OnResourceChange -= calculateBlastAngle;
            _jawMeter = null;
        }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            AttachJawMeter(); // OnEnable ran before the status existed; bind now that it does.

            animationTransforms = new List<Transform>() { ThrusterTopRight, ThrusterRight, ThrusterBottomRight, ThrusterBottomLeft, ThrusterLeft, ThrusterTopLeft };

            // Seed the drift frame's degeneracy hold (see PerformShipPuppetry) with something
            // sane for the first frame.
            _lastAppendageFrame = transform.rotation;

            // The chain orbits about the chassis's own rest position (captured by ResolveParts
            // above). Zero on both arts; read rather than authored so a rig that seats `fuse`
            // elsewhere is still right.
            _chassisPivot = TryGetRestPositionInVessel(Chassis, out var chassisRest) ? chassisRest : Vector3.zero;
        }

        // POSITIONS MUST SETTLE WHEN THE STICK IS IDLE TOO.
        //
        // `VesselAnimation.Update` takes one of two paths: `Idle()` when the pilot is not touching
        // the stick, `PerformShipPuppetry(...)` otherwise. `Idle()` relaxes ROTATIONS only -
        // `ResetAnimation` writes `localRotation` and nothing else - which is complete for every
        // other vessel, because this is the only animation in the fleet that POSITIONS its parts.
        // A field that places a part cannot be written only when the part is being animated.
        //
        // AND A STICK-IDLE DRIFT IS STILL A DRIFT. InputStatus.Idle is raised during play by the
        // TOUCH strategy alone (gamepad/keyboard never set it), and on touch it can overlap
        // IsDrifting for the post-release ease-out window - during which the resting layout used
        // to yank the appendages home while the ship was still visibly sliding. Rerouting to the
        // puppetry with zero inputs holds the drift layout instead; it is exactly the pose a
        // centred stick produces (the drifting branch never evaluates Brake, whose Brake(0) would
        // otherwise pitch the wings - so this is NOT a drop-in for Idle outside a drift).
        protected override void Idle()
        {
            if (VesselStatus != null && VesselStatus.IsDrifting)
            {
                PerformShipPuppetry(0, 0, 0, 0);
                return;
            }
            _wasDrifting = false;   // the drift edge detector must see a non-drift frame even on
                                    // the touch-idle path, or a re-entry adopts a stale pose
            base.Idle();
            ApplyRestingLayout();
        }

        /// <summary>Where the positioned parts live when nothing is asking them to move: the
        /// legacy chain at zero input - every part at its rest, dragged by (flightPivot -
        /// authoredPivot): the boosters +0.347 forward, the wings 0.1 aft. Shared by the idle
        /// path and by PerformShipPuppetry, so the two can never describe a different ship.</summary>
        void ApplyRestingLayout()
        {
            Quaternion frame = transform.rotation;
            MovePartFromRest(Chassis, Vector3.zero, frame);
            SeatPartOnChassis(RightWing, wingAuthoredPivot, WingStation, frame);
            SeatPartOnChassis(LeftWing, wingAuthoredPivot, WingStation, frame);
            if (animationTransforms == null) return;
            for (int i = 0; i < animationTransforms.Count; i++)
                SeatPartOnChassis(animationTransforms[i], thrusterAuthoredPivot, thrusterFlightPivot, frame);
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            // NOTE ON ROLL DIRECTION. A previous pass negated roll here, on the theory that the
            // bank read backwards. It does - but the puppetry is not where that lives: the HULL
            // itself banks that way, from VesselTransformer.Roll(), which is byte-identical to
            // bleeding-edge and shared by six vessels (Manta, Squirrel, Falcon, Rhino, Shrike and
            // this one). Negating here only made the wings disagree with the hull they are bolted
            // to. The puppetry must follow the flight model, whatever the flight model does, so
            // roll is passed through untouched and the direction question belongs to
            // VesselTransformer - fleet-wide, and a deliberate decision rather than a side effect
            // of a vessel-construction pass.

            // THE TERMS BELOW ARE THE ONES THIS SHIP HAS ALWAYS USED. Three passes of per-axis
            // sign scalers were tried here and every one of them was wrong on playtest, because
            // the defect was never a sign: it was the CHASSIS HIERARCHY, which the old art
            // delivered for free and the rig does not - first its orientation term (flight 3),
            // then its ORBIT and its shared PIVOTS (flight 17, see PlacePartOnChassis). With the
            // roll mirror at its default (off) nothing in this method flips an axis. If an axis
            // reads backwards, it is backwards in the flight model or in the rig, and that is
            // where it gets fixed.
            Quaternion chassisTurn = Quaternion.Euler(pitch * animationScaler,
                                                      yaw * animationScaler,
                                                      roll * animationScaler);

            AnimatePart(Chassis, chassisTurn, Vector3.zero, transform.rotation);

            // A DRIFT SPLITS THE SHIP IN TWO. The fuselage and jaws turn to AIM wherever the pilot
            // is pointing, while the wings and engines go on flying straight down COURSE - the
            // direction the ship is actually travelling - and slide apart (wings forward, engines
            // back) to open a gap the aiming fuselage can turn through without clipping them. They
            // are the instrument that says which way the Dolphin is really going.
            //
            // THE COURSE FRAME IS THE HULL, RE-AIMED ONTO COURSE, AND NOTHING ELSE. Take the
            // vessel's current rotation and swing it by the SHORTEST arc from its own nose onto
            // Course. Three properties fall out of that one line, and each of them is a thing an
            // earlier version got wrong:
            //
            //   * the appendages read as "a dolphin flying straight along Course", because that is
            //     literally what the frame is - the ship, pointed where it is going;
            //   * their UP stays the hull's up, which is roughly the camera's, because the chase
            //     camera rolls with the hull. FromToRotation is a pure swing about an axis
            //     perpendicular to the nose, so it injects no roll of its own;
            //   * it CANNOT accumulate. It is a pure function of (hull rotation, Course), rebuilt
            //     from scratch every frame with no state. A `DriftHandle` Transform could not be:
            //     parented under the vessel, the hull's own aiming carried it between one frame's
            //     write and the next read, and re-pointing only its forward axis left that twist
            //     in place - 24.6 degrees of it over an ordinary aim, on parts commanded to hold
            //     still. Freezing the entry orientation instead is stable but wrong: the parts
            //     then hold where the ship WAS rather than where it is going.
            bool drifting = VesselStatus.IsDrifting;
            Quaternion appendageFrame = transform.rotation;
            if (drifting)
            {
                Vector3 course = VesselStatus.Course;
                if (course.sqrMagnitude > 1e-6f)
                {
                    // FromToRotation is degenerate when the two vectors are ANTIPARALLEL - the
                    // swing axis is arbitrary, so as the nose wobbles around the antipode the
                    // frame's roll flips up to 180 degrees frame-to-frame and the appendages
                    // roll-thrash. Aiming fully backwards mid-drift is a reachable pose (nothing
                    // clamps drift aim), so near the antipode the previous frame's answer is
                    // held instead: continuous by construction, and re-converges the moment the
                    // nose leaves the degenerate cone. (The legacy DriftHandle had the mirror
                    // problem - SafeLookRotation failed safe at aim-vs-course ~90 degrees.)
                    if (Vector3.Dot(transform.forward, course.normalized) < -0.999f)
                        appendageFrame = _lastAppendageFrame;
                    else
                        appendageFrame = Quaternion.FromToRotation(transform.forward, course)
                                         * transform.rotation;
                }

                // Slew limit - drift path only; outside a drift the frame IS the hull and must
                // track it exactly.
                float maxStep = CageSlewDegreesPerSecond * Time.deltaTime;
                float step = Quaternion.Angle(_lastAppendageFrame, appendageFrame);
                if (step > maxStep)
                    appendageFrame = Quaternion.Slerp(_lastAppendageFrame, appendageFrame,
                                                      maxStep / step);
            }
            _lastAppendageFrame = appendageFrame;

            if (drifting && !_wasDrifting)
            {
                _driftBlend = 0f;
                var intoCage = Quaternion.Inverse(appendageFrame);
                foreach (var part in CagedParts())
                {
                    if (!part) continue;
                    _driftEntryOrientation[part] = intoCage * part.rotation;
                    _driftEntryPosition[part] = intoCage * (part.position - transform.position);
                }
            }
            _wasDrifting = drifting;
            if (drifting)
            {
                // Exponential, not linear: the legacy re-parent's local convergence and the
                // best-yet build's lerp were both fast-then-settle (63% at 0.5s, 95% at ~1.5s at
                // lerpAmount 2), and a constant-velocity lunge with a hard stop reads mechanical
                // beside every other ease on this ship.
                _driftBlend = Mathf.Lerp(_driftBlend, 1f, lerpAmount * Time.deltaTime);
            }

            // Positions ride the SAME frame as orientations. During a drift that is the
            // Course-aligned frame, so the whole appendage cage - positions AND orientations -
            // holds the course while the hull aims through it, exactly what the legacy art's
            // course-aimed DriftHandle re-parent produced (position and orientation together).
            // An earlier version read positions in the VESSEL's frame on the reasoning that the
            // clearance is "forward along the ship's z" - which made the cage SWEEP SIDEWAYS
            // with the aiming hull while its orientations claimed to fly straight, i.e. exactly
            // the "parts still move as I aim" read the drift split exists to remove. With the
            // Dolphin's locked-course drift the cage now holds perfectly still while the nose
            // turns.
            Vector3 wingOffset = ForwardWingOffset;
            Vector3 thrusterOffset = BackwardThrusterOffset;

            // THE APPENDAGES' OWN TERMS - bleeding-edge's, verbatim: the wings pitch on the brake,
            // yaw on (yaw +- throttle) at 3x, and bank on (roll +- pitch); the boosters take all
            // three inputs at thrusterAnimationScaler. The chassis term is NOT written here: on
            // the legacy art it arrived through the HIERARCHY (every one of these parts was a
            // child of the chassis), and PlacePartOnChassis puts that hierarchy back - orbit and
            // pivot both - rather than composing the chassis turn into the orientation alone,
            // which is what flights 1-16 did and what left the parts spinning in place while the
            // fuselage swept away under them (see the field comment above the pivots).
            //
            // The roll mirror is an opt-in (mirrorAppendageRoll) and OFF by default: with it off
            // nothing in this method flips an axis, and the direction question belongs to the
            // flight model.
            float rollSign = mirrorAppendageRoll ? -1f : 1f;

            // THE WINGS' THROTTLE TERM IS THE ONE THING HERE THAT IS NOT BLEEDING-EDGE'S, and
            // both of flight 18's asks come off it. Measured on the shipped rig: throttle is
            // InputStatus.XDiff, which RESTS AT 0.5, so bleeding-edge's `(yaw +- throttle) * 75`
            // holds the wings 37.5 degrees swept at a centred stick - past the 33.41-degree onset
            // at which a wing root enters the fuselage - and buries them 0.1808 wu at full
            // throttle. Unsweeping them at cruise moves the drawn wing 0.52 wu FORWARD for free
            // (a swept wing draws further aft than its pivot), and wingStationForward carries the
            // remaining 0.58 to the ship's midpoint. The yaw-stick half is untouched.
            float wingSweep = SweepAboveCruise(throttle) * wingThrottleSweep;

            Quaternion rightWingTurn = Quaternion.Euler(Brake(throttle) * animationScaler,
                                                        yaw * exaggeratedAnimationScaler + wingSweep,
                                                        (rollSign * roll + pitch) * animationScaler);

            Quaternion leftWingTurn = Quaternion.Euler(Brake(throttle) * animationScaler,
                                                       yaw * exaggeratedAnimationScaler - wingSweep,
                                                       (rollSign * roll - pitch) * animationScaler);

            Quaternion thrusterTurn = Quaternion.Euler(pitch * thrusterAnimationScaler,
                                                       yaw * thrusterAnimationScaler,
                                                       rollSign * roll * thrusterAnimationScaler);

            if (drifting)
            {
                // The drift cage: rest orientation, rest + clearance position, held in the
                // course frame. Signed off (flight 12) and untouched by the chain.
                PlacePartInCage(RightWing, wingOffset, appendageFrame);
                PlacePartInCage(LeftWing, wingOffset, appendageFrame);
                for (int partIndex = 0; partIndex < animationTransforms.Count; partIndex++)
                    PlacePartInCage(animationTransforms[partIndex], thrusterOffset, appendageFrame);
                return;
            }

            PlacePartOnChassis(RightWing, chassisTurn, rightWingTurn, wingAuthoredPivot, WingStation, appendageFrame);
            PlacePartOnChassis(LeftWing, chassisTurn, leftWingTurn, wingAuthoredPivot, WingStation, appendageFrame);

            // All six boosters share one turn and one pair of pivots, exactly as they shared one
            // Euler and one defaultThrusterPosition on bleeding-edge; what fans them out is each
            // bone's own rest, which the chain carries through untouched.
            for (int partIndex = 0; partIndex < animationTransforms.Count; partIndex++)
                PlacePartOnChassis(animationTransforms[partIndex], chassisTurn, thrusterTurn,
                                   thrusterAuthoredPivot, thrusterFlightPivot, appendageFrame);
        }

        // NEITHER THE DRIFT RE-PARENTING NOR THE DRIFT HANDLE IS USED ANY MORE, deliberately.
        // The old art hung the wings and engines off a `DriftHandle` GameObject aimed along
        // Course; the rig resolves the same frame as a quaternion instead, which is both simpler
        // and the only version that cannot accumulate twist (see PerformShipPuppetry).
        // Re-parenting a rig is separately unsafe: those bones' parents ('winghold.l|r',
        // 'jetholdT|m|B.l|r') carry the rest angles that fan the six engines out, so re-homing
        // them onto one node would flatten the armature and collapse the jets onto a point. The
        // `DriftHandle` object survives in the prefab as an inert empty; nothing reads it, and
        // git carries the removed methods.

        // 'frame' is the space this part is resolved in - BOTH halves: the vessel's live
        // rotation for anything that belongs to the hull, the Course-aligned frame for the parts
        // that go on flying straight while it aims. Never the part's OWN parent, which on this
        // rig is a bone whose axes are nothing like the ship's; that is what made pitch read as
        // roll and inverted it. Used for the chassis alone since flight 17; the appendages go
        // through PlacePartOnChassis below.
        void AnimatePart(Transform part, Quaternion turn, Vector3 offset, Quaternion frame)
        {
            if (!part) return;
            RotatePartFromRestInFrame(part, turn, frame);
            MovePartFromRest(part, offset, frame);
        }

        // THE LEGACY CHASSIS CHAIN, ONE PART AT A TIME. Where a legacy part frame carried a point
        // resting at restPos: the chassis turn orbits the whole part about the chassis pivot, the
        // part's own turn swings it about its FLIGHT pivot, and its geometry rides from the
        // authored pivot to the flight pivot. Pure, so the flight path and the resting layout
        // read the same function and cannot describe two different ships.
        static Vector3 ChainPosition(Vector3 restPos, Vector3 chassisPivot, Quaternion chassisTurn,
                                     Quaternion ownTurn, Vector3 authoredPivot, Vector3 flightPivot)
            => chassisPivot + chassisTurn * (flightPivot + ownTurn * (restPos - chassisPivot - authoredPivot));

        // The flight-time pose writer for the wings and boosters: orientation chassis * own *
        // rest, position on the chain, both lerped in the part's own parent space exactly as
        // bleeding-edge's AnimatePart lerped them under the chassis.
        void PlacePartOnChassis(Transform part, Quaternion chassisTurn, Quaternion ownTurn,
                                Vector3 authoredPivot, Vector3 flightPivot, Quaternion frame)
        {
            if (!part || !TryGetRestPositionInVessel(part, out var restPos)) return;
            RotatePartFromRestInFrame(part, chassisTurn * ownTurn, frame);
            MovePartInFrame(part, ChainPosition(restPos, _chassisPivot, chassisTurn, ownTurn,
                                                authoredPivot, flightPivot), frame);
        }

        // The chain at zero input, position only: rest + (flightPivot - authoredPivot). Rotations
        // are the idle path's (VesselAnimation.Idle relaxes every part to its rest), which is the
        // same pose the chain converges to with both turns at identity.
        void SeatPartOnChassis(Transform part, Vector3 authoredPivot, Vector3 flightPivot, Quaternion frame)
        {
            if (!part || !TryGetRestPositionInVessel(part, out var restPos)) return;
            MovePartInFrame(part, ChainPosition(restPos, _chassisPivot, Quaternion.identity, Quaternion.identity,
                                                authoredPivot, flightPivot), frame);
        }

        IEnumerable<Transform> CagedParts()
        {
            yield return RightWing;
            yield return LeftWing;
            if (animationTransforms == null) yield break;
            for (int i = 0; i < animationTransforms.Count; i++)
                yield return animationTransforms[i];
        }

        // The drift-time pose writer: EXACT in cage coordinates, so the course frame carries the
        // part with zero lag while _driftBlend runs its adopted entry pose to its station. See
        // the cage-adoption note on the fields. The chassis and jaws never come through here -
        // they belong to the hull.
        void PlacePartInCage(Transform part, Vector3 offset, Quaternion cage)
        {
            if (!part) return;
            if (!_driftEntryOrientation.TryGetValue(part, out var entryRot)) return;
            if (!TryGetRestPositionInVessel(part, out var restPos)) return;

            Quaternion stationRot = RestOrientationInVessel(part);
            Vector3 stationPos = restPos + offset;

            part.rotation = cage * Quaternion.Slerp(entryRot, stationRot, _driftBlend);
            part.position = transform.position
                          + cage * Vector3.Lerp(_driftEntryPosition[part], stationPos, _driftBlend);
        }

        // The jaws open around their rest pose too - identity on the legacy nose halves, the rig's
        // authored jaw angle on 'jaw.u'/'jaw.b'.
        //
        // This is the Dolphin's energy meter rendered on the HULL: the gape IS the width of the
        // blast the next crystal impact will release, so a pilot can read their blast without
        // looking at the HUD (which shows the same angle on its Time icon, taking its range from
        // MinJawAngleDegrees/MaxJawAngleDegrees so the two can never disagree). The two angles must
        // equal the blast's gape half-angle at empty and full energy -
        // atan((min|maxExplosionScale / 2) / coneHeight) on VesselExplosionByCrystalEffectSO + the
        // AOEConicExplosion prefab - or the hull lies about the blast. It was 21 degrees against an
        // 18.43-degree cone until this was measured.
        //
        // Since the blast became a CAPSULE sweep, the jaws are not just a matched number: the blast
        // extends along the very axis these jaws open across, so the hull's silhouette IS the
        // blast's silhouette in that plane - including at rest, where the blast is a short capsule
        // and the jaws therefore sit slightly open rather than shut.
        private void calculateBlastAngle(float currentAmmo)
        {
            float angle = GapeAngleAt(currentAmmo, MinJawAngle, MaxJawAngle);
            if (topJaw) topJaw.localRotation = Quaternion.Euler(-angle, 0, 0) * RestRotationOf(topJaw);
            if (bottomJaw) bottomJaw.localRotation = Quaternion.Euler(angle, 0, 0) * RestRotationOf(bottomJaw);
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(NoseTop);
            Transforms.Add(RightWing);
            Transforms.Add(NoseBottom);
            Transforms.Add(LeftWing);
            Transforms.Add(ThrusterTopRight);
            Transforms.Add(ThrusterRight);
            Transforms.Add(ThrusterBottomRight);
            Transforms.Add(ThrusterBottomLeft);
            Transforms.Add(ThrusterLeft);
            Transforms.Add(ThrusterTopLeft);
            Transforms.Add(topJaw);
            Transforms.Add(bottomJaw);

            // Idle() pairs InitialRotations with Transforms BY INDEX, so this list is built in
            // Transforms order, one entry per part. LocalRotationOf keeps that alignment even
            // when a part is unbound (it contributes identity instead of throwing).
            for (int i = 0; i < Transforms.Count; i++)
                InitialRotations.Add(LocalRotationOf(Transforms[i]));
        }
    }
}