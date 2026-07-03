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
            var vessel = shipInstance as IVesselStatus;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                Vector3 direction = (child.position - vessel.Transform.position).normalized;
                child.position = vessel.Transform.position + direction * radius;
            }
        }

        void Update()
        {
            Vector2 rightStick = (shipInstance as IVesselStatus).InputStatus.RightNormalizedJoystickPosition;

            Vector3 targetFocus = new Vector3(0, 0, 300f * rightStick.sqrMagnitude + 70f);
            gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, targetFocus, Time.deltaTime * speed);

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.RotateAround(pivotObject.transform.position, pivotObject.transform.forward, rotationSpeed * Time.deltaTime);
                child.LookAt(gunFocus);
            }
        }
    }
}