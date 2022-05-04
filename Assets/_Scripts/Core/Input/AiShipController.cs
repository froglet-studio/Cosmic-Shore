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

        public Transform shipTransform;
        [SerializeField]
        public Transform muton;


        Quaternion displacementQ;


        // Start is called before the first frame update
        void Start()
        {
            float throttle = defaultThrottle;
        }

        // Update is called once per frame
        void Update()
        {

            throttle = Mathf.Lerp(throttle, defaultThrottle, .1f);
            shipTransform.localRotation = Quaternion.Lerp(shipTransform.localRotation,
                                                         Quaternion.LookRotation(muton.position - shipTransform.position, shipTransform.up),
                                                         lerpAmount);


            //Move ship forward
            shipTransform.position += shipTransform.forward * throttle;

        }

    }
}
