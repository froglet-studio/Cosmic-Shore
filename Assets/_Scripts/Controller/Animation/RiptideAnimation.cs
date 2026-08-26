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

        const float animationScaler = 25f;
        const float exaggeratedAnimationScaler = 3 * animationScaler;

        // THE DRIFT CLEARANCE. A drifting Dolphin swings its hull to aim while it keeps sliding, so
        // the wings slide FORWARD and the engines slide BACK to open a gap the fuselage and jaws can
        // turn through without clipping. That is the whole purpose of the positional half of this
        // puppetry, and it is stated here because the code lost it twice:
        //
        //   * as absolute local positions, which only ever read correctly because the legacy
        //     part-per-mesh art hung every part off the model root at roughly those places (the
        //     engines rested at z -2.047 against a constant saying -1.7, so even there it dragged
        //     them 0.35 forward). On a rig a bone's local position is relative to its PARENT BONE;
        //   * and as a degenerate pair - the "default" and "backward" engine positions were the
        //     SAME vector, so the engines never moved at all. Translating them faithfully preserved
        //     that, which is how a hull shipped with half its clearance missing.
        //
        // Offsets are in the VESSEL's space (+z forward) and are authorable, because how much room
        // the jaws need is a feel question, not a fact about the model.
        [Header("Drift clearance")]
        [Tooltip("How far FORWARD the wings slide while drifting, in vessel units, so the hull can " +
                 "swing to aim without the wings fouling it.")]
        [SerializeField] float driftWingForward = 2.3f;

        [Tooltip("How far BACK the engines slide while drifting - the other half of the same gap. " +
                 "Zero here is what shipped, and it is why the jaws clipped the engines.")]
        [SerializeField] float driftJetBackward = 2.3f;

        [Tooltip("How far back the engines sit AT REST, in vessel units. This is not clearance: " +
                 "it is where the engines live. The legacy art authored its engine cases at " +
                 "z -2.047 and the rig's jet bones rest at z -1.90 (both models are unit-1 and " +
                 "every scale in the chain is 1, so those are directly comparable world units), " +
                 "so the rig sits them 0.15 further FORWARD than the ship they replaced. A feel " +
                 "value from there - tune it here, not in code.")]
        [SerializeField] float jetRestBackward = 0.15f;

        Vector3 ForwardWingOffset => new(0, 0, driftWingForward);
        Vector3 RestThrusterOffset => new(0, 0, -jetRestBackward);
        Vector3 BackwardThrusterOffset => new(0, 0, -(jetRestBackward + driftJetBackward));
        static readonly Vector3 defaultWingOffset = Vector3.zero;

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
        private void OnEnable() => AttachJawMeter();

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
        }

        // POSITIONS MUST SETTLE WHEN THE STICK IS IDLE TOO.
        //
        // `VesselAnimation.Update` takes one of two paths: `Idle()` when the pilot is not touching
        // the stick, `PerformShipPuppetry(...)` otherwise. `Idle()` relaxes ROTATIONS only -
        // `ResetAnimation` writes `localRotation` and nothing else - which is complete for every
        // other vessel, because this is the only animation in the fleet that POSITIONS its parts.
        //
        // So the whole positional layer lived on a path an idle vessel does not take: the engines'
        // resting setback was applied only while the stick was off centre and then froze wherever
        // it had reached the moment the pilot let go. A field that places a part cannot be written
        // only when the part is being animated.
        protected override void Idle()
        {
            base.Idle();
            ApplyRestingLayout();
        }

        /// <summary>Where the positioned parts live when nothing is asking them to move: the wings
        /// at rest, the engines at their resting setback. Shared by the idle path and by
        /// PerformShipPuppetry, so the two can never describe a different ship.</summary>
        void ApplyRestingLayout()
        {
            Quaternion frame = transform.rotation;
            MovePartFromRest(Chassis, Vector3.zero, frame);
            MovePartFromRest(RightWing, defaultWingOffset, frame);
            MovePartFromRest(LeftWing, defaultWingOffset, frame);
            if (animationTransforms == null) return;
            for (int i = 0; i < animationTransforms.Count; i++)
                MovePartFromRest(animationTransforms[i], RestThrusterOffset, frame);
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
            // the defect was never a sign: it was the CHASSIS TERM, which the old art delivered
            // through the hierarchy and the rig does not (see the composition note below).
            // Nothing in this method flips an axis. If an axis reads backwards, it is backwards
            // in the flight model or in the rig, and that is where it gets fixed.
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
                    appendageFrame = Quaternion.FromToRotation(transform.forward, course)
                                     * transform.rotation;
            }

            // Positions are a separate question from orientation, and they are read in the
            // VESSEL's frame: a clearance offset is "forward along the ship's z", which is what
            // the gap is measured against. Reading it in the course frame instead would make the
            // parts TRANSLATE as the hull aims, which is motion the manoeuvre does not want.
            Vector3 wingOffset = drifting ? ForwardWingOffset : defaultWingOffset;
            Vector3 thrusterOffset = drifting ? BackwardThrusterOffset : RestThrusterOffset;

            // THE CHASSIS TERM, PUT BACK BY HAND. On the part-per-mesh art every one of these
            // parts was a direct CHILD of the chassis (Dolphin.prefab before the rig swap:
            // LeftWing / RightWing.001 / Engine case L|R.1-3 all hung off `Dolphin_Test`, which is
            // what `Chassis` resolved to), so each inherited the chassis's own turn for free and
            // added its own on top. On the rig the wings hang off `winghold.l|r` and the engines
            // off `jetholdT|m|B.l|r` - a sibling branch of `fuse` - so that inherited term is
            // simply gone. Its loss is not subtle: the wings' own PITCH input is Brake(throttle),
            // which is zero unless the pilot is braking, so every bit of pitch response the wings
            // had came from the chassis. Without it they sit dead on that axis. Composed, never
            // added: Euler angles do not add at these amplitudes.
            Quaternion rightWingTurn = drifting ? Quaternion.identity
                : chassisTurn * Quaternion.Euler(Brake(throttle) * animationScaler,
                                                 (yaw + throttle) * exaggeratedAnimationScaler,
                                                 (roll + pitch) * animationScaler);

            Quaternion leftWingTurn = drifting ? Quaternion.identity
                : chassisTurn * Quaternion.Euler(Brake(throttle) * animationScaler,
                                                 (yaw - throttle) * exaggeratedAnimationScaler,
                                                 (roll - pitch) * animationScaler);

            AnimatePart(RightWing, rightWingTurn, wingOffset, appendageFrame);
            AnimatePart(LeftWing, leftWingTurn, wingOffset, appendageFrame);

            // Each thruster is driven around ITS OWN rest pose, looked up per part. The previous
            // InitialRotations[partIndex] indexing was offset by two against animationTransforms
            // (InitialRotations starts with the two nose entries), so every engine animated around
            // a neighbour's rest pose - harmless while all six rested at identity, wrong on the
            // Dolphin's authored 26-169 degree engine cases and fatal on a rig.
            Quaternion thrusterTurn = drifting ? Quaternion.identity
                : chassisTurn * Quaternion.Euler(pitch * exaggeratedAnimationScaler,
                                                 yaw * exaggeratedAnimationScaler,
                                                 roll * exaggeratedAnimationScaler);

            for (int partIndex = 0; partIndex < animationTransforms.Count; partIndex++)
                AnimatePart(animationTransforms[partIndex], thrusterTurn, thrusterOffset, appendageFrame);
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

        // 'frame' is the space this part's ROTATION is resolved in - the vessel's live rotation
        // for anything that belongs to the hull, the Course-aligned frame for the parts that go
        // on flying straight while it aims. Never the part's OWN parent, which on this rig is a
        // bone whose axes are nothing like the ship's; that is what made pitch read as roll and
        // inverted it. POSITION is always resolved in the vessel's frame - see below.
        void AnimatePart(Transform part, Quaternion turn, Vector3 offset, Quaternion frame)
        {
            if (!part) return;
            RotatePartFromRestInFrame(part, turn, frame);
            MovePartFromRest(part, offset, transform.rotation);   // always the VESSEL's z
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