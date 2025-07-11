using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] Vector3 followOffset = new(0f, 10f, -50f);
        [SerializeField] float followSmoothTime = 0.2f;
        [SerializeField] float rotationSmoothTime = 5f;
        [SerializeField] bool useFixedUpdate = false;
        [SerializeField] bool ignoreRoll = false;

        Camera cachedCamera;
        Vector3 velocity;

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

        public bool IgnoreRoll
        {
            get => ignoreRoll;
            set => ignoreRoll = value;
        }

        void Awake()
        {
            cachedCamera = GetComponent<Camera>();
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

            // 1) Build an offset?rotation that follows the ship’s forward+pitch but ignores its roll
            Quaternion offsetRot = ignoreRoll
                ? Quaternion.LookRotation(followTarget.forward, Vector3.up)
                : followTarget.rotation;

            // 2) Move the camera to ship.position + that rotated offset
            Vector3 desiredPos = followTarget.position + offsetRot * followOffset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, followSmoothTime);

            // 3) Look at the ship (world-up so no roll)
            Vector3 toTarget = followTarget.position - transform.position;
            Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);

            // 4) Smooth?damp into that rotation
            float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }

        public void SetFollowTarget(Transform target) => followTarget = target;
        public void SetFollowOffset(Vector3 offset)
        {
            followOffset = offset;
        }
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
