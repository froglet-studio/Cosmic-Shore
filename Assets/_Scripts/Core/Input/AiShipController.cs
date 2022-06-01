using UnityEngine;
using TMPro;
using Cinemachine;

namespace StarWriter.Core.Input
{
    public class AiShipController : MonoBehaviour
    {

       
        private float throttle;

        public float defaultThrottle = .6f;
        public float lerpAmount = .035f;
        public float lerpIncrease = .001f;

        public Transform shipTransform;
        [SerializeField]
        public Transform muton;
        Vector3 distance;

        public float speed;

        // Start is called before the first frame update
        void Start()
        {
            float throttle = defaultThrottle;
        }

        // Update is called once per frame
        void Update()
        {
            distance = muton.position - shipTransform.position;
            throttle = Mathf.Lerp(throttle, defaultThrottle, .1f);

            shipTransform.localRotation = Quaternion.Lerp(shipTransform.localRotation,
                                                         Quaternion.LookRotation(distance, shipTransform.up),
                                                         lerpAmount/distance.magnitude);
            lerpAmount += lerpIncrease;

            //Move ship forward
            shipTransform.position += shipTransform.forward *Time.deltaTime* throttle;
            speed = throttle;
        }

    }
}
