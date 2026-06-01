using UnityEngine;

namespace CosmicShore.Splats
{
    // Minimal WASD + mouse-look fly camera. Lives only in the splat sandbox scene so the
    // splat cloud can be inspected without depending on the gameplay vessel stack. Hold
    // right mouse to rotate. Shift to fly fast, Ctrl to fly slow. Space/Q to ascend/descend.
    [DisallowMultipleComponent]
    public class SplatSandboxFlyCam : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float baseSpeed = 25f;
        [SerializeField, Min(0.01f)] private float fastMultiplier = 4f;
        [SerializeField, Min(0.01f)] private float slowMultiplier = 0.25f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 3f;
        [SerializeField] private bool holdRightMouseToLook = true;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            var euler = transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
        }

        private void Update()
        {
            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            if (holdRightMouseToLook && !Input.GetMouseButton(1)) return;

            float mx = Input.GetAxis("Mouse X") * lookSensitivity;
            float my = Input.GetAxis("Mouse Y") * lookSensitivity;
            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMove()
        {
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            if (move.sqrMagnitude < 1e-6f) return;
            move = move.normalized;

            float speed = baseSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= fastMultiplier;
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) speed *= slowMultiplier;

            transform.position += move * speed * Time.deltaTime;
        }
    }
}
