using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class GunRingTransformer : MonoBehaviour
    {
        [RequireInterface(typeof(IVesselStatus))]
        [SerializeField] MonoBehaviour shipInstance;
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

                var vessel = shipInstance as IVesselStatus;
                Vector3 direction = (child.position - vessel.Transform.position).normalized;
                child.position = vessel.Transform.position + direction * radius;
            }
        }

        void Update()
        {
            Vector2 rightStick = (shipInstance as IVesselStatus).InputStatus.RightNormalizedJoystickPosition;

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