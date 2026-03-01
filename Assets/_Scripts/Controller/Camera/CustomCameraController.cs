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
        private CameraSettingsSO _currentSettings;
        private Coroutine _distanceLerpRoutine;
        public bool adaptiveZoomEnabled;
        private float _neutralOffsetZ;

        // --- Transition mode ---
        // When > 0, UpdateCamera forces SmoothDamp (ignoring _disableRotationLerp)
        // with a longer smooth time for a graceful blend from an arbitrary world pose.
        private float _transitionTimeRemaining;
        private float _transitionSmoothTime;

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
            if (!_followTarget) return;

            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = _followTarget.position;

            // During a transition, force SmoothDamp regardless of _disableRotationLerp
            // so the camera glides smoothly from an arbitrary pose to the vessel.
            bool isTransitioning = _transitionTimeRemaining > 0f;
            if (isTransitioning)
                _transitionTimeRemaining -= Time.deltaTime;

            Vector3 desiredPos = _followTarget.position + _followTarget.rotation * _followOffset;
            Vector3 shipDelta = _followTarget.position - _lastTargetPos;
            float fwd = Vector3.Dot(shipDelta, _followTarget.forward);
            float lat = Vector3.Dot(shipDelta, _followTarget.right);

            bool shouldSnap = !isTransitioning
                              && (_disableRotationLerp || Mathf.Abs(lat) > Mathf.Abs(fwd));

            if (shouldSnap)
            {
                transform.position = desiredPos;
                _velocity = Vector3.zero;
            }
            else
            {
                float smoothTime = isTransitioning ? _transitionSmoothTime : _followSmoothTime;
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPos, ref _velocity, smoothTime
                );
            }

            if (!SafeLookRotation.TryGet(_followTarget.position - transform.position, _followTarget.up, out var targetRot, this, logError: false))
                targetRot = transform.rotation;

            if (shouldSnap)
            {
                transform.rotation = targetRot;
            }
            else
            {
                float rotSpeed = isTransitioning
                    ? 1f / Mathf.Max(_transitionSmoothTime, 0.01f)
                    : _rotationSmoothTime;
                float t = 1f - Mathf.Exp(-rotSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            _lastTargetPos = _followTarget.position;
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
            _followTarget = target;
            _lastTargetPos = Vector3.zero;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Immediately positions the camera at the correct follow offset from the target,
        /// clearing all smoothing state. Call after configuring settings and follow target.
        /// </summary>
        public void SnapToTarget()
        {
            if (!_followTarget) return;

            transform.position = _followTarget.position + _followTarget.rotation * _followOffset;

            if (SafeLookRotation.TryGet(_followTarget.position - transform.position, _followTarget.up, out var targetRot, this, logError: false))
                transform.rotation = targetRot;

            _lastTargetPos = _followTarget.position;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Starts a smooth transition from the given world-space pose to the follow target.
        /// During the transition, SmoothDamp is forced regardless of <see cref="_disableRotationLerp"/>
        /// so the camera glides from an arbitrary position (e.g. menu orbit) to the vessel
        /// without any jerk. The target position is recalculated every frame in
        /// <see cref="UpdateCamera"/>, keeping the transition smooth even when the vessel moves.
        /// After the transition, normal UpdateCamera behavior resumes.
        /// </summary>
        public void StartTransitionFromPose(Vector3 position, Quaternion rotation, float duration)
        {
            transform.SetPositionAndRotation(position, rotation);
            _velocity = Vector3.zero;
            _transitionTimeRemaining = duration;
            // SmoothDamp converges ~95% at 3× smoothTime, so use duration/3
            _transitionSmoothTime = duration * 0.33f;
            if (_followTarget)
                _lastTargetPos = _followTarget.position;
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
    }
}
