using UnityEngine;
using CosmicShore.Game.CameraSystem;

namespace CosmicShore.Game.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour, ICameraController
    {
        // Configuration Fields (populated by ApplySettings)

        private Transform followTarget;
        private Vector3  followOffset        = new Vector3(0f, 10f, -20f);
        private float    followSmoothTime    = 0.2f;
        private float    rotationSmoothTime  = 5f;
        private bool     disableRotationLerp = false;
        private bool     useFixedUpdate      = false;

        //Runtime State
        private Camera             cachedCamera;
        private Vector3            velocity;
        private Vector3            _lastTargetPos;
        private CameraSettingsSO   currentSettings;  

        void Awake()
        {
            cachedCamera = GetComponent<Camera>();
            cachedCamera.useOcclusionCulling = false;
        }

        void LateUpdate()
        {
            if (!useFixedUpdate)
                UpdateCamera();
        }

        void FixedUpdate()
        {
            if (useFixedUpdate)
                UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (followTarget == null) return;

            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = followTarget.position;

            // 1) Position
            Vector3 desiredPos = followTarget.position + followTarget.rotation * followOffset;
            Vector3 shipDelta  = followTarget.position - _lastTargetPos;
            float   fwd        = Vector3.Dot(shipDelta, followTarget.forward);
            float   lat        = Vector3.Dot(shipDelta, followTarget.right);

            if (Mathf.Abs(lat) > Mathf.Abs(fwd))
            {
                transform.position = desiredPos;
                velocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPos, ref velocity, followSmoothTime
                );
            }

            // 2) Rotation
            Quaternion targetRot = Quaternion.LookRotation(
                followTarget.position - transform.position,
                followTarget.up
            );

            if (disableRotationLerp || Mathf.Abs(lat) > Mathf.Abs(fwd))
                transform.rotation = targetRot;
            else
            {
                float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            _lastTargetPos = followTarget.position;
        }

        /// <summary>
        /// Pull configuration from a CameraSettingsSO.
        /// </summary>
        public void ApplySettings(CameraSettingsSO settings)
        {
            currentSettings = settings;
            if (currentSettings == null) return;

            // Common follow/rotation/update
            followOffset         = currentSettings.followOffset;
            followSmoothTime     = currentSettings.followSmoothTime;
            rotationSmoothTime   = currentSettings.rotationSmoothTime;
            disableRotationLerp  = currentSettings.disableRotationLerp;
            useFixedUpdate       = currentSettings.useFixedUpdate;

            // Frustum clip planes
            cachedCamera.nearClipPlane = currentSettings.nearClipPlane;
            cachedCamera.farClipPlane  = currentSettings.farClipPlane;
        }

        /// <summary>
        /// Set which Transform to follow.
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget   = target;
            _lastTargetPos = Vector3.zero;
        }

        /// <summary>
        /// Ensures settings re-apply when enabling.
        /// </summary>
        public void Activate()
        {
            gameObject.SetActive(true);
            if (currentSettings != null)
            {
                cachedCamera.nearClipPlane = currentSettings.nearClipPlane;
                cachedCamera.farClipPlane  = currentSettings.farClipPlane;
            }
        }

        /// <summary>
        /// Simply disable the GameObject.
        /// </summary>
        public void Deactivate() => gameObject.SetActive(false);

        //Legacy API for CameraManager
        /// <summary>Expose underlying Camera for CameraManager (e.g. vCam).</summary>
        public Camera Camera => cachedCamera;

        /// <summary>Allow CameraManager to tweak the follow offset at runtime.</summary>
        public void SetFollowOffset(Vector3 offset) => followOffset = offset;

        /// <summary>Allow CameraManager to read the original offset.</summary>
        public Vector3 GetFollowOffset() => followOffset;

        /// <summary>Called by CameraManager.Orthographic(...)</summary>
        public void SetOrthographic(bool ortho, float size)
        {
            cachedCamera.orthographic = ortho;
            if (ortho) cachedCamera.orthographicSize = size;
        }
    }
}
