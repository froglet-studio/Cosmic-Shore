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

        public Transform shipTransform;
        [SerializeField] public Transform muton;
        Vector3 distance;

        public float speed;
        [SerializeField] FlowFieldData flowFieldData;

        // Start is called before the first frame update
        void Start()
        {
            
            lerp = defaultLerp;
            throttle = defaultThrottle;
            
        }

        // Update is called once per frame
        void Update()
        {
            distance = muton.position - shipTransform.position;
            //throttle = Mathf.Lerp(throttle, defaultThrottle, .1f);

            shipTransform.localRotation = Quaternion.Lerp(shipTransform.localRotation,
                                                         Quaternion.LookRotation(distance, shipTransform.up),
                                                         lerp/distance.magnitude);
            lerp += lerpIncrease;
            throttle += throttleIncrease;
            

            //Move ship forward
            Vector3 flowVector = flowFieldData.FlowVector(transform);

            //Vector3 flowVector = flowFieldData.FlowVector(transform);
            shipTransform.position += shipTransform.forward * Time.deltaTime * throttle + flowVector;
            speed = throttle;
        }

    }
}
