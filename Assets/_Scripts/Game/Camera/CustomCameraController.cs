using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class CustomCameraController : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] Vector3 followOffset = new Vector3(0f, 0f, -8f);
        [SerializeField] float followSmoothTime = 0.2f;
        [SerializeField] float rotationSmoothTime = 0.2f;
        [SerializeField] bool useFixedUpdate = false;

        Camera cachedCamera;
        Vector3 velocity;
        Quaternion rotationVelocity;

        public Camera Camera => cachedCamera;

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

            Vector3 desired = followTarget.position + followTarget.rotation * followOffset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, followSmoothTime);
            Quaternion lookRot = Quaternion.LookRotation(followTarget.position - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSmoothTime);
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
