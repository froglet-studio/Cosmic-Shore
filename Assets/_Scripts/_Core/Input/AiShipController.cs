using UnityEngine;
using TMPro;
using Cinemachine;

namespace StarWriter.Core.Input
{
    public class AiShipController : MonoBehaviour
    {
        public float defaultThrottle = .6f;
        public float defaultLerp = .035f;

        public float throttleIncrease = .001f;
        public float lerpIncrease = .001f;

        public float throttle;
        public float lerp;

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;



        ShipData shipData;

        RaycastHit hit;
        float maxDistance = 20f;

        //RaycastHit hit;

        [SerializeField] public Transform muton;
        Vector3 distance;

        public float speed;
        [SerializeField] FlowFieldData flowFieldData;

        // Start is called before the first frame update
        void Start()
        {
            lerp = defaultLerp;
            throttle = defaultThrottle;
            shipData = GetComponent<ShipData>();
        }

        // Update is called once per frame
        void Update()
        {
            distance = muton.position - transform.position;
            //throttle = Mathf.Lerp(throttle, defaultThrottle, .1f);

            transform.localRotation = Quaternion.Lerp(transform.localRotation,
                                                         Quaternion.LookRotation(distance, transform.up),
                                                         lerp/distance.magnitude);


            ShootLaser(new Vector3(raycastWidth, raycastHeight, 0));
            ShootLaser(new Vector3(-raycastWidth, raycastHeight, 0));
            ShootLaser(new Vector3(raycastWidth, -raycastHeight, 0));
            ShootLaser(new Vector3(-raycastWidth, -raycastHeight, 0));

            lerp += lerpIncrease;
            throttle += throttleIncrease;

            //Move ship forward
            Vector3 flowVector = flowFieldData.FlowVector(transform);

            //Vector3 flowVector = flowFieldData.FlowVector(transform);
            transform.position += transform.forward * Time.deltaTime * throttle + flowVector;
            speed = throttle;
            shipData.speed = speed;
        }

        void ShootLaser(Vector3 position)
        {
            if (Physics.Raycast(transform.position + position, transform.forward, out hit, maxDistance))

            {
                Debug.DrawLine(transform.position + position, hit.point, Color.red);
            }
            else
            {
                Debug.DrawLine(transform.position + position, (transform.position + position) + transform.forward * maxDistance, Color.green);
            }
        }

    }
}
