using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs the Rhino shield swipe: pivots the shield capsule about its parent's origin
    /// through the swipe arc (rotation and mount position together, so the sword carves a
    /// real arc through space instead of spinning in place), and tweens a camera rotation
    /// offset for the local pilot that arrives at the stance exactly when the sword settles.
    /// The camera offset is eased here rather than by the controller because FixedCamera
    /// vessels apply rotation unsmoothed. Only scale is driven on this transform elsewhere
    /// (ShieldSkimmerScaleDriver), so rotation/position are ours to animate.
    /// </summary>
    public sealed class ShieldSwipeActionExecutor : ShipActionExecutorBase
    {
        [Header("References")]
        [Tooltip("The shield/sword transform to sweep (the ForceFieldSkimmer root). Falls back to the near-field skimmer.")]
        [SerializeField] Transform shieldRoot;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        IVesselStatus _status;
        CustomCameraController _camera;
        CancellationTokenSource _animCts;

        Vector3 _baseLocalPos;
        Quaternion _baseLocalRot;
        bool _baseCaptured;

        float _yaw;        // current sweep offsets (degrees) in the shield parent's frame
        float _roll;
        float _camYaw;     // current camera offsets (degrees), camera-local
        float _camRoll;
        float _activeSign; // +1 right stance, -1 left stance, 0 idle

        // Which triggers are physically held (set on Begin, cleared on End per direction),
        // so releasing the stance-owning trigger can hand the stance to the other one.
        RhinoShieldSwipeActionSO _heldRightSO;
        RhinoShieldSwipeActionSO _heldLeftSO;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            ResetImmediate();
        }

        void OnTurnEndOfMiniGame() => ResetImmediate();

        void Update()
        {
            if (_activeSign == 0f || _status == null || !_status.IsLocalUser) return;

            // The release edge can be lost mid-hold: a gamepad disconnect swaps input
            // strategies with no release synthesis, and exiting menu freestyle pauses
            // input before the release is processed. Watch the physical trigger for the
            // local pilot and recenter once it is no longer down. Remote peers don't
            // poll - their stance ends via the owner's replicated stop event.
            if (!_status.AutoPilotEnabled && ActiveTriggerStillDown()) return;

            var so = _activeSign > 0f ? _heldRightSO : _heldLeftSO;
            _heldRightSO = null;
            _heldLeftSO = null;
            _activeSign = 0f;

            if (so)
            {
                RestartAnimation(out var token);
                RunReturnAsync(so, token).Forget();
            }
            else
            {
                ResetImmediate();
            }
        }

        bool ActiveTriggerStillDown()
        {
            var input = _status.InputStatus;
            if (input == null) return false;

            // Only trigger-writing devices can have begun a swipe. A strategy swap (pad
            // disconnect -> keyboard) leaves the analogs frozen at their last held value,
            // so a non-trigger device counts as released, not as still-held.
            if (input.ActiveInputDevice is not (InputDeviceType.Gamepad or InputDeviceType.DualMouse))
                return false;

            // Same deadzone the gamepad strategy uses for its press/release edges.
            const float deadzone = 0.05f;
            return _activeSign > 0f
                ? input.RightTriggerAnalog > deadzone
                : input.LeftTriggerAnalog > deadzone;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            ResolveShieldRoot();
            CaptureBasePose();
        }

        public void BeginSwipe(RhinoShieldSwipeActionSO so, IVesselStatus status)
        {
            _status ??= status;
            ResolveShieldRoot();
            if (!shieldRoot || !so) return;
            CaptureBasePose();

            if (so.DirectionSign > 0f) _heldRightSO = so;
            else _heldLeftSO = so;

            _activeSign = so.DirectionSign;

            // The active controller can change between swipes (death cam, replay cam);
            // a stale offset on the previous controller would persist forever.
            var camera = ResolveCamera();
            if (_camera && !ReferenceEquals(_camera, camera))
                _camera.SetRotationOffset(Quaternion.identity);
            _camera = camera;

            RestartAnimation(out var token);
            RunSwipeAsync(so, so.DirectionSign, token).Forget();
        }

        public void EndSwipe(RhinoShieldSwipeActionSO so, IVesselStatus status)
        {
            _status ??= status;
            if (!shieldRoot || !so) return;

            bool isRight = so.DirectionSign > 0f;
            if (isRight) _heldRightSO = null;
            else _heldLeftSO = null;

            // Only the trigger that owns the current stance moves the sword;
            // a release of the other trigger after a cross-swipe is a no-op.
            if (_activeSign == 0f || !Mathf.Approximately(_activeSign, so.DirectionSign)) return;

            // If the opposite trigger is still held it takes the stance back
            // instead of the sword snapping to center under a held trigger.
            var fallback = isRight ? _heldLeftSO : _heldRightSO;
            if (fallback)
            {
                BeginSwipe(fallback, status);
                return;
            }

            _activeSign = 0f;
            RestartAnimation(out var token);
            RunReturnAsync(so, token).Forget();
        }

        async UniTaskVoid RunSwipeAsync(RhinoShieldSwipeActionSO so, float sign, CancellationToken ct)
        {
            try
            {
                float startYaw = _yaw, startRoll = _roll;
                float startCamYaw = _camYaw, startCamRoll = _camRoll;

                // Rightward yaw is positive about up. Counterclockwise roll (from the
                // pilot's seat) is POSITIVE about forward - AngleAxis(+90, forward) maps
                // right to up - so roll takes the same sign as yaw.
                float peakYaw = sign * so.SwipeYawDegrees;
                float peakRoll = sign * so.SwipeRollDegrees;
                float restYaw = sign * so.RestYawDegrees;
                float restRoll = sign * so.RestRollDegrees;
                float camYawTarget = sign * so.CameraYawDegrees;
                float camRollTarget = sign * so.CameraRollDegrees;

                float outDur = Mathf.Max(0.01f, so.SwipeOutSeconds);
                float settleDur = Mathf.Max(0.01f, so.SettleSeconds);
                float total = outDur + settleDur;

                float elapsed = 0f;
                while (elapsed < total)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed < outDur)
                    {
                        float t = SmoothStep01(elapsed / outDur);
                        _yaw = Mathf.Lerp(startYaw, peakYaw, t);
                        _roll = Mathf.Lerp(startRoll, peakRoll, t);
                    }
                    else
                    {
                        float t = SmoothStep01((elapsed - outDur) / settleDur);
                        _yaw = Mathf.Lerp(peakYaw, restYaw, t);
                        _roll = Mathf.Lerp(peakRoll, restRoll, t);
                    }

                    float camT = SmoothStep01(elapsed / total);
                    _camYaw = Mathf.Lerp(startCamYaw, camYawTarget, camT);
                    _camRoll = Mathf.Lerp(startCamRoll, camRollTarget, camT);

                    ApplyShieldPose();
                    ApplyCameraOffset();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                _yaw = restYaw;
                _roll = restRoll;
                _camYaw = camYawTarget;
                _camRoll = camRollTarget;
                ApplyShieldPose();
                ApplyCameraOffset();
            }
            catch (System.OperationCanceledException) { }
        }

        async UniTaskVoid RunReturnAsync(RhinoShieldSwipeActionSO so, CancellationToken ct)
        {
            try
            {
                float startYaw = _yaw, startRoll = _roll;
                float startCamYaw = _camYaw, startCamRoll = _camRoll;
                float duration = Mathf.Max(0.01f, so.ReturnSeconds);

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = SmoothStep01(elapsed / duration);
                    _yaw = Mathf.Lerp(startYaw, 0f, t);
                    _roll = Mathf.Lerp(startRoll, 0f, t);
                    _camYaw = Mathf.Lerp(startCamYaw, 0f, t);
                    _camRoll = Mathf.Lerp(startCamRoll, 0f, t);

                    ApplyShieldPose();
                    ApplyCameraOffset();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                _yaw = 0f;
                _roll = 0f;
                _camYaw = 0f;
                _camRoll = 0f;
                ApplyShieldPose();
                ClearCameraOffset();
            }
            catch (System.OperationCanceledException) { }
        }

        void RestartAnimation(out CancellationToken token)
        {
            _animCts?.Cancel();
            _animCts?.Dispose();
            _animCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            token = _animCts.Token;
        }

        void ApplyShieldPose()
        {
            if (!shieldRoot) return;
            var sweep = Quaternion.AngleAxis(_yaw, Vector3.up) * Quaternion.AngleAxis(_roll, Vector3.forward);
            shieldRoot.localRotation = sweep * _baseLocalRot;
            shieldRoot.localPosition = sweep * _baseLocalPos;
        }

        void ApplyCameraOffset()
        {
            if (!_camera) return;
            _camera.SetRotationOffset(
                Quaternion.AngleAxis(_camYaw, Vector3.up) * Quaternion.AngleAxis(_camRoll, Vector3.forward));
        }

        void ClearCameraOffset()
        {
            if (_camera) _camera.SetRotationOffset(Quaternion.identity);
            _camera = null;
        }

        CustomCameraController ResolveCamera()
        {
            // Actions replay on every peer; the sword sweep must run everywhere (the
            // capsule is a live collider) but only the local pilot's camera may move.
            if (_status == null || !_status.IsLocalUser || _status.AutoPilotEnabled) return null;
            if (!IsLocalCameraTarget()) return null;
            return CameraManager.Instance?.GetActiveController() as CustomCameraController;
        }

        bool IsLocalCameraTarget()
        {
            if (_status == null) return false;

            var cm = CameraManager.Instance;
            if (cm == null) return false;

            var follow = cm.PlayerFollowTarget;
            var shipTransform = _status.ShipTransform;
            var cameraTarget = _status.CameraFollowTarget ?? shipTransform;
            return follow == shipTransform || follow == cameraTarget;
        }

        void ResolveShieldRoot()
        {
            if (!shieldRoot && _status?.NearFieldSkimmer)
                shieldRoot = _status.NearFieldSkimmer.transform;
        }

        void CaptureBasePose()
        {
            if (_baseCaptured || !shieldRoot) return;
            _baseLocalPos = shieldRoot.localPosition;
            _baseLocalRot = shieldRoot.localRotation;
            _baseCaptured = true;
        }

        // Never leave a half-applied swipe behind (pooling / vessel swap / turn end).
        void ResetImmediate()
        {
            _animCts?.Cancel();
            _animCts?.Dispose();
            _animCts = null;

            _yaw = 0f;
            _roll = 0f;
            _camYaw = 0f;
            _camRoll = 0f;
            _activeSign = 0f;
            _heldRightSO = null;
            _heldLeftSO = null;

            if (_baseCaptured && shieldRoot)
            {
                shieldRoot.localRotation = _baseLocalRot;
                shieldRoot.localPosition = _baseLocalPos;
            }
            ClearCameraOffset();
        }

        static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
