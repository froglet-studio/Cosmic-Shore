using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] Vector3 followOffset = new Vector3(0f, 0f, -8f);
        [SerializeField] float followSmoothTime = 0.2f;
        [SerializeField] float rotationSmoothTime = 5f;
        [SerializeField] bool useFixedUpdate = false;
        [SerializeField] bool ignoreRoll = true;

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

            Quaternion offsetRotation = followTarget.rotation;
            if (ignoreRoll)
            {
                offsetRotation = Quaternion.LookRotation(followTarget.forward, Vector3.up);
            }

            Vector3 desired = followTarget.position + offsetRotation * followOffset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, followSmoothTime);
            Quaternion lookRot = Quaternion.LookRotation(followTarget.position - transform.position, Vector3.up);
            float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, t);
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
