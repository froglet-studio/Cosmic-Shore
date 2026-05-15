using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class GunRingTransformer : MonoBehaviour
    {
        [SerializeField] VesselStatus shipInstance;
        [SerializeField] Transform gunFocus;
        [SerializeField] GameObject pivotObject;

        [SerializeField] private float radius = 20.0f;
        [SerializeField] private float rotationSpeed = 20.0f;
        [SerializeField] private float speed = 10.0f;

        void Start()
        {
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child == transform) continue;

                Vector3 direction = (child.position - shipInstance.transform.position).normalized;
                child.position = shipInstance.transform.position + direction * radius;
            }
        }

        void Update()
        {
            // TODO: replace with InputController read once BrittleStar input is wired
            Vector2 rightStick = UnityEngine.InputSystem.Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;

            Vector3 targetFocus = new Vector3(0, 0, 300f * rightStick.sqrMagnitude + 70f);
            gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, targetFocus, Time.deltaTime * speed);

            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child == transform) continue;

                child.RotateAround(pivotObject.transform.position, pivotObject.transform.forward, rotationSpeed * Time.deltaTime);
                child.LookAt(gunFocus);
            }
        }
    }
}