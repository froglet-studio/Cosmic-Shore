using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using Camera = UnityEngine.Camera;

namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour, ICameraController
    {
        private Transform _followTarget;
        private Vector3 _followOffset = new(0f, 10f, 0f); 

        // --- Smoothing and Update Control ---
        private float _followSmoothTime = 0.2f;
        private float _rotationSmoothTime = 5f;
        private bool _disableRotationLerp = false;
        private const bool UseFixedUpdate = false;

        private Vector3 _velocity;
        private Vector3 _lastTargetPos;
        private float _lateralDominance; // low-pass-filtered 0..1: how lateral the ship's motion is
        private CameraSettingsSO _currentSettings;
        private Coroutine _distanceLerpRoutine;
        public bool adaptiveZoomEnabled;
        private float _neutralOffsetZ;
        private float _lookAhead;   // CameraSettingsSO.lookAheadDistance
        private float _lookLift;    // CameraSettingsSO.lookAheadLift
        private float _chaseEase;   // CameraSettingsSO.chaseEaseRate (0 = hard-attached)
        private float _framingFov;  // CameraSettingsSO.framingFieldOfView (0 = none)
        private Vector3 _easedLook; // the eased look point while _chaseEase > 0
        private bool _easedLookValid;

        /// <summary>
        /// Look behind: pose the camera at the MIRROR of its follow offset — the same distance
        /// ahead of the vessel that it normally sits behind — while still looking at the ship,
        /// so the pilot sees their own nose against whatever is chasing them. Driven only by
        /// <c>VesselRearView</c> (Docs/REAR_VIEW.md); nothing else may write it.
        ///
        /// <para>It is a FLAG rather than a written offset on purpose. The mirror is applied at
        /// the point of use (<see cref="EffectiveOffset"/>) and <see cref="_followOffset"/> is
        /// left alone, so everything that legitimately moves this camera keeps writing that
        /// field and keeps working while the rear view is up — the zoom-out abilities, adaptive
        /// zoom, the skimmer's camera-scaling prism effect, and a vessel swap re-applying its
        /// own settings. Writing a mirrored offset instead would mean the first of those to
        /// fire silently put the camera back behind the ship.</para>
        /// </summary>
        public bool RearView { get; set; }

        /// <summary>
        /// While set, the camera frames THIS WORLD POINT instead of the vessel — the Butterfly's
        /// Fold places its destination across the whole cell, and a pilot cannot choose a place
        /// they cannot see. Driven only by <c>VesselPlacementView</c>; nothing else may write it.
        ///
        /// <para>It moves the POINT and nothing else: the offset, the distance and the ROTATION
        /// FRAME still come from the vessel, so the camera sits behind the destination at the
        /// vessel's own follow distance, oriented the way the pilot is oriented. That last part is
        /// load-bearing rather than incidental — the Fold addresses its target in the vessel's
        /// ROLLED frame, so "roll the world until the place you want is where your thumbs already
        /// are" only reads if the camera rolls with it.</para>
        ///
        /// <para>Applied at the point of use (<see cref="FollowPoint"/>), never by writing
        /// <see cref="_followTarget"/> — the same reasoning as <see cref="RearView"/>. The follow
        /// target is re-written by the spawn chain, by a vessel swap and by every system that
        /// re-applies a <c>CameraSettingsSO</c>; a placement written into it would be silently
        /// dropped by the first of those to fire, and would leave the camera framing a point in
        /// space if the ability's teardown missed a path.</para>
        ///
        /// <para>It BEATS the rear view rather than composing with it. Two vantages that re-pose
        /// one camera must be ORDERED (<c>Docs/REAR_VIEW.md</c>), and the order is the one the
        /// pilot asked for most recently and most specifically: a placement is a decision being
        /// made right now, and looking backwards from a destination you have not chosen yet is
        /// not a thing anyone asked for.</para>
        /// </summary>
        public Vector3? PlacementAnchor { get; set; }

        /// <summary>
        /// A still vantage the camera eases INTO and holds instead of chasing the vessel: a world
        /// position, the point it looks at, and its up. Driven only by the Thresher
        /// (<c>ThresherExecutor</c>, <c>R_VesselActions/THRESHER.md</c>) — when its planted orbit
        /// spins faster than a chase camera can follow without making the pilot dizzy, the camera
        /// detaches and WATCHES the ship spin instead of riding it.
        ///
        /// <para>Applied at the point of use as a BLEND over the ordinary follow pose, never by
        /// writing <see cref="_followOffset"/> or the follow target — the same reasoning as
        /// <see cref="RearView"/>. Setting it eases in over <see cref="SpectateBlendSeconds"/>;
        /// clearing it eases back out to the live follow pose, so neither edge is a cut. Null (the
        /// default, and the only value any other vessel ever sees) leaves the camera exactly as
        /// before this existed: the blend stays at 0 and no line of the follow path changes.</para>
        /// </summary>
        public SpectateView? Spectate
        {
            get => _spectate;
            set
            {
                _spectate = value;
                if (value.HasValue) _lastSpectate = value.Value;
            }
        }
        private SpectateView? _spectate;
        private SpectateView _lastSpectate;
        private float _spectateBlend;
        // The un-blended follow pose, kept while a blend is up so the follow path's smoothing
        // continues from where IT left the camera, never from the vantage it was blended toward.
        private Vector3 _followPosePosition;
        private Quaternion _followPoseRotation;

        /// <summary>Seconds to ease fully into, or back out of, a <see cref="Spectate"/> vantage.</summary>
        public float SpectateBlendSeconds { get; set; } = 0.6f;

        /// <summary>
        /// A cap (degrees/second) on how fast the FOLLOW FRAME — the rotation the offset is laid
        /// out in, and whose up the camera looks along — may turn toward the target's rotation.
        /// 0 (the default, and the only value any other vessel ever sees) makes the frame the
        /// target's rotation exactly, as before this existed.
        ///
        /// <para>Why it exists: the fleet's cameras are hard-attached (<c>CameraMode.FixedCamera</c>
        /// sets <see cref="_disableRotationLerp"/>), so a hull that turns faster than any stick can
        /// turn it carries the camera with it in the same frame. The Thresher's planted ball does
        /// exactly that — hooking onto the orbit swings the hull up to 91° in one frame — and read
        /// as the camera "getting whipped around". Below the cap the frame tracks the hull exactly,
        /// so ordinary flying is unchanged; only the spikes are spread over time. Driven only by
        /// <c>ThresherExecutor</c>; cleared on a follow-target change.</para>
        /// </summary>
        public float MaxFollowTurnRate { get; set; }

        /// <summary>
        /// With <see cref="MaxFollowTurnRate"/> set, also a cap (degrees/second²) on how fast the follow frame's
        /// turn RATE may change, so it eases into and out of a turn instead of starting and stopping one in a
        /// frame (the rate cap alone is bang-bang: a hull winding a 400°/s spin down swung the frame from +240
        /// to −240°/s in one frame). It tracks the target's own turn and closes the remaining angle no faster
        /// than it can stop (√(2·a·angle)), proportionally near zero so it settles rather than chattering.
        /// 0 (the default, and every other vessel) = the rate cap alone. Driven only by <c>ThresherExecutor</c>
        /// (<c>Docs/Studios/THRESHER_PLANT_LAB.md</c> round 2); cleared with the rate cap.
        /// </summary>
        public float MaxFollowAngularAcceleration { get; set; }
        private Quaternion _followFrame = Quaternion.identity;
        private bool _followFrameValid;
        private Vector3 _followAngularVelocity;          // degrees/s, axis x rate (angular-acceleration mode)
        private Quaternion _lastFollowTargetRotation;

        /// <summary>This frame's follow frame (see <see cref="MaxFollowTurnRate"/>).</summary>
        private Quaternion StepFollowFrame(float dt)
        {
            Quaternion target = _followTarget.rotation;
            if (MaxFollowTurnRate <= 0f || !_followFrameValid)
            {
                _followFrame = target;
                _followFrameValid = true;
                _followAngularVelocity = Vector3.zero;
                _lastFollowTargetRotation = target;
                return target;
            }
            dt = Mathf.Max(0f, dt);
            if (MaxFollowAngularAcceleration > 0f && dt > 0f)
            {
                float accel = MaxFollowAngularAcceleration;
                Vector3 targetRate = AngularStep(_lastFollowTargetRotation, target) / dt;
                Vector3 error = AngularStep(_followFrame, target);
                float e = error.magnitude;
                Vector3 want = targetRate + (e > 1e-5f ? error / e * Mathf.Min(Mathf.Sqrt(2f * accel * e), 10f * e) : Vector3.zero);
                want = Vector3.ClampMagnitude(want, MaxFollowTurnRate);
                _followAngularVelocity = Vector3.MoveTowards(_followAngularVelocity, want, accel * dt);
                float step = _followAngularVelocity.magnitude * dt;
                if (step > 1e-6f) _followFrame = Quaternion.AngleAxis(step, _followAngularVelocity.normalized) * _followFrame;
            }
            else
            {
                _followFrame = Quaternion.RotateTowards(_followFrame, target, MaxFollowTurnRate * dt);
            }
            _lastFollowTargetRotation = target;
            return _followFrame;
        }

        /// <summary>The rotation from <paramref name="from"/> to <paramref name="to"/> as axis × degrees,
        /// taking the short way round.</summary>
        static Vector3 AngularStep(Quaternion from, Quaternion to)
        {
            (to * Quaternion.Inverse(from)).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || Mathf.Abs(angle) < 1e-5f) return Vector3.zero;
            return axis.normalized * angle;
        }

        /// <summary>A still camera vantage (see <see cref="Spectate"/>).</summary>
        public struct SpectateView
        {
            public Vector3 Position;
            public Vector3 LookAt;
            public Vector3 Up;
        }

        /// <summary>
        /// How much of the follow offset's HEIGHT (y) the camera keeps: 1 = the authored offset,
        /// 0 = level with the vessel, directly behind it. Driven by the Butterfly's
        /// <c>SpreadWingsActionExecutor</c> — Mass mode drops the camera to directly behind the
        /// hull, Dust mode keeps the authored height (<c>R_VesselActions/BUTTERFLY.md</c> §2.0) —
        /// and by <c>ToggleTranslationModeActionExecutor</c>: the Sparrow flies level and keeps its
        /// authored height only in the turret stance (<c>SPARROW_TURRET_STANCE.md</c>).
        ///
        /// <para>Applied at the point of use (<see cref="EffectiveOffset"/>), never by writing
        /// <see cref="_followOffset"/>, for the same reason as <see cref="RearView"/>: every
        /// re-applied <c>CameraSettingsSO</c> and zoom ability writes that field, and the first
        /// of them to fire would silently put the height back. Reset to 1 whenever the follow
        /// target changes — a height belongs to the ship it was set for.</para>
        /// </summary>
        public float FollowHeightScale
        {
            get => _followHeightScale;
            set => _followHeightScale = Mathf.Clamp01(value);
        }
        private float _followHeightScale = 1f;

        /// <summary>
        /// How far the camera has moved into the SURROUND framing: 0 = the authored offset and look point,
        /// 1 = straight behind the hull at the authored distance × <see cref="SurroundDistanceScale"/>, looking
        /// at the hull itself (no look-ahead, no lift), so the hull sits in the middle of the frame. Driven only
        /// by the Rhino's omni-crystal Surge (<c>RhinoSurgeSwordRing</c>, <c>R_VesselActions/RHINO_ENERGY_SWORD.md</c>
        /// § "The omni-crystal Surge"), which eases it in and out itself: a ring of swords round the hull only
        /// reads if the hull is its centre and the whole ring is on screen.
        ///
        /// <para>Applied at the point of use (<see cref="EffectiveOffset"/>, <see cref="LookPoint"/>), never by
        /// writing <see cref="_followOffset"/>, for the same reason as <see cref="RearView"/>: adaptive zoom and
        /// every settings re-apply own that field. 0 (the default, and every other vessel) changes no line of the
        /// follow path. Reset with the follow target.</para>
        /// </summary>
        public float SurroundFraming01
        {
            get => _surroundFraming01;
            set => _surroundFraming01 = Mathf.Clamp01(value);
        }
        private float _surroundFraming01;

        /// <summary>The camera distance multiplier at full <see cref="SurroundFraming01"/> (1 = the authored distance).</summary>
        public float SurroundDistanceScale
        {
            get => _surroundDistanceScale;
            set => _surroundDistanceScale = Mathf.Max(0.01f, value);
        }
        private float _surroundDistanceScale = 1f;

        /// <summary>
        /// A vessel-owned multiplier on the camera's distance (and its look point), applied at the point of
        /// use like <see cref="SurroundFraming01"/>: 1 = the authored framing. Driven only by the Rhino, whose
        /// camera pulls back as Space lengthens its sword (<c>RhinoSwordCamera</c>, Garrett 2026-10-26: "Space
        /// alone can grow the sword, but move the camera accordingly"). Scaling the look point with the
        /// offset keeps the hull where it sat on screen. Reset with the follow target.
        /// </summary>
        public float VesselDistanceScale
        {
            get => _vesselDistanceScale;
            set => _vesselDistanceScale = Mathf.Max(0.01f, value);
        }
        private float _vesselDistanceScale = 1f;

        /// <summary>
        /// The surround framing applied to an offset: blend toward straight behind (or straight ahead, under the
        /// rear view's mirror) at the offset's own length, then scale the distance. Pure, so it is tested directly.
        /// </summary>
        public static Vector3 SurroundOffset(Vector3 offset, float framing01, float distanceScale)
        {
            float s = Mathf.Clamp01(framing01);
            if (s <= 0f) return offset;
            Vector3 centred = new(0f, 0f, (offset.z > 0f ? 1f : -1f) * offset.magnitude);
            return Vector3.Lerp(offset, centred, s) * Mathf.Lerp(1f, Mathf.Max(0.01f, distanceScale), s);
        }

        /// <summary>
        /// The world point the camera frames this frame: the placement anchor if one is set, else
        /// the follow target's own position — seen through a portal the camera has not reached yet
        /// while one is being carried (<see cref="CarryThroughSphere"/>).
        /// </summary>
        private Vector3 FollowPoint =>
            PlacementAnchor ?? (_followTarget ? _followTarget.position - PortalShift : Vector3.zero);

        /// <summary>The transform this camera follows (the vessel's camera follow target).</summary>
        public Transform FollowTarget => _followTarget;

        /// <summary>The settings asset last applied (the Vessel Studio window re-applies it live after an edit).</summary>
        public CameraSettingsSO CurrentSettings => _currentSettings;

        // --- Portal carry ---------------------------------------------------------------------
        //
        // A wormhole moves the SHIP the instant it enters, but a chase camera is tens to hundreds
        // of units behind it (the Butterfly's is 207). Moving the camera on the same frame is a
        // cut — the whole picture changes around a ship that has not visibly moved — and letting
        // the teleport guard snap it is the same cut with the smoothing state thrown away too.
        // So the camera follows the ship THROUGH the mouth instead: it keeps framing where the
        // ship WOULD be had the two mouths been one (the ship's position mapped back through the
        // pair), and it is itself moved across on the frame IT reaches the near mouth. Until
        // then the pilot sees their own ship through the mouth's exact view (WormholeView), which
        // is rendered from exactly the far-side vantage this camera is about to take — so the
        // hand-over is a change of frame with nothing on screen to show it. (The Butterfly's
        // original ring gates used a planar version of this; they became wormholes.)
        private bool _carrying;
        private Vector3 _carryShift;          // far mouth - near mouth, the pair's translation
        private Vector3 _carryCentre;         // the near mouth's centre
        private float _carryRadius;           // the near mouth's radius
        private float _carryDeadline;

        /// <summary>
        /// Longest a carry may last. Not a gameplay clock — nothing is added or removed by it —
        /// but a guard on a presentation state, so a camera that somehow never reaches the mouth
        /// (a pilot who turns straight back, a follow distance longer than the arena) is handed
        /// across rather than left framing a point on the wrong side of the world.
        /// </summary>
        private const float MaxCarrySeconds = 6f;

        /// <summary>The translation the camera is still waiting to take, or zero.</summary>
        private Vector3 PortalShift => _carrying ? _carryShift : Vector3.zero;

        /// <summary>True while the camera is still on the near side of a portal its ship has
        /// already gone through.</summary>
        public bool IsCarryingThroughPortal => _carrying;

        /// <summary>The portal translation still pending (far mouth minus near mouth), or zero.
        /// A view of the far side is posed by adding this to the camera's pose.</summary>
        public Vector3 PendingPortalShift => PortalShift;

        /// <summary>The near mouth of the portal being carried through (valid while
        /// <see cref="IsCarryingThroughPortal"/>).</summary>
        public Vector3 CarryMouthCentre => _carryCentre;

        /// <summary>
        /// The ship this camera follows has just been carried into a wormhole mouth — the sphere
        /// at <paramref name="centre"/> — and out of its partner, displaced by
        /// <paramref name="shift"/>. Follow it through rather than cutting: keep framing the ship
        /// through the near sphere (whose surface shows exactly the view from the far side —
        /// <see cref="WormholeView"/>) and move across when the camera itself reaches the sphere.
        ///
        /// <para><b>Identity-guarded</b>: a camera that is not following
        /// <paramref name="subject"/> (or one of its children) ignores the call, so a transit may
        /// ask every camera without knowing which one is the player's. Returns whether this
        /// camera took the carry.</para>
        ///
        /// <para>A sphere has no "side": crossing is reaching it, within the clearance at which its
        /// surface stops drawing for this camera (<see cref="WormholeGeometry.Clearance"/>) — the
        /// two share one number, so the camera lands just inside the far mouth's own clearance,
        /// where that mouth has just stopped drawing and the world beyond it is seen directly. A
        /// camera already there — the rear view sits AHEAD of the ship — is moved across at once,
        /// which is the same hand-over one frame earlier.</para>
        /// </summary>
        public bool CarryThroughSphere(Transform subject, Vector3 centre, float radius, Vector3 shift)
        {
            if (!_followTarget || !subject) return false;
            if (_followTarget != subject && !_followTarget.IsChildOf(subject)) return false;
            if (shift.sqrMagnitude < 1e-6f) return false;

            // A carry already in flight is finished first: two wormholes in a row compose, and the
            // camera must be in the first one's far frame before it can follow the ship into the
            // second.
            if (_carrying) FinishCarry();

            // A placement moves the framed point to an explicit world position; there is no ship
            // for the camera to trail through the mouth, so hand it across outright.
            if (PlacementAnchor.HasValue) { ShiftCamera(shift); return true; }

            _carryShift = shift;
            _carryCentre = centre;
            _carryRadius = Mathf.Max(0.01f, radius);
            _carryDeadline = Time.time + MaxCarrySeconds;
            _carrying = true;

            // Already through (rear view, or a camera that sits level with the ship): move now.
            if (CameraHasCrossed()) FinishCarry();
            else PublishCarryToCorridor();
            return true;
        }

        private bool CameraHasCrossed()
        {
            // "At the surface" counts as through: a camera within its own near clip of the sphere
            // would clip the mouth it is looking through and show the near side for a frame.
            float nearClip = Camera ? Camera.nearClipPlane : 0.3f;
            float reach = _carryRadius + WormholeGeometry.Clearance(nearClip);
            return (transform.position - _carryCentre).sqrMagnitude <= reach * reach;
        }

        /// <summary>
        /// Would the pilot still see their ship THROUGH the mouth from here? The line of sight to
        /// where the ship is framed must still pass through the ball (or the ship is framed inside
        /// it). Once it does not — the ship turned hard, or went in near the rim and the camera is
        /// trailing wide — continuing the carry would leave the pilot looking at a mouth with no
        /// ship in it, so the camera is handed across instead.
        /// </summary>
        private bool ShipVisibleThroughMouth(Vector3 framed) =>
            WormholeGeometry.SegmentHitsBall(transform.position, framed, _carryCentre, _carryRadius);

        private void TickCarry(Vector3 framed)
        {
            if (!_carrying) return;
            if (CameraHasCrossed() || !ShipVisibleThroughMouth(framed) || Time.time > _carryDeadline)
                FinishCarry();
        }

        /// <summary>
        /// Move the camera through the wormhole: its pose and its smoothing state together, by the
        /// pair's own translation. The map has no rotation, so the camera keeps its orientation and
        /// its SmoothDamp velocity exactly, which is what makes the frame after the hand-over
        /// continue the frame before it.
        /// </summary>
        private void FinishCarry()
        {
            if (!_carrying) return;
            var shift = _carryShift;
            _carrying = false;
            _carryShift = Vector3.zero;
            ShiftCamera(shift);
            PublishCarryToCorridor();
        }

        private void ShiftCamera(Vector3 shift)
        {
            transform.position += shift;
            _followPosePosition += shift;
            _lastTargetPos += shift;
            Feel.Shift(shift);   // the framed point moved with the camera; it is not motion to lag behind
        }

        private void CancelCarry()
        {
            if (!_carrying) return;
            _carrying = false;
            _carryShift = Vector3.zero;
            PublishCarryToCorridor();
        }

        /// <summary>
        /// Tell the occlusion corridor where the ship is framed from this camera's side of any
        /// portal it is still carrying. Identity-guarded at the corridor, so only a camera
        /// following the corridor's own vessel can move it.
        /// </summary>
        private void PublishCarryToCorridor()
        {
            if (_followTarget) PrismOcclusionCorridor.SetViewShift(_followTarget, PortalShift);
        }

        /// <summary>
        /// The offset actually used to pose the camera this frame: the authored one (its height
        /// scaled by <see cref="FollowHeightScale"/>), or its z-mirror while
        /// <see cref="RearView"/> is set. Mirroring z alone — not x, not y — is
        /// what puts the camera directly ahead at the same distance and the same height, rather
        /// than at some reflected vantage the vessel's settings never described.
        ///
        /// <para>There was briefly a FIRST-PERSON vantage here too, for the Serpent's scope. It
        /// is retired: a magnified cockpit view read as nauseating, so the scope's magnification
        /// moved into its own window and this camera went back to doing one thing
        /// (<c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 4). A future cockpit would be a
        /// third case here, not a revival of a flag nothing was setting.</para>
        /// </summary>
        private Vector3 EffectiveOffset => SurroundedOffset * FramingScale;

        /// <summary>The authored offset with the vessel's distance scale (<see cref="VesselDistanceScale"/>) and
        /// the surround framing (<see cref="SurroundFraming01"/>); neither during a placement.</summary>
        private Vector3 SurroundedOffset => PlacementAnchor.HasValue
            ? AuthoredOffset
            : SurroundOffset(AuthoredOffset, _surroundFraming01, _surroundDistanceScale) * _vesselDistanceScale;

        /// <summary>The authored offset with the height scale and rear-view mirror, before <see cref="FramingScale"/>.</summary>
        private Vector3 AuthoredOffset =>
            new(_followOffset.x,
                _followOffset.y * _followHeightScale,
                RearView && !PlacementAnchor.HasValue ? -_followOffset.z : _followOffset.z);

        /// <summary>
        /// How much nearer (below 1) or further the camera sits than authored, so the hull reads the size it was
        /// framed at (<see cref="CameraSettingsSO.framingFieldOfView"/>) through the player's own field of view:
        /// tan(framing / 2) / tan(current / 2). 1 when the settings name no framing field of view (every vessel
        /// but the Stoat) or during a placement.
        /// </summary>
        private float FramingScale
        {
            get
            {
                if (!(_framingFov > 0f) || PlacementAnchor.HasValue || !Camera || Camera.orthographic) return 1f;
                float now = Mathf.Clamp(Camera.fieldOfView, 1f, 179f);
                return Mathf.Tan(_framingFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(now * 0.5f * Mathf.Deg2Rad);
            }
        }

        /// <summary>
        /// The point the camera LOOKS AT: the framed point, moved <see cref="CameraSettingsSO.lookAheadDistance"/>
        /// along the vessel's nose and <see cref="CameraSettingsSO.lookAheadLift"/> along its up. Both are 0 on
        /// every vessel that does not author them, which is the framed point itself (the camera looks at the
        /// hull, as it always has). A placement frames an explicit world point, so it is never moved; rear
        /// view mirrors the look-ahead as it mirrors the offset, so the camera ahead looks past the hull
        /// behind it. Scaled by the warp like the offset, so the framing holds at any warp size. Taken in the
        /// follow frame (<paramref name="frame"/>, the target's rotation or its turn-rate-capped copy), as the offset is.
        /// </summary>
        private Vector3 LookPoint(Vector3 followPoint, float warp, Quaternion frame)
        {
            if (PlacementAnchor.HasValue || !_followTarget || (_lookAhead == 0f && _lookLift == 0f)) return followPoint;
            float ahead = RearView ? -_lookAhead : _lookAhead;
            // The surround framing looks at the hull itself, so it sits in the middle of the frame.
            Vector3 lookLocal = new Vector3(0f, _lookLift * _followHeightScale, ahead)
                                * ((1f - _surroundFraming01) * _vesselDistanceScale);
            float s = FramingScale;
            if (!Mathf.Approximately(s, 1f)) lookLocal = FramedLook(SurroundedOffset, lookLocal, s);
            return followPoint + frame * (lookLocal * warp);
        }

        /// <summary>
        /// The look point, in the hull's frame, that keeps the hull where it sat on screen at the framing field of
        /// view once the camera has moved to <c>offset · s</c>: the angle between the hull and the view's centre
        /// becomes atan(tan(angle) / s), in the same plane. Pure, so it is tested directly.
        /// </summary>
        public static Vector3 FramedLook(Vector3 offset, Vector3 lookLocal, float s)
        {
            Vector3 toHull = -offset, toLook = lookLocal - offset;
            if (toHull.sqrMagnitude < 1e-8f || toLook.sqrMagnitude < 1e-8f || !(s > 1e-4f)) return lookLocal;
            Vector3 h = toHull.normalized, v = toLook.normalized;
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(h, v), -1f, 1f));
            Vector3 cam = offset * s;
            if (angle < 1e-5f || angle > Mathf.PI * 0.5f - 1e-3f) return cam + v * toLook.magnitude * s;
            Vector3 axis = Vector3.Cross(h, v).normalized;
            float framed = Mathf.Atan(Mathf.Tan(angle) / s);
            return cam + Quaternion.AngleAxis(framed * Mathf.Rad2Deg, axis) * h * (toLook.magnitude * s);
        }

        private bool _warpClipActive;

        /// <summary>
        /// Near clip = the settings' near plane × the warp at the framed point, while a field warps
        /// it; restored to the settings' value once it no longer does. A field-free session never
        /// touches the plane, so ApplySettings / Activate stay its only writers there.
        /// </summary>
        private void ApplyWarpToNearClip(float warp)
        {
            bool warped = !Mathf.Approximately(warp, 1f);
            if (!warped && !_warpClipActive) return;
            float authoredNear = _currentSettings ? _currentSettings.nearClipPlane : 0.3f;
            Camera.nearClipPlane = Mathf.Max(0.001f, authoredNear * warp);
            _warpClipActive = warped;
        }

        // --- Camera Shake ---
        private float _shakeTimeRemaining;
        private float _shakeDuration;
        private float _shakeIntensity;

        // --- Camera feel (Docs/CAMERA_FEEL.md) ---
        // Inertia, kicks, shake, rumble, drift lean and breathing, added on top of the settled pose and taken
        // off again at the top of the next frame (UndoFeel). The dynamic and eased rigs smooth FROM the camera's
        // current pose, so a feel offset left in it would be smoothed into the follow itself; taking it off first
        // means every pose, zoom, rear-view and portal line below only ever sees the clean pose.
        private CameraFeel _feel;
        private Vector3 _feelPosition;                          // world offset added last frame
        private Quaternion _feelRotation = Quaternion.identity; // camera-local tilt added last frame
        private bool _feelApplied;
        private IVesselStatus _feelVessel;                      // the vessel the follow target belongs to
        private Component _feelVesselBody;                      // the same object, for Unity's destroyed check
        private float _feelWeight = 1f;
        // The followed hull's measured size and what it and the hull's table row give the G. Fields rather than
        // properties so the Inspector's Debug view shows them while tuning.
        private float _feelHullSize;     // the mean of the model's length, width and height, world units; 0 = not yet
        private int _feelHullTries;      // a model can appear a few frames after the follow starts (the spawn chain)
        private float _feelGForce = 1f;
        private float _feelResponse = 1f;

        /// <summary>This camera's feel state (<see cref="VesselCameraFeel"/> feeds it).</summary>
        internal CameraFeel Feel => _feel ??= new CameraFeel(GetInstanceID() * 0.137f);

        /// <summary>True when this camera is following <paramref name="vessel"/>, so its events are felt here.</summary>
        internal bool FeelsFor(IVesselStatus vessel) => _feelVesselBody && ReferenceEquals(_feelVessel, vessel);

        /// <summary>How hard the followed hull's events land on this camera (<see cref="CameraFeelConfigSO.ProfileFor"/>).</summary>
        internal float FeelWeight => _feelWeight;

        /// <summary>A world direction in this camera's own frame.</summary>
        internal Vector3 ToLocal(Vector3 worldDirection) => Quaternion.Inverse(transform.rotation) * worldDirection;

        private void Awake()
        {
            Camera.useOcclusionCulling = false;
        }

        private void OnEnable() => VesselCameraFeel.Register(this);

        private void OnDisable()
        {
            VesselCameraFeel.Unregister(this);
            UndoFeel();
            Feel.Reset();
        }

        private void LateUpdate()
        {
            if (!UseFixedUpdate)
                UpdateCamera();
        }

        private void UpdateCamera()
        {
            UndoFeel();

            if (!_followTarget)
            {
                RecoverLostFollowTarget();
                return;
            }

            // A placement frames an explicit WORLD point, which is not in the frame a carry is
            // measuring in; the placement view snaps the camera itself, so the carry just ends.
            if (_carrying && PlacementAnchor.HasValue) CancelCarry();

            // The point being FRAMED, which is the vessel unless a placement anchor is set. Every
            // read below is of this rather than of the target's own position — including the
            // teleport guard and `_lastTargetPos`, which exist to describe how far what the camera
            // is looking at moved this frame. Leaving them on the vessel would make the guard
            // blind during a placement (the vessel is stopped, so its delta is zero while the
            // framed point sweeps the whole cell) and would then fire it on the frame the anchor
            // is released.
            Vector3 followPoint = FollowPoint;

            if (_spectateBlend > 0f)
                transform.SetPositionAndRotation(_followPosePosition, _followPoseRotation);

            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = followPoint;

            // Warp field (Docs/WARP_FIELD.md): the camera sits at a warp-sized distance and clips
            // at a warp-sized near plane, so a vessel shrinking toward a field's centre keeps the
            // same framing — the near field gives nothing away and the far field grows. Applied at
            // the point of use like FollowHeightScale, never by writing _followOffset, which every
            // settings re-apply and zoom ability owns. Exactly 1 with no field.
            float warp = WarpFieldRuntime.ScaleAt(followPoint);
            ApplyWarpToNearClip(warp);

            Vector3 shipDelta = followPoint - _lastTargetPos;

            // Teleport guard: on a kickoff park / fresh spawn the follow target jumps a long way in one
            // frame (normal flight is only a few units/frame). Snap the camera into place instead of
            // SmoothDamping a wild swing across the arena - that swing read as a "wonky, jittery start".
            const float teleportStep = 50f;
            bool teleported = shipDelta.sqrMagnitude > teleportStep * teleportStep;
            if (teleported) _followFrameValid = false;   // a teleport cuts the follow frame too

            // The follow frame: the target's rotation, or a turn-rate-capped copy of it while a
            // vessel asks for one (MaxFollowTurnRate).
            Quaternion rotation = StepFollowFrame(Time.deltaTime);
            Vector3 frameUp = rotation * Vector3.up;
            Vector3 desiredPos = followPoint + rotation * (EffectiveOffset * warp);

            if (teleported)
            {
                transform.position = desiredPos;
                if (SafeLookRotation.TryGet(LookPoint(followPoint, warp, rotation) - transform.position, frameUp, out var snapRot, this, logError: false))
                    transform.rotation = snapRot;
                _velocity = Vector3.zero;
                _lateralDominance = 0f;
                _lastTargetPos = followPoint;
                _spectateBlend = 0f;   // a teleport cuts; a vantage set before it is stale
                _easedLook = LookPoint(followPoint, warp, rotation);
                _easedLookValid = true;
                Feel.Reset();          // and so is the feel: the jump is not motion to lag behind
                return;
            }

            // An eased chase (the Vessel Studio's: position and look point each close on their target at
            // chaseEaseRate per second, up tied to the follow frame's), so the hull swings in the frame through a
            // turn instead of sitting nailed to one pixel. 0 on every vessel that does not set it.
            if (_chaseEase > 0f)
            {
                float k = 1f - Mathf.Exp(-_chaseEase * Time.deltaTime);
                Vector3 look = LookPoint(followPoint, warp, rotation);
                if (!_easedLookValid) { _easedLook = look; _easedLookValid = true; }
                transform.position = Vector3.Lerp(transform.position, desiredPos, k);
                _easedLook = Vector3.Lerp(_easedLook, look, k);
                _velocity = Vector3.zero;
                if (SafeLookRotation.TryGet(_easedLook - transform.position, frameUp, out var easedRot, this, logError: false))
                    transform.rotation = easedRot;
                _lastTargetPos = followPoint;
                ApplySpectateBlend();
                TickCarry(followPoint);
                PublishCarryToCorridor();
                ApplyShake();
                ApplyFeel(followPoint, warp);
                return;
            }

            float fwd = Vector3.Dot(shipDelta, _followTarget.forward);
            float lat = Vector3.Dot(shipDelta, _followTarget.right);

            // How lateral the ship's motion is (0 = pure forward, 1 = pure strafe), LOW-PASS FILTERED so
            // it can't flip frame-to-frame. The old code hard-SNAPPED the camera when |lat| > |fwd| and
            // SMOOTHED otherwise; on an agile, banking vessel (Manta) whose lateral ≈ forward motion that
            // binary flipped every few frames, alternating instant vs lagged position+rotation = visible
            // jitter. Here we blend the responsiveness CONTINUOUSLY (snappier on strafes, smoother on
            // forward) so there is no discontinuity to stutter on.
            float rawDominance = Mathf.Abs(lat) / (Mathf.Abs(fwd) + Mathf.Abs(lat) + 1e-4f);
            _lateralDominance = Mathf.Lerp(_lateralDominance, rawDominance, 1f - Mathf.Exp(-10f * Time.deltaTime));

            if (_disableRotationLerp)
            {
                // Hard-attached camera (no smoothing) - consistent every frame, so it never jitters.
                transform.position = desiredPos;
                _velocity = Vector3.zero;
            }
            else
            {
                // SmoothDamp stays continuous (velocity preserved); just shorten its time constant as the
                // motion gets more lateral so the camera keeps up with strafes without ever jumping.
                float posSmoothTime = Mathf.Lerp(_followSmoothTime, _followSmoothTime * 0.1f, _lateralDominance);
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPos, ref _velocity, Mathf.Max(1e-4f, posSmoothTime)
                );
            }

            if (!SafeLookRotation.TryGet(LookPoint(followPoint, warp, rotation) - transform.position, frameUp, out var targetRot, this, logError: false))
                targetRot = transform.rotation;

            if (_disableRotationLerp)
            {
                transform.rotation = targetRot;
            }
            else
            {
                // Blend the Slerp factor from the smooth base toward instant (1) as motion gets lateral -
                // continuous, so no snap/smooth flip. This is the main fix for the Manta rotation jitter.
                float baseT = 1f - Mathf.Exp(-_rotationSmoothTime * Time.deltaTime);
                float t = Mathf.Lerp(baseT, 1f, _lateralDominance);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            _lastTargetPos = followPoint;

            ApplySpectateBlend();

            // After the pose is settled and BEFORE shake: the hand-over moves the settled pose,
            // and the shake is a decoration on top of whichever side of the portal that is.
            TickCarry(followPoint);
            PublishCarryToCorridor();

            ApplyShake();
            ApplyFeel(followPoint, warp);
        }

        /// <summary>
        /// Add the camera feel (<c>Docs/CAMERA_FEEL.md</c>) on top of the settled pose: last, after the spectate
        /// blend, the portal hand-over and the shake. Off during a placement (the pilot is aiming at a point
        /// across the cell), faded out under a spectate vantage, and scaled by the player's camera-shake setting.
        /// </summary>
        private void ApplyFeel(Vector3 followPoint, float warp)
        {
            var config = CameraFeelConfigSO.Instance;
            float scale = config.Enabled && !PlacementAnchor.HasValue
                ? config.Strength * VesselCameraFeel.AccessibilityScale * (1f - Mathf.SmoothStep(0f, 1f, _spectateBlend))
                : 0f;
            if (scale <= 0f)
            {
                Feel.Reset();
                return;
            }

            // Read every frame rather than cached, so a retuned table shows live in play mode. The hull is measured
            // once per follow (a few tries while its model comes up); a bigger hull's pilot feels less G, slower.
            float tableG = 1f;
            if (_feelVesselBody)
            {
                (tableG, _feelWeight) = config.ProfileFor(_feelVessel.VesselType);
                if (!(_feelHullSize > 0f) && _feelHullTries < MaxHullMeasureTries)
                {
                    _feelHullTries++;
                    _feelHullSize = MeasureHullSize(_feelVesselBody.transform);
                }
            }
            else _feelWeight = 1f;
            var (sizeG, response) = config.SizeFeel(_feelHullSize);
            _feelGForce = tableG * sizeG;
            _feelResponse = response;

            Quaternion view = transform.rotation;
            float distance = Mathf.Max(1f, EffectiveOffset.magnitude * warp);
            Feel.Step(config, Time.deltaTime, followPoint, view, distance, _feelGForce, _feelResponse,
                DriftSlip(config, view));

            _feelPosition = view * (Feel.LocalOffset * (distance * scale));
            _feelRotation = Quaternion.Euler(Feel.LocalAngles * scale);
            transform.SetPositionAndRotation(transform.position + _feelPosition, view * _feelRotation);
            _feelApplied = true;
        }

        const int MaxHullMeasureTries = 30;
        static readonly List<Renderer> s_hullRenderers = new(32);

        /// <summary>
        /// How big a hull is: the mean of the length, width and height of the box around its model's meshes, in the
        /// hull's own orientation (so it does not change with the way the hull points) and in world units (so a
        /// scaled prefab measures its real size). The skimmer's visuals are not hull, and nor are trails, lines or
        /// particles. 0 when there is no model to measure yet. Runs once per follow target, never per frame.
        /// </summary>
        public static float MeasureHullSize(Transform root)
        {
            if (!root) return 0f;
            root.GetComponentsInChildren(false, s_hullRenderers);
            Quaternion toHull = Quaternion.Inverse(root.rotation);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            bool any = false;
            for (int i = 0; i < s_hullRenderers.Count; i++)
            {
                var r = s_hullRenderers[i];
                if (!r.enabled) continue;
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : r is MeshRenderer && r.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh
                    : null;
                if (!mesh || r.GetComponentInParent<SkimmerImpactor>(true)) continue;
                Bounds b = mesh.bounds;
                Transform t = r.transform;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = toHull * (t.TransformPoint(local) - root.position);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                any = true;
            }
            s_hullRenderers.Clear();
            if (!any) return 0f;
            Vector3 size = max - min;
            return (size.x + size.y + size.z) / 3f;
        }

        /// <summary>Take last frame's feel back off, leaving the clean pose (see <see cref="ApplyFeel"/>).</summary>
        private void UndoFeel()
        {
            if (!_feelApplied) return;
            _feelApplied = false;
            transform.SetPositionAndRotation(transform.position - _feelPosition,
                transform.rotation * Quaternion.Inverse(_feelRotation));
        }

        /// <summary>
        /// The followed hull's drift, -1..1: the side of the view its travel (<c>Course</c>) slips toward, as a share
        /// of <see cref="CameraFeelConfigSO.DriftFullSlipDegrees"/>. 0 when it is not drifting.
        /// </summary>
        private float DriftSlip(CameraFeelConfigSO config, Quaternion view)
        {
            if (!_feelVesselBody || !_feelVessel.IsDrifting) return 0f;
            Vector3 course = _feelVessel.Course;
            if (course.sqrMagnitude < 1e-6f) return 0f;
            float side = Vector3.Dot(course.normalized, view * Vector3.right);
            return Mathf.Clamp(side / Mathf.Sin(config.DriftFullSlipDegrees * Mathf.Deg2Rad), -1f, 1f);
        }

        /// <summary>
        /// Blend the settled follow pose toward the <see cref="Spectate"/> vantage. A no-op while
        /// no vantage is set and the blend has decayed to zero, which is every frame for every
        /// vessel but a spinning Thresher.
        /// </summary>
        private void ApplySpectateBlend()
        {
            float target = _spectate.HasValue ? 1f : 0f;
            if (_spectateBlend <= 0f && target <= 0f) return;

            float step = SpectateBlendSeconds > 0f ? Time.unscaledDeltaTime / SpectateBlendSeconds : 1f;
            _spectateBlend = Mathf.MoveTowards(_spectateBlend, target, step);
            if (_spectateBlend <= 0f) return;

            _followPosePosition = transform.position;
            _followPoseRotation = transform.rotation;

            var view = _lastSpectate;
            float w = Mathf.SmoothStep(0f, 1f, _spectateBlend);
            Vector3 up = view.Up.sqrMagnitude > 1e-6f ? view.Up : Vector3.up;
            if (!SafeLookRotation.TryGet(view.LookAt - view.Position, up, out var spectateRot, this, logError: false))
                spectateRot = transform.rotation;
            transform.position = Vector3.Lerp(transform.position, view.Position, w);
            transform.rotation = Quaternion.Slerp(transform.rotation, spectateRot, w);
        }

        /// <summary>
        /// Decaying random displacement, applied after the pose is settled. Factored out so the
        /// first-person path — which writes its own pose and returns early — still recoils.
        /// </summary>
        private void ApplyShake()
        {
            if (_shakeTimeRemaining <= 0f) return;

            _shakeTimeRemaining -= Time.unscaledDeltaTime;
            float decay = Mathf.Clamp01(_shakeTimeRemaining / _shakeDuration);
            // Perlin-based shake for smoother motion than pure random. ~10 Hz reads as a weighty
            // "thud" rather than the ~25 Hz buzz that looked like high-frequency jitter.
            const float shakeFreq = 10f;
            float t = Time.unscaledTime * shakeFreq;
            float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f;
            // The player's camera-shake setting scales this too (1, the default, leaves it as tuned).
            transform.position += new Vector3(x, y, z) * (_shakeIntensity * decay * VesselCameraFeel.AccessibilityScale);
        }

        public void ApplySettings(CameraSettingsSO settings)
        {
            _currentSettings = settings;
            if (!_currentSettings) return;

            var flags = _currentSettings.mode;

            Camera.nearClipPlane = _currentSettings.nearClipPlane;
            Camera.farClipPlane = _currentSettings.farClipPlane;
            _lookAhead = _currentSettings.lookAheadDistance;
            _lookLift = _currentSettings.lookAheadLift;
            _chaseEase = Mathf.Max(0f, _currentSettings.chaseEaseRate);
            _framingFov = Mathf.Max(0f, _currentSettings.framingFieldOfView);
            _easedLookValid = false;

            if (flags.HasFlag(CameraMode.DynamicCamera))
            {
                _followOffset.x = settings.followOffset.x;
                _followOffset.y = settings.followOffset.y;

                _followSmoothTime = settings.followSmoothTime;
                _rotationSmoothTime = settings.rotationSmoothTime;
                _disableRotationLerp = settings.disableSmoothing;

                SetCameraDistance(settings.dynamicMinDistance);
            }
            else
            {
                _followOffset = settings.followOffset;
                _disableRotationLerp = true;
                adaptiveZoomEnabled = settings.enableAdaptiveZoom;
                _neutralOffsetZ = _followOffset.z;
            }
        }

        public void SetFollowTarget(Transform target)
        {
            // A carry, a height scale and a spectate vantage belong to the ship they were started for.
            if (target != _followTarget)
            {
                CancelCarry();
                _followHeightScale = 1f;
                _surroundFraming01 = 0f;
                _surroundDistanceScale = 1f;
                _vesselDistanceScale = 1f;
                _spectate = null;
                _spectateBlend = 0f;
                MaxFollowTurnRate = 0f;
                MaxFollowAngularAcceleration = 0f;
                _followFrameValid = false;
                // Hand the pose over clean: a new owner (the replay camera poses this rig by hand) must never
                // have last frame's feel subtracted from a pose it wrote itself.
                UndoFeel();
                Feel.Reset();
            }
            // The vessel whose skims, hits and shots this camera feels (VesselCameraFeel): the follow target's own.
            if (target != _followTarget || (target && !_feelVesselBody))
            {
                _feelVessel = target ? target.GetComponentInParent<IVesselStatus>() : null;
                _feelVesselBody = _feelVessel as Component;
                _feelHullSize = 0f;
                _feelHullTries = 0;
            }

            // Remember WHO took the target away, so a frozen camera can name its cause instead of
            // being diagnosed by reading every caller in the project (see RecoverLostFollowTarget).
            // Captured only on a real loss - a null over a live target - so this costs nothing on
            // the ordinary path, which only ever hands the camera a vessel.
            if (!target && _followTarget)
                _followTargetClearedBy = System.Environment.StackTrace;
            if (target) { _reportedLostFollowTarget = false; _followTargetClearedBy = null; }

            _followTarget = target;
            _lastTargetPos = Vector3.zero;
            _velocity = Vector3.zero;
        }

        // --- Lost follow target -----------------------------------------------------------------
        //
        // The camera frames nothing without a follow target - UpdateCamera returns on the first
        // line - so losing it while the PLAYER's rig is on screen freezes the view where it is
        // while the ship flies on, and nothing re-points it until some later SetupGamePlayCameras
        // (in Menu_Main, the next freestyle entry). That is the whole of "the camera stopped
        // following me; I had to go to the menu and back". It has two shapes and both are covered:
        // a caller handed this rig a NULL target (CameraManager.EndWindowedPlayerCamera did, for
        // a loan that was never taken), or the Transform it followed was DESTROYED under it (a
        // vessel swapped or despawned without the rig being told).
        string _followTargetClearedBy;
        bool _reportedLostFollowTarget;

        /// <summary>
        /// The player's rig has no live follow target while it is the camera on screen: say so
        /// ONCE, with whoever cleared it, and latch back onto the player's own ship if
        /// <see cref="CameraManager"/> still knows a live one. Only the player rig, and only while
        /// it is the active camera - the replay camera clears its target on purpose
        /// (<c>CameraManager.BeginManualReplayCamera</c>) and is posed by hand.
        /// </summary>
        private void RecoverLostFollowTarget()
        {
            var manager = CameraManager.Instance;
            if (manager == null) return;
            if (manager.GetCloseCamera() != transform) return;
            if (!ReferenceEquals(manager.GetActiveController(), this)) return;

            var ship = manager.PlayerFollowTarget;
            bool canRecover = ship && ship.gameObject.activeInHierarchy;

            if (!_reportedLostFollowTarget)
            {
                _reportedLostFollowTarget = true;
                string cause = _followTargetClearedBy != null
                    ? "It was handed a NULL follow target by:\n" + _followTargetClearedBy
                    : "The Transform it was following was DESTROYED without the camera being " +
                      "re-pointed.";
                CSDebug.LogWarning(
                    "[CustomCameraController] The player camera lost its follow target while on " +
                    "screen, so it stopped following the ship. " +
                    (canRecover ? $"Re-latching onto '{ship.name}'. " : "No live player vessel to re-latch onto. ") +
                    cause, this);
            }

            if (!canRecover) return;
            SetFollowTarget(ship);
            SnapToTarget();
        }

        /// <summary>
        /// Immediately positions the camera at the correct follow offset from the target,
        /// clearing all smoothing state. Call after configuring settings and follow target.
        /// </summary>
        public void SnapToTarget()
        {
            if (!_followTarget) return;

            Vector3 followPoint = FollowPoint;
            _followFrameValid = false;
            transform.position = followPoint + _followTarget.rotation * EffectiveOffset;

            _easedLook = LookPoint(followPoint, 1f, _followTarget.rotation);
            _easedLookValid = true;
            if (SafeLookRotation.TryGet(_easedLook - transform.position, _followTarget.up, out var targetRot, this, logError: false))
                transform.rotation = targetRot;

            _lastTargetPos = followPoint;
            _velocity = Vector3.zero;

            // The pose was just written whole, so there is no feel on it to take off, and a snap is a cut.
            _feelApplied = false;
            Feel.Reset();
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            if (!_currentSettings) return;
            
            Camera.nearClipPlane = _currentSettings.nearClipPlane;
            Camera.farClipPlane = _currentSettings.farClipPlane;
        }

        public void Deactivate() => gameObject.SetActive(false);

        /// <summary>The camera this controller drives, resolved on first use. Not only in Awake: in edit mode (the
        /// edit-mode camera tests) AddComponent does not run Awake, and ApplySettings then read a null camera.</summary>
        public Camera Camera => _camera ? _camera : (_camera = GetComponent<Camera>());
        private Camera _camera;

        /// <summary>
        /// Sets the distance (Z) behind the target. Always negative.
        /// </summary>
        public void SetCameraDistance(float distance)
        {
            if (_distanceLerpRoutine != null)
            {
                StopCoroutine(_distanceLerpRoutine);
                _distanceLerpRoutine = null;
            }

            _followOffset.z = distance;
        }

        /// <summary>
        /// Gets the current distance (absolute value).
        /// </summary>
        public float GetCameraDistance() => _followOffset.z;

        public float NeutralOffsetZ => _neutralOffsetZ;
        public float ZoomSmoothTime { get; } = 0.2f;
    
        public bool AdaptiveZoomEnabled => adaptiveZoomEnabled;

        /// <summary>
        /// Rarely used override to set full offset directly.
        /// </summary>
        public void SetFollowOffset(Vector3 offset)
        {
            _followOffset = offset;
        }

        /// <summary>
        /// Returns the current full offset vector.
        /// </summary>
        public Vector3 GetFollowOffset() => _followOffset;

        /// <summary>
        /// Switches to orthographic view if requested.
        /// </summary>
        public void SetOrthographic(bool ortho, float size)
        {
            // ortho-marks: the game camera draws the world and no 2D marks of its own; OrthographicLens keeps its Panini off
            Camera.orthographic = ortho;
            if (ortho) Camera.orthographicSize = size;
        }

        /// <summary>
        /// Triggers a decaying camera shake. Subsequent calls override the current shake
        /// only if the new intensity is stronger.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;

            // Only override if this shake is stronger than what's already playing
            if (_shakeTimeRemaining > 0f && intensity < _shakeIntensity * (_shakeTimeRemaining / _shakeDuration))
                return;

            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimeRemaining = duration;
        }
    }
}
