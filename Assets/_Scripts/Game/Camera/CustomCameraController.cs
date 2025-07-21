using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour
    {
        [Header("Follow Settings")] [SerializeField]
        private Transform followTarget;

        [SerializeField] private Vector3 followOffset = new Vector3(0f, 10f, -20f);
        [SerializeField] private float followSmoothTime = 0.2f;

        [Header("Rotation Settings")] [SerializeField]
        private float rotationSmoothTime = 5f;

        [Tooltip("If true, disables rotation smoothing entirely (instant look)")]
        public bool disableRotationLerp = false;

        [Header("Camera Settings")] [SerializeField]
        private bool useFixedUpdate = false;

        [SerializeField] private float farClipPlane = 10000f;
        [SerializeField] private float fieldOfView = 60f;

        private Camera cachedCamera;
        private Vector3 velocity;
        private Vector3 _lastTargetPos;

        public Camera Camera => cachedCamera;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        public float FollowSmoothTime
        {
            get => followSmoothTime;
            set => followSmoothTime = Mathf.Max(0f, value);
        }

        public float RotationSmoothTime
        {
            get => rotationSmoothTime;
            set => rotationSmoothTime = Mathf.Max(0f, value);
        }

        void Awake()
        {
            cachedCamera = GetComponent<Camera>();
            cachedCamera.fieldOfView = fieldOfView;
            cachedCamera.useOcclusionCulling = false;
            cachedCamera.farClipPlane = farClipPlane;
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

        void UpdateCamera()
        {
            if (followTarget == null)
                return;

            // Initialize last-frame position
            if (_lastTargetPos == Vector3.zero)
                _lastTargetPos = followTarget.position;

            //  Compute desired world pos
            Quaternion offsetRot = followTarget.rotation;
            Vector3 desiredPos = followTarget.position + offsetRot * followOffset;

            //  Detect how the ship moved this frame (in local space)
            Vector3 shipDelta = followTarget.position - _lastTargetPos;
            float fwdMove = Vector3.Dot(shipDelta, followTarget.forward);
            float latMove = Vector3.Dot(shipDelta, followTarget.right);

            // Position: smooth forward/back, snap sideways
            if (Mathf.Abs(latMove) > Mathf.Abs(fwdMove))
            {
                transform.position = desiredPos;
                velocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPos,
                    ref velocity,
                    followSmoothTime
                );
            }

            Vector3 toTarget = followTarget.position - transform.position;
            Quaternion targetRot = Quaternion.LookRotation(toTarget, followTarget.up);

            if (disableRotationLerp || Mathf.Abs(latMove) > Mathf.Abs(fwdMove))
            {
                // Instant look
                transform.rotation = targetRot;
            }
            else
            {
                // Smoothed look
                float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    t
                );
            }

            // 5. Store for next frame
            _lastTargetPos = followTarget.position;
        }

        // --- Utility setters/getters ---
        public void SetFollowTarget(Transform target) => followTarget = target;
        public void SetFollowOffset(Vector3 offset) => followOffset = offset;
        public Vector3 GetFollowOffset() => followOffset;

        public void SetFieldOfView(float fov) => cachedCamera.fieldOfView = fov;

        public void SetClipPlanes(float near, float far)
        {
            cachedCamera.nearClipPlane = near;
            cachedCamera.farClipPlane = far;
        }

        public void SetOrthographic(bool ortho, float size)
        {
            cachedCamera.orthographic = ortho;
            if (ortho)
                cachedCamera.orthographicSize = size;
        }
    }
}