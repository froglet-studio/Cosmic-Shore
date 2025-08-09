using System.Collections;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game.CameraSystem
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
            if (_followTarget == null) return;

            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = _followTarget.position;

            Vector3 desiredPos = _followTarget.position + _followTarget.rotation * _followOffset;
            Vector3 shipDelta = _followTarget.position - _lastTargetPos;
            float fwd = Vector3.Dot(shipDelta, _followTarget.forward);
            float lat = Vector3.Dot(shipDelta, _followTarget.right);

            if (_disableRotationLerp)
            {
                transform.position = desiredPos;
                _velocity = Vector3.zero;
            }
            else
            {
                if (Mathf.Abs(lat) > Mathf.Abs(fwd))
                {
                    transform.position = desiredPos;
                    _velocity = Vector3.zero;
                }
                else
                {
                    transform.position = Vector3.SmoothDamp(
                        transform.position, desiredPos, ref _velocity, _followSmoothTime
                    );
                }
            }

            Quaternion targetRot = Quaternion.LookRotation(
                _followTarget.position - transform.position,
                _followTarget.up
            );

            if (_disableRotationLerp || Mathf.Abs(lat) > Mathf.Abs(fwd))
            {
                transform.rotation = targetRot;
            }
            else
            {
                float t = 1f - Mathf.Exp(-_rotationSmoothTime * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            _lastTargetPos = _followTarget.position;
        }

        public void ApplySettings(CameraSettingsSO settings)
        {
            _currentSettings = settings;
            if (_currentSettings == null) return;

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
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            if (_currentSettings == null) return;
            
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
