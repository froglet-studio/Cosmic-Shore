using System.Collections;
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
        /// How much of the follow offset's HEIGHT (y) the camera keeps: 1 = the authored offset,
        /// 0 = level with the vessel, directly behind it. Driven only by the Butterfly's
        /// <c>SpreadWingsActionExecutor</c> — Mass mode drops the camera to directly behind the
        /// hull, Dust mode keeps the authored height (<c>R_VesselActions/BUTTERFLY.md</c> §2.0).
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
        /// The world point the camera frames this frame: the placement anchor if one is set, else
        /// the follow target's own position — seen through a portal the camera has not reached yet
        /// while one is being carried (<see cref="CarryThroughSphere"/>).
        /// </summary>
        private Vector3 FollowPoint =>
            PlacementAnchor ?? (_followTarget ? _followTarget.position - PortalShift : Vector3.zero);

        /// <summary>The transform this camera follows (the vessel's camera follow target).</summary>
        public Transform FollowTarget => _followTarget;

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
            _lastTargetPos += shift;
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
        private Vector3 EffectiveOffset => AuthoredOffset * FramingScale;

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
        /// behind it. Scaled by the warp like the offset, so the framing holds at any warp size.
        /// </summary>
        private Vector3 LookPoint(Vector3 followPoint, float warp)
        {
            if (PlacementAnchor.HasValue || !_followTarget || (_lookAhead == 0f && _lookLift == 0f)) return followPoint;
            float ahead = RearView ? -_lookAhead : _lookAhead;
            Vector3 lookLocal = new(0f, _lookLift * _followHeightScale, ahead);
            float s = FramingScale;
            if (!Mathf.Approximately(s, 1f)) lookLocal = FramedLook(AuthoredOffset, lookLocal, s);
            return followPoint + _followTarget.rotation * (lookLocal * warp);
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

        private void Awake()
        {
            Camera = GetComponent<Camera>();
            Camera.useOcclusionCulling = false;
        }

        private void LateUpdate()
        {
            if (!UseFixedUpdate)
                UpdateCamera();
        }
        
        private void UpdateCamera()
        {
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

            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = followPoint;

            // Warp field (Docs/WARP_FIELD.md): the camera sits at a warp-sized distance and clips
            // at a warp-sized near plane, so a vessel shrinking toward a field's centre keeps the
            // same framing — the near field gives nothing away and the far field grows. Applied at
            // the point of use like FollowHeightScale, never by writing _followOffset, which every
            // settings re-apply and zoom ability owns. Exactly 1 with no field.
            float warp = WarpFieldRuntime.ScaleAt(followPoint);
            ApplyWarpToNearClip(warp);

            Vector3 desiredPos = followPoint + _followTarget.rotation * (EffectiveOffset * warp);
            Vector3 shipDelta = followPoint - _lastTargetPos;

            // Teleport guard: on a kickoff park / fresh spawn the follow target jumps a long way in one
            // frame (normal flight is only a few units/frame). Snap the camera into place instead of
            // SmoothDamping a wild swing across the arena - that swing read as a "wonky, jittery start".
            const float teleportStep = 50f;
            if (shipDelta.sqrMagnitude > teleportStep * teleportStep)
            {
                transform.position = desiredPos;
                if (SafeLookRotation.TryGet(LookPoint(followPoint, warp) - transform.position, _followTarget.up, out var snapRot, this, logError: false))
                    transform.rotation = snapRot;
                _velocity = Vector3.zero;
                _lateralDominance = 0f;
                _lastTargetPos = followPoint;
                _easedLook = LookPoint(followPoint, warp);
                _easedLookValid = true;
                return;
            }

            // An eased chase (the Vessel Studio's: position and look point each close on their target at
            // chaseEaseRate per second, up tied to the hull's), so the hull swings in the frame through a turn
            // instead of sitting nailed to one pixel. 0 on every vessel that does not set it.
            if (_chaseEase > 0f)
            {
                float k = 1f - Mathf.Exp(-_chaseEase * Time.deltaTime);
                Vector3 look = LookPoint(followPoint, warp);
                if (!_easedLookValid) { _easedLook = look; _easedLookValid = true; }
                transform.position = Vector3.Lerp(transform.position, desiredPos, k);
                _easedLook = Vector3.Lerp(_easedLook, look, k);
                _velocity = Vector3.zero;
                if (SafeLookRotation.TryGet(_easedLook - transform.position, _followTarget.up, out var easedRot, this, logError: false))
                    transform.rotation = easedRot;
                _lastTargetPos = followPoint;
                TickCarry(followPoint);
                PublishCarryToCorridor();
                ApplyShake();
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

            if (!SafeLookRotation.TryGet(LookPoint(followPoint, warp) - transform.position, _followTarget.up, out var targetRot, this, logError: false))
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

            // After the pose is settled and BEFORE shake: the hand-over moves the settled pose,
            // and the shake is a decoration on top of whichever side of the portal that is.
            TickCarry(followPoint);
            PublishCarryToCorridor();

            ApplyShake();
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
            transform.position += new Vector3(x, y, z) * (_shakeIntensity * decay);
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
            // A carry and a height scale belong to the ship they were started for.
            if (target != _followTarget)
            {
                CancelCarry();
                _followHeightScale = 1f;
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
            transform.position = followPoint + _followTarget.rotation * EffectiveOffset;

            _easedLook = LookPoint(followPoint, 1f);
            _easedLookValid = true;
            if (SafeLookRotation.TryGet(_easedLook - transform.position, _followTarget.up, out var targetRot, this, logError: false))
                transform.rotation = targetRot;

            _lastTargetPos = followPoint;
            _velocity = Vector3.zero;
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            if (!_currentSettings) return;
            
            Camera.nearClipPlane = _currentSettings.nearClipPlane;
            Camera.farClipPlane = _currentSettings.farClipPlane;
        }

        public void Deactivate() => gameObject.SetActive(false);

        public Camera Camera { get; private set; }

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
