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
        /// The world point the camera frames this frame: the placement anchor if one is set, else
        /// the follow target's own position — seen through a portal the camera has not reached yet
        /// while one is being carried (<see cref="CarryThroughPortal"/>).
        /// </summary>
        private Vector3 FollowPoint =>
            PlacementAnchor ?? (_followTarget ? _followTarget.position - PortalShift : Vector3.zero);

        /// <summary>The transform this camera follows (the vessel's camera follow target).</summary>
        public Transform FollowTarget => _followTarget;

        // --- Portal carry ---------------------------------------------------------------------
        //
        // A portal moves the SHIP the instant it crosses, but a chase camera is tens to hundreds
        // of units behind it (the Butterfly's is 207). Moving the camera on the same frame is a
        // cut — the whole picture changes around a ship that has not visibly moved — and letting
        // the teleport guard snap it is the same cut with the smoothing state thrown away too.
        // So the camera follows the ship THROUGH the portal instead: it keeps framing where the
        // ship WOULD be had the two mouths been one (the ship's position mapped back through the
        // pair), and it is itself moved across on the frame IT reaches the near mouth. Until
        // then the pilot sees their own ship through the gate's window (FoldGatePortalView),
        // which is rendered from exactly the far-side vantage this camera is about to take — so
        // the hand-over is a change of frame with nothing on screen to show it.
        private bool _carrying;
        private Vector3 _carryShift;          // far mouth - near mouth, the portal's translation
        private Vector3 _carryCentre;         // the near mouth
        private Vector3 _carryNormal;         // the near plane's normal, pointing to the exit side
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
        /// The ship this camera follows has just been carried through a portal from the mouth at
        /// <paramref name="nearCentre"/> to one displaced by <paramref name="shift"/>. Follow it
        /// through rather than cutting.
        ///
        /// <para><b>Identity-guarded</b>: a camera that is not following
        /// <paramref name="subject"/> (or one of its children) ignores the call, so a transit may
        /// ask every camera without knowing which one is the player's. Returns whether this
        /// camera took the carry.</para>
        ///
        /// <para>A camera already on the exit side of the mouth — the rear view sits AHEAD of the
        /// ship, so it went through first — is simply moved across now, which is the same
        /// hand-over one frame earlier.</para>
        /// </summary>
        public bool CarryThroughPortal(Transform subject, Vector3 nearCentre, Vector3 exitNormal,
                                       float mouthRadius, Vector3 shift)
        {
            if (!_followTarget || !subject) return false;
            if (_followTarget != subject && !_followTarget.IsChildOf(subject)) return false;
            if (shift.sqrMagnitude < 1e-6f) return false;

            // A carry already in flight is finished first: two portals in a row compose, and the
            // camera must be in the first one's far frame before it can follow the ship into the
            // second.
            if (_carrying) FinishCarry();

            // A placement moves the framed point to an explicit world position; there is no ship
            // for the camera to trail through the mouth, so hand it across outright.
            if (PlacementAnchor.HasValue) { ShiftCamera(shift); return true; }

            _carryShift = shift;
            _carryCentre = nearCentre;
            _carryNormal = exitNormal.sqrMagnitude > 1e-6f ? exitNormal.normalized : Vector3.forward;
            _carryRadius = Mathf.Max(0.01f, mouthRadius);
            _carryDeadline = Time.time + MaxCarrySeconds;
            _carrying = true;

            // Already through (rear view, or a camera that sits level with the ship): move now.
            if (CameraHasCrossed()) FinishCarry();
            else PublishCarryToCorridor();
            return true;
        }

        private bool CameraHasCrossed()
        {
            // "At the plane" counts as through: a camera within its own near clip of the mouth
            // would clip the window it is looking through and show the near side for a frame.
            float nearClip = Camera ? Camera.nearClipPlane : 0.3f;
            return Vector3.Dot(transform.position - _carryCentre, _carryNormal) >= -nearClip * 2f;
        }

        /// <summary>
        /// Would the pilot still see their ship THROUGH the mouth from here? The line from the
        /// camera to where the ship is framed must pierce the near plane inside the ring. Once it
        /// does not — the ship turned hard, or went through near the rim and the camera is
        /// trailing wide — continuing the carry would leave the pilot looking at a ring with no
        /// ship in it, so the camera is handed across instead.
        /// </summary>
        private bool ShipVisibleThroughMouth(Vector3 framed)
        {
            float dCam = Vector3.Dot(transform.position - _carryCentre, _carryNormal);
            float dShip = Vector3.Dot(framed - _carryCentre, _carryNormal);
            if (dShip <= 0f) return true;                    // ship not yet beyond - nothing to lose
            if (dCam >= 0f) return true;                     // camera through - handled elsewhere
            float t = dCam / (dCam - dShip);
            Vector3 pierce = Vector3.Lerp(transform.position, framed, t);
            Vector3 rel = pierce - _carryCentre;
            Vector3 lateral = rel - Vector3.Dot(rel, _carryNormal) * _carryNormal;
            return lateral.sqrMagnitude <= _carryRadius * _carryRadius;
        }

        private void TickCarry(Vector3 framed)
        {
            if (!_carrying) return;
            if (CameraHasCrossed() || !ShipVisibleThroughMouth(framed) || Time.time > _carryDeadline)
                FinishCarry();
        }

        /// <summary>
        /// Move the camera through the portal: its pose and its smoothing state together, by the
        /// portal's own translation. The pair shares one axis, so the map has no rotation — the
        /// camera keeps its orientation and its SmoothDamp velocity exactly, which is what makes
        /// the frame after the hand-over continue the frame before it.
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
        /// The offset actually used to pose the camera this frame: the authored one, or its
        /// z-mirror while <see cref="RearView"/> is set. Mirroring z alone — not x, not y — is
        /// what puts the camera directly ahead at the same distance and the same height, rather
        /// than at some reflected vantage the vessel's settings never described.
        ///
        /// <para>There was briefly a FIRST-PERSON vantage here too, for the Serpent's scope. It
        /// is retired: a magnified cockpit view read as nauseating, so the scope's magnification
        /// moved into its own window and this camera went back to doing one thing
        /// (<c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 4). A future cockpit would be a
        /// third case here, not a revival of a flag nothing was setting.</para>
        /// </summary>
        private Vector3 EffectiveOffset =>
            RearView && !PlacementAnchor.HasValue
                ? new Vector3(_followOffset.x, _followOffset.y, -_followOffset.z)
                : _followOffset;

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

            Vector3 desiredPos = followPoint + _followTarget.rotation * EffectiveOffset;
            Vector3 shipDelta = followPoint - _lastTargetPos;

            // Teleport guard: on a kickoff park / fresh spawn the follow target jumps a long way in one
            // frame (normal flight is only a few units/frame). Snap the camera into place instead of
            // SmoothDamping a wild swing across the arena - that swing read as a "wonky, jittery start".
            const float teleportStep = 50f;
            if (shipDelta.sqrMagnitude > teleportStep * teleportStep)
            {
                transform.position = desiredPos;
                if (SafeLookRotation.TryGet(followPoint - transform.position, _followTarget.up, out var snapRot, this, logError: false))
                    transform.rotation = snapRot;
                _velocity = Vector3.zero;
                _lateralDominance = 0f;
                _lastTargetPos = followPoint;
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

            if (!SafeLookRotation.TryGet(followPoint - transform.position, _followTarget.up, out var targetRot, this, logError: false))
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
            // A carry belongs to the ship it was started for.
            if (target != _followTarget) CancelCarry();

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

            if (SafeLookRotation.TryGet(followPoint - transform.position, _followTarget.up, out var targetRot, this, logError: false))
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
