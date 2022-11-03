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

        ShipData shipData;

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
            lerp += lerpIncrease;
            throttle += throttleIncrease;

            //Move ship forward
            Vector3 flowVector = flowFieldData.FlowVector(transform);

            //Vector3 flowVector = flowFieldData.FlowVector(transform);
            transform.position += transform.forward * Time.deltaTime * throttle + flowVector;
            speed = throttle;
            shipData.speed = speed;
        }
    }
}
