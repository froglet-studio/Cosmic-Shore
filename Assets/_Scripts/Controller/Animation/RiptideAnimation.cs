using CosmicShore.Gameplay;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public class RiptideAnimation : VesselAnimation
    {
        [SerializeField] Transform DriftHandle;
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

        [Header("Bank")]
        [Tooltip("How the wings bank relative to the HULL when you roll. +1 banks them with the " +
                 "hull, -1 against it, and the magnitude exaggerates or damps the read; 0 stops " +
                 "the wings responding to roll at all.\n\n" +
                 "This is a signed scaler rather than a fixed sign because the authored value was " +
                 "chosen against the legacy art, where the wings hung off the model root at " +
                 "identity. On the rig they hang off 'winghold' bones carrying a -180 degree " +
                 "roll, and the hull and wings read as banking opposite ways. The hull is the one " +
                 "that is right (VesselTransformer.Roll, shared by six vessels), so the wings are " +
                 "what moves - and it is exposed here so it can be judged in the editor instead " +
                 "of guessed in code.")]
        [SerializeField] float wingRollResponse = -1f;

        Vector3 ForwardWingOffset => new(0, 0, driftWingForward);
        Vector3 BackwardThrusterOffset => new(0, 0, -driftJetBackward);
        static readonly Vector3 defaultThrusterOffset = Vector3.zero;
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

            DriftHandle = ResolvePart(DriftHandle, "DriftHandle");

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

            Vector3 wingOffset;
            Vector3 thrusterOffset;

            AnimatePart(Chassis,
                        pitch * animationScaler,
                        yaw * animationScaler,
                        roll * animationScaler,
                        Vector3.zero,
                        transform);

            // A DRIFT SPLITS THE SHIP IN TWO, and this is the whole point of the manoeuvre's look:
            // the fuselage and jaws turn to AIM wherever the pilot is pointing, while the wings and
            // engines stay lined up with COURSE - the direction the ship is actually travelling -
            // so the hull reads as slewing across its own path. The appendages are the instrument
            // that tells everyone which way the Dolphin is really going. They also slide apart
            // (wings forward, engines back) to open a gap the aiming fuselage can turn through
            // without clipping them.
            //
            // So they are puppeteered in the DRIFT HANDLE's frame, which is aimed along Course,
            // and the chassis and jaws stay in the vessel's. No re-parenting is involved: the frame
            // carries the part's rest pose (VesselAnimation.RotatePartFromRestInFrame), which does
            // the same job without moving anything in the hierarchy.
            Transform courseFrame = transform;
            if (VesselStatus.IsDrifting)
            {
                SafeLookRotation.TrySet(DriftHandle, VesselStatus.Course, transform.up,
                                        DriftHandle ? DriftHandle.gameObject : gameObject,
                                        logError: false);
                if (DriftHandle) courseFrame = DriftHandle;
                wingOffset = ForwardWingOffset;
                thrusterOffset = BackwardThrusterOffset;
            }
            else
            {
                wingOffset = defaultWingOffset;
                thrusterOffset = defaultThrusterOffset;
            }

            // WHILE DRIFTING THE APPENDAGES GO QUIET. They are the course indicator: they hold
            // Course and say nothing else, so the only thing moving in response to the stick is
            // the half of the ship that is aiming - the fuselage and the jaws. Leaving them
            // responsive reads as the whole ship still flying while it is supposed to be sliding.
            bool aiming = VesselStatus.IsDrifting;
            float wingPitch = aiming ? 0f : Brake(throttle) * animationScaler;
            float wingYawR  = aiming ? 0f : (yaw + throttle) * exaggeratedAnimationScaler;
            float wingYawL  = aiming ? 0f : (yaw - throttle) * exaggeratedAnimationScaler;
            float wingRollR = aiming ? 0f : (roll * wingRollResponse + pitch) * animationScaler;
            float wingRollL = aiming ? 0f : (roll * wingRollResponse - pitch) * animationScaler;

            AnimatePart(RightWing, wingPitch, wingYawR, wingRollR, wingOffset, courseFrame);
            AnimatePart(LeftWing,  wingPitch, wingYawL, wingRollL, wingOffset, courseFrame);

            var pitchScalar = aiming ? 0f : pitch * exaggeratedAnimationScaler;
            var yawScalar = aiming ? 0f : yaw * exaggeratedAnimationScaler;
            var rollScalar = aiming ? 0f : roll * exaggeratedAnimationScaler;


            // Each thruster is driven around ITS OWN rest pose, looked up per part. The previous
            // InitialRotations[partIndex] indexing was offset by two against animationTransforms
            // (InitialRotations starts with the two nose entries), so every engine animated around
            // a neighbour's rest pose - harmless while all six rested at identity, wrong on the
            // Dolphin's authored 26-169 degree engine cases and fatal on a rig.
            for (int partIndex = 0; partIndex < animationTransforms.Count; partIndex++)
            {
                AnimatePart(animationTransforms[partIndex], pitchScalar, yawScalar, rollScalar, thrusterOffset, courseFrame);
            }

        }

        // Swings the wings and thrusters out to the drift handle while drifting, and HOME again
        // when not. "Home" is each part's own authored parent, captured at Initialize - never a
        // single shared node. On the part-per-mesh art every one of these was a direct child of
        // Chassis, so this is exactly the old behaviour; on the rigged model they are bones whose
        // parents ('winghold.l/r', 'jetholdT/m/B.l/r') carry the rest angles that fan the six
        // engines out, and re-homing them all onto 'fuse' would permanently flatten the armature
        // and collapse the jets onto one point.
        // The drift RE-PARENTING is gone, deliberately. The methods that lived here hung the
        // wings and engines off a handle aimed along Course, so they swung WITH the aim - the
        // opposite of what a drift needs from them: those parts exist to get out of the hull's way
        // while it turns, not to turn with it. Once the frame was fixed to the vessel and the
        // clearance became an offset, that machinery did nothing at all, and inert machinery in
        // this file has already cost three playtests. 'DriftHandle' survives as an empty anchor
        // (no children, nothing reads it) in case a course-aligned frame is wanted again; git
        // carries the removed methods.

        // 'frame' is the space this part is puppeteered in - the vessel for anything that belongs
        // to the hull, the Course-aligned drift handle for the parts that signal where the ship is
        // really going. Never the part's OWN parent, which on this rig is a bone whose axes are
        // nothing like the ship's; that is what made pitch read as roll and inverted it.
        void AnimatePart(Transform part, float pitch, float yaw, float roll, Vector3 offset,
                         Transform frame)
        {
            if (!part) return;
            RotatePartFromRestInFrame(part, pitch, yaw, roll, frame);
            MovePartFromRest(part, offset, frame);
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
            Transforms.Add(DriftHandle);
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