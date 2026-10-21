using CosmicShore.Data;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Starlings' "flight" (design: <c>R_VesselActions/STARLING.md</c> §1). The Starlings is a swarm, so there is nothing to
    /// fly: the hull is the VIEW, a carrier that sits on the swarm's centre and that the pilot turns, zooms and rolls. Its
    /// position eases onto <see cref="FollowTarget"/>, which <see cref="StarlingSwarmExecutor"/> writes every frame.
    ///
    /// <para><b>The controls</b> are the fleet's stick mix (Garrett, 2026-10-21): the sums turn the view (yaw about its own up,
    /// pitch about its own right, stopping the moment the sticks are let go - the lab's <c>turnView</c>), <b>x-diff zooms</b>
    /// (sticks spread apart = in, squeezed = out) and <b>y-diff rolls</b> the camera about the view's axis. On a phone one
    /// thumb turns the view, and two fingers <b>pinch</b> to zoom and <b>spin</b> to roll, 1:1 with the fingers; while two
    /// fingers are down the view does not turn. The numbers are <see cref="StarlingConfigSO"/>'s, pushed by the executor.</para>
    ///
    /// <para>Its reported speed is always 0: the carrier must lay no trail (<see cref="VesselPrismController"/> lays only
    /// above 3 u/s) and must not drive the speed-tunnel FOV. The camera is written only while this hull is the camera's
    /// target and only by the local pilot, and handed back to its authored distance only if nobody else moved it since
    /// (the Shrike's and Thresher's rule).</para>
    /// </summary>
    public class StarlingVesselTransformer : SingleStickVesselTransformer
    {
        [Header("Starling view")]
        [Tooltip("Turn rate of the viewing plane at full stick (deg/s). StarlingConfigSO.viewTurnRate overrides it when the " +
                 "executor is wired; this is the fallback.")]
        [SerializeField, Min(1f)] float viewTurnRate = 90f;
        [Tooltip("How fast the view's centre eases onto the swarm (1/s). StarlingConfigSO.followRate overrides it.")]
        [SerializeField, Min(0.01f)] float followRate = 3f;

        StarlingConfigSO _controls;
        CustomCameraController _cam;
        float _camWritten;
        bool _camEngaged;
        bool _pinching;
        Vector2 _pinchVector;

        /// <summary>Where the view's centre should be (the swarm's sampled centroid). Written by the executor only.</summary>
        public Vector3? FollowTarget { get; set; }

        /// <summary>Set by the executor from its config so the lab owns the turn and follow numbers, and the config the zoom
        /// and roll ones.</summary>
        public void Configure(StarlingConfigSO config)
        {
            if (!config) return;
            _controls = config;
            viewTurnRate = Mathf.Max(1f, config.ViewTurnRate);
            followRate = Mathf.Max(0.01f, config.FollowRate);
        }

        protected override void RotateShip()
        {
            float dt = Time.deltaTime;
            Quaternion rotation = transform.rotation;
            float zoomAxis = 0f, rollDegrees = 0f, pinchRatio = 0f;

            if (TwoFingers(out var a, out var b))
            {
                // A phone: pinch zooms and spin rolls, 1:1 with the fingers. The view does not turn meanwhile.
                Vector2 v = b - a;
                if (_pinching)
                {
                    pinchRatio = v.magnitude > 1e-3f ? _pinchVector.magnitude / v.magnitude : 0f;
                    rollDegrees = StarlingPilotControls.Spun(_pinchVector.x, _pinchVector.y, v.x, v.y);
                }
                _pinching = true;
                _pinchVector = v;
            }
            else
            {
                _pinching = false;
                if (InputStatus != null)
                {
                    float step = viewTurnRate * dt;
                    float yaw = InputStatus.XSum, pitch = InputStatus.YSum;
                    if (Mathf.Abs(yaw) > 1e-3f) rotation = Quaternion.AngleAxis(yaw * step, rotation * Vector3.up) * rotation;
                    if (Mathf.Abs(pitch) > 1e-3f) rotation = Quaternion.AngleAxis(pitch * step, rotation * Vector3.right) * rotation;

                    // x-diff and y-diff are thumb SPREAD on a phone, which the pinch above already reads (and which the
                    // touch mix holds after a thumb lifts), so only a pad, keyboard or mouse drives them as rates.
                    if (_controls && InputStatus.ActiveInputDevice != InputDeviceType.Touch)
                    {
                        zoomAxis = StarlingPilotControls.SpreadAxis(InputStatus.XDiff, _controls.DiffDeadZone);
                        rollDegrees = StarlingPilotControls.Axis(InputStatus.YDiff, _controls.DiffDeadZone) * _controls.RollRate * dt;
                    }
                }
            }

            if (Mathf.Abs(rollDegrees) > 1e-4f) rotation = Quaternion.AngleAxis(rollDegrees, rotation * Vector3.forward) * rotation;
            transform.rotation = rotation;
            accumulatedRotation = rotation;
            if (VesselStatus != null) VesselStatus.Course = transform.forward;

            UpdateZoom(zoomAxis, pinchRatio, dt);
        }

        /// <summary>Two fingers on the glass that no control owns (an ability card's touch is the card's, FlightTouchClaims).</summary>
        bool TwoFingers(out Vector2 a, out Vector2 b)
        {
            a = b = default;
            if (!EnhancedTouchSupport.enabled || !IsLocalPilot()) return false;
            int found = 0;
            var touches = Touch.activeTouches;
            for (int i = 0; i < touches.Count && found < 2; i++)
            {
                if (FlightTouchClaims.Claims(touches[i].startScreenPosition)) continue;
                if (found == 0) a = touches[i].screenPosition;
                else b = touches[i].screenPosition;
                found++;
            }
            return found == 2;
        }

        bool IsLocalPilot() => VesselStatus != null && VesselStatus.Player != null && VesselStatus.Player.IsLocalPilot
                               && !VesselStatus.AutoPilotEnabled;

        void UpdateZoom(float axis, float pinchRatio, float dt)
        {
            if (!_controls || !IsLocalPilot()) { ReleaseCamera(); return; }
            var cam = CameraManager.Instance ? CameraManager.Instance.GetActiveController() as CustomCameraController : null;
            if (!cam || !cam.FollowTarget || !(cam.FollowTarget == transform || cam.FollowTarget.IsChildOf(transform)))
            {
                ReleaseCamera();
                return;
            }
            _cam = cam;
            float neutral = cam.NeutralOffsetZ;
            if (neutral >= 0f) return;   // not a chase camera
            if (_camEngaged && !Mathf.Approximately(cam.GetCameraDistance(), _camWritten))
                _camEngaged = false;     // somebody else moved it: start from where they left it
            if (Mathf.Abs(axis) < 1e-4f && pinchRatio <= 0f) return;

            float now = cam.GetCameraDistance();
            float next = pinchRatio > 0f
                ? StarlingPilotControls.Pinched(now, neutral, 1f, 1f / pinchRatio, _controls.ZoomNearest, _controls.ZoomFarthest)
                : StarlingPilotControls.Zoomed(now, neutral, axis, _controls.ZoomRate, dt, _controls.ZoomNearest, _controls.ZoomFarthest);
            cam.SetCameraDistance(next);
            _camWritten = next;
            _camEngaged = true;
        }

        void ReleaseCamera()
        {
            if (_camEngaged && _cam && Mathf.Approximately(_cam.GetCameraDistance(), _camWritten))
                _cam.SetCameraDistance(_cam.NeutralOffsetZ);
            _camEngaged = false;
            _cam = null;
        }

        protected override float ComputeThrottleTarget() => 0f;

        protected override void MoveShip() => Follow();

        protected override void MoveRestricted() => Follow();

        void Follow()
        {
            if (VesselStatus != null) VesselStatus.Speed = 0f;
            if (FollowTarget is not { } target) return;
            float k = 1f - Mathf.Exp(-followRate * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
        }

        public override void ResetTransformer()
        {
            FollowTarget = null;
            _pinching = false;
            ReleaseCamera();
            base.ResetTransformer();
        }
    }
}
