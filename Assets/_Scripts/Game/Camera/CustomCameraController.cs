using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour, ICameraController
    {
        // --- Target and Offset ---
        private Transform followTarget;
        private Vector3 followOffset = new(0f, 10f, 0f); // Z=0 by default; distance via SetCameraDistance

        // --- Smoothing and Update Control ---
        private float followSmoothTime = 0.2f;
        private float rotationSmoothTime = 5f;
        private bool disableRotationLerp = false; 
        private bool useFixedUpdate = false;

        // --- Internal State ---
        private Camera cachedCamera;
        private Vector3 velocity;
        private Vector3 _lastTargetPos;
        private CameraSettingsSO currentSettings;
        private Coroutine distanceLerpRoutine;

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

            Vector3 desiredPos = followTarget.position + followTarget.rotation * followOffset;
            Vector3 shipDelta = followTarget.position - _lastTargetPos;
            float fwd = Vector3.Dot(shipDelta, followTarget.forward);
            float lat = Vector3.Dot(shipDelta, followTarget.right);

            if (disableRotationLerp)
            {
                // Hard snap the camera position
                transform.position = desiredPos;
                velocity = Vector3.zero;
            }
            else
            {
                // Old logic : Only viable for manta if required in future
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
            }

            // --- ROTATION ---
            Quaternion targetRot = Quaternion.LookRotation(
                followTarget.position - transform.position,
                followTarget.up
            );

            if (disableRotationLerp || Mathf.Abs(lat) > Mathf.Abs(fwd))
            {
                // Hard snap rotation
                transform.rotation = targetRot;
            }
            else
            {
                float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
            }

            _lastTargetPos = followTarget.position;
        }

        public void ApplySettings(CameraSettingsSO settings)
        {
            currentSettings = settings;
            if (currentSettings == null) return;

            // Only copy X/Y from SO; Z is controlled by SetCameraDistance()
            followOffset.x = currentSettings.followOffset.x;
            followOffset.y = currentSettings.followOffset.y;

            followSmoothTime    = currentSettings.followSmoothTime;
            rotationSmoothTime  = currentSettings.rotationSmoothTime;
            disableRotationLerp = currentSettings.disableRotationLerp;
            useFixedUpdate      = currentSettings.useFixedUpdate;
            cachedCamera.nearClipPlane = currentSettings.nearClipPlane;
            cachedCamera.farClipPlane  = currentSettings.farClipPlane;

            // Immediately apply the proper distance
            if (currentSettings.controlOverrides.HasFlag(ControlOverrideFlags.FarCam))
                SetCameraDistance(currentSettings.farCamDistance);
            else
                SetCameraDistance(currentSettings.closeCamDistance);
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            _lastTargetPos = Vector3.zero;
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            if (currentSettings != null)
            {
                cachedCamera.nearClipPlane = currentSettings.nearClipPlane;
                cachedCamera.farClipPlane  = currentSettings.farClipPlane;
            }
        }

        public void Deactivate() => gameObject.SetActive(false);

        public Camera Camera => cachedCamera;

        /// <summary>
        /// Sets the distance (Z) behind the target. Always negative.
        /// </summary>
        public void SetCameraDistance(float distance)
        {
            if (distanceLerpRoutine != null)
            {
                StopCoroutine(distanceLerpRoutine);
                distanceLerpRoutine = null;
            }
            followOffset.z = -Mathf.Abs(distance);
        }

        /// <summary>
        /// Gets the current distance (absolute value).
        /// </summary>
        public float GetCameraDistance() => Mathf.Abs(followOffset.z);

        /// <summary>
        /// Smoothly lerps the distance (Z) over the given duration.
        /// </summary>
        public void LerpCameraDistance(float start, float end, float duration)
        {
            if (distanceLerpRoutine != null)
                StopCoroutine(distanceLerpRoutine);
            distanceLerpRoutine = StartCoroutine(LerpCameraDistanceRoutine(start, end, duration));
        }
        private System.Collections.IEnumerator LerpCameraDistanceRoutine(float start, float end, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                float z = Mathf.Lerp(start, end, t / duration);
                followOffset.z = -Mathf.Abs(z);
                t += Time.deltaTime;
                yield return null;
            }
            followOffset.z = -Mathf.Abs(end);
        }

        /// <summary>
        /// Rarely used override to set full offset directly.
        /// </summary>
        public void SetFollowOffset(Vector3 offset)
        {
            followOffset = offset;
        }

        /// <summary>
        /// Returns the current full offset vector.
        /// </summary>
        public Vector3 GetFollowOffset() => followOffset;

        /// <summary>
        /// Switches to orthographic view if requested.
        /// </summary>
        public void SetOrthographic(bool ortho, float size)
        {
            cachedCamera.orthographic = ortho;
            if (ortho) cachedCamera.orthographicSize = size;
        }
    }
}
